using UnityEngine;

public class Look : MonoBehaviour
{
    [Header("Swivel")]
    public Transform swivel;
    public Transform target;
    public bool looking;
    public Vector2 lookDirection;

    [Header("Sensitivity")]
    public float aimSpeed;
    public float controllerAimSpeed;
    public bool controller;

    [Header("Pitch Clamp")]
    public bool clampPitch = true;
    public Vector2 pitchClamp = new Vector2(-89.9f, 89.9f);

    [Header("Lerp")]
    public float lookLerpSpeed; // per second; sharpness of a frame-rate independent exponential ease toward the look target (was a per-frame factor, ~10 feels close). 0 or less snaps to the target, no smoothing

    // Look is kept as accumulated angles, not a rotation: a quaternion forgets how many turns it has wound,
    // so easing toward one takes the short way round and a fast 360 flick turns back or stalls.
    private float targetYaw; // degrees, unwrapped; input adds to it
    private float targetPitch; // degrees, clamped on input when clampPitch
    private float currentYaw; // eases toward targetYaw; what the swivel shows
    private float currentPitch; // eases toward targetPitch; what the swivel shows

    void Start()
    {
        if (swivel == null) swivel = transform;
        Vector3 euler = swivel.eulerAngles;
        float pitch = euler.x;
        if (pitch > 180f) pitch -= 360f;
        targetYaw = euler.y;
        currentYaw = euler.y;
        targetPitch = pitch;
        currentPitch = pitch;
        // swivel.SetParent(null, true);
    }

    public void SetLookDirection(Vector2 input, bool isController)
    {
        lookDirection = input;
        controller = isController;
        looking = input != Vector2.zero;
    }

    void Update()
    {
    }

    void LateUpdate()
    {
        if (looking)
        {
            float sensitivity = controller ? controllerAimSpeed : aimSpeed;
            targetYaw += lookDirection.x * sensitivity * Time.deltaTime;
            targetPitch -= lookDirection.y * sensitivity * Time.deltaTime;
            if (clampPitch) targetPitch = Mathf.Clamp(targetPitch, pitchClamp.x, pitchClamp.y);
        }

        if (lookLerpSpeed <= 0f)
        {
            currentYaw = targetYaw;
            currentPitch = targetPitch;
        }
        else
        {
            // Frame-rate independent: the same share of the gap closes per second at any fps.
            float t = 1f - Mathf.Exp(-lookLerpSpeed * Time.deltaTime);
            currentYaw = Mathf.Lerp(currentYaw, targetYaw, t);
            currentPitch = Mathf.Lerp(currentPitch, targetPitch, t);
        }

        // Keep the floats small so precision holds over long sessions. Both shift by the same whole turns,
        // so the gap (any spin still owed) is kept and the displayed rotation doesn't change.
        if (Mathf.Abs(currentYaw) > 360f)
        {
            float wrap = Mathf.Floor(currentYaw / 360f) * 360f;
            currentYaw -= wrap;
            targetYaw -= wrap;
        }

        swivel.rotation = Quaternion.Euler(currentPitch, currentYaw, 0f);
        if (target != null) swivel.position = target.position;
    }
}
