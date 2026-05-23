using UnityEngine;

public class TextureApply : MonoBehaviour
{
    public Texture2D texture;
    public Material material;

    public void ApplyTexture(Transform obj)
    {
        Renderer renderer = obj.GetComponent<Renderer>();
        if (renderer != null && texture != null)
        {
            renderer.sharedMaterial.mainTexture = texture;
        }
        foreach (Transform child in obj)
        {
            ApplyTexture(child);
        }
    }

    [ContextMenu("Apply Texture")]
    public void ApplyAllTextures()
    {
        foreach (Transform child in transform)
        {
            ApplyTexture(child);
        }
    }

    public void ApplyMaterial(Transform obj)
    {
        Renderer renderer = obj.GetComponent<Renderer>();
        if (renderer != null && material != null)
        {
            renderer.sharedMaterial = material;
        }
        foreach (Transform child in obj)
        {
            ApplyMaterial(child);
        }
    }

    [ContextMenu("Apply Material")]
    public void ApplyAllMaterials()
    {
        foreach (Transform child in transform)
        {
            ApplyMaterial(child);
        }
    }
}