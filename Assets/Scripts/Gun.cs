using UnityEngine;
using Unity.Mathematics;

public class Gun : Item
{
    public float damage;
    public float critMult;
    public float range;
    public bool automatic;
    public int bulletChambered;
    public float fireCooldownTime;
    public float reloadTime;
    public int magazineSize;
    public int ammoInMagazine;
    public int totalAmmo;
    [Header("Charge Info")]
    public HitIndicator hitIndicator;
    public bool chargeable;
    public float charge;        // measured in t
    public float maxCharge;     // measured in t
    public Vector2 critRange;   // charge range (as % of maxCharge) that guarantees critical hit
    public AnimationCurve cooldownGraphMult;    // affects fire cooldown based on charge level
    public AnimationCurve damageMult;           // affects damage based on charge level

    [Header("Recoil Info")]
    public Vector2 recoilPattern;
    public float recoilLerpSpeed;

    [Header("Effects")]
    public ParticleSystem muzzleFlash;
    public ParticleSystem hitEffect;
    public AudioSource gunshotSound;
    public AudioSource reloadSound;
    public AudioSource emptyClickSound;

    private bool cooldownPending;
    private bool charging;

    void Start()
    {
        if (hitIndicator != null)
        {
            hitIndicator.SetCritRange(critRange.x, critRange.y);
        }
    }

    public bool CanShoot()
    {
        if (!base.CanTrigger())
        {
            return false;
        }
        return bulletChambered > 0;
    }

    public override void SlapTrigger(bool isPressed)
    {
        if (chargeable)
        {
            triggerHeld = isPressed;
            bool wasEmpty = isPressed && !CanShoot();
            if (isPressed)
            {
                if (CanCharge()) BeginCharge();
            }
            else
            {
                if (charging && CanShoot()) DoTrigger();
                charging = false;
                charge = 0f;
            }
            if (wasEmpty && !cooldownPending) ChamberRound();
            return;
        }

        // Snapshot empty-state BEFORE base, since base may fire and empty the chamber.
        bool wasEmptyNC = isPressed && !CanShoot();
        base.SlapTrigger(isPressed);
        if (!isPressed) return;
        // Only manually chamber if the gun was actually empty at press-time AND there's no
        // pending cooldown — otherwise the scheduled Invoke will chamber for us, and chambering
        // here would bypass the fire cooldown.
        if (wasEmptyNC && !cooldownPending) ChamberRound();
    }

    public override void DoTrigger()
    {
        base.DoTrigger();
        if (CanShoot())
        {
            bulletChambered--;
            cooldownPending = true;
            float effectiveDamage = chargeable ? damage * damageMult.Evaluate(charge/maxCharge) : damage;
            if (chargeable && charge/maxCharge >= critRange.x && charge/maxCharge <= critRange.y)
            {
                effectiveDamage = damage * critMult;
            }
            float effectiveCooldown = chargeable ? fireCooldownTime * cooldownGraphMult.Evaluate(charge/maxCharge) : fireCooldownTime;
            if (hitIndicator != null) hitIndicator.Pulse(effectiveCooldown);
            Invoke("ChamberRound", effectiveCooldown);
            if (gunshotSound != null) gunshotSound.Play();
            if (muzzleFlash != null) muzzleFlash.Play();
            Ray ray = new Ray(transform.position, transform.forward);
            if (Physics.Raycast(ray, out RaycastHit hit, range, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                Object obj = hit.collider.GetComponentInParent<Object>();
                if (obj != null)
                {
                    obj.Damage(effectiveDamage);
                    obj.HitPhysics(hit.point, hit.normal, effectiveDamage);
                }

                if (hitEffect != null)
                {
                    ParticleSystem effect = Instantiate(hitEffect, hit.point, Quaternion.LookRotation(hit.normal));
                    // Destroy(effect.gameObject, effect.main.duration);
                }
            }
        }
    }

    public override void Update()
    {
        base.Update();
        if (chargeable && held && triggerHeld)
        {
            if (!charging && CanCharge()) BeginCharge();
            if (charging) charge = math.min(charge + Time.deltaTime, maxCharge);
        }
        else if (automatic && held && triggerHeld && CanShoot())
        {
            DoTrigger();
        }
    }

    public void ChamberRound()
    {
        cooldownPending = false;
        if (ammoInMagazine > 0 && bulletChambered == 0)
        {
            ammoInMagazine--;
            bulletChambered++;
        }
        else if (ammoInMagazine == 0)
        {
            if (emptyClickSound != null) emptyClickSound.Play();
        }
    }

    public bool CanCharge()
    {
        return chargeable && !cooldownPending && (CanShoot() || ammoInMagazine > 0);
    }

    private void BeginCharge()
    {
        charging = true;
        charge = 0f;
        if (hitIndicator != null) hitIndicator.StartCharge(maxCharge);
        if (triggerSound != null) triggerSound.Play();
    }
}