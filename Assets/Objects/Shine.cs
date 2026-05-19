using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Shine : MonoBehaviour
{
    [Header("Glow Effect")]
    public float duration = 0.1f;
    public AnimationCurve shineCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);
    [ColorUsage(true, true)] public Color glowColor = Color.white;
    // public float glowIntensity = 2f;
    public Material glowMaterial;
    public bool applyToAllChildren = false;

    private Renderer[] rends;
    private Material[] glowInstances;
    private Coroutine ShineIt;

    void Awake()
    {
        if (glowMaterial == null) return;

        rends = applyToAllChildren
            ? GetComponentsInChildren<Renderer>()
            : new[] { GetComponentInChildren<Renderer>() };

        var instances = new List<Material>();
        foreach (var r in rends)
        {
            if (r == null) continue;
            var inst = new Material(glowMaterial);
            var mats = new List<Material>(r.sharedMaterials) { inst };
            r.materials = mats.ToArray();
            instances.Add(inst);
        }
        glowInstances = instances.ToArray();
        SetAlpha(0f);
    }

    public void Shiney()
    {
        if (glowInstances == null || glowInstances.Length == 0) return;
        if (ShineIt != null) StopCoroutine(ShineIt);
        ShineIt = StartCoroutine(ShineItUp());
    }

    private IEnumerator ShineItUp()
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            SetAlpha(shineCurve.Evaluate(elapsed / duration));
            yield return null;
        }
        SetAlpha(0f);
        ShineIt = null;
    }

    private void SetAlpha(float a)
    {
        Color c = glowColor;
        c.a = a;
        Color emission = glowColor * a;
        foreach (var inst in glowInstances)
        {
            inst.SetColor("_BaseColor", c);
            inst.SetColor("_EmissionColor", emission);
        }
    }
}
