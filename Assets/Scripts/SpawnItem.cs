using UnityEngine;

public class SpawnItem : MonoBehaviour
{
    public GameObject itemPrefab;
    public GameObject spawnedItem;
    public float respawnTime = 10f;
    private bool itemPresent = false;
    public bool spawnOnStart = false;


    void Start()
    {
        if (spawnOnStart)
        {
            Spawn();
        }
    }

    void Spawn()
    {
        if (!itemPresent)
        {
            spawnedItem = Instantiate(itemPrefab, transform.position, Quaternion.identity);
            spawnedItem.GetComponent<Item>().spawner = this;
            itemPresent = true;
        }
    }

    public void ItemPickedUp()
    {
        itemPresent = false;
        Invoke("Spawn", respawnTime);
    }
}
