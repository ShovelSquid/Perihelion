using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Shine))]
public class Item : MonoBehaviour
{
    public Rigidbody rb;
    protected Shine shine;
    [System.Serializable]
    public struct EquipInfo
    {
        public Sprite hotwheelIcon;
        public Sprite bigUIIcon;
        public string label;
        public string equipAnimation;
        public bool rightHand;
        public bool leftHand;
        public HandSide primaryHand; // dominant hand, places a two-handed or hand-less item; one-handed items use their flagged hand
    }
    // Grips are where a hand bone goes (any transform under the item, bones included).
    // hand1 is held by the primaryHand (or whichever hand holds a one-handed item); hand2 is the off-hand grip.
    [UnityEngine.Serialization.FormerlySerializedAs("handR")] public Transform hand1;
    [UnityEngine.Serialization.FormerlySerializedAs("handL")] public Transform hand2;
    public EquipInfo equipInfo;
    public bool pickupable;
    // public bool inInventory;
    public Animator anim;
    [Header("Hold Info")]
    protected HitIndicator hitIndicator;
    public Mob holder;
    // public bool holdable;
    public bool equipped;
    // public bool aim = false;
    // public bool isTool;
    public bool triggerHeld;
    // protected Transform holdTargetBase;
    // protected Transform aimPoint;
    // public float holdLerpSpeed;
    // public float aimLerpSpeed;

    [Header("Item Info")]
    // begin bunch of bullshit
    public int stack;
    public int maxStack;
    // public int stackScale = 1;  // multiples of 4

    [Header("Effects")]
    public AudioSource triggerSound;
    public AudioSource activateSound;

    public ParticleSystem pickupFX;

    // end bunch of bullshit

    public UnityEvent onPickup;

    // void Start() {
    //     if (spawner != null) {
    //         onPickup.addEventListener(spawner.ItemPickedUp);
    //     }
    // }

    protected virtual void Awake()
    {
        shine = GetComponent<Shine>();
        rb = GetComponent<Rigidbody>();
        if (anim == null) anim = GetComponent<Animator>();
        if (holder != null)
        {
            // aimTarget = holder.itemAimTarget;
            // aimPoint = holder.itemAimPoint;
            // holdTargetBase = new GameObject().transform;
            // holdTargetBase.parent = holdTarget.parent;
            // holdTargetBase.position = holdTarget.position;
            // holdTargetBase.rotation = holdTarget.rotation;
        }
        if (hitIndicator == null && holder is Player && ((Player)holder).hitIndicator != null)
        {
            hitIndicator = ((Player)holder).hitIndicator;
        }
    }

    public bool UsesHands
    {
        get { return equipInfo.rightHand || equipInfo.leftHand; }
    }

    public bool IsTwoHanded
    {
        get { return equipInfo.rightHand && equipInfo.leftHand; }
    }

    public HandSide DefaultHand
    {
        get
        {
            if (equipInfo.leftHand && !equipInfo.rightHand) return HandSide.Left;
            if (equipInfo.rightHand && !equipInfo.leftHand) return HandSide.Right;
            return equipInfo.primaryHand;
        }
    }

    // Grips follow role, not side: a one-handed item always uses hand1 in whichever hand holds it;
    // a two-handed item gives hand1 to its primaryHand and hand2 to the other. Falls back to the other grip if one is empty.
    public Transform GripFor(HandSide side)
    {
        bool primary = !IsTwoHanded || side == equipInfo.primaryHand;
        if (primary) return hand1 != null ? hand1 : hand2;
        return hand2 != null ? hand2 : hand1;
    }

    // The point and forward the item aims with. HandRig turns the item so this forward hits its
    // aim point; guns override it with their fire point.
    public virtual Transform Muzzle
    {
        get { return transform; }
    }

    public virtual bool CanTrigger()
    {
        return true;
    }

    public virtual void SlapTrigger(bool isPressed)
    {
        triggerHeld = isPressed;
        if (!isPressed)
        {
            return;
        }
        if (triggerSound != null) triggerSound.Play();
        if (CanTrigger())
        {
            DoTrigger();
        }
    }

