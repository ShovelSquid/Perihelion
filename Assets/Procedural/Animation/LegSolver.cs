using UnityEngine;
using System.Collections.Generic;

public class Leg
{
    public Transform hipSocket;     // where it connects to the hip
    public Rigidbody foot;          // the foot rigidbody
    public Transform footTarget;    // the foot IK target
    public Transform footRestTarget;    // the rest target of the foot pose
    public float maxRadius;
    public float restRadius;

    public float DistanceToRest()
    {
        // return a score of how far the foot is from the foot rest target.
        return Vector3.Distance(footTarget.position, footRestTarget.position);
    }

    public float ComfortLevel()
    {
        // is leg buckling??
        // i.e.; is leg behind hip socket relative to current hip velocity?
        return DistanceToRest();
    }
}

public class LegSolver : MonoBehaviour
{
    public Rigidbody hip;
    public Vector3 hipVelocity;         // try to keep this ZERO.
    public Vector3 hipAngularVelocity;  // likewise, keep this zero.
    public float hipTargetHeight;       // target height from FLOOR, NOT from world y. gabbagool.
    public Transform hipTarget;
    public List<Leg> legs = new List<Leg>();
    public int minLegsGrounded;        // the amount of legs needed to be on the ground when moving

    // need to get offset of rest pose feet to hip to get desired pose

    public Awake()
    {
        float floor = 0;
        foreach (Leg leg in legs)
        {
            floor += leg.foot.position.y;
        }
        floor /= legs.Count;
        hipTargetHeight = hip.position.y - floor;       // get distance between feet height and hip height. that's the ideal height.
    }

    public Leg BestLegToMove()
    {
        // every time I write code I feel like I gain xp. it's so bomb yo
        Leg l = legs[0];
        foreach(Leg leg in legs)
        {
            if (leg.ComfortLevel() > l.ComfortLevel())
            {
                l = leg;
            }
        }
        return l;
    }

    public void RotateHip()
    {
        // not sure how this function should work but the hips should be able to rotate.
    }

    public void UpdateFootForce(Leg leg)
    {
        // get other legs/foot forces, hip velocity, and update the foot force to achieve desired hip velocity.
    }

    public Transform CalculateFootTarget(Leg leg)
    {
        // get current hip velocity, as well as hip socket. CAN'T cross leg; beware of ray from hip socket to foot of other legs.
        // note obstacles in way of path? other legs, walls, etc? recalculate until target is found?


        // get desired foot target from raycast from hip socket to area that counters current hip velocity
        Vector3 footTarget;

        // generate foot trajectory; path that foot will take, acounting for desired foot movement height, time, etc.
            // use bulletmanager?
            // also: does it do a trajectory directly to the target, or does it overshoot/correct? 2nd feels more realx
        // check if any obstacles (including self legs) are along foot trajectory
            // either regen or solve trajectory to be separate? enter state of checking?
        l = IsCrossingLegs(leg.hipSocket, footTarget);
        if (l != null)
        {
            // note l and course correct, 
        }
    }

    public Leg IsCrossingLegs(Vector3 hipSocket, Vector3 footTarget)
    {
        // check ray from hipsocket to foot target, make sure it doesn't overlap with any other leg rays. type shit
    }

    public float LegBuckling(Leg leg)
    {
        // get hip socket position, foot position, and current hip velocity/position.
        // if foot is behind hip socket relative to hip/hip velocity, it is buckling.
        // I'm not sure if this is the definition of buckling tbh, it's just in an awkward pos.
    }



    // need to determine which foot is best to move

    // need to determine where to move that foot

    // need to create an arc of travel from current position to desired position

    // need to calculate how forces are affected on foot based off of angle and distance
            // affects foot desirability to move
    // need to calculate not just current position but also velocity

    // 

}