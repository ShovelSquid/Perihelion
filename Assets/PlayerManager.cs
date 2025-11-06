using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerManager : MonoBehaviour
{
    public GameObject player;

    public void OnMove(InputAction.CallbackContext moveInputContext)
    {
        Debug.Log("Move input detected");
        Vector2 moveInput = moveInputContext.ReadValue<Vector2>();
        Vector2 moveDirection = moveInput;
        if (moveInput != Vector2.zero)
        {
            Vector3 cameraForward = Camera.main.transform.forward;
            Vector3 cameraRight = Camera.main.transform.right;
            cameraForward.y = 0;
            cameraRight.y = 0;
            cameraForward.Normalize();
            cameraRight.Normalize();

            Vector3 direction = cameraRight * moveInput.x + cameraForward * moveInput.y;
            moveDirection = new Vector2(direction.x, direction.z).normalized;
        }

        player.GetComponent<Move>().SetMoveDirection(moveDirection);
    }

    public void OnRotate(InputAction.CallbackContext rotateInputContext)
    {
        Debug.Log("Rotate input detected");
        string actionName = rotateInputContext.action.name;
        Debug.Log("action name: " + actionName);
        bool on = false;
        if (rotateInputContext.canceled)
        {
            on = false;
        }
        if (rotateInputContext.performed)
        {
            on = true;
        }
        Debug.Log("on: " + on);
        if (actionName == "Q")
        {
            Camera.main.GetComponent<CameraController>().OnRotateLeft(on);
        }
        if (actionName == "E")
        {
            Camera.main.GetComponent<CameraController>().OnRotateRight(on);
        }
    }

    public void OnJump(InputAction.CallbackContext jumpInput)
    {
        Debug.Log("Jump input detected");
        bool jumpPressed = jumpInput.started;
        if (jumpPressed)
        {
            player.GetComponent<Move>().Jump();
        }
    }
}
