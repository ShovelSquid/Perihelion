using UnityEngine;

// Keeps a screen-space UI element on top of a world point, at a constant screen size.
// Shared by healthbars (target = SetHealthbarAnchor.healthbarAnchor) and aim cursors (SetWorldPoint every frame).
// Runs late so the camera, SetHealthbarAnchor and HandRig have all moved this frame before we project;
// projecting earlier puts the element one frame behind the camera and it jitters.
// Supports Screen Space Overlay and Screen Space Camera canvases.
[DefaultExecutionOrder(200)]
[RequireComponent(typeof(RectTransform))]
public class ScreenAnchor : MonoBehaviour
{
    [Header("World Anchor")]
    public Transform target; // followed when set; otherwise the point from SetWorldPoint is used
    public Vector3 worldOffset; // world-space offset added to the anchor
    [Header("Screen")]
    public Vector2 screenOffset; // canvas units (pixels at canvas scale 1) added after projection
    public bool clampToScreen = false; // keep the element on screen instead of letting it slide off
    public float edgePadding = 24f; // canvas units kept between a clamped element and the screen edge
    public Camera cam; // defaults to Camera.main

    public bool IsVisible { get; private set; }
    public Vector2 ScreenPoint { get; private set; } // pixels, bottom-left origin, after off-screen handling

    RectTransform rect;
    RectTransform parentRect;
    Canvas canvas;
    CanvasGroup group; // optional; hiding fades it out without disabling this component
    Vector3 manualPoint;
    bool hasManualPoint;

    void Awake()
    {
        rect = GetComponent<RectTransform>();
        parentRect = rect.parent as RectTransform;
        canvas = GetComponentInParent<Canvas>();
        group = GetComponent<CanvasGroup>();
        if (cam == null) cam = Camera.main;
        if (canvas == null) Debug.LogWarning($"{name}: ScreenAnchor must sit under a Canvas.", this);
    }

    public void SetWorldPoint(Vector3 point)
    {
        manualPoint = point;
        hasManualPoint = true;
    }

    public void ClearWorldPoint()
    {
        hasManualPoint = false;
    }

    // Converts an angle away from the view direction into canvas units, for sizing things like bloom rings.
    // Exact at the screen center and close enough near it; perspective cameras only.
    public float AngleToCanvasUnits(float degrees)
    {
        if (cam == null) return 0f;
        float focalPixels = (cam.pixelHeight * 0.5f) / Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float pixels = Mathf.Tan(degrees * Mathf.Deg2Rad) * focalPixels;
        return canvas != null ? pixels / canvas.scaleFactor : pixels;
    }

    void LateUpdate()
    {
        if (cam == null || canvas == null || parentRect == null) return;
        if (target == null && !hasManualPoint)
        {
            SetVisible(false);
            return;
        }

        Vector3 world = (target != null ? target.position : manualPoint) + worldOffset;
        Vector3 projected = cam.WorldToScreenPoint(world);
        Vector2 screen = new Vector2(projected.x, projected.y);
        bool inFront = projected.z > 0f;

        bool visible = ResolveOffscreen(ref screen, inFront);
        ScreenPoint = screen;
        SetVisible(visible);
        if (!visible) return;

        Camera uiCam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screen, uiCam, out Vector2 local))
        {
            rect.localPosition = new Vector3(local.x + screenOffset.x, local.y + screenOffset.y, 0f);
        }
    }

    // Decides what happens when the anchor is behind the camera or outside the screen.
    // `screen` is in pixels (bottom-left origin) and may be edited; return false to hide the element.
    // Note: for points behind the camera, WorldToScreenPoint returns a mirrored position.
    bool ResolveOffscreen(ref Vector2 screen, bool inFront)
    {
        // TODO(human): off-screen policy (hide vs clamp to the edge using clampToScreen / edgePadding)
        if (inFront == false) return false;
        if (screen.x < 0f || screen.x > Screen.width || screen.y < 0f || screen.y > Screen.height) return false;
        return true;
        // this is mostly intellisense I'm not sure if this is the way to go. i don't really mind about this yet
    }

    void SetVisible(bool visible)
    {
        IsVisible = visible;
        if (group != null) group.alpha = visible ? 1f : 0f;
    }
}
