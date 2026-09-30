using UnityEngine;
/// <summary>
/// Renders a health bar above the character in the world space.
/// Sets the position of the heatlhbar by raycasting through the characters collider towards the camera,
/// and then getting the top right point along the collider relative to the camera position.
/// </summary>
/// 
/// relative to camera up.

public class SetHealthbarAnchor : MonoBehaviour
{
    private HitbarManager hitbarManager;
    public CapsuleCollider displayCol;
    private Camera mainCamera; 
    [Tooltip("The direction vector used for raycasting to position the health bar relative to the camera.")]
    public Vector2 healthbarRayVector = new Vector2(1, 1);
    public Transform healthbarAnchor; // world-space base position for the healthbar
    public bool active = false;
    public bool hovering = false;

    private void Awake()
    {
        hitbarManager = FindObjectOfType<HitbarManager>();
        if (healthbarAnchor == null)
        {
            healthbarAnchor = new GameObject("HealthbarAnchor").transform;
            healthbarAnchor.SetParent(transform);
            healthbarAnchor.localPosition = Vector3.zero;
        }
        // add display collider where the health bar will be anchored relative to the character
        // we're turning it off and just using its radius for efficiency, it exists to be visually repositioned.

        // col = displayCol;
        mainCamera = Camera.main;
    }

    // LateUpdate so the camera has already moved this frame (CameraController runs in FixedUpdate/LateUpdate),
    // otherwise the bar lags one frame behind and jitters.
    private void LateUpdate()
    {
        // Logic to update the health bar position relative to the character and camera would go here.

        // get vector towards camera, get camera up vector, raycast through collider along healthbarRayVector to get healthbar base position transform.
        // Which then gets screen positioned so that it doesn't shrink or get too massive, and is used for the UI.

        if (displayCol == null || mainCamera == null || healthbarAnchor == null) return;

        // Read center/radius from the collider's own fields, not bounds: bounds is zero while the collider is disabled.
        Transform colT = displayCol.transform;
        Vector3 center = colT.TransformPoint(displayCol.center);
        Vector3 scale = colT.lossyScale;
        float radius = displayCol.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
        Transform cam = mainCamera.transform;

        // Direction in the camera's screen plane, so "top right" means top right as the player sees it.
        Vector3 dir = (cam.right * healthbarRayVector.x + cam.up * healthbarRayVector.y).normalized;

        healthbarAnchor.position = center + dir * radius;
    }
}
