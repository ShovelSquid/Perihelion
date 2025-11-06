using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Mathematics;

public class Healthbar : MonoBehaviour
{
    public Image healthbar;
    public Image flashbar;
    public TextMeshProUGUI hptext;
    public int max_hp;
    public int hp;
    public bool lerping;
    public float lerpSpeed;
    public Color healColor;
    public Color damageColor;
    // Update is called once per frame
    void Update()
    {
        if (lerping)
        {
            float val = Mathf.Lerp(flashbar.rectTransform.localScale.x, 0f, lerpSpeed * Time.deltaTime);
            flashbar.rectTransform.localScale = new Vector3(val, 1, 1);
            if (math.abs(val) <= 0.0001f)
            {
                lerping = false;
                flashbar.rectTransform.localScale = new Vector3(0, 1, 1);
            }
        }
    }

    public void SetMaxHealth(int max_hp)
    {
        this.max_hp = max_hp;
        hp = max_hp;
        healthbar.rectTransform.localScale = new Vector3(1, 1, 1);
        flashbar.rectTransform.localScale = new Vector3(0, 1, 1);
        flashbar.rectTransform.localPosition = new Vector3(100f, 0, 0);
        hptext.text = hp + " / " + max_hp;
        lerping = false;
    }

    public void SetHealth(int newHp)
    {
        float OTo1 = (float)newHp / (float)max_hp;
        Debug.Log("OTo1: " + OTo1);
        Vector3 oldPosition = flashbar.rectTransform.localPosition;
        Vector3 newPosition = new Vector3(OTo1 * 100f, 0, 0);
        float diff = (newPosition.x - oldPosition.x)/100f;
        healthbar.rectTransform.localScale = new Vector3((float)newHp / (float)max_hp, 1, 1);
        flashbar.rectTransform.localPosition = newPosition;
        flashbar.rectTransform.localScale = new Vector3(-diff, 1, 1);
        if (diff > 0)
        {
            flashbar.color = healColor;
        }
        else
        {
            flashbar.color = damageColor;
        }
        hptext.text = newHp + " / " + max_hp;
        lerping = true;
        hp = newHp;
    }
}
