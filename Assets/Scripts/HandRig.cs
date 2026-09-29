using UnityEngine;
using System;
using UnityEngine.Animations.Rigging;

// Right must stay first (value 0) so existing serialized Item data defaults to right-handed.
public enum HandSide
{
    Right,
    Left
}

// Owns both hands: which item each hand holds, where that item rests (from the hand's
// animated socket), and the TwoBoneIK targets. This is the only script that writes IK targets.
public class HandRig : MonoBehaviour
{
    [System.Serializable]
    public class HandSlot
    {
        public Transform socket; // pure animated, pre-IK hand pose; only read here, whatever writes it (rig constraint today, ghost/pose armature later)
        public Transform ikTarget; // this hand's TwoBoneIK target; only HandRig writes it
        public Rig ikRig; // weight 1 while this hand grips, 0 otherwise
        public AimItem driver; // physics driver used when this hand places an item
        public Item item; // equipped item, or null
        [System.NonSerialized] public bool places; // true when this slot's socket positions the item
    }

    [Header("Hands")]
    public HandSlot right = new HandSlot();
    public HandSlot left = new HandSlot();
    [Header("Aim")]
    public bool aiming; // while true, placed items are driven to their own aimTarget

    public bool IsEmpty
    {
        get { return right.item == null && left.item == null; }
    }

    void Awake()
    {
        if (right.driver == null) right.driver = GetComponent<AimItem>();
        if (right.driver == null) right.driver = gameObject.AddComponent<AimItem>();
        if (left.driver == null || left.driver == right.driver)
        {
            left.driver = gameObject.AddComponent<AimItem>();
            left.driver.CopyTuning(right.driver);
        }
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

        // Activate first so Item.Awake has run and item.rb is filled.
        item.gameObject.SetActive(true);
        item.equipped = true;
        Rigidbody body = item.rb != null ? item.rb : item.GetComponent<Rigidbody>();

        if (TryGetHoldPose(side, item, out Vector3 pos, out Quaternion rot))
        {
            if (body != null)
            {
                body.position = pos;
                body.rotation = rot;
                if (!body.isKinematic)
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }
            }
            item.transform.SetPositionAndRotation(pos, rot);
        }
        else
        {
            Debug.LogWarning($"{name}: HandRig {side} slot has no socket, so {item.name} can't be placed in the hand.", this);
        }

        WarnIfGripNotChild(item, item.GripFor(side));
        if (twoHanded) WarnIfGripNotChild(item, item.GripFor(otherSide));

        if (slot.driver != null) slot.driver.Attach(body);
        return true;
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
        if (slot.places && slot.driver != null) slot.driver.Detach();
        slot.item = null;
        slot.places = false;
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

    void FixedUpdate()
    {
        // Driving happens here (not in AimItem) so the target is computed and applied in the
        // same physics step, with no script-order dependency.
        DriveSlot(HandSide.Right);
        DriveSlot(HandSide.Left);
    }

    void DriveSlot(HandSide side)
    {
        HandSlot slot = GetSlot(side);
        if (slot.item == null || !slot.places || slot.driver == null) return;

        Vector3 pos;
        Quaternion rot;
        if (aiming && slot.item.aimTarget != null)
        {
            pos = slot.item.aimTarget.position;
            rot = slot.item.aimTarget.rotation;
        }
        else if (!TryGetHoldPose(side, slot.item, out pos, out rot))
        {
            return;
        }
        slot.driver.Drive(pos, rot);
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

    // IK is written in Update because Rigidbody interpolation has already placed the item
    // for this frame, and Animation Rigging evaluates after Update.
    void Update()
    {
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
