using UnityEngine;

public class Turret : MonoBehaviour
{
    public float yawRotationSpeed = 5f;
    public Vector2 maxYaw;
    public bool yawClamp = false;
    public float pitchRotationSpeed = 5f;
    public Vector2 maxPitch;
    public bool pitchClamp = false;
    public Transform pivotVector;
    public GameObject body;
    public Transform lookVector;
    public Transform lookPoint;
    public bool inheritLookPoint = false;
    private float lookPointLerpSpeed = 50f;

    public float RotateYaw(float yawInput)
    {
        float yawAmount = yawInput * yawRotationSpeed * Time.deltaTime;
        pivotVector.Rotate(0f, yawAmount, 0f, Space.World);
        return yawAmount;
    }
    public float RotatePitch(float pitchInput)
    {
        float pitchAmount = pitchInput * pitchRotationSpeed * Time.deltaTime;
        pivotVector.Rotate(-pitchAmount, 0f, 0f, Space.Self);
        return pitchAmount;
    }



    void Update()
    {
        if (!inheritLookPoint)
        {            
            // Debug draw the look vector
            Debug.DrawRay(lookVector.position, lookVector.forward * 5f, Color.red);
            Vector3 direction = (lookVector.position + lookVector.forward * 50000f - pivotVector.position).normalized;
            lookPoint.position = Vector3.Lerp(lookPoint.position, lookVector.position + lookVector.forward * 50000f, lookPointLerpSpeed * Time.deltaTime);
            RaycastHit hit;
            if (Physics.SphereCast(lookVector.position, 4f, lookVector.forward, out hit, 50000f))
            {
                if (hit.collider != null)
                {
                    Debug.DrawRay(pivotVector.position, direction * hit.distance, Color.green);
                    lookPoint.position = hit.point;
                }
            }
        }

        // Calculate direction to lookPoint
        Vector3 directionToLookPoint = (lookPoint.position - pivotVector.position).normalized;
        Debug.DrawRay(pivotVector.position, directionToLookPoint * 500f, Color.blue);
        
        // Calculate yaw (horizontal rotation around world Y-axis)
        Vector3 directionFlat = new Vector3(directionToLookPoint.x, 0, directionToLookPoint.z).normalized;
        float targetYaw = Mathf.Atan2(directionFlat.x, directionFlat.z) * Mathf.Rad2Deg;
        float currentYaw = pivotVector.eulerAngles.y;
        float newYaw = Mathf.MoveTowardsAngle(currentYaw, targetYaw, yawRotationSpeed * Time.deltaTime);
        
        // Calculate pitch (vertical rotation)
        float horizontalDistance = new Vector2(directionToLookPoint.x, directionToLookPoint.z).magnitude;
        float targetPitch = -Mathf.Atan2(directionToLookPoint.y, horizontalDistance) * Mathf.Rad2Deg;
        float currentPitch = pivotVector.eulerAngles.x;
        if (currentPitch > 180) currentPitch -= 360; // Normalize to -180 to 180
        float newPitch = Mathf.MoveTowardsAngle(currentPitch, targetPitch, pitchRotationSpeed * Time.deltaTime);
        
        // Apply rotation
        pivotVector.rotation = Quaternion.Euler(newPitch, newYaw, 0);
        body.transform.rotation = Quaternion.Euler(pivotVector.eulerAngles.x, pivotVector.eulerAngles.y, 0f);

    }
}
