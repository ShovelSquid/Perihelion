using UnityEngine;

public class Fruit : Item
{
    public int heal_amount;
    public override void OnPickup(Mob mob)
    {
        mob.Heal(heal_amount);
        // if (spawner != null)
        // {
        //     spawner.ItemPickedUp();
        // }
        GotPickedUp();
    }
}
