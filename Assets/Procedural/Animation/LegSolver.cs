// Procedural leg solver — the legs push the hips around, spending energy to do it
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class Joint
{
    public Rigidbody rb;
    public Rigidbody parentRb;      // bone this one hangs off of, takes the reaction torque
    public Transform bone;          // the physical bone transform (rb's transform)
    public Transform poseTarget;    // matching bone on the ghost rig, chased with torque
    public Vector3 restEuler;       // pose captured at startup, held when there is no ghost bone
    public float length;    // length of limb
    public Vector3 weight;   // force applied to joint externally.
    // the max rotation angles of the joint. zero range means the axis is free.
    public Vector2 maxX;
    public Vector2 maxY;
    public Vector2 maxZ;
    public float damage = 0; // stays between 1 and 0 for fine and destroyed
    public float mass;
    public float strength = 1f;

    public float Capacity()
    {
        return strength * (1f - damage); // how much of this joint still works
    }
}

[System.Serializable]
public class Leg
{
    public Transform root; // top of the leg chain, every rigidbody under it becomes a joint
    public Transform foot; // the bone that plants on the ground. needs a rigidbody.
    public int footIndex = -1;  // which joint is the foot
    public List<Joint> joints = new List<Joint>();
    public Collider footCollider;   // the foot's own collider, so contact casts scale with the rig
    public float reach;             // hip to sole, summed at startup. step distances are fractions of this.
    public bool contact;
    public float loadShare; // fraction of the body's weight this leg carries, 0 when not planted
    public Vector3 push;    // general direction of force applied, solve propagates through joints.
    public Vector2 distanceToHips; // x is start of far range, y is max range. produces a gradient of 0 to 1 of desire to adjust leg.
    public float adjustDesirability = 0;    // 0 to 1 gradient from distance to hips.
    public Vector3 homeOffset;      // where the foot likes to sit, relative to the hips
    public Vector3 plantedPoint;    // where the foot is currently holding the ground
    // stepping state
    public bool stepping;
    public float stepTime;
    public Vector3 stepFrom;
    public Vector3 stepTo;

    public Joint Foot()
    {
        return joints[footIndex];
    }
}

public class LegSolver : MonoBehaviour
{
    public Joint hips;
    // forces and torques carry different units and, on a scaled rig, wildly different magnitudes.
    // one shared cap cannot serve both: holding this walker up wants tens of kN, swinging a leg
    // from the hip wants hundreds of kN·m.
    public float maxForce = 60000f;     // force cap per leg/joint, scaled by capacity
    public float maxTorque = 600000f;   // torque cap per joint, scaled by capacity
    public bool useEnergyBudget = false;    // off until the legs walk. demand is still tallied for the inspector.
    public float maxEnergyRate = 4000f; // max energy output per second, all forces scale down past this
    public float energyConsumeRate;     // energy being spent right now, goal is to keep this low
    public Transform hipTarget;
    public float hipWeight;             // extra load carried at the hips
    public Vector3 hipForce;
    public List<Leg> legs = new List<Leg>();
    public Transform poseRoot;          // ghost rig playing animations, joints chase its bones. optional.

    [Header("Balance")]
    public float hipPosGain = 20f;      // spring pulling hips to the hip target
    public float hipDamp = 9f;          // hip velocity damping, 2*sqrt(hipPosGain) is critical
    public float hipRotGain = 20f;      // torque spring toward hip target rotation
    public float hipRotDamp = 5f;       // hip angular velocity damping
    public float balanceGain = 2f;      // push to keep center of mass over the feet
    // how far ahead the balance controller looks. 1 is the physical capture point, higher reacts
    // sooner and firmer, 0 falls back to steering on com position alone.
    public float captureLead = 1f;
    public float footHoldGain = 40f;    // spring holding planted feet in place
    public float footHoldDamp = 5f;     // planted foot velocity damping

