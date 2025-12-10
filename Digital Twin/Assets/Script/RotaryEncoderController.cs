using UnityEngine;
using UnityEngine.InputSystem;
using System;

/// <summary>
/// Rotary Encoder Controller for Unity Digital Twin
/// Simulates physical rotary encoder behavior with keyboard input and MQTT sync
/// 
/// IMPORTANT SETUP:
/// 1. Create an empty GameObject as parent (this will be the rotation center)
/// 2. Attach THIS SCRIPT to the parent GameObject
/// 3. Place your knob 3D model as a child of the parent
/// 4. Position the parent GameObject at the center where you want rotation to occur
/// 
/// Keyboard Controls:
/// - Q: Rotate Counter-Clockwise (CCW)
/// - E: Rotate Clockwise (CW)
/// - W: Press button (push down animation)
/// 
/// Physical Sync:
/// - Listens to MQTT for physical encoder rotation/button events
/// - Mirrors physical encoder movements in Unity
/// </summary>
public class RotaryEncoderController : MonoBehaviour
{
    [Header("Rotation Settings")]
    [Tooltip("Number of steps per full 360° rotation")]
    public int stepsPerRotation = 24;

    [Tooltip("Rotation speed (degrees per second)")]
    public float rotationSpeed = 360f;

    [Header("Button Press Animation")]
    [Tooltip("Press animation duration (seconds)")]
    public float pressDuration = 0.2f;

    [Tooltip("How far the knob moves down when pressed (local Y axis)")]
    public float pressDepth = 0.05f;

    [Tooltip("Optional: Assign the knob child object if press animation should only affect it")]
    public Transform knobVisual;

    [Header("MQTT Integration")]
    [Tooltip("Reference to MQTT Manager for physical encoder sync")]
    public mqttManager mqttManager;

    [Tooltip("Enable physical encoder synchronization")]
    public bool enablePhysicalSync = true;

    [Header("Events")]
    [Tooltip("Fired when encoder rotates clockwise")]
    public UnityEngine.Events.UnityEvent OnRotateClockwise;

    [Tooltip("Fired when encoder rotates counter-clockwise")]
    public UnityEngine.Events.UnityEvent OnRotateCounterClockwise;

    [Tooltip("Fired when button is pressed")]
    public UnityEngine.Events.UnityEvent OnButtonPress;

    // Private state
    private int currentStep = 0;
    private float targetRotation = 0f;
    private float currentRotation = 0f;

    private bool isPressed = false;
    private float pressTimer = 0f;
    private Vector3 initialPosition;
    private Vector3 pressedPosition;

    private float degreesPerStep;

    // Physical encoder state tracking
    private int lastPhysicalStep = 0;
    private bool lastPhysicalButton = false;

    void Start()
    {
        // Calculate rotation per step
        degreesPerStep = 360f / stepsPerRotation;

        // Auto-setup: Create parent-child structure at runtime if needed
        if (knobVisual == null && transform.childCount == 0)
        {
            // This GameObject IS the knob model, we need to create a parent wrapper
            Debug.Log("[RotaryEncoder] Auto-creating rotation center wrapper...");

            // Store original parent and transform data
            Transform originalParent = transform.parent;
            Vector3 worldPos = transform.position;
            Quaternion worldRot = transform.rotation;
            Vector3 worldScale = transform.lossyScale;

            // Create wrapper parent at the knob's position, offset to rotation center
            GameObject rotationCenter = new GameObject(transform.name + "_RotationCenter");
            // Offset parent position by -0.085 in X and Z to center rotation
            Vector3 centerOffset = new Vector3(-0.085f, 0f, -0.085f);
            rotationCenter.transform.position = worldPos + transform.TransformDirection(centerOffset);
            rotationCenter.transform.rotation = worldRot;
            rotationCenter.transform.SetParent(originalParent, true);

            // Move this script to the parent
            RotaryEncoderController newController = rotationCenter.AddComponent<RotaryEncoderController>();
            newController.stepsPerRotation = this.stepsPerRotation;
            newController.rotationSpeed = this.rotationSpeed;
            newController.pressDuration = this.pressDuration;
            newController.pressDepth = this.pressDepth;
            newController.mqttManager = this.mqttManager;
            newController.enablePhysicalSync = this.enablePhysicalSync;
            newController.knobVisual = transform; // The original knob model becomes the visual

            // Make the knob a child of the wrapper
            transform.SetParent(rotationCenter.transform, true);
            // Set knob local position to 0.085, 0, 0.085 (opposite of parent offset)
            transform.localPosition = new Vector3(0.085f, 0f, 0.085f);
            transform.localRotation = Quaternion.identity;

            // Destroy this component (the script will run on the new parent)
            Destroy(this);

            Debug.Log($"[RotaryEncoder] Created structure: {rotationCenter.name} -> {transform.name}");
            return;
        }

        // If knobVisual is not assigned, use this transform for press animation
        if (knobVisual == null)
        {
            knobVisual = transform.GetChild(0); // Use first child
            Debug.Log($"[RotaryEncoder] Auto-assigned knob visual: {knobVisual.name}");
        }

        // Store initial position for button press animation
        initialPosition = knobVisual.localPosition;
        pressedPosition = initialPosition - new Vector3(0, pressDepth, 0);

        // Initialize rotation tracking from current rotation
        currentRotation = transform.localEulerAngles.y;
        targetRotation = currentRotation;

        // Setup MQTT listener for physical encoder
        if (enablePhysicalSync && mqttManager != null)
        {
            SetupPhysicalSync();
        }

        Debug.Log($"[RotaryEncoder] Initialized: {stepsPerRotation} steps/rotation, {degreesPerStep}°/step");
        Debug.Log($"[RotaryEncoder] Initial rotation: {currentRotation}°");
        Debug.Log($"[RotaryEncoder] Rotation target: {transform.name}, Press target: {knobVisual.name}");
    }

