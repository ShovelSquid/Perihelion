using UnityEngine;
using UnityEngine.InputSystem;

public class Reticle : MonoBehaviour
{
    private bool menuGuySelected = false;
    void Update()
    {
        Ray ray = Camera.main.ScreenPointToRay(new Vector3(Mouse.current.position.x.ReadValue(), Mouse.current.position.y.ReadValue(), 0));
        Debug.DrawRay(ray.origin, ray.direction * 1000, Color.red);
        RaycastHit[] hits = Physics.RaycastAll(ray);
        bool menuGuyHit = false;
        foreach (var hit in hits)
        {
            if (hit.transform.gameObject.CompareTag("MenuGuy"))
            {
                menuGuyHit = true;
                break;
            }
        }
        if (menuGuyHit && !menuGuySelected)
        {
            foreach (GameObject obj in GameObject.FindGameObjectsWithTag("MenuGuy"))
            {
                obj.GetComponent<Outline>().enabled = true;
                menuGuySelected = true;
            }
        }
        else if (!menuGuyHit && menuGuySelected)
        {
            foreach (GameObject obj in GameObject.FindGameObjectsWithTag("MenuGuy"))
            {
                obj.GetComponent<Outline>().enabled = false;
                menuGuySelected = false;
            }
        }

    }
}