    public virtual void DoTrigger()
    {
        if (activateSound != null) activateSound.Play();
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.layer == LayerMask.NameToLayer("Mobs"))
        {
            // onPickup.Invoke(other.GetComponent<Mob>());
            Mob m = other.GetComponent<Mob>();
            if (m != null && pickupable)
            {
                Debug.Log("can pick up");
                OnPickup(m);
            }
        }
    }

    // public virtual void Aim(bool aim)
    // {
    //     this.aim = aim;
    //     if (!aim)
    //     {
    //         // holdTarget.rotation = holdTargetBase.rotation;
    //         // holdTarget.position = holdTargetBase.position;
    //         // holdTarget.rotation = Quaternion.identity;
    //     }
    // }

    // public virtual void LateUpdate()
    // {
    //     if (equipped)
    //     {
    //         if (aim)
    //         {
    //             aimPoint.position = Vector3.Lerp(aimPoint.position, aimTarget.position, aimLerpSpeed * Time.deltaTime);
    //             aimPoint.rotation = Quaternion.Slerp(aimPoint.rotation, aimTarget.rotation, aimLerpSpeed * Time.deltaTime);
    //             holdTransform.rotation = Quaternion.LookRotation(aimPoint.position - holdTransform.position, Vector3.up);
    //         }
    //         else
    //         {
    //             holdTransform.rotation = Quaternion.Slerp(holdTransform.rotation, holdTarget.rotation, holdLerpSpeed * Time.deltaTime);
    //         }
    //         holdTransform.position = Vector3.Lerp(holdTransform.position, holdTarget.position, holdLerpSpeed * Time.deltaTime);
    //             // : Quaternion.Slerp(transform.rotation, holdTarget.rotation, holdLerpSpeed * Time.deltaTime);
    //     }
    // }

    public void GotPickedUp()
    {
        //do pickup effects here, like particles or sound
        Debug.Log(gameObject.name + " got picked up");
        if (shine != null) shine.Shiney();
        if (pickupFX != null)
        {
            Instantiate(pickupFX, transform.position, Quaternion.identity);
        }
        // disable all colliders and keep it kinematic so it doesn't fall through the floor or get in the way of the player
        Collider[] colliders = GetComponentsInChildren<Collider>();
        foreach (Collider col in colliders)
        {
            col.enabled = false;
        }
        Invoke("End", 0.1f);
        // gameObject.SetActive(false);
    }

    public void End()
    {
        Destroy(gameObject);
    }

    public virtual void Update()
    {
        
    }

    // public virtual void Equip(bool equip)
    // {
    //     if (!equip) 
    //     { 
    //         gameObject.SetActive(false); 
    //         equipped = false;
    //         if (hitIndicator != null) hitIndicator.gameObject.SetActive(false);
    //         return;    
    //     }
    //     else gameObject.SetActive(true);
    //     holder.item = equip ? this : null;
    //     holder.EnableIK(equipInfo.rightHand, equipInfo.leftHand);
    //     holder.SetIK();
    //     holder.item = equip ? this : null;
    //     if (hitIndicator != null)
    //     {
    //         hitIndicator.gameObject.SetActive(true);
    //         hitIndicator.SetAmmo(0, 0);
    //     }
    //     equipped = equip;
    //     if (anim != null && equipInfo.equipAnimation != "")
    //     {
    //         anim.Play(equipInfo.equipAnimation, -1, 0f);
    //     }
    //     if (holder.anim != null && equipInfo.equipAnimation != "")
    //     {
    //         holder.anim.Play(equipInfo.equipAnimation, -1, 0f);
    //     }
    // }


    public virtual void OnPickup(Mob mob)
    {
        // code for item, handled by subclass
        onPickup.Invoke();
        mob.PickupItem(this);
        // if (spawner != null)
        // {
        //     spawner.ItemPickedUp();
        // }
        // Destroy(gameObject);
    }
}
