using UnityEngine;
// using Unity.Mathematics;

public class Gun : Item
{
    public BulletManager bulletManager;
    public GameObject projectilePrefab;
    public float damage;
    public float projectileSpeed;
    public Vector2 shotCount;
    public Vector2 spreadAngle;
    public Vector2 spreadNoise;
    public float critMult;
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
    public AnimationCurve speedMult;            // affects projectile speed based on charge level


    [Header("Recoil Info")]
    public Vector2 recoilPattern;
    public float recoilLerpSpeed;

    [Header("Effects")]
    public ParticleSystem muzzleFlash;
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
        if (MustReload() && CanReload()) StartReload();
    }

    public bool OutOfAmmo()
    {
        return ammoInMagazine <= 0 && totalAmmo <= 0 && bulletChambered <= 0;
    }

    public bool MustReload()
    {
        return ammoInMagazine <= 0 && totalAmmo > 0 && bulletChambered <= 0;
    }

    public bool CanReload()
    {
        if (cooldownPending) return false;
        if (ammoInMagazine == magazineSize || totalAmmo <= 0) return false;
        return true;
    }

    public void StartReload()
    {
        if (cooldownPending) return;
        if (!CanReload()) return;
        if (reloadSound != null) reloadSound.Play();
        if (hitIndicator != null) hitIndicator.Pulse(reloadTime);
        cooldownPending = true;
        Invoke("Reload", reloadTime);
    }

    public void Reload()
    {
        if (reloadSound != null) reloadSound.Play();
        int neededAmmo = magazineSize - ammoInMagazine;
        int ammoToLoad = Mathf.Min(neededAmmo, totalAmmo);
        ammoInMagazine += ammoToLoad;
        totalAmmo -= ammoToLoad;
        ChamberRound();
    }

    public void ForceChamber()
    {
        if (ammoInMagazine > 0 && bulletChambered == 0)
        {
            ammoInMagazine--;
            bulletChambered++;
        }
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
            float effectiveProjectileSpeed = chargeable ? projectileSpeed * speedMult.Evaluate(charge/maxCharge) : projectileSpeed;
            if (hitIndicator != null) hitIndicator.Pulse(effectiveCooldown);
            Invoke("ChamberRound", effectiveCooldown);
            if (gunshotSound != null) gunshotSound.Play();
            if (muzzleFlash != null) muzzleFlash.Play();
            if (bulletManager != null && projectilePrefab != null)
            {
                int actualShotCount = 0;
                if (shotCount == Vector2.zero) actualShotCount = 1;
                else actualShotCount = Random.Range((int)shotCount.x, (int)shotCount.y + 1);
                for (int i = 0; i < actualShotCount; i++)
                {
                    // xy spread baesd off of x and y of spreadAngle
                    Vector2 angleOffset = new Vector2(
                        Random.Range(-spreadAngle.x, spreadAngle.x),
                        Random.Range(-spreadAngle.y, spreadAngle.y)
                    );
                    angleOffset += new Vector2(
                        Random.Range(-spreadNoise.x, spreadNoise.x),
                        Random.Range(-spreadNoise.y, spreadNoise.y)
                    );
                    Quaternion spreadRotation = Quaternion.Euler(angleOffset.x, angleOffset.y, 0f);
                    Vector3 shotDirection = spreadRotation * transform.forward;
                    // create bullet
                    Projectile p = bulletManager.Get(projectilePrefab);
                    p.Proj.position = transform.position;
                    p.speed = effectiveProjectileSpeed;
                    p.direction = transform.forward;
                    p.damage = effectiveDamage;
                    p.Fire(shotDirection);
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
            if (charging) charge = Mathf.Min(charge + Time.deltaTime, maxCharge);
            if (automatic && charging && charge >= maxCharge && CanShoot())
            {
                DoTrigger();
                charging = false;
                charge = 0f;
            }
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
        if (charging) return;
        charging = true;
        charge = 0f;
        if (hitIndicator != null) hitIndicator.StartCharge(maxCharge);
        if (triggerSound != null) triggerSound.Play();
    }
}