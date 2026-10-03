using UnityEngine;
using System;
using System.Collections.Generic;
using UnityEngine.Animations.Rigging;

// Right must stay first (value 0) so existing serialized Item data defaults to right-handed.
public enum HandSide
{
    Right,
    Left
}

// Owns both hands: which item each hand holds, that item's pose (hand socket, then the muzzle aim
// correction toward the hand's real aim point, then kickback), and the
// TwoBoneIK targets. This is the only script that writes IK targets.
// Runs after the default-order scripts so it reads the aim point AimInput moved this frame and any
// recoil fired this frame. The Animator and rig still evaluate after every Update regardless of order.
[DefaultExecutionOrder(100)]
public class HandRig : MonoBehaviour
{
    [System.Serializable]
    public class HandSlot
    {
        public Transform socket; // pure animated, pre-IK hand pose; only read here, whatever writes it (rig constraint today, ghost/pose armature later)
        public Transform ikTarget; // this hand's TwoBoneIK target; only HandRig writes it
        public Rig ikRig; // weight 1 while this hand grips, 0 otherwise
        public Item item; // equipped item, or null
        [System.NonSerialized] public bool places; // true when this slot's socket positions the item
        [System.NonSerialized] public Item supporting; // one-handed item this empty hand steadies; refreshed on equip/release, not per frame
        [System.NonSerialized] public Rigidbody body; // the placed item's body, held kinematic while in this hand
        [System.NonSerialized] public bool savedKinematic; // body.isKinematic from before it was held, restored on release
        [System.NonSerialized] public RigidbodyInterpolation savedInterpolation; // body.interpolation from before it was held, restored on release
        [System.NonSerialized] public float bloom; // degrees; this hand's current spread radius; floored at the held gun's baseSpread
        [System.NonSerialized] public Vector3 kickback; // meters, muzzle-local position offset along the barrel (displayed)
        [System.NonSerialized] public Vector3 kickbackPeak; // meters; what the last shot pushed kickback to, scaled down by the return curve
        [System.NonSerialized] public Vector2 flip; // degrees of visual-only muzzle flip, muzzle frame (x right, y up); never affects aim
        [System.NonSerialized] public Vector2 flipPeak; // degrees; what the last shot pushed flip to, scaled down by the return curve
        [System.NonSerialized] public float recoverTime; // seconds since the last kick; drives the gun's recoilReturn curve
        [System.NonSerialized] public Vector3 aimMuzzlePos; // muzzle pose after the aim solve, before kickback/flip: where shots actually leave from
        [System.NonSerialized] public Quaternion aimMuzzleRot = Quaternion.identity;
        [System.NonSerialized] public bool hasAimPose; // false until the slot has been posed at least once
        [System.NonSerialized] public AimPart target; // part this hand is locked on (collider, optional Hitbox, owner), or null
        [System.NonSerialized] public float targetScore; // the locked part's last score; lower wins
        [System.NonSerialized] public Vector3 lookDir; // world, unit; this hand's own look direction, chasing the crosshair at its lead or trail rate; its cone axis
        [System.NonSerialized] public bool hasLookDir; // false until lookDir has snapped to its first crosshair direction
        [System.NonSerialized] public Vector3 idealPoint; // world; the locked part's point nearest the cone axis, or this hand's look point at the eased aim depth
        [System.NonSerialized] public Vector3 anchorPoint; // world; eases toward idealPoint so the real point travels across depth
        [System.NonSerialized] public bool hasAnchor; // false until anchorPoint has snapped to its first ideal point
        [System.NonSerialized] public Vector3 cursorPoint; // world; eases toward idealPoint at cursorFollowSpeed; where the aim cursor's frame is drawn, display only
        [System.NonSerialized] public bool hasCursor; // false until cursorPoint has snapped to its first ideal point
        [System.NonSerialized] public Vector3 realOffset; // meters around anchorPoint, flat in the plane facing the eye: sway across the reticle disk plus kicks; never past the bloom radius
        [System.NonSerialized] public Vector3 realVelocity; // m/s
        [System.NonSerialized] public Vector3 realPoint; // world; anchorPoint + realOffset; what the muzzle aims at, where the dot is drawn, and exactly where shots go
        [System.NonSerialized] public bool hasAimPoints; // false until ideal and real points have been computed
        [System.NonSerialized] public float noiseSeed; // per hand, so the hands never sway in sync
        [System.NonSerialized] public float noiseTime; // advances at swayFrequency
    }

    // A class, not a struct: Unity compiles C# 9, which has no struct field initializers, and weight should start at 1.
    [System.Serializable]
    public class LayerWeight
    {
        public LayerMask layers; // owner layers this entry covers
        public float weight = 1f; // aim priority for owners on those layers, passed to ScorePart
    }

    [Header("Hands")]
    public HandSide defaultHand = HandSide.Right; // dominant hand: equips go here unless a side is given; holds hand1 of two-handed items
    public HandSlot right = new HandSlot();
    public HandSlot left = new HandSlot();
    [Header("Aim")]
    public bool aiming; // while true, the aim weight blends toward 1
    public Transform aimPoint; // shared point AimInput moves; placed items turn their muzzle toward it
    // aimPoint snaps from a near hit to its far fallback when the crosshair slides off a collider, and the
    // muzzle isn't at the camera, so aiming straight at it swings the gun across the parallax gap in one
    // frame. The gun aims at a copy that keeps aimPoint's exact direction from the camera and only eases
    // its distance: fast when it comes closer (a wall appeared), slower when it goes farther.
    public float aimDepthInSharpness = 40f; // per second
    public float aimDepthOutSharpness = 12f; // per second
    [Range(0f, 1f)] public float idleAimWeight = 0f; // correction applied when not aiming (0 = pure animation)
    public float aimBlendSharpness = 10f; // per second; higher blends faster; framerate independent
    [Range(1, 4)] public int aimIterations = 2; // correction passes; the muzzle is offset from the pivot, so one pass undershoots
    public float maxAimCorrectionAngle = 60f; // degrees; caps the correction so aim points behind or far off-axis can't spin the item
    public float minAimDistance = 0.5f; // meters beyond the muzzle's reach from the socket; closer aim points are pushed out to this
    [Range(0f, 1f)] public float uprightWeight = 1f; // how hard the muzzle's up is pulled to the character's up (0 = keep the hand's roll)
    public float rollPerYaw = 0.1f; // degrees of roll per degree the muzzle points left/right of the character's forward; negative flips direction
    public float maxAimRoll = 15f; // degrees; caps the yaw-driven roll
    [Header("Sway and Recoil")]
    // The real point sways inside the reticle disk (bloom degrees seen from the eye) and the bloom radius leashes it, so the dot never leaves the prongs.
    public float offsetFrequency = 6f; // Hz; how quickly the spring pulls the real aim point to its sway target and back after kicks
    [Range(0.1f, 2f)] public float offsetDampingRatio = 0.5f; // 1 = critically damped, no overshoot; below 1 overshoots after a kick
    public float swayFrequency = 0.5f; // noise units per second; how fast the sway target wanders across the reticle disk
    [UnityEngine.Serialization.FormerlySerializedAs("supportRecoilScale")] [Range(0f, 1f)] public float supportBloomScale = 0.5f; // bloom-per-shot multiplier while a free hand steadies a one-handed item
    [Range(0f, 1f)] public float supportSwayScale = 0.5f; // multiplier on the item's swayFill while a free hand steadies a one-handed item
    [Header("Look and Movement")]
    public Transform lookSource; // whose rotation counts as "looking"; defaults to Camera.main
    public Rigidbody moveBody; // whose speed counts as "moving" (airborne included, since it's speed-based); defaults to this object's Rigidbody
    public float lookSmoothing = 12f; // per second; smooths the measured look rate so single-frame mouse spikes don't jolt the dot
    [Header("Hand Lead and Trail")]
    public float leadSharpness = 25f; // per second; how fast the hand on the side the view turns toward follows the crosshair
    public float trailSharpness = 8f; // per second; how fast the other hand follows, so it lags; pitch-only turns use the midpoint
    [Header("Aim Assist")]
    public float assistLookSharpness = 15f; // per second; smooths the centre look direction the shared part chooser measures from, so flicking across a gap doesn't drop the target
    public float convergeAngle = 3f; // degrees off the crosshair inside which the best centre part is shared by both hands while dual-wielding
    public float convergeExitScale = 1.3f; // the shared zone is left only past convergeAngle * this, so ShouldConverge can hold a part that drifted just past convergeAngle
    public float cursorFollowSpeed = 20f; // per second; eases the aim cursor's frame toward the hand's ideal point so lock-on snaps glide; 0 or less means no easing (the frame sits on the ideal point, unlike idealFollowSpeed where 0 freezes the anchor); display only, the dot and shots stay exact
    public float assistRange = 150f; // meters; parts farther than this are ignored
    public LayerMask assistMask = Physics.DefaultRaycastLayers; // layers that block line of sight to a part; the default (DefaultRaycastLayers) includes the Hitbox layer, so limbs block it too
    public List<LayerWeight> ownerLayerWeights = new List<LayerWeight>(); // aim priority by the part owner's layer, e.g. Mobs 2, Buildings 1; the first entry whose mask holds the owner's layer wins
    public float defaultLayerWeight = 1f; // weight for owners on layers no entry lists
    [Header("Visual Kick")]
    // Cosmetic kickback slide and muzzle flip, layered after the aim pose is recorded, so they can be as big
    // as you like without moving shots or the cursor dot. Each shot sets a peak; the held gun's recoilReturn
    // curve over recoilReturnTime scales it back to zero, so the gun can hang up and then ease home.
    public float visualAttackSharpness = 30f; // per second; how fast the display chases the envelope, so the snap-up isn't a one-frame pop
    [UnityEngine.Serialization.FormerlySerializedAs("maxRecoilDistance")] public float maxKickback = 0.2f; // meters; clamps stacked kickback
    public float maxFlip = 45f; // degrees; clamps stacked flips from automatic fire

