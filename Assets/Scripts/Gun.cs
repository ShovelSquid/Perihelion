using UnityEngine;
// using Unity.Mathematics;

public class Gun : Item
{
    protected BulletManager bulletManager;
    public GameObject projectilePrefab;
    public Transform firePoint;
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
    public Charge charge = new Charge();

    [Header("Recoil Info")]
    public Vector2 recoilPattern;
    public float recoilLerpSpeed;

    [Header("Effects")]
    public ParticleSystem muzzleFlash;
    public AudioSource gunshotSound;
    public AudioSource reloadSound;
    public AudioSource emptyClickSound;

    protected bool cooldownPending;

    void Start()
    {
        charge.OnBegin += OnChargeBegin;
    }

    protected override void Awake()
    {
        base.Awake();
        bulletManager = FindObjectOfType<BulletManager>();
    }

    private void OnChargeBegin(float max)
    {
        if (hitIndicator != null) hitIndicator.StartCharge(max);
        if (triggerSound != null) triggerSound.Play();
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
        if (charge.enabled)
        {
            triggerHeld = isPressed;
            bool wasEmpty = isPressed && !CanShoot();
            if (isPressed)
            {
                if (CanCharge()) charge.Begin();
            }
            else
            {
                if (charge.charging && CanShoot()) DoTrigger();
            }
            if (wasEmpty && !cooldownPending) ChamberRound(true);
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
        if (hitIndicator != null) hitIndicator.SetAmmo(bulletChambered, magazineSize + bulletChambered);
        cooldownPending = true;
        if (anim != null) anim.SetTrigger("Reload");
        Invoke("Reload", reloadTime);
    }

    public void Reload()
    {
        if (reloadSound != null) reloadSound.Play();
        int neededAmmo = magazineSize - ammoInMagazine;
        int ammoToLoad = Mathf.Min(neededAmmo, totalAmmo);
        ammoInMagazine += ammoToLoad;
        totalAmmo -= ammoToLoad;
        if (hitIndicator != null) hitIndicator.SetAmmo(ammoInMagazine + bulletChambered, magazineSize + 1);
        ChamberRound(true);
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
            // if (!aim) Aim(true);
            bool ch = charge.enabled;
            float t = charge.T;
            float effectiveDamage = ch ? damage * charge.damageMult.Evaluate(t) : damage;
            if (ch && charge.IsCrit)
            {
                effectiveDamage = damage * critMult;
            }
            float effectiveCooldown = ch ? fireCooldownTime * charge.cooldownMult.Evaluate(t) : fireCooldownTime;
            float effectiveProjectileSpeed = ch ? projectileSpeed * charge.speedMult.Evaluate(t) : projectileSpeed;
            if (hitIndicator != null) hitIndicator.Pulse(effectiveCooldown);
            if (hitIndicator != null) hitIndicator.SetAmmo(ammoInMagazine + bulletChambered - 0.15f, magazineSize + 1);
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
                    Vector3 shotDirection = spreadRotation * firePoint.forward;
                    // create bullet
                    Projectile p = bulletManager.Get(projectilePrefab);
                    p.Proj.position = firePoint.position;
                    p.speed = effectiveProjectileSpeed;
                    p.direction = firePoint.forward;
                    p.damage = effectiveDamage;
                    p.Fire(shotDirection);
                }
            }
            charge.Cancel();
            if (anim != null) anim.SetTrigger("Shoot");
        }
    }

    public override void Update()
    {
        base.Update();
        if (charge.enabled && equipped && triggerHeld)
        {
            // if (!charge.charging && CanCharge()) charge.Begin();
            // charge.Tick(Time.deltaTime);
            if (automatic && charge.charging && charge.IsFull && CanShoot())
            {
                DoTrigger();
            }
        }
        else if (automatic && equipped && triggerHeld && CanShoot())
        {
            DoTrigger();
        }
    }
    public void ChamberRound() => ChamberRound(false);

    public override void Equip(bool equip)
    {
        base.Equip(equip);
        if (!equip)
        {
            cooldownPending = false;
            return;
        }
        if (equip)
        {
            if (hitIndicator != null) hitIndicator.SetAmmo(ammoInMagazine + bulletChambered, magazineSize + 1);
        }
    }

    public void ChamberRound(bool anim8 = false)
    {
        cooldownPending = false;
        if (ammoInMagazine > 0 && bulletChambered == 0)
        {
            ammoInMagazine--;
            bulletChambered++;
            if (anim8 &&anim != null) anim.SetTrigger("Chamber");
            if (hitIndicator != null) hitIndicator.SetAmmo(ammoInMagazine + bulletChambered, magazineSize + 1);
        }
        else if (ammoInMagazine == 0)
        {
            if (emptyClickSound != null) emptyClickSound.Play();
        }
    }

    public bool CanCharge()
    {
        return charge.enabled && !cooldownPending && (CanShoot() || ammoInMagazine > 0);
    }
}