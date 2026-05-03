using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;

[RequireComponent(typeof(CanvasGroup))]
public class DialogueUI : MonoBehaviour
{
    public TextMeshProUGUI actorText;
    public TextMeshProUGUI bodyText;
    public Button nextButton;

    public float autoAdvanceSeconds = 0f; // 0 = wait for nextButton
    public bool isOpen { get; private set; }

    private Coroutine _displayRoutine;

    void Awake()
    {
        // if (nextButton != null) nextButton.onClick.AddListener(OnNextPressed);
    }

    public void Show(string actor, string[] lines)
    {
        gameObject.SetActive(true);
        isOpen = true;
        actorText.text = actor ?? "";
        if (_displayRoutine != null) StopCoroutine(_displayRoutine);
        _displayRoutine = StartCoroutine(DisplayLines(lines));
    }

    IEnumerator DisplayLines(string[] lines)
    {
        for (int i = 0; i < lines.Length; i++)
        {
            bodyText.text = lines[i];
            if (autoAdvanceSeconds > 0f)
            {
                yield return new WaitForSeconds(autoAdvanceSeconds);
            }
            else
            {
                bool pressed = false;
                void local() => pressed = true;
                if (nextButton != null) nextButton.onClick.AddListener(local);
                while (!pressed) yield return null;
                if (nextButton != null) nextButton.onClick.RemoveListener(local);
            }
        }
        Close();
    }

    // void OnNextPressed() { /* button wired for manual advance handled in coroutine */ }

    public void Close()
    {
        isOpen = false;
        gameObject.SetActive(false);
    }

    public void StartDialogue()
    {
        // Placeholder for any setup needed when dialogue starts
    }

    public void EndDialogue()
    {
        Close();
    }
}