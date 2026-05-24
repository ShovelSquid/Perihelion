using System.Collections.Generic;
using UnityEngine;

public class BulletManager : MonoBehaviour
{
    // public LayerMask hitMask = ~0;
    // public ParticleSystem hitEffect;
    // public int growthSize = 20;

    private List<Projectile> activeBullets = new List<Projectile>();
    private Dictionary<GameObject, Queue<Projectile>> pools = new Dictionary<GameObject, Queue<Projectile>>();

    public void Prewarm(GameObject prefab)
    {
        if (prefab == null || prefab.GetComponent<Projectile>().prewarmCount <= 0) return;
        if (!pools.TryGetValue(prefab, out Queue<Projectile> pool))
        {
            pool = new Queue<Projectile>();
            pools[prefab] = pool;
        }
        for (int i = 0; i < prefab.GetComponent<Projectile>().prewarmCount; i++)
        {
            GameObject obj = Instantiate(prefab, transform);
            obj.SetActive(false);
            Projectile p = obj.GetComponent<Projectile>();
            p.bm = this;
            p.prefabKey = prefab;
            pool.Enqueue(p);
        }
    }

    public Projectile Get(GameObject prefab)
    {
        if (!pools.ContainsKey(prefab))
            Prewarm(prefab);
        if (!pools.TryGetValue(prefab, out Queue<Projectile> pool))
        {
            pool = new Queue<Projectile>();
            pools[prefab] = pool;
        }
        if (pool.Count == 0) Prewarm(prefab);
        Projectile p = pool.Dequeue();
        p.gameObject.SetActive(true);
        activeBullets.Add(p);
        return p;
    }

    public void Return(Projectile p)
    {
        p.gameObject.SetActive(false);
        activeBullets.Remove(p);
        if (p.prefabKey != null && pools.TryGetValue(p.prefabKey, out Queue<Projectile> pool))
            pool.Enqueue(p);
        else
            Destroy(p.gameObject);
    }

    void Update()
    {
        for (int i = activeBullets.Count - 1; i >= 0; i--)
        {
            Projectile bullet = activeBullets[i];
            bullet.Move();

            if (SweptHit(bullet, out RaycastHit hit))
            {
                OnBulletHit(bullet, hit);
                continue;
            }

            if (Time.time > bullet.deathtime) bullet.End();
        }
    }

    /// <summary>
    /// Sweeps from the bullet's previous position to its current position.
    /// SphereCast when radius > 0, Raycast otherwise. Prevents tunneling at any speed.
    /// </summary>
    private bool SweptHit(Projectile bullet, out RaycastHit hit)
    {
        Vector3 delta = bullet.Proj.position - bullet.previousPosition;
        float dist = delta.magnitude;
        if (dist < 1e-5f) { hit = default; return false; }
        Vector3 dir = delta / dist;
        if (bullet.radius > 0f)
            return Physics.SphereCast(bullet.previousPosition, bullet.radius, dir, out hit, dist, bullet.hitMask, QueryTriggerInteraction.Ignore);
        return Physics.Raycast(bullet.previousPosition, dir, out hit, dist, bullet.hitMask, QueryTriggerInteraction.Ignore);
    }

    private void OnBulletHit(Projectile bullet, RaycastHit hit)
    {
        Object obj = hit.collider.GetComponentInParent<Object>();
        if (obj != null)
        {
            obj.Damage(bullet.damage);
            obj.HitPhysics(hit.point, hit.normal, bullet.damage);
        }
        if (bullet.hitEffect != null)
            Instantiate(bullet.hitEffect, hit.point, Quaternion.LookRotation(hit.normal));
        bullet.End();
    }

    void OnDrawGizmos()
    {
        if (activeBullets == null) return;
        Gizmos.color = Color.yellow;
        foreach (Projectile b in activeBullets)
            for (int i = 0; i < b.points.Count - 1; i++)
                Gizmos.DrawLine(b.points[i], b.points[i + 1]);
    }
}
