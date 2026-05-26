using UnityEngine;
using System.Collections.Generic;

public class Player : Mob
{

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
}