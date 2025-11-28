using Unity.VisualScripting;
using UnityEngine;


public enum ResourceType
{
    Mana,
    Power,
    Fuel,
    Ammo
}

public abstract class Ability : MonoBehaviour
{
    [Header("Ability Settings")]
    public string abilityName;
    public bool isEnabled = true;
    public KeyCode activationKey = KeyCode.None; // For testing in editor

    public float cooldownTime;
    public bool isReady = true;

    public ResourceType resourceType;
    public float resourceCurrent;
    public float resourceCost;
    public float resourceRegenRate;
    public float resourceRegenRateMax;
    
    protected Mob mob;
    protected Move movement;
    protected Animator animator;
    
    protected virtual void Awake()
    {
        mob = GetComponent<Mob>();
        movement = GetComponent<Move>();
        animator = GetComponent<Animator>();
    }
    
    // Called when ability is activated
    public virtual void Activate()
    {
        if (!isEnabled) return;
        OnActivate();
    }
    
    // Called when ability is deactivated
    public virtual void Deactivate()
    {
        OnDeactivate();
    }

    public virtual void Cancel()
    {
        OnCancel();
    }
    
    // Override these in specific abilities
    protected abstract void OnActivate();
    protected abstract void OnDeactivate();
    protected abstract void OnCancel();
    
    // Optional: Called every frame while active
    public virtual void UpdateAbility() { }
    
    // Optional: For toggle abilities
    public virtual bool IsActive() { return false; }
}