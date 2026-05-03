using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Events;

public class Item : MonoBehaviour
{
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
    // end bunch of bullshit

    public UnityEvent onPickup;

    // void Start() {
    //     if (spawner != null) {
    //         onPickup.addEventListener(spawner.ItemPickedUp);
    //     }
    // }


    void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.layer == LayerMask.NameToLayer("Mobs"))
        {
            // onPickup.Invoke(other.GetComponent<Mob>());
            OnPickup(other.GetComponent<Mob>());
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
