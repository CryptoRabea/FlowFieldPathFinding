using UnityEngine;
using UnityEngine.InputSystem;

public class RTSCameraController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 15f;
    public float sprintMultiplier = 2.5f; // Speed multiplier when sprinting
    public float dragSpeed = 0.5f;
    public float edgeScrollSpeed = 20f;
    public float panBorderThickness = 10f;
    public bool useEdgeScroll = true;
    public Vector2 minPosition;
    public Vector2 maxPosition;
    public bool isCamInverted = false;

    [Header("Zoom")]
    public float zoomSpeed = 50f;
    public float minZoom = 15f;
    public float maxZoom = 80f;

    [Header("Rotation")]
    public float rotationSpeed = 60f; // degrees per second
    private float initialRotationX; // Store initial Y rotation for reset
    private float initialRotationY; // Store initial Y rotation for reset
    private float initialRotationZ; // Store initial Y rotation for reset

    private Camera cam;
    private Vector2 moveInput;
    private Vector2 moveUpDown;
    private float zoomInput;
    private float rotationInput;
    private bool isSprinting = false;
    private bool isDragging = false;
    private Vector3 lastMousePos;

    private InputSystem_Actions inputActions;

    private void Awake()
    {
        if (minPosition == Vector2.zero)
        {  minPosition =new Vector2(-1000f,-1000f); }
        if (maxPosition == Vector2.zero)
        {  maxPosition =new Vector2(1000f,1000f); }
            cam = GetComponent<Camera>();
        inputActions = new InputSystem_Actions();

        // Store initial rotation
        initialRotationX = transform.eulerAngles.x;
        initialRotationY = transform.eulerAngles.y;
        initialRotationZ = transform.eulerAngles.z;

        // WASD / Arrow movement on the x , z axis 
        inputActions.Player.Move.performed += ctx => moveInput = ctx.ReadValue<Vector2>();
        inputActions.Player.Move.canceled += ctx => moveInput = Vector2.zero;

        // Scroll zoom
        inputActions.Player.Zoom.performed += ctx => zoomInput = ctx.ReadValue<float>();
        inputActions.Player.Zoom.canceled += ctx => zoomInput = 0f;

        // Rotation (Q / E)
        inputActions.Player.Rotate.performed += ctx => rotationInput = ctx.ReadValue<float>();
        inputActions.Player.Rotate.canceled += ctx => rotationInput = 0f;

        // Sprint (Shift)
        inputActions.Player.Sprint.performed += ctx => isSprinting = true;
        inputActions.Player.Sprint.canceled += ctx => isSprinting = false;

        // Rotation (Z / C)  movement on the y,axis 
        inputActions.Player.MoveUpDown.performed += ctx => moveUpDown = ctx.ReadValue<Vector2>();
        inputActions.Player.MoveUpDown.canceled += ctx => moveUpDown = Vector2.zero;
    }

    private void OnEnable() => inputActions.Enable();
    private void OnDisable() => inputActions.Disable();

    private void Update()
    {
        HandleMovement();
        HandleZoom();
        HandleTouch();
        HandleRotation();
        HandleMiddleMouseDrag();
    }

    private void HandleMovement()
    {
        Vector3 dirXZ = new Vector3(moveInput.x, 0, moveInput.y);
        Vector3 dirY = new(0, moveUpDown.y, 0);

        // Edge scrolling
        if (useEdgeScroll && Mouse.current != null)
        {
            Vector2 mousePos = Mouse.current.position.ReadValue();
            if (mousePos.y >= Screen.height - panBorderThickness) dirXZ.z += 1;
            if (mousePos.y <= panBorderThickness) dirXZ.z -= 1;
            if (mousePos.x >= Screen.width - panBorderThickness) dirXZ.x += 1;
            if (mousePos.x <= panBorderThickness) dirXZ.x -= 1;
        }

        // Apply sprint multiplier when shift is held
        float currentSpeed = isSprinting ? moveSpeed * sprintMultiplier : moveSpeed;

        Vector3 movementXZ = Quaternion.Euler(0, transform.eulerAngles.y, 0) * dirXZ.normalized * currentSpeed * Time.deltaTime;
        transform.position += movementXZ;
        Vector3 movementY = Quaternion.Euler(0, transform.eulerAngles.y, 0) * dirY.normalized * currentSpeed * Time.deltaTime;
        transform.position += movementY;

        // Clamp position
        transform.position = new Vector3(
            Mathf.Clamp(transform.position.x, minPosition.x, maxPosition.x),
            transform.position.y,
            Mathf.Clamp(transform.position.z, minPosition.y, maxPosition.y)
        );
    }

    private void HandleZoom()
    {
        float newZoom = cam.orthographic ? cam.orthographicSize - zoomInput * zoomSpeed * Time.deltaTime
                                         : cam.fieldOfView - zoomInput * zoomSpeed * Time.deltaTime;

        if (cam.orthographic)
            cam.orthographicSize = Mathf.Clamp(newZoom, minZoom, maxZoom);
        else
            cam.fieldOfView = Mathf.Clamp(newZoom, minZoom, maxZoom);
    }

    private void HandleRotation()
    {
        // Check for Q (rotate left) and E (rotate right) keys directly
        if (Keyboard.current != null)
        {
            // Check for Shift + Q/E for instant 90-degree snaps
            bool isShiftPressed = Keyboard.current.shiftKey.isPressed;

            if (isShiftPressed && Keyboard.current.qKey.wasPressedThisFrame)
            {
                // Snap 90 degrees left (counter-clockwise)
                transform.Rotate(Vector3.up, -90f, Space.World);
            }
            else if (isShiftPressed && Keyboard.current.eKey.wasPressedThisFrame)
            {
                // Snap 90 degrees right (clockwise)
                transform.Rotate(Vector3.up, 90f, Space.World);
            }
            else
            {
                // Continuous rotation when just Q or E is held (without Shift)
                float rotationRightLeft = 0f;
                float rotationUpDown = 0f;

                if (Keyboard.current.qKey.isPressed)
                    rotationRightLeft -= 1f; // Rotate left (counter-clockwise)

                if (Keyboard.current.eKey.isPressed)
                    rotationRightLeft += 1f; // Rotate right (clockwise)

                if (Keyboard.current.rKey.isPressed)
                    rotationUpDown -= 1f; // Rotate up 

                if (Keyboard.current.vKey.isPressed)
                    rotationUpDown += 1f; // Rotate down 

                if (Mathf.Abs(rotationRightLeft) > 0.01f)
                {
                    transform.Rotate(Vector3.up, rotationRightLeft * rotationSpeed * Time.deltaTime, Space.World);
                }
                if (Mathf.Abs(rotationUpDown) > 0.01f)
                {
                    transform.Rotate(Vector3.right, rotationUpDown * rotationSpeed * Time.deltaTime, Space.Self);
                }
            }

            // Reset rotation to initial angle when Space is pressed
            if (Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                Vector3 currentEuler = transform.eulerAngles;
                transform.eulerAngles = new(initialRotationX, initialRotationY, initialRotationZ);
            }
        }
    }

    private void HandleMiddleMouseDrag()
    {
        if (Mouse.current == null) return;

        if (Mouse.current.middleButton.wasPressedThisFrame)
        {
            isDragging = true;
            lastMousePos = Mouse.current.position.ReadValue();
        }
        else if (Mouse.current.middleButton.wasReleasedThisFrame)
        {
            isDragging = false;
        }

        if (isDragging)
        {
            Vector3 mousePos = Mouse.current.position.ReadValue();
            Vector3 delta = mousePos - lastMousePos;
            lastMousePos = mousePos;

            if (isCamInverted)
                delta = -delta;

            // Convert screen drag to world-space motion relative to camera
            Vector3 right = Camera.main.transform.right;
            Vector3 forward = Camera.main.transform.forward;
            forward.y = 0; // Keep movement horizontal
            forward.Normalize();

            Vector3 move = dragSpeed * Time.deltaTime * (right * delta.x + forward * delta.y);
            transform.position += move;
        }

    }

    private void HandleTouch()
    {
        if (Touchscreen.current == null || Touchscreen.current.touches.Count == 0) return;

        if (Touchscreen.current.touches.Count == 1)
        {
            // Drag camera
            Vector2 delta = Touchscreen.current.touches[0].delta.ReadValue();
            transform.position += Quaternion.Euler(0, transform.eulerAngles.y, 0)
                                * new Vector3(-delta.x, 0, -delta.y)
                                * Time.deltaTime * moveSpeed * 0.5f;
        }

        if (Touchscreen.current.touches.Count >= 2)
        {
            // Pinch zoom
            Vector2 pos0 = Touchscreen.current.touches[0].position.ReadValue();
            Vector2 pos1 = Touchscreen.current.touches[1].position.ReadValue();
            Vector2 prev0 = pos0 - Touchscreen.current.touches[0].delta.ReadValue();
            Vector2 prev1 = pos1 - Touchscreen.current.touches[1].delta.ReadValue();

            float prevDist = Vector2.Distance(prev0, prev1);
            float currDist = Vector2.Distance(pos0, pos1);
            float pinch = currDist - prevDist;

            float newZoom = cam.orthographic ? cam.orthographicSize - pinch * 0.1f
                                             : cam.fieldOfView - pinch * 0.1f;

            if (cam.orthographic)
                cam.orthographicSize = Mathf.Clamp(newZoom, minZoom, maxZoom);
            else
                cam.fieldOfView = Mathf.Clamp(newZoom, minZoom, maxZoom);
        }
    }
}
