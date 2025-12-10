using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

/// <summary>
/// NeoPixel Ring Controller for Digital Twin
/// Creates 24 point lights in a ring formation and controls them via MQTT
/// Mimics the behavior of the physical NeoPixel ring (24x SK6812 RGBW)
/// </summary>
public class NeoPixelRingController : MonoBehaviour
{
    [Header("NeoPixel Ring Configuration")]
    [SerializeField] private int ledCount = 24;
    [SerializeField] private float ringRadius = 1.0f;
    [SerializeField] private float lightIntensity = 2.0f;
    [SerializeField] private float lightRange = 0.5f;

    [Header("MQTT Manager Reference")]
    [Tooltip("Drag the mqttManager GameObject here from the scene")]
    [SerializeField] private mqttManager mqttManager;

    [Header("MQTT Configuration")]
    [SerializeField] private string mqttBaseTopic = "student/CASA0019/Gilang/studyspace";

    [Header("Room Selection")]
    [SerializeField] private string currentRoomId = "24380"; // Default room

    [Header("Display Mode")]
    [SerializeField] private bool timelineMode = true; // true = Bookings, false = Condition

    // Point lights array
    private Light[] pointLights;
    private GameObject[] lightObjects;

    // Timeline data (bookings)
    private bool[] slotBooked = new bool[24];
    private bool hasTimelineData = false;

    // Status data (condition)
    private float occupancy = 0f;
    private float noise = 0f;
    private float temperature = 0f;
    private float light = 0f;
    private string state = "neutral";
    private bool hasStatusData = false;

    // Animation state for status mode
    private int currentAttr = 0; // 0=occ, 1=noise, 2=temp, 3=light
    private int currentLEDCount = 0;
    private int targetLEDCount = 0;
    private float lastAttrChange = 0f;
    private float lastAnimStep = 0f;
    private const float ATTR_CHANGE_INTERVAL = 5f; // seconds
    private const float ANIM_STEP_INTERVAL = 0.12f; // seconds

    void Start()
    {
        CreateNeoPixelRing();

        // Test lights immediately to verify they work
        StartCoroutine(TestLightsOnStart());

        // Subscribe to MQTT manager events
        SetupMqttSubscription();
    }

    /// <summary>
    /// Setup MQTT subscription using mqttManager
    /// </summary>
    void SetupMqttSubscription()
    {
        if (mqttManager == null)
        {
            Debug.LogError("[MQTT] mqttManager reference is not set! Please drag the mqttManager GameObject to the Inspector.");
            return;
        }

        Debug.Log("[MQTT] Setting up MQTT subscription...");

        // Subscribe to message arrival event
        mqttManager.OnMessageArrived += HandleMqttMessage;

        // Subscribe to connection events
        mqttManager.OnConnectionSucceeded += HandleConnectionStatus;

        Debug.Log("[MQTT] Event handlers registered successfully");
    }

    /// <summary>
    /// Handle MQTT connection status changes
    /// </summary>
    void HandleConnectionStatus(bool connected)
    {
        Debug.Log($"[MQTT] Connection status changed: {(connected ? "CONNECTED" : "DISCONNECTED")}");

        if (connected)
        {
            Debug.Log("[MQTT] ✓ Successfully connected to MQTT broker");
        }
    }

