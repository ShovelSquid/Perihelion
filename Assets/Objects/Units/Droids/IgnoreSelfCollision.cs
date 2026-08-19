using UnityEngine;
[DisallowMultipleComponent]
public class IgnoreSelfCollision : MonoBehaviour
{
    Collider[] cols;

    void OnEnable()
    {
        cols = GetComponentsInChildren<Collider>(includeInactive: true);
        for (int i = 0; i < cols.Length; i++)
        for (int j = i + 1; j < cols.Length; j++)
        {
            if (!cols[i] || !cols[j]) continue;
            if (cols[i].isTrigger || cols[j].isTrigger) continue;  // leave pickup/detection volumes alone
            Physics.IgnoreCollision(cols[i], cols[j], true);
        }
    }
}