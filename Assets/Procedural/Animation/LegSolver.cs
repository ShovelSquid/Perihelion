using UnityEngine;

// Car-style leg solver: every planted foot behaves like a raycast wheel contact. It pushes the hip
// Rigidbody at its hip socket with suspension, lateral grip and drive forces, clamped to a friction
// cone. Feet push, never pull. Swinging (or ground-less) legs contribute zero force.
public class LegSolver : MonoBehaviour
{
    [System.Serializable]
    public class Leg
    {
        public Transform hipSocket; // Where this leg's force is applied on the hip body
        public Transform footTarget; // Foot IK target this solver writes
        public Transform footRest; // Optional rest pose reference
        public float restLength = 0f; // Hip height above the FLOOR (not world y); 0 = derived in Awake
        public float maxLength = 0f; // Overstretch limit; 0 = restLength * 1.25 in Awake
        public float maxStride = 1f; // Horizontal foot offset from the socket that counts as fully stretched

        [System.NonSerialized] public bool planted;
        [System.NonSerialized] public Vector3 plantPoint;
        [System.NonSerialized] public Vector3 groundNormal;
        [System.NonSerialized] public float swingProgress = -1f; // -1 = not swinging, 0..1 = mid-swing
        [System.NonSerialized] public Vector3 swingFrom;
        [System.NonSerialized] public Vector3 swingTo;
        [System.NonSerialized] public float gripUsage;
        [System.NonSerialized] public Vector3 lastForce;
        [System.NonSerialized] public float lastLength;
    }

    [Header("Body")]
    public Rigidbody hip;
    public Leg[] legSet;
    public int minLegsGrounded = 2;
    public LayerMask groundLayer = ~0;
    public float probeDistance = 0.5f; // Extra raycast reach beyond maxLength

    [Header("Suspension (per unit mass)")]
    public float springK = 60f;
    public float damperC = 8f;

    [Header("Traction (per unit mass)")]
    public float lateralGrip = 12f;
    public float driveGain = 10f;
    public float mu = 1.2f; // Friction coefficient for the cone
    public float maxSpeed = 5f;

    [Header("Upright")]
    public float uprightK = 20f;
    public float uprightD = 4f;
    public float yawK = 10f;
    public float yawD = 3f;
    public bool useAverageGroundNormal = true;

    [Header("Stepping")]
    public float stepDuration = 0.35f;
    public float stepHeight = 0.4f;
    public float raibertScale = 0.5f;

    [Header("Debug")]
    public float gizmoForceScale = 0.001f;

    private Vector3 targetVelocity;
    private int plantedCount;
    private Vector3 avgGroundNormal = Vector3.up;

    void Awake()
    {
        if (hip == null) hip = GetComponent<Rigidbody>();
        if (hip == null)
        {
            Debug.LogWarning("LegSolver: no hip Rigidbody assigned or found, disabling.", this);
            enabled = false;
            return;
        }
        if (legSet == null || legSet.Length == 0)
        {
            Debug.LogWarning("LegSolver: legSet is empty, disabling.", this);
            enabled = false;
            return;
        }

        for (int i = 0; i < legSet.Length; i++)
        {
            Leg leg = legSet[i];
            if (leg == null || leg.hipSocket == null) continue;

            leg.swingProgress = -1f;
            leg.groundNormal = Vector3.up;

            RaycastHit hit;
            bool found = ProbeGround(leg.hipSocket.position, Vector3.down, 100f, out hit);
            if (leg.restLength <= 0f)
            {
                if (found) leg.restLength = hit.distance;
                else
                {
                    Debug.LogWarning("LegSolver: no ground under " + leg.hipSocket.name + ", using restLength 1.", this);
                    leg.restLength = 1f;
                }
            }
            if (leg.maxLength <= leg.restLength) leg.maxLength = leg.restLength * 1.25f;
            leg.lastLength = leg.restLength;

            if (found)
            {
                leg.planted = true;
                leg.plantPoint = hit.point;
                leg.groundNormal = hit.normal;
                if (leg.footTarget != null) leg.footTarget.position = leg.plantPoint;
            }
        }
    }

    // Vector2 is world XZ (x -> X, y -> Z), same convention as Move.SetMoveDirection.
    public void SetMoveDirection(Vector2 direction)
    {
        direction = Vector2.ClampMagnitude(direction, 1f);
        targetVelocity = new Vector3(direction.x, 0f, direction.y) * maxSpeed;
    }

    void FixedUpdate()
    {
        UpdateStepping(Time.fixedDeltaTime);
        ApplyLegForces();
        ApplyUprightAndYaw();
    }

