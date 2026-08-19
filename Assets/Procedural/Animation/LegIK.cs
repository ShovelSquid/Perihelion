// Hinge-constrained CCD for a leg chain.
//
// The bones here are near enough to hinges — the ConfigurableJoints allow a wide swing about their
// primary axis and almost nothing about the other two — so a general 3D IK would spend its time
// proposing rotations physx then refuses. Solving on the hinge axis directly means every pose it
// produces is one the rig can actually hold.
//
// Nothing in here touches the physical rig. It solves on scratch state from the rest pose and hands
// back world rotations; LegSolver's pose servo is what drives the real bones there. That keeps the
// solve deterministic and immune to whatever the physics is doing this frame.
using UnityEngine;

public static class LegIK
{
    public struct Link
    {
        public Vector3 offsetFromParent;    // where this joint sits, in the parent's rotation frame
        public Quaternion restFromParent;   // its orientation at rest, relative to the parent
        public Vector3 hingeAxis;           // the axis it actually swings about, in its own frame
        public Vector2 limit;               // how far it may swing, degrees
    }

    // Sweep from the joint nearest the tip back toward the root. At each one, swing the tip as far
    // toward the goal as that hinge allows, then move on. A handful of passes converges.
    // angles carries over between frames, so a goal that moves smoothly gives a pose that does too.
    public static void Solve(Link[] links, float[] angles, Vector3[] pos, Quaternion[] rot,
                             Vector3 basePos, Quaternion baseRot, Vector3 goal, int iterations)
    {
        int count = links.Length;
        if (count == 0) return;
        int tip = count - 1;

        for (int pass = 0; pass < iterations; pass++)
        {
            // the tip joint has no reach of its own, so start one in from it
            for (int i = tip - 1; i >= 0; i--)
            {
                Forward(links, angles, basePos, baseRot, pos, rot);

                Vector3 axis = rot[i] * links[i].hingeAxis;
                Vector3 toTip = Flatten(pos[tip] - pos[i], axis);
                Vector3 toGoal = Flatten(goal - pos[i], axis);
                if (toTip.sqrMagnitude < 1e-8f || toGoal.sqrMagnitude < 1e-8f) continue;

                float swing = Vector3.SignedAngle(toTip, toGoal, axis);
                angles[i] = Mathf.Clamp(angles[i] + swing, links[i].limit.x, links[i].limit.y);
            }
        }

        Forward(links, angles, basePos, baseRot, pos, rot);
    }

    // walk the chain out from the hips, accumulating each joint's rest pose plus its hinge angle
    static void Forward(Link[] links, float[] angles, Vector3 basePos, Quaternion baseRot,
                        Vector3[] pos, Quaternion[] rot)
    {
        Vector3 point = basePos;
        Quaternion frame = baseRot;
        for (int i = 0; i < links.Length; i++)
        {
            point += frame * links[i].offsetFromParent;
            frame = frame * links[i].restFromParent * Quaternion.AngleAxis(angles[i], links[i].hingeAxis);
            pos[i] = point;
            rot[i] = frame;
        }
    }

    // drop the component along the hinge, since a hinge can only move things in its own plane
    static Vector3 Flatten(Vector3 v, Vector3 axis)
    {
        return v - axis * Vector3.Dot(v, axis);
    }
}
