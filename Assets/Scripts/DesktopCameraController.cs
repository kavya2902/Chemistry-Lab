using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Basic WASD + mouse-look controller for desktop testing without a VR headset.
/// Uses the new Input System (Mouse/Keyboard) so it is compatible with projects
/// where Active Input Handling is set to "Input System Package".
/// </summary>
[RequireComponent(typeof(Camera))]
public class DesktopCameraController : MonoBehaviour
{
    [Tooltip("Movement speed in units per second.")]
    public float moveSpeed = 2f;

    [Tooltip("Mouse look sensitivity.")]
    public float lookSpeed = 2f;

    /// <summary>Converts raw mouse pixel delta into a comfortable look speed.</summary>
    private const float MouseDeltaScale = 0.05f;

    private float _pitch;
    private float _yaw;
    private bool _mouseLocked;

    private void Start()
    {
        Vector3 euler = transform.eulerAngles;
        _pitch = euler.x;
        _yaw = euler.y;
    }

    private void Update()
    {
        Mouse mouse = Mouse.current;
        Keyboard keyboard = Keyboard.current;
        if (mouse == null || keyboard == null)
        {
            return;
        }

        // Right-click to lock/unlock the mouse for looking around.
        if (mouse.rightButton.wasPressedThisFrame)
        {
            _mouseLocked = !_mouseLocked;
            Cursor.lockState = _mouseLocked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !_mouseLocked;
        }

        if (!_mouseLocked)
        {
            return;
        }

        HandleLook(mouse);
        HandleMovement(keyboard);
    }

    /// <summary>Applies mouse-look rotation from the current mouse delta.</summary>
    private void HandleLook(Mouse mouse)
    {
        Vector2 delta = mouse.delta.ReadValue() * (lookSpeed * MouseDeltaScale);
        _yaw += delta.x;
        _pitch -= delta.y;
        _pitch = Mathf.Clamp(_pitch, -80f, 80f);
        transform.eulerAngles = new Vector3(_pitch, _yaw, 0f);
    }

    /// <summary>Applies WASD movement relative to the camera's facing direction.</summary>
    private void HandleMovement(Keyboard keyboard)
    {
        float horizontal = 0f;
        float vertical = 0f;

        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) horizontal -= 1f;
        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) horizontal += 1f;
        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) vertical -= 1f;
        if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) vertical += 1f;

        Vector3 direction = new Vector3(horizontal, 0f, vertical);
        transform.Translate(direction * (moveSpeed * Time.deltaTime), Space.Self);
    }
}
