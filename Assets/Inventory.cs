using System;
using System.Collections.Generic;
using UnityEngine;

public class Inventory : MonoBehaviour
{
    public int total_slots;
    public List<(Item, int)> items = new List<(Item, int)>();
    public List<int> empty_slots = new List<int>();           // indices
    public List<(Item, int, int)> unfull_slots = new List<(Item, int, int)>();     // item, index, amt
    public List<(Item, int)> stackable_items = new List<(Item, int)>();               // list of items available to stack, and their indices
    public int max_stack_scale;
    public bool free;

    void Start()
    {
        for (int i = 0; i < total_slots; i++)
        {
            items.Add((new Item(0, Item.item_type.nothing), 64));
            empty_slots.Add(i);
        }
    }

    public bool HasSpace()
    {
        free = true;
        if (empty_slots.Count > 0)
        {
            return true;
        }
        if (unfull_slots.Count > 0)
        {
            return true;
        }
        free = false;
        return false;
    }

    public int HasSpaceForItem(Item item)
    {
        foreach ((Item, int) i in stackable_items)
        {
            if (i == (item, i.Item2))
            {
                return i.Item2;
            }
        }
        if (empty_slots.Count > 0)
        {
            return empty_slots[0];
        }
        return 0;
    }

    public bool SlotFull(int slot)
    {
        if (items[slot].Item1.type == Item.item_type.tool)
        {
            return true;
        }
        if (SpaceLeft(slot) > 0)
        {
            return true;
        }
        return false;
    }

    public int SlotExists(Item item)
    {
        foreach ((Item, int) t in items) {
            if (item == t.Item1)
            {
                if (!SlotFull(t.Item2))
                {
                    return t.Item2;
                }
            }
        }
        return 0;
    }

    public int SpaceLeft(int index)
    {
        Item item = items[index].Item1;
        int currentAmount = items[index].Item2;

        if (item.type == Item.item_type.tool)
        {
            // Tools can't be stacked, only one per slot
            return currentAmount == 0 ? 1 : 0;
        }
        if (item.type == Item.item_type.nothing)
        {
            // Empty slot, can accept max stack
            return (int)Math.Pow(max_stack_scale, item.stack_scale);
        }
        // For stackable items
        int maxStack = (int)Math.Pow(max_stack_scale, item.stack_scale);
        return maxStack - currentAmount;
    }

    int AddToStack(Item item, int inx, int amt)
    {
        int spaceLeft = SpaceLeft(inx);
        int itemsToAdd = Math.Min(amt, spaceLeft);

        if (itemsToAdd > 0)
        {
            // Get current amount and add to it
            int currentAmount = items[inx].Item2;
            items[inx] = (item, currentAmount + itemsToAdd);
        }
        
        return itemsToAdd;
    }

    public int AddItem(Item item, int amt)
    {
        int amountToAdd = amt;
        int amountAdded = 0;

        // Keep trying to add items as long as we have more to add and there's space
        while (amountToAdd > 0 && HasSpace())
        {
            // Find a slot that has the same item and is not full, or find an empty slot
            int slotIndex = HasSpaceForItem(item);

            // If HasSpaceForItem returns 0 and it's not a valid index, find an empty slot
            if (slotIndex == 0 && items[0].Item1.type != Item.item_type.nothing && items[0].Item1.item_code != item.item_code)
            {
                 if(empty_slots.Count > 0)
                 {
                    slotIndex = empty_slots[0];
                 }
                 else
                 {
                    // No space left for this item type
                    break;
                 }
            }

            int addedNow = AddToStack(item, slotIndex, amountToAdd);

            if (addedNow > 0)
            {
                amountAdded += addedNow;
                amountToAdd -= addedNow;
            }
            else
            {
                // Can't add any more to any slot
                break;
            }
        }
        return amountAdded;
    }
}
