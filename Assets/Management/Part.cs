using UnityEngine;

public class Part : MonoBehaviour
{
    // Legs, Arms, Torso, Head, Weapon, Sensor, Engine, Battery, Utility, Storage
    public enum PartType
    {
        Legs,
        Arms,
        Torso,
        Head,
        Weapon,
        Sensor,
        Engine,
        Battery,
        Utility,
        Storage
    }

    public PartType partType;
    public int health;
    public float damagedHealth;
    public float destroyedHealth;
    public int armor;
    public int power;
    public float mass;
}