    [Header("Stepping")]
    public LayerMask groundMask = ~0;
    // contact is measured from the sole of the foot collider, so it works at any rig scale.
    // contactRayLength is only the fallback for a foot with no collider on it.
    public float contactMargin = 0.5f;      // how far below the sole still counts as contact
    public float contactRayLength = 0.3f;   // fallback reach when the foot has no collider
    public float stepThreshold = 0.5f;      // desirability needed before a leg steps
    public float stepDuration = 0.25f;
    public float stepLead = 0.2f;       // seconds of hip velocity to lead foot placement by
    public float stepGain = 40f;        // spring pulling the foot along the step arc
    public float stepDamp = 5f;         // stepping foot velocity damping
    public int minPlantedLegs = 1;      // never step below this many planted legs
    // both are fractions of a leg's reach, so retuning the rig's scale does not retune the gait
    public float stepHeight = 0.1f;     // peak of the step arc
    public Vector2 stepRange = new Vector2(0.4f, 0.8f);   // default distanceToHips given to each leg

    [Header("Pose Matching")]
    public float poseGain = 2000f;  // torque spring toward the ghost pose, in rad/s^2 per rad of error
    public float poseDamp = 90f;    // joint angular velocity damping, roughly 2*sqrt(poseGain) is critical

    public List<Leg> builtLegs = new List<Leg>();

    private float totalMass;
    private float energyDemand; // force asked for this frame, before the budget clamps it
    private float energyScale = 1f; // this frame's budget scale, from last frame's demand
    private readonly HashSet<Rigidbody> ownBodies = new HashSet<Rigidbody>();   // every body we drive
    // RaycastNonAlloc truncates at capacity and does not sort, so leave room for the whole walker
    private readonly RaycastHit[] rayHits = new RaycastHit[16];


    void Awake()
    {
        if (hips.rb != null)
        {
            hips.bone = hips.rb.transform;
            hips.mass = hips.rb.mass;
        }

        // collect every rigidbody under each leg root into joints. branches and non-physical bones are fine.
        builtLegs.Clear();
        foreach (Leg leg in legs)
        {
            if (leg.root == null || leg.foot == null) continue;

            // these persist through serialization, so a rebuild has to start from empty
            leg.joints.Clear();
            leg.footIndex = -1;

            foreach (Rigidbody rb in leg.root.GetComponentsInChildren<Rigidbody>())
            {
                Joint joint = new Joint();
                joint.rb = rb;
                joint.bone = rb.transform;
                joint.mass = rb.mass;
                joint.restEuler = rb.transform.localRotation.eulerAngles;
                if (poseRoot != null) joint.poseTarget = FindPoseBone(rb.name);
                ReadJointLimits(joint);

                // limb length is measured to the bone this one hangs off of, whatever sits between them
                Rigidbody parentRb = rb.transform.parent != null ? rb.transform.parent.GetComponentInParent<Rigidbody>() : null;
                joint.parentRb = parentRb;
                foreach (Joint parent in leg.joints)
                {
                    if (parent.rb == parentRb) parent.length = Vector3.Distance(parent.bone.position, rb.position);
                }

                if (rb.transform == leg.foot) leg.footIndex = leg.joints.Count;
                leg.joints.Add(joint);
            }

            if (leg.footIndex < 0)
            {
                Debug.LogWarning($"Leg {leg.root.name} has no rigidbody on its foot {leg.foot.name}, skipping it.");
                continue;
            }

            leg.footCollider = leg.Foot().rb.GetComponentInChildren<Collider>();
            leg.reach = MeasureReach(leg);
            leg.distanceToHips = stepRange * leg.reach;

            leg.homeOffset = hips.bone.InverseTransformPoint(leg.Foot().bone.position);
            leg.plantedPoint = leg.Foot().bone.position;
            builtLegs.Add(leg);
        }

        totalMass = hips.mass;
        ownBodies.Clear();
        if (hips.rb != null) ownBodies.Add(hips.rb);
        foreach (Leg leg in builtLegs)
        {
            foreach (Joint joint in leg.joints)
            {
                totalMass += joint.mass;
                ownBodies.Add(joint.rb);    // so the ground raycasts can tell us apart from the world
            }
        }
    }

