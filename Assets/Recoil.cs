using UnityEngine;
using System.Collections;
public class Recoil : MonoBehaviour
{
    public Gun gun;
    public Rigidbody rb;

    public void AddRecoil(Vector3 recoilAmount, float recoilDuration)
    {
        // Apply recoil to the gun transform
        rb.AddForceAtPosition(recoilAmount, gun.firePoint.position, ForceMode.Impulse);
    }
}
