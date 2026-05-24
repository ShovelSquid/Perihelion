using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

[ExecuteAlways]
public class SetResolution : MonoBehaviour
{
    public RenderTexture renderTexture;
    public List<CanvasScaler> targetCanvases = new List<CanvasScaler>();
    [Min(1)] public int downscale = 1;

    int lastWidth;
    int lastHeight;

    void OnEnable()
    {
        Resize();
    }

    void Update()
    {
        if (Screen.width != lastWidth || Screen.height != lastHeight)
        {
            Resize();
        }
    }

    public void Resize()
    {
        if (renderTexture == null) return;

        int w = Mathf.Max(1, Screen.width / downscale);
        int h = Mathf.Max(1, Screen.height / downscale);

        if (renderTexture.width == w && renderTexture.height == h)
        {
            lastWidth = Screen.width;
            lastHeight = Screen.height;
            return;
        }

        if (renderTexture.IsCreated()) renderTexture.Release();
        renderTexture.width = w;
        renderTexture.height = h;
        renderTexture.Create();

        lastWidth = Screen.width;
        lastHeight = Screen.height;

        foreach (var canvas in targetCanvases)
        {
            if (canvas != null)
            {
                canvas.referencePixelsPerUnit = 100f;
                canvas.referenceResolution = new Vector2(w, h);
            }
        }
    }
}
