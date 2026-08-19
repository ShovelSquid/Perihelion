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

    // rest pose relative to the parent joint, captured at startup. the ik chain is rebuilt from
    // these every solve rather than from the live bones, so physics jitter cannot feed back into it.
    public Vector3 restOffset;      // offset from the parent joint, in the parent's rotation frame
    public Quaternion restFromParent = Quaternion.identity;
    public Vector3 hingeAxis = Vector3.right;   // the axis this joint actually swings about
    public Quaternion ikWorldRot;   // world rotation the ik wants this bone in
    public bool ikDriven;           // whether the pose servo should chase ikWorldRot this frame

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
    public Transform ikTarget;      // where this foot is asked to be. visible in the scene, optional.
    public float soleHeight;        // foot bone origin above the sole, so goals can be given on the ground
    public Vector3 ikGoal;          // the goal actually fed to ik, rate limited so it cannot jump

    // ik scratch, sized once at startup so the solve never allocates
    [System.NonSerialized] public LegIK.Link[] links;
    [System.NonSerialized] public float[] ikAngles;
    [System.NonSerialized] public Vector3[] ikPos;
    [System.NonSerialized] public Quaternion[] ikRot;
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
    [Range(0f, 1f)]
    [Tooltip("Desirability a leg must reach before it steps. Desirability is a 0-1 gradient, so a value of 1 never triggers.")]
    public float stepThreshold = 0.5f;
    [Tooltip("Seconds a swing takes. Keep it well under sqrt(comHeight / gravity) or the body falls before the foot lands.")]
    public float stepDuration = 0.25f;
    [Tooltip("Seconds of hip velocity to lead foot placement by. Neutral is about stepDuration/2 — larger brakes, smaller accelerates.")]
    public float stepLead = 0.125f;
    public float stepGain = 40f;        // spring pulling the foot along the step arc
    public float stepDamp = 5f;         // stepping foot velocity damping
    public int minPlantedLegs = 1;      // never step below this many planted legs
    // foot placement is what actually drives a walker. these steer it, see StartStep.
    public Vector3 desiredVelocity;     // written by the locomotion driver, zero means stand still
    public float placementGain = 0.35f; // how hard a velocity error pulls the landing spot
    [Range(0f, 1f)]
    [Tooltip("FRACTION OF LEG REACH. Furthest a foot may land from the hips.")]
    public float strideLimit = 0.55f;
    [Range(0f, 1f)]
    [Tooltip("FRACTION OF LEG REACH. Peak height of the step arc.")]
    public float stepHeight = 0.12f;
    [Tooltip("FRACTION OF LEG REACH. Foot-to-hip drift that maps to 0 and 1 desirability. Resolved values show under builtLegs > distanceToHips.")]
    public Vector2 stepRange = new Vector2(0.15f, 0.35f);

    [Header("Leg Control")]
    // a swing leg is solved with ik: we care where the foot lands, so joint angles are the answer.
    // a stance leg is driven by jacobian transpose: we care what force it puts into the ground,
    // and the joint torques that produce it are the answer. the leg does the pushing either way.
    public bool useLegIK = true;
    public int ikIterations = 8;
    [Tooltip("Leg reaches per second the ik goal may travel. Caps touchdown jumps without lagging a normal swing.")]
    public float goalSlew = 8f;
    [Range(0f, 1f)]
    [Tooltip("1 makes stance legs push by extending their joints. 0 falls back to shoving the foot rigidbody directly, which is the old cheat.")]
    public float stanceJacobian = 1f;

    [Header("Pose Matching")]
    public float poseGain = 2000f;  // torque spring toward the ghost pose, in rad/s^2 per rad of error
    public float poseDamp = 90f;    // joint angular velocity damping, roughly 2*sqrt(poseGain) is critical

    public List<Leg> builtLegs = new List<Leg>();

    [Header("Balance State (read only)")]
    public Vector3 capturePoint;    // where the com is heading if nothing changes
    public Vector3 supportCenter;   // average of the planted feet
    public float balanceError;      // how far the capture point sits from the support center
    public bool offBalance;         // capture point has left the feet, only a step saves us now

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
            // goals arrive as ground points, but ik drives the foot bone, which rides above the sole
            leg.soleHeight = leg.footCollider != null
                ? leg.Foot().bone.position.y - leg.footCollider.bounds.min.y
                : 0f;
            BuildIKChain(leg);

            leg.homeOffset = hips.bone.InverseTransformPoint(leg.Foot().bone.position);
            leg.plantedPoint = leg.Foot().bone.position;
            leg.ikGoal = leg.plantedPoint;  // start where the foot already is, or frame one slews from the origin
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

    // capture the leg's rest shape relative to its own parent joints, and size the solver scratch.
    // measuring against the parent joint rather than the transform parent means non-physical bones
    // sitting between two rigidbodies do not disturb the chain.
    void BuildIKChain(Leg leg)
    {
        int count = leg.joints.Count;
        leg.links = new LegIK.Link[count];
        leg.ikAngles = new float[count];
        leg.ikPos = new Vector3[count];
        leg.ikRot = new Quaternion[count];

        for (int i = 0; i < count; i++)
        {
            Joint joint = leg.joints[i];
            Transform parent = ParentBone(leg, i);

            joint.restOffset = Quaternion.Inverse(parent.rotation) * (joint.bone.position - parent.position);
            joint.restFromParent = Quaternion.Inverse(parent.rotation) * joint.bone.rotation;

            ConfigurableJoint cj = joint.rb.GetComponent<ConfigurableJoint>();
            if (cj != null && cj.axis != Vector3.zero) joint.hingeAxis = cj.axis.normalized;

            leg.links[i] = new LegIK.Link
            {
                offsetFromParent = joint.restOffset,
                restFromParent = joint.restFromParent,
                hingeAxis = joint.hingeAxis,
                // a free axis still has to be bounded for ccd, or it will spin the bone right round
                limit = IsFree(joint.maxX) ? new Vector2(-180f, 180f) : joint.maxX,
            };
        }
    }

    // the bone the ik chain hangs this joint off: the previous joint in the leg, or the hips
    Transform ParentBone(Leg leg, int index)
    {
        for (int i = index - 1; i >= 0; i--)
        {
            if (leg.joints[i].rb == leg.joints[index].parentRb) return leg.joints[i].bone;
        }
        return hips.bone;
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

        // ik is re-decided every frame, so clear last frame's claim before anything sets it again
        foreach (Leg leg in builtLegs)
        {
            foreach (Joint joint in leg.joints) joint.ikDriven = false;
        }

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

    public Vector3 GetCenterOfMassVelocity()
    {
        Vector3 momentum = Vector3.zero;
        float mass = 0f;
        if (hips.rb != null)
        {
            momentum += hips.rb.linearVelocity * hips.mass;
            mass += hips.mass;
        }
        foreach (Leg leg in builtLegs)
        {
            foreach (Joint joint in leg.joints)
            {
                momentum += joint.rb.linearVelocity * joint.mass;
                mass += joint.mass;
            }
        }
        return mass > 0f ? momentum / mass : Vector3.zero;
    }

    // where the center of mass is heading, not just where it sits. falling is about momentum:
    // a body leaning out but drifting back is fine, an upright one moving fast is already lost.
    // this is the capture point — how far an inverted pendulum of this height carries before it
    // would topple, so steering it back over the feet is what actually keeps the walker up.
    public Vector3 GetCapturePoint(float groundHeight)
    {
        Vector3 com = GetCenterOfMass();
        if (captureLead <= 0f) return com;

        float gravity = Physics.gravity.magnitude;
        if (gravity <= 0f) return com;

        float height = Mathf.Max(com.y - groundHeight, 0f);
        Vector3 drift = GetCenterOfMassVelocity();
        drift.y = 0f;
        return com + drift * (Mathf.Sqrt(height / gravity) * captureLead);
    }

    // the planted leg carrying the least weight is the one we can pick up without dropping the body
    public Leg GetFreestLeg()
    {
        Leg best = null;
        foreach (Leg leg in builtLegs)
        {
            if (leg.stepping || !leg.contact) continue;
            if (best == null || leg.loadShare < best.loadShare) best = leg;
        }
        return best;
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
        supportCenter = Vector3.zero;
        int planted = PlantedCount();
        if (planted > 0)
        {
            foreach (Leg leg in builtLegs)
            {
                if (leg.contact && !leg.stepping) supportCenter += leg.plantedPoint;
            }
            supportCenter /= planted;
        }

        capturePoint = GetCapturePoint(supportCenter.y);
        Vector3 balance = supportCenter - capturePoint;
        balance.y = 0f;
        balanceError = balance.magnitude;
        // once the capture point is outside the feet no amount of pushing keeps us up, only a step
        offBalance = planted > 0 && balanceError > SupportRadius();

        UpdateLoadShares(capturePoint);

        // the spring has to move the whole body, not just the hip bone
        hipForce = spring * totalMass + support + (planted > 0 ? balance * balanceGain * totalMass : Vector3.zero);

        Debug.DrawLine(capturePoint, capturePoint + Vector3.up * 2f, offBalance ? Color.red : Color.green);
        Debug.DrawLine(supportCenter, capturePoint, Color.grey);
    }

    // how far the planted feet reach out from their own center, footprints included. this is the
    // crude stand-in for a support polygon: inside it we can push our way back upright, outside it
    // the only recovery is to put a foot down somewhere new.
    public float SupportRadius()
    {
        float radius = 0f;
        foreach (Leg leg in builtLegs)
        {
            if (!leg.contact || leg.stepping) continue;
            Vector3 offset = leg.plantedPoint - supportCenter;
            offset.y = 0f;
            float r = offset.magnitude;
            if (leg.footCollider != null)
            {
                Bounds bounds = leg.footCollider.bounds;
                r += Mathf.Max(bounds.extents.x, bounds.extents.z);   // the foot itself holds ground
            }
            if (r > radius) radius = r;
        }
        return radius;
    }

    // split the body's weight across the planted feet by how close each one is to the capture point.
    // an equal split has the far foot pushing up just as hard as the near one, and that difference
    // in lever arm is a tipping moment the balance controller then has to spend its budget undoing.
    // for two feet the inverse-distance weights are exactly the seesaw solution, so moments cancel.
    void UpdateLoadShares(Vector3 capture)
    {
        float total = 0f;
        foreach (Leg leg in builtLegs)
        {
            if (leg.contact && !leg.stepping)
            {
                Vector3 offset = leg.plantedPoint - capture;
                offset.y = 0f;
                // a foot right under the capture point takes the lot, so keep the divisor off zero
                leg.loadShare = 1f / Mathf.Max(offset.magnitude, 1e-3f);
                total += leg.loadShare;
            }
            else
            {
                leg.loadShare = 0f;
            }
        }

        if (total <= 0f) return;
        foreach (Leg leg in builtLegs) leg.loadShare /= total;   // shares sum to the whole body weight
    }

    public void UpdateLegForce(Leg leg)
    {
        // this leg carries its share of the hip force, pushing off its planted point
        Vector3 share = hipForce * leg.loadShare;
        share = Vector3.ClampMagnitude(share, maxForce * LegCapacity(leg));
        leg.push = share.normalized;

        // the honest path: the leg presses the ground by extending, and the ground lifts the body
        // back up through the chain. the direct path shoves the hips and the foot as a force pair,
        // which works but leaves the leg itself doing nothing, so it hangs there looking inert.
        float jacobian = Mathf.Clamp01(stanceJacobian);
        if (jacobian > 0f) ApplyStanceForce(leg, -share * jacobian);
        if (jacobian < 1f)
        {
            Vector3 direct = Budget(share * (1f - jacobian));
            hips.rb.AddForce(direct);
            leg.Foot().rb.AddForce(-direct);
        }

        // keep the planted foot from sliding out
        Rigidbody foot = leg.Foot().rb;
        Vector3 hold = ((leg.plantedPoint - foot.position) * footHoldGain - foot.linearVelocity * footHoldDamp) * foot.mass;
        hold = Vector3.ClampMagnitude(hold, maxForce * leg.Foot().Capacity());
        foot.AddForce(Budget(hold));

        Debug.DrawRay(leg.plantedPoint, share * 0.002f, Color.cyan);
    }

    // solve the leg's joint angles so the foot lands on its goal, and hand them to the pose servo.
    // this is what makes the leg articulate instead of being dragged around by its ankle.
    void SolveLegIK(Leg leg, Vector3 goal)
    {
        if (leg.links == null || leg.links.Length == 0) return;

        goal += Vector3.up * leg.soleHeight;    // goals sit on the ground, the foot bone rides above it
        Transform baseBone = ParentBone(leg, 0);

        LegIK.Solve(leg.links, leg.ikAngles, leg.ikPos, leg.ikRot,
                    baseBone.position, baseBone.rotation, goal, ikIterations);

        for (int i = 0; i < leg.joints.Count; i++)
        {
            leg.joints[i].ikWorldRot = leg.ikRot[i];
            leg.joints[i].ikDriven = true;
        }

        for (int i = 1; i < leg.ikPos.Length; i++) Debug.DrawLine(leg.ikPos[i - 1], leg.ikPos[i], Color.magenta);
        Debug.DrawLine(leg.ikPos[leg.ikPos.Length - 1], goal, Color.magenta);
    }

    // a real leg pushes the ground by extending, not by having its foot shoved. the joint torque
    // that produces force F at the foot is the jacobian transpose: t = (axis x lever) . F. these
    // are muscle torques like any other, so the parent takes the reaction and nothing is invented.
    void ApplyStanceForce(Leg leg, Vector3 force)
    {
        Vector3 footPos = leg.Foot().rb.worldCenterOfMass;
        for (int i = 0; i < leg.joints.Count; i++)
        {
            if (i == leg.footIndex) continue;   // the foot itself has no leverage on itself
            Joint joint = leg.joints[i];

            Vector3 axis = joint.rb.rotation * joint.hingeAxis;
            Vector3 lever = footPos - joint.rb.worldCenterOfMass;
            Vector3 torque = axis * Vector3.Dot(Vector3.Cross(axis, lever), force);

            torque = Vector3.ClampMagnitude(torque, maxTorque * joint.Capacity());
            Vector3 applied = Budget(torque);
            joint.rb.AddTorque(applied);
            if (joint.parentRb != null) joint.parentRb.AddTorque(-applied);
        }
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

        // let the leg that wants it most step, as long as enough legs stay planted.
        // being knocked off balance overrides that: a recovery step is worth going briefly
        // under-supported for, because standing still while the com runs away just falls over.
        Leg mover = offBalance ? GetFreestLeg() : GetMostMovableLeg();
        bool wanted = mover != null && (offBalance || mover.adjustDesirability > stepThreshold);
        int needed = offBalance ? 1 : minPlantedLegs;
        if (wanted && PlantedCount() > needed)
        {
            StartStep(mover, offBalance);
        }

        // every leg publishes where its foot is asked to be, swinging or planted. the ik target
        // transform is the visible copy of that, so a goal can be watched or dragged in the scene.
        foreach (Leg leg in builtLegs)
        {
            Vector3 goal;

            if (leg.stepping)
            {
                leg.stepTime += Time.fixedDeltaTime;
                float t = Mathf.Clamp01(leg.stepTime / stepDuration);
                goal = Vector3.Lerp(leg.stepFrom, leg.stepTo, t);
                goal.y += Mathf.Sin(t * Mathf.PI) * stepHeight * leg.reach;  // arc the foot up and over

                if (t >= 1f)
                {
                    leg.stepping = false;
                    leg.plantedPoint = leg.stepTo;
                }
                Debug.DrawLine(leg.Foot().rb.position, leg.stepTo, Color.yellow);
            }
            else if (leg.contact)
            {
                goal = leg.plantedPoint;    // planted: hold the patch of ground we took
            }
            else
            {
                // in the air and not stepping. plantedPoint is stale here — it is the last place
                // this foot touched down, a world point the body is falling away from, so chasing
                // it stretches the leg to its limits and flails. hang the foot under the body
                // instead, and reach for the ground if it is close enough to land on.
                goal = hips.bone.TransformPoint(leg.homeOffset);
                RaycastHit hit;
                if (GroundCast(goal + Vector3.up * leg.reach * 0.5f, leg.reach * 1.5f, out hit))
                {
                    goal = hit.point;
                }
            }

            // landing and step hand-off move the goal in one jump. fed straight to ik that is a step
            // input, and the pose servo answers a step input with a torque spike — which is the flop
            // you see on touchdown. the cap is loose enough that a normal swing arc passes untouched.
            leg.ikGoal = Vector3.MoveTowards(leg.ikGoal, goal, goalSlew * leg.reach * Time.fixedDeltaTime);
            goal = leg.ikGoal;

            if (leg.ikTarget != null) leg.ikTarget.position = goal;

            if (useLegIK)
            {
                // both phases get their shape from ik — swinging, so the foot arrives where it was
                // sent; planted, so the leg keeps folding to hold the foot still while the body
                // travels over it. that second one is what walking actually looks like. the phases
                // differ in force, not in whether the leg articulates: only stance pushes.
                SolveLegIK(leg, goal);
            }
            else if (leg.stepping)
            {
                Rigidbody foot = leg.Foot().rb;
                Vector3 force = ((goal - foot.position) * stepGain - foot.linearVelocity * stepDamp) * foot.mass;
                force = Vector3.ClampMagnitude(force, maxForce * LegCapacity(leg));
                foot.AddForce(Budget(force));
            }
        }
    }

    void StartStep(Leg leg, bool recovering)
    {
        leg.stepping = true;
        leg.stepTime = 0f;
        leg.stepFrom = leg.Foot().rb.position;

        Vector3 velocity = hips.rb.linearVelocity;
        velocity.y = 0f;
        Vector3 wanted = desiredVelocity;
        wanted.y = 0f;

        Vector3 target;
        if (recovering)
        {
            // catching a fall: put the foot where the com is actually going, not where the gait
            // would like it. this is the only move that works once the capture point is outside.
            target = capturePoint;
        }
        else
        {
            // foot placement is how a walker controls its speed. landing at the home spot plus
            // half a stance of travel holds the current pace; landing further out brakes, landing
            // short of it accelerates. so the velocity error is what actually drives us at the goal.
            target = hips.bone.TransformPoint(leg.homeOffset)
                   + velocity * stepLead
                   + (velocity - wanted) * placementGain;
        }

        // a leg cannot land where it cannot reach. without this a big velocity error commands a
        // lunge the leg just stretches at, and the body topples instead of striding.
        Vector3 fromHips = target - hips.rb.position;
        fromHips.y = 0f;
        float maxStride = leg.reach * strideLimit;
        if (fromHips.magnitude > maxStride) fromHips = fromHips.normalized * maxStride;
        target = hips.rb.position + fromHips;

        // drop it onto the ground. cast from and over a leg's worth of distance, since the target
        // is built at hip height and the ground could be well below it.
        RaycastHit hit;
        if (GroundCast(target + Vector3.up * leg.reach * 0.5f, leg.reach * 1.5f, out hit))
        {
            target = hit.point;
        }
        else
        {
            target.y = leg.plantedPoint.y;  // no ground found, keep the height we last stood at
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
                Quaternion targetWorld;
                if (joint.ikDriven)
                {
                    // the ik already solved on the hinge axes inside their limits, so this pose is
                    // one the joints can hold. clamping it again would only fight the solution.
                    targetWorld = joint.ikWorldRot;
                }
                else
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
                    targetWorld = parentRot * local;
                }

                Quaternion delta = targetWorld * Quaternion.Inverse(joint.rb.rotation);
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
    public bool GroundCast(Vector3 origin, float distance, out RaycastHit best)
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
