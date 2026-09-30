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

    bool ProbeGround(Vector3 origin, Vector3 dir, float range, out RaycastHit hit)
    {
        return Physics.Raycast(origin, dir, out hit, range, groundLayer, QueryTriggerInteraction.Ignore);
    }
}
