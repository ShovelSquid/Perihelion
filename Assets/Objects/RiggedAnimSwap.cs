using UnityEngine;
using System.Collections.Generic;

public class RiggedAnimSwap : MonoBehaviour
{
    public Transform rigRoot;
    public GameObject riggedRoot;
    public GameObject piecesRoot;

    public void Snap()
    {
        if (rigRoot == null || piecesRoot == null) return;
        Dictionary<string, Transform> bones = new Dictionary<string, Transform>();
        foreach (Transform t in rigRoot.GetComponentsInChildren<Transform>(true))
            bones[t.name] = t;
        foreach (Transform piece in piecesRoot.GetComponentsInChildren<Transform>(true))
            if (bones.TryGetValue(piece.name, out Transform bone))
                piece.SetPositionAndRotation(bone.position, bone.rotation);
    }

    public void Swap()
    {
        Snap();
        if (riggedRoot != null) riggedRoot.SetActive(false);
        if (piecesRoot != null) piecesRoot.SetActive(true);
    }
}
