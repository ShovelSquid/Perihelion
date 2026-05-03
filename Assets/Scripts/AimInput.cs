using UnityEngine;

public class AimInput : MonoBehaviour
{
    public Transform aimPoint;
    void Update()
    {
        Ray ray = new Ray(transform.position, transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            aimPoint.position = hit.point;
        }
        else
        {
            aimPoint.position = ray.GetPoint(100f);
        }
    }
}
