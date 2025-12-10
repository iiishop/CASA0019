using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Concentric Condition Display for World Space Canvas
/// Displays room condition data as concentric circles radiating from top-right
/// Attach to a Canvas GameObject set to World Space
/// View from top-down perspective
/// </summary>
public class ConcentricConditionDisplay : MonoBehaviour
{
    [Header("Twin Object Reference")]
    [Tooltip("The digital twin object (for positioning and radius reference)")]
    public Transform twinObject;

    [Tooltip("Radius of the digital twin object")]
    public float twinRadius = 0.5f;

    [Header("Circle Settings")]
    [Tooltip("Number of concentric circles (one per metric)")]
    public int circleCount = 4;

    [Tooltip("Gap between each concentric circle")]
    public float circleGap = 0.08f;

    [Tooltip("Thickness of each circle ring")]
    public float ringThickness = 0.06f;

    [Tooltip("Height offset between each ring (Z-axis)")]
    public float ringHeightStep = 0.01f;

    [Tooltip("Text height offset above ring")]
    public float textHeightOffset = 0.02f;

    [Tooltip("Horizontal distance from ring edge to label (top)")]
    public float labelDistance = 0.05f;

    [Tooltip("Horizontal distance from ring edge to value (right)")]
    public float valueDistance = 0.1f;

    [Header("Visual Settings")]
    [Tooltip("Font size for metric labels")]
    public int labelFontSize = 12;

    [Tooltip("Font size for metric values")]
    public int valueFontSize = 16;

    [Tooltip("Label color")]
    public Color labelColor = new Color(0.9f, 0.9f, 0.9f);

    [Tooltip("Value color")]
    public Color valueColor = Color.white;

    [Header("Metric Colors")]
    [Tooltip("Color for Occupancy ring")]
    public Color occupancyColor = new Color(0.2f, 0.6f, 1f); // Blue

    [Tooltip("Color for Noise ring")]
    public Color noiseColor = new Color(1f, 0.4f, 0.2f); // Orange

    [Tooltip("Color for Temperature ring")]
    public Color temperatureColor = new Color(1f, 0.2f, 0.4f); // Red

    [Tooltip("Color for Light ring")]
    public Color lightColor = new Color(1f, 0.9f, 0.2f); // Yellow

    [Header("MQTT Integration")]
    [Tooltip("Reference to MQTT Manager")]
    public mqttManager mqttManager;

    [Tooltip("Current room ID to display")]
    public string currentRoomId = "24380";

    [Header("Current Values")]
    public float occupancy = 0f;
    public float noise = 0f;
    public float temperature = 0f;
    public float light = 0f;
    public string state = "neutral";

    private Canvas canvas;
    private GameObject displayContainer;

    // UI Elements for each metric
    private class MetricDisplay
    {
        public GameObject container;
        public Image ringImage;
        public Image fillImage;
        public TextMeshProUGUI labelText;
        public TextMeshProUGUI valueText;
        public float maxValue;
        public string unit;
    }

    private MetricDisplay[] metricDisplays;
    private string[] metricNames = { "Occupancy", "Noise", "Temperature", "Light" };
    private string[] metricUnits = { "%", "dB", "°C", "lux" };
    private float[] metricMaxValues = { 100f, 100f, 35f, 1000f };
    private Color[] metricColors;

    private bool hasData = false;

    void Start()
    {
        canvas = GetComponent<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("[ConcentricDisplay] Canvas component not found!");
            enabled = false;
            return;
        }

        if (canvas.renderMode != RenderMode.WorldSpace)
        {
            Debug.LogWarning("[ConcentricDisplay] Canvas should be set to World Space render mode.");
        }

        // Initialize metric colors array
        metricColors = new Color[] { occupancyColor, noiseColor, temperatureColor, lightColor };

        // Auto-find MQTT Manager if not assigned
        if (mqttManager == null)
        {
            GameObject mqttObj = GameObject.FindGameObjectWithTag("mqttmanager");
            if (mqttObj != null)
            {
                mqttManager = mqttObj.GetComponent<mqttManager>();
                Debug.Log("[ConcentricDisplay] 🔍 Auto-found MQTT Manager");
            }
        }

