using System;
using UnityEngine;

[Serializable]
public class Charge
{
    public bool enabled = false;
    public float maxCharge = 1f;
    [Tooltip("Charge range (as fraction of maxCharge) that counts as a critical hit")]
    public Vector2 critRange = new Vector2(0f, 0f);

    [Header("Multipliers (evaluated at T = charge / maxCharge)")]
    public AnimationCurve cooldownMult = AnimationCurve.Linear(0f, 1f, 1f, 1f);
    public AnimationCurve damageMult   = AnimationCurve.Linear(0f, 1f, 1f, 1f);
    public AnimationCurve speedMult    = AnimationCurve.Linear(0f, 1f, 1f, 1f);

    protected HitIndicator hitIndicator;

    public float charge { get; private set; }
    public bool charging { get; private set; }

    public float T => maxCharge > 0f ? charge / maxCharge : 0f;
    public bool IsFull => charge >= maxCharge;
    public bool IsCrit => T >= critRange.x && T <= critRange.y;

    public event Action<float> OnBegin;
    public event Action OnCancel;
    public bool setCritOnStart = true;

    public void Start()
    {
        if (setCritOnStart)
        {
            hitIndicator.SetCritRange(critRange.x, critRange.y);
        }
    }

    public void Begin()
    {
        if (charging) return;
        charging = true;
        charge = 0f;
        OnBegin?.Invoke(maxCharge);
    }

    public void Tick(float dt)
    {
        if (!charging) return;
        charge = Mathf.Min(charge + dt, maxCharge);
    }

    public void Cancel()
    {
        charging = false;
        charge = 0f;
        OnCancel?.Invoke();
    }

    private void Update()
    {
        Tick(Time.deltaTime);
    }
}
