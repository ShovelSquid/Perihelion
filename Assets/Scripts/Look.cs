using UnityEngine;
using Unity.Mathematics;

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
    public float lookLerpSpeed;

    private Quaternion targetRotation;

    void Start()
    {
        if (swivel == null) swivel = transform;
        targetRotation = swivel.rotation;
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
        if (looking)
        {
            float sensitivity = controller ? controllerAimSpeed : aimSpeed;
            Vector3 euler = targetRotation.eulerAngles;
            euler.y += lookDirection.x * sensitivity * Time.deltaTime;
            euler.x -= lookDirection.y * sensitivity * Time.deltaTime;
            if (clampPitch)
            {
                float pitch = euler.x;
                if (pitch > 180f) pitch -= 360f;
                pitch = math.clamp(pitch, pitchClamp.x, pitchClamp.y);
                euler.x = pitch;
            }
            targetRotation = Quaternion.Euler(euler);
        }
        swivel.rotation = Quaternion.Slerp(swivel.rotation, targetRotation, lookLerpSpeed * Time.deltaTime);
    }

    void LateUpdate()
    {
        if (target != null) swivel.position = target.position;
    }
}
