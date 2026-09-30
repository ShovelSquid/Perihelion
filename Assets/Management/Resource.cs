using UnityEngine;

public class Resource : MonoBehaviour
{
    // Fuel, Bullets, Rockets, Metal Scrap, Energy Cells, Water, Food, Air, Medicine, Tools
    public enum ResourceType
    {
        Fuel,
        Bullets,
        Rockets,
        MetalScrap,
        EnergyCells,
        Water,
        Food,
        Air,
        Medicine,
        Tools
    }

    public ResourceType resourceType;
}