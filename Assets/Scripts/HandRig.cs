using UnityEngine;
using System;
using UnityEngine.Animations.Rigging;

// Right must stay first (value 0) so existing serialized Item data defaults to right-handed.
public enum HandSide
{
    Right,
    Left
}

// Owns both hands: which item each hand holds, that item's pose (hand socket, then muzzle aim
// correction, then recoil spring), and the TwoBoneIK targets. This is the only script that writes IK targets.
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
        [System.NonSerialized] public Rigidbody body; // the placed item's body, held kinematic while in this hand
        [System.NonSerialized] public bool savedKinematic; // body.isKinematic from before it was held, restored on release
        [System.NonSerialized] public RigidbodyInterpolation savedInterpolation; // body.interpolation from before it was held, restored on release
        [System.NonSerialized] public Vector3 recoilPos; // meters, muzzle-local recoil offset
        [System.NonSerialized] public Vector3 recoilPosVelocity; // m/s, muzzle-local
        [System.NonSerialized] public Vector3 recoilRot; // degrees, muzzle-local Euler recoil offset
        [System.NonSerialized] public Vector3 recoilRotVelocity; // deg/s, muzzle-local
    }

    [Header("Hands")]
    public HandSlot right = new HandSlot();
    public HandSlot left = new HandSlot();
    [Header("Aim")]
    public bool aiming; // while true, the aim weight blends toward 1
    public Transform aimPoint; // shared point AimInput moves; placed items turn their muzzle toward it
    [Range(0f, 1f)] public float idleAimWeight = 0f; // correction applied when not aiming (0 = pure animation)
    public float aimBlendSharpness = 10f; // per second; higher blends faster; framerate independent
    [Range(1, 4)] public int aimIterations = 2; // correction passes; the muzzle is offset from the pivot, so one pass undershoots
    public float maxAimCorrectionAngle = 60f; // degrees; caps the correction so aim points behind or far off-axis can't spin the item
    public float minAimDistance = 0.5f; // meters beyond the muzzle's reach from the socket; closer aim points are pushed out to this
    [Header("Recoil")]
    public float recoilFrequency = 8f; // Hz; the spring's natural frequency; higher snaps back faster
    [Range(0f, 2f)] public float recoilDampingRatio = 0.6f; // 1 = critically damped; below 1 overshoots slightly
    public float maxRecoilDistance = 0.2f; // meters; clamps stacked kicks from automatic fire
    public float maxRecoilAngle = 25f; // degrees; same clamp for rotation

    float aimWeight;

    public bool IsEmpty
    {
        get { return right.item == null && left.item == null; }
    }

    void Awake()
    {
        // Start at the resting weight so there is no blend-in on the first frame.
        aimWeight = idleAimWeight;
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
        ResetRecoil(slot);

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

        WarnIfGripNotChild(item, item.GripFor(side));
        if (twoHanded) WarnIfGripNotChild(item, item.GripFor(otherSide));

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

    void WarnIfGripNotChild(Item item, Transform grip)
    {
        if (grip == null || grip.parent == item.transform) return;
        Debug.LogWarning($"{name}: grip {grip.name} on {item.name} is not a direct child of the item; the socket formula assumes direct children at unit scale.", this);
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
        ResetRecoil(slot);
        slot.item = null;
        slot.places = false;
    }

    void ResetRecoil(HandSlot slot)
    {
        slot.recoilPos = Vector3.zero;
        slot.recoilPosVelocity = Vector3.zero;
        slot.recoilRot = Vector3.zero;
        slot.recoilRotVelocity = Vector3.zero;
    }

    // Kicks the recoil spring of the slot placing this item. Velocities are in the muzzle's local
    // frame, so back is -z and muzzle rise is negative x (m/s and deg/s).
    // Returns false when this rig isn't placing the item.
    public bool Kick(Item item, Vector3 linearKick, Vector3 angularKick)
    {
        if (item == null) return false;
        HandSlot slot = null;
        if (right.item == item && right.places) slot = right;
        else if (left.item == item && left.places) slot = left;
        if (slot == null) return false;
        slot.recoilPosVelocity += linearKick;
        slot.recoilRotVelocity += angularKick;
        return true;
    }

    void StepRecoil(HandSlot slot, float dt)
    {
        if (dt <= 0f) return;
        float omega = 2f * Mathf.PI * Mathf.Max(recoilFrequency, 0.01f);
        float k = omega * omega;
        float c = 2f * recoilDampingRatio * omega;
        // Substeps keep a stiff spring stable at low framerates.
        int n = Mathf.Max(1, Mathf.CeilToInt(dt * 120f));
        float h = dt / n;
        for (int i = 0; i < n; i++)
        {
            slot.recoilPosVelocity += (-k * slot.recoilPos - c * slot.recoilPosVelocity) * h;
            slot.recoilPos += slot.recoilPosVelocity * h;
            slot.recoilRotVelocity += (-k * slot.recoilRot - c * slot.recoilRotVelocity) * h;
            slot.recoilRot += slot.recoilRotVelocity * h;
        }
        slot.recoilPos = Vector3.ClampMagnitude(slot.recoilPos, maxRecoilDistance);
        slot.recoilRot = Vector3.ClampMagnitude(slot.recoilRot, maxRecoilAngle);
    }

    void ApplyRecoil(HandSlot slot, Vector3 pivot, Quaternion muzzleRotLocal, ref Vector3 pos, ref Quaternion rot)
    {
        if (slot.recoilPos.sqrMagnitude < 1e-10f && slot.recoilRot.sqrMagnitude < 1e-10f) return;
        // Rotation offset expressed in muzzle space, applied about the socket so the muzzle rises around the hand.
        Quaternion muzzleRot = rot * muzzleRotLocal;
        Quaternion d = muzzleRot * Quaternion.Euler(slot.recoilRot) * Quaternion.Inverse(muzzleRot);
        rot = d * rot;
        pos = pivot + d * (pos - pivot);
        // Kickback runs along the rotated barrel; the grip leaves the socket, so the arm IK visibly absorbs it.
        pos += (rot * muzzleRotLocal) * slot.recoilPos;
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
        Transform grip = item.GripFor(side);
        if (grip == null)
        {
            pos = socket.position;
            rot = socket.rotation;
            return true;
        }
        // The grip's local offset is inverted onto the socket so the grip lands on the hand;
        // valid because grips are direct children of the item at unit scale.
        rot = socket.rotation * Quaternion.Inverse(grip.localRotation);
        pos = socket.position - rot * grip.localPosition;
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
        ApplyAim(pivot, basePos, baseRot, muzzleOffset, muzzleRotLocal, ref pos, ref rot);
        // Recoil goes on top of the aimed pose, so the item springs back onto the aim point.
        StepRecoil(slot, Time.deltaTime);
        ApplyRecoil(slot, pivot, muzzleRotLocal, ref pos, ref rot);

        // Written directly: MovePosition/MoveRotation would only apply at the next physics step.
        itemT.SetPositionAndRotation(pos, rot);
    }

    void ApplyAim(Vector3 pivot, Vector3 basePos, Quaternion baseRot, Vector3 muzzleOffset, Quaternion muzzleRotLocal, ref Vector3 pos, ref Quaternion rot)
    {
        pos = basePos;
        rot = baseRot;
        if (aimPoint == null || aimWeight <= 0.0001f) return;

        Vector3 target = aimPoint.position;
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

        total = Quaternion.RotateTowards(Quaternion.identity, total, maxAimCorrectionAngle);
        // Weighting the accumulated rotation, not each iteration, keeps the weight linear in angle.
        total = Quaternion.Slerp(Quaternion.identity, total, aimWeight);
        rot = total * baseRot;
        pos = pivot + total * (basePos - pivot);
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

        // Both slots are posed before any IK write, so a two-handed item placed by either hand
        // is final before its off-hand IK target is written.
        PoseSlot(HandSide.Right);
        PoseSlot(HandSide.Left);
        WriteIK(HandSide.Right);
        WriteIK(HandSide.Left);
    }

    void WriteIK(HandSide side)
    {
        HandSlot slot = GetSlot(side);
        Transform grip = (slot.item != null && slot.item.UsesHands) ? slot.item.GripFor(side) : null;
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
