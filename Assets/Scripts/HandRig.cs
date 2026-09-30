using UnityEngine;
using System;
using UnityEngine.Animations.Rigging;

// Right must stay first (value 0) so existing serialized Item data defaults to right-handed.
public enum HandSide
{
    Right,
    Left
}

// Owns both hands: which item each hand holds, that item's pose (hand socket, then the muzzle aim
// correction toward the aim point rotated by the hand's sway/recoil offset, then kickback), and the
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
        [System.NonSerialized] public Vector2 offset; // degrees the muzzle points away from the ideal aim direction, in the muzzle's own frame; x right, y up
        [System.NonSerialized] public Vector2 offsetVelocity; // deg/s
        [System.NonSerialized] public Vector2 flip; // degrees of visual-only muzzle flip, muzzle frame (x right, y up); never affects aim
        [System.NonSerialized] public Vector2 flipPeak; // degrees; what the last shot pushed flip to, scaled down by the return curve
        [System.NonSerialized] public float recoverTime; // seconds since the last kick; drives the gun's recoilReturn curve
        [System.NonSerialized] public Vector3 aimMuzzlePos; // muzzle pose after the aim solve, before kickback/flip: where shots actually leave from
        [System.NonSerialized] public Quaternion aimMuzzleRot = Quaternion.identity;
        [System.NonSerialized] public bool hasAimPose; // false until the slot has been posed at least once
        [System.NonSerialized] public Vector3 idealMuzzlePos; // muzzle pose from the same solve with zero offset: where the gun points without sway/kick
        [System.NonSerialized] public Quaternion idealMuzzleRot = Quaternion.identity;
        [System.NonSerialized] public Vector2 restPoint; // unit disk; where firing last sent the dot, scaled by the live bloom
        [System.NonSerialized] public float lastRerollTime = -999f; // Time.time the rest point was last re-rolled
        [System.NonSerialized] public float noiseSeed; // per hand, so the hands never sway in sync
        [System.NonSerialized] public float noiseTime; // advances at swayFrequency
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
    public float offsetFrequency = 6f; // Hz; how quickly the aim offset follows the sway target and recovers from kicks
    [Range(0.1f, 2f)] public float offsetDampingRatio = 1f; // 1 = critically damped, no overshoot
    public float swayFrequency = 0.5f; // noise units per second; how fast the sway target wanders
    [UnityEngine.Serialization.FormerlySerializedAs("maxRecoilAngle")] public float maxAimOffset = 25f; // degrees; caps stacked kicks from automatic fire
    [UnityEngine.Serialization.FormerlySerializedAs("supportRecoilScale")] [Range(0f, 1f)] public float supportBloomScale = 0.5f; // bloom-per-shot multiplier while a free hand steadies a one-handed item
    [Range(0f, 1f)] public float supportSwayScale = 0.5f; // sway-radius multiplier while a free hand steadies a one-handed item
    [Header("Look and Movement")]
    public Transform lookSource; // whose rotation counts as "looking"; defaults to Camera.main
    public Rigidbody moveBody; // whose speed counts as "moving" (airborne included, since it's speed-based); defaults to this object's Rigidbody
    public float lookSmoothing = 12f; // per second; smooths the measured look rate so single-frame mouse spikes don't jolt the dot
    public float fullDragLookSpeed = 180f; // deg/s of look at which lookDragCurve reaches its end
    // Look speed (0..1 of fullDragLookSpeed) to drag strength (0..1, times the gun's lookDrag, as a fraction
    // of the bloom radius). The spring provides the lag in time; this only shapes how far the dot trails.
    public AnimationCurve lookDragCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    [Header("Visual Kick")]
    // Cosmetic kickback slide and muzzle flip, layered after the aim pose is recorded, so they can be as big
    // as you like without moving shots or the cursor dot. Each shot sets a peak; the held gun's recoilReturn
    // curve over recoilReturnTime scales it back to zero, so the gun can hang up and then ease home.
    public float visualAttackSharpness = 30f; // per second; how fast the display chases the envelope, so the snap-up isn't a one-frame pop
    [UnityEngine.Serialization.FormerlySerializedAs("maxRecoilDistance")] public float maxKickback = 0.2f; // meters; clamps stacked kickback
    public float maxFlip = 45f; // degrees; clamps stacked flips from automatic fire

    float aimWeight;
    Vector3 aimTarget; // smoothed-depth copy of aimPoint the guns actually aim at
    float aimDistance; // smoothed camera-to-aimPoint distance
    bool hasAimDistance;
    Vector2 lookRate; // smoothed deg/s, x = yaw (right +), y = pitch (up +)
    Vector3 lastLookForward;
    bool hasLastLook;

    public bool IsEmpty
    {
        get { return right.item == null && left.item == null; }
    }

    void Awake()
    {
        // Start at the resting weight so there is no blend-in on the first frame.
        aimWeight = idleAimWeight;
        if (lookSource == null && Camera.main != null) lookSource = Camera.main.transform;
        if (moveBody == null) moveBody = GetComponent<Rigidbody>();
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

    // Degrees this hand's muzzle points off the aim point (muzzle frame, x right, y up), or zero when it
    // places nothing. For UI and debugging.
    public Vector2 GetAimOffset(HandSide side)
    {
        HandSlot slot = GetSlot(side);
        return slot.places && slot.item != null ? slot.offset : Vector2.zero;
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
        slot.offset = Vector2.zero;
        slot.offsetVelocity = Vector2.zero;
        slot.flip = Vector2.zero;
        slot.flipPeak = Vector2.zero;
        slot.recoverTime = 0f;
        slot.restPoint = Vector2.zero;
        slot.lastRerollTime = -999f;
        slot.hasAimPose = false;
        slot.bloom = 0f;
        // noiseSeed and noiseTime are left alone so re-equips don't restart the sway.
    }

    // Kicks the aim state of the slot placing this item. kickback is meters in the muzzle's local
    // frame (back is -z). bloom is degrees added to the hand's spread, scaled by supportBloomScale while
    // the other hand steadies the item. The aim offset is not thrown: the shot only picks a new rest point
    // and the offset spring carries the dot there, so every place the dot visibly goes is somewhere a later
    // shot can land. The punch you see comes from the visual flip instead: flipRise along the muzzle's up
    // (so a rolled or swaying gun lifts along its own tilt), flipSide along the offset's current sideways
    // drift, in degrees. Kickback and flip stack on what is currently shown and restart the return curve,
    // so automatic fire stays up until you stop.
    // Returns false when this rig isn't placing the item.
    public bool Kick(Item item, Vector3 kickback, float bloom, float flipRise = 0f, float flipSide = 0f)
    {
        if (item == null) return false;
        HandSlot slot = null;
        HandSlot other = null;
        if (right.item == item && right.places) { slot = right; other = left; }
        else if (left.item == item && left.places) { slot = left; other = right; }
        if (slot == null) return false;
        bool supported = other.supporting == item;
        slot.bloom += bloom * (supported ? supportBloomScale : 1f);
        float drift = Mathf.Abs(slot.offsetVelocity.x) > 0.01f ? Mathf.Sign(slot.offsetVelocity.x) : 0f;
        // Where firing sends the dot: anywhere in the bloom circle, evenly by area (insideUnitCircle is
        // area-uniform, so it doesn't cluster at the center). Re-rolled on the gun's interval, not every shot:
        // a fast gun re-rolling per shot hands the offset spring a new random target before it can arrive, so
        // the aim averages them out near the center. Waiting lets the dot actually travel, and every bullet
        // fired on the way goes exactly where the dot is.
        Gun gun = item as Gun;
        float interval = gun != null ? gun.restRerollInterval : 0f;
        if (Time.time - slot.lastRerollTime >= interval)
        {
            slot.restPoint = UnityEngine.Random.insideUnitCircle;
            slot.lastRerollTime = Time.time;
        }
        slot.flipPeak = Vector2.ClampMagnitude(slot.flip + new Vector2(drift * flipSide, flipRise), maxFlip);
        slot.kickbackPeak = Vector3.ClampMagnitude(slot.kickback + kickback, maxKickback);
        slot.recoverTime = 0f;
        return true;
    }

    // Advances the slot's angular aim state (bloom, sway target, aim offset) and its kickback by dt.
    void StepAimState(HandSide side, HandSlot slot, float dt)
    {
        Gun gun = slot.item as Gun;
        // Non-gun items have no spread or sway, but both springs still step so a kick never sticks.
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
        // The rest point does not fade on its own: it's scaled by the live bloom, so the reticle shrinking
        // is what brings the dot back in. A separate fade made it shrink twice as fast as the reticle.

        bool supported = GetSlot(Other(side)).supporting == slot.item;
        slot.noiseTime += dt * swayFrequency;
        Vector2 n = new Vector2(
            Mathf.PerlinNoise(slot.noiseSeed, slot.noiseTime) * 2f - 1f,
            Mathf.PerlinNoise(slot.noiseSeed + 37.1f, slot.noiseTime) * 2f - 1f);
        n = Vector2.ClampMagnitude(n, 1f);
        // Target = where the last shot threw the dot + sway around it + drag trailing the look, all as
        // fractions of the bloom radius; the leash below keeps the sum inside the circle.
        Vector2 swayTarget = Vector2.zero;
        if (gun != null)
        {
            Vector2 sway = n * (gun.swayAmount * (supported ? supportSwayScale : 1f));
            // Trail behind the turn: turning right leaves the dot left, looking up leaves it low.
            float lookT = fullDragLookSpeed > 0f ? Mathf.Clamp01(lookRate.magnitude / fullDragLookSpeed) : 0f;
            float dragAmount = lookDragCurve != null && lookDragCurve.length > 0 ? lookDragCurve.Evaluate(lookT) : lookT;
            Vector2 drag = lookRate.sqrMagnitude > 1e-6f ? -lookRate.normalized * (dragAmount * gun.lookDrag) : Vector2.zero;
            swayTarget = (slot.restPoint + sway + drag) * slot.bloom;
        }

        Vector3 offset = slot.offset;
        Vector3 offsetVelocity = slot.offsetVelocity;
        StepSpring(ref offset, ref offsetVelocity, swayTarget, offsetFrequency, offsetDampingRatio, dt);
        // The bloom circle is the leash: a hard kick rides its edge instead of leaving the reticle. Kick grows
        // bloom before this step runs, so heavy guns still get a wide circle to kick into. Projecting back and
        // dropping only the outward velocity (not snapping) keeps the spring from buzzing against the edge.
        float leash = gun != null ? Mathf.Min(slot.bloom, maxAimOffset) : maxAimOffset;
        float dist = offset.magnitude;
        if (dist > leash && dist > 1e-6f)
        {
            Vector3 dir = offset / dist;
            offset = dir * leash;
            float outward = Vector3.Dot(offsetVelocity, dir);
            if (outward > 0f) offsetVelocity -= dir * outward;
        }
        slot.offset = offset;
        slot.offsetVelocity = offsetVelocity;

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

    // Semi-implicit spring pulling x toward target; serves the aim offset.
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
    // of recoil is the aim offset, applied inside ApplyAim.
    void ApplyKickback(HandSlot slot, Quaternion muzzleRotLocal, ref Vector3 pos, Quaternion rot)
    {
        if (slot.kickback.sqrMagnitude < 1e-10f) return;
        // The grip leaves the socket, so the arm IK visibly absorbs it.
        pos += (rot * muzzleRotLocal) * slot.kickback;
    }

    // Visual-only muzzle flip, in the muzzle's own frame (same convention as the aim offset: negative
    // pitch lifts), rotated about the hand so the grip stays put and the arm IK follows the barrel.
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
        StepAimState(side, slot, Time.deltaTime);
        // A second solve with zero offset gives where the gun points without sway or kick, after the same
        // aim weight and cap, so the cursor's center is measured rather than guessed from the offset.
        Vector3 idealPos = basePos;
        Quaternion idealRot = baseRot;
        ApplyAim(pivot, basePos, baseRot, muzzleOffset, muzzleRotLocal, Vector2.zero, ref idealPos, ref idealRot);
        slot.idealMuzzlePos = idealPos + idealRot * muzzleOffset;
        slot.idealMuzzleRot = idealRot * muzzleRotLocal;

        ApplyAim(pivot, basePos, baseRot, muzzleOffset, muzzleRotLocal, slot.offset, ref pos, ref rot);

        // Record the aim pose here, before any visual layer: Gun fires and AimCursor casts from this,
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

    void ApplyAim(Vector3 pivot, Vector3 basePos, Quaternion baseRot, Vector3 muzzleOffset, Quaternion muzzleRotLocal, Vector2 offset, ref Vector3 pos, ref Quaternion rot)
    {
        pos = basePos;
        rot = baseRot;
        if (aimPoint == null || aimWeight <= 0.0001f) return;

        Vector3 target = aimTarget;
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
            // This is target' = muzzlePos + Rotate(dir, offset) * dist in the muzzle's own frame. It is recomputed
            // each pass so it uses the upright-corrected up. Because it happens inside the solve, bullets fired
            // along the muzzle land off the aim point by exactly the offset. Negative pitch lifts, positive yaw turns right.
            // With aim weight 0 or no aimPoint the offset doesn't show; acceptable because firing always aims
            // (PlayerManager calls Mob.Aim(true) before the trigger, and Aim(false) waits 1 s).
            if (offset.sqrMagnitude > 1e-10f)
            {
                Quaternion muzzleRot = r * muzzleRotLocal;
                toAim = muzzleRot * Quaternion.Euler(-offset.y, offset.x, 0f) * Quaternion.Inverse(muzzleRot) * toAim;
            }
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
            return;
        }
        Vector3 eye = lookSource.position;
        Vector3 toPoint = aimPoint.position - eye;
        float distance = toPoint.magnitude;
        if (distance < 1e-4f)
        {
            aimTarget = aimPoint.position;
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
        aimTarget = eye + toPoint / distance * aimDistance;
    }

    // Where this hand's gun points with no sway or kick (same solve, zero offset). For the cursor's center.
    public bool TryGetIdealPose(HandSide side, out Vector3 pos, out Quaternion rot)
    {
        HandSlot slot = GetSlot(side);
        bool ok = slot.places && slot.item != null && slot.hasAimPose;
        pos = ok ? slot.idealMuzzlePos : default;
        rot = ok ? slot.idealMuzzleRot : Quaternion.identity;
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
