using UnityEngine;

[RequireComponent(typeof(Shake))]
[RequireComponent(typeof(Shine))]
public class HitEffect : MonoBehaviour
{
    private Shake shake;
    private Shine shine;
    public AnimationCurve rampIntensity = AnimationCurve.EaseInOut(0, 0, 1, 1);
    public float intensity = 0f; // on a scale of 0 to 1, parent health percent

    private void Awake()
    {
        shake = GetComponent<Shake>();
        shine = GetComponent<Shine>();
    }
    public void Play()
    {
        shake.Shakey();
        shine.Shiney();
    }
}
