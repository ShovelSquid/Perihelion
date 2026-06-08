using UnityEngine;

public class Spawner : MonoBehaviour
{
    private Object parentObject;
    public GameObject spawn;
    public Transform scaleBase;
    public float spawnRadius = 5f;
    public bool spawnOnEdge;
    public GameObject spawnEffect;
    public AudioClip spawnSound;


    public void Awake()
    {
        parentObject = GetComponentInParent<Object>();
    }

    public void Spawn(int amount)
    {
        for (int i = 0; i < amount; i++)
        {
            SpawnOne();
        }
    }

    public void SpawnOne()
    {
        if (spawn == null) return;
        Vector3 pos;
        float scale = (scaleBase.localScale.x + scaleBase.localScale.y + scaleBase.localScale.z) / 3f;
        if (spawnOnEdge)
        {
            Vector2 circle = Random.insideUnitCircle.normalized * spawnRadius * scale;
            pos = transform.position + new Vector3(circle.x, 0, circle.y);
        }
        else
        {
            Vector2 circle = Random.insideUnitCircle * spawnRadius * scale;
            pos = transform.position + new Vector3(circle.x, 0, circle.y);
        }
        if (spawnEffect != null)
        {
            Instantiate(spawnEffect, pos, Quaternion.identity);
        }
        if (spawnSound != null)
        {
            AudioSource.PlayClipAtPoint(spawnSound, pos);
        }
        GameObject spawnedObject = Instantiate(spawn, pos, Quaternion.identity);
        Palette p = spawnedObject.GetComponent<Palette>();
        if (parentObject != null && parentObject.colorPalette != null && p != null)
        {
            if (parentObject.colorPalette != null)
            {
                p.referencePalette = parentObject.colorPalette.referencePalette;
                // p.colorOnStart = false;
                p.colorName = parentObject.colorPalette.colorName;
                p.ColorObject(parentObject.colorPalette.colorName);
            }
            spawnedObject.GetComponent<Object>().team = parentObject.team;
        }
    }
}