    // how far this leg can reach, so the gait constants can be fractions of the rig instead of
    // magic numbers that only hold at one scale. walks the foot's own chain up to the root rather
    // than summing every joint, so a leg with branches on it does not measure long.
    float MeasureReach(Leg leg)
    {
        float reach = leg.footCollider != null ? leg.footCollider.bounds.extents.y : 0f;

        Joint joint = leg.Foot();
        for (int guard = 0; guard < leg.joints.Count && joint != null; guard++)
        {
            Joint parent = null;
            foreach (Joint candidate in leg.joints)
            {
                if (candidate.rb == joint.parentRb) { parent = candidate; break; }
            }
            if (parent == null) break;  // reached the top of this leg

            reach += parent.length;
            joint = parent;
        }

        return reach > 0f ? reach : 1f; // single-bone leg, fall back to raw units
    }

    // mirror the bone's ConfigurableJoint limits into our own, so ClampToLimits aims at a pose
    // physx will actually allow. without this the solver spends its whole budget torquing toward
    // angles the constraint solver refuses, and never learns why the bone did not move.
    // assumes the joint frame matches the bone's, ie axis is X and secondaryAxis is Y — which is
    // how these rigs are built. a joint on a tilted frame would need its limits rotated to match.
    void ReadJointLimits(Joint joint)
    {
        ConfigurableJoint cj = joint.rb.GetComponent<ConfigurableJoint>();
        if (cj == null) return;

        // Awake rebuilds every Joint from scratch, so these always start free. the guard is here so
        // limits survive if joints ever get authored or cached instead of rebuilt.
        if (IsFree(joint.maxX)) joint.maxX = AngularRange(cj.angularXMotion, cj.lowAngularXLimit.limit, cj.highAngularXLimit.limit);
        if (IsFree(joint.maxY)) joint.maxY = AngularRange(cj.angularYMotion, -cj.angularYLimit.limit, cj.angularYLimit.limit);
        if (IsFree(joint.maxZ)) joint.maxZ = AngularRange(cj.angularZMotion, -cj.angularZLimit.limit, cj.angularZLimit.limit);
    }

    Vector2 AngularRange(ConfigurableJointMotion motion, float low, float high)
    {
        // a locked axis needs a range that is tiny but not zero, or IsFree reads it as unlimited
        if (motion == ConfigurableJointMotion.Locked) return new Vector2(-0.01f, 0.01f);
        if (motion == ConfigurableJointMotion.Free) return Vector2.zero;
        // a limited axis authored as 0..0 is locked in all but name, so give it the same treatment
        if (low == 0f && high == 0f) return new Vector2(-0.01f, 0.01f);
        return new Vector2(low, high);
    }

    void FixedUpdate()
    {
        if (hips.rb == null || hipTarget == null) return;

        // last frame's demand sets this frame's budget scale. one frame behind, close enough at 50hz.
        // demand is always tallied so the inspector shows what the legs are asking for, but until
        // they actually walk the budget does not throttle anything.
        energyConsumeRate = useEnergyBudget ? Mathf.Min(energyDemand, maxEnergyRate) : energyDemand;
        energyScale = useEnergyBudget && energyDemand > maxEnergyRate ? maxEnergyRate / energyDemand : 1f;
        energyDemand = 0f;

        UpdateContacts();
        UpdateHipForce();

        foreach (Leg leg in builtLegs)
        {
            if (leg.contact && !leg.stepping) {
                UpdateLegForce(leg);
                Debug.DrawRay(leg.Foot().bone.position, leg.push, Color.white);
            }
        }

        UpdateHipsRotation();
        UpdateStepping();
        UpdatePoseTargets();
        ApplyWeights();

        Debug.DrawRay(GetCenterOfMass(), Vector3.up * 0.5f, Color.magenta);
    }

    public Vector3 GetCenterOfMass()
    {
        Vector3 center = Vector3.zero;
        float mass = 0f;
        if (hips.rb != null)
        {
            center += hips.rb.worldCenterOfMass * hips.mass;
            mass += hips.mass;
        }
        foreach (Leg leg in builtLegs)
        {
            foreach (Joint joint in leg.joints)
            {
                center += joint.rb.worldCenterOfMass * joint.mass;
                mass += joint.mass;
            }
        }
        return mass > 0f ? center / mass : transform.position;
    }