    float aimWeight;
    Vector3 aimTarget; // smoothed-depth copy of aimPoint; the centre aim target, used by hands without a look direction
    float aimDistance; // smoothed camera-to-aimPoint distance
    bool hasAimDistance;
    Vector3 aimDir; // exact eye-to-aimPoint unit direction; each hand's lookDir chases it
    Vector2 lookRate; // smoothed deg/s, x = yaw (right +), y = pitch (up +)
    Vector3 lastLookForward;
    bool hasLastLook;
    Vector3 lookReference; // smoothed "general look" point at the eased aim depth; the shared part chooser's axis points at it
    Vector3 assistLookDir; // smoothed eye-to-aimPoint direction behind lookReference
    bool hasAssistLook;
    AimPart sharedTarget; // the part both dual-wielded hands lock this frame, or null; set once per frame by UpdateSharedTarget
    bool converged; // ShouldConverge's answer from last frame, fed back so the shared zone has hysteresis
    Object self; // this rig's own damageable, so its own parts are skipped even if a ragdoll is unparented
    static readonly RaycastHit[] losHits = new RaycastHit[16]; // shared line-of-sight buffer; nothing allocated per frame

    public bool IsEmpty
    {
        get { return right.item == null && left.item == null; }
    }

    // Each hand places its own one-handed item, so the hands can lock different parts unless they share the centre one.
    bool IsDualWielding
    {
        get { return right.places && left.places && right.item != null && left.item != null && right.item != left.item; }
    }

    void Awake()
    {
        // Start at the resting weight so there is no blend-in on the first frame.
        aimWeight = idleAimWeight;
        if (lookSource == null && Camera.main != null) lookSource = Camera.main.transform;
        if (moveBody == null) moveBody = GetComponent<Rigidbody>();
        self = GetComponent<Object>();
        // Seeded per hand and per rig, so the two hands and different characters sway out of sync.
        right.noiseSeed = UnityEngine.Random.Range(0f, 1000f);
        left.noiseSeed = UnityEngine.Random.Range(0f, 1000f);
    }

    void Start()
    {
        // Items assigned in the inspector go through the same Equip path as runtime equips.
        EquipAuthored(HandSide.Right);
        EquipAuthored(HandSide.Left);
    }

    void EquipAuthored(HandSide side)
    {
        HandSlot slot = GetSlot(side);
        if (slot.item == null) return;
        Item cached = slot.item;
        slot.item = null;
        slot.places = false;
        Equip(cached, side);
    }

    public HandSlot GetSlot(HandSide side)
    {
        return side == HandSide.Left ? left : right;
    }

    public Item GetItem(HandSide side)
    {
        return GetSlot(side).item;
    }

    // The item this hand positions, or null (empty hand, or the off hand of a two-handed item). Cursors and UI read it.
    public Item GetPlacedItem(HandSide side)
    {
        HandSlot slot = GetSlot(side);
        return slot.places ? slot.item : null;
    }

    // Degrees of spread this hand currently has, or 0 when it places nothing. Cursors and UI read it.
    public float GetBloom(HandSide side)
    {
        HandSlot slot = GetSlot(side);
        return slot.places && slot.item != null ? slot.bloom : 0f;
    }

    // Degrees this hand's real aim point sits off its ideal aim point, seen from the aimed muzzle (ideal
    // aim frame, x right, y up), or zero when it places nothing. For UI and debugging.
    public Vector2 GetAimOffset(HandSide side)
    {
        HandSlot slot = GetSlot(side);
        if (!slot.places || slot.item == null || !slot.hasAimPose || !slot.hasAimPoints) return Vector2.zero;
        Vector3 realDir = slot.realPoint - slot.aimMuzzlePos;
        if (realDir.sqrMagnitude < 1e-6f) return Vector2.zero;
        Vector3 local = Quaternion.Inverse(LookFromMuzzle(slot, slot.idealPoint)) * realDir;
        float x = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
        float y = Mathf.Atan2(local.y, new Vector2(local.x, local.z).magnitude) * Mathf.Rad2Deg;
        return new Vector2(x, y);
    }

    // Where this hand is locked: the part its ideal aim point sits on, or null. For UI and debugging.
    public AimPart GetTarget(HandSide side)
    {
        return GetSlot(side).target;
    }

    // The part both hands share while dual-wielding near the crosshair, or null. For UI and debugging.
    public AimPart GetSharedTarget()
    {
        return sharedTarget;
    }

    // ideal is this hand's cursor-frame point: the ideal aim point (the chosen part, or the centre target)
    // eased at cursorFollowSpeed, for display only. real is the real aim point (ideal plus follow lag, sway
    // and kicks; where the muzzle actually points). False when the hand places nothing or hasn't been posed
    // yet. Cursors read it; shot and aim math uses idealPoint and realPoint directly.
    public bool TryGetAimPoints(HandSide side, out Vector3 ideal, out Vector3 real)
    {
        HandSlot slot = GetSlot(side);
        bool ok = slot.places && slot.item != null && slot.hasAimPose && slot.hasAimPoints;
        ideal = ok ? slot.cursorPoint : default;
        real = ok ? slot.realPoint : default;
        return ok;
    }

    static HandSide Other(HandSide side)
    {
        return side == HandSide.Left ? HandSide.Right : HandSide.Left;
    }

    // Visits each distinct held item once: right first, then left only if it is a
    // different item, so a two-handed item is visited once.
    public void ForEachItem(Action<Item> action)
    {
        if (action == null) return;
        if (right.item != null) action(right.item);
        if (left.item != null && left.item != right.item) action(left.item);
    }

    public void SetAiming(bool a)
    {
        aiming = a;
    }

    public bool Equip(Item item, HandSide side)
    {
        if (item == null) return false;
        // A two-handed item always places from the default hand, so hand1/hand2 keep their orientation.
        if (item.IsTwoHanded) side = defaultHand;

        HandSlot slot = GetSlot(side);
        // Already placed in this hand: re-equipping is a no-op (Hotwheel re-equips the current item).
        if (slot.item == item && slot.places) return true;

        // Moving an item between hands: clear its old slots without hiding it.
        if (right.item == item || left.item == item) Release(item, false);

        HandSide otherSide = Other(side);
        HandSlot other = GetSlot(otherSide);
        bool twoHanded = item.IsTwoHanded;

        if (slot.item != null && slot.item != item) Release(slot.item, true);
        if (twoHanded && other.item != null && other.item != item) Release(other.item, true);

        slot.item = item;
        slot.places = true;
        if (twoHanded)
        {
            other.item = item;
            other.places = false;
        }

        // Inspector-authored holds skip Mob.Equip, and Gun needs its holder to find this rig for recoil.
        // Setting it before activation lets Item.Awake bind the Player hit indicator on first activation.
        if (item.holder == null) item.holder = GetComponent<Mob>();

        // Activate first so Item.Awake has run and item.rb is filled.
        item.gameObject.SetActive(true);
        item.equipped = true;
        Rigidbody body = item.rb != null ? item.rb : item.GetComponent<Rigidbody>();
        if (body != null) HoldBody(slot, body);
        ResetAimState(slot);

        if (TryGetHoldPose(side, item, out Vector3 pos, out Quaternion rot))
        {
            if (body != null)
            {
                body.position = pos;
                body.rotation = rot;
            }
            item.transform.SetPositionAndRotation(pos, rot);
        }
        else
        {
            Debug.LogWarning($"{name}: HandRig {side} slot has no socket, so {item.name} can't be placed in the hand.", this);
        }

        RefreshSupport();
        return true;
    }

