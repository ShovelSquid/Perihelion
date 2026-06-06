using UnityEngine;

public class BillboardText : MonoBehaviour
{
    private Camera cam;
    public Transform target;
    private bool hasTarget = false;
    public bool ScreenOverlay = false;
    void Awake()
    {
        if (cam == null)
            cam = Camera.main;
        if (target != null)
        {
            hasTarget = true;
        }
    }
    void LateUpdate()
    {
        if (hasTarget)
        {
            transform.position = target.position;
            if (ScreenOverlay)
            {
                transform.position = cam.WorldToScreenPoint(target.position);
            }
        }
        if (!ScreenOverlay)
        {
            transform.LookAt(transform.position + cam.transform.rotation * Vector3.forward, cam.transform.rotation * Vector3.up);
        }
    }
}
