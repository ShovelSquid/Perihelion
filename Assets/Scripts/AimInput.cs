using UnityEngine;

public class AimInput : MonoBehaviour
{
    public Transform aimPoint;
    public float distance = 1000f;
    public bool useAimPoint = true;
    void Update()
    {
        Ray ray = new Ray(transform.position, transform.forward);
        aimPoint.position = ray.GetPoint(distance);
        if (!useAimPoint) return;
        if (Physics.Raycast(ray, out RaycastHit hit, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            aimPoint.position = hit.point;
        }
    }
}
