using UnityEngine;
using UnityEngine.UI;
using TMPro;


public class HotwheelSlot : MonoBehaviour
{
    public Image icon;
    public Image frame;
    public Sprite selectedFrame;
    public Sprite baseFrame;
    public TextMeshProUGUI label;

    public void SetSlot(Hotwheel.Slot slot, bool equipped)
    {
        if (icon != null && slot.icon != null) 
        { 
            icon.sprite = slot.icon;
            icon.gameObject.SetActive(true);
        }
        else if (icon != null && slot.icon == null) icon.gameObject.SetActive(false);
        if (label != null) label.text = slot.label;
        if (frame != null) frame.sprite = equipped ? selectedFrame : baseFrame;
    }
}