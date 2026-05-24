using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Inventory))]
public class Object : MonoBehaviour
{
    public Animator anim;
    protected Rigidbody rb;
    protected Inventory body;
    public Healthbar healthbar;
    public ParticleSystem hitParticle;
    public ParticleSystem hitMistParticle;
    public bool invincible = false;
    public bool still = false; // If true, this object can't be moved or affected by physics
    public float hp;
    public int max_hp;
    // public float mass = 1f;
    public float density = 1f;
    public bool destroyed = false;
    public GameObject destroyedVersion; // optional prefab to spawn when building is destroyed (e.g. rubble)
    public List<float> damageThresholds = new List<float>(); // from 1 to 0, in descending order. When hp drops below these percentages, the damage state changes (handled by DamageStates.cs)
    public int damageState = 0; // num correlating to num of list on damage states script for current texture
    private DamageStates damageStates;
    public ParticleSystem deathEffect;


    void Awake()
    {
        if (body == null) body = GetComponent<Inventory>();
        if (rb == null) rb = GetComponent<Rigidbody>();
        if (anim == null) anim = GetComponent<Animator>();
        damageStates = GetComponent<DamageStates>();
        hp = max_hp;
    }

    protected virtual void Start()
    {
        // if (body == null) body = GetComponent<Inventory>();
        // if (rb != null) rb.mass = rb.mass * density;
        if (healthbar != null) healthbar.SetMaxHealth(max_hp);
    }

    public virtual void Damage(float damage)
    {
        if (destroyed) return;
        if (invincible) return;
        hp -= damage;

        HitEffect fx = GetComponent<HitEffect>();
        if (fx != null) fx.Play();
        if (hitParticle != null) hitParticle.Emit(1);

        int dState = GetDamageState();
        if (dState != damageState && damageStates != null)
        {
            damageState = dState;
            damageStates.UpdateDamageState(damageState);
        }

        if (hp < 1)
        {
            float extraDamage = -hp;
            hp = 0;
            Die(extraDamage);
        }

        if (healthbar != null) healthbar.SetHealth((int)hp);


    }

    public int GetDamageState()
    {
        if (damageThresholds.Count == 0) return 0;
        int state = 0;
        float healthPct = hp / max_hp;
        // Thresholds are 0..1, descending (e.g. [0.75, 0.5, 0.25]).
        // Walk from the lowest threshold (largest index) upward; first match wins.
        for (int i = damageThresholds.Count - 1; i >= 0; i--)
        {
            if (healthPct <= damageThresholds[i])
            {
                state = i;
                break;
            }
        }
        return state;
    }

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
        }
        if (deathEffect != null)
        {
            Instantiate(deathEffect, transform.position, Quaternion.identity);
        }
        gameObject.SetActive(false); // don't destroy the building object, since we want to keep its collider and other components for the rubble. Just hide it.
        // if (anim != null) anim.SetBool("Destroyed", true);
        destroyed = true;
    }

    public virtual void HitPhysics(Vector3 point, Vector3 normal, float force)
    {
        if (rb != null && !still)
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
