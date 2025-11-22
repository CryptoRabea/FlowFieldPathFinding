using UnityEngine;
using UnityEngine.InputSystem;

public class ClickToMoveInspector : MonoBehaviour
{
    [Header("References")]
    public CharacterController controller;

    [Header("Input Actions (assign in Inspector)")]
    public InputAction clickAction;       // Right click action
    public InputAction mousePositionAction; // Mouse position action

    [Header("Movement Settings")]
    public float speed = 5f;

    private Vector3 targetPosition;
    private bool hasTarget = false;

    void OnEnable()
    {
        clickAction.Enable();
        mousePositionAction.Enable();
    }

    void OnDisable()
    {
        clickAction.Disable();
        mousePositionAction.Disable();
    }

    void Update()
    {
        // Detect right click
        if (clickAction.WasPressedThisFrame())
        {
            Vector2 mousePos = mousePositionAction.ReadValue<Vector2>();
            Ray ray = Camera.main.ScreenPointToRay(mousePos);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit))
            {
                // Set target position (ignore Y axis)
                targetPosition = new Vector3(hit.point.x, transform.position.y, hit.point.z);
                hasTarget = true;
            }
        }

        // Move towards target
        if (hasTarget)
        {
            Vector3 direction = targetPosition - transform.position;
            if (direction.magnitude > 0.1f)
            {
                controller.Move(direction.normalized * speed * Time.deltaTime);
            }
            else
            {
                hasTarget = false;
            }
        }
    }
}

