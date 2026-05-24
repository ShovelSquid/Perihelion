using UnityEngine;
using System;
using System.Collections;
using Unity.Mathematics;
using System.Collections.Generic;

[RequireComponent(typeof(Inventory))]
public class Mob : Object
{
    public Inventory inv;
    public Item heldItem;
    public AudioSource adio;
    public Collider box;
    public AudioClip fallDamagSound;
    public ParticleSystem directionalHitParticle;
    public ParticleSystem airJumpParticle;
    public ParticleSystem groundJumpParticle;
    public Transform jumpFXPoint;
    public Transform spawnPoint;
    public bool dead = false;
    public bool respawn = false;
    public int xp;
    public int xp_base;
    public int level;
    public int reference_number;
    public int hp_base;
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
    public float projectileAbsorption = 0f;
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
        box = GetComponent<Collider>();
        isRegenerating = true;
    }

    protected override void Start()
    {
        base.Start();
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

    public virtual void Respawn()
    {
        transform.position = spawnPoint.position;
        dead = false;
        anim.enabled = true;
        isRegenerating = true;
        Invoke("ReadyAttack", attackSpeed);
        Heal(max_hp);
        Debug.Log("Respawned");
        if (healthRegenCoroutine != null) StopCoroutine(healthRegenCoroutine);
        healthRegenCoroutine = StartCoroutine(HealthRegen(healthRegenCooldown));
    }

    public override void Damage(float damage)
    {
        if (invincible) return;
        base.Damage(damage);
        if (healthRegenCoroutine != null) StopCoroutine(healthRegenCoroutine);
        healthRegenCoroutine = StartCoroutine(HealthRegen(healthRegenDamageCooldown));
    }

    public void DoHitEffect(Vector3 hitPoint, Vector3 hitNormal, float hitForce)
    {
        if (directionalHitParticle != null)
        {
            var d = Instantiate(directionalHitParticle, hitPoint, Quaternion.LookRotation(hitNormal));
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

    public void PickupItem(Item item)
    {
        if (heldItem == null)
        {
            heldItem = item;
            Debug.Log(gameObject.name + " picked up " + item.gameObject.name);
            item.GotPickedUp();
        }
        else if (inv != null)
        {
            Item i = inv.AddItem(item);
            if (i == null)
            {
                Debug.Log(gameObject.name + " picked up " + item.gameObject.name);
                item.GotPickedUp();
            }
            else if (i == item)
            {
                Debug.Log(gameObject.name + " couldn't pick up " + item.gameObject.name + " at all");
            }
            else
            {
                Instantiate(i.gameObject, item.transform.position + item.transform.forward, item.transform.rotation);
                item.GotPickedUp();
                Debug.Log(gameObject.name + " picked up some of " + item.gameObject.name);
            }
        }

    }

    public virtual void Attack(Mob mob)
    {
        Debug.Log(gameObject.name + " attacked " + mob.gameObject.name);
        if (attackReady)
        {
            if (attackAnimations.Count > 0)
            {
                anim.SetLayerWeight(1, 1f);
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

    protected override void Die(float extraDamage = 0f)
    {
        base.Die(extraDamage);
        if (anim != null) anim.enabled = false;
        dead = true;
        isRegenerating = false;
        attackReady = false;
        Debug.Log("Dead");
        if (healthRegenCoroutine != null) StopCoroutine(healthRegenCoroutine);
        if (respawn) Invoke("Respawn", respawnTime);
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