    /// <summary>
    /// Handle incoming MQTT messages (called by mqttManager)
    /// </summary>
    void HandleMqttMessage(mqttObj mqttMessage)
    {
        string topic = mqttMessage.topic;
        string message = mqttMessage.msg;

        Debug.Log($"[MQTT RECEIVED!!!] ⭐⭐⭐ Topic: {topic}");
        Debug.Log($"[MQTT RECEIVED!!!] Message: {message}");

        // Process message on main thread (this is already on main thread thanks to mqttManager)
        ProcessMessage(topic, message);
    }    /// <summary>
         /// Test lights on startup to verify they work
         /// </summary>
    IEnumerator TestLightsOnStart()
    {
        Debug.Log("[TEST] Starting light test sequence...");

        yield return new WaitForSeconds(1f);

        // Test 1: Turn all lights red
        Debug.Log("[TEST] Test 1: All lights RED");
        for (int i = 0; i < ledCount; i++)
        {
            pointLights[i].color = Color.red;
            pointLights[i].intensity = lightIntensity;
        }

        yield return new WaitForSeconds(2f);

        // Test 2: Turn all lights green
        Debug.Log("[TEST] Test 2: All lights GREEN");
        for (int i = 0; i < ledCount; i++)
        {
            pointLights[i].color = Color.green;
            pointLights[i].intensity = lightIntensity;
        }

        yield return new WaitForSeconds(2f);

        // Test 3: Turn off all lights
        Debug.Log("[TEST] Test 3: All lights OFF, waiting for MQTT data...");
        ClearAllLights();

        Debug.Log("[TEST] Light test complete. Lights should now be controlled by MQTT.");
    }

    void Update()
    {
        // Handle status mode animation
        if (!timelineMode && hasStatusData)
        {
            UpdateStatusAnimation();
        }

        // Debug: Check light status every 5 seconds
        if (Time.frameCount % 300 == 0) // Every ~5 seconds at 60fps
        {
            int litCount = 0;
            for (int i = 0; i < ledCount; i++)
            {
                if (pointLights[i].intensity > 0)
                {
                    litCount++;
                }
            }

            Debug.Log($"[DEBUG] Mode: {(timelineMode ? "TIMELINE" : "STATUS")} | " +
                      $"Lit LEDs: {litCount}/{ledCount} | " +
                      $"Has Timeline Data: {hasTimelineData} | " +
                      $"Has Status Data: {hasStatusData}");
        }
    }
    void OnDestroy()
    {
        // Unsubscribe from mqttManager events
        if (mqttManager != null)
        {
            mqttManager.OnMessageArrived -= HandleMqttMessage;
            mqttManager.OnConnectionSucceeded -= HandleConnectionStatus;
        }
    }

    /// <summary>
    /// Creates 24 point lights arranged in a ring
    /// </summary>
    void CreateNeoPixelRing()
    {
        pointLights = new Light[ledCount];
        lightObjects = new GameObject[ledCount];

        for (int i = 0; i < ledCount; i++)
        {
            // Calculate position in ring (starting from top, going clockwise)
            float angle = (i * 360f / ledCount) * Mathf.Deg2Rad;
            Vector3 position = new Vector3(
                Mathf.Sin(angle) * ringRadius,
                0f,
                Mathf.Cos(angle) * ringRadius
            );

            // Create GameObject for each LED
            GameObject lightObj = new GameObject($"NeoPixel_{i:D2}");
            lightObj.transform.SetParent(transform);
            lightObj.transform.localPosition = position;
            lightObj.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // Rotate X=90° to point downward

            // Add Spot Light component
            Light pointLight = lightObj.AddComponent<Light>();
            pointLight.type = LightType.Spot;
            pointLight.intensity = lightIntensity;
            pointLight.range = lightRange;
            pointLight.spotAngle = 30f; // Spotlight cone angle
            pointLight.color = Color.black; // Start with lights off

            pointLights[i] = pointLight;
            lightObjects[i] = lightObj;
        }

        Debug.Log($"Created {ledCount} NeoPixels in ring formation");
    }

    /// <summary>
    /// Process MQTT message (called from main thread)
    /// </summary>
    void ProcessMessage(string topic, string message)
    {
        Debug.Log($"[MQTT PROCESS] Topic: {topic}");
        Debug.Log($"[MQTT PROCESS] Message Length: {message.Length} chars");
        Debug.Log($"[MQTT PROCESS] Message: {message}");

        // Parse JSON message
        if (topic.EndsWith("/timeline"))
        {
            Debug.Log("[MQTT] Processing TIMELINE message");
            ParseTimelineMessage(message);
        }
        else if (topic.EndsWith("/status"))
        {
            Debug.Log("[MQTT] Processing STATUS message");
            ParseStatusMessage(message);
        }
        else
        {
            Debug.LogWarning($"[MQTT] Unknown topic format: {topic}");
        }
    }

