using UnityEngine;
using UnityEngine.AI;

// Drives a Move the same way PlayerManager does, but from a target instead of input.
// Pathing is advisory: when a NavMesh is baked, CalculatePath supplies corners to walk toward;
// without one the brain heads straight for the target. Physics (Move) still owns the body.
[RequireComponent(typeof(Move))]
public class MobBrain : MonoBehaviour
{
    private Mob mob;
    private Move move;
    private NavMeshPath path;
    private readonly Vector3[] corners = new Vector3[32];
    private int cornerCount;
    private int corner;
    private float repathTimer;
    private readonly Collider[] neighbours = new Collider[16];

    [Header("Target")]
    public Transform target;
    public float stopDistance = 2f;

    [Header("Pathing")]
    public float repathInterval = 0.5f;
    public float cornerReachedDistance = 0.75f;
    public float navMeshSampleDistance = 2f; // how far off the mesh a pivot or target may sit and still path

    [Header("Separation")]
    public LayerMask mobLayer;
    public float separationRadius = 1.5f;
    public float separationWeight = 1f;

    void Awake()
    {
        mob = GetComponent<Mob>();
        move = GetComponent<Move>();
        path = new NavMeshPath();
        // Stagger so a freshly spawned wave doesn't all repath on the same frame.
        repathTimer = Random.Range(0f, repathInterval);
    }

    void Update()
    {
        if (mob.dead || target == null)
        {
            Stop();
            return;
        }
        repathTimer -= Time.deltaTime;
        if (repathTimer <= 0f)
        {
            repathTimer = repathInterval;
            Repath();
        }
        Vector3 toTarget = Flat(target.position - transform.position);
        if (toTarget.magnitude <= stopDistance)
        {
            Stop();
            return;
        }
        Vector3 steer = CombineSteering(PathDirection(toTarget), Separation());
        move.SetMoveDirection(new Vector2(steer.x, steer.z));
    }

    // Only release input once; SetMoveDirection(zero) every frame would keep Move's
    // deceleration branch re-arming after it has already settled.
    void Stop()
    {
        if (move.moveDirection != Vector2.zero) move.SetMoveDirection(Vector2.zero);
    }

    void Repath()
    {
        cornerCount = 0;
        corner = 1; // corner 0 is where we're standing
        if (!NavMesh.SamplePosition(transform.position, out NavMeshHit from, navMeshSampleDistance, NavMesh.AllAreas)) return;
        if (!NavMesh.SamplePosition(target.position, out NavMeshHit to, navMeshSampleDistance, NavMesh.AllAreas)) return;
        if (NavMesh.CalculatePath(from.position, to.position, NavMesh.AllAreas, path))
            cornerCount = path.GetCornersNonAlloc(corners); // path.corners allocates a new array per read
    }

    Vector3 PathDirection(Vector3 toTarget)
    {
        if (cornerCount < 2) return toTarget.normalized;
        while (corner < cornerCount - 1 && Flat(corners[corner] - transform.position).magnitude < cornerReachedDistance) corner++;
        return Flat(corners[corner] - transform.position).normalized;
    }

    // Sum of pushes away from nearby mobs, each scaled 1 at contact to 0 at separationRadius.
    Vector3 Separation()
    {
        Vector3 push = Vector3.zero;
        int count = Physics.OverlapSphereNonAlloc(transform.position, separationRadius, neighbours, mobLayer, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            Collider c = neighbours[i];
            if (c.transform.IsChildOf(transform)) continue;
            Vector3 other = c.attachedRigidbody != null ? c.attachedRigidbody.position : c.transform.position;
            Vector3 away = Flat(transform.position - other);
            float d = away.magnitude;
            if (d < 0.001f) continue;
            push += away / d * (1f - d / separationRadius);
        }
        return push;
    }

    Vector3 CombineSteering(Vector3 pathDir, Vector3 separation)
    {
        // TODO(human): blend pathDir (unit length toward the next corner/target) with separation
        // (unweighted push away from neighbours, grows with crowding) into one direction for Move.
        return pathDir;
    }

    static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
}
