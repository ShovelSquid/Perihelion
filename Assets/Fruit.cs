using UnityEngine;

public class Fruit : Item
{
    public int heal_amount;

    public Fruit(int heal_amount, int stack_scale, item_type type) : base(stack_scale, type)
    {
        this.heal_amount = heal_amount;
    }

    public override void OnPickup(Mob mob)
    {
        mob.Heal(heal_amount);
        if (spawner != null)
        {
            spawner.ItemPickedUp();
        }
        Destroy(gameObject);
    }
}
