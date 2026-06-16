using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerManager : MonoBehaviour
{
    public GameObject player;
    public Player mob;
    public MenuScript menu;
    public CameraController cam;
    public Hotwheel hotwheel;
    public Move move;
    public Look look;
    private bool cursorLocked = false;
    public bool playerInputEnabled = true;
    private Vector2 rawMoveInput;
    [Header("Hotwheel")]
    [SerializeField] float scrollStep = 1f; // accumulated scroll delta needed to change one slot (tune per platform/device)
    private float scrollAccum;
    public bool scrollInverted = false;
    [Header("Ability References")]
    private AbilityManager abilities;
    
    void Awake()
    {
        if (player != null)
        {
            abilities = player.GetComponent<AbilityManager>();
        }
    }

    void Start()
    {
        LockCursor();
        mob = player.GetComponent<Player>();
        move = player.GetComponent<Move>();
        look = player.GetComponent<Look>();
        cam = Camera.main.GetComponent<CameraController>();
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
        rawMoveInput = moveInputContext.ReadValue<Vector2>();
        if (rawMoveInput == Vector2.zero)
        {
            move.SetMoveDirection(Vector2.zero);
        }
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

    void Update()
    {
        if (rawMoveInput == Vector2.zero) return;
        Vector3 fwd = look.swivel.forward;
        Vector3 right = look.swivel.right;
        fwd.y = 0;
        right.y = 0;
        fwd.Normalize();
        right.Normalize();
        Vector3 direction = right * rawMoveInput.x + fwd * rawMoveInput.y;
        move.SetMoveDirection(new Vector2(direction.x, direction.z).normalized);
    }

    public void OnPrimary(InputAction.CallbackContext primaryInputContext)
    {
        if (!playerInputEnabled) return;
        bool primaryPressed = primaryInputContext.started;
        if (primaryPressed)
        {
            mob.item?.SlapTrigger(true);
        }
        else if (primaryInputContext.canceled)
        {
            mob.item?.SlapTrigger(false);
        }
    }

    public void OnSecondary(InputAction.CallbackContext secondaryInputContext)
    {
        if (!playerInputEnabled) return;
        bool secondaryPressed = secondaryInputContext.started;
        if (secondaryPressed)
        {
            mob.Aim(true);
        }
        else if (secondaryInputContext.canceled)
        {
            mob.Aim(false);
        }
    }

    public void OnLook(InputAction.CallbackContext lookInputContext)
    {
        if (!playerInputEnabled) return;
        Vector2 lookInput = lookInputContext.ReadValue<Vector2>();
        bool isController = lookInputContext.control.device is Gamepad;
        Debug.Log("Look input from " + (isController ? "Controller" : "Keyboard/Mouse"));
        // cam.OnLook(lookInput, isController);
        look.SetLookDirection(lookInput, isController);
    }

    public void OnJump(InputAction.CallbackContext jumpContext)
    {
        if (!playerInputEnabled) return;
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

    public void OnReload(InputAction.CallbackContext reloadContext)
    {
        if (!playerInputEnabled) return;
        if (reloadContext.started)
        {
            if (mob.item is Gun gun)
            {
                gun.StartReload();
            }
        }
    }

    public void OnInteract(InputAction.CallbackContext interactContext)
    {
        if (!playerInputEnabled) return;
        if (interactContext.started)
        {
            mob.Activate();
        }
    }

    public void OnScroll(InputAction.CallbackContext scrollContext)
    {
        if (!playerInputEnabled) return;
        if (!scrollContext.performed) return;            // accumulate each change exactly once
        float scrollDelta = scrollContext.ReadValue<float>();
        if (scrollInverted) scrollDelta = -scrollDelta;
        scrollAccum += scrollDelta; // action is an Axis now, not a Vector2
        while (scrollAccum >= scrollStep)  { hotwheel.EquipNext(true);  scrollAccum -= scrollStep; }
        while (scrollAccum <= -scrollStep) { hotwheel.EquipNext(false); scrollAccum += scrollStep; }
    }

    public void OnNumber(InputAction.CallbackContext numberContext)
    {
        if (!playerInputEnabled) return;
        if (numberContext.ReadValue<float>() < 0.5f) return; // press only; Pass-Through also fires on key release
        int numberPressed = int.Parse(numberContext.control.name);
        hotwheel.EquipSlot(numberPressed - 1);
    }
}
