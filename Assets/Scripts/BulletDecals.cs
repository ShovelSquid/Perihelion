using UnityEngine;
using UnityEngine.Rendering.Universal;

// Pooled bullet-hole decals, placed by BulletManager on every bullet hit.
// Uses URP Decal Projectors, which only render if the URP renderer has the "Decal" renderer feature
// (add it on PC_Renderer, and PXL_Renderer if used) and the material uses a Shader Graphs/Decal shader.
// A fixed ring of projectors is reused: the oldest hole is recycled once maxDecals are on screen,
// so heavy fire never allocates.
public class BulletDecals : MonoBehaviour
{
    [Header("Look")]
    public Material decalMaterial; // Shader Graphs/Decal material with the bullet-hole texture
    public Vector2 size = new Vector2(0.12f, 0.12f); // meters across the surface
    public float projectionDepth = 0.2f; // meters the projector box reaches into/out of the surface; enough to wrap small bumps
    public Vector2 sizeJitter = new Vector2(0.85f, 1.15f); // random scale range per hole so repeats don't look stamped
    public bool randomRotation = true; // spin each hole around the surface normal
    [Header("Lifetime")]
    public int maxDecals = 128; // ring size; the oldest hole is recycled past this
    public float lifetime = 20f; // seconds before a hole starts fading; 0 = never fade
    public float fadeTime = 2f; // seconds to fade out after lifetime
    [Header("Surfaces")]
    public bool skipMobs = true; // no holes on characters, where they'd float over animated skin

    DecalProjector[] ring;
    float[] spawnTime;
    int next;

    void Awake()
    {
        if (decalMaterial == null) Debug.LogWarning($"{name}: BulletDecals has no decal material, so no holes will show.", this);
        ring = new DecalProjector[Mathf.Max(1, maxDecals)];
        spawnTime = new float[ring.Length];
    }

    public void Spawn(RaycastHit hit)
    {
        if (decalMaterial == null || hit.collider == null) return;
        // A SphereCast that starts overlapping reports a zero point and distance; there's no real surface to mark.
        if (hit.distance <= 0f && hit.point == Vector3.zero) return;
        if (skipMobs && hit.collider.GetComponentInParent<Mob>() != null) return;

        DecalProjector d = ring[next];
        // Unity null: the projector dies with whatever it was parented to, so rebuild it on demand.
        if (d == null)
        {
            GameObject go = new GameObject("BulletDecal");
            d = go.AddComponent<DecalProjector>();
            // Hierarchy scale is ignored so parenting to a scaled object doesn't stretch the hole.
            d.scaleMode = DecalScaleMode.ScaleInvariant;
            ring[next] = d;
        }

        float s = Random.Range(sizeJitter.x, sizeJitter.y);
        d.material = decalMaterial;
        d.size = new Vector3(size.x * s, size.y * s, projectionDepth);
        d.pivot = Vector3.zero; // box centered on the hit, so it reaches equally into and out of the surface
        d.fadeFactor = 1f;

        // Projectors shoot along their forward, so face into the surface.
        Quaternion rot = Quaternion.LookRotation(-hit.normal);
        if (randomRotation) rot = rot * Quaternion.AngleAxis(Random.Range(0f, 360f), Vector3.forward);

        // Parented to what was hit so holes ride along on doors, props and anything that moves.
        Transform t = d.transform;
        t.SetParent(hit.collider.transform, true);
        t.SetPositionAndRotation(hit.point, rot);
        d.gameObject.SetActive(true);

        spawnTime[next] = Time.time;
        next = (next + 1) % ring.Length;
    }

    void Update()
    {
        if (lifetime <= 0f) return;
        float now = Time.time;
        for (int i = 0; i < ring.Length; i++)
        {
            DecalProjector d = ring[i];
            if (d == null || !d.gameObject.activeSelf) continue;
            float age = now - spawnTime[i] - lifetime;
            if (age <= 0f) continue;
            if (fadeTime <= 0f || age >= fadeTime)
            {
                d.gameObject.SetActive(false);
                continue;
            }
            d.fadeFactor = 1f - age / fadeTime;
        }
    }
}
