using UnityEngine;
using System.Collections.Generic;

public class InteractionTrigger : MonoBehaviour
{
    public Object interactObject;
    public List<Object> objectsInRange = new List<Object>();

    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponentInParent<Object>() is Object obj)
        {
            if (obj != interactObject && !objectsInRange.Contains(obj))
            {
                objectsInRange.Add(obj);
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.GetComponentInParent<Object>() is Object obj)
        {
            if (obj != interactObject && objectsInRange.Contains(obj))
            {
                objectsInRange.Remove(obj);
            }
        }
    }

    public bool InTrigger(Object obj)
    {
        return objectsInRange.Contains(obj);
    }

    // private void OnTriggerEnter(Collider other)
    // {
    //     if (other.GetComponent<Mob>() is Mob m)
    //     {
    //         // if (m.)
    //     }
    // }

    // private void OnTriggerExit(Collider other)
    // {
    //     if (other.GetComponent<Mob>() is Mob m)
    //     {
    //         if (interactObject != null)
    //         {
    //             interactObject.InteractOutline(false);
    //         }
    //     }
    // }
}