using UnityEngine;
using System.Collections.Generic;
using UnityEngine.InputSystem;

public class AbilityManager : MonoBehaviour
{
    [Header("References")]
    public Mob mob;
    public Move movement;
    
    [Header("Abilities")]
    public List<Ability> abilities = new List<Ability>();
    
    // Cached abilities for quick access
    private Sprint sprint;
    private ChargeJump chargeJump;
    private Dash dash;
    
    void Awake()
    {
        // Find all abilities on this GameObject
        abilities.AddRange(GetComponents<Ability>());
        
        // Cache specific abilities
        sprint = GetComponent<Sprint>();
        chargeJump = GetComponent<ChargeJump>();
        dash = GetComponent<Dash>();
    }
    
    void Update()
    {
        // Update all active abilities
        foreach (Ability ability in abilities)
        {
            if (ability.IsActive())
            {
                ability.UpdateAbility();
            }
        }
    }
    
    // Called from Input System
    public void OnSprint(InputAction.CallbackContext context)
    {
        if (sprint == null || !sprint.isEnabled) return;
        
        if (context.performed)
        {
            sprint.Activate();
        }
        else if (context.canceled)
        {
            sprint.Deactivate();
        }
    }
    
    public void OnChargeJump(InputAction.CallbackContext context)
    {
        if (chargeJump == null || !chargeJump.isEnabled) return;

        // Check if held long enough for charge jump
        float holdDuration = (float)context.duration;
        
        if (holdDuration >= chargeJump.minChargeTime) // Add this field to ChargeJump
        {
            // Execute charged jump
            chargeJump.Activate();
        }
        
        if (context.performed)
        {
            chargeJump.Activate();
        }
        else if (context.canceled)
        {
            chargeJump.Deactivate();
        }
    }
    
    public void OnDash(InputAction.CallbackContext context)
    {
        if (dash == null || !dash.isEnabled) return;
        
        if (context.performed)
        {
            dash.Activate();
        }
    }
    
    // Public methods to enable/disable abilities
    public void EnableAbility<T>() where T : Ability
    {
        T ability = GetComponent<T>();
        if (ability != null) ability.isEnabled = true;
    }
    
    public void DisableAbility<T>() where T : Ability
    {
        T ability = GetComponent<T>();
        if (ability != null) ability.isEnabled = false;
    }
}