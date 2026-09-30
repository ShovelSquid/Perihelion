// using UnityEngine;

// public class Health : MonoBehaviour
// {
//     public Healthbar healthbar;
//     public bool invincible = false;
//     public float hp;
//     public int max_hp;
//     public bool destroyed = false;
//     public virtual void Damage(float damage)
//     {
//         if (destroyed) return;
//         if (invincible) return;
//         hp -= damage;

//         HitEffect fx = GetComponent<HitEffect>();
//         if (fx != null) fx.Play();
//         if (hitParticle != null) hitParticle.Emit(1);
//         if (damageStates != null) damageStates.UpdateDamageState(hp / max_hp);

//         // int dState = GetDamageState();
//         // if (dState != damageState && damageStates != null)
//         // {
//         //     damageState = dState;
//         //     damageStates.UpdateDamageState(hp / max_hp);
//         // }

//         if (hp < 1)
//         {
//             float extraDamage = -hp;
//             hp = 0;
//             Die(extraDamage);
//         }

//         if (colorPalette != null && colorPalette.colorOnDamage) colorPalette.ColorObject(colorPalette.colorName, "Damage", 0.3f);
//         Invoke("ResetColor", damageflashDuration);
//         if (shine != null) shine.Shiney(damageflashDuration);
//         if (healthbar != null) healthbar.SetHealth((int)hp);
//     }

//     protected virtual void Die(float extraDamage = 0f)
//     {
//         if (rb != null)
//         {
//             rb.isKinematic = false;
//             rb.useGravity = true;
//         }
//         if (destroyedVersion != null)
//         {
//             GameObject e = Instantiate(destroyedVersion, transform.position, transform.rotation);
//             e.transform.localScale = transform.localScale;
//             Palette p = e.GetComponent<Palette>();
//             if (p != null && colorPalette != null)
//             {
//                 p.referencePalette = colorPalette.referencePalette;
//                 p.ColorObject(colorPalette.colorName, "Destroy", 0.5f);
//             }
//             spawnedDestroyed = e;
//         }
//         if (deathEffect != null)
//         {
//             Instantiate(deathEffect, transform.position, Quaternion.identity);
//         }
//         gameObject.SetActive(false); // don't destroy the building object, since we want to keep its collider and other components for the rubble. Just hide it.
//         // if (anim != null) anim.SetBool("Destroyed", true);
//         Invoke("End", endTime);
//         destroyed = true;
//     }
//     public void End()
//     {
//         if (healthbar != null) Destroy(healthbar.gameObject);
//         Destroy(gameObject);
//     }


//     public virtual void Heal(float heal)
//     {
//         if (destroyed) return;
//         hp += heal;
//         if (hp > max_hp) hp = max_hp;
//         if (healthbar != null) healthbar.SetHealth((int)hp);
//     }

// }
