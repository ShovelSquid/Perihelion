// Dash Ability
using UnityEngine;

public class Dash : Ability
{
    [Header("Dash Settings")]
    public float dashForce = 20f;
    public float dashDuration = 0.2f;
    public float dashCooldown = 1f;
    public LayerMask dashThroughLayers; // Layers to ignore during dash
    
    private bool isDashing = false;
    private float dashEndTime;
    private float lastDashTime;
    private Vector3 dashDirection;
    
    protected override void OnActivate()
    {
        if (Time.time < lastDashTime + dashCooldown) return;
        if (movement == null) return;
        
        isDashing = true;
        dashEndTime = Time.time + dashDuration;
        lastDashTime = Time.time;
        
        // Get dash direction from player input or facing direction
        dashDirection = movement.moveDirection != Vector2.zero 
            ? new Vector3(movement.moveDirection.x, 0, movement.moveDirection.y).normalized 
            : transform.forward;
        
        // Apply dash force
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = dashDirection * dashForce;
        }
        
        // Optional: Make invincible during dash
        if (mob != null)
        {
            mob.invincible = true;
        }
        
        Debug.Log("Dash!");
    }
    
    protected override void OnDeactivate()
    {
        // Called manually when dash ends
    }
    protected override void OnCancel()
    {
        // Called manually when dash ends
    }

    
    public override void UpdateAbility()
    {
        if (isDashing && Time.time >= dashEndTime)
        {
            isDashing = false;
            
            if (mob != null)
            {
                mob.invincible = false;
            }
            
            Debug.Log("Dash ended");
        }
    }
    
    public override bool IsActive()
    {
        return isDashing;
    }
}