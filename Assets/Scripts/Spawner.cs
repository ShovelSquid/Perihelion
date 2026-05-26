using UnityEngine;

public class Spawner : MonoBehaviour
{
    public GameObject spawn;
    public Transform scaleBase;
    public float spawnRadius = 5f;
    public bool spawnOnEdge;
    public GameObject spawnEffect;
    public AudioClip spawnSound;


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
        Instantiate(spawn, pos, Quaternion.identity);
    }
}