        // Subscribe to MQTT
        if (mqttManager != null)
        {
            mqttManager.OnMessageArrived += HandleMQTTMessage;
            Debug.Log("[ConcentricDisplay] 📡 Subscribed to MQTT messages");
        }

        CreateConcentricDisplay();

        Debug.Log("[ConcentricDisplay] ✓ Concentric condition display initialized");
    }

    void OnDestroy()
    {
        if (mqttManager != null)
        {
            mqttManager.OnMessageArrived -= HandleMQTTMessage;
        }
    }

    void CreateConcentricDisplay()
    {
        // Create main container
        displayContainer = new GameObject("ConcentricDisplay");
        displayContainer.transform.SetParent(transform, false);

        RectTransform containerRect = displayContainer.AddComponent<RectTransform>();
        containerRect.anchorMin = new Vector2(0.5f, 0.5f);
        containerRect.anchorMax = new Vector2(0.5f, 0.5f);
        containerRect.sizeDelta = new Vector2(1000, 1000);

        // Initialize metric displays array
        metricDisplays = new MetricDisplay[circleCount];

        // Create concentric circles from outer to inner
        for (int i = 0; i < circleCount; i++)
        {
            metricDisplays[i] = CreateMetricCircle(i);
        }

        // Update initial display
        UpdateDisplay();
    }

    MetricDisplay CreateMetricCircle(int index)
    {
        MetricDisplay display = new MetricDisplay();
        display.maxValue = metricMaxValues[index];
        display.unit = metricUnits[index];

        // Calculate radius for this circle
        // Example: radius=5, thickness=1, gap=1
        // Ring 0: inner=5, outer=6
        // Ring 1: inner=7, outer=8
        // Ring 2: inner=9, outer=10
        // Ring 3: inner=11, outer=12
        // Formula: inner = twinRadius + index * (ringThickness + circleGap)
        //          outer = inner + ringThickness

        float innerRadius = twinRadius + index * (ringThickness + circleGap);
        float outerRadius = innerRadius + ringThickness;
        float centerRadius = (innerRadius + outerRadius) / 2f;

        // Convert to canvas units (multiply by 100)
        float innerRadiusCanvas = innerRadius * 100;
        float outerRadiusCanvas = outerRadius * 100;
        float centerRadiusCanvas = centerRadius * 100;
        float ringThicknessCanvas = ringThickness * 100;

        // Create container for this metric (centered on twin)
        display.container = new GameObject($"Metric_{metricNames[index]}");
        display.container.transform.SetParent(displayContainer.transform, false);

        RectTransform containerRect = display.container.AddComponent<RectTransform>();
        containerRect.anchorMin = new Vector2(0.5f, 0.5f);
        containerRect.anchorMax = new Vector2(0.5f, 0.5f);
        containerRect.sizeDelta = new Vector2(outerRadiusCanvas * 2, outerRadiusCanvas * 2);
        containerRect.anchoredPosition = Vector2.zero;

        // Set Z position for layering - outer rings are higher (away from twin surface)
        Vector3 localPos = containerRect.localPosition;
        localPos.z = index * ringHeightStep; // Positive Z = away from surface
        containerRect.localPosition = localPos;

        Debug.Log($"[ConcentricDisplay] Ring {index} ({metricNames[index]}) set to Z={localPos.z}");

        // Create outer ring (background)
        // Key: Image size must match the actual ring dimensions to maintain consistent thickness
        GameObject ringObj = new GameObject("Ring");
        ringObj.transform.SetParent(display.container.transform, false);

        RectTransform ringRect = ringObj.AddComponent<RectTransform>();
        ringRect.anchorMin = new Vector2(0.5f, 0.5f);
        ringRect.anchorMax = new Vector2(0.5f, 0.5f);
        // Size the image based on outer diameter, but sprite will maintain consistent ring thickness
        ringRect.sizeDelta = new Vector2(outerRadiusCanvas * 2, outerRadiusCanvas * 2);
        ringRect.anchoredPosition = Vector2.zero;

        display.ringImage = ringObj.AddComponent<Image>();
        display.ringImage.sprite = CreateRingSprite(ringThicknessCanvas, outerRadiusCanvas);
        display.ringImage.color = new Color(metricColors[index].r, metricColors[index].g, metricColors[index].b, 0.3f);

        // Create fill indicator (arc)
        GameObject fillObj = new GameObject("Fill");
        fillObj.transform.SetParent(display.container.transform, false);

        RectTransform fillRect = fillObj.AddComponent<RectTransform>();
        fillRect.anchorMin = new Vector2(0.5f, 0.5f);
        fillRect.anchorMax = new Vector2(0.5f, 0.5f);
        fillRect.sizeDelta = new Vector2(outerRadiusCanvas * 2, outerRadiusCanvas * 2);
        fillRect.anchoredPosition = Vector2.zero;

        display.fillImage = fillObj.AddComponent<Image>();
        display.fillImage.sprite = CreateRingSprite(ringThicknessCanvas, outerRadiusCanvas);
        display.fillImage.color = metricColors[index];
        display.fillImage.fillMethod = Image.FillMethod.Radial360;
        display.fillImage.fillOrigin = (int)Image.Origin360.Top;
        display.fillImage.fillAmount = 0f;
        display.fillImage.type = Image.Type.Filled;

        // Create label text (metric name) - positioned at top of circle
        GameObject labelObj = new GameObject("Label");
        labelObj.transform.SetParent(display.container.transform, false);

        RectTransform labelRect = labelObj.AddComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0.5f, 1f);
        labelRect.anchorMax = new Vector2(0.5f, 1f);
        labelRect.pivot = new Vector2(0.5f, 0f);
        labelRect.sizeDelta = new Vector2(150, 25);
        // Position label with adjustable distance from ring edge
        labelRect.anchoredPosition = new Vector2(0, labelDistance * 100);

        // Raise text above ring (further away from surface)
        Vector3 labelLocalPos = labelRect.localPosition;
        labelLocalPos.z = textHeightOffset;
        labelRect.localPosition = labelLocalPos;

        display.labelText = labelObj.AddComponent<TextMeshProUGUI>();
        display.labelText.text = metricNames[index];
        display.labelText.fontSize = labelFontSize;
        display.labelText.color = labelColor;
        display.labelText.alignment = TextAlignmentOptions.Center;
        display.labelText.fontStyle = FontStyles.Bold;

        // Create value text - positioned at right of circle
        GameObject valueObj = new GameObject("Value");
        valueObj.transform.SetParent(display.container.transform, false);

        RectTransform valueRect = valueObj.AddComponent<RectTransform>();
        valueRect.anchorMin = new Vector2(1f, 0.5f);
        valueRect.anchorMax = new Vector2(1f, 0.5f);
        valueRect.pivot = new Vector2(0f, 0.5f);
        valueRect.sizeDelta = new Vector2(100, 30);
        // Position value with adjustable distance from ring edge
        valueRect.anchoredPosition = new Vector2(valueDistance * 100, 0);

        // Raise text above ring (further away from surface)
        Vector3 valueLocalPos = valueRect.localPosition;
        valueLocalPos.z = textHeightOffset;
        valueRect.localPosition = valueLocalPos;

        display.valueText = valueObj.AddComponent<TextMeshProUGUI>();
        display.valueText.text = $"--{display.unit}";
        display.valueText.fontSize = valueFontSize;
        display.valueText.color = valueColor;
        display.valueText.alignment = TextAlignmentOptions.Left;
        display.valueText.fontStyle = FontStyles.Bold;

        return display;
    }

    void HandleMQTTMessage(mqttObj mqttObject)
    {
        string topic = mqttObject.topic;
        string message = mqttObject.msg;

        // Handle current room change
        if (topic.EndsWith("/current"))
        {
            string newRoomId = ExtractStringValue(message, "current_room");
            if (!string.IsNullOrEmpty(newRoomId) && newRoomId != currentRoomId)
            {
                currentRoomId = newRoomId;
                hasData = false;
                Debug.Log($"[ConcentricDisplay] 🔄 Room changed to {currentRoomId}");
                UpdateDisplay();
            }
            return;
        }

        // Handle status message
        if (topic.Contains("/status"))
        {
            string roomId = ExtractStringValue(message, "room");
            if (roomId == currentRoomId)
            {
                occupancy = ExtractFloatValue(message, "occupancy");
                noise = ExtractFloatValue(message, "noise");
                temperature = ExtractFloatValue(message, "temperature");
                light = ExtractFloatValue(message, "light");
                state = ExtractStringValue(message, "state");

                hasData = true;

                Debug.Log($"[ConcentricDisplay] 📊 Updated: Occ={occupancy:F1}% Noise={noise:F1}dB Temp={temperature:F1}°C Light={light:F0}lux");

                UpdateDisplay();
            }
        }
    }

    void UpdateDisplay()
    {
        if (!hasData || metricDisplays == null) return;

        float[] values = { occupancy, noise, temperature, light };

        for (int i = 0; i < metricDisplays.Length && i < values.Length; i++)
        {
            MetricDisplay display = metricDisplays[i];
            float value = values[i];
            float fillAmount = Mathf.Clamp01(value / display.maxValue);

            // Update fill amount
            if (display.fillImage != null)
            {
                display.fillImage.fillAmount = fillAmount;
            }

            // Update value text
            if (display.valueText != null)
            {
                display.valueText.text = $"{value:F1}{display.unit}";
            }
        }
    }

    Sprite CreateRingSprite(float actualThickness, float actualRadius)
    {
        int size = 512; // High resolution for smooth edges
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color[] colors = new Color[size * size];

        Vector2 center = new Vector2(size / 2f, size / 2f);
        float outerRadius = size / 2f;

        // Calculate inner radius based on actual thickness-to-radius ratio
        // This ensures consistent visual thickness across all rings
        float thicknessRatio = actualThickness / actualRadius;
        float innerRadius = outerRadius * (1f - thicknessRatio);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                bool inRing = distance <= outerRadius && distance >= innerRadius;

                // Smooth anti-aliasing
                float alpha = 0f;
                if (distance <= outerRadius && distance >= innerRadius)
                {
                    // Smooth transition at edges
                    float outerEdge = Mathf.SmoothStep(0f, 1f, (outerRadius - distance + 1f));
                    float innerEdge = Mathf.SmoothStep(0f, 1f, (distance - innerRadius + 1f));
                    alpha = Mathf.Min(outerEdge, innerEdge);
                }

                colors[y * size + x] = alpha > 0 ? new Color(1, 1, 1, alpha) : Color.clear;
            }
        }

        texture.SetPixels(colors);
        texture.Apply();

        // Use Bilinear filtering for smooth edges
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;

        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    // Helper parsing methods

    float ExtractFloatValue(string json, string key)
    {
        try
        {
            int keyIndex = json.IndexOf($"\"{key}\"");
            if (keyIndex < 0) return 0f;

            int colonIndex = json.IndexOf(':', keyIndex);
            int commaIndex = json.IndexOf(',', colonIndex);
            int braceIndex = json.IndexOf('}', colonIndex);

            int endIndex = (commaIndex > 0 && commaIndex < braceIndex) ? commaIndex : braceIndex;
            string valueStr = json.Substring(colonIndex + 1, endIndex - colonIndex - 1).Trim();

            return float.Parse(valueStr);
        }
        catch
        {
            return 0f;
        }
    }

    string ExtractStringValue(string json, string key)
    {
        try
        {
            int keyIndex = json.IndexOf($"\"{key}\"");
            if (keyIndex < 0) return "";

            int colonIndex = json.IndexOf(':', keyIndex);
            int quoteStart = json.IndexOf('"', colonIndex);
            int quoteEnd = json.IndexOf('"', quoteStart + 1);

            return json.Substring(quoteStart + 1, quoteEnd - quoteStart - 1);
        }
        catch
        {
            return "";
        }
    }

    /// <summary>
    /// Public method to manually update values (for testing)
    /// </summary>
    public void SetValues(float occ, float noi, float temp, float lit)
    {
        occupancy = occ;
        noise = noi;
        temperature = temp;
        light = lit;
        hasData = true;
        UpdateDisplay();
    }
}
