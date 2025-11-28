// To add abilities to a mob at runtime:
using UnityEngine;
public class PowerupPickup : MonoBehaviour
{
    public enum PowerupType { Sprint, ChargeJump, Dash }
    public PowerupType type;
    
    void OnTriggerEnter(Collider other)
    {
        AbilityManager abilityManager = other.GetComponent<AbilityManager>();
        if (abilityManager != null)
        {
            switch (type)
            {
                case PowerupType.Sprint:
                    Sprint sprint = other.gameObject.AddComponent<Sprint>();
                    sprint.isEnabled = true;
                    break;
                case PowerupType.ChargeJump:
                    ChargeJump chargeJump = other.gameObject.AddComponent<ChargeJump>();
                    chargeJump.isEnabled = true;
                    break;
                case PowerupType.Dash:
                    Dash dash = other.gameObject.AddComponent<Dash>();
                    dash.isEnabled = true;
                    break;
            }
            
            Destroy(gameObject);
        }
    }
}