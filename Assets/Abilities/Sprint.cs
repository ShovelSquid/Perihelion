// Sprint Ability
using UnityEngine;

public class Sprint : Ability
{
    [Header("Sprint Settings")]
    public float sprintSpeedMultiplier = 1.5f;
    public float staminaCost = 1f;
    public float staminaRegenDelay = 2f;
    
    private bool isSprinting = false;
    private float originalSpeed;
    
    protected override void Awake()
    {
        base.Awake();
        if (movement != null)
        {
            originalSpeed = movement.maxGroundSpeed;
        }
    }
    
    protected override void OnActivate()
    {
        if (mob != null && mob.stamina <= 0) return;
        
        isSprinting = true;
        
        if (movement != null)
        {
            movement.maxGroundSpeed = originalSpeed * sprintSpeedMultiplier;
        }
        
        Debug.Log("Sprint activated!");
    }
    
    protected override void OnDeactivate()
    {
        isSprinting = false;
        
        if (movement != null)
        {
            movement.maxGroundSpeed = originalSpeed;
        }
        
        Debug.Log("Sprint deactivated!");
    }

    protected override void OnCancel()
    {
        OnDeactivate();
        // Called manually when dash ends
    }
    
    public override void UpdateAbility()
    {
        if (isSprinting && mob != null)
        {
            // Drain stamina while sprinting
            mob.stamina -= staminaCost * Time.deltaTime;
            if (mob.stamina <= 0)
            {
                Deactivate();
            }
        }
    }
    
    public override bool IsActive()
    {
        return isSprinting;
    }
}