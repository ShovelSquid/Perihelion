using UnityEngine;

public class CameraInFront : MonoBehaviour
{
    public CapsuleCollider punchCollider;
    public Transform baseCamera;
    public float frontOffset = 0.1f;
    public float upOffset = 0f;
    public LayerMask collisionMask = 0;
    public float lerpSpeed = 10f;

    void LateUpdate()
    {
        if (punchCollider == null || baseCamera == null) return;

        // Cache the current camera position so we can lerp from it at the end.
        Vector3 previous = transform.position;

        // Snap to baseCamera momentarily so the capsule query reflects the rest pose
        // (if the capsule is parented to this transform). Final position is lerped below.
        transform.position = baseCamera.position;

        // Capsule's length axis in world space.
        Vector3 axis;
        switch (punchCollider.direction)
        {
            case 0:  axis = punchCollider.transform.right;   break;
            case 2:  axis = punchCollider.transform.forward; break;
            default: axis = punchCollider.transform.up;      break; // 1 = Y
        }

        // Endpoints of the capsule's inner segment (between the hemispheres).
        Vector3 worldCenter = punchCollider.transform.TransformPoint(punchCollider.center);
        float halfDist = Mathf.Max(0f, punchCollider.height * 0.5f - punchCollider.radius);
        Vector3 topWorld = worldCenter + axis * halfDist;
        Vector3 botWorld = worldCenter - axis * halfDist;

        Vector3 lossy = punchCollider.transform.lossyScale;
        float worldRadius = punchCollider.radius * Mathf.Max(Mathf.Abs(lossy.x), Mathf.Abs(lossy.z));

        Collider[] hits = Physics.OverlapCapsule(topWorld, botWorld, worldRadius, collisionMask, QueryTriggerInteraction.Ignore);

        float highestLocalY = float.NegativeInfinity;
        Vector3 highestHitWorld = Vector3.zero;
        bool found = false;

        foreach (var c in hits)
        {
            if (c == punchCollider) continue;
            if (c.transform == transform) continue;

            Vector3 cp = c.ClosestPoint(worldCenter);
            Vector3 local = punchCollider.transform.InverseTransformPoint(cp);
            float yLocal = punchCollider.direction == 0 ? local.x
                         : punchCollider.direction == 2 ? local.z
                         : local.y;
            if (yLocal > highestLocalY)
            {
                highestLocalY = yLocal;
                highestHitWorld = cp;
                found = true;
            }
        }

        Vector3 target = found
            ? highestHitWorld + axis * frontOffset + Vector3.up * upOffset
            : baseCamera.position;

        transform.position = Vector3.Lerp(previous, target, lerpSpeed * Time.deltaTime);
    }
}
