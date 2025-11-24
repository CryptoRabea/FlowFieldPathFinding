using UnityEngine;
using UnityEngine.InputSystem; // Required for new Input System

public class FreeFlyEditorStyleCamera : MonoBehaviour
{
    public float moveSpeed = 10f;
    public float boostMultiplier = 3f;
    public float lookSensitivity = 2f;

    private float rotX;
    private float rotY;

    void Update()
    {
        HandleLook();
        HandleMove();
    }

    void HandleLook()
    {
        // Right mouse button lock/unlock
        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            Cursor.lockState = CursorLockMode.Locked;
        }
        else if (Mouse.current.rightButton.wasReleasedThisFrame)
        {
            Cursor.lockState = CursorLockMode.None;
        }

        if (Mouse.current.rightButton.isPressed)
        {
            float mx = Mouse.current.delta.x.ReadValue() * lookSensitivity;
            float my = Mouse.current.delta.y.ReadValue() * lookSensitivity;

            rotY += mx;
            rotX -= my;
            rotX = Mathf.Clamp(rotX, -89f, 89f);

            transform.rotation = Quaternion.Euler(rotX, rotY, 0f);
        }
    }

    void HandleMove()
    {
        float speed = Keyboard.current.leftShiftKey.isPressed ? moveSpeed * boostMultiplier : moveSpeed;

        Vector3 dir = Vector3.zero;

        if (Keyboard.current.wKey.isPressed) dir += transform.forward;
        if (Keyboard.current.sKey.isPressed) dir -= transform.forward;
        if (Keyboard.current.aKey.isPressed) dir -= transform.right;
        if (Keyboard.current.dKey.isPressed) dir += transform.right;
        if (Keyboard.current.eKey.isPressed) dir += transform.up;
        if (Keyboard.current.qKey.isPressed) dir -= transform.up;

        transform.position += dir * speed * Time.deltaTime;
    }
}