    void HoldBody(HandSlot slot, Rigidbody body)
    {
        slot.body = body;
        slot.savedKinematic = body.isKinematic;
        slot.savedInterpolation = body.interpolation;
        // Zero velocities while still dynamic: Unity 6 warns when velocity is set on a kinematic body.
        if (!body.isKinematic)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }
        body.isKinematic = true;
        // HandRig writes the transform every frame; interpolation would blend it back toward stale physics poses.
        body.interpolation = RigidbodyInterpolation.None;
    }

    void ReleaseBody(HandSlot slot)
    {
        // Unity null check: the item may have been destroyed while held.
        if (slot.body != null)
        {
            slot.body.isKinematic = slot.savedKinematic;
            slot.body.interpolation = slot.savedInterpolation;
        }
        slot.body = null;
    }

    public void Unequip(HandSide side)
    {
        Item held = GetItem(side);
        // Releasing either hand of a two-handed item releases the whole item.
        if (held != null) Release(held, true);
    }

    public void UnequipAll()
    {
        Unequip(HandSide.Right);
        Unequip(HandSide.Left);
    }

    void Release(Item item, bool hide)
    {
        ReleaseSlot(right, item);
        ReleaseSlot(left, item);
        RefreshSupport();
        if (hide && item != null)
        {
            item.equipped = false;
            item.gameObject.SetActive(false);
        }
    }

    void ReleaseSlot(HandSlot slot, Item item)
    {
        if (slot.item != item) return;
        // Restore physics before the item is hidden or re-held by the other hand.
        if (slot.places) ReleaseBody(slot);
        ResetAimState(slot);
        slot.item = null;
        slot.places = false;
    }

    void ResetAimState(HandSlot slot)
    {
        slot.kickback = Vector3.zero;
        slot.kickbackPeak = Vector3.zero;
        slot.flip = Vector2.zero;
        slot.flipPeak = Vector2.zero;
        slot.recoverTime = 0f;
        slot.hasAimPose = false;
        slot.bloom = 0f;
        slot.target = null;
        slot.targetScore = 0f;
        slot.realOffset = Vector3.zero;
        slot.realVelocity = Vector3.zero;
        slot.hasAnchor = false;
        slot.hasCursor = false;
        slot.hasAimPoints = false;
        slot.hasLookDir = false;
        // noiseSeed and noiseTime are left alone so re-equips don't restart the sway.
    }

    // Kicks the aim state of the slot placing this item. kickback is meters in the muzzle's local
    // frame (back is -z). bloom is degrees added to the hand's spread, scaled by supportBloomScale while
    // the other hand steadies the item. aimKick throws the hand's real aim point across the reticle in
    // screen axes (x right, y up, seen from the eye), measured in bloom radii after this shot's bloom is
    // added, so 1 throws the dot a full reticle radius whatever the gun's spread. The bloom-radius leash
    // keeps stacked kicks on the reticle's edge, and the offset spring swings the dot back across,
    // overshooting when underdamped. Shots go exactly at the real point, so wherever the dot is thrown is
    // where the next shot goes. The visual flip adds the punch on top: flipRise along the muzzle's up
    // (so a rolled or swaying gun lifts along its own tilt), flipSide along the real point's current sideways
    // drift, in degrees. Kickback and flip stack on what is currently shown and restart the return curve,
    // so automatic fire stays up until you stop.
    // Returns false when this rig isn't placing the item.
    public bool Kick(Item item, Vector3 kickback, float bloom, float flipRise = 0f, float flipSide = 0f, Vector2 aimKick = default)
    {
        if (item == null) return false;
        HandSlot slot = null;
        HandSlot other = null;
        if (right.item == item && right.places) { slot = right; other = left; }
        else if (left.item == item && left.places) { slot = left; other = right; }
        if (slot == null) return false;
        bool supported = other.supporting == item;
        slot.bloom += bloom * (supported ? supportBloomScale : 1f);
        float sideVelocity = Vector3.Dot(slot.realVelocity, slot.aimMuzzleRot * Vector3.right);
        float drift = Mathf.Abs(sideVelocity) > 0.01f ? Mathf.Sign(sideVelocity) : 0f;
        if (slot.hasAimPose && slot.hasAimPoints)
        {
            // Same eye frame as the sway, so one bloom radius lands on the reticle edge. The aimed muzzle is
            // the fallback eye because it equals StepAimState's coneOrigin once the hand is posed.
            ReticleFrame(slot, slot.aimMuzzlePos, out _, out Vector3 screenRight, out Vector3 screenUp, out float dist);
            slot.realOffset += (screenRight * aimKick.x + screenUp * aimKick.y) * DegToMetres(slot.bloom, dist);
        }
        slot.flipPeak = Vector2.ClampMagnitude(slot.flip + new Vector2(drift * flipSide, flipRise), maxFlip);
        slot.kickbackPeak = Vector3.ClampMagnitude(slot.kickback + kickback, maxKickback);
        slot.recoverTime = 0f;
        return true;
    }

    // Advances the slot's aim state (bloom, ideal point, real point with its sway and kick spring) and its
    // kickback by dt. coneOrigin is where this hand's shots leave from, so the part chooser's line of sight
    // is measured from the muzzle; the reticle disk the real point sways in, like the chooser's cone, is
    // measured from the eye.
    void StepAimState(HandSide side, HandSlot slot, float dt, Vector3 coneOrigin)
    {
        Gun gun = slot.item as Gun;
        // Non-gun items have no spread, but the springs still step so a kick never sticks.
        float recovery = gun != null ? 1f - Mathf.Exp(-Mathf.Max(0f, gun.bloomRecovery) * dt) : 1f;
        if (gun == null)
        {
            slot.bloom = 0f;
        }
        else
        {
            // Moving, being airborne and looking raise the floor bloom recovers toward; shots add on top and
            // decay back to it. Velocity is split around the character's up: horizontal is movement, vertical
            // is airborne, so jumping and falling count without a ground check. Each source has its own cap.
            Vector3 velocity = moveBody != null ? moveBody.linearVelocity : Vector3.zero;
            float verticalSpeed = Mathf.Abs(Vector3.Dot(velocity, transform.up));
            float horizontalSpeed = Vector3.ProjectOnPlane(velocity, transform.up).magnitude;
            float moveAdd = Mathf.Min(horizontalSpeed * gun.moveBloom, gun.maxMoveBloom);
            float airAdd = Mathf.Min(verticalSpeed * gun.airBloom, gun.maxAirBloom);
            float lookAdd = Mathf.Min(lookRate.magnitude * gun.lookBloom, gun.maxLookBloom);
            float ceiling = Mathf.Max(gun.baseSpread, gun.maxSpread);
            float floor = Mathf.Max(0f, gun.baseSpread + moveAdd + airAdd + lookAdd);
            floor = Mathf.Min(floor, ceiling);
            // Recovering both ways means starting to run widens the reticle smoothly instead of popping.
            slot.bloom = Mathf.Lerp(slot.bloom, floor, recovery);
            // Floor at baseSpread (not the moving floor) so a fresh equip starts sized but shots can still sit above it.
            slot.bloom = Mathf.Clamp(slot.bloom, Mathf.Max(0f, gun.baseSpread), ceiling);
        }
        bool supported = GetSlot(Other(side)).supporting == slot.item;
        slot.noiseTime += dt * swayFrequency;

        if (UpdateIdealPoint(side, slot, dt, coneOrigin) && slot.item != null)
        {
            Item item = slot.item;
            // The anchor trails the ideal point, so a target switch slides the real point across depth
            // instead of teleporting it.
            if (!slot.hasAnchor)
            {
                slot.anchorPoint = slot.idealPoint;
                slot.hasAnchor = true;
            }
            else
            {
                slot.anchorPoint = Vector3.Lerp(slot.anchorPoint, slot.idealPoint, 1f - Mathf.Exp(-Mathf.Max(0f, item.idealFollowSpeed) * dt));
            }
            // Display only: the cursor frame trails the ideal point so a lock-on snap glides on screen.
            // Nothing reads it but TryGetAimPoints, so shots and the dot stay exact.
            if (!slot.hasCursor || cursorFollowSpeed <= 0f)
            {
                slot.cursorPoint = slot.idealPoint;
                slot.hasCursor = true;
            }
            else
            {
                slot.cursorPoint = Vector3.Lerp(slot.cursorPoint, slot.idealPoint, 1f - Mathf.Exp(-cursorFollowSpeed * dt));
            }

            // The reticle is a disk of bloom degrees seen from the eye, so the real point lives in the plane
            // facing the eye. A sphere would project centre-heavy, and its depth axis would never show on screen.
            ReticleFrame(slot, coneOrigin, out Vector3 eye, out Vector3 screenRight, out Vector3 screenUp, out float dist);
            float radius = DegToMetres(slot.bloom, dist);
            Vector2 d = item.usesAiming ? SwayDisk(slot.noiseSeed, slot.noiseTime) : Vector2.zero;
            Vector3 swayTarget = (screenRight * d.x + screenUp * d.y) * (radius * item.swayFill * (supported ? supportSwayScale : 1f));

            Vector3 realOff = slot.realOffset;
            Vector3 realVel = slot.realVelocity;
            StepSpring(ref realOff, ref realVel, swayTarget, offsetFrequency, offsetDampingRatio, dt);
            // Flatten onto the eye-facing plane so a camera turn can't leave a depth residue. Velocity is
            // flattened too, or the spring would push the residue straight back in next frame.
            Vector3 viewDir = slot.anchorPoint - eye;
            realOff = Vector3.ProjectOnPlane(realOff, viewDir);
            realVel = Vector3.ProjectOnPlane(realVel, viewDir);
            // The full bloom radius (not scaled by the item's sway fill) is the leash, so the dot never leaves
            // the prongs, and zero bloom pins it to the anchor. Projecting back and dropping only the outward
            // velocity (not snapping) keeps the spring from buzzing against the edge.
            float offsetDist = realOff.magnitude;
            if (offsetDist > radius && offsetDist > 1e-6f)
            {
                Vector3 dir = realOff / offsetDist;
                realOff = dir * radius;
                float outward = Vector3.Dot(realVel, dir);
                if (outward > 0f) realVel -= dir * outward;
            }
            slot.realOffset = realOff;
            slot.realVelocity = realVel;
            slot.realPoint = slot.anchorPoint + slot.realOffset;
            slot.hasAimPoints = true;
        }

        // Visual kick envelope: peak * curve(t / returnTime). The curve owns the shape (hold, then ease home);
        // the display chases it at visualAttackSharpness so the jump to a new peak still reads as a snap.
        slot.recoverTime += dt;
        float envelope = RecoilEnvelope(gun, slot.recoverTime);
        float chase = 1f - Mathf.Exp(-visualAttackSharpness * dt);
        slot.kickback = Vector3.Lerp(slot.kickback, slot.kickbackPeak * envelope, chase);
        slot.flip = Vector2.Lerp(slot.flip, slot.flipPeak * envelope, chase);
    }

    // 1 right after a kick, 0 once the gun is home. Falls back to a linear 0.3 s return for items without a
    // curve, so a kick never sticks.
    static float RecoilEnvelope(Gun gun, float t)
    {
        float duration = gun != null ? Mathf.Max(0.01f, gun.recoilReturnTime) : 0.3f;
        float u = Mathf.Clamp01(t / duration);
        if (gun != null && gun.recoilReturn != null && gun.recoilReturn.length > 0)
        {
            return gun.recoilReturn.Evaluate(u);
        }
        return 1f - u;
    }

    // Semi-implicit spring pulling x toward target; serves the real aim point's offset.
    static void StepSpring(ref Vector3 x, ref Vector3 v, Vector3 target, float frequency, float dampingRatio, float dt)
    {
        if (dt <= 0f) return;
        float omega = 2f * Mathf.PI * Mathf.Max(frequency, 0.01f);
        float k = omega * omega;
        float c = 2f * dampingRatio * omega;
        // Substeps keep a stiff spring stable at low framerates.
        int steps = Mathf.Max(1, Mathf.CeilToInt(dt * 120f));
        float h = dt / steps;
        for (int i = 0; i < steps; i++)
        {
            v += (-k * (x - target) - c * v) * h;
            x += v * h;
        }
    }

    // Support only changes when a hand's contents change, so it is cached here from Equip/Release
    // instead of being re-derived every frame.
    void RefreshSupport()
    {
        right.supporting = FindSupported(right, left);
        left.supporting = FindSupported(left, right);
    }

    // The one-handed item this empty hand steadies, if any: the other hand must be placing it
    // and the item must allow a support hand.
    static Item FindSupported(HandSlot slot, HandSlot other)
    {
        if (slot.item != null) return null;
        if (other.item == null || !other.places || !other.item.CanBeSupported) return null;
        return other.item;
    }

    // Slides the item along its aimed barrel by the kickback offset. Position only: the angular part
    // of recoil is the real aim point's kick, which ApplyAim aims at.
    void ApplyKickback(HandSlot slot, Quaternion muzzleRotLocal, ref Vector3 pos, Quaternion rot)
    {
        if (slot.kickback.sqrMagnitude < 1e-10f) return;
        // The grip leaves the socket, so the arm IK visibly absorbs it.
        pos += (rot * muzzleRotLocal) * slot.kickback;
    }

    // Visual-only muzzle flip, in the muzzle's own frame (negative pitch lifts, positive yaw turns right),
    // rotated about the hand so the grip stays put and the arm IK follows the barrel.
    void ApplyFlip(HandSlot slot, Vector3 pivot, Quaternion muzzleRotLocal, ref Vector3 pos, ref Quaternion rot)
    {
        if (slot.flip.sqrMagnitude < 1e-10f) return;
        Quaternion muzzleRot = rot * muzzleRotLocal;
        Quaternion d = muzzleRot * Quaternion.Euler(-slot.flip.y, slot.flip.x, 0f) * Quaternion.Inverse(muzzleRot);
        rot = d * rot;
        pos = pivot + d * (pos - pivot);
    }

    // Where this hand's shots actually leave from: the muzzle after the aim solve, before kickback and
    // flip. False when the hand places nothing or hasn't been posed yet (callers fall back to the muzzle).
    public bool TryGetAimPose(HandSide side, out Vector3 pos, out Quaternion rot)
    {
        HandSlot slot = GetSlot(side);
        bool ok = slot.places && slot.item != null && slot.hasAimPose;
        pos = ok ? slot.aimMuzzlePos : default;
        rot = ok ? slot.aimMuzzleRot : Quaternion.identity;
        return ok;
    }

    public bool TryGetAimPose(Item item, out Vector3 pos, out Quaternion rot)
    {
        if (item != null && right.item == item && right.places) return TryGetAimPose(HandSide.Right, out pos, out rot);
        if (item != null && left.item == item && left.places) return TryGetAimPose(HandSide.Left, out pos, out rot);
        pos = default;
        rot = Quaternion.identity;
        return false;
    }

    bool TryGetHoldPose(HandSide side, Item item, out Vector3 pos, out Quaternion rot)
    {
        Transform socket = GetSlot(side).socket;
        if (socket == null)
        {
            pos = default;
            rot = default;
            return false;
        }
        Transform grip = item.GripFor(true);
        if (grip == null)
        {
            pos = socket.position;
            rot = socket.rotation;
            return true;
        }
        // The grip's offset from the item root is inverted onto the socket so the grip lands on the hand.
        // Measured in world space against the root (not localPosition/localRotation), so the grip can sit
        // under any bone of the item's own armature; rotation-only frame, so the root's scale can't distort it.
        Transform root = item.transform;
        Quaternion invRoot = Quaternion.Inverse(root.rotation);
        Quaternion gripRotRel = invRoot * grip.rotation;
        Vector3 gripPosRel = invRoot * (grip.position - root.position);
        rot = socket.rotation * Quaternion.Inverse(gripRotRel);
        pos = socket.position - rot * gripPosRel;
        return true;
    }

    void PoseSlot(HandSide side)
    {
        HandSlot slot = GetSlot(side);
        if (slot.item == null || !slot.places) return;
        if (!TryGetHoldPose(side, slot.item, out Vector3 basePos, out Quaternion baseRot)) return;

        Transform itemT = slot.item.transform;
        Transform muzzle = slot.item.Muzzle;
        if (muzzle == null) muzzle = itemT;

        // Muzzle offset in a rotation-only frame (not InverseTransformPoint), so the root's scale
        // can't distort it; valid while the muzzle is rigidly attached to the item.
        Quaternion invRot = Quaternion.Inverse(itemT.rotation);
        Vector3 muzzleOffset = invRot * (muzzle.position - itemT.position);
        Quaternion muzzleRotLocal = invRot * muzzle.rotation;

        Vector3 pivot = slot.socket.position;
        Vector3 pos = basePos;
        Quaternion rot = baseRot;
        // Last frame's aimed muzzle is where shots leave from, so line of sight is measured from there; before the
        // first pose, the unaimed muzzle stands in.
        Vector3 coneOrigin = slot.hasAimPose ? slot.aimMuzzlePos : basePos + baseRot * muzzleOffset;
        StepAimState(side, slot, Time.deltaTime, coneOrigin);

        ApplyAim(pivot, basePos, baseRot, muzzleOffset, muzzleRotLocal, slot.hasAimPoints ? slot.realPoint : aimTarget, ref pos, ref rot);

        // Record the aim pose here, before any visual layer: Gun fires from this and the aim cone is measured from it,
        // so kickback and flip can be exaggerated freely without moving the shots.
        slot.aimMuzzlePos = pos + rot * muzzleOffset;
        slot.aimMuzzleRot = rot * muzzleRotLocal;
        slot.hasAimPose = true;

        // Kickback slides along the aimed barrel; visual only, it never changes where the muzzle points.
        Vector3 beforeKickback = pos;
        ApplyKickback(slot, muzzleRotLocal, ref pos, rot);
        // Flip pivots about the hand, which kickback has just slid back with the item.
        ApplyFlip(slot, pivot + (pos - beforeKickback), muzzleRotLocal, ref pos, ref rot);

        // Written directly: MovePosition/MoveRotation would only apply at the next physics step.
        itemT.SetPositionAndRotation(pos, rot);
    }

    // Turns the item about the hand so its muzzle points at target (the hand's real aim point).
    // With aim weight 0 or no aimPoint the real point doesn't show; acceptable because firing always aims
    // (PlayerManager calls Mob.Aim(true) before the trigger, and Aim(false) waits 1 s).
    void ApplyAim(Vector3 pivot, Vector3 basePos, Quaternion baseRot, Vector3 muzzleOffset, Quaternion muzzleRotLocal, Vector3 target, ref Vector3 pos, ref Quaternion rot)
    {
        pos = basePos;
        rot = baseRot;
        if (aimPoint == null || aimWeight <= 0.0001f) return;

        Vector3 fromPivot = target - pivot;
        if (fromPivot.sqrMagnitude < 1e-6f) return;

        // Clamp (not skip) near aim points out to a minimum distance, so crossing the threshold doesn't pop.
        float reach = (basePos + baseRot * muzzleOffset - pivot).magnitude;
        float minDist = reach + minAimDistance;
        if (fromPivot.sqrMagnitude < minDist * minDist)
        {
            target = pivot + fromPivot.normalized * minDist;
        }

        Vector3 p = basePos;
        Quaternion r = baseRot;
        Quaternion total = Quaternion.identity;
        for (int i = 0; i < aimIterations; i++)
        {
            // Roll before the aim step so the last operation of each pass is the aim; rolling about
            // the socket shifts the barrel sideways a little and the aim step then cancels that.
            Quaternion roll = RollCorrection(r * muzzleRotLocal);
            total = roll * total;
            r = roll * r;
            p = pivot + roll * (p - pivot);

            Vector3 muzzlePos = p + r * muzzleOffset;
            Vector3 muzzleFwd = r * (muzzleRotLocal * Vector3.forward);
            Vector3 toAim = target - muzzlePos;
            if (toAim.sqrMagnitude < 1e-8f) break;
            // Nearly opposite: the FromToRotation axis is undefined.
            if (Vector3.Dot(muzzleFwd.normalized, toAim.normalized) < -0.999f) break;
            Quaternion step = Quaternion.FromToRotation(muzzleFwd, toAim);
            total = step * total;
            r = step * r;
            // Rotating about the socket keeps the grip in the hand.
            p = pivot + step * (p - pivot);
        }

        // Cap on how far the barrel swings, not on total rotation, so the roll fix doesn't eat the budget.
        Vector3 baseFwd = baseRot * (muzzleRotLocal * Vector3.forward);
        float swing = Vector3.Angle(baseFwd, total * baseFwd);
        if (swing > maxAimCorrectionAngle)
        {
            total = Quaternion.Slerp(Quaternion.identity, total, maxAimCorrectionAngle / swing);
        }
        // Weighting the accumulated rotation, not each iteration, keeps the weight linear in angle.
        total = Quaternion.Slerp(Quaternion.identity, total, aimWeight);
        rot = total * baseRot;
        pos = pivot + total * (basePos - pivot);
    }

    // Rotation about the muzzle's forward that turns its up toward the character's up, tilted by
    // rollPerYaw as the muzzle swings left/right. FromToRotation alone never touches roll, so
    // without this the item keeps whatever roll the hand bone gives it.
    Quaternion RollCorrection(Quaternion muzzleRot)
    {
        if (uprightWeight <= 0f) return Quaternion.identity;
        Vector3 fwd = muzzleRot * Vector3.forward;
        Vector3 up = transform.up;
        // Looking nearly straight up or down: "upright" is undefined, so leave the roll alone.
        if (Mathf.Abs(Vector3.Dot(fwd, up)) > 0.98f) return Quaternion.identity;

        float yaw = Vector3.SignedAngle(Vector3.ProjectOnPlane(transform.forward, up), Vector3.ProjectOnPlane(fwd, up), up);
        float rollAngle = Mathf.Clamp(yaw * rollPerYaw, -maxAimRoll, maxAimRoll);
        Vector3 desiredUp = Quaternion.AngleAxis(rollAngle, fwd) * up;

        Quaternion desired = Quaternion.LookRotation(fwd, desiredUp);
        Quaternion fix = desired * Quaternion.Inverse(muzzleRot);
        return Quaternion.Slerp(Quaternion.identity, fix, uprightWeight);
    }

    // Moved from Mob's old IK target-assignment method; kept for reference.
    // if (rightHandTarget != null && rightTarget != null)
    // {
    //     rightHandTarget.position = rightTarget.position;
    //     rightHandTarget.rotation = rightTarget.rotation;
    // }
    // else if (rightHandTarget != null && idleRightHandTarget != null)
    // {
    //     rightHandTarget.position = idleRightHandTarget.position;
    //     rightHandTarget.rotation = idleRightHandTarget.rotation;
    // }
    // if (leftHandTarget != null && leftTarget != null)
    // {
    //     leftHandTarget.position = leftTarget.position;
    //     leftHandTarget.rotation = leftTarget.rotation;
    // }
    // else if (leftHandTarget != null && idleLeftHandTarget != null)
    // {
    //     leftHandTarget.position = idleLeftHandTarget.position;
    //     leftHandTarget.rotation = idleLeftHandTarget.rotation;
    // }

    // Items are posed here, then IK is written from the final item pose in the same frame.
    // The socket is written by the rig during the Animator's evaluation, which runs after every
    // Update, so here it still holds last frame's pre-IK hand pose. It is local to the moving
    // hierarchy, so this frame's root and body movement is already included; only the animated
    // hand motion is one frame late. Posing in LateUpdate would read this frame's socket, but the
    // IK targets would then only be consumed next frame, so the hands would trail the item.
    // Posing here keeps item and hands in the same frame. A custom rig constraint inside the rig
    // graph would remove the lag, and is out of scope.
    void Update()
    {
        float target = aiming ? 1f : idleAimWeight;
        aimWeight = Mathf.Lerp(aimWeight, target, 1f - Mathf.Exp(-aimBlendSharpness * Time.deltaTime));
        UpdateLookRate(Time.deltaTime);
        UpdateAimTarget(Time.deltaTime);
        UpdateSharedTarget();

        // Both slots are posed before any IK write, so a two-handed item placed by either hand
        // is final before its off-hand IK target is written.
        PoseSlot(HandSide.Right);
        PoseSlot(HandSide.Left);
        WriteIK(HandSide.Right);
        WriteIK(HandSide.Left);
    }

    void UpdateAimTarget(float dt)
    {
        if (aimPoint == null) return;
        // Without a look source there's no camera to hold the direction from, so aim at the raw point.
        if (lookSource == null)
        {
            aimTarget = aimPoint.position;
            lookReference = aimTarget;
            return;
        }
        Vector3 eye = lookSource.position;
        Vector3 toPoint = aimPoint.position - eye;
        float distance = toPoint.magnitude;
        if (distance < 1e-4f)
        {
            aimTarget = aimPoint.position;
            lookReference = aimTarget;
            return;
        }
        if (!hasAimDistance)
        {
            aimDistance = distance;
            hasAimDistance = true;
        }
        else
        {
            float sharpness = distance < aimDistance ? aimDepthInSharpness : aimDepthOutSharpness;
            aimDistance = Mathf.Lerp(aimDistance, distance, 1f - Mathf.Exp(-sharpness * dt));
        }
        // Exact direction, eased depth: the crosshair stays on target while the gun slides across edges.
        Vector3 dir = toPoint / distance;
        aimDir = dir;
        aimTarget = eye + dir * aimDistance;

        // The part chooser measures from a smoothed "general look", not the exact crosshair direction, so
        // flicking across a gap between parts doesn't drop the lock for a frame.
        if (!hasAssistLook)
        {
            assistLookDir = dir;
            hasAssistLook = true;
        }
        else
        {
            assistLookDir = Vector3.Slerp(assistLookDir, dir, 1f - Mathf.Exp(-assistLookSharpness * dt));
        }
        lookReference = eye + assistLookDir * aimDistance;
    }

    // Steps this hand's own look direction toward the exact crosshair direction. The hand on the side the view
    // turns toward leads at leadSharpness and the other trails at trailSharpness; pitch-only motion uses the midpoint.
    // Both settle on aimDir, so the hands meet at the crosshair once the view stops. Returns false without a look
    // source or aim distance, and the hand then uses the centre axis.
    bool StepHandLook(HandSide side, HandSlot slot, float dt)
    {
        if (lookSource == null || !hasAimDistance)
        {
            slot.hasLookDir = false;
            return false;
        }
        if (!slot.hasLookDir)
        {
            slot.lookDir = aimDir;
            slot.hasLookDir = true;
            return true;
        }
        Vector3 delta = aimDir - slot.lookDir;
        float turn = 0f; // +1 when the view turns toward this hand's side, -1 when away
        if (delta.sqrMagnitude > 1e-8f) turn = Vector3.Dot(delta.normalized, lookSource.right) * (side == HandSide.Right ? 1f : -1f);
        float sharpness = Mathf.Lerp(trailSharpness, leadSharpness, 0.5f + 0.5f * turn);
        slot.lookDir = Vector3.Slerp(slot.lookDir, aimDir, 1f - Mathf.Exp(-sharpness * dt));
        return true;
    }

    // Runs once per frame before either hand is posed: finds the best part near the crosshair, scored from the
    // centre look axis, and whether both hands share it. The scan reaches the exit edge (convergeAngle times
    // convergeExitScale), not just convergeAngle, so ShouldConverge can hold a part that drifted between the two.
    // Line of sight is checked from the eye here; each hand re-checks its own when it takes the part.
    void UpdateSharedTarget()
    {
        if (!IsDualWielding || aimPoint == null || lookSource == null)
        {
            sharedTarget = null;
            converged = false;
            return;
        }
        Vector3 eye = lookSource.position;
        Vector3 axis = lookReference - eye;
        if (axis.sqrMagnitude < 1e-6f)
        {
            sharedTarget = null;
            converged = false;
            return;
        }
        axis.Normalize();
        float zone = convergeAngle * Mathf.Max(1f, convergeExitScale);

        // Zero lockPull leaves bestPoint on the edge EvaluatePart measured, so its angle is the one it tested and scored.
        AimPart best = FindBest(eye, axis, zone, eye, 0f, null, out Vector3 bestPoint, out _);
        float bestAngle = best != null ? Vector3.Angle(axis, bestPoint - eye) : float.PositiveInfinity; // infinite when no part is in the zone

        converged = ShouldConverge(best, bestAngle, converged);
        sharedTarget = converged ? best : null;
    }

    // Decides whether both hands share the best part near the crosshair this frame. centrePart is the best part
    // scored from the centre look axis (null when none is within convergeAngle * convergeExitScale), angleOffCentre
    // its degrees off the crosshair, wasConverged last frame's answer. Return true to lock both hands on centrePart.
    bool ShouldConverge(AimPart centrePart, float angleOffCentre, bool wasConverged)
    {
        // Always share: the lead/trail look already spreads the hands while the view turns, and sharing the centre part lands them on one point.
        return true;
    }

    // Picks this hand's ideal aim point: the best registered part inside its assist cone, sticky so the lock
    // doesn't flicker between parts, else this hand's look point. The cone starts at the eye (lookSource) and
    // points along this hand's lead/trail look direction, so "inside the circle on screen" is what selects; measuring
    // from the muzzle instead put an offset gun's axis beside every off-crosshair target. coneOrigin is the hand's
    // shot origin, used for line of sight, and stands in for the eye when there is no look source.
    // The no-lock fallback is that look direction at the eased aim depth. While dual-wielding, the part both
    // hands share (sharedTarget) is tried first, from the centre axis.
    // False when there is no aim point at all.
    bool UpdateIdealPoint(HandSide side, HandSlot slot, float dt, Vector3 coneOrigin)
    {
        if (aimPoint == null)
        {
            slot.target = null;
            slot.hasAimPoints = false;
            return false;
        }
        Item item = slot.item;
        Vector3 eye = lookSource != null ? lookSource.position : coneOrigin;
        Vector3 centreAxis = lookReference - eye;
        // The cone and the no-lock fallback follow this hand's lead/trail look; without one they use the centre axis and aim target.
        bool hasLook = StepHandLook(side, slot, dt);
        Vector3 axis = hasLook ? slot.lookDir : centreAxis;
        Vector3 fallback = hasLook ? eye + slot.lookDir * aimDistance : aimTarget;
        if (item == null || !item.usesAiming || axis.sqrMagnitude < 1e-6f)
        {
            slot.target = null;
            slot.idealPoint = fallback;
            return true;
        }
        axis.Normalize();
        // Bloom alone rests at a fraction of a degree, so assistAngle keeps the cone usable on a calm gun.
        float coneAngle = slot.bloom + Mathf.Max(0f, item.assistAngle);

        // The shared part is measured from the centre axis, with a cone that holds it anywhere in the shared zone.
        // This hand's own line of sight is still checked, so if it can't see the part it scans on its own.
        if (IsDualWielding && sharedTarget != null)
        {
            float sharedCone = coneAngle + convergeAngle * Mathf.Max(1f, convergeExitScale);
            if (EvaluatePart(sharedTarget, eye, centreAxis.normalized, sharedCone, coneOrigin, item.lockPull, out Vector3 sharedPoint, out float sharedScore))
            {
                slot.target = sharedTarget;
                slot.targetScore = sharedScore;
                slot.idealPoint = sharedPoint;
                return true;
            }
        }

        // The current lock is re-scored first; it is dropped if it left the cone, died or got blocked.
        AimPart current = slot.target;
        Vector3 currentPoint = default;
        float currentScore = 0f;
        if (current != null && !EvaluatePart(current, eye, axis, coneAngle, coneOrigin, item.lockPull, out currentPoint, out currentScore))
        {
            current = null;
        }

        // Challengers come object first; the current lock is skipped since it was just re-scored.
        AimPart best = FindBest(eye, axis, coneAngle, coneOrigin, item.lockPull, current, out Vector3 bestPoint, out float bestScore);

        // A challenger only takes the lock by beating the current part by the item's stickiness fraction.
        if (current == null || (best != null && bestScore < currentScore * (1f - item.stickiness)))
        {
            current = best;
            currentPoint = bestPoint;
            currentScore = bestScore;
        }

        slot.target = current;
        slot.targetScore = current != null ? currentScore : 0f;
        slot.idealPoint = current != null ? currentPoint : fallback;
        return true;
    }

    // The best candidate in the cone, object first: each registered AimBody is gated on its whole footprint, then
    // only its hitbox parts are scored, or its body colliders when it has none. skip is left out because the caller
    // re-scores its current lock itself. Returns null when nothing qualifies.
    AimPart FindBest(Vector3 origin, Vector3 axis, float coneAngle, Vector3 shotOrigin, float lockPull, AimPart skip, out Vector3 bestPoint, out float bestScore)
    {
        AimPart best = null;
        bestPoint = default;
        bestScore = float.PositiveInfinity;
        // By index: the registry is a plain list, and Object enable/disable can't run mid-scan.
        IReadOnlyList<AimBody> bodies = AimBody.Active;
        for (int i = 0; i < bodies.Count; i++)
        {
            AimBody body = bodies[i];
            if (!BodyGate(body, origin, axis, coneAngle)) continue;
            List<AimPart> candidates = body.parts.Count > 0 ? body.parts : body.bodies;
            for (int j = 0; j < candidates.Count; j++)
            {
                AimPart part = candidates[j];
                if (part == skip) continue;
                if (!EvaluatePart(part, origin, axis, coneAngle, shotOrigin, lockPull, out Vector3 point, out float score)) continue;
                if (score < bestScore)
                {
                    best = part;
                    bestPoint = point;
                    bestScore = score;
                }
            }
        }
        return best;
    }

    // Whether part is a valid candidate for a hand cone (eye origin, unit axis, half-angle in degrees), and if
    // so its lock point and score. The lock point is the part's point nearest the cone axis, pulled toward the
    // part's centre by lockPull (0..1). Line of sight and the scored distance come from shotOrigin, so a part
    // the eye sees but the gun can't hit is skipped. The cheap distance and bounds tests live in ColliderInCone,
    // shared with the object gate, and run before any ClosestPoint or raycast.
    bool EvaluatePart(AimPart part, Vector3 origin, Vector3 axis, float coneAngle, Vector3 shotOrigin, float lockPull, out Vector3 point, out float score)
    {
        point = default;
        score = float.PositiveInfinity;
        if (part == null) return false;
        Collider col = part.collider;
        if (!AimPart.IsLive(col)) return false;
        // Unity null check turns a destroyed Hitbox into a real null for ScorePart.
        Hitbox hitbox = part.hitbox != null ? part.hitbox : null;
        if (part.owner != null && (part.owner.destroyed || part.owner == self)) return false;
        if (col.transform.IsChildOf(transform)) return false;

        // The cone test and the score use the edge point, so the pull moves where a lock lands, never which part wins.
        if (!ColliderInCone(col, origin, axis, coneAngle, out point, out float angleOffCentre)) return false;

        // The pulled point must be visible too; if a limb or cover hides it, settle for the edge.
        Vector3 edge = point;
        if (lockPull > 0f) point = Vector3.Lerp(edge, ClosestOn(col, col.bounds, col.bounds.center), Mathf.Clamp01(lockPull));
        if (!HasLineOfSight(shotOrigin, point, col, part.owner))
        {
            if (point == edge || !HasLineOfSight(shotOrigin, edge, col, part.owner)) return false;
            point = edge;
        }

        score = ScorePart(col, hitbox, OwnerLayerWeight(part.owner), angleOffCentre, coneAngle, Vector3.Distance(shotOrigin, point));
        return !float.IsNaN(score) && !float.IsInfinity(score);
    }

    // The object-first gate: true when the cone touches any of body's colliders. No line of sight here,
    // because EvaluatePart checks whatever passes.
    bool BodyGate(AimBody body, Vector3 origin, Vector3 axis, float coneAngle)
    {
        // Unity null check also catches a destroyed owner.
        if (body == null || body.owner == null || body.owner.destroyed || body.owner == self) return false;
        if (!body.TryGetBounds(out Bounds bounds)) return false;
        Vector3 toCentre = bounds.center - origin;
        float centreDist = toCentre.magnitude;
        float radius = bounds.extents.magnitude;
        // Range is measured to the sphere's near side, so a big object whose centre is out of range still
        // gates in when its parts aren't.
        if (centreDist - radius > assistRange) return false;
        if (!SphereInCone(toCentre, centreDist, radius, axis, coneAngle)) return false;
        // Bodies first, then parts, so a head poking out of the movement capsule still gates its owner in.
        return AnyInCone(body.bodies, origin, axis, coneAngle) || AnyInCone(body.parts, origin, axis, coneAngle);
    }

    bool AnyInCone(List<AimPart> list, Vector3 origin, Vector3 axis, float coneAngle)
    {
        for (int i = 0; i < list.Count; i++)
        {
            Collider col = list[i].collider;
            if (AimPart.IsLive(col) && ColliderInCone(col, origin, axis, coneAngle, out _, out _)) return true;
        }
        return false;
    }

    // Pure cone geometry for one live collider: false when it is out of range or no part of it is inside the
    // cone. point is its point nearest the axis, and angle is that point's degrees off it.
    bool ColliderInCone(Collider col, Vector3 origin, Vector3 axis, float coneAngle, out Vector3 point, out float angle)
    {
        point = default;
        angle = float.PositiveInfinity;
        Bounds bounds = col.bounds;
        Vector3 toCentre = bounds.center - origin;
        float centreDist = toCentre.magnitude;
        if (centreDist > assistRange) return false;
        if (!SphereInCone(toCentre, centreDist, bounds.extents.magnitude, axis, coneAngle)) return false;

        // Nearest point to the aim ray, in two passes: the centre's projection onto the ray pulled onto the
        // collider, then that point's projection pulled on again, which settles close to the true nearest point.
        point = ClosestOn(col, bounds, origin + axis * Mathf.Max(0f, Vector3.Dot(toCentre, axis)));
        point = ClosestOn(col, bounds, origin + axis * Mathf.Max(0f, Vector3.Dot(point - origin, axis)));
        angle = Vector3.Angle(axis, point - origin);
        return angle <= coneAngle;
    }

    // Bounding-sphere angular test: false only when the whole sphere sits outside the cone. An eye inside
    // the sphere always passes.
    static bool SphereInCone(Vector3 toCentre, float centreDist, float radius, Vector3 axis, float coneAngle)
    {
        if (centreDist <= 1e-4f) return true;
        float angularRadius = Mathf.Asin(Mathf.Clamp01(radius / centreDist)) * Mathf.Rad2Deg;
        return Vector3.Angle(axis, toCentre) - angularRadius <= coneAngle;
    }

    // ClosestPoint only supports box, sphere, capsule and convex mesh colliders. Anything else (a non-convex
    // mesh body, a terrain) uses its world bounds, so the gate and the lock point stay meaningful.
    static Vector3 ClosestOn(Collider col, Bounds bounds, Vector3 p)
    {
        bool supported = col is BoxCollider || col is SphereCollider || col is CapsuleCollider || (col is MeshCollider mesh && mesh.convex);
        return supported ? col.ClosestPoint(p) : bounds.ClosestPoint(p);
    }

    // True when nothing but the rig itself (body or held items) sits between origin and the part's point.
    // RaycastNonAlloc results are unsorted, so the nearest non-own hit is picked by hand.
    // The part owner's own non-part colliders (e.g. a movement capsule around its limbs) don't block, but its
    // other registered parts do, so an arm can still hide the head.
    bool HasLineOfSight(Vector3 origin, Vector3 point, Collider partCollider, Object partOwner)
    {
        Vector3 delta = point - origin;
        float dist = delta.magnitude;
        if (dist < 1e-4f) return true;
        int count = Physics.RaycastNonAlloc(origin, delta / dist, losHits, dist + 0.05f, assistMask, QueryTriggerInteraction.Ignore);
        Collider nearest = null;
        float nearestDist = float.PositiveInfinity;
        for (int i = 0; i < count; i++)
        {
            Collider c = losHits[i].collider;
            if (c == null || IsOwnCollider(c)) continue;
            if (losHits[i].distance >= nearestDist) continue;
            // Ownership is only resolved for hits that would become the nearest, which keeps the parent walks rare.
            if (c != partCollider && IsOwnerBody(c, partOwner)) continue;
            nearestDist = losHits[i].distance;
            nearest = c;
        }
        return nearest == null || nearest == partCollider;
    }

    // True when c belongs to owner but isn't one of its registered aim parts.
    static bool IsOwnerBody(Collider c, Object owner)
    {
        if (owner == null || AimPart.IsRegistered(c)) return false;
        return c.GetComponentInParent<Object>() == owner;
    }

    // Aim priority for a part's owner. Uses the owner's own layer, not the collider's, so limbs on the
    // Hitbox layer still rank by who they belong to.
    float OwnerLayerWeight(Object owner)
    {
        if (owner == null) return defaultLayerWeight;
        int layer = owner.gameObject.layer;
        for (int i = 0; i < ownerLayerWeights.Count; i++)
        {
            LayerWeight entry = ownerLayerWeights[i];
            if (entry != null && (entry.layers.value & (1 << layer)) != 0) return entry.weight;
        }
        return defaultLayerWeight;
    }

    bool IsOwnCollider(Collider c)
    {
        Transform t = c.transform;
        if (t.IsChildOf(transform)) return true;
        if (right.item != null && t.IsChildOf(right.item.transform)) return true;
        if (left.item != null && t.IsChildOf(left.item.transform)) return true;
        return false;
    }

    /// <summary>
    /// Cost of locking this hand onto a part; lower wins, like angleOffCentre / (aimWeight * ownerLayerWeight).
    /// Return float.PositiveInfinity to skip a part entirely (e.g. teammates or broken parts).
    /// col is the part's collider. hitbox may be null for a plain collider on the Hitbox layer; treat
    /// aimWeight as the default 1 then. ownerLayerWeight is what ownerLayerWeights gives the owner's layer,
    /// or defaultLayerWeight (1) when that layer isn't listed.
    /// angleOffCentre is degrees off the eye's look axis (how far from the crosshair); coneAngle is the assist
    /// cone half-angle (bloom + the item's assistAngle), for normalising; distance is meters from the shot
    /// origin to the part's nearest point.
    /// </summary>
    float ScorePart(Collider col, Hitbox hitbox, float ownerLayerWeight, float angleOffCentre, float coneAngle, float distance)
    {
        // TODO(human): weigh angleOffCentre, ownerLayerWeight, hitbox.aimWeight (1 when hitbox is null), distance and anything else you want (team, hitbox.IsBroken, ...) into one cost.
        return angleOffCentre;
    }

    static float DegToMetres(float degrees, float distance)
    {
        return distance * Mathf.Tan(Mathf.Clamp(degrees, 0f, 89f) * Mathf.Deg2Rad);
    }

    // The plane this hand's reticle is drawn in: the eye, its right and up, and the eye-to-anchor distance
    // that bloom degrees convert at. Sway and kicks share it, so a kick of one bloom radius lands on the edge.
    void ReticleFrame(HandSlot slot, Vector3 fallbackEye, out Vector3 eye, out Vector3 screenRight, out Vector3 screenUp, out float dist)
    {
        eye = lookSource != null ? lookSource.position : fallbackEye;
        screenRight = lookSource != null ? lookSource.right : slot.aimMuzzleRot * Vector3.right;
        screenUp = lookSource != null ? lookSource.up : slot.aimMuzzleRot * Vector3.up;
        dist = Mathf.Max(0.1f, Vector3.Distance(eye, slot.anchorPoint));
    }

    // A point in the unit disk that covers it evenly over a few seconds. The angle keeps orbiting at a
    // wandering speed that can briefly reverse. The radius is a triangle wave with a noisy phase, so it spends
    // equal time at every u in 0..1, and the square root makes that coverage even by area, not centre-heavy.
    static Vector2 SwayDisk(float seed, float t)
    {
        float theta = 2f * Mathf.PI * (t * 0.37f + Mathf.PerlinNoise(seed, t * 0.5f) * 2f);
        float u = Mathf.Abs(Frac(t * 0.61f + Mathf.PerlinNoise(seed + 37.1f, t * 0.3f)) * 2f - 1f);
        float r = Mathf.Sqrt(u);
        return new Vector2(Mathf.Cos(theta), Mathf.Sin(theta)) * r;
    }

    // Fractional part, wrapping negatives into 0..1 too (unlike x % 1).
    static float Frac(float x)
    {
        return x - Mathf.Floor(x);
    }

    // Turns from the aimed muzzle toward a world point, keeping the muzzle's up; the muzzle's own rotation
    // when there are no aim points yet or the point sits on the muzzle.
    Quaternion LookFromMuzzle(HandSlot slot, Vector3 point)
    {
        Vector3 dir = point - slot.aimMuzzlePos;
        if (!slot.hasAimPoints || dir.sqrMagnitude < 1e-6f) return slot.aimMuzzleRot;
        return Quaternion.LookRotation(dir, slot.aimMuzzleRot * Vector3.up);
    }

    // Everything a shot needs: the aimed muzzle (where shots leave from) and the rotation from it toward the
    // real point (the dot), which is the direction every shot takes. The rotation toward the ideal point (the
    // reticle's centre) and the current bloom radius are reported for UI and debugging.
    public bool TryGetShotCone(Item item, out Vector3 origin, out Quaternion aimRot, out Quaternion idealRot, out float bloom)
    {
        HandSlot slot = null;
        if (item != null && right.item == item && right.places) slot = right;
        else if (item != null && left.item == item && left.places) slot = left;
        bool ok = slot != null && slot.hasAimPose;
        origin = ok ? slot.aimMuzzlePos : default;
        aimRot = ok ? LookFromMuzzle(slot, slot.realPoint) : Quaternion.identity;
        idealRot = ok ? LookFromMuzzle(slot, slot.idealPoint) : Quaternion.identity;
        bloom = ok ? slot.bloom : 0f;
        return ok;
    }

    // Where this hand's gun would point with no sway or kick: the aimed muzzle turned toward the ideal point.
    public bool TryGetIdealPose(HandSide side, out Vector3 pos, out Quaternion rot)
    {
        HandSlot slot = GetSlot(side);
        bool ok = slot.places && slot.item != null && slot.hasAimPose;
        pos = ok ? slot.aimMuzzlePos : default;
        rot = ok ? LookFromMuzzle(slot, slot.idealPoint) : Quaternion.identity;
        return ok;
    }

    // Measures how fast the view is turning from the look source's rotation change, so mouse and gamepad
    // behave the same. Yaw is around the character's up; pitch is the change in elevation.
    void UpdateLookRate(float dt)
    {
        if (lookSource == null || dt <= 0f) return;
        Vector3 up = transform.up;
        Vector3 fwd = lookSource.forward;
        Vector2 raw = Vector2.zero;
        if (hasLastLook)
        {
            float yaw = Vector3.SignedAngle(Vector3.ProjectOnPlane(lastLookForward, up), Vector3.ProjectOnPlane(fwd, up), up);
            float pitch = (Mathf.Asin(Mathf.Clamp(Vector3.Dot(fwd, up), -1f, 1f)) - Mathf.Asin(Mathf.Clamp(Vector3.Dot(lastLookForward, up), -1f, 1f))) * Mathf.Rad2Deg;
            raw = new Vector2(yaw, pitch) / dt;
        }
        lastLookForward = fwd;
        hasLastLook = true;
        lookRate = Vector2.Lerp(lookRate, raw, 1f - Mathf.Exp(-lookSmoothing * dt));
    }

    void WriteIK(HandSide side)
    {
        HandSlot slot = GetSlot(side);
        Transform grip = null;
        if (slot.item != null) grip = slot.item.GripFor(slot.places);
        else if (slot.supporting != null) grip = slot.supporting.GripFor(false);
        float weight;
        if (grip != null && slot.ikTarget != null)
        {
            slot.ikTarget.SetPositionAndRotation(grip.position, grip.rotation);
            weight = 1f;
        }
        else
        {
            weight = 0f;
            // Park the target on the socket so re-enabling IK doesn't jump from a stale pose.
            if (slot.ikTarget != null && slot.socket != null)
            {
                slot.ikTarget.SetPositionAndRotation(slot.socket.position, slot.socket.rotation);
            }
        }
        if (slot.ikRig != null) slot.ikRig.weight = weight;
    }
}
