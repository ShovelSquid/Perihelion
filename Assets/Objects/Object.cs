using UnityEngine;

public class Object : MonoBehaviour
{
    public Animator anim;
    public Rigidbody rb;
    public bool invincible = false;
    public float hp;
    public int max_hp;
    public float mass = 1f;
    public float density = 1f;
    public bool destroyed = false;

    protected virtual void Start()
    {
        if (rb != null)
        {
            rb.mass = mass;
        }
    }

    public virtual void Damage(float damage)
    {
        if (invincible) return;
        hp -= damage;
        if (hp < 1)
        {
            float extraDamage = -hp;
            hp = 0;
            Die();
        }
    }

    public virtual void Heal(float heal)
    {
        hp += heal;
        if (hp > max_hp)
        {
            hp = max_hp;
        }
}
    protected virtual void Die()
    {
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
        }

        if (anim != null)
        {
            anim.SetBool("Destroyed", true);
            // anim.SetTrigger("Die");
        }
        destroyed = true;
    }

}
