using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;

public class Palette : MonoBehaviour
{
    MaterialPropertyBlock block;
    public Palette referencePalette;
    public List<Renderer> objectsToColor = new List<Renderer>();
    public bool allRenderersInChildren;
    public string colorName;
    public bool colorHealthbar;
    public bool colorOutline;
    public bool colorOnStart = true;
    [System.Serializable]
    public struct ColorPair
    {
        public string name;
        public Color color;
        [ColorUsage(true, true)]
        public Color emissionColor;
        public Color hpColor;
    }
    public List<ColorPair> palette = new List<ColorPair>();

    void Awake()
    {
        block = new MaterialPropertyBlock();
    }

    void Start()
    {
        if (!colorOnStart) return;
        ColorObject(colorName);
    }

    public void ColorObject(string colorName, string colorName2 = null, float t = 0f, bool emission = false)
    {
        if (colorName == "") return;
        if (!referencePalette.palette.Exists(p => p.name == colorName)) return;
        if (allRenderersInChildren)
        {
            foreach (Renderer r in GetComponentsInChildren<Renderer>())
            {
                if (colorName2 != null)
                {
                    CombineColors(r, colorName, colorName2, t);
                }
                else
                {
                    SingleColor(r, colorName);
                }
            }
        }
        else
        {
            foreach (Renderer r in objectsToColor)
            {
                if (colorName2 != null)
                {
                    CombineColors(r, colorName, colorName2, t);
                }
                else
                {
                    SingleColor(r, colorName);
                }
            }
        }
        if (colorHealthbar)
        {
            ColorHealthbar(colorName);
        }
        if (colorOutline)
        {
            ColorOutline(colorName);
        }
    }

    public void SingleColor(Renderer r, string colorName)
    {
        if (colorName == "") return;
        if (!referencePalette.palette.Exists(p => p.name == colorName)) return;
        ColorPair? pair = referencePalette.palette.Find(p => p.name == colorName);
        if (pair.HasValue)
        {
            if (block == null) block = new MaterialPropertyBlock();
            r.GetPropertyBlock(block);
            block.SetColor("_Color", pair.Value.color);
            block.SetColor("_BaseColor", pair.Value.color);
            block.SetColor("_EmissionColor", pair.Value.emissionColor);
            r.SetPropertyBlock(block);
        }
    }

    public void CombineColors(Renderer r, string color1, string color2, float t)
    {
        if (color1 == "" || color2 == "") return;
        if (!referencePalette.palette.Exists(p => p.name == color1) || !referencePalette.palette.Exists(p => p.name == color2)) return;
        ColorPair? pair1 = referencePalette.palette.Find(p => p.name == color1);
        ColorPair? pair2 = referencePalette.palette.Find(p => p.name == color2);
        if (pair1.HasValue && pair2.HasValue)
        {
            if (block == null) block = new MaterialPropertyBlock();
            r.GetPropertyBlock(block);
            Color blendedColor = Color.Lerp(pair1.Value.color, pair2.Value.color, t);
            Color blendedEmissionColor = Color.Lerp(pair1.Value.emissionColor, pair2.Value.emissionColor, t);
            block.SetColor("_Color", blendedColor);
            block.SetColor("_BaseColor", blendedColor);
            block.SetColor("_EmissionColor", blendedEmissionColor);
            r.SetPropertyBlock(block);
        }        
    }

    public void ColorHealthbar(string colorName)
    {
        Healthbar hp = GetComponentInChildren<Healthbar>();
        ColorPair? pair = referencePalette.palette.Find(p => p.name == colorName);
        if (pair.HasValue && hp != null)
        {
            if (pair.Value.hpColor.a == 0f) return; // if hpColor is fully transparent, don't change color of healthbar
            hp.healthbar.color = pair.Value.hpColor;
        }
    }

    public void ColorOutline(string colorName)
    {
        Outline o = GetComponentInChildren<Outline>();
        ColorPair? pair = referencePalette.palette.Find(p => p.name == colorName);
        if (pair.HasValue && o != null)
        {
            if (pair.Value.color.a == 0f) return; // if color is fully transparent, don't change color of outline
            o.OutlineColor = pair.Value.hpColor;
        }
    }
}