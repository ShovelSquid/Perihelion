using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Mathematics;

public class Healthbar : MonoBehaviour
{
    public bool worldSpace;
    private Canvas worldUI;
    public Image healthbar;
    public Image flashbar;
    public TextMeshProUGUI hptext;
    public int max_hp;
    public int hp;
    public bool lerping;
    public float lerpSpeed;
    public float lerpDelay;
    [ColorUsage(true, true)] public Color healColor;
    [ColorUsage(true, true)] public Color damageColor;
    public string colorProperty = "_BaseColor"; // shader graph reference name
    private float lerpStartTime;
    private Material flashMatInstance;
    private int colorPropertyId;
    private bool flashIsDamage;


    void Awake()
    {
        worldUI = GameObject.Find("WorldUI").GetComponent<Canvas>();
        colorPropertyId = Shader.PropertyToID(colorProperty);
        if (flashbar != null && flashbar.material != null)
        {
            flashMatInstance = new Material(flashbar.material);
            flashbar.material = flashMatInstance;
        }
    }
    void Start()
    {
        if (worldSpace && worldUI != null && transform.parent != worldUI.transform)
        {
            transform.parent = worldUI.transform;
        }
    }

    void Update()
    {
        if (lerping && Time.time >= lerpStartTime)
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
        if (hptext != null)
        {
            hptext.text = hp + " / " + max_hp;
        }
        lerping = false;
    }

    public void SetHealth(int newHp)
    {
        bool isDamage = newHp < hp;
        bool isHeal = newHp > hp;
        if (!isDamage && !isHeal) return;

        float newPctPos = (float)newHp / max_hp * 100f;
        float oldPctPos = (float)hp / max_hp * 100f;

        // Current outer edge of the flash (the edge furthest from the anchor).
        // For damage: scale is positive, outer edge = anchor + scale*100 (rightmost).
        // For heal:   scale is negative, outer edge = anchor + scale*100 (leftmost).
        float currentAnchor = flashbar.rectTransform.localPosition.x;
        float currentScale  = flashbar.rectTransform.localScale.x;
        float currentOuter  = currentAnchor + currentScale * 100f;

        if (isDamage)
        {
            float spanRight = oldPctPos;
            if (lerping && flashIsDamage)
            {
                spanRight = Mathf.Max(spanRight, currentOuter);
            }
            flashbar.rectTransform.localPosition = new Vector3(newPctPos, 0, 0);
            flashbar.rectTransform.localScale = new Vector3((spanRight - newPctPos) / 100f, 1, 1);
            if (flashMatInstance != null) flashMatInstance.SetColor(colorPropertyId, damageColor);
            flashIsDamage = true;
        }
        else
        {
            float spanLeft = oldPctPos;
            if (lerping && !flashIsDamage)
            {
                spanLeft = Mathf.Min(spanLeft, currentOuter);
            }
            flashbar.rectTransform.localPosition = new Vector3(newPctPos, 0, 0);
            flashbar.rectTransform.localScale = new Vector3((spanLeft - newPctPos) / 100f, 1, 1);
            if (flashMatInstance != null) flashMatInstance.SetColor(colorPropertyId, healColor);
            flashIsDamage = false;
        }

        healthbar.rectTransform.localScale = new Vector3((float)newHp / max_hp, 1, 1);
        if (hptext != null)
        {
            hptext.text = newHp + " / " + max_hp;
        }
        lerping = true;
        lerpStartTime = Time.time + lerpDelay;
        hp = newHp;
    }
}
