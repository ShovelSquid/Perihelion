using UnityEngine;

// Screen-space HitIndicator (ammo, charge, cooldown rings) that floats on one hand's gun.
// Follows the visible gun (recoil flip and kickback included), so it moves with the gun on screen.
// While a gun is in the hand, that gun draws into this indicator instead of the Player's shared one;
// when the gun leaves the hand it gets its previous indicator back.
// Runs after HandRig (100) has posed the gun and before ScreenAnchor (200) projects the point.
[DefaultExecutionOrder(190)]
[RequireComponent(typeof(ScreenAnchor))]
public class GunIndicator : MonoBehaviour
{
    public HandRig hands; // the rig whose hand this indicator tracks
    public HandSide hand = HandSide.Right;
    public HitIndicator indicator; // a child of this object; switched off while the hand holds no gun
    public bool followMuzzle = false; // false = the gun's root (reads as "on the gun"), true = its fire point
    // Use ScreenAnchor's worldOffset / screenOffset to push the indicator off the gun (e.g. beside it).

    ScreenAnchor anchor;
    Gun bound;
    HitIndicator previous; // what the bound gun drew into before we took over

    void Awake()
    {
        anchor = GetComponent<ScreenAnchor>();
        if (hands == null) Debug.LogWarning($"{name}: GunIndicator has no HandRig assigned, so it stays hidden.", this);
        if (indicator == null) indicator = GetComponentInChildren<HitIndicator>(true);
    }

    void LateUpdate()
    {
        Gun gun = hands != null ? hands.GetPlacedItem(hand) as Gun : null;
        if (gun != bound)
        {
            Unbind();
            Bind(gun);
        }

        if (gun == null)
        {
            anchor.ClearWorldPoint();
            SetVisible(false);
            return;
        }

        SetVisible(true);
        Transform point = followMuzzle ? gun.Muzzle : gun.transform;
        // Explicit null check: Muzzle falls back to the gun itself, but a destroyed gun is Unity-null.
        if (point == null) point = gun.transform;
        anchor.SetWorldPoint(point.position);
    }

    void Bind(Gun gun)
    {
        bound = gun;
        if (gun == null || indicator == null) return;
        previous = gun.HitIndicator;
        gun.SetHitIndicator(indicator);
    }

    void Unbind()
    {
        // Only hand the old indicator back if nothing else has taken the gun over since.
        if (bound != null && bound.HitIndicator == indicator) bound.SetHitIndicator(previous);
        bound = null;
        previous = null;
    }

    // The indicator child is toggled, not this object, so LateUpdate keeps running and can switch it back on.
    void SetVisible(bool visible)
    {
        if (indicator != null && indicator.gameObject.activeSelf != visible) indicator.gameObject.SetActive(visible);
    }

    void OnDisable()
    {
        Unbind();
    }
}
