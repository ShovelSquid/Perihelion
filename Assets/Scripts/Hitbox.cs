using System.Collections.Generic;
using UnityEngine;

// One body part. Goes on each body-part collider (skinned characters get regular
// colliders on their bones) and maps that collider to part data (name, own health
// pool, armor, aim weight) and the owning Object. BulletManager resolves a Hitbox on
// the struck collider before falling back to Object, and the owning Object registers
// this collider with its Hitbox as an AimPart for HandRig's aim assist. The component
// is optional: plain colliders on the Hitbox layer are aimable too.
public class Hitbox : MonoBehaviour
{
    [Header("Part")]
    public string partName; // defaults to the GameObject's name when left empty
    public float maxHealth = 50f; // this part's own health pool
    public float armor = 0f; // flat damage removed from each hit before it reaches the part and the owner, floored at 0
    public float aimWeight = 1f; // how much aim assist favours this part, above 1 for heads; only HandRig.ScorePart reads it
    public Object owner; // the damageable this part belongs to; found in the parents when unset

    [System.NonSerialized] public float health; // current part pool, starts at maxHealth

    private Collider partCollider;

    public Collider PartCollider
    {
        get { return partCollider; }
    }

    public bool IsBroken
    {
        get { return health <= 0f; }
    }

    void Awake()
    {
        if (string.IsNullOrEmpty(partName)) partName = gameObject.name;
        if (owner == null) owner = GetComponentInParent<Object>();
        health = maxHealth;
        partCollider = GetComponent<Collider>();
        // Collider is abstract, so RequireComponent can't add one; warn instead.
        if (partCollider == null)
            Debug.LogWarning("Hitbox on " + gameObject.name + " has no Collider; bullets and aim assist can't find it.", this);
        else if (partCollider is MeshCollider mesh && !mesh.convex)
            Debug.LogWarning("Hitbox on " + gameObject.name + " uses a non-convex MeshCollider; ClosestPoint needs box, sphere, capsule or convex mesh colliders.", this);
    }

    // Armor comes off first, the rest lowers this part's pool and is passed to the owner,
    // whose own invincible/destroyed guards still apply. Returns the damage actually dealt.
    public float Damage(float amount)
    {
        if (amount <= 0f) return 0f;
        float effective = Mathf.Max(0f, amount - armor);
        health = Mathf.Max(0f, health - effective);
        if (owner != null) owner.Damage(effective);
        return effective;
    }
}

// One aimable collider. Hitbox parts are registered here by their owning Object; body colliders
// are wrapped in an AimPart too but never registered, so IsRegistered keeps meaning "hitbox part".
// HandRig reaches both through the owner's AimBody.
public class AimPart
{
    public readonly Collider collider; // the part's collider; aim points are found on it
    public readonly Hitbox hitbox; // optional part data; null for a plain collider on the Hitbox layer
    public readonly Object owner; // whose layer sets aim priority

    private static readonly List<AimPart> active = new List<AimPart>();
    private static readonly Dictionary<Collider, AimPart> byCollider = new Dictionary<Collider, AimPart>(); // fast "is this collider a part" lookup for line of sight

    public static IReadOnlyList<AimPart> Active
    {
        get { return active; }
    }

    public AimPart(Collider collider, Hitbox hitbox, Object owner)
    {
        this.collider = collider;
        this.hitbox = hitbox;
        this.owner = owner;
    }

    public static void Register(AimPart part)
    {
        if (part == null) return;
        active.Add(part);
        // ReferenceEquals, not ==, so a collider destroyed later still matches its own key on Unregister.
        if (!ReferenceEquals(part.collider, null)) byCollider[part.collider] = part;
    }

    public static void Unregister(AimPart part)
    {
        if (part == null) return;
        active.Remove(part);
        if (!ReferenceEquals(part.collider, null) && byCollider.TryGetValue(part.collider, out AimPart mapped) && mapped == part)
            byCollider.Remove(part.collider);
    }

    // True when col is a currently registered aim part of any Object.
    public static bool IsRegistered(Collider col)
    {
        return !ReferenceEquals(col, null) && byCollider.ContainsKey(col);
    }

    // Exists (Unity null check, so destroyed colliders fail), enabled, and active in the hierarchy.
    public static bool IsLive(Collider col)
    {
        return col != null && col.enabled && col.gameObject.activeInHierarchy;
    }

    // Domain reload can be disabled in play mode options, which keeps statics alive
    // between sessions; clear the registry so destroyed parts from the last run don't linger.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRegistry()
    {
        active.Clear();
        byCollider.Clear();
    }
}

// One Object's whole aim footprint. HandRig tests the cone against the whole object first,
// then scores only its parts, or its body colliders when it has none.
public class AimBody
{
    public readonly Object owner; // the Object that collected this footprint
    public readonly List<AimPart> bodies; // non-trigger, non-part colliders with a null Hitbox; never in AimPart's registry
    public readonly List<AimPart> parts; // the same instances the owner registers with AimPart as hitbox parts

    private static readonly List<AimBody> active = new List<AimBody>();

    public static IReadOnlyList<AimBody> Active
    {
        get { return active; }
    }

    public AimBody(Object owner, List<AimPart> bodies, List<AimPart> parts)
    {
        this.owner = owner;
        this.bodies = bodies ?? new List<AimPart>();
        this.parts = parts ?? new List<AimPart>();
    }

    public static void Register(AimBody body)
    {
        if (body == null) return;
        active.Add(body);
    }

    public static void Unregister(AimBody body)
    {
        if (body == null) return;
        active.Remove(body);
    }

    // Read fresh each call because the owner moves; false when none of its colliders is live.
    public bool TryGetBounds(out Bounds bounds)
    {
        bounds = default;
        bool any = false;
        Encapsulate(bodies, ref bounds, ref any);
        Encapsulate(parts, ref bounds, ref any);
        return any;
    }

    private static void Encapsulate(List<AimPart> list, ref Bounds bounds, ref bool any)
    {
        for (int i = 0; i < list.Count; i++)
        {
            Collider col = list[i].collider;
            if (!AimPart.IsLive(col)) continue;
            if (any)
            {
                bounds.Encapsulate(col.bounds);
            }
            else
            {
                bounds = col.bounds;
                any = true;
            }
        }
    }

    // Domain reload can be disabled in play mode options, which keeps statics alive
    // between sessions; clear the registry so destroyed objects from the last run don't linger.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRegistry()
    {
        active.Clear();
    }
}
