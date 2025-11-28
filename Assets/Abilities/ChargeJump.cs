// Charge Jump Ability
using UnityEngine;

public class ChargeJump : Ability
{
    [Header("Charge Jump Settings")]
    public float maxChargeTime = 2f;
    public float jumpForceMult = 1.25f;
    public AnimationCurve chargeCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    
    public bool isCharging = false;
    private float chargeStartTime;
    public float minChargeTime = 0.25f;
    private float originalJumpForce;
    private float originalSpeed;
    public float chargeSpeedMultiplier = 0.5f; // Movement speed multiplier while charging
    
    protected override void Awake()
    {
        base.Awake();
        if (movement != null)
        {
            originalJumpForce = movement.jumpForceGround;
        }
    }
    
    protected override void OnActivate()
    {
        if (movement != null && movement.inAir) return;
        
        isCharging = true;
        chargeStartTime = Time.time;
        if (movement != null)
        {
            originalSpeed = movement.maxGroundSpeed;
            movement.maxGroundSpeed = originalSpeed * chargeSpeedMultiplier; // Disable movement while charging
        }
        Debug.Log("Charging jump...");
    }
    
    protected override void OnDeactivate()
    {
        if (!isCharging) return;
        
        float chargeTime = Time.time - chargeStartTime;
        float chargePercent = Mathf.Clamp01(chargeTime / maxChargeTime);
        float chargedForce = originalJumpForce * Mathf.Lerp(1f, jumpForceMult, chargeCurve.Evaluate(chargePercent));
        
        if (movement != null)
        {
            movement.jumpForceGround = chargedForce;
            movement.Jump(); // Trigger the jump
            movement.jumpForceGround = originalJumpForce; // Reset
            movement.maxGroundSpeed = originalSpeed; // Restore original speed
        }
        
        isCharging = false;
        Debug.Log($"Released jump with {chargePercent * 100}% charge!");
    }

    protected override void OnCancel()
    {
        if (isCharging)
        {
            isCharging = false;
            if (movement != null)
            {
                movement.maxGroundSpeed = originalSpeed; // Restore original speed
            }
            Debug.Log("Charge jump canceled.");
        }
    }
    
    public override void UpdateAbility()
    {
        if (isCharging)
        {
            float chargeTime = Time.time - chargeStartTime;
            float chargePercent = Mathf.Clamp01(chargeTime / maxChargeTime);
            
            // Optional: Visual feedback (particle effects, animation, etc.)
            // animator?.SetFloat("ChargeAmount", chargePercent);
        }
    }
    
    public override bool IsActive()
    {
        return isCharging;
    }
}