    public Leg GetMostMovableLeg()
    {
        Leg best = null;
        foreach (Leg leg in builtLegs)
        {
            if (leg.stepping || !leg.contact) continue;
            if (best == null || leg.adjustDesirability > best.adjustDesirability) best = leg;
        }
        return best;
    }

    void UpdateContacts()
    {
        foreach (Leg leg in builtLegs)
        {
            Rigidbody foot = leg.Foot().rb;
            bool wasContact = leg.contact;

            // cast from the middle of the foot down past its sole. the bone pivot sits at the ankle,
            // which on a scaled rig can be a long way above the ground the foot is already standing on.
            Vector3 origin = foot.position;
            float reach = contactRayLength;
            if (leg.footCollider != null)
            {
                Bounds bounds = leg.footCollider.bounds;
                origin = bounds.center;
                reach = bounds.extents.y + contactMargin;
            }

            RaycastHit hit;
            leg.contact = GroundCast(origin, reach, out hit);
            if (leg.contact && !wasContact) leg.plantedPoint = hit.point; // just landed, grab the ground here
            Debug.DrawRay(origin, Vector3.down * reach, leg.contact ? Color.green : Color.red);
        }
    }

    void UpdateHipForce()
    {
        // spring toward the hip target, damped, plus holding up the whole body's weight
        Vector3 spring = (hipTarget.position - hips.rb.position) * hipPosGain - hips.rb.linearVelocity * hipDamp;
        Vector3 support = -Physics.gravity * totalMass + Vector3.down * hipWeight;

        // nudge the hips so the center of mass stays over the planted feet
        Vector3 supportCenter = Vector3.zero;
        int planted = PlantedCount();
        if (planted > 0)
        {
            foreach (Leg leg in builtLegs)
            {
                if (leg.contact && !leg.stepping) supportCenter += leg.plantedPoint;
            }
            supportCenter /= planted;
        }
        Vector3 balance = supportCenter - GetCenterOfMass();
        balance.y = 0f;

        // the spring has to move the whole body, not just the hip bone
        hipForce = spring * totalMass + support + (planted > 0 ? balance * balanceGain * totalMass : Vector3.zero);
    }

    public void UpdateLegForce(Leg leg)
    {
        // this leg carries its share of the hip force, pushing off its planted point
        Vector3 share = hipForce / Mathf.Max(1, PlantedCount());
        share = Vector3.ClampMagnitude(share, maxForce * LegCapacity(leg));
        leg.push = share.normalized;
        share = Budget(share);
        hips.rb.AddForce(share);
        leg.Foot().rb.AddForce(-share); // ground reaction, the foot pushes down so the hips go up

        // keep the planted foot from sliding out
        Rigidbody foot = leg.Foot().rb;
        Vector3 hold = ((leg.plantedPoint - foot.position) * footHoldGain - foot.linearVelocity * footHoldDamp) * foot.mass;
        hold = Vector3.ClampMagnitude(hold, maxForce * leg.Foot().Capacity());
        foot.AddForce(Budget(hold));

        Debug.DrawRay(leg.plantedPoint, share * 0.002f, Color.cyan);
    }

    public void UpdateHipsRotation()
    {
        // torque the hips toward the hip target's rotation
        Quaternion delta = hipTarget.rotation * Quaternion.Inverse(hips.rb.rotation);
        float angle;
        Vector3 axis;
        delta.ToAngleAxis(out angle, out axis);
        if (angle > 180f) angle -= 360f;
        if (float.IsInfinity(axis.x)) return; // already aligned

        Vector3 torque = axis.normalized * (angle * Mathf.Deg2Rad * hipRotGain) - hips.rb.angularVelocity * hipRotDamp;
        torque = Vector3.ClampMagnitude(torque, maxTorque * hips.Capacity());
        hips.rb.AddTorque(Budget(torque));
    }

