using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public class CameraController : MonoBehaviour
{
    public GameObject player;
    public Transform platform;
    public List<Transform> highlights;     // use this later
    public Transform lerpTarget;
    public float platformRadius;
    public float distance;
    public float distanceMax;
    public float distanceMin;
    public float baseHeight;
    public float height;
    public float heightMax;
    public float heightMin;
    // clamp this to be normalized
    public float angle;
    public float lerpSpeed;
    public float lerpRotationSpeed;
    public bool lerping;
    public bool attachToPlat;
    public bool attachToPlayer;
    [Range(0f, 1f)]
    public float playerWeight;
    public bool rotateLeft;
    public bool rotateRight;
    public float rotateSpeed;
    [Range(0.01f, 300f)]
    public float lookSensitivity;
    [Range(0.01f, 300f)]
    public float controllerLookSensitivity;
    public bool controller = true;
    public Vector2 lookDirection;
    public bool looking;
    public float terminalVelocity;

    void Start()
    {
        CalculateLerpTarget();
        if (player != null)
        {
            Move mv = player.GetComponent<Move>();
            mv.onEnterPlat.AddListener(PlayerEnterPlatform);
            mv.onExitPlat.AddListener(PlayerExitPlatform);
        }
    }

    void OnValidate()
    {
        angle = Mathf.Clamp(angle, 0, 360);
        distance = Mathf.Clamp(distance, distanceMin, distanceMax);
        playerWeight = Mathf.Clamp01(playerWeight);
        CalculateLerpTarget();
    }

    public void CalculateLerpTarget()
    {
        lerping = true;
        Vector3 platPos = Vector3.zero;
        Quaternion platRot = Quaternion.identity;
        if (attachToPlat && platform != null)
        {
            // Vector3 directionToCamera = (platform.position - transform.position).normalized;
            platPos = platform.position + (Quaternion.Euler(0, angle, 0) * Vector3.forward * distance) + (Vector3.up * height);
            platRot = Quaternion.LookRotation(platform.position - platPos, Vector3.up);
        }
        Vector3 playerPos = Vector3.zero;
        Quaternion playerRot = Quaternion.identity;
        if (attachToPlayer && player != null)
        {
            Move mv = player.GetComponent<Move>();
            if (mv.inAir)
            {
                height = Mathf.Clamp(math.remap(0f, terminalVelocity, baseHeight, heightMax, mv.fallSpeed), baseHeight, heightMax);
            }
            else
            {
                height = baseHeight;
            }
            playerPos = player.transform.position + (Quaternion.Euler(0, angle, 0) * Vector3.forward * distance) + (Vector3.up * height);
            playerRot = Quaternion.LookRotation(player.transform.position - playerPos, Vector3.up);
        }
        if (attachToPlayer && attachToPlat)
        {
            playerWeight = Mathf.Clamp(Mathf.InverseLerp(0f, platformRadius, Vector3.Distance(platform.position, player.transform.position)), 0.5f, 1f);
        }
        if (looking)
        {
            DoLook();
        }
        lerpTarget.position = Vector3.Lerp(platPos, playerPos, playerWeight);
        lerpTarget.rotation = Quaternion.Slerp(platRot, playerRot, playerWeight);
    }

    void FixedUpdate()
    {
        CalculateLerpTarget();
        if (lerping)
        {
            if (Vector3.Distance(transform.position, lerpTarget.position) < 0.01f && Vector3.Distance(transform.rotation.eulerAngles, lerpTarget.rotation.eulerAngles) < 0.01f)
            {
                transform.position = lerpTarget.position;
                transform.rotation = lerpTarget.rotation;
                lerping = false;
            }
            else
            {
                transform.position = Vector3.Lerp(transform.position, lerpTarget.position, lerpSpeed * Time.deltaTime);
                transform.rotation = Quaternion.Slerp(transform.rotation, lerpTarget.rotation, lerpRotationSpeed * Time.deltaTime);
            }
        }
        if (rotateLeft && rotateRight)
        {
            // do nothing
        }
        else
        {
            if (rotateLeft)
            {
                angle -= rotateSpeed * Time.deltaTime;
                if (angle <= 0) angle += 360;
            }
            if (rotateRight)
            {
                angle += rotateSpeed * Time.deltaTime;
                if (angle >= 360) angle -= 360;
            }
        }
    }


    public void PlayerEnterPlatform(Collider platform)
    {
        Debug.Log("Player entered platform: " + platform.name);
        // Attach the camera to the platform
        attachToPlat = true;
        this.platform = platform.transform;
        platformRadius = platform.bounds.extents.magnitude;
        CalculateLerpTarget();
    }

    public void PlayerExitPlatform(Collider platform)
    {
        Debug.Log("Player exited platform: " + platform.name);
        // Detach the camera from the platform
        attachToPlat = false;
        this.platform = null;
        playerWeight = 1f;
        CalculateLerpTarget();
    }

    public void OnLook(Vector2 lookInput, bool isController)
    {
        lookDirection = lookInput;
        if (lookDirection != Vector2.zero)
        {
            looking = true;
            controller = isController;
        }
        else
        {
            looking = false;
        }
    }

    private void DoLook()
    {
        float sensitivity = lookSensitivity;
        if (controller)
        {
            sensitivity = controllerLookSensitivity;
        }
        angle += lookDirection.x * sensitivity * Time.deltaTime;
        baseHeight -= lookDirection.y * sensitivity * Time.deltaTime;
        baseHeight = Mathf.Clamp(baseHeight, heightMin, heightMax);
        if (angle < 0) angle += 360;
        if (angle >= 360) angle -= 360;

    }
    
    public void OnRotateLeft(bool on)
    {
        Debug.Log("camera working");
        if (on)
        {
            rotateLeft = true;
        }
        else
        {
            rotateLeft = false;
        }
    }
    public void OnRotateRight(bool on)
    {
        Debug.Log("camera working");
        if (on)
        {
            rotateRight = true;
        }
        else
        {
            rotateRight = false;
        }
    }
}