    void ApplyLegForces()
    {
        plantedCount = 0;
        Vector3 normalSum = Vector3.zero;
        for (int i = 0; i < legSet.Length; i++)
        {
            Leg leg = legSet[i];
            if (leg == null || leg.hipSocket == null || !leg.planted) continue;
            plantedCount++;
            normalSum += leg.groundNormal;
        }
        avgGroundNormal = normalSum.sqrMagnitude > 1e-6f ? normalSum.normalized : Vector3.up;
        if (plantedCount == 0) return; // Airborne: no force

        Vector3 heading = Vector3.zero;
        float desiredSpeed = targetVelocity.magnitude;
        if (desiredSpeed >= 0.01f) heading = targetVelocity / desiredSpeed;

        // Scale per-unit-mass gains by mass and share the load between planted legs so tuning is mass-independent.
        float massScale = hip.mass / plantedCount;

        for (int i = 0; i < legSet.Length; i++)
        {
            Leg leg = legSet[i];
            if (leg == null || leg.hipSocket == null || !leg.planted) continue;

            Vector3 socket = leg.hipSocket.position;
            Vector3 v = hip.GetPointVelocity(socket);

            Vector3 probeDir = leg.groundNormal.sqrMagnitude > 1e-6f ? -leg.groundNormal : Vector3.down;
            RaycastHit hit;
            if (!ProbeGround(socket, probeDir, leg.maxLength + probeDistance, out hit))
            {
                // Lost the ground: treat as airborne, stepping will send it looking for a new footing
                leg.planted = false;
                leg.gripUsage = 0f;
                leg.lastForce = Vector3.zero;
                continue;
            }

            // plantPoint is the pinned foot; the hit only supplies length and normal
            leg.groundNormal = hit.normal;
            float length = hit.distance;
            leg.lastLength = length;
            Vector3 n = hit.normal;

            // Suspension: feet push, never pull
            float fs = Mathf.Max(0f, (springK * (leg.restLength - length) - damperC * Vector3.Dot(v, n)) * massScale);

            // Lateral grip (cancels slip, or all horizontal slip = braking when no heading) plus drive along heading
            Vector3 slip = Vector3.ProjectOnPlane(v, n);
            Vector3 lateral = heading == Vector3.zero ? slip : slip - Vector3.Project(slip, heading);
            Vector3 fLat = -lateral * lateralGrip * massScale;

            Vector3 fDrive = Vector3.zero;
            if (heading != Vector3.zero)
            {
                Vector3 velError = Vector3.ProjectOnPlane(targetVelocity - v, n);
                fDrive = Vector3.Project(velError, heading) * driveGain * massScale;
            }

            // Friction cone: tangential force cannot exceed mu * normal force
            Vector3 fTan = fLat + fDrive;
            float limit = mu * fs;
            float requested = fTan.magnitude;
            if (limit <= 1e-4f)
            {
                leg.gripUsage = requested > 1e-4f ? 1f : 0f;
                fTan = Vector3.zero;
            }
            else
            {
                leg.gripUsage = requested / limit; // Unclamped so stepping can react to saturation
                if (leg.gripUsage > 1f) fTan /= leg.gripUsage;
            }

            Vector3 force = n * fs + fTan;
            leg.lastForce = force;
            hip.AddForceAtPosition(force, socket, ForceMode.Force);
        }
    }

    void ApplyUprightAndYaw()
    {
        if (plantedCount == 0) return; // Do not torque an airborne body

        Vector3 upAxis = useAverageGroundNormal ? avgGroundNormal : Vector3.up;

        // hip.mass scaling keeps gains mass-independent (same intent as Move.cs using ForceMode.Acceleration)
        Vector3 tilt = Vector3.Cross(hip.transform.up, upAxis);
        Vector3 torque = (tilt * uprightK - hip.angularVelocity * uprightD) * hip.mass;

        if (new Vector3(targetVelocity.x, 0f, targetVelocity.z).sqrMagnitude > 0.01f)
        {
            Vector3 desiredFwd = Vector3.ProjectOnPlane(targetVelocity, upAxis).normalized;
            Vector3 fwd = Vector3.ProjectOnPlane(hip.transform.forward, upAxis);
            float yawErr = Vector3.SignedAngle(fwd, desiredFwd, upAxis) * Mathf.Deg2Rad;
            torque += upAxis * (yawErr * yawK - Vector3.Dot(hip.angularVelocity, upAxis) * yawD) * hip.mass;
        }

        hip.AddTorque(torque, ForceMode.Force);
    }

