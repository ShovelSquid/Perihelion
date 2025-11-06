using UnityEngine;
using TMPro;

public class StatDisplay : MonoBehaviour
{
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI valueText;
    private Stat _linkedStat;

    // Initialize the display with a Stat object
    public void LinkToStat(Stat stat)
    {
        _linkedStat = stat;
        nameText.text = _linkedStat.Name;
        UpdateValueText(_linkedStat.Value); // Set initial value

        // Subscribe to the stat's output event to get live updates
        _linkedStat.Output += UpdateValueText;
    }

    // This method is called by the event when the stat's value changes
    private void UpdateValueText(float newValue)
    {
        valueText.text = newValue.ToString("0.##"); // Format to 2 decimal places if needed
    }

    // IMPORTANT: Unsubscribe when the object is destroyed to prevent memory leaks
    private void OnDestroy()
    {
        if (_linkedStat != null)
        {
            _linkedStat.Output -= UpdateValueText;
        }
    }
}