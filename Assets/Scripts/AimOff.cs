using UnityEngine;

public class AimOff : MonoBehaviour
{
    public Animator anim;
    public Transform aimTarget;    
    public Transform aimPivot; // Chest/shoulder bone; falls back to this transform (feet) if unset
    public float aimSharpness = 10f; // per second; how fast the aim blend values chase their targets, framerate independent

    [Header("Body Aim (AimX / AimY)")]
    public float maxYaw = 90f;   // Degrees the AimX = ±1 clips actually turn
    public float maxPitch = 60f; // Degrees the AimY = ±1 clips actually turn
    // Remap normalized angle (-1..1 of max) to blend value; bend these to cancel blend-tree drift
    public AnimationCurve yawCurve = AnimationCurve.Linear(-1f, -1f, 1f, 1f);
    public AnimationCurve pitchCurve = AnimationCurve.Linear(-1f, -1f, 1f, 1f);

    [Header("Arm Aim (ArmAimX / ArmAimY)")]
    public float armMaxYaw = 150f;   // Wider than body so the arm keeps tracking after the body saturates
    public float armMaxPitch = 85f;
    public AnimationCurve armYawCurve = AnimationCurve.Linear(-1f, -1f, 1f, 1f);
    public AnimationCurve armPitchCurve = AnimationCurve.Linear(-1f, -1f, 1f, 1f);

    float aimX;
    float aimY;
    float armAimX;
    float armAimY;

    void Update()
    {
        // must offset the aim direction based on the character's orientation or pivot point
        // facing character's forward direction
        Transform pivot = aimPivot != null ? aimPivot : transform;
        Vector3 aimDirection = aimTarget.position - pivot.position;
        Vector3 local = transform.InverseTransformDirection(aimDirection);

        float yaw = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
        float pitch = Mathf.Atan2(local.y, new Vector2(local.x, local.z).magnitude) * Mathf.Rad2Deg;

        aimX = Remap(yaw, maxYaw, yawCurve);
        aimY = Remap(pitch, maxPitch, pitchCurve);
        armAimX = Remap(yaw, armMaxYaw, armYawCurve);
        armAimY = Remap(pitch, armMaxPitch, armPitchCurve);

        anim.SetFloat("AimX", Lerp(anim.GetFloat("AimX"), aimX, Time.deltaTime));
        anim.SetFloat("AimY", Lerp(anim.GetFloat("AimY"), aimY, Time.deltaTime));
        anim.SetFloat("ArmAimX", Lerp(anim.GetFloat("ArmAimX"), armAimX, Time.deltaTime));
        anim.SetFloat("ArmAimY", Lerp(anim.GetFloat("ArmAimY"), armAimY, Time.deltaTime));
    }

    // Clamp before evaluating so the curve only ever sees its authored -1..1 domain
    static float Remap(float angle, float maxAngle, AnimationCurve curve)
    {
        return curve.Evaluate(Mathf.Clamp(angle / maxAngle, -1f, 1f));
    }
    float Lerp(float current, float target, float deltaTime)
    {
        return Mathf.Lerp(current, target, 1f - Mathf.Exp(-aimSharpness * deltaTime));
    }

}