    /// <summary>
    /// Parse timeline (bookings) JSON message
    /// </summary>
    void ParseTimelineMessage(string json)
    {
        Debug.Log("[PARSE] Starting to parse timeline message...");
        try
        {
            // Simple JSON parsing for timeline array
            // Expected format: {"room": "24380", "timeline": ["free", "booked", "free", ...]}

            int timelineStart = json.IndexOf("\"timeline\"");
            if (timelineStart == -1)
            {
                Debug.LogWarning("[PARSE] 'timeline' key not found in JSON");
                Debug.LogWarning($"[PARSE] JSON was: {json}");
                return;
            }

            int arrayStart = json.IndexOf('[', timelineStart);
            if (arrayStart == -1)
            {
                Debug.LogWarning("[PARSE] Opening bracket '[' not found after 'timeline' key");
                return;
            }

            int arrayEnd = json.IndexOf(']', arrayStart);
            if (arrayEnd == -1)
            {
                Debug.LogWarning("[PARSE] Closing bracket ']' not found");
                return;
            }

            string arrayContent = json.Substring(arrayStart + 1, arrayEnd - arrayStart - 1);
            Debug.Log($"[PARSE] Array content extracted: {arrayContent.Substring(0, Mathf.Min(50, arrayContent.Length))}...");

            string[] slots = arrayContent.Split(',');
            Debug.Log($"[PARSE] Split into {slots.Length} slots");

            for (int i = 0; i < Mathf.Min(slots.Length, ledCount); i++)
            {
                string slot = slots[i].Trim().Replace("\"", "");
                slotBooked[i] = (slot == "booked");
            }

            Debug.Log($"[PARSE] ✓ Successfully parsed {Mathf.Min(slots.Length, ledCount)} timeline slots");

            hasTimelineData = true;

            // Always render timeline data when received, regardless of current mode
            Debug.Log($"[PARSE] Timeline mode is: {timelineMode}, calling RenderTimeline()...");
            RenderTimeline();
        }
        catch (Exception ex)
        {
            Debug.LogError("[PARSE ERROR] ❌ Error parsing timeline message: " + ex.Message);
            Debug.LogError($"[PARSE ERROR] Stack trace: {ex.StackTrace}");
            Debug.LogError($"[PARSE ERROR] JSON was: {json}");
        }
    }

