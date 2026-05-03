using UnityEngine;
using UnityEngine.Events;

public class BulletUITrigger : MonoBehaviour
{
    public Animator anim;

    public void Trigger()
    {
        anim.SetTrigger("Shot");
    }
}
