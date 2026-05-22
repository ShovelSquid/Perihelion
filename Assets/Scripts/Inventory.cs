using System;
using System.Collections.Generic;
using UnityEngine;

public class Inventory : MonoBehaviour
{
    public bool still = false; // If true, this inventory can't add items
    public int maxSlots = 8;
    // max stack = item's stack scale * max stack scale * 4
    public int maxStackScale = 3;
    public List<GameObject> items = new List<GameObject>();

    [Header("Drop Settings")]
    public Mesh dropMesh;
    public bool dropOnSurface = true;
    public float dropForce;
    public float dropRadius = 1.5f;
    public float minSpacing = 0.3f;
    public int maxAttemptsPerItem = 30;

    public void Drop()
    {
        Vector3 center = transform.position + transform.forward;
        var placed = new List<Vector3>();

        foreach (GameObject item in items)
        {
            if (item == null) continue;

            Vector3 offset = FindDropOffset(placed);
            placed.Add(offset);

            Quaternion rot = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f);
            GameObject spawned = Instantiate(item, center + offset, rot);

            Rigidbody rb = spawned.GetComponent<Rigidbody>();
            if (rb != null && dropForce > 0f)
            {
                Vector3 outward = offset.sqrMagnitude > 0.0001f
                    ? offset.normalized
                    : UnityEngine.Random.onUnitSphere;
                rb.AddForce(outward * dropForce, ForceMode.Impulse);
            }

            Debug.Log($"Dropped {item.name}");
        }
    }

    private Vector3 FindDropOffset(List<Vector3> existing)
    {
        float minSqr = minSpacing * minSpacing;
        for (int attempt = 0; attempt < maxAttemptsPerItem; attempt++)
        {
            Vector3 candidate = SampleCandidate();
            bool tooClose = false;
            for (int i = 0; i < existing.Count; i++)
            {
                if ((candidate - existing[i]).sqrMagnitude < minSqr)
                {
                    tooClose = true;
                    break;
                }
            }
            if (!tooClose) return candidate;
        }
        return SampleCandidate();
    }

    private Vector3 SampleCandidate()
    {
        if (dropMesh == null) return UnityEngine.Random.insideUnitSphere * dropRadius;
        return dropOnSurface ? RandomPointOnMesh(dropMesh) : RandomPointInMesh(dropMesh);
    }

    private Vector3 RandomPointOnMesh(Mesh mesh)
    {
        Vector3[] verts = mesh.vertices;
        int[] tris = mesh.triangles;
        int triCount = tris.Length / 3;
        if (triCount == 0) return Vector3.zero;

        float total = 0f;
        var cumulative = new float[triCount];
        for (int i = 0; i < triCount; i++)
        {
            Vector3 a = verts[tris[i * 3]];
            Vector3 b = verts[tris[i * 3 + 1]];
            Vector3 c = verts[tris[i * 3 + 2]];
            total += Vector3.Cross(b - a, c - a).magnitude * 0.5f;
            cumulative[i] = total;
        }

        float r = UnityEngine.Random.value * total;
        int chosen = 0;
        for (; chosen < triCount - 1; chosen++)
            if (cumulative[chosen] >= r) break;

        Vector3 va = verts[tris[chosen * 3]];
        Vector3 vb = verts[tris[chosen * 3 + 1]];
        Vector3 vc = verts[tris[chosen * 3 + 2]];
        float u = UnityEngine.Random.value;
        float v = UnityEngine.Random.value;
        if (u + v > 1f) { u = 1f - u; v = 1f - v; }
        return va + u * (vb - va) + v * (vc - va);
    }

    private Vector3 RandomPointInMesh(Mesh mesh)
    {
        Bounds b = mesh.bounds;
        return new Vector3(
            UnityEngine.Random.Range(b.min.x, b.max.x),
            UnityEngine.Random.Range(b.min.y, b.max.y),
            UnityEngine.Random.Range(b.min.z, b.max.z)
        );
    }

    public Item AddItem(Item item)
    {
        if (still) return item;
        if (item == null) return null;

        int maxStack = item.stackScale * maxStackScale * 4;
        int remaining = item.stack;

        // Top up existing stacks of the same kind.
        foreach (GameObject slot in items)
        {
            if (slot == null) continue;
            if (remaining <= 0) break;
            Item existing = slot.GetComponent<Item>();
            if (existing == null) continue;
            if (!SameItemType(existing, item)) continue;

            int space = maxStack - existing.stack;
            if (space <= 0) continue;

            int toAdd = Mathf.Min(space, remaining);
            existing.stack += toAdd;
            remaining -= toAdd;
        }

        // Anything left over: claim a new slot if there's room.
        if (remaining > 0 && items.Count < maxSlots)
        {
            item.stack = remaining;
            items.Add(item.gameObject);
            Debug.Log($"Added {item.name} to inventory (stack={remaining}).");
            return null;
        }

        if (remaining <= 0)
        {
            Debug.Log($"Merged {item.name} into existing stacks.");
            return null;
        }

        // Inventory full and existing stacks couldn't hold it all.
        item.stack = remaining;
        Debug.Log($"Inventory full. {remaining} of {item.name} left over.");
        return item;
    }

    private bool SameItemType(Item a, Item b)
    {
        // No stable item id on Item.cs yet — match by base name (strip "(Clone)").
        string nameA = a.gameObject.name.Replace("(Clone)", "").Trim();
        string nameB = b.gameObject.name.Replace("(Clone)", "").Trim();
        return nameA == nameB;
    }

