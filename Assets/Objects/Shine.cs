using System.Collections;
using UnityEngine;

public class Shine : MonoBehaviour
{
    [Header("Glow Effect")]
    public float duration = 0.5f;
    public AnimationCurve shineCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);
    public Color glowColor = Color.white;
    public float glowIntensity = 2f;
    public Material glowMaterial; // Assign in Inspector, or leave null for auto-create
    
    private Coroutine ShineIt;


    public void Shiney()
    {
        if (ShineIt != null)
        {
            StopCoroutine(ShineIt);
        }
        ShineIt = StartCoroutine(ShineItUp());
    }

    private IEnumerator ShineItUp()
    {
        yield break;
    }

}
