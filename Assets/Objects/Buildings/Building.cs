using UnityEngine;

public class Building : Object
{
    public Spawner spawner;

    public override void Interact()
    {
        if (spawner != null)
        {
            spawner.Spawn(1);
        }
    }

    // protected override void Die(float extraDamage = 0f)
    // {
    //     base.Die(extraDamage);
    //     if (destroyedVersion != null)
    //     {
    //         GameObject e = Instantiate(destroyedVersion, transform.position, transform.rotation);
    //         e.transform.localScale = transform.localScale;
    //     }
    //     gameObject.SetActive(false); // don't destroy the building object, since we want to keep its collider and other components for the rubble. Just hide it.
    //     // Destroy(gameObject);
    // }
}
