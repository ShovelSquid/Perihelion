using UnityEngine;
using UnityEngine.InputSystem;

public class Gun : MonoBehaviour
{
    public Animator anim;
    public Transform firePoint;
    public GameObject projectilePrefab;
    public float fireRate = 0.5f;
    public bool automatic = true;
    public bool chambering = false;
    private float nextFireTime = 0f;
    public bool triggerOn = false;


    public void OnShoot(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            triggerOn = true;
        }
        else if (context.canceled)
        {
            triggerOn = false;
        }
    }

    void Update()
    {
        if (triggerOn && Time.time >= nextFireTime)
        {
            Shoot();
        }
    }

    void Shoot()
    {
        Instantiate(projectilePrefab, firePoint.position, firePoint.rotation);
        chambering = true;
        if (anim != null) 
        {
            anim.SetTrigger("Shoot");
        }
        nextFireTime = Time.time + 1f / fireRate;
    }
}