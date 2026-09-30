using TMPro;
using UnityEngine;

// Example panel widget: shows the object's name (or a fixed override) in a TextMeshPro label.
[RequireComponent(typeof(TextMeshProUGUI))]
public class ObjectPanelLabel : MonoBehaviour, IObjectPanelWidget
{
    public string overrideText; // shown instead of the object's name when set
    public bool stripClone = true; // drop Unity's "(Clone)" suffix from spawned objects' names

    public void Bind(Object obj)
    {
        TextMeshProUGUI label = GetComponent<TextMeshProUGUI>();
        if (!string.IsNullOrEmpty(overrideText))
        {
            label.text = overrideText;
            return;
        }
        string text = obj != null ? obj.name : "";
        if (stripClone) text = text.Replace("(Clone)", "").Trim();
        label.text = text;
    }
}
