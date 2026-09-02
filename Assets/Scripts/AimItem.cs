using UnityEngine;

public class AimItem : MonoBehaviour
{
    public Rigidbody rb;
    public Transform HandLIKTarget;
    public Transform HandRIKTarget;
    public Item item;
    public bool aiming;
    public float aimForce = 10f;
    public float aimForceRotation = 10f;
    public float aimDamp = 1f;


    public void Aim()
    {
        aiming = true;
    }
    public void StopAiming()
    {
        aiming = false;
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


    public void FixedUpdate()
    {
        // add force to rigidbody towards aimtarget or holdtarget
        float c = 2f * aimDamp * Mathf.Sqrt(aimForce);
        if (aiming)
        {
            rb.AddForce((item.aimTarget.position - item.transform.position)*aimForce- (c * rb.linearVelocity));
            rb.AddTorque(TorqueTowards(item.aimTarget.rotation));
        }
        else
        {
            rb.AddForce((item.holdTarget.position - item.transform.position) * aimForce - (c * rb.linearVelocity));
            rb.AddTorque(TorqueTowards(item.holdTarget.rotation));
        }

        if (item.equipInfo.leftHand)
        {
            HandLIKTarget.position = item.handL.position;
            HandLIKTarget.rotation = item.handL.rotation;
        }
        if (item.equipInfo.rightHand)
        {
            HandRIKTarget.position = item.handR.position;
            HandRIKTarget.rotation = item.handR.rotation;
        }

    }
}
