using System.Collections.Generic;
using UnityEngine;

// One body part. Goes on each body-part collider (skinned characters get regular
// colliders on their bones) and maps that collider to part data (name, own health
// pool, armor, aim weight) and the owning Object. BulletManager resolves a Hitbox on
// the struck collider before falling back to Object, and HandRig's aim assist reads
// the static Active registry to find parts inside each hand's cone.
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

    private static readonly List<Hitbox> active = new List<Hitbox>();

    public static IReadOnlyList<Hitbox> Active
    {
        get { return active; }
    }

    public Collider PartCollider
    {
        get { return partCollider; }
    }

    public bool IsBroken
    {
        get { return health <= 0f; }
    }

    // Domain reload can be disabled in play mode options, which keeps statics alive
    // between sessions; clear the registry so destroyed parts from the last run don't linger.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRegistry()
    {
        active.Clear();
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

    // Object.Die deactivates the owner, which disables its hitboxes, so dead characters leave the registry on their own.
    void OnEnable()
    {
        if (!active.Contains(this)) active.Add(this);
    }

    void OnDisable()
    {
        active.Remove(this);
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
