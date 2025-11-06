using UnityEngine;

public class BillboardText : MonoBehaviour
{
    void LateUpdate()
    {
        transform.forward = Camera.main.transform.forward;
    }
}
