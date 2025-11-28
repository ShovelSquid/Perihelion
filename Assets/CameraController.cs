using System.Collections.Generic;
using JetBrains.Annotations;
using Unity.Mathematics;
using UnityEngine;

public class CameraController : MonoBehaviour
{
    public GameObject player;
    public Transform platform;
    public List<Transform> highlights;     // use this later
    public Transform lerpTarget;
    [Header("Third Person Mode")]
    public bool thirdPersonMode;
    public bool weirdThirdPersonMode;
    public float thirdPersonDistance;
    public Quaternion thirdPersonRotation;
    private float yawAngle = 0f;   // Horizontal rotation
    private float pitchAngle = 0f; // Vertical rotation
    [Header("Camera Settings")]
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

    [Header("Camera Collision")]
    public LayerMask collisionLayers; // Set this to what the camera should collide with (e.g., walls, terrain)
    public float collisionRadius = 0.3f; // Sphere radius for collision detection
    public float collisionPadding = 0.2f; // Extra distance to keep from walls
    public float collisionHeightOffset = 1.0f; // Height offset from the player position
    public float collisionSmoothSpeed = 10f; // How fast camera moves when avoiding collision
    
    private float currentDistance; // The actual distance after collision adjustment
    
    void Start()
    {
        currentDistance = distance;
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
        if (weirdThirdPersonMode)
        {
            CalculateThirdPersonLerpTarget();
            return;
        }
        if (thirdPersonMode)
        {
            CalculateThirdPersonLerpTarget();
            return;
        }
        lerping = true;
        Vector3 platPos = Vector3.zero;
        Quaternion platRot = Quaternion.identity;
        if (attachToPlat && platform != null)
        {
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
            if (attachToPlayer && attachToPlat)
            {
                playerWeight = Mathf.Clamp(Mathf.InverseLerp(0f, platformRadius, Vector3.Distance(platform.position, player.transform.position)), 0.5f, 1f);
            }

            
            // Calculate desired camera position
            // Vector3 desiredPosition = player.transform.position + (Quaternion.Euler(0, angle, 0) * Vector3.forward * distance) + (Vector3.up * height);
            
            // Check for collisions and adjust distance
            // currentDistance = CheckCameraCollision(player.transform.position + (Vector3.up * height), desiredPosition);
            
            // Use adjusted distance
            playerPos = player.transform.position + (Quaternion.Euler(0, angle, 0) * Vector3.forward * distance) + (Vector3.up * height);
            playerRot = Quaternion.LookRotation(player.transform.position - playerPos, Vector3.up);
        }
        if (looking)
        {
            DoLook();
        }
        lerpTarget.position = Vector3.Lerp(platPos, playerPos, playerWeight);
        lerpTarget.rotation = Quaternion.Slerp(platRot, playerRot, playerWeight);
    }

    public void CalculateThirdPersonLerpTarget()
    {
        lerping = true;
        Vector3 playerPos = player.transform.position;
        Quaternion playerRot = Quaternion.identity;
        if (player != null)
        {
            playerPos = player.transform.position - (thirdPersonRotation * Vector3.forward * thirdPersonDistance);
            // playerRot = Quaternion.LookRotation(player.transform.position - playerPos, Vector3.up);
        }
        if (looking)
        {
            DoLook();
        }
        lerpTarget.position = playerPos;
        lerpTarget.rotation = thirdPersonRotation;
    }


    
    private float CheckCameraCollision(Vector3 targetPoint, Vector3 desiredCameraPos)
    {
        // Direction from target (player) to camera
        Vector3 direction = (desiredCameraPos - targetPoint).normalized;
        float desiredDistance = Vector3.Distance(targetPoint, desiredCameraPos);
        
        // Perform a spherecast from player to camera position
        RaycastHit hit;
        if (Physics.SphereCast(targetPoint, collisionRadius, direction, out hit, desiredDistance, collisionLayers))
        {
            // Something is blocking the camera, move it closer
            float adjustedDistance = hit.distance - collisionPadding;
            
            // Smoothly transition to the new distance
            return Mathf.Lerp(currentDistance, Mathf.Max(distanceMin, adjustedDistance), collisionSmoothSpeed * Time.deltaTime);
        }
        else
        {
            // Nothing blocking, smoothly return to desired distance
            return Mathf.Lerp(currentDistance, distance, collisionSmoothSpeed * Time.deltaTime);
        }
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
        if (weirdThirdPersonMode)
        {
            // Rotate the third person rotation by look input
            float yaw = lookDirection.x * sensitivity * Time.deltaTime;   // Horizontal rotation
            float pitch = -lookDirection.y * sensitivity * Time.deltaTime; // Vertical rotation (inverted)
            
            // Apply rotation: yaw around world up, pitch around local right
            thirdPersonRotation *= Quaternion.Euler(pitch, yaw, 0f);
            
            // Optional: Clamp pitch to prevent camera from flipping upside down
            Vector3 euler = thirdPersonRotation.eulerAngles;
            euler.x = ClampAngle(euler.x, -80f, 80f);
            thirdPersonRotation = Quaternion.Euler(euler);
            return;
        }
        if (thirdPersonMode)
        {
            // Accumulate angles independently
            yawAngle += lookDirection.x * sensitivity * Time.deltaTime;
            pitchAngle -= lookDirection.y * sensitivity * Time.deltaTime;
            
            // Clamp pitch to prevent flipping
            pitchAngle = Mathf.Clamp(pitchAngle, -80f, 80f);
            
            // Wrap yaw
            if (yawAngle < 0) yawAngle += 360;
            if (yawAngle >= 360) yawAngle -= 360;
            
            // Build rotation: yaw around WORLD up, then pitch around local right
            thirdPersonRotation = Quaternion.Euler(pitchAngle, yawAngle, 0f);
            return;
        }
        angle += lookDirection.x * sensitivity * Time.deltaTime;
        baseHeight -= lookDirection.y * sensitivity * Time.deltaTime;
        baseHeight = Mathf.Clamp(baseHeight, heightMin, heightMax);
        if (angle < 0) angle += 360;
        if (angle >= 360) angle -= 360;
    }

    // Helper method to properly clamp euler angles
    private float ClampAngle(float angle, float min, float max)
    {
        if (angle > 180f) angle -= 360f;
        return Mathf.Clamp(angle, min, max);
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