    /// <summary>
    /// Parse status (condition) JSON message
    /// </summary>
    void ParseStatusMessage(string json)
    {
        Debug.Log("[PARSE] Starting to parse status message...");
        try
        {
            // Simple JSON parsing for status data
            // Expected format: {"room":"24380","occupancy":45.5,"noise":52.3,"temperature":22.1,"light":380.0,"state":"good"}

            occupancy = ExtractFloatValue(json, "occupancy");
            noise = ExtractFloatValue(json, "noise");
            temperature = ExtractFloatValue(json, "temperature");
            light = ExtractFloatValue(json, "light");
            state = ExtractStringValue(json, "state");

            Debug.Log($"[PARSE] Status parsed: occupancy={occupancy}, noise={noise}, temp={temperature}, light={light}, state={state}");

            hasStatusData = true;

            if (!timelineMode)
            {
                Debug.Log($"[PARSE] In status mode, computing attr target for attr {currentAttr}");
                ComputeAttrTarget(currentAttr);
                currentLEDCount = 0;
            }
            else
            {
                Debug.Log("[PARSE] In timeline mode, not rendering status");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("[PARSE ERROR] Error parsing status message: " + ex.Message);
            Debug.LogError($"[PARSE ERROR] JSON was: {json}");
        }
    }

    /// <summary>
    /// Extract float value from JSON string (simple parser)
    /// </summary>
    float ExtractFloatValue(string json, string key)
    {
        string searchKey = $"\"{key}\":";
        int startIndex = json.IndexOf(searchKey);
        if (startIndex == -1) return 0f;

        startIndex += searchKey.Length;
        int endIndex = json.IndexOfAny(new char[] { ',', '}' }, startIndex);

        string valueStr = json.Substring(startIndex, endIndex - startIndex).Trim();

        float result;
        if (float.TryParse(valueStr, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out result))
        {
            return result;
        }

        return 0f;
    }

    /// <summary>
    /// Extract string value from JSON string (simple parser)
    /// </summary>
    string ExtractStringValue(string json, string key)
    {
        string searchKey = $"\"{key}\":\"";
        int startIndex = json.IndexOf(searchKey);
        if (startIndex == -1)
        {
            Debug.LogWarning($"[PARSE] Key '{key}' not found in JSON");
            return "";
        }

        startIndex += searchKey.Length;
        int endIndex = json.IndexOf('\"', startIndex);

        if (endIndex == -1)
        {
            Debug.LogWarning($"[PARSE] Closing quote not found for key '{key}'");
            return "";
        }

        string value = json.Substring(startIndex, endIndex - startIndex);
        Debug.Log($"[PARSE] Extracted {key} = '{value}'");
        return value;
    }

    /// <summary>
    /// Render timeline mode (bookings visualization)
    /// </summary>
    void RenderTimeline()
    {
        Debug.Log($"[RENDER] Starting RenderTimeline, ledCount={ledCount}");

        int bookedCount = 0;
        int freeCount = 0;

        for (int i = 0; i < ledCount; i++)
        {
            if (i < slotBooked.Length)
            {
                // Red = booked, Green = free
                Color ledColor = slotBooked[i] ? Color.red : Color.green;
                pointLights[i].color = ledColor;
                pointLights[i].intensity = lightIntensity;

                if (slotBooked[i]) bookedCount++;
                else freeCount++;

                if (i < 3) // Log first 3 LEDs for debugging
                {
                    Debug.Log($"[RENDER] LED {i}: Color={ledColor}, Intensity={lightIntensity}, Booked={slotBooked[i]}");
                }
            }
            else
            {
                pointLights[i].intensity = 0f;
            }
        }

        Debug.Log($"[RENDER] Timeline rendered: {bookedCount} booked (red), {freeCount} free (green)");
    }

    /// <summary>
    /// Compute target LED count for status animation
    /// </summary>
    void ComputeAttrTarget(int attr)
    {
        switch (attr)
        {
            case 0: // Occupancy (0-100 → 0-24)
                targetLEDCount = Mathf.RoundToInt(occupancy / 4.2f);
                break;
            case 1: // Noise (30-80 → 0-24)
                targetLEDCount = Mathf.RoundToInt((noise - 30f) / 2.1f);
                break;
            case 2: // Temperature (17-29 → 0-24)
                targetLEDCount = Mathf.RoundToInt((temperature - 17f) * 2f);
                break;
            case 3: // Light (100-600 → 0-24)
                targetLEDCount = Mathf.RoundToInt((light - 100f) / 21f);
                break;
        }

        targetLEDCount = Mathf.Clamp(targetLEDCount, 0, ledCount);
    }

    /// <summary>
    /// Get color for current attribute
    /// </summary>
    Color GetAttrColor(int attr)
    {
        switch (attr)
        {
            case 0: // Occupancy → deep blue
                return new Color(0f, 0.08f, 1f);
            case 1: // Noise → warm yellow
                return Color.yellow;
            case 2: // Temperature → warm-ish green
                return new Color(0f, 1f, 0.08f);
            case 3: // Light → soft white
                return new Color(0.8f, 0.8f, 1f);
            default:
                return Color.white;
        }
    }

    /// <summary>
    /// Update status mode animation
    /// </summary>
    void UpdateStatusAnimation()
    {
        float currentTime = Time.time;

        // Rotate attribute every 5 seconds
        if (currentTime - lastAttrChange > ATTR_CHANGE_INTERVAL)
        {
            lastAttrChange = currentTime;
            currentAttr = (currentAttr + 1) % 4;
            ComputeAttrTarget(currentAttr);
            ClearAllLights();
            currentLEDCount = 0;
        }

        // Progressive fill animation
        if (currentTime - lastAnimStep > ANIM_STEP_INTERVAL)
        {
            lastAnimStep = currentTime;

            if (currentLEDCount < targetLEDCount)
            {
                pointLights[currentLEDCount].color = GetAttrColor(currentAttr);
                pointLights[currentLEDCount].intensity = lightIntensity;
                currentLEDCount++;
            }
        }
    }

    /// <summary>
    /// Clear all lights
    /// </summary>
    void ClearAllLights()
    {
        for (int i = 0; i < ledCount; i++)
        {
            pointLights[i].intensity = 0f;
        }
    }

    /// <summary>
    /// Switch display mode (Timeline/Status)
    /// </summary>
    public void ToggleMode()
    {
        timelineMode = !timelineMode;

        Debug.Log($"Mode switched to: {(timelineMode ? "BOOKINGS" : "CONDITION")}");

        if (timelineMode && hasTimelineData)
        {
            RenderTimeline();
        }
        else if (!timelineMode && hasStatusData)
        {
            ComputeAttrTarget(currentAttr);
            ClearAllLights();
            currentLEDCount = 0;
        }
        else
        {
            ClearAllLights();
        }
    }

    /// <summary>
    /// Change to a different room
    /// </summary>
    public void ChangeRoom(string newRoomId)
    {
        if (currentRoomId == newRoomId) return;

        currentRoomId = newRoomId;
        hasTimelineData = false;
        hasStatusData = false;
        ClearAllLights();

        Debug.Log($"Changed to room: {newRoomId}");
        Debug.Log("[MQTT] Note: Room change requires updating topics in mqttManager component");
    }

    // Public methods for inspector/external control
    public void SetTimelineMode() => timelineMode = true;
    public void SetStatusMode() => timelineMode = false;
    public string GetCurrentRoom() => currentRoomId;
    public bool IsTimelineMode() => timelineMode;

    /// <summary>
    /// Manual test: Create fake timeline data and render
    /// </summary>
    [ContextMenu("Test: Render Fake Timeline")]
    public void TestRenderFakeTimeline()
    {
        Debug.Log("[MANUAL TEST] Creating fake timeline data...");

        // Create a test pattern: alternating booked/free
        for (int i = 0; i < ledCount; i++)
        {
            slotBooked[i] = (i % 2 == 0);
        }

        hasTimelineData = true;
        timelineMode = true;
        RenderTimeline();

        Debug.Log("[MANUAL TEST] Fake timeline rendered");
    }

    /// <summary>
    /// Manual test: Create fake status data and render
    /// </summary>
    [ContextMenu("Test: Render Fake Status")]
    public void TestRenderFakeStatus()
    {
        Debug.Log("[MANUAL TEST] Creating fake status data...");

        occupancy = 75f;
        noise = 55f;
        temperature = 22f;
        light = 380f;
        state = "good";

        hasStatusData = true;
        timelineMode = false;
        currentAttr = 0;

        ComputeAttrTarget(currentAttr);
        ClearAllLights();
        currentLEDCount = 0;

        Debug.Log("[MANUAL TEST] Fake status data created, animation will start");
    }

    /// <summary>
    /// Manual test: Turn all lights red
    /// </summary>
    [ContextMenu("Test: All Lights Red")]
    public void TestAllRed()
    {
        Debug.Log("[MANUAL TEST] Setting all lights to RED");
        for (int i = 0; i < ledCount; i++)
        {
            pointLights[i].color = Color.red;
            pointLights[i].intensity = lightIntensity;
        }
    }

    /// <summary>
    /// Manual test: Turn all lights green
    /// </summary>
    [ContextMenu("Test: All Lights Green")]
    public void TestAllGreen()
    {
        Debug.Log("[MANUAL TEST] Setting all lights to GREEN");
        for (int i = 0; i < ledCount; i++)
        {
            pointLights[i].color = Color.green;
            pointLights[i].intensity = lightIntensity;
        }
    }


}