    void UpdateStepping()
    {
        // desirability climbs as a foot drifts out of comfortable range of the hips
        foreach (Leg leg in builtLegs)
        {
            Vector3 flat = leg.Foot().rb.position - hips.rb.position;
            flat.y = 0f;
            leg.adjustDesirability = Mathf.InverseLerp(leg.distanceToHips.x, leg.distanceToHips.y, flat.magnitude);
        }

        // let the leg that wants it most step, as long as enough legs stay planted
        Leg mover = GetMostMovableLeg();
        if (mover != null && mover.adjustDesirability > stepThreshold && PlantedCount() > minPlantedLegs)
        {
            StartStep(mover);
        }

        // pull stepping feet along their arc
        foreach (Leg leg in builtLegs)
        {
            if (!leg.stepping) continue;
            leg.stepTime += Time.fixedDeltaTime;
            float t = Mathf.Clamp01(leg.stepTime / stepDuration);
            Vector3 point = Vector3.Lerp(leg.stepFrom, leg.stepTo, t);
            point.y += Mathf.Sin(t * Mathf.PI) * stepHeight * leg.reach; // arc the foot up and over

            Rigidbody foot = leg.Foot().rb;
            Vector3 force = ((point - foot.position) * stepGain - foot.linearVelocity * stepDamp) * foot.mass;
            force = Vector3.ClampMagnitude(force, maxForce * LegCapacity(leg));
            foot.AddForce(Budget(force));
            Debug.DrawLine(foot.position, leg.stepTo, Color.yellow);

            if (t >= 1f)
            {
                leg.stepping = false;
                leg.plantedPoint = leg.stepTo;
            }
        }
    }

    void StartStep(Leg leg)
    {
        leg.stepping = true;
        leg.stepTime = 0f;
        leg.stepFrom = leg.Foot().rb.position;

        // land under the foot's home spot, led ahead by where the hips are going
        Vector3 target = hips.bone.TransformPoint(leg.homeOffset) + hips.rb.linearVelocity * stepLead;
        RaycastHit hit;
        if (GroundCast(target + Vector3.up, 2f, out hit))
        {
            target = hit.point;
        }
        leg.stepTo = target;
    }

    void UpdatePoseTargets()
    {
        // chase the ghost rig's pose with torque. the animation is a suggestion, physics is the law.
        // no ghost bone means hold the startup pose instead, otherwise the leg buckles under its own load.
        foreach (Leg leg in builtLegs)
        {
            foreach (Joint joint in leg.joints)
            {
                Quaternion target = joint.poseTarget != null ? joint.poseTarget.localRotation : Quaternion.Euler(joint.restEuler);

                // a ConfigurableJoint measures its limits from the pose the bone was built in, not from
                // identity, and these bones rest a long way from identity. so clamp how far the target
                // strays from rest, then put rest back — otherwise the clamp hauls the leg toward
                // identity and fights the rig instead of agreeing with physx.
                Quaternion rest = Quaternion.Euler(joint.restEuler);
                Quaternion strain = ClampToLimits(joint, Quaternion.Inverse(rest) * target);

                // target that local rotation, clamped to joint limits, relative to the physical parent
                Quaternion local = rest * strain;
                Quaternion parentRot = joint.bone.parent != null ? joint.bone.parent.rotation : Quaternion.identity;
                Quaternion delta = parentRot * local * Quaternion.Inverse(joint.rb.rotation);
                float angle;
                Vector3 axis;
                delta.ToAngleAxis(out angle, out axis);
                if (angle > 180f) angle -= 360f;
                if (float.IsInfinity(axis.x)) continue;

                // scale by inertia so the gains mean the same thing whatever the bone weighs
                Vector3 torque = axis.normalized * (angle * Mathf.Deg2Rad * poseGain) - joint.rb.angularVelocity * poseDamp;
                torque *= joint.rb.inertiaTensor.magnitude;
                torque = Vector3.ClampMagnitude(torque, maxTorque * joint.Capacity());

                // a muscle pulls on both bones, so the parent takes the reaction
                Vector3 applied = Budget(torque);
                joint.rb.AddTorque(applied);
                if (joint.parentRb != null) joint.parentRb.AddTorque(-applied);
            }
        }
    }

    void ApplyWeights()
    {
        // external loads on joints. not ours, costs no energy.
        if (hips.weight != Vector3.zero) hips.rb.AddForce(hips.weight);
        foreach (Leg leg in builtLegs)
        {
            foreach (Joint joint in leg.joints)
            {
                if (joint.weight != Vector3.zero) joint.rb.AddForce(joint.weight);
            }
        }
    }

