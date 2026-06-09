using UnityEngine;
using System.Collections.Generic;

public class Player : Mob
{
    public Healthbar goldBar;
    public void LookAt(Object obj)
    {
        if (interactObject != null && interactObject == obj) return;
        if (interactObject != null && interactObject != obj)
        {
            interactObject.InteractOutline(false);
        }
        interactObject = obj;
        if (interactObject != null && interactObject.interactible && interactObject.interactionTrigger != null && interactObject.interactionTrigger.InTrigger(this))
        {
            interactObject.InteractOutline(true);
        }
    }

    public override void AddGold(int amount)
    {
        base.AddGold(amount);
        // gold = Mathf.Clamp(gold, 0, maxGold);
        if (goldBar != null)
        {
            goldBar.SetHealth(gold);
        }
    }

    protected override void Start()
    {
        base.Start();
        if (goldBar != null)
        {
            goldBar.SetMaxHealth(maxGold);
            goldBar.SetHealth(gold);
        }
    }

}