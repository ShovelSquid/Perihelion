using System.Collections;
using UnityEngine;

public class Shake : MonoBehaviour
{
    public float duration = 0.5f;
    public float magnitude = 0.1f;
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
            // transform.localScale = originalScale;
        }
        originalScale = transform.localScale;
        originalRot = transform.localRotation;
        // originalScale = transform.localScale;
        ShakeIt = StartCoroutine(ShakeItUp());
    }

    // private IEnumerator ShakeItUp()
    // {
    //     float elapsed = 0f;
        
    //     while (elapsed < duration)
    //     {
    //         // Random target rotation
    //         Quaternion randomRotation = originalTrans.localRotation * Quaternion.Euler(
    //             Random.Range(-magnitude, magnitude), 
    //             Random.Range(-magnitude, magnitude), 
    //             Random.Range(-magnitude, magnitude)
    //         );
            
    //         // Slerp to random rotation
    //         float t = shakeCurve.Evaluate(elapsed / duration);
    //         transform.localRotation = Quaternion.Slerp(transform.localRotation, randomRotation, t);
            
    //         elapsed += Time.deltaTime;
    //         yield return null;
    //     }
        
    //     // Slerp back to original rotation
    //     float returnElapsed = 0f;
    //     float returnDuration = 0.2f;
    //     Quaternion currentRotation = transform.localRotation;
        
    //     while (returnElapsed < returnDuration)
    //     {
    //         returnElapsed += Time.deltaTime;
    //         float returnT = returnElapsed / returnDuration;
    //         transform.localRotation = Quaternion.Slerp(currentRotation, originalTrans.localRotation, returnT);
    //         yield return null;
    //     }
        
    //     // Ensure it's exactly back to original
    //     transform.localRotation = originalTrans.localRotation;
    //     yield break;
    // }

    private IEnumerator ShakeItUp()
    {
        float elapsed = 0f;
        float mag;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            mag = shakeCurve.Evaluate(elapsed / duration) * magnitude;
            transform.localRotation = new Quaternion(
                originalRot.x + Random.Range(-mag, mag),
                originalRot.y + Random.Range(-mag, mag),
                originalRot.z + Random.Range(-mag, mag),
                originalRot.w + Random.Range(-mag, mag)
            );
            transform.localScale = new Vector3(
                originalScale.x * 1 + Random.Range(-mag, mag),
                originalScale.y * 1 + Random.Range(-mag, mag),
                originalScale.z * 1 + Random.Range(-mag, mag)
            );
            yield return new WaitForSeconds(Random.Range(0.01f, 0.1f));
        }
        transform.localRotation = originalRot;
        transform.localScale = originalScale;
        ShakeIt = null;
        yield break;
    }
}
