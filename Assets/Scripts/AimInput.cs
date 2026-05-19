using UnityEngine;

public class AimInput : MonoBehaviour
{
    public Transform aimPoint;
    void Update()
    {
        Ray ray = new Ray(transform.position, transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, 1000f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            aimPoint.position = hit.point;
        }
        else
        {
            aimPoint.position = ray.GetPoint(1000f);
        }
    }
}