//     public bool HasSpace()
//     {
//         free = true;
//         if (empty_slots.Count > 0)
//         {
//             return true;
//         }
//         if (unfull_slots.Count > 0)
//         {
//             return true;
//         }
//         free = false;
//         return false;
//     }

//     public int HasSpaceForItem(Item item)
//     {
//         foreach ((Item, int) i in stackable_items)
//         {
//             if (i == (item, i.Item2))
//             {
//                 return i.Item2;
//             }
//         }
//         if (empty_slots.Count > 0)
//         {
//             return empty_slots[0];
//         }
//         return 0;
//     }

//     public bool SlotFull(int slot)
//     {
//         if (items[slot].Item1.type == Item.item_type.tool)
//         {
//             return true;
//         }
//         if (SpaceLeft(slot) > 0)
//         {
//             return true;
//         }
//         return false;
//     }

//     public int SlotExists(Item item)
//     {
//         foreach ((Item, int) t in items) {
//             if (item == t.Item1)
//             {
//                 if (!SlotFull(t.Item2))
//                 {
//                     return t.Item2;
//                 }
//             }
//         }
//         return 0;
//     }

//     public int SpaceLeft(int index)
//     {
//         Item item = items[index].Item1;
//         int currentAmount = items[index].Item2;

//         if (item.type == Item.item_type.tool)
//         {
//             // Tools can't be stacked, only one per slot
//             return currentAmount == 0 ? 1 : 0;
//         }
//         if (item.type == Item.item_type.nothing)
//         {
//             // Empty slot, can accept max stack
//             return (int)Math.Pow(max_stack_scale, item.stack_scale);
//         }
//         // For stackable items
//         int maxStack = (int)Math.Pow(max_stack_scale, item.stack_scale);
//         return maxStack - currentAmount;
//     }

//     int AddToStack(Item item, int inx, int amt)
//     {
//         int spaceLeft = SpaceLeft(inx);
//         int itemsToAdd = Math.Min(amt, spaceLeft);

//         if (itemsToAdd > 0)
//         {
//             // Get current amount and add to it
//             int currentAmount = items[inx].Item2;
//             items[inx] = (item, currentAmount + itemsToAdd);
//         }
        
//         return itemsToAdd;
//     }

//     public int AddItem(Item item, int amt)
//     {
//         int amountToAdd = amt;
//         int amountAdded = 0;

//         // Keep trying to add items as long as we have more to add and there's space
//         while (amountToAdd > 0 && HasSpace())
//         {
//             // Find a slot that has the same item and is not full, or find an empty slot
//             int slotIndex = HasSpaceForItem(item);

//             // If HasSpaceForItem returns 0 and it's not a valid index, find an empty slot
//             if (slotIndex == 0 && items[0].Item1.type != Item.item_type.nothing && items[0].Item1.item_code != item.item_code)
//             {
//                  if(empty_slots.Count > 0)
//                  {
//                     slotIndex = empty_slots[0];
//                  }
//                  else
//                  {
//                     // No space left for this item type
//                     break;
//                  }
//             }

//             int addedNow = AddToStack(item, slotIndex, amountToAdd);

//             if (addedNow > 0)
//             {
//                 amountAdded += addedNow;
//                 amountToAdd -= addedNow;
//             }
//             else
//             {
//                 // Can't add any more to any slot
//                 break;
//             }
//         }
//         return amountAdded;
//     }
}
