using UnityEngine;
using System;
using System.Collections;
using Unity.Mathematics;
using System.Collections.Generic;

public class Mob : MonoBehaviour
{
    public Inventory loot;
    public Inventory body;
    public Animator anim;
    public AudioSource adio;
    public BoxCollider box;
    public AudioClip fallDamagSound;
    public ParticleSystem hitParticle;
    public ParticleSystem airJumpParticle;
    public ParticleSystem groundJumpParticle;
    public Transform jumpFXPoint;
    public Healthbar healthbar;
    public Transform spawnPoint;
    public bool dead = false;
    public bool respawn = false;
    public int xp;
    public int xp_base;
    public int level;
    public int reference_number;
    public float hp;
    public int hp_base;
    public int max_hp;
    [Header("Fall Damage")]
    public float fallDamageSpeedMin;
    [Range(0f, 1f)]
    public float fallDamagePct;
    [Range(0f, 10f)]
    public float fallDamageExp;
    [Header("Combat")]
    public List<string> attackAnimations = new List<string>();
    public float damage;
    public float attackSpeed;
    public bool attackReady;
    public bool takeDamage;
    public bool giveHeal;
    public bool takeFallDamage;
    public int fallSpeedTest;
    public bool invincible = false;
    [Header("Stamina")]
    public float stamina = 100f;
    public float maxStamina = 100f;
    public float staminaRegen = 5f;
    public float staminaRegenDelay = 2f;
    public float staminaRegenCooldown = 3f;
    public float staminaRegenAmount = 1f;
    private bool isRegeneratingStamina = false;
    private Coroutine staminaRegenCoroutine;

    [Header("Health Regen")]
    public float healthRegenDamageCooldown = 5f;
    public float healthRegenCooldown = 3f;
    public float healthRegenAmount = 1f;
    private bool isRegenerating = false;
    private Coroutine healthRegenCoroutine;
    public float respawnTime = 5f;

    void Awake()
    {
        box = GetComponent<BoxCollider>();
        if (healthbar != null)
        {
            healthbar.SetMaxHealth(max_hp);
        }
        isRegenerating = true;
    }

    void Start()
    {
        if (isRegenerating)
        {
            healthRegenCoroutine = StartCoroutine(HealthRegen(healthRegenCooldown));
        }
        if (!attackReady)
        {
            Invoke("ReadyAttack", attackSpeed);
        }
    }

    void OnValidate()
    {
        if (takeDamage)
        {
            Damage(damage);
            takeDamage = false;
        }
        if (giveHeal)
        {
            Heal(damage);
            giveHeal = false;
        }
        if (takeFallDamage)
        {
            FallDamage(fallSpeedTest, 1f);
            takeFallDamage = false;
        }
    }

    void Update()
    {
    }

    public void FallDamage(float fallSpeed, float normalY)
    {
        Damage((int)((max_hp * fallDamagePct) * Math.Pow(fallSpeed / fallDamageSpeedMin, fallDamageExp) * normalY));
        adio.PlayOneShot(fallDamagSound);
    }

    public void Respawn()
    {
        transform.position = spawnPoint.position;
        dead = false;
        anim.enabled = true;
        isRegenerating = true;
        Invoke("ReadyAttack", attackSpeed);
        Heal(max_hp);
        Debug.Log("Respawned");
        // if (healthbar != null)
        // {
        //     healthbar.SetHealth((int)hp);
        // }
        StopCoroutine(healthRegenCoroutine);
        healthRegenCoroutine = StartCoroutine(HealthRegen(healthRegenCooldown));
    }

    public void Damage(float damage)
    {
        if (invincible) return;
        hp -= damage;
        if (hp < 1)
        {
            float extraDamage = -hp;
            hp = 0;
            Die(extraDamage);
        }
        if (healthbar != null)
        {
            healthbar.SetHealth((int)hp);
        }
        StopCoroutine(healthRegenCoroutine);
        healthRegenCoroutine = StartCoroutine(HealthRegen(healthRegenDamageCooldown));
        if (hitParticle != null) hitParticle.Emit(1);
    }

    public void Heal(float heal)
    {
        hp += heal;
        if (hp > max_hp)
        {
            hp = max_hp;
        }
        if (healthbar != null)
        {
            healthbar.SetHealth((int)hp);
        }
    }

    public void OnCollisionEnter(Collision other)
    {
        Debug.Log(gameObject.name + " collided with " + other.gameObject.name);
        if (other.gameObject.layer == LayerMask.NameToLayer("Mobs"))
        {
            Debug.Log(gameObject.name + " is on good terms with " + other.gameObject.name);
            if (other.gameObject.tag != gameObject.tag)
            {
                Debug.Log(gameObject.name + " would like to go out with " + other.gameObject.name);
                Attack(other.gameObject.GetComponent<Mob>());
            }
        }
    }

    public void Attack(Mob mob)
    {
        Debug.Log(gameObject.name + " attacked " + mob.gameObject.name);
        if (attackReady)
        {
            // anim.SetTrigger("Attack");
            if (attackAnimations.Count > 0)
            {
                anim.SetLayerWeight(1, 1f); // Set layer 1 weight to 1 (fully active)
                anim.Play(attackAnimations[UnityEngine.Random.Range(0, attackAnimations.Count)], 1, 0f);
            }
            attackReady = false;
            Invoke("ReadyAttack", attackSpeed);
            mob.Damage(damage);
        }
    }

    public void SetMyLayerWeight(float weight)
    {
        if (anim != null)
        {
            anim.SetLayerWeight(1, weight);
        }
    }

    void ReadyAttack()
    {
        if (dead) attackReady = false;
        else attackReady = true;
    }

    void Die(float extraDamage)
    {
        if (anim != null)
        {
            anim.enabled = false;
            // anim.SetTrigger("Die");
        }
        dead = true;
        isRegenerating = false;
        attackReady = false;
        Debug.Log("Dead");
        StopCoroutine(healthRegenCoroutine);
        // healthRegenCoroutine = null;
        if (respawn) Invoke("Respawn", respawnTime);
        // StartCoroutine(DelayAction(respawnTime, Respawn));
    }

    public IEnumerator HealthRegen(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        while (isRegenerating && hp < max_hp)
        {
            yield return new WaitForSeconds(healthRegenCooldown);
            Heal(healthRegenAmount);
        }
    }

    public IEnumerator StaminaRegen(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        while (isRegeneratingStamina && stamina < maxStamina)
        {
            yield return new WaitForSeconds(staminaRegenCooldown);
            stamina += staminaRegenAmount;
            if (stamina > maxStamina)
            {
                stamina = maxStamina;
            }
        }
        isRegeneratingStamina = false;
    }

    private IEnumerator DelayAction(float delay, Action action)
    {
        Debug.Log("respawning in " + delay + " seconds");
        yield return new WaitForSeconds(delay);
        Debug.Log("called respawn");
        action?.Invoke();
    }
}
