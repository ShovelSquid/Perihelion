using UnityEngine;
using System.Collections.Generic;

public class Building : Object
{
    public List<float> damageThresholds = new List<float>(); // from 1 to 0, in descending order. When hp drops below these percentages, the building's damage state changes (handled by DamageStates.cs)
    private DamageStates damageStates;
    public int damageState = 0; // num correlating to num of list on damage states script for current texture
    public GameObject destroyedVersion; // optional prefab to spawn when building is destroyed (e.g. rubble)

    void Awake()
    {
        damageStates = GetComponent<DamageStates>();
        if (damageStates == null)
        {
            Debug.LogError("Building " + name + " is missing a DamageStates component.");
        }
    }

    public override void Damage(float damage)
    {
        base.Damage(damage);
        int dState = GetDamageState();
        if (dState != damageState && damageStates != null)
        {
            damageState = dState;
            damageStates.UpdateDamageState(damageState);
        }
    }

    protected override void Die(float extraDamage = 0f)
    {
        base.Die(extraDamage);
        if (destroyedVersion != null)
        {
            GameObject e = Instantiate(destroyedVersion, transform.position, transform.rotation);
            e.transform.localScale = transform.localScale;
        }
        gameObject.SetActive(false); // don't destroy the building object, since we want to keep its collider and other components for the rubble. Just hide it.
        // Destroy(gameObject);
    }
 
    public int GetDamageState()
    {
        if (damageThresholds.Count == 0) return 0;
        int state = 0;
        float healthPct = hp / max_hp;
        // Thresholds are 0..1, descending (e.g. [0.75, 0.5, 0.25]).
        // Walk from the lowest threshold (largest index) upward; first match wins.
        for (int i = damageThresholds.Count - 1; i >= 0; i--)
        {
            if (healthPct <= damageThresholds[i])
            {
                state = i;
                break;
            }
        }
        return state;
    }
}
