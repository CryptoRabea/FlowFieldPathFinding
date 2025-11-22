using UnityEngine;
using UnityEngine.InputSystem;

public class FPSCounter : MonoBehaviour
{
    [Header("FPS Settings")]
    [SerializeField] private bool showFPS = true;
    [SerializeField] private float updateInterval = 0.5f;

    [Header("Display Settings")]
    [SerializeField] private Color textColor = Color.green;
    [SerializeField] private int fontSize = 20;
    [SerializeField] private Vector2 position = new Vector2(10, 10);

    private float fps;
    private float accum = 0f;
    private int frames = 0;
    private float timeLeft;

    private GUIStyle style;

    void Start()
    {
        timeLeft = updateInterval;

        // Initialize GUI style
        style = new GUIStyle();
        style.fontSize = fontSize;
        style.normal.textColor = textColor;
        style.fontStyle = FontStyle.Bold;
    }

    void Update()
    {
        // Toggle FPS display with P key using new Input System
        if (Keyboard.current != null && Keyboard.current.pKey.wasPressedThisFrame)
        {
            showFPS = !showFPS;
        }

        if (!showFPS) return;

        // Calculate FPS
        timeLeft -= Time.unscaledDeltaTime;
        accum += 1.0f / Time.unscaledDeltaTime;
        frames++;

        if (timeLeft <= 0f)
        {
            fps = accum / frames;
            timeLeft = updateInterval;
            accum = 0f;
            frames = 0;
        }
    }

    void OnGUI()
    {
        if (!showFPS) return;

        // Update style color in case it changed
        style.normal.textColor = textColor;
        style.fontSize = fontSize;

        // Display FPS
        GUI.Label(new Rect(position.x, position.y, 200, 50),
                  $"FPS: {fps:F1}", style);
    }
}