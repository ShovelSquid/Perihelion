using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Inventory))]
public class Object : MonoBehaviour
{
    public Animator anim;
    protected Rigidbody rb;
    protected Inventory body;
    public Healthbar healthbar;
    public Team team;
    public bool useTeamColor = true;
    public Palette colorPalette;
    private Shine shine;
    public float damageflashDuration = 0.1f;
    public ParticleSystem hitParticle;
    public ParticleSystem hitMistParticle;
    public bool invincible = false;
    public bool still = false; // If true, this object can't be moved or affected by physics
    public bool interactible = false; // If true, player can interact with this object (e.g. press E to interact)
    [HideInInspector]
    public InteractionTrigger interactionTrigger;
    public Outline outline;
    public float hp;
    public int max_hp;
    // public float mass = 1f;
    public float density = 1f;
    public bool destroyed = false;
    public GameObject destroyedVersion; // optional prefab to spawn when building is destroyed (e.g. rubble)
    public List<float> damageThresholds = new List<float>(); // from 1 to 0, in descending order. When hp drops below these percentages, the damage state changes (handled by DamageStates.cs)
    public int damageState = 0; // num correlating to num of list on damage states script for current texture
    private DamageStates damageStates;
    private GameObject spawnedDestroyed;
    public ParticleSystem deathEffect;
    public float deathForce = 10f;
    public float deathForceMult;
    public float hitRadius = 1f;
    public float endTime = 5f;


    protected virtual void Awake()
    {
        if (body == null) body = GetComponent<Inventory>();
        if (rb == null) rb = GetComponent<Rigidbody>();
        if (anim == null) anim = GetComponent<Animator>();
        if (interactionTrigger == null) interactionTrigger = GetComponentInChildren<InteractionTrigger>();
        if (colorPalette == null) colorPalette = GetComponent<Palette>();
        if (colorPalette != null && useTeamColor && team != null)
        {
            colorPalette.colorName = team.colorName;
            // colorPalette.ColorObject(team.colorName);
        }
        if (shine == null) shine = GetComponent<Shine>();
        damageStates = GetComponent<DamageStates>();
        hp = max_hp;
    }

    protected virtual void Start()
    {
        // if (body == null) body = GetComponent<Inventory>();
        // if (rb != null) rb.mass = rb.mass * density;
        if (healthbar != null) healthbar.SetMaxHealth(max_hp);
        if (outline != null) outline.enabled = false;
        if (colorPalette != null) colorPalette.ColorObject(colorPalette.colorName);
        // if (team != null && colorPalette != null && colorPalette.colorName == "")
        // {
        //     colorPalette.colorName = team.colorName;
        //     colorPalette.ColorObject(team.colorName);
        // }
    }

    public virtual void Interact()
    {
        // Override this method in child classes to make the object interactible (e.g. open a door, loot a chest, etc.)
    }

    public virtual void Activate()
    {
        // Override this method in child classes to make the object activate (e.g. turn on a machine, start a trap, etc.)
    }

    public void InteractOutline(bool on)
    {
        if (outline != null)
        {
            outline.enabled = on;
        }
    }

    public virtual void Damage(float damage)
    {
        if (destroyed) return;
        if (invincible) return;
        hp -= damage;

        HitEffect fx = GetComponent<HitEffect>();
        if (fx != null) fx.Play();
        if (hitParticle != null) hitParticle.Emit(1);
        if (damageStates != null) damageStates.UpdateDamageState(hp / max_hp);

        // int dState = GetDamageState();
        // if (dState != damageState && damageStates != null)
        // {
        //     damageState = dState;
        //     damageStates.UpdateDamageState(hp / max_hp);
        // }

        if (hp < 1)
        {
            float extraDamage = -hp;
            hp = 0;
            Die(extraDamage);
        }

        if (colorPalette != null && colorPalette.colorOnDamage) colorPalette.ColorObject(colorPalette.colorName, "Damage", 0.3f);
        Invoke("ResetColor", damageflashDuration);
        if (shine != null) shine.Shiney(damageflashDuration);
        if (healthbar != null) healthbar.SetHealth((int)hp);
    }

    public void ResetColor()
    {
        if (colorPalette != null && colorPalette.colorOnDamage) colorPalette.ColorObject(colorPalette.colorName);
    }

    // public int GetDamageState()
    // {
    //     if (damageThresholds.Count == 0) return 0;
    //     int state = 0;
    //     float healthPct = hp / max_hp;
    //     // Thresholds are 0..1, descending (e.g. [0.75, 0.5, 0.25]).
    //     // Walk from the lowest threshold (largest index) upward; first match wins.
    //     for (int i = damageThresholds.Count - 1; i >= 0; i--)
    //     {
    //         if (healthPct <= damageThresholds[i])
    //         {
    //             state = i;
    //             break;
    //         }
    //     }
    //     return state;
    // }

    public virtual void Heal(float heal)
    {
        if (destroyed) return;
        hp += heal;
        if (hp > max_hp) hp = max_hp;
        if (healthbar != null) healthbar.SetHealth((int)hp);
    }

    protected virtual void Die(float extraDamage = 0f)
    {
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
        }
        if (destroyedVersion != null)
        {
            GameObject e = Instantiate(destroyedVersion, transform.position, transform.rotation);
            e.transform.localScale = transform.localScale;
            Palette p = e.GetComponent<Palette>();
            if (p != null && colorPalette != null)
            {
                p.referencePalette = colorPalette.referencePalette;
                p.ColorObject(colorPalette.colorName, "Destroy", 0.5f);
            }
            spawnedDestroyed = e;
        }
        if (deathEffect != null)
        {
            Instantiate(deathEffect, transform.position, Quaternion.identity);
        }
        gameObject.SetActive(false); // don't destroy the building object, since we want to keep its collider and other components for the rubble. Just hide it.
        // if (anim != null) anim.SetBool("Destroyed", true);
        Invoke("End", endTime);
        destroyed = true;
    }
    public void End()
    {
        if (healthbar != null) Destroy(healthbar.gameObject);
        Destroy(gameObject);
    }

    public virtual void HitPhysics(Vector3 point, Vector3 normal, float force)
    {
        if (destroyed && spawnedDestroyed != null)
        {
            Rigidbody[] rbs = spawnedDestroyed.GetComponentsInChildren<Rigidbody>();
            float totalWeight = 0f;
            foreach (Rigidbody b in rbs)
            {
                float dist = (b.position - point).magnitude;
                if (dist > hitRadius) continue;
                totalWeight += 1f - dist / hitRadius;
            }
            if (totalWeight > 0f)
            {
                foreach (Rigidbody b in rbs)
                {
                    Vector3 delta = b.position - point;
                    float dist = delta.magnitude;
                    if (dist > hitRadius) continue;
                    Vector3 dir = dist > 1e-5f ? delta / dist : -normal;
                    float share = (1f - dist / hitRadius) / totalWeight;
                    //  * Mathf.Pow((transform.localScale.x + transform.localScale.y + transform.localScale.z) / 3, 3) 
                    b.AddForceAtPosition(dir * (force + deathForce) * deathForceMult * share, point, ForceMode.Impulse);
                }
            }
        }
        else if (rb != null && !still)
        {
            rb.AddForceAtPosition(-normal * force * rb.mass, point, ForceMode.Impulse);
        }
        if (hitMistParticle != null)
        {
            var hmp = Instantiate(hitMistParticle, point, Quaternion.LookRotation(normal));
            // hmp.transform.position = point;
            // hmp.transform.rotation = Quaternion.LookRotation(normal);
        }
    }
}