    void UpdateStepping(float dt)
    {
        int swingingCount = 0;
        int legsCount = legSet.Length;
        int maxSwinging = Mathf.Max(0, legsCount - minLegsGrounded);

        for (int i = 0; i < legsCount; i++)
        {
            Leg leg = legSet[i];
            if (leg == null || leg.hipSocket == null) continue;

            if (leg.planted)
            {
                // Planted foot stays pinned
                if (leg.footTarget != null) leg.footTarget.position = leg.plantPoint;
                continue;
            }

            if (leg.swingProgress < 0f)
            {
                // Lost ground without a swing: airborne recovery, ignores the maxSwinging cap
                BeginSwing(leg);
            }
            else
            {
                leg.swingProgress += dt / Mathf.Max(stepDuration, 0.01f);
                float t = Mathf.Min(leg.swingProgress, 1f);
                if (leg.footTarget != null)
                {
                    leg.footTarget.position = Vector3.Lerp(leg.swingFrom, leg.swingTo, t)
                        + Vector3.up * (Mathf.Sin(t * Mathf.PI) * stepHeight);
                }

                if (leg.swingProgress >= 1f)
                {
                    RaycastHit hit;
                    if (ProbeGround(leg.swingTo + Vector3.up * stepHeight, Vector3.down, stepHeight + leg.maxLength + probeDistance, out hit))
                    {
                        leg.planted = true;
                        leg.plantPoint = hit.point;
                        leg.groundNormal = hit.normal;
                        leg.swingProgress = -1f;
                        if (leg.footTarget != null) leg.footTarget.position = leg.plantPoint;
                        continue;
                    }

                    // No ground at the landing spot: keep searching from here
                    leg.swingFrom = leg.swingTo;
                    leg.swingTo = RaibertTarget(leg);
                    leg.swingProgress = 0f;
                }
            }
            swingingCount++;
        }

        if (swingingCount >= maxSwinging) return;

        // Lift the single most strained planted leg, if any is past its limit
        Leg best = null;
        float bestScore = 0f;
        for (int i = 0; i < legsCount; i++)
        {
            Leg leg = legSet[i];
            if (leg == null || leg.hipSocket == null || !leg.planted) continue;

            float score;
            if (leg.lastLength > leg.maxLength)
            {
                score = Mathf.Infinity;
            }
            else
            {
                float stretch = Vector3.ProjectOnPlane(leg.plantPoint - leg.hipSocket.position, leg.groundNormal).magnitude
                    / Mathf.Max(leg.maxStride, 0.01f);
                score = Mathf.Max(stretch, leg.gripUsage);
            }

            if (score > 1f && (best == null || score > bestScore))
            {
                best = leg;
                bestScore = score;
            }
        }

        if (best != null) BeginSwing(best);
    }

    void BeginSwing(Leg leg)
    {
        leg.swingFrom = leg.footTarget != null ? leg.footTarget.position : leg.plantPoint;
        leg.swingTo = RaibertTarget(leg);
        leg.planted = false;
        leg.swingProgress = 0f;
        leg.gripUsage = 0f;
        leg.lastForce = Vector3.zero;
    }

    // Raibert heuristic: step to where the body will be half a step ahead
    Vector3 RaibertTarget(Leg leg)
    {
        Vector3 v = targetVelocity.magnitude > 0.1f ? targetVelocity : hip.linearVelocity;
        Vector3 predicted = leg.hipSocket.position + v * stepDuration * raibertScale;

        RaycastHit hit;
        if (ProbeGround(predicted + Vector3.up * stepHeight, Vector3.down, stepHeight + leg.maxLength + probeDistance, out hit))
            return hit.point;
        return leg.plantPoint; // Foot stays roughly where it was
    }

    void OnDrawGizmosSelected()
    {
        if (legSet == null) return;
        for (int i = 0; i < legSet.Length; i++)
        {
            Leg leg = legSet[i];
            if (leg == null || leg.hipSocket == null) continue;

            Vector3 socket = leg.hipSocket.position;
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(socket, 0.05f);

            if (leg.planted)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawLine(socket, leg.plantPoint);
                Gizmos.DrawWireSphere(leg.plantPoint, 0.08f);
            }
            else if (leg.swingProgress >= 0f)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(leg.swingTo, 0.08f);
                Gizmos.DrawLine(leg.swingFrom, leg.swingTo);
            }

            Gizmos.color = Color.red;
            Gizmos.DrawLine(socket, socket + leg.lastForce * gizmoForceScale);
        }
    }

    bool ProbeGround(Vector3 origin, Vector3 dir, float range, out RaycastHit hit)
    {
        return Physics.Raycast(origin, dir, out hit, range, groundLayer, QueryTriggerInteraction.Ignore);
    }
}
