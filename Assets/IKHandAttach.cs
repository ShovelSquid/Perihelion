using UnityEngine;

public class IKHandAttach : MonoBehaviour
{
    public Transform HandLIKTarget;
    public Transform HandRIKTarget;
    public Item item;

    // Update is called once per frame
    void Update()
    {
        if (item.equipInfo.leftHand)
        {
            HandLIKTarget.position = item.handL.position;
            HandLIKTarget.rotation = item.handL.rotation;
        }
        if (item.equipInfo.rightHand)
        {
            HandRIKTarget.position = item.handR.position;
            HandRIKTarget.rotation = item.handR.rotation;
        }
    }
}
