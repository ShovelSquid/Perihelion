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
    public float missDistance = 150f; // meters; depth used when the ray hits nothing; parallax is already negligible here, and it keeps depth jumps short
    public LayerMask hitMask = Physics.DefaultRaycastLayers; // layers the muzzle ray can land on; exclude the player's own layer if the cursor snaps onto the body
    [Header("Depth Smoothing")]
    // Only the cursor's depth along the ray is smoothed, never its direction, so it slides across the
    // parallax gap at collider edges instead of teleporting while still pointing exactly where the gun does.
    public float depthInSharpness = 40f; // per second, when the hit gets closer (you just found a wall): near-instant
    public float depthOutSharpness = 12f; // per second, when the hit gets farther (you slid off an edge): eases out
    [Header("Parts")]
    public RectTransform dot; // center mark, always on the hit point
    public RectTransform prongUp; // pushed up by gap + bloom
    public RectTransform prongDown; // pushed down by gap + bloom
    public RectTransform prongLeft; // pushed left by gap + bloom
    public RectTransform prongRight; // pushed right by gap + bloom
    public float gap = 4f; // canvas units between the hit point and each prong at zero bloom

    ScreenAnchor anchor;
    Canvas canvas;
    bool? partsActive; // null until the first frame, so the first toggle always applies
    float depth; // smoothed distance from the muzzle to the cursor along the ideal ray
    bool hasDepth; // false after the cursor was hidden, so the first frame back snaps instead of sliding

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
            hasDepth = false;
            return;
        }

        // Explicit null check, not ??, which bypasses Unity's destroyed-object check.
        Transform muzzle = item.Muzzle;
        if (muzzle == null) muzzle = item.transform;

        // One ray: the muzzle with the hand's aim offset undone, for the prongs. HandRig applies the offset in
        // the muzzle frame as Euler(-y, x, 0), so the inverse of that gives the direction the gun would point
        // without it. The dot is placed by angle, not by a second ray: two rays hitting different depths at a
        // collider edge project far apart on screen (the muzzle isn't at the camera), which made the dot jump.
        // Read the hand's recorded aim pose (before visual kickback/flip), the same one Gun fires from,
        // so the dot follows the shots rather than the thrown-around gun model.
        Vector3 origin = muzzle.position;
        Quaternion aimRot = muzzle.rotation;
        if (hands.TryGetAimPose(hand, out Vector3 aimPos, out Quaternion recorded))
        {
            origin = aimPos;
            aimRot = recorded;
        }
        Vector2 offset = hands.GetAimOffset(hand);
        Vector3 idealDir = aimRot * (Quaternion.Inverse(Quaternion.Euler(-offset.y, offset.x, 0f)) * Vector3.forward);

        float hitDistance = CastDistance(origin, idealDir);
        if (!hasDepth)
        {
            depth = hitDistance;
            hasDepth = true;
        }
        else
        {
            float sharpness = hitDistance < depth ? depthInSharpness : depthOutSharpness;
            depth = Mathf.Lerp(depth, hitDistance, 1f - Mathf.Exp(-sharpness * Time.deltaTime));
        }

        // The cursor frame sits on the ideal ray; the dot shows where the shot actually lands inside it.
        anchor.SetWorldPoint(origin + idealDir * depth);

        float radius = gap + anchor.AngleToCanvasUnits(hands.GetBloom(hand));
        Place(dot, DotOffset(offset, aimRot));
        Place(prongUp, Vector2.up * radius);
        Place(prongDown, Vector2.down * radius);
        Place(prongLeft, Vector2.left * radius);
        Place(prongRight, Vector2.right * radius);
    }

    float CastDistance(Vector3 origin, Vector3 dir)
    {
        if (Physics.Raycast(origin, dir, out RaycastHit hit, maxDistance, hitMask, QueryTriggerInteraction.Ignore))
        {
            return hit.distance;
        }
        return missDistance;
    }

    // The dot's offset inside the cursor, in canvas units, straight from the aim offset angle. Same
    // degrees-to-pixels mapping as the prongs, so "dot at a prong tip" means exactly "at the bloom edge",
    // and nothing here depends on hit depth. The muzzle's right/up are projected onto the camera's screen
    // axes so a rolled gun throws the dot along its own tilt.
    Vector2 DotOffset(Vector2 offset, Quaternion muzzleRot)
    {
        Camera cam = anchor.cam;
        if (cam == null) return Vector2.zero;
        Transform c = cam.transform;
        Vector3 right = muzzleRot * Vector3.right;
        Vector3 up = muzzleRot * Vector3.up;
        Vector2 screenRight = new Vector2(Vector3.Dot(right, c.right), Vector3.Dot(right, c.up));
        Vector2 screenUp = new Vector2(Vector3.Dot(up, c.right), Vector3.Dot(up, c.up));
        // Normalized so looking steeply along the barrel doesn't shrink the axes; a degenerate axis contributes nothing.
        screenRight = screenRight.sqrMagnitude > 1e-6f ? screenRight.normalized : Vector2.zero;
        screenUp = screenUp.sqrMagnitude > 1e-6f ? screenUp.normalized : Vector2.zero;
        return screenRight * anchor.AngleToCanvasUnits(offset.x) + screenUp * anchor.AngleToCanvasUnits(offset.y);
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
