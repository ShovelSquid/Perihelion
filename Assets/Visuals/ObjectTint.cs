using UnityEngine;

public class ObjectTint : MonoBehaviour
{
    public bool ApplyToChildren = true;
    public Color baseColor = Color.white;
    [ColorUsage(true, true)] public Color emissionColor = Color.black;
    public string skipMaterialNameContains = "Glow";

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    void Awake()
    {
        var block = new MaterialPropertyBlock();
        var rends = ApplyToChildren
            ? GetComponentsInChildren<Renderer>()
            : new[] { GetComponent<Renderer>() };

        foreach (var rend in rends)
        {
            if (rend == null) continue;

            var mats = rend.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] == null) continue;
                if (!string.IsNullOrEmpty(skipMaterialNameContains)
                    && mats[i].name.Contains(skipMaterialNameContains)) continue;

                rend.GetPropertyBlock(block, i);
                block.SetColor(BaseColorId, baseColor);
                block.SetColor(EmissionColorId, emissionColor);
                rend.SetPropertyBlock(block, i);
            }
        }
    }
}
