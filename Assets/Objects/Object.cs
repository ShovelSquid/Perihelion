using UnityEngine;

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

    void Awake()
    {
        if (body == null) body = GetComponent<Inventory>();
        if (rb == null) rb = GetComponent<Rigidbody>();
        if (anim == null) anim = GetComponent<Animator>();
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
        if (invincible) return;
        hp -= damage;

        HitEffect fx = GetComponent<HitEffect>();
        if (fx != null) fx.Play();
        if (hitParticle != null) hitParticle.Emit(1);

        if (hp < 1)
        {
            float extraDamage = -hp;
            hp = 0;
            Die(extraDamage);
        }

        if (healthbar != null) healthbar.SetHealth((int)hp);
    }

    public virtual void Heal(float heal)
    {
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
        if (anim != null) anim.SetBool("Destroyed", true);
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
