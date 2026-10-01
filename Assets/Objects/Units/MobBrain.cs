using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

// Drives a Move the same way PlayerManager does, but from a target instead of input.
// Pathing is advisory: when a NavMesh is baked, CalculatePath supplies corners to walk toward;
// without one the brain heads straight for the target. Physics (Move) still owns the body.
// Combat: when it holds an aiming item and can see its target in range, it stops, faces it and aims through the HandRig.
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
    private HandRig hands;
    private Rigidbody rb;
    private Transform aimPointHelper; // created here only when the rig had no aim point; destroyed with the brain
    private bool engaged;
    private Vector3 aimCentre; // world; this frame's target aim centre, also read by FixedUpdate
    private Transform cachedTarget; // the target the caches below were built for
    private Object targetObject;
    private Mob targetMob;
    private bool targetHasObject;
    private Transform targetRoot; // the target Object's transform, or the target itself when it has none; sight ignores colliders under it
    private bool targetCacheDirty;
    private readonly List<Collider> targetColliders = new List<Collider>(); // the target Object's non-trigger colliders, rebuilt only when the target changes or one is destroyed
    private readonly RaycastHit[] sightHits = new RaycastHit[32]; // shared sight buffer; nothing allocated per frame

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

    [Header("Combat")]
    public Transform eye; // where the mob sees from and its HandRig looks from; assign a head bone, defaults to this transform
    public float shootRange = 20f; // meters from the eye to the target's aim centre
    public float keepDistance = 8f; // meters, flat; while engaged the mob stops approaching inside this
    public float aimHeight = 1f; // meters above target.position to aim at when the target has no colliders
    public LayerMask sightMask = Physics.DefaultRaycastLayers; // layers that block sight; the mob's own and the target's colliders never do
    public float turnSpeed = 360f; // degrees per second the body turns toward the target while standing

    void Awake()
    {
        mob = GetComponent<Mob>();
        move = GetComponent<Move>();
        rb = GetComponent<Rigidbody>();
        path = new NavMeshPath();
        // Stagger so a freshly spawned wave doesn't all repath on the same frame.
        repathTimer = Random.Range(0f, repathInterval);
    }

    void Start()
    {
        // Mob.Awake fills mob.hands, and Start runs after every Awake.
        hands = mob.hands != null ? mob.hands : GetComponent<HandRig>();
        if (eye == null) eye = transform;
        if (hands != null)
        {
            // HandRig.Awake defaulted it to Camera.main, the player's view, which would steer this mob's cone and look bloom.
            hands.lookSource = eye;
            if (hands.aimPoint == null)
            {
                aimPointHelper = new GameObject(name + " AimPoint").transform;
                aimPointHelper.position = eye.position + eye.forward * keepDistance;
                hands.aimPoint = aimPointHelper;
            }
        }
    }

    void Update()
    {
        if (mob.dead || target == null)
        {
            Disengage();
            Stop();
            return;
        }
        repathTimer -= Time.deltaTime;
        if (repathTimer <= 0f)
        {
            repathTimer = repathInterval;
            Repath();
        }
        UpdateCombat();
        Vector3 toTarget = Flat(target.position - transform.position);
        // While engaged the mob holds at keepDistance and shoots instead of closing to stopDistance.
        if (toTarget.magnitude <= (engaged ? keepDistance : stopDistance))
        {
            Stop();
            return;
        }
        Vector3 steer = CombineSteering(PathDirection(toTarget), Separation());
        move.SetMoveDirection(new Vector2(steer.x, steer.z));
    }

    void UpdateCombat()
    {
        if (hands == null) return;
        if (eye == null) eye = transform; // a destroyed head bone reads as null
        RefreshTargetCache();
        aimCentre = TargetAimCentre();
        if (hands.aimPoint != null) hands.aimPoint.position = aimCentre;
        // Cheap checks first, so the sight raycast only runs when everything else passes.
        bool canEngage = HoldsAimingItem() && TargetAlive()
            && (aimCentre - eye.position).sqrMagnitude <= shootRange * shootRange
            && HasSight(eye.position, aimCentre);
        if (canEngage && !engaged) Engage();
        else if (!canEngage && engaged) Disengage();
    }

    void Engage()
    {
        engaged = true;
        // Aim calls are edges only, because Mob.Aim(false) restarts a 1 s coroutine each call.
        mob.Aim(true);
    }

    void Disengage()
    {
        if (!engaged) return;
        engaged = false;
        // Pathing resumes by itself: Update's hold distance falls back to stopDistance.
        mob.Aim(false);
    }

    void RefreshTargetCache()
    {
        if (target == cachedTarget && !targetCacheDirty) return;
        // Target switch: fresh reaction, no held trigger.
        if (cachedTarget != null && target != cachedTarget && engaged) Disengage();
        cachedTarget = target;
        targetCacheDirty = false;
        targetObject = target.GetComponentInParent<Object>();
        targetHasObject = targetObject != null;
        targetMob = targetObject as Mob;
        targetRoot = targetHasObject ? targetObject.transform : target;
        targetColliders.Clear();
        if (!targetHasObject) return;
        // Runs only on target change or after a cached collider was destroyed. Mirrors Object.CollectAimParts:
        // triggers, held items and colliders that belong to a nested Object don't count.
        targetObject.GetComponentsInChildren<Collider>(true, targetColliders);
        for (int i = targetColliders.Count - 1; i >= 0; i--)
        {
            Collider c = targetColliders[i];
            if (c.isTrigger
                || c.GetComponentInParent<Item>(true) != null
                || c.GetComponentInParent<Object>(true) != targetObject)
                targetColliders.RemoveAt(i);
        }
    }

    // HandRig's cone search picks the actual part (head, limbs) around this centre from the AimPart registry.
    Vector3 TargetAimCentre()
    {
        bool any = false;
        Bounds bounds = default;
        for (int i = 0; i < targetColliders.Count; i++)
        {
            Collider c = targetColliders[i];
            if (c == null)
            {
                targetCacheDirty = true;
                continue;
            }
            if (!c.enabled || !c.gameObject.activeInHierarchy) continue;
            if (!any)
            {
                bounds = c.bounds;
                any = true;
            }
            else bounds.Encapsulate(c.bounds);
        }
        return any ? bounds.center : target.position + Vector3.up * aimHeight;
    }

    bool TargetAlive()
    {
        if (targetHasObject && (targetObject == null || targetObject.destroyed)) return false;
        if (targetMob != null && targetMob.dead) return false;
        return target.gameObject.activeInHierarchy;
    }

    bool HoldsAimingItem()
    {
        Item r = hands.GetItem(HandSide.Right);
        Item l = hands.GetItem(HandSide.Left);
        return (r != null && r.usesAiming) || (l != null && l.usesAiming);
    }

    bool HasSight(Vector3 from, Vector3 to)
    {
        Vector3 delta = to - from;
        float dist = delta.magnitude;
        if (dist < 1e-4f) return true;
        int count = Physics.RaycastNonAlloc(from, delta / dist, sightHits, dist, sightMask, QueryTriggerInteraction.Ignore);
        // Results are unsorted, but anything short of the aim centre that isn't this mob or the target
        // blocks, so no nearest-hit pass is needed.
        for (int i = 0; i < count; i++)
        {
            Collider hit = sightHits[i].collider;
            if (hit == null) continue;
            Transform t = hit.transform;
            if (t.IsChildOf(transform) || t.IsChildOf(targetRoot) || IsHeldItem(t)) continue;
            return false;
        }
        return true;
    }

    bool IsHeldItem(Transform t)
    {
        Item r = hands.GetItem(HandSide.Right);
        if (r != null && t.IsChildOf(r.transform)) return true;
        Item l = hands.GetItem(HandSide.Left);
        return l != null && l != r && t.IsChildOf(l.transform);
    }

    void FixedUpdate()
    {
        // Move only turns the body while it has move input, so turning here only without it
        // means the two never rotate the body in the same step.
        if (!engaged || rb == null || mob.dead || move.moveDirection != Vector2.zero) return;
        Vector3 face = Flat(aimCentre - rb.position);
        if (face.sqrMagnitude < 1e-4f) return;
        rb.MoveRotation(Quaternion.RotateTowards(rb.rotation, Quaternion.LookRotation(face, Vector3.up), turnSpeed * Time.fixedDeltaTime));
    }

    // Die hides the mob before Mob marks it dead, so this is where death lands. Mob.Aim(false) would try to
    // start a coroutine on the now-inactive object, so aim is lowered on the rig directly.
    void OnDisable()
    {
        if (!engaged) return;
        engaged = false;
        if (hands != null) hands.SetAiming(false);
    }

    void OnDestroy()
    {
        if (aimPointHelper != null) Destroy(aimPointHelper.gameObject);
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
