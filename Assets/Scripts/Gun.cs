using UnityEngine;
using Unity.Mathematics;

public class Gun : Item
{
    public float damage;
    public float range;
    public bool automatic;
    public int bulletChambered;
    public float fireCooldownTime;
    public float reloadTime;
    public int magazineSize;
    public int ammoInMagazine;
    public int totalAmmo;

    [Header("Recoil Info")]
    public Vector2 recoilPattern;
    public float recoilLerpSpeed;

    [Header("Effects")]
    public ParticleSystem muzzleFlash;
    public ParticleSystem hitEffect;
    public AudioSource gunshotSound;
    public AudioSource reloadSound;
    public AudioSource emptyClickSound;


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
        base.SlapTrigger(isPressed);
        if (!isPressed)
        {
            return;
        }
        if (CanShoot())
        {
            DoTrigger();
        }
        else
        {
            ChamberRound();
        }

    }

    public override void DoTrigger()
    {
        base.DoTrigger();
        if (CanShoot())
        {
            bulletChambered--;
            Invoke("ChamberRound", fireCooldownTime);
            if (gunshotSound != null) gunshotSound.Play();
            if (muzzleFlash != null) muzzleFlash.Play();
            Ray ray = new Ray(transform.position, transform.forward);
            if (Physics.Raycast(ray, out RaycastHit hit, range, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                Object obj = hit.collider.GetComponentInParent<Object>();
                if (obj != null)
                {
                    obj.Damage(damage);
                    obj.HitPhysics(hit.point, hit.normal, damage);
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
        if (automatic && held && triggerHeld && CanShoot())
        {
            DoTrigger();
        }
    }

    public void ChamberRound()
    {
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
}