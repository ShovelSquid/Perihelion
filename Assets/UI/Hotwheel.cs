using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class Hotwheel : MonoBehaviour
{
    public Player player;
    public float horizontalpadding;
    public int totalSlots;
    public int equippedSlot;
    public HotwheelSlot slotPrefab;
    public Image bigItemIcon;
    public TextMeshProUGUI bigItemLabel;

    [System.Serializable]
    public struct Slot
    {
        public HotwheelSlot slotObject;
        public Sprite icon;
        public string label;
        public Sprite bigIcon;
        // public Color color;
        public Item actionItem;
    }
    public List<Slot> slots = new List<Slot>();

    void Start()
    {
        // CreateSlots already paints every slot's frame plus the big icon/label for the
        // equipped slot, so building the wheel here is enough to initialize the display.
        CreateSlots();
        EquipSlot(equippedSlot);
    }

    // Rebuilds one child HotwheelSlot per slot, laid out in a single horizontal row
    // centered on this object. One slot sits dead center; each additional slot pushes
    // the row outward symmetrically so the center never drifts. Safe to call repeatedly.
    [ContextMenu("Rebuild Slots")]
    public void CreateSlots()
    {
        if (slotPrefab == null)
        {
            Debug.LogWarning($"{name}: Hotwheel has no slotPrefab assigned.", this);
            return;
        }

        if (totalSlots < 0) totalSlots = 0;

        // Keep the data list length in step with totalSlots: pad new entries empty,
        // and destroy + drop any that fall off the end (RemoveSlot trims the rightmost).
        while (slots.Count < totalSlots) slots.Add(new Slot());
        while (slots.Count > totalSlots)
        {
            int last = slots.Count - 1;
            if (slots[last].slotObject != null) SafeDestroy(slots[last].slotObject.gameObject);
            slots.RemoveAt(last);
        }

        // Center-to-center spacing = slot width + padding, so `horizontalpadding`
        // is the literal gap between neighbouring slots.
        float slotWidth = 0f;
        RectTransform prefabRect = slotPrefab.GetComponent<RectTransform>();
        if (prefabRect != null) slotWidth = prefabRect.rect.width;
        float step = slotWidth + horizontalpadding;

        int n = slots.Count;
        for (int i = 0; i < n; i++)
        {
            Slot s = slots[i];

            // Replace any previously-built object so this is idempotent.
            if (s.slotObject != null) SafeDestroy(s.slotObject.gameObject);

            HotwheelSlot inst = Instantiate(slotPrefab, transform, false);
            inst.name = $"Slot {i}";

            // Anchor to the parent's center, then offset symmetrically:
            //   x = (i - (n-1)/2) * step
            // n == 1 -> 0 (centered); the row grows outward from the middle as n
            // increases and the center never moves.
            RectTransform rt = (RectTransform)inst.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2((i - (n - 1) * 0.5f) * step, 0f);

            s.slotObject = inst;   // Slot is a struct -> write the field back...
            slots[i] = s;          // ...and reassign the list element.

            inst.SetSlot(s, i == equippedSlot);
        }

        if (n > 0)
        {
            equippedSlot = Mathf.Clamp(equippedSlot, 0, n - 1);
            SetBigIcon(slots[equippedSlot].bigIcon, slots[equippedSlot].label);
        }

        static void SafeDestroy(GameObject go)
        {
            if (Application.isPlaying) Destroy(go); else DestroyImmediate(go);
        }
    }

    public void AddSlot()
    {
        // adds an additional empty slot, reformats all slots properly
        totalSlots++;
        CreateSlots();
    }

    public void RemoveSlot()
    {
        // removes the rightmost slot (highest in index) regardless of if equipped or not
        if (totalSlots > 0) totalSlots--;
        CreateSlots();
    }

    public void SetBigIcon(Sprite icon, string label)
    {
        if (bigItemIcon != null) 
        {
            if (icon != null) bigItemIcon.gameObject.SetActive(true);
            else bigItemIcon.gameObject.SetActive(false);
            bigItemIcon.sprite = icon;
        }
        if (bigItemLabel != null) 
        {
            // if (label != "") bigItemLabel.gameObject.SetActive(true);
            // else bigItemLabel.gameObject.SetActive(false);
            bigItemLabel.text = label;
        }
    }

    public void EquipSlot(int slot)
    {
        if (slot < 0 || slot >= totalSlots) return;
        if (slot != equippedSlot) 
        {
            if (slots[equippedSlot].actionItem != null) player.Equip(slots[equippedSlot].actionItem);
            if (player.hitIndicator != null)
            {
                player.hitIndicator.gameObject.SetActive(true);
                player.hitIndicator.SetAmmo(0, 0);
            }
            // else
            // {
            //     player.EnableIK(false, false);
            // }
            slots[equippedSlot].slotObject.SetSlot(slots[equippedSlot], false);
        }
        equippedSlot = slot;
        slots[slot].slotObject.SetSlot(slots[slot], true);
        if (slots[slot].actionItem != null) player.Equip(slots[slot].actionItem);
        else
        {
            player.EnableIK(false, false);
            player.item = null;
            player.hitIndicator.gameObject.SetActive(false);
        }
        SetBigIcon(slots[slot].bigIcon, slots[slot].label);
    }

    public void EquipNext(bool forward = true)
    {
        int nextSlot = equippedSlot + (forward ? 1 : -1);
        if (nextSlot >= totalSlots) nextSlot = 0;
        else if (nextSlot < 0) nextSlot = totalSlots - 1;
        EquipSlot(nextSlot);
    }
}
