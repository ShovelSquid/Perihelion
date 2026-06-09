using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Shine))]
public class Item : MonoBehaviour
{
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
    }
    public Transform handL;
    public Transform handR;
    public EquipInfo equipInfo;
    public bool pickupable;
    // public bool inInventory;
    public Animator anim;
    [Header("Hold Info")]
    protected HitIndicator hitIndicator;
    public Mob holder;
    // public bool holdable;
    public bool equipped;
    // public bool isTool;
    public bool triggerHeld;
    public Transform holdTransform;
    protected Transform holdTarget;
    protected Transform aimPoint;
    protected Transform aimTarget;
    public float holdLerpSpeed;
    public float aimLerpSpeed;

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
        if (holder != null)
        {
            holdTarget = holder.itemHoldTarget;
            aimTarget = holder.itemAimTarget;
            aimPoint = holder.itemAimPoint;
        }
        if (holdTransform == null) holdTransform = transform;
        if (hitIndicator == null && holder is Player && ((Player)holder).hitIndicator != null)
        {
            hitIndicator = ((Player)holder).hitIndicator;
        }
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

    public virtual void LateUpdate()
    {
        if (equipped)
        {
            aimPoint.position = Vector3.Lerp(aimPoint.position, aimTarget.position, aimLerpSpeed * Time.deltaTime);
            aimPoint.rotation = Quaternion.Slerp(aimPoint.rotation, aimTarget.rotation, aimLerpSpeed * Time.deltaTime);
            holdTransform.position = Vector3.Lerp(holdTransform.position, holdTarget.position, holdLerpSpeed * Time.deltaTime);

            holdTransform.rotation = Quaternion.LookRotation(aimPoint.position - holdTransform.position, Vector3.up);
                // : Quaternion.Slerp(transform.rotation, holdTarget.rotation, holdLerpSpeed * Time.deltaTime);
        }
    }

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

    public virtual void Equip(bool equip)
    {
        if (!equip) 
        { 
            gameObject.SetActive(false); 
            equipped = false;
            if (hitIndicator != null) hitIndicator.gameObject.SetActive(false);
            return;    
        }
        else gameObject.SetActive(true);
        holder.EnableIK(equipInfo.rightHand, equipInfo.leftHand);
        holder.SetIK(equipInfo.rightHand ? handR : null, equipInfo.leftHand ? handL : null);
        holder.heldItem = equip ? this : null;
        if (hitIndicator != null)
        {
            hitIndicator.gameObject.SetActive(true);
            hitIndicator.SetAmmo(0, 0);
        }
        equipped = equip;
        if (anim != null && equipInfo.equipAnimation != "")
        {
            anim.Play(equipInfo.equipAnimation, -1, 0f);
        }
        if (holder.anim != null && equipInfo.equipAnimation != "")
        {
            holder.anim.Play(equipInfo.equipAnimation, -1, 0f);
        }
    }


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
