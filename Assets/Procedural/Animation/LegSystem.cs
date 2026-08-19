// Locomotion driver — decides where the body should be. LegSolver works out how to get it there.
//
// The whole contract between the two is hipTarget: a position and a rotation. Nothing here knows
// about contacts, forces or joints, and nothing in the solver knows about goals or pathing.
//
// The target is a carrot, not a destination. It walks toward the goal at walkSpeed and is leashed
// so it can never get further than maxLead from the body — otherwise a goal fifty units away puts
// fifty units of spring error into the hips and the body gets dragged off its feet instead of
// walking there.
using UnityEngine;

public class LegSystem : MonoBehaviour
{
    public LegSolver solver;
    public Transform hipTarget;     // the handle the solver already reads. moved from here.

    [Header("Goal")]
    public Transform goal;              // walk to this if set, otherwise to goalPoint
    public Vector3 goalPoint;
    public float arriveRadius = 3f;     // close enough. stops the carrot jittering on the spot.

    [Header("Gait")]
    public float walkSpeed = 12f;       // how fast the carrot advances, so how fast we walk
    public float turnSpeed = 120f;      // degrees per second the body may swing its heading
    public float rideHeight = 22f;      // how far above the ground the hips are carried
    public float maxLead = 4f;          // furthest the carrot may get ahead of the actual hips

    [Header("Recovery")]
    public bool haltWhenOffBalance = true;  // stop chasing the goal while catching a fall
    public float recoverySpeed = 30f;       // how fast the carrot retreats back over the body

    [Header("State (read only)")]
    public Vector3 travel;      // the velocity we are asking for, handed to the solver
    public bool arrived;

    void Reset()
    {
        solver = GetComponent<LegSolver>();
        if (solver != null) hipTarget = solver.hipTarget;
    }

    public Vector3 GoalPosition()
    {
        return goal != null ? goal.position : goalPoint;
    }

    void FixedUpdate()
    {
        if (solver == null || hipTarget == null || solver.hips.rb == null) return;

        Vector3 hips = solver.hips.rb.position;
        Vector3 target = hipTarget.position;

        Vector3 toGoal = GoalPosition() - hips;
        toGoal.y = 0f;
        arrived = toGoal.magnitude <= arriveRadius;

        // while the capture point is outside the feet, chasing the goal only makes the fall worse.
        // pull the carrot back over the body so the solver spends everything on catching itself.
        bool recovering = haltWhenOffBalance && solver.offBalance;
        if (recovering || arrived)
        {
            travel = Vector3.zero;
            Vector3 home = new Vector3(hips.x, target.y, hips.z);
            float rate = (recovering ? recoverySpeed : walkSpeed) * Time.fixedDeltaTime;
            target = Vector3.MoveTowards(target, home, rate);
        }
        else
        {
            travel = toGoal.normalized * walkSpeed;
            target += travel * Time.fixedDeltaTime;
        }

        target = Leash(target, hips);
        target = RideHeight(target);

        hipTarget.position = target;
        hipTarget.rotation = Heading(hipTarget.rotation, toGoal);

        // foot placement reads this. it is what turns "go there" into a step that pushes.
        solver.desiredVelocity = travel;

        Debug.DrawLine(hips, hipTarget.position, Color.cyan);
        Debug.DrawLine(hipTarget.position, GoalPosition(), arrived ? Color.green : Color.white);
    }

    // keep the carrot within reach of the body, so the hip spring stays a walk and not a tow rope
    Vector3 Leash(Vector3 target, Vector3 hips)
    {
        Vector3 lead = target - hips;
        lead.y = 0f;
        if (lead.magnitude > maxLead) lead = lead.normalized * maxLead;
        return new Vector3(hips.x + lead.x, target.y, hips.z + lead.z);
    }

    // carry the target a fixed height over whatever ground is under it, so slopes and steps do not
    // need the goal to be authored at the right altitude
    Vector3 RideHeight(Vector3 target)
    {
        RaycastHit hit;
        float span = rideHeight * 3f;
        if (solver.GroundCast(target + Vector3.up * rideHeight, span, out hit))
        {
            target.y = hit.point.y + rideHeight;
        }
        return target;
    }

    // face where we are going, but only ever turn at turnSpeed so the body leans into it
    Quaternion Heading(Quaternion current, Vector3 toGoal)
    {
        if (travel.sqrMagnitude < 1e-4f || toGoal.sqrMagnitude < 1e-4f) return current;
        Quaternion wanted = Quaternion.LookRotation(toGoal.normalized, Vector3.up);
        return Quaternion.RotateTowards(current, wanted, turnSpeed * Time.fixedDeltaTime);
    }
}
