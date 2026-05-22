using UnityEngine;

public class BillboardText : MonoBehaviour
{
    private Camera cam;
    public Transform target;
    private bool hasTarget = false;
    void Awake()
    {
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
        }
        transform.LookAt(transform.position + cam.transform.rotation * Vector3.forward, cam.transform.rotation * Vector3.up);
    }
}
