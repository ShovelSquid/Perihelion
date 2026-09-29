using UnityEngine;

// Pure physics driver: pushes one Rigidbody toward whatever pose it is handed.
// HandRig decides the pose (hand socket or aimTarget) and calls Drive each physics step.
public class AimItem : MonoBehaviour
{
    public Rigidbody rb;
    public float aimForceFar = 600f;
    public float aimForceNear = 100f;
    public float maxDistance = 5f;
    public float aimForceRotation = 10f;
    public float aimDamp = 1f;


    public void Attach(Rigidbody body)
    {
        rb = body;
        if (body != null) body.interpolation = RigidbodyInterpolation.Interpolate;
    }

    public void Detach()
    {
        rb = null;
    }

    public void CopyTuning(AimItem other)
    {
        if (other == null) return;
        aimForceFar = other.aimForceFar;
        aimForceNear = other.aimForceNear;
        maxDistance = other.maxDistance;
        aimForceRotation = other.aimForceRotation;
        aimDamp = other.aimDamp;
    }

    void ClampToTarget(Vector3 pos)
    {
        Vector3 offset = rb.position - pos;
        float dist = offset.magnitude;
        if (dist <= maxDistance) return;
        Vector3 dir = offset / dist;
        rb.position = pos + dir * maxDistance;
        float outwaredVelocity = Vector3.Dot(rb.linearVelocity, dir);
        if (outwaredVelocity > 0f)
        {
            rb.linearVelocity -= dir * outwaredVelocity;
        }
    }

    Vector3 TorqueTowards(Quaternion targetRot)
    {
        Quaternion delta = targetRot * Quaternion.Inverse(rb.rotation);

        // quaternions double-cover: q and -q are the same rotation, but one
        // describes the long way around. Force the short path.
        if (delta.w < 0f) { delta.x = -delta.x; delta.y = -delta.y; delta.z = -delta.z; delta.w = -delta.w; }

        delta.ToAngleAxis(out float angle, out Vector3 axis);
        if (angle < 0.001f || !float.IsFinite(axis.x)) return Vector3.zero;

        Vector3 error = axis.normalized * (angle * Mathf.Deg2Rad);

        float k = aimForceRotation;
        float c = 2f * aimDamp * Mathf.Sqrt(k);
        return error * k - rb.angularVelocity * c;
    }


    public void Drive(Vector3 targetPosition, Quaternion targetRotation)
    {
        if (rb == null) return;
        // add force to rigidbody towards aimtarget or holdtarget
        // Error is measured from rb.position, not the item transform: with interpolation on,
        // the transform is the interpolated render pose and rb.position is the physics pose.
        Vector3 toTarget = targetPosition - rb.position;
        float aimForce = toTarget.magnitude > maxDistance ? aimForceFar : aimForceNear;
        float c = 2f * aimDamp * Mathf.Sqrt(aimForce);
        rb.AddForce(toTarget * aimForce - (c * rb.linearVelocity));
        rb.AddTorque(TorqueTowards(targetRotation));
        ClampToTarget(targetPosition);
    }
}
