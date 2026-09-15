using UnityEngine;

public class Building : MonoBehaviour
{
    // Refinery, Factory, Turret, PowerPlant, Storage, Miner, Station
    public enum BuildingType
    {
        Refinery,
        Factory,
        Turret,
        PowerPlant,
        Storage,
        Miner,
        Station
    }

    public BuildingType buildingType;
}