using UnityEngine;
using UnityEngine.InputSystem;

public class CameraModeToggle : MonoBehaviour
{
    private RTSCameraController rtsCam;
    private FreeFlyEditorStyleCamera freeCam;
    private bool isFreeCam = false;

    private void Awake()
    {
        rtsCam = GetComponent<RTSCameraController>();
        freeCam = GetComponent<FreeFlyEditorStyleCamera>();

        // Default = RTS Camera
        if (rtsCam != null) rtsCam.enabled = true;
        if (freeCam != null) freeCam.enabled = false;
    }

    private void Update()
    {
        if ( Keyboard.current.lKey.isPressed) 
        {
            ToggleCamera();
        }
    }

    private void ToggleCamera()
    {
        isFreeCam = !isFreeCam;

        if (isFreeCam)
        {
            // Switch ON Free Camera
            if (rtsCam != null) rtsCam.enabled = false;
            if (freeCam != null) freeCam.enabled = true;

            // Unlock cursor immediately for editor-style camera
            Cursor.lockState = CursorLockMode.None;
        }
        else
        {
            // Switch ON RTS Camera
            if (rtsCam != null) rtsCam.enabled = true;
            if (freeCam != null) freeCam.enabled = false;

            // RTS camera does not lock the cursor
            Cursor.lockState = CursorLockMode.None;
        }
    }
}
