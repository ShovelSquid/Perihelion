using System.Collections.Generic;
using UnityEngine;

// A per-hand aim cursor whose prong frame sits on the hand's ideal aim point, with the dot on the hand's
// real aim point. Prongs are any number of child RectTransforms at any angle around this object's origin,
// the centre. Each prong's authored anchoredPosition is its zero-bloom pose, and the hand's bloom pushes it
// straight outward from the centre along that authored direction. Prong art and facing (Image or Shapes2D)
// are set up by hand in the editor and are never changed by this script.
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
    public List<RectTransform> prongs = new List<RectTransform>(); // each sits where it was authored at zero bloom and is pushed outward from the centre by bloom
    public float maxDotOffset = 300f; // canvas units; keeps a grazing near real point from flinging the dot across the screen
    // legacy; folded into prongs in Awake so existing scenes keep working until re-wired
    [HideInInspector] public RectTransform prongUp;
    [HideInInspector] public RectTransform prongDown;
    [HideInInspector] public RectTransform prongLeft;
    [HideInInspector] public RectTransform prongRight;

    ScreenAnchor anchor;
    Canvas canvas;
    bool? partsActive; // null until the first frame, so the first toggle always applies
    // Built once in Awake and never re-read: placing a prong overwrites its anchoredPosition, so reading it
    // again each frame would compound the push.
    readonly List<ProngRest> prongRests = new List<ProngRest>();

    struct ProngRest
    {
        public RectTransform part;
        public Vector2 restDir; // unit direction from the centre, taken from the authored position
        public float restDist; // canvas units from the centre at zero bloom
    }

    void Awake()
    {
        anchor = GetComponent<ScreenAnchor>();
        canvas = GetComponentInParent<Canvas>();
        if (hands == null) Debug.LogWarning($"{name}: AimCursor has no HandRig assigned, so it stays hidden.", this);
        FoldLegacyProng(prongUp);
        FoldLegacyProng(prongDown);
        FoldLegacyProng(prongLeft);
        FoldLegacyProng(prongRight);
        // Cache after folding, so the legacy prongs get a rest pose too.
        CacheProngRests();
    }

    // An old four-prong cursor keeps its prongs: each assigned one joins the list once.
    void FoldLegacyProng(RectTransform legacy)
    {
        if (legacy != null && !prongs.Contains(legacy)) prongs.Add(legacy);
    }

    void CacheProngRests()
    {
        prongRests.Clear();
        foreach (RectTransform part in prongs)
        {
            if (part == null) continue;
            Vector2 rest = part.anchoredPosition;
            // A prong authored on the centre has no direction to be pushed along, so it is left where it is.
            if (rest.sqrMagnitude < 0.0001f)
            {
                Debug.LogWarning($"{name}: AimCursor prong {part.name} is authored on the centre, so it has no outward direction and stays put.", part);
                continue;
            }
            prongRests.Add(new ProngRest { part = part, restDir = rest.normalized, restDist = rest.magnitude });
        }
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

        // The cursor frame sits on HandRig's eased cursor point (the ideal point trailed at cursorFollowSpeed,
        // the bloom circle centre); the dot is offset from it onto the real point, which is where the muzzle
        // actually aims and shots go, so easing the frame never moves the dot. A very low cursorFollowSpeed
        // can briefly stretch that offset past maxDotOffset.
        anchor.SetWorldPoint(ideal);

        // Canvas units every prong moves outward from its authored rest; zero bloom leaves each prong where it was authored.
        float bloomPush = anchor.AngleToCanvasUnits(hands.GetBloom(hand));
        Place(dot, DotOffset(ideal, real));
        foreach (ProngRest r in prongRests) Place(r.part, r.restDir * (r.restDist + bloomPush));
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
        foreach (RectTransform prong in prongs) SetActive(prong, active);
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
