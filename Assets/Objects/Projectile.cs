using UnityEngine;
using System.Collections.Generic;
using Unity.Mathematics;

public class Projectile : MonoBehaviour
{
    [Header("Base Settings")]
    public float damage = 10f;
    private float baseDamage;
    public float mass = 1f;
    [Range(0f, 1f)]
    public float penetration = 1f;
    [Header("Projectile Settings")]
    private float baseSpeed;
    public float speed = 100f;
    public float minSpeed = 50f;
    public float lifeTime = 5f;
    public float collisionRadius = 0.5f;
    public LayerMask collisionLayers;
    
    [Header("Path Visualization")]
    public bool showPath = true;
    public int pathResolution = 50; // Number of points to check along path
    public Color pathColor = Color.yellow;
    public float trailWidth = 0.1f;
    public float trailTime = 0.5f;
    public Gradient trailGradient = new Gradient();
    public Material trailMaterial;
    
    [Header("References")]
    public Rigidbody rb;
    
    private Vector3 previousPosition;
    private List<Vector3> pathPoints = new List<Vector3>();

    void Start()
    {
        baseSpeed = speed;
        rb.linearVelocity = transform.forward * speed;
        previousPosition = transform.position;
        baseDamage = damage;
        Destroy(gameObject, lifeTime);
        rb.mass = mass;
        
        // Setup TrailRenderer (easier option)
        if (showPath)
        {
            // Setup default gradient if not configured
            // if (trailGradient.colorKeys.Length == 0)
            // {
            //     trailGradient.SetKeys(
            //         new GradientColorKey[] { 
            //             new GradientColorKey(Color.white, 0.0f), 
            //             new GradientColorKey(Color.yellow, 0.5f),
            //             new GradientColorKey(Color.red, 1.0f) 
            //         },
            //         new GradientAlphaKey[] { 
            //             new GradientAlphaKey(1.0f, 0.0f), 
            //             new GradientAlphaKey(0.5f, 0.5f),
            //             new GradientAlphaKey(0.0f, 1.0f) 
            //         }
            //     );
            // }
            TrailRenderer trail = gameObject.AddComponent<TrailRenderer>();
            trail.time = trailTime; // Trail lasts for the lifetime of projectile
            trail.startWidth = trailWidth;
            trail.endWidth = trailWidth * 0.5f;
            trail.colorGradient = trailGradient;
            
            // Set material
            if (trailMaterial != null)
            {
                trail.material = trailMaterial;
            }
            else
            {
                trail.material = new Material(Shader.Find("Sprites/Default"));
            }
            
            trail.autodestruct = false; // Keep trail when projectile is destroyed
        }
    }

    void FixedUpdate()
    {
        // Check for collisions along the path using SphereCast
        Vector3 currentPosition = transform.position;
        Vector3 direction = (currentPosition - previousPosition).normalized;
        float distance = Vector3.Distance(previousPosition, currentPosition);
        
        if (distance > 0.001f)
        {
            RaycastHit hit;
            if (Physics.SphereCast(previousPosition, collisionRadius, direction, out hit, distance, collisionLayers))
            {
                // Hit something!
                OnProjectileHit(hit);
                Destroy(gameObject);
                return;
            }
        }
        
        // Store path for visualization
        if (showPath)
        {
            pathPoints.Add(currentPosition);
        }
        
        previousPosition = currentPosition;
    }

    void OnProjectileHit(RaycastHit hit)
    {
        Debug.Log($"Projectile hit {hit.collider.name} at {hit.point}");
        
        // Apply damage, spawn effects, etc.
        Object obj = hit.collider.GetComponent<Object>();
        if (obj != null)
        {
            obj.Damage(damage);
            speed -= math.abs(1 - penetration) * obj.density;
        }
        damage = baseDamage * speed/baseSpeed;
        if (speed < minSpeed)
        {
            Destroy(gameObject);
            return;
        }
        
        // Optional: Spawn impact effect at hit point
        // Instantiate(impactEffect, hit.point, Quaternion.LookRotation(hit.normal));
    }

    void OnDrawGizmos()
    {
        if (!showPath || pathPoints.Count < 2) return;
        
        Gizmos.color = pathColor;
        
        // Draw spheres along the path
        foreach (Vector3 point in pathPoints)
        {
            Gizmos.DrawWireSphere(point, collisionRadius);
        }
        
        // Draw lines connecting path points
        for (int i = 0; i < pathPoints.Count - 1; i++)
        {
            Gizmos.DrawLine(pathPoints[i], pathPoints[i + 1]);
        }
        
        // Draw current collision sphere
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, collisionRadius);
    }

    // Predict and visualize the entire path before firing
    public static List<Vector3> PredictPath(Vector3 startPosition, Vector3 velocity, float timeStep, int steps, float gravity = -9.81f)
    {
        List<Vector3> pathPoints = new List<Vector3>();
        Vector3 currentPos = startPosition;
        Vector3 currentVel = velocity;
        
        for (int i = 0; i < steps; i++)
        {
            pathPoints.Add(currentPos);
            
            // Apply gravity
            currentVel.y += gravity * timeStep;
            
            // Update position
            currentPos += currentVel * timeStep;
        }
        
        return pathPoints;
    }

    // Calculate ballistic trajectory to hit a target
    public static Vector3 CalculateBallisticVelocity(Vector3 startPos, Vector3 targetPos, float speed, float gravity = -9.81f)
    {
        Vector3 direction = targetPos - startPos;
        float horizontalDist = new Vector3(direction.x, 0, direction.z).magnitude;
        float verticalDist = direction.y;
        
        // Calculate launch angle
        float speedSquared = speed * speed;
        float discriminant = speedSquared * speedSquared - gravity * (gravity * horizontalDist * horizontalDist + 2 * verticalDist * speedSquared);
        
        if (discriminant < 0)
        {
            // Can't reach target with given speed, aim directly at it
            return direction.normalized * speed;
        }
        
        float angle = Mathf.Atan((speedSquared - Mathf.Sqrt(discriminant)) / (gravity * horizontalDist));
        
        // Calculate velocity components
        Vector3 horizontalDir = new Vector3(direction.x, 0, direction.z).normalized;
        float horizontalSpeed = Mathf.Sqrt(speedSquared / (1 + Mathf.Tan(angle) * Mathf.Tan(angle)));
        float verticalSpeed = horizontalSpeed * Mathf.Tan(angle);
        
        return horizontalDir * horizontalSpeed + Vector3.up * verticalSpeed;
    }
}