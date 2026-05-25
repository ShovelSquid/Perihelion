using UnityEngine;
using System.Collections.Generic;

public class Palette : MonoBehaviour
{
    [System.Serializable]
    public struct ColorPair
    {
        public string name;
        public Color color;
        [ColorUsage(true, true)]
        public Color emissionColor;
    }
    public List<ColorPair> palette = new List<ColorPair>();

    public void ColorObject(Renderer r, string colorName)
    {
        ColorPair? pair = palette.Find(p => p.name == colorName);
        if (pair.HasValue)
        {
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            r.GetPropertyBlock(block);
            block.SetColor("_Color", pair.Value.color);
            block.SetColor("_EmissionColor", pair.Value.emissionColor);
            r.SetPropertyBlock(block);
        }
    }
}