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

    void Awake()
    {
        anchor = GetComponent<ScreenAnchor>();
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

        Vector3 origin = muzzle.position;
        Vector3 forward = muzzle.forward;
        Vector3 point;
        if (Physics.Raycast(origin, forward, out RaycastHit hit, maxDistance, hitMask, QueryTriggerInteraction.Ignore))
        {
            point = hit.point;
        }
        else
        {
            point = origin + forward * maxDistance;
        }
        anchor.SetWorldPoint(point);

        float radius = gap + anchor.AngleToCanvasUnits(hands.GetBloom(hand));
        Place(dot, Vector2.zero);
        Place(prongUp, Vector2.up * radius);
        Place(prongDown, Vector2.down * radius);
        Place(prongLeft, Vector2.left * radius);
        Place(prongRight, Vector2.right * radius);
    }

    static void Place(RectTransform part, Vector2 position)
    {
        if (part != null) part.anchoredPosition = position;
    }
}
