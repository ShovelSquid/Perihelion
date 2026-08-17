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
public class LegSetup
{
    public Transform root;  // top of the leg chain, every rigidbody under it becomes a joint
    public Transform foot;  // the bone that plants on the ground. needs a rigidbody.
}

[System.Serializable]
public class Leg
{
    public Transform root;
    public int footIndex = -1;  // which joint is the foot
    public List<Joint> joints = new List<Joint>();
    public bool contact;
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
    public float maxStrength = 500f;    // force cap per leg/joint, scaled by capacity
    public float maxEnergyRate = 4000f; // max energy output per second, all forces scale down past this
    public float energyConsumeRate;     // energy being spent right now, goal is to keep this low
    public Transform hipTarget;
    public float hipWeight;             // extra load carried at the hips
    public Vector3 hipForce;
    public List<LegSetup> legs = new List<LegSetup>();
    public Transform poseRoot;          // ghost rig playing animations, joints chase its bones. optional.

    [Header("Balance")]
    public float hipPosGain = 20f;      // spring pulling hips to the hip target
    public float hipDamp = 5f;          // hip velocity damping
    public float hipRotGain = 20f;      // torque spring toward hip target rotation
    public float hipRotDamp = 5f;       // hip angular velocity damping
    public float balanceGain = 2f;      // push to keep center of mass over the feet
    public float footHoldGain = 40f;    // spring holding planted feet in place
    public float footHoldDamp = 5f;     // planted foot velocity damping

    [Header("Stepping")]
    public LayerMask groundMask = ~0;
    public float contactRayLength = 0.3f;   // how far below a foot still counts as contact
    public float stepThreshold = 0.5f;      // desirability needed before a leg steps
    public float stepDuration = 0.25f;
    public float stepHeight = 0.25f;
    public float stepLead = 0.2f;       // seconds of hip velocity to lead foot placement by
    public float stepGain = 40f;        // spring pulling the foot along the step arc
    public float stepDamp = 5f;         // stepping foot velocity damping
    public int minPlantedLegs = 1;      // never step below this many planted legs
    public Vector2 stepRange = new Vector2(0.4f, 0.8f);   // default distanceToHips given to each leg

    [Header("Pose Matching")]
    public float poseGain = 2000f;  // torque spring toward the ghost pose, in rad/s^2 per rad of error
    public float poseDamp = 90f;    // joint angular velocity damping, roughly 2*sqrt(poseGain) is critical

    public List<Leg> builtLegs = new List<Leg>();

    private float totalMass;
    private float energyDemand; // force asked for this frame, before the budget clamps it
    private float energyScale = 1f; // this frame's budget scale, from last frame's demand


    void Awake()
    {
        if (hips.rb != null)
        {
            hips.bone = hips.rb.transform;
            hips.mass = hips.rb.mass;
        }

        // collect every rigidbody under each leg root into joints. branches and non-physical bones are fine.
        builtLegs.Clear();
        foreach (LegSetup setup in legs)
        {
            if (setup.root == null || setup.foot == null) continue;

            Leg leg = new Leg();
            leg.root = setup.root;
            leg.distanceToHips = stepRange;
            foreach (Rigidbody rb in setup.root.GetComponentsInChildren<Rigidbody>())
            {
                Joint joint = new Joint();
                joint.rb = rb;
                joint.bone = rb.transform;
                joint.mass = rb.mass;
                joint.restEuler = rb.transform.localRotation.eulerAngles;
                if (poseRoot != null) joint.poseTarget = FindPoseBone(rb.name);

                // limb length is measured to the bone this one hangs off of, whatever sits between them
                Rigidbody parentRb = rb.transform.parent != null ? rb.transform.parent.GetComponentInParent<Rigidbody>() : null;
                joint.parentRb = parentRb;
                foreach (Joint parent in leg.joints)
                {
                    if (parent.rb == parentRb) parent.length = Vector3.Distance(parent.bone.position, rb.position);
                }

                if (rb.transform == setup.foot) leg.footIndex = leg.joints.Count;
                leg.joints.Add(joint);
            }

            if (leg.footIndex < 0)
            {
                Debug.LogWarning($"Leg {setup.root.name} has no rigidbody on its foot {setup.foot.name}, skipping it.");
                continue;
            }

            leg.homeOffset = hips.bone.InverseTransformPoint(leg.Foot().bone.position);
            leg.plantedPoint = leg.Foot().bone.position;
            builtLegs.Add(leg);
        }

        totalMass = hips.mass;
        foreach (Leg leg in builtLegs)
        {
            foreach (Joint joint in leg.joints) totalMass += joint.mass;
        }
    }

    void FixedUpdate()
    {
        if (hips.rb == null || hipTarget == null) return;

        // last frame's demand sets this frame's budget scale. one frame behind, close enough at 50hz.
        energyConsumeRate = Mathf.Min(energyDemand, maxEnergyRate);
        energyScale = energyDemand > maxEnergyRate ? maxEnergyRate / energyDemand : 1f;
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
            RaycastHit hit;
            leg.contact = Physics.Raycast(foot.position, Vector3.down, out hit, contactRayLength, groundMask);
            if (leg.contact && !wasContact) leg.plantedPoint = hit.point; // just landed, grab the ground here
            Debug.DrawRay(foot.position, Vector3.down * contactRayLength, leg.contact ? Color.green : Color.red);
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
        share = Vector3.ClampMagnitude(share, maxStrength * LegCapacity(leg));
        leg.push = share.normalized;
        share = Budget(share);
        hips.rb.AddForce(share);
        leg.Foot().rb.AddForce(-share); // ground reaction, the foot pushes down so the hips go up

        // keep the planted foot from sliding out
        Rigidbody foot = leg.Foot().rb;
        Vector3 hold = ((leg.plantedPoint - foot.position) * footHoldGain - foot.linearVelocity * footHoldDamp) * foot.mass;
        hold = Vector3.ClampMagnitude(hold, maxStrength * leg.Foot().Capacity());
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
        torque = Vector3.ClampMagnitude(torque, maxStrength * hips.Capacity());
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
            point.y += Mathf.Sin(t * Mathf.PI) * stepHeight; // arc the foot up and over

            Rigidbody foot = leg.Foot().rb;
            Vector3 force = ((point - foot.position) * stepGain - foot.linearVelocity * stepDamp) * foot.mass;
            force = Vector3.ClampMagnitude(force, maxStrength * LegCapacity(leg));
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
        if (Physics.Raycast(target + Vector3.up, Vector3.down, out hit, 2f, groundMask))
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

                // target that local rotation, clamped to joint limits, relative to the physical parent
                Quaternion local = ClampToLimits(joint, target);
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
                torque = Vector3.ClampMagnitude(torque, maxStrength * joint.Capacity());

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

    Quaternion ClampToLimits(Joint joint, Quaternion local)
    {
        Vector3 e = local.eulerAngles;
        e.x = ClampAngle(e.x, joint.maxX);
        e.y = ClampAngle(e.y, joint.maxY);
        e.z = ClampAngle(e.z, joint.maxZ);
        return Quaternion.Euler(e);
    }

    float ClampAngle(float a, Vector2 limits)
    {
        if (limits.x == 0f && limits.y == 0f) return a; // no limit set, leave the axis free
        if (a > 180f) a -= 360f;
        return Mathf.Clamp(a, limits.x, limits.y);
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
