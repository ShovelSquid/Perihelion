// Connects a Stat to another Stat
using UnityEngine;

public class Wire : MonoBehaviour
{
    public Stat inputStat;  // This will now be a field you can drag a Stat asset into
    public Stat outputStat; // This will also be a field for a Stat asset

    public enum Operation { Set, Add, Subtract, Multiply, Divide }
    public Operation operation;

    void OnEnable()
    {
        if (inputStat != null && outputStat != null)
        {
            inputStat.Output += OnTrigger;
        }
    }

    void OnDisable()
    {
        if (inputStat != null)
        {
            inputStat.Output -= OnTrigger;
        }
    }

    private void OnTrigger(float inputValue)
    {
        if (outputStat != null)
        {
            switch (operation)
            {
                case Operation.Set:
                    outputStat.Set(inputValue);
                    break;
                case Operation.Add:
                    outputStat.Add(inputValue);
                    break;
                case Operation.Subtract:
                    outputStat.Add(-inputValue);
                    break;
                case Operation.Multiply:
                    outputStat.Multiply(inputValue);
                    break;
                case Operation.Divide:
                    outputStat.Divide(inputValue);
                    break;
            }
        }
    }
}