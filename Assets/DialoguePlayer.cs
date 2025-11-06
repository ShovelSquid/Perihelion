using UnityEngine;
using UnityEngine.AI;

public class DialoguePlayer : MonoBehaviour
{
    public Dialogue dialogue;
    public Highlight highlight;
    public GameObject actor;
    public bool on;
    void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.layer == LayerMask.NameToLayer("Events"))
        {
            if (other.gameObject.tag == "Dialogue")
            {
                actor = other.gameObject;
                dialogue = actor.GetComponent<Dialogue>();
                highlight = actor.GetComponent<Highlight>();
                highlight.HighlightCharacter(true);
                dialogue.EnterDialogueRange();
            }
        }
    }
    void OnTriggerExit(Collider other)
    {
        if (other.gameObject.layer == LayerMask.NameToLayer("Events"))
        {
            if (other.gameObject.tag == "Dialogue")
            {
                highlight.HighlightCharacter(false);
                dialogue.ExitDialogueRange();
                dialogue = null;
                highlight = null;
                actor = null;
            }
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        if (!on) return;
        if (dialogue == null) return;
        if (collision.gameObject == dialogue.colder.gameObject)
        {
            dialogue.StartDialogue();
            // Debug.Log("INITIATE THE FUCKING DIALOGUE MOTHER OF FUCKERS");
        }
    }
}
