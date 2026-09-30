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
    public Vector2 spreadAngle; // degrees (x pitch, y yaw) of pellet pattern; only used when one shot fires more than one projectile. The hand's bloom and sway now carry aim inaccuracy
    public Vector2 spreadNoise; // degrees (x pitch, y yaw) of small per-projectile jitter for texture; applied to every projectile
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

    [Header("Spread")]
    public float baseSpread = 0.5f; // degrees; the resting bloom radius, which is the cursor's idle size
    public float maxSpread = 6f; // degrees; bloom cap under sustained fire
    public float bloomPerShot = 1.5f; // degrees added to the holding hand's bloom per shot
    public float bloomRecovery = 4f; // per second; exponential pull back toward baseSpread; framerate independent
    [Range(0f, 1f)] public float swayAmount = 0.75f; // fraction of the current bloom radius the sway can wander; 0 = no sway
    public float restRerollInterval = 0.12f; // seconds between new aim targets while firing; give HandRig's offset spring time to arrive (~0.1 s at 6 Hz), 0 = every shot
    // Floor bloom sources: each is rate x amount, capped on its own, then added to baseSpread (and the total capped by maxSpread).
    public float moveBloom = 0.3f; // degrees per m/s of the holder's horizontal speed
    public float maxMoveBloom = 2f; // degrees; most that moving can add
    public float airBloom = 0.3f; // degrees per m/s of the holder's vertical speed (jumping, falling)
    public float maxAirBloom = 3f; // degrees; most that being airborne can add
    public float lookBloom = 0.01f; // degrees per deg/s of look turn rate
    public float maxLookBloom = 2f; // degrees; most that looking can add
    [Range(0f, 1f)] public float lookDrag = 0.8f; // fraction of the bloom radius the dot trails behind a full-speed turn (HandRig.lookDragCurve shapes it)

    [Header("Recoil Info")]
    public Vector3 recoilOffset; // muzzle-local tilt added to the straight-back kickback direction and the loose-gun impulse; every serialized value is zero today
    public float recoilForce; // overall strength; scales the visual kickback and flip (and the loose-gun impulse)
    public float recoilLerpSpeed; // currently unused; kept for its serialized data
    public float kickbackDistance = 0.01f; // meters of visual slide back along the barrel per unit of recoilForce; never moves the shots
    public float flipAngle = 1.5f; // degrees per unit of recoilForce of visual-only muzzle flip along the gun's own up; never moves the shots
    public float flipSideAngle = 0.3f; // degrees per unit of recoilForce of visual-only sideways flip, along the direction the aim offset is already drifting
    // Shape of the visual kick's return, 0..1 in time over recoilReturnTime, 1 = full kick, 0 = home.
    // Default holds the kick for 40% of the time, then eases home.
    public AnimationCurve recoilReturn = new AnimationCurve(
        new Keyframe(0f, 1f, 0f, 0f),
        new Keyframe(0.4f, 1f, 0f, 0f),
        new Keyframe(1f, 0f, 0f, 0f));
    public float recoilReturnTime = 1f; // seconds from a shot to fully home; each shot restarts it

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

    public override Transform Muzzle
    {
        get { return firePoint != null ? firePoint : transform; }
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

    public void AddRecoil()
    {
        Transform m = Muzzle;
        Vector3 kickDir = (Vector3.back + recoilOffset).normalized;
        // Held: the gun is kinematic, so kick the hand's aim offset and visual kick instead of the body.
        Vector3 kickback = kickDir * (recoilForce * kickbackDistance);
        if (holder != null && holder.hands != null && holder.hands.Kick(this, kickback, bloomPerShot, flipAngle * recoilForce, flipSideAngle * recoilForce)) return;
        // Loose dynamic gun: keep the physics impulse (same as the old formula with a zero recoilOffset).
        // A kinematic unheld gun gets no recoil.
        if (rb != null && !rb.isKinematic)
        {
            rb.AddForceAtPosition((m.rotation * kickDir) * recoilForce, m.position, ForceMode.Impulse);
        }
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
            AddRecoil();
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
                // Bullets leave along the aimed muzzle recorded by the hand (before its visual kickback and flip),
                // so the gun can be thrown around on screen while shots still go where the cursor dot is.
                // A gun that isn't held falls back to its real muzzle.
                Transform muzzle = Muzzle;
                Vector3 shotOrigin = muzzle.position;
                Quaternion shotRot = muzzle.rotation;
                if (holder != null && holder.hands != null && holder.hands.TryGetAimPose(this, out Vector3 aimPos, out Quaternion aimRot))
                {
                    shotOrigin = aimPos;
                    shotRot = aimRot;
                }
                for (int i = 0; i < actualShotCount; i++)
                {
                    // xy spread baesd off of x and y of spreadAngle
                    Vector2 angleOffset = new Vector2(
                        Random.Range(-spreadNoise.x, spreadNoise.x),
                        Random.Range(-spreadNoise.y, spreadNoise.y)
                    );
                    // spreadAngle is only the pellet pattern of multi-projectile shots; single shots get just the small jitter.
                    if (actualShotCount > 1)
                    {
                        angleOffset += new Vector2(
                            Random.Range(-spreadAngle.x, spreadAngle.x),
                            Random.Range(-spreadAngle.y, spreadAngle.y)
                        );
                    }
                    // Jitter in the muzzle's own frame, so the spread axes stay put as the gun turns.
                    Vector3 shotDirection = shotRot * (Quaternion.Euler(angleOffset.x, angleOffset.y, 0f) * Vector3.forward);
                    // create bullet
                    Projectile p = bulletManager.Get(projectilePrefab);
                    p.Proj.position = shotOrigin;
                    p.speed = effectiveProjectileSpeed;
                    p.direction = shotRot * Vector3.forward;
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
        Transform m = Muzzle;
        Debug.DrawRay(m.position, m.forward * 655f, Color.red);
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