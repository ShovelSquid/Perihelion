using UnityEngine;

public class Drop : MonoBehaviour
{
    public Item item;
    public bool isGold;
    public Vector2 amount;
    public ParticleSystem pickupEffect;

    void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.layer == LayerMask.NameToLayer("Mobs"))
        {
            // onPickup.Invoke(other.GetComponent<Mob>());
            Mob m = other.GetComponent<Mob>();
            if (m != null)
            {
                Debug.Log("can pick up GOOOLLLDDD");
                if (isGold)
                {
                    int amt = Random.Range((int)amount.x, (int)amount.y);
                    m.AddGold(amt);
                    Debug.Log("picked up " + amt + " geeeeeeeooooooooold");
                }
                else if (item != null)
                {
                    m.PickupItem(item);
                }
                if (pickupEffect != null) Instantiate(pickupEffect, transform.position, Quaternion.identity);
                Destroy(gameObject);
            }
        }
    }

}