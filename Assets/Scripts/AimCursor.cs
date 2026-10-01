using UnityEngine;

// A per-hand aim cursor whose prong frame sits on the hand's ideal aim point, spread by that hand's
// bloom, with the dot on the hand's real aim point. Prong visuals (Image or Shapes2D) are set up in the editor.
// Runs after HandRig (100) has posed the item and after the camera's LateUpdate, and before ScreenAnchor (200) projects the point, so the cursor lands in the same frame.
[DefaultExecutionOrder(190)]
// ScreenAnchor only hides through a CanvasGroup on the same object, so without one a hidden cursor would freeze on screen.
[RequireComponent(typeof(ScreenAnchor))]
[RequireComponent(typeof(CanvasGroup))]
public class AimCursor : MonoBehaviour
{
    [Header("Source")]
    public HandRig hands; // the rig whose hand this cursor tracks
    public HandSide hand = HandSide.Right; // which hand's placed item drives this cursor
    [Header("Parts")]
    public RectTransform dot; // center mark, on the real aim point
    public RectTransform prongUp; // pushed up by gap + bloom
    public RectTransform prongDown; // pushed down by gap + bloom
    public RectTransform prongLeft; // pushed left by gap + bloom
    public RectTransform prongRight; // pushed right by gap + bloom
    public float gap = 4f; // canvas units between the ideal point and each prong at zero bloom
    public float maxDotOffset = 300f; // canvas units; keeps a grazing near real point from flinging the dot across the screen

    ScreenAnchor anchor;
    Canvas canvas;
    bool? partsActive; // null until the first frame, so the first toggle always applies

    void Awake()
    {
        anchor = GetComponent<ScreenAnchor>();
        canvas = GetComponentInParent<Canvas>();
        if (hands == null) Debug.LogWarning($"{name}: AimCursor has no HandRig assigned, so it stays hidden.", this);
    }

    void LateUpdate()
    {
        Item item = hands != null ? hands.GetPlacedItem(hand) : null;
        // Only weapons get a cursor. Nothing placed by this hand (empty, the off hand of a two-handed item,
        // or a non-gun item): switch the parts off rather than just fading, so unused cursors are truly gone.
        bool used = item is Gun;
        SetPartsActive(used);
        if (!used)
        {
            anchor.ClearWorldPoint();
            return;
        }

        // Not posed yet, or no aim point: nothing to point at this frame.
        if (!hands.TryGetAimPoints(hand, out Vector3 ideal, out Vector3 real))
        {
            anchor.ClearWorldPoint();
            return;
        }

        // The cursor frame sits on the ideal point (the bloom circle centre); the dot shows the real point
        // inside it, which is where the muzzle actually aims and shots go.
        anchor.SetWorldPoint(ideal);

        float radius = gap + anchor.AngleToCanvasUnits(hands.GetBloom(hand));
        Place(dot, DotOffset(ideal, real));
        Place(prongUp, Vector2.up * radius);
        Place(prongDown, Vector2.down * radius);
        Place(prongLeft, Vector2.left * radius);
        Place(prongRight, Vector2.right * radius);
    }

    // Screen-space gap between the two points, in canvas units, projected through the world camera
    // (ScreenAnchor.cam) so gun roll is baked in. Depth is ignored: only x and y count. Zero when either
    // point is behind the camera (ScreenAnchor already handles the frame itself), clamped to maxDotOffset.
    Vector2 DotOffset(Vector3 idealPoint, Vector3 actualPoint)
    {
        Camera cam = anchor.cam;
        if (cam == null) return Vector2.zero;
        Vector3 a = cam.WorldToScreenPoint(idealPoint);
        Vector3 b = cam.WorldToScreenPoint(actualPoint);
        if (a.z <= 0f || b.z <= 0f) return Vector2.zero;
        Vector2 pixels = new Vector2(b.x - a.x, b.y - a.y);
        Vector2 units = canvas != null ? pixels / canvas.scaleFactor : pixels;
        return Vector2.ClampMagnitude(units, maxDotOffset);
    }

    // The parts are toggled, not this object: a disabled cursor would stop its own LateUpdate and could
    // never switch itself back on when a weapon is equipped.
    void SetPartsActive(bool active)
    {
        if (partsActive == active) return;
        partsActive = active;
        SetActive(dot, active);
        SetActive(prongUp, active);
        SetActive(prongDown, active);
        SetActive(prongLeft, active);
        SetActive(prongRight, active);
    }

    static void SetActive(RectTransform part, bool active)
    {
        if (part != null) part.gameObject.SetActive(active);
    }

    static void Place(RectTransform part, Vector2 position)
    {
        if (part != null) part.anchoredPosition = position;
    }
}
