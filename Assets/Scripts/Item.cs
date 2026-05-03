using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Events;

public class Item : MonoBehaviour
{
    public bool inInventory;
    public Inventory inv;
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
    public int stack_scale;
    public enum item_type { nothing, consumable, tool, weapon, armor, material, key_item }
    public item_type type;
    public int item_code;
    public SpawnItem spawner;
    public Item(int stack_scale, item_type type)
    {
        this.stack_scale = stack_scale;
        this.type = type;
    }

    [Header("Effects")]
    public AudioSource triggerSound;
    public AudioSource activateSound;

    // end bunch of bullshit

    public UnityEvent onPickup;

    // void Start() {
    //     if (spawner != null) {
    //         onPickup.addEventListener(spawner.ItemPickedUp);
    //     }
    // }

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
            OnPickup(other.GetComponent<Mob>());
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
        if (isTool && inInventory && holdable && Vector3.Distance(transform.position, aimPoint.position) < 10f)
        {
            transform.position = Vector3.Lerp(transform.position, aimPoint.position - transform.forward * 10f, holdLerpSpeed * Time.deltaTime);
        }
    }


    public virtual void OnPickup(Mob mob)
    {
        // code for item, handled by subclass
        onPickup.Invoke();
        if (spawner != null)
        {
            spawner.ItemPickedUp();
        }
        Destroy(gameObject);
    }
}
