using UnityEngine;

// Canvases rendered into renderTexture keep a fixed CanvasScaler reference resolution,
// so they scale with the RT (and therefore the window) instead of staying a fixed pixel size.
[ExecuteAlways]
public class SetResolution : MonoBehaviour
{
    public RenderTexture renderTexture;
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

        foreach (var cam in Camera.allCameras)
        {
            if (cam.targetTexture == renderTexture)
            {
                cam.targetTexture = null;
                cam.targetTexture = renderTexture;
            }
        }
    }
}
