using UnityEngine;

public class Highlight : MonoBehaviour
{
    public Outline outline;

    public void HighlightCharacter(bool highlight)
    {
        if (highlight)
        {
            outline.enabled = true;
        }
        else
        {
            outline.enabled = false;
        }
    }
}