    // every force and torque goes through here. tallies demand and scales by the energy budget.
    Vector3 Budget(Vector3 force)
    {
        energyDemand += force.magnitude;
        return force * energyScale;
    }

    // joint limits in quaternion space. the local rotation splits into a twist about the bone's
    // X axis and the swing off it, so the two clamp independently instead of arguing over euler
    // decomposition order. zero range on an axis still means that axis is free.
    Quaternion ClampToLimits(Joint joint, Quaternion local)
    {
        bool freeTwist = IsFree(joint.maxX);
        bool freeSwing = IsFree(joint.maxY) && IsFree(joint.maxZ);
        if (freeTwist && freeSwing) return local;

        // take the short way round so every angle below lands in -180 to 180
        if (local.w < 0f) local = new Quaternion(-local.x, -local.y, -local.z, -local.w);

        // swing-twist split: the twist keeps only the part of the rotation lying along X
        float twistLength = Mathf.Sqrt(local.x * local.x + local.w * local.w);
        Quaternion twist = twistLength > 1e-6f
            ? new Quaternion(local.x / twistLength, 0f, 0f, local.w / twistLength)
            : Quaternion.identity;  // folded a full 180 off axis, no twist left to read
        Quaternion swing = local * Quaternion.Inverse(twist);

        // the twist is a single signed angle about X
        if (!freeTwist)
        {
            float twistAngle = 2f * Mathf.Atan2(twist.x, twist.w) * Mathf.Rad2Deg;
            twist = Quaternion.AngleAxis(Mathf.Clamp(twistAngle, joint.maxX.x, joint.maxX.y), Vector3.right);
        }

        // what is left always swings about an axis in the YZ plane, so its rotation vector reads
        // straight off as degrees about Y and Z with no third component to muddy the clamp
        if (!freeSwing)
        {
            float swingAngle;
            Vector3 axis;
            swing.ToAngleAxis(out swingAngle, out axis);
            if (swingAngle > 1e-4f && !float.IsInfinity(axis.x))
            {
                Vector3 s = axis.normalized * swingAngle;
                s.y = ClampAxis(s.y, joint.maxY);
                s.z = ClampAxis(s.z, joint.maxZ);
                float clampedAngle = s.magnitude;
                swing = clampedAngle > 1e-4f ? Quaternion.AngleAxis(clampedAngle, s / clampedAngle) : Quaternion.identity;
            }
        }

        return swing * twist;
    }

    bool IsFree(Vector2 limits)
    {
        return limits.x == 0f && limits.y == 0f; // no limit set, the axis turns as far as it likes
    }

    float ClampAxis(float angle, Vector2 limits)
    {
        return IsFree(limits) ? angle : Mathf.Clamp(angle, limits.x, limits.y);
    }

    float LegCapacity(Leg leg)
    {
        float capacity = 0f;
        foreach (Joint joint in leg.joints) capacity += joint.Capacity();
        return leg.joints.Count > 0 ? capacity / leg.joints.Count : 0f;
    }

    int PlantedCount()
    {
        int planted = 0;
        foreach (Leg leg in builtLegs)
        {
            if (leg.contact && !leg.stepping) planted++;
        }
        return planted;
    }

    // nearest hit that is not part of this walker. a plain Physics.Raycast would stop dead on our
    // own foot collider and never see the ground behind it, so every hit gets scanned.
    bool GroundCast(Vector3 origin, float distance, out RaycastHit best)
    {
        best = default;
        bool found = false;
        int count = Physics.RaycastNonAlloc(origin, Vector3.down, rayHits, distance, groundMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            if (ownBodies.Contains(rayHits[i].collider.attachedRigidbody)) continue;
            if (!found || rayHits[i].distance < best.distance)
            {
                best = rayHits[i];
                found = true;
            }
        }
        return found;
    }

    Transform FindPoseBone(string boneName)
    {
        // search the ghost rig for a bone with a matching name
        foreach (Transform t in poseRoot.GetComponentsInChildren<Transform>())
        {
            if (t.name == boneName) return t;
        }
        return null;
    }
}
