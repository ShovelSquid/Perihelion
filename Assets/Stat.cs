using System;
using UnityEngine;

public enum DataSource { Constant, Variable }

[CreateAssetMenu(fileName = "New Stat", menuName = "Visual Scripting/Stat")]
public class Stat : ScriptableObject
{
    public float BaseValue;
    public string Name;
    private float _lastValue;

    // This is the "Output Port". Other objects can subscribe to this event.
    public event Action<float> Output;

    public float Value
    {
        get { return BaseValue + 0; } // Add modifiers here if needed
        // You can also add logic to calculate the final value based on buffs, debuffs,
    }

    public Stat(float baseValue = 0, string name = "")
    {
        Name = name;
        BaseValue = baseValue;
        _lastValue = Value;
    }

    // Manually trigger the output
    public void Trigger()
    {
        Output?.Invoke(Value);
    }

    // Checks if the value has changed and fires the event
    private void CheckForChange(bool forceTrigger = false)
    {
        if (forceTrigger || Math.Abs(Value - _lastValue) > 0.001f)
        {
            _lastValue = Value;
            // Fire the event, sending the new value to all subscribers.
            Output?.Invoke(Value);
        }
    }
    public void Set(float newValue)
    {
        BaseValue = newValue;
    }
    public void Add(float newValue)
    {
        BaseValue += newValue;
    }

    public void Subtract(float newValue)
    {
        BaseValue -= newValue;
    }

    public void Multiply(float newValue)
    {
        BaseValue *= newValue;
    }

    public void Divide(float newValue)
    {
        if (Math.Abs(newValue) > 0.001f) // Avoid division by zero
        {
            BaseValue /= newValue;
        }
        else
        {
            BaseValue = 0; // handle it being equal to a VARIABLE designated to infinity. I.e. a stat that equals a STAGNATE x or y, which stores Inf in a new symbol, equivalent to 1 just in a higher dimension of math
        }
        // how do I set the value to be equal to a stat? 
        // basevalue = x
    }
}