    void Update()
    {
        HandleKeyboardInput();
        UpdateRotationAnimation();
        UpdatePressAnimation();
    }

    void HandleKeyboardInput()
    {
        // Q - Counter-Clockwise
        if (Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame)
        {
            // Publish MQTT for CCW rotation (NOT retained - transient event)
            if (mqttManager != null && mqttManager.isConnected)
            {
                mqttManager.topicPublish = "student/CASA0019/Gilang/encoder";
                mqttManager.messagePublish = "{\"encoder\":\"rotation\",\"direction\":\"ccw\"}";
                mqttManager.Publish(false); // false = not retained
            }
            RotateCounterClockwise();
        }

        // E - Clockwise
        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            // Publish MQTT for CW rotation (NOT retained - transient event)
            if (mqttManager != null && mqttManager.isConnected)
            {
                mqttManager.topicPublish = "student/CASA0019/Gilang/encoder";
                mqttManager.messagePublish = "{\"encoder\":\"rotation\",\"direction\":\"cw\"}";
                mqttManager.Publish(false); // false = not retained
            }
            RotateClockwise();
        }

        // W: Press button
        if (Keyboard.current != null && Keyboard.current.wKey.wasPressedThisFrame)
        {
            PressButton();
        }
    }

    /// <summary>
    /// Rotate encoder clockwise by one step
    /// </summary>
    public void RotateClockwise()
    {
        currentStep++;
        if (currentStep >= stepsPerRotation)
        {
            currentStep = 0;
        }

        targetRotation = currentStep * degreesPerStep;

        Debug.Log($"[RotaryEncoder] Rotate CW → Step {currentStep}/{stepsPerRotation} ({targetRotation}°)");

        OnRotateClockwise?.Invoke();
    }

    /// <summary>
    /// Rotate encoder counter-clockwise by one step
    /// </summary>
    public void RotateCounterClockwise()
    {
        currentStep--;
        if (currentStep < 0)
        {
            currentStep = stepsPerRotation - 1;
        }

        targetRotation = currentStep * degreesPerStep;

        Debug.Log($"[RotaryEncoder] Rotate CCW → Step {currentStep}/{stepsPerRotation} ({targetRotation}°)");

        OnRotateCounterClockwise?.Invoke();
    }

    /// <summary>
    /// Press the encoder button (trigger animation and publish MQTT)
    /// </summary>
    public void PressButton()
    {
        if (!isPressed)
        {
            isPressed = true;
            pressTimer = 0f;

            Debug.Log($"[RotaryEncoder] 🔘 Button PRESSED - Publishing MQTT");

            // Publish MQTT message for mode toggle (NOT retained - it's a transient event)
            if (mqttManager != null && mqttManager.isConnected)
            {
                mqttManager.topicPublish = "student/CASA0019/Gilang/encoder";
                mqttManager.messagePublish = "{\"encoder\":\"button\",\"pressed\":true}";
                mqttManager.Publish(false); // false = not retained for button press
                Debug.Log($"[RotaryEncoder] 📤 Published button press to MQTT (transient)");
            }
            else
            {
                Debug.LogWarning($"[RotaryEncoder] ⚠️ MQTT not connected, button press not published");
            }

            OnButtonPress?.Invoke();
        }
    }

    /// <summary>
    /// Smoothly animate rotation to target angle
    /// </summary>
    void UpdateRotationAnimation()
    {
        if (Mathf.Abs(currentRotation - targetRotation) > 0.1f)
        {
            // Calculate the angle difference
            float angleDiff = Mathf.DeltaAngle(currentRotation, targetRotation);
            float rotationStep = Mathf.Sign(angleDiff) * Mathf.Min(Mathf.Abs(angleDiff), rotationSpeed * Time.deltaTime);

            currentRotation += rotationStep;

            // Normalize angle to 0-360 range
            currentRotation = Mathf.Repeat(currentRotation, 360f);

            // Apply incremental rotation around local Y axis
            // This rotates around the object's own center, not world origin
            transform.Rotate(0, rotationStep, 0, Space.Self);
        }
    }

    /// <summary>
    /// Animate button press (down and up motion)
    /// </summary>
    void UpdatePressAnimation()
    {
        if (isPressed)
        {
            pressTimer += Time.deltaTime;

            float progress = pressTimer / pressDuration;

            if (progress <= 0.5f)
            {
                // First half: press down
                float t = progress * 2f; // 0→1
                knobVisual.localPosition = Vector3.Lerp(initialPosition, pressedPosition, t);
            }
            else
            {
                // Second half: release up
                float t = (progress - 0.5f) * 2f; // 0→1
                knobVisual.localPosition = Vector3.Lerp(pressedPosition, initialPosition, t);
            }

            if (progress >= 1f)
            {
                // Animation complete
                isPressed = false;
                knobVisual.localPosition = initialPosition;
                Debug.Log($"[RotaryEncoder] Button RELEASED");
            }
        }
    }

    /// <summary>
    /// Setup MQTT subscription to listen for physical encoder events
    /// </summary>
    void SetupPhysicalSync()
    {
        if (mqttManager == null)
        {
            Debug.LogWarning("[RotaryEncoder] MQTT Manager not assigned, physical sync disabled");
            return;
        }

        // Subscribe to MQTT messages
        mqttManager.OnMessageArrived += HandlePhysicalEncoderMessage;

        Debug.Log("[RotaryEncoder] Physical encoder sync enabled");
    }

    /// <summary>
    /// Handle MQTT messages from physical encoder
    /// Expected format: {"encoder": "rotation", "direction": "cw|ccw"}
    ///                  {"encoder": "button", "pressed": true}
    /// </summary>
    void HandlePhysicalEncoderMessage(mqttObj mqttObject)
    {
        if (!enablePhysicalSync) return;

        // Check if this is an encoder message
        string topic = mqttObject.topic;
        string message = mqttObject.msg;

        // Filter: only process messages from encoder topic
        if (!topic.Contains("encoder"))
        {
            return; // Not an encoder message
        }

        Debug.Log($"[RotaryEncoder] 🎮 Physical sync - Topic: {topic}");
        Debug.Log($"[RotaryEncoder] 🎮 Message: {message}");

        try
        {
            // Parse encoder events
            // Expected: {"encoder":"rotation","direction":"cw|ccw"}
            //       or: {"encoder":"button","pressed":true}

            if (message.Contains("\"rotation\""))
            {
                // Rotation event
                if (message.Contains("\"cw\""))
                {
                    Debug.Log("[RotaryEncoder] ➡️ Physical encoder rotated CLOCKWISE");
                    RotateClockwise();
                }
                else if (message.Contains("\"ccw\""))
                {
                    Debug.Log("[RotaryEncoder] ⬅️ Physical encoder rotated COUNTER-CLOCKWISE");
                    RotateCounterClockwise();
                }
            }
            else if (message.Contains("\"button\"") && message.Contains("true"))
            {
                // Button press event
                Debug.Log("[RotaryEncoder] 🔘 Physical button PRESSED");
                PressButton();
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[RotaryEncoder] ❌ Error parsing encoder message: {ex.Message}");
            Debug.LogError($"[RotaryEncoder] Topic was: {topic}");
            Debug.LogError($"[RotaryEncoder] Message was: {message}");
        }
    }

    /// <summary>
    /// Set encoder to specific step position
    /// </summary>
    public void SetStep(int step)
    {
        currentStep = Mathf.Clamp(step, 0, stepsPerRotation - 1);
        targetRotation = currentStep * degreesPerStep;

        Debug.Log($"[RotaryEncoder] Set to step {currentStep}");
    }

    /// <summary>
    /// Get current step position
    /// </summary>
    public int GetCurrentStep()
    {
        return currentStep;
    }

    void OnDestroy()
    {
        // Unsubscribe from MQTT events
        if (mqttManager != null)
        {
            mqttManager.OnMessageArrived -= HandlePhysicalEncoderMessage;
        }
    }

    // Gizmos for debugging in Scene view
    void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying) return;

        // Draw rotation indicator
        Gizmos.color = Color.cyan;
        Vector3 forward = transform.TransformDirection(Vector3.forward);
        Gizmos.DrawRay(transform.position, forward * 0.3f);

        // Draw step markers
        Gizmos.color = Color.yellow;
        for (int i = 0; i < stepsPerRotation; i++)
        {
            float angle = i * (360f / stepsPerRotation) * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle));
            Vector3 pos = transform.position + dir * 0.25f;
            Gizmos.DrawWireSphere(pos, 0.02f);
        }
    }
}
