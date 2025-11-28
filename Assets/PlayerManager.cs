using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerManager : MonoBehaviour
{
    public GameObject player;
    public MenuScript menu;
    public CameraController cam;
    private bool cursorLocked = false;
    public bool playerInputEnabled = true;
    [Header("Ability References")]
    private AbilityManager abilities;
    
    void Awake()
    {
        if (player != null)
        {
            abilities = player.GetComponent<AbilityManager>();
        }
    }


    void LockCursor()
    {
        #if UNITY_WEBGL && !UNITY_EDITOR
            // On WebGL, just hide cursor, don't lock
            Cursor.visible = false;
        #else
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            cursorLocked = true;
        #endif
    }
    void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        cursorLocked = false;
    }

    public void OnMove(InputAction.CallbackContext moveInputContext)
    {
        if (!playerInputEnabled) return;
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
        if (!playerInputEnabled) return;
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
            cam.OnRotateLeft(on);
        }
        if (actionName == "E")
        {
            cam.OnRotateRight(on);
        }
    }

    public void OnClick(InputAction.CallbackContext clickInputContext)
    {
        if (!playerInputEnabled) return;
        // Debug.Log("Click input detected");
        // bool clickPressed = clickInputContext.started;
        // if (clickPressed)
        // {
        //     player.GetComponent<Attack>().ClickAttack();
        // }
    }

    public void OnLook(InputAction.CallbackContext lookInputContext)
    {
        if (!playerInputEnabled) return;
        Vector2 lookInput = lookInputContext.ReadValue<Vector2>();
        bool isController = lookInputContext.control.device is Gamepad;
        Debug.Log("Look input from " + (isController ? "Controller" : "Keyboard/Mouse"));
        cam.OnLook(lookInput, isController);
    }

    public void OnJump(InputAction.CallbackContext jumpContext)
    {
        if (!playerInputEnabled) return;
        Move move = player.GetComponent<Move>();
        if (move == null) return;
        // if (jumpContext.started)
        // {
        //     abilities.OnChargeJump(jumpContext);
        // }
        // else if (jumpContext.canceled)
        // {
        //     abilities.OnChargeJump(jumpContext);
        //     // Handled in ChargeJump ability
        // }
        if (jumpContext.canceled)
        {
            move.Jump();
        }
    }

    public void OnMenu(InputAction.CallbackContext menuContext)
    {
        bool isController = menuContext.control.device is Gamepad;
        menu.controller = isController;
        if (menu.paused)
        {
            LockCursor();
            menu.Resume();
        }
        else
        {
            UnlockCursor();
            menu.Pause();
        }
    }
    
    public void OnSubmit(InputAction.CallbackContext submitContext)
    {
        Debug.Log("SUBMITTING SUBMITTING");
        if (menu.paused && submitContext.performed)
        {
            menu.Select();
        }
    }
    
    public void OnNavigate(InputAction.CallbackContext navigateContext)
    {
        Vector2 navigateInput = navigateContext.ReadValue<Vector2>();
        Debug.Log("PRESSY PRESSY : " + navigateInput);
        if (menu.paused)
        {
            if (!navigateContext.performed) return;
            if (navigateInput.y > 0.5f)
            {
                menu.SelectDownOne();
                menu.wentUp = false;
            }
            else if (navigateInput.y < -0.5f)
            {
                menu.SelectUpOne();
                menu.wentDown = false;
            }
            else
            {
                menu.wentUp = false;
                menu.wentDown = false;
            }
        }
    }
}
