using UnityEngine;

public class SpicyObject : Object
{
    public Shake shake;
    public float minShake = 0.1f;
    public float maxShake = 5f;
    public AnimationCurve shakeCurve = AnimationCurve.EaseInOut(0, 1, 1, 1);
    public Shine shine;

    public override void Damage(float damage)
    {
        base.Damage(damage);
        float hppct = hp / max_hp;;
        
        // Map the curve value to shake magnitude
        // As HP decreases (hpPercent -> 0), we want more shake
        // So we invert: use (1 - hpPercent) or evaluate curve inversely
        shake.magnitude = Mathf.Lerp(minShake, maxShake, shakeCurve.Evaluate(1f - hppct));
        if (shake != null)
        {
            shake.Shakey();
        }
        if (shine != null)
        {
            shine.Shiney();
        }
    }

    public static float Map(float value, float fromMin, float fromMax, float toMin, float toMax)
    {
        return (value - fromMin) / (fromMax - fromMin) * (toMax - toMin) + toMin;
    }
}

