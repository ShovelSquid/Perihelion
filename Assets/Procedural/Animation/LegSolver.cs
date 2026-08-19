using UnityEngine;
using System.Collections.Generic;

public class Leg
{
    public Transform hipSocket;     // where it connects to the hip
    public Rigidbody foot;          // the foot rigidbody
    public Transform footTarget;    // the foot IK target
}

public class LegSolver : MonoBehaviour
{
    public Rigidbody hip;
    public Transform hipTarget;
    public List<Leg> legs = new List<Leg>();

    // need to get offset of rest pose feet to hip to get desired pose 

    // need to calculate where hip currently is in relation to where it should be

    // need to calculate where hip currently should be for balance

    // need to determine which foot is best to move

    // need to determine where to move that foot

    // need to create an arc of travel from current position to desired position

    // need to calculate how forces are affected on foot based off of angle and distance
            // affects foot desirability to move
    // need to calculate not just current position but also velocity

    // 

}