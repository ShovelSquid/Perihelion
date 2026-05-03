using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Playables;
using Newtonsoft.Json.Linq;
using TMPro;

public class Dialogue : MonoBehaviour
{
    public DialogueUI ui;
    public Canvas worldUi;
    public GameObject barkTextPrefab;
    private GameObject bark;
    public Transform barkTextSpawn;
    public UnityEvent onDialogueStarted;
    public UnityEvent onDialogueEnded;
    public Collider colder;
    public string actor;
    public string text;
    [Header("Text Data")]
    public TextAsset dialogueJSON;
    private JObject json;
    [Tooltip("Time in seconds the bark text shows on screen")]
    public float barkTime = 3f;
    [Tooltip("Time in seconds before the bark text can be shown again")]
    public float barkCooldown = 0.2f;
    public bool barkOnCooldown = false;
    [Header("Cinematic")]
    public PlayableDirector timeline;

    void Awake()
    {
        if (dialogueJSON != null)
        {
            json = JObject.Parse(dialogueJSON.text);
        }
    }

    public void EnterDialogueRange()
    {
        if (dialogueJSON != null)
        {
            if (barkOnCooldown) return;
            if (json != null)
            {
                bark = Instantiate(barkTextPrefab, worldUi.transform);
                bark.transform.position = barkTextSpawn.position;
                var textComp = bark.GetComponent<TextMeshProUGUI>();
                text = (string)json["enter_barks"]["1"]["lines"][1];
                textComp.text = text;
                barkOnCooldown = true;
                Invoke("DestroyBark", barkTime);
            }
            else
            {
                text = "No bark found";
            }
        }
    }

    public void ExitDialogueRange()
    {
        text = "fine be that way.";
    }

    public void StartDialogue()
    {
        onDialogueStarted?.Invoke();
        ui.StartDialogue();
    }

    public void EndDialogue()
    {
        onDialogueEnded?.Invoke();
        ui.EndDialogue();
    }

    private void DestroyBark()
    {
        if (bark != null)
        {
            Destroy(bark);
            Invoke("ResetBarkCooldown", barkCooldown);
        }
    }

    private void ResetBarkCooldown() {
        barkOnCooldown = false;
    }
}
