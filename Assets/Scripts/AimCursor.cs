using UnityEngine;

// A per-hand aim cursor that sits on the point the hand's held item actually hits, with four prongs
// spread by that hand's bloom. Prong visuals (Image or Shapes2D) are set up in the editor.
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
    public float maxDistance = 1000f; // meters; matches AimInput's default ray length
    public LayerMask hitMask = Physics.DefaultRaycastLayers; // layers the muzzle ray can land on; exclude the player's own layer if the cursor snaps onto the body
    [Header("Parts")]
    public RectTransform dot; // center mark, always on the hit point
    public RectTransform prongUp; // pushed up by gap + bloom
    public RectTransform prongDown; // pushed down by gap + bloom
    public RectTransform prongLeft; // pushed left by gap + bloom
    public RectTransform prongRight; // pushed right by gap + bloom
    public float gap = 4f; // canvas units between the hit point and each prong at zero bloom

    ScreenAnchor anchor;
    Canvas canvas;

    void Awake()
    {
        anchor = GetComponent<ScreenAnchor>();
        canvas = GetComponentInParent<Canvas>();
        if (hands == null) Debug.LogWarning($"{name}: AimCursor has no HandRig assigned, so it stays hidden.", this);
    }

    void LateUpdate()
    {
        Item item = hands != null ? hands.GetPlacedItem(hand) : null;
        // Nothing placed by this hand (empty, or the off hand of a two-handed item): hide.
        if (item == null)
        {
            anchor.ClearWorldPoint();
            return;
        }

        // Explicit null check, not ??, which bypasses Unity's destroyed-object check.
        Transform muzzle = item.Muzzle;
        if (muzzle == null) muzzle = item.transform;

        // Two rays: the actual muzzle (sway + kick included) for the dot, and the same muzzle with the
        // hand's aim offset undone for the prongs. HandRig applies the offset in the muzzle frame as
        // Euler(-y, x, 0), so the inverse of that gives the direction the gun would point without it.
        Vector3 origin = muzzle.position;
        Vector2 offset = hands.GetAimOffset(hand);
        Vector3 actualDir = muzzle.forward;
        Vector3 idealDir = muzzle.rotation * (Quaternion.Inverse(Quaternion.Euler(-offset.y, offset.x, 0f)) * Vector3.forward);
        Vector3 actualPoint = CastPoint(origin, actualDir);
        Vector3 idealPoint = CastPoint(origin, idealDir);

        // The cursor frame sits on the ideal point; the dot shows where the shot actually lands inside it.
        anchor.SetWorldPoint(idealPoint);

        float radius = gap + anchor.AngleToCanvasUnits(hands.GetBloom(hand));
        Place(dot, DotOffset(idealPoint, actualPoint));
        Place(prongUp, Vector2.up * radius);
        Place(prongDown, Vector2.down * radius);
        Place(prongLeft, Vector2.left * radius);
        Place(prongRight, Vector2.right * radius);
    }

    Vector3 CastPoint(Vector3 origin, Vector3 dir)
    {
        if (Physics.Raycast(origin, dir, out RaycastHit hit, maxDistance, hitMask, QueryTriggerInteraction.Ignore))
        {
            return hit.point;
        }
        return origin + dir * maxDistance;
    }

    // Screen-space gap between the two hit points, in canvas units. Projected through the world camera
    // (ScreenAnchor.cam) rather than converted from the offset angle, so gun roll and muzzle/camera
    // parallax are already baked in.
    Vector2 DotOffset(Vector3 idealPoint, Vector3 actualPoint)
    {
        Camera cam = anchor.cam;
        if (cam == null) return Vector2.zero;
        Vector3 a = cam.WorldToScreenPoint(idealPoint);
        Vector3 b = cam.WorldToScreenPoint(actualPoint);
        if (a.z <= 0f || b.z <= 0f) return Vector2.zero;
        Vector2 pixels = new Vector2(b.x - a.x, b.y - a.y);
        return canvas != null ? pixels / canvas.scaleFactor : pixels;
    }

    static void Place(RectTransform part, Vector2 position)
    {
        if (part != null) part.anchoredPosition = position;
    }
}
