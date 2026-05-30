using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Shine))]
public class Item : MonoBehaviour
{
    private Shine shine;
    public bool inInventory;
    public Animator anim;
    [Header("Hold Info")]
    public bool holdable;
    public bool held;
    public bool isTool;
    public bool triggerHeld;
    // public Transform holdPoint;
    public Transform holdTarget;
    public Transform aimPoint;
    public Transform aimTarget;
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

    void Awake()
    {
        shine = GetComponent<Shine>();
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

    public virtual void Update()
    {
        
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.layer == LayerMask.NameToLayer("Mobs"))
        {
            // onPickup.Invoke(other.GetComponent<Mob>());
            Mob m = other.GetComponent<Mob>();
            if (m != null)
            {
                Debug.Log("can pick up");
                OnPickup(m);
            }
        }
    }

    public virtual void LateUpdate()
    {
        if (holdable && inInventory)
        {
            // holdPoint.position = Vector3.Lerp(holdPoint.position, holdTarget.position, holdLerpSpeed * Time.deltaTime);
            // holdPoint.rotation = Quaternion.Slerp(holdPoint.rotation, holdTarget.rotation, holdLerpSpeed * Time.deltaTime);
        }
        if (isTool && inInventory)
        {
            aimPoint.position = Vector3.Lerp(aimPoint.position, aimTarget.position, aimLerpSpeed * Time.deltaTime);
            aimPoint.rotation = Quaternion.Slerp(aimPoint.rotation, aimTarget.rotation, aimLerpSpeed * Time.deltaTime);
        }
        if (holdable && inInventory)
        {
            transform.position = Vector3.Lerp(transform.position, holdTarget.position, holdLerpSpeed * Time.deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, holdTarget.rotation, holdLerpSpeed * Time.deltaTime);
            if (isTool)
            {
                transform.rotation = Quaternion.LookRotation(aimPoint.position - transform.position, Vector3.up);
            }
        }
        // if (isTool && inInventory && holdable && Vector3.Distance(transform.position, aimPoint.position) < 10f)
        // {
        //     transform.position = Vector3.Lerp(transform.position, aimPoint.position - transform.forward * 10f, holdLerpSpeed * Time.deltaTime);
        // }
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
