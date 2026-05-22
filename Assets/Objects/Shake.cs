using System.Collections;
using UnityEngine;

public class Shake : MonoBehaviour
{
    public float duration = 0.5f;
    public float magnitude = 5f;          // degrees
    public float scaleMagnitude = 0.05f;  // additive scale jitter
    public float pointInterval = 0.05f;   // seconds between new shake targets
    public AnimationCurve shakeCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);

    private Quaternion originalRot;
    private Vector3 originalScale;
    private Coroutine ShakeIt;

    public void Shakey()
    {
        if (ShakeIt != null)
        {
            StopCoroutine(ShakeIt);
            transform.localRotation = originalRot;
            transform.localScale = originalScale;
        }
        originalRot = transform.localRotation;
        originalScale = transform.localScale;
        ShakeIt = StartCoroutine(ShakeItUp());
    }

    private IEnumerator ShakeItUp()
    {
        float elapsed = 0f;
        float segmentElapsed = 0f;

        Quaternion fromRot = originalRot;
        Vector3 fromScale = originalScale;
        Quaternion toRot = SampleTargetRot(1f);
        Vector3 toScale = SampleTargetScale(1f);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            segmentElapsed += Time.deltaTime;

            float curveT = shakeCurve.Evaluate(elapsed / duration);

            if (segmentElapsed >= pointInterval)
            {
                fromRot = toRot;
                fromScale = toScale;
                toRot = SampleTargetRot(curveT);
                toScale = SampleTargetScale(curveT);
                segmentElapsed = 0f;
            }

            float t = pointInterval > 0f ? Mathf.Clamp01(segmentElapsed / pointInterval) : 1f;
            transform.localRotation = Quaternion.Slerp(fromRot, toRot, t);
            transform.localScale = Vector3.Lerp(fromScale, toScale, t);
            yield return null;
        }

        transform.localRotation = originalRot;
        transform.localScale = originalScale;
        ShakeIt = null;
    }

    private Quaternion SampleTargetRot(float intensity)
    {
        float mag = magnitude * intensity;
        return originalRot * Quaternion.Euler(
            Random.Range(-mag, mag),
            Random.Range(-mag, mag),
            Random.Range(-mag, mag)
        );
    }

    private Vector3 SampleTargetScale(float intensity)
    {
        float mag = scaleMagnitude * intensity;
        return new Vector3(
            originalScale.x + Random.Range(-mag, mag),
            originalScale.y + Random.Range(-mag, mag),
            originalScale.z + Random.Range(-mag, mag)
        );
    }
}
