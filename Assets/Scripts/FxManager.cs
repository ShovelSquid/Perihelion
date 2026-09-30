using System.Collections.Generic;
using UnityEngine;

// Plays one-shot particle effects from a per-prefab pool (same dictionary-of-queues shape as BulletManager).
// Effect prefabs must not loop, or they never return to the pool.
public class FxManager : MonoBehaviour
{
    [System.Serializable]
    public class WarmEntry
    {
        public ParticleSystem prefab;
        public int count = 4;
    }

    [Header("Pooling")]
    public int defaultWarmCount = 0;
    public List<WarmEntry> warm = new List<WarmEntry>();

    private Dictionary<ParticleSystem, Queue<ParticleSystem>> pools = new Dictionary<ParticleSystem, Queue<ParticleSystem>>();

    void Awake()
    {
        foreach (WarmEntry entry in warm)
        {
            if (entry.prefab != null && entry.count > 0) Prewarm(entry.prefab, entry.count);
        }
    }

    // Keeps call sites to one line and preserves plain-Instantiate behaviour in scenes without an FxManager.
    public static void PlayOrInstantiate(FxManager fx, ParticleSystem prefab, Vector3 pos, Quaternion rot)
    {
        if (prefab == null) return;
        if (fx != null) fx.Play(prefab, pos, rot);
        else Instantiate(prefab, pos, rot);
    }

    private Queue<ParticleSystem> GetPool(ParticleSystem prefab)
    {
        Queue<ParticleSystem> pool;
        if (!pools.TryGetValue(prefab, out pool))
        {
            pool = new Queue<ParticleSystem>();
            pools[prefab] = pool;
        }
        return pool;
    }

    public void Prewarm(ParticleSystem prefab, int count)
    {
        if (prefab == null) return;
        Queue<ParticleSystem> pool = GetPool(prefab);
        for (int i = 0; i < count; i++) pool.Enqueue(Spawn(prefab));
    }

    private ParticleSystem Spawn(ParticleSystem prefab)
    {
        ParticleSystem ps = Instantiate(prefab, transform);

        FxPoolItem item = ps.GetComponent<FxPoolItem>();
        if (item == null) item = ps.gameObject.AddComponent<FxPoolItem>();
        item.manager = this;
        item.prefabKey = prefab;

        // Callback only on the ROOT system: it fires once the root and its child systems are all finished.
        // Setting it on children too would return the same instance to the pool multiple times.
        var main = ps.main;
        main.stopAction = ParticleSystemStopAction.Callback;
        if (main.loop) Debug.LogWarning($"{name}: {prefab.name} is looping and will never return to the pool.", prefab);

        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        return ps;
    }

    public void Play(ParticleSystem prefab, Vector3 pos, Quaternion rot)
    {
        if (prefab == null) return;
        Queue<ParticleSystem> pool = GetPool(prefab);

        ParticleSystem ps = null;
        while (pool.Count > 0 && ps == null) ps = pool.Dequeue(); // skip destroyed entries
        if (ps == null) ps = Spawn(prefab);

        ps.transform.SetPositionAndRotation(pos, rot);
        // Clear BEFORE Play so a reused instance doesn't flash its previous particles.
        ps.Clear(true);
        ps.Play(true);
    }

    public void Return(ParticleSystem ps, ParticleSystem key)
    {
        if (ps == null) return;
        Queue<ParticleSystem> pool;
        if (key != null && pools.TryGetValue(key, out pool)) pool.Enqueue(ps);
        else Destroy(ps.gameObject);
    }
}

public class FxPoolItem : MonoBehaviour
{
    [HideInInspector] public FxManager manager;
    [HideInInspector] public ParticleSystem prefabKey;

    // Only fires because FxManager sets main.stopAction = Callback on the root system.
    void OnParticleSystemStopped()
    {
        if (manager != null) manager.Return(GetComponent<ParticleSystem>(), prefabKey);
    }
}
