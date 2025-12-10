using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Creates a clock dial showing hours 9-20 (9am to 8pm)
/// Attach to a Canvas GameObject set to World Space
/// The dial rotates 90 degrees: 9 at top, 12 at right, 15 at bottom, 18 at left
/// </summary>
public class TimeDialController : MonoBehaviour
{
    [Header("Dial Settings")]
    [Tooltip("Radius of the time dial in world units")]
    public float dialRadius = 0.15f;

    [Tooltip("Text distance from center (multiplier of dialRadius)")]
    public float textDistanceMultiplier = 1.3f;

    [Tooltip("Font size for hour numbers")]
    public int fontSize = 28;

    [Tooltip("Font size for major hours (9, 12, 15, 18)")]
    public int majorHourFontSize = 36;

    [Tooltip("Color of hour numbers")]
    public Color textColor = new Color(0.9f, 0.9f, 0.9f);

    [Tooltip("Show tick marks between hours")]
    public bool showTickMarks = true;

    [Tooltip("Tick mark length")]
    public float tickLength = 0.025f;

    [Tooltip("Tick mark width")]
    public float tickWidth = 3f;

    [Header("Visual Enhancement")]
    [Tooltip("Show outer circle ring")]
    public bool showOuterRing = true;

    [Tooltip("Outer ring thickness")]
    public float ringThickness = 3f;

    [Tooltip("Ring color")]
    public Color ringColor = new Color(0.3f, 0.6f, 1f, 0.8f);

    [Tooltip("Show center dot")]
    public bool showCenterDot = true;

    [Tooltip("Center dot size")]
    public float centerDotSize = 8f;



    private Canvas canvas;
    private RectTransform canvasRect;
    private GameObject dialContainer;

    void Start()
    {
        canvas = GetComponent<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("[TimeDialController] Canvas component not found! Please attach to a Canvas GameObject.");
            enabled = false;
            return;
        }

        if (canvas.renderMode != RenderMode.WorldSpace)
        {
            Debug.LogWarning("[TimeDialController] Canvas should be set to World Space render mode.");
        }

        canvasRect = canvas.GetComponent<RectTransform>();

        CreateDial();

        Debug.Log("[TimeDialController] Time dial created successfully (9-20 hours, rotated 90°)");
    }

    void CreateDial()
    {
        // Create container for all dial elements
        dialContainer = new GameObject("DialContainer");
        dialContainer.transform.SetParent(transform, false);
        RectTransform containerRect = dialContainer.AddComponent<RectTransform>();
        containerRect.anchorMin = new Vector2(0.5f, 0.5f);
        containerRect.anchorMax = new Vector2(0.5f, 0.5f);
        containerRect.sizeDelta = new Vector2(dialRadius * 2 * 100, dialRadius * 2 * 100);

        // Hours to display: 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20
        int[] hours = { 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20 };

        for (int i = 0; i < hours.Length; i++)
        {
            int hour = hours[i];

            // Calculate angle: start from top (9 o'clock) and go clockwise
            // Normal clock: 12 is at 90°, 3 is at 0°, 6 is at 270°, 9 is at 180°
            // Our rotated clock: 9 at top (90°), 12 at right (0°), 15 at bottom (270°), 18 at left (180°)
            // Formula: angle = 90° - (hour - 9) * 30°
            float angle = 90f - (i * 30f); // 30 degrees per hour for 12 hours
            float radians = angle * Mathf.Deg2Rad;

            // Position on circle - farther out for text
            float textRadius = dialRadius * textDistanceMultiplier;
            Vector2 position = new Vector2(
                Mathf.Cos(radians) * textRadius * 100,
                Mathf.Sin(radians) * textRadius * 100
            );

            // Check if this is a major hour (9, 12, 15, 18)
            bool isMajorHour = (hour == 9 || hour == 12 || hour == 15 || hour == 18);

            // Create hour text
            CreateHourText(hour, position, dialContainer.transform, isMajorHour);

            // Create tick marks if enabled
            if (showTickMarks)
            {
                // Create a tick mark halfway between this hour and the next
                float tickAngle = angle - 15f; // Halfway between hours
                CreateTickMark(tickAngle, dialContainer.transform);
            }
        }

        // Create visual enhancements
        if (showOuterRing)
        {
            CreateOuterRing();
        }

        if (showCenterDot)
        {
            CreateCenterDot();
        }
    }

    void CreateHourText(int hour, Vector2 position, Transform parent, bool isMajorHour = false)
    {
        GameObject hourObj = new GameObject($"Hour_{hour}");
        hourObj.transform.SetParent(parent, false);

        RectTransform rectTransform = hourObj.AddComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.sizeDelta = new Vector2(60, 60);
        rectTransform.anchoredPosition = position;

        int currentFontSize = isMajorHour ? majorHourFontSize : fontSize;

        // Try to use TextMeshPro first, fallback to Unity UI Text
        TextMeshProUGUI tmpText = hourObj.AddComponent<TextMeshProUGUI>();
        if (tmpText != null)
        {
            tmpText.text = hour.ToString();
            tmpText.fontSize = currentFontSize;
            tmpText.color = textColor;
            tmpText.alignment = TextAlignmentOptions.Center;
            tmpText.fontStyle = FontStyles.Bold;
        }
        else
        {
            // Fallback to regular UI Text
            Text text = hourObj.AddComponent<Text>();
            text.text = hour.ToString();
            text.fontSize = currentFontSize;
            text.color = textColor;
            text.alignment = TextAnchor.MiddleCenter;
            text.fontStyle = FontStyle.Bold;
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }
    }

    void CreateTickMark(float angle, Transform parent)
    {
        GameObject tickObj = new GameObject($"Tick_{angle}");
        tickObj.transform.SetParent(parent, false);

        RectTransform rectTransform = tickObj.AddComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.sizeDelta = new Vector2(tickLength * 100, tickWidth); // Swapped width and height for horizontal line

        // Position at the edge of dial
        float radians = angle * Mathf.Deg2Rad;
        float innerRadius = (dialRadius - tickLength / 2) * 100;
        Vector2 position = new Vector2(
            Mathf.Cos(radians) * innerRadius,
            Mathf.Sin(radians) * innerRadius
        );
        rectTransform.anchoredPosition = position;
        rectTransform.rotation = Quaternion.Euler(0, 0, angle); // Changed rotation to align with radius

        Image image = tickObj.AddComponent<Image>();
        image.color = new Color(textColor.r, textColor.g, textColor.b, 0.7f);

        // Rounded corners
        image.sprite = CreateRoundedRectSprite();
    }

    void CreateOuterRing()
    {
        GameObject ringObj = new GameObject("OuterRing");
        ringObj.transform.SetParent(dialContainer.transform, false);

        RectTransform rectTransform = ringObj.AddComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.sizeDelta = new Vector2(dialRadius * 2 * 100, dialRadius * 2 * 100);

        Image image = ringObj.AddComponent<Image>();
        image.sprite = CreateRingSprite();
        image.color = ringColor;
    }

    void CreateCenterDot()
    {
        GameObject dotObj = new GameObject("CenterDot");
        dotObj.transform.SetParent(dialContainer.transform, false);

        RectTransform rectTransform = dotObj.AddComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.sizeDelta = new Vector2(centerDotSize, centerDotSize);

        Image image = dotObj.AddComponent<Image>();
        image.sprite = CreateCircleSprite();
        image.color = ringColor;
    }

    Sprite CreateCircleSprite()
    {
        int size = 32;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color[] colors = new Color[size * size];

        Vector2 center = new Vector2(size / 2f, size / 2f);
        float radius = size / 2f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                colors[y * size + x] = distance <= radius ? Color.white : Color.clear;
            }
        }

        texture.SetPixels(colors);
        texture.Apply();

        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    Sprite CreateRingSprite()
    {
        int size = 128;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color[] colors = new Color[size * size];

        Vector2 center = new Vector2(size / 2f, size / 2f);
        float outerRadius = size / 2f;
        float innerRadius = outerRadius - ringThickness;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                bool inRing = distance <= outerRadius && distance >= innerRadius;

                // Anti-aliasing
                float alpha = 1f;
                if (inRing)
                {
                    float outerEdge = Mathf.Clamp01((outerRadius - distance) * 2f);
                    float innerEdge = Mathf.Clamp01((distance - innerRadius) * 2f);
                    alpha = Mathf.Min(outerEdge, innerEdge);
                }

                colors[y * size + x] = inRing ? new Color(1, 1, 1, alpha) : Color.clear;
            }
        }

        texture.SetPixels(colors);
        texture.Apply();

        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    Sprite CreateRoundedRectSprite()
    {
        int width = 16;
        int height = 32;
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        Color[] colors = new Color[width * height];

        float cornerRadius = width / 2f;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                bool isInside = true;
                float alpha = 1f;

                // Top rounded corner
                if (y > height - cornerRadius)
                {
                    float dy = y - (height - cornerRadius);
                    float dx = x - width / 2f;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    if (distance > cornerRadius)
                    {
                        isInside = false;
                    }
                    else
                    {
                        alpha = Mathf.Clamp01((cornerRadius - distance + 0.5f));
                    }
                }

                // Bottom rounded corner
                if (y < cornerRadius)
                {
                    float dy = y - cornerRadius;
                    float dx = x - width / 2f;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    if (distance > cornerRadius)
                    {
                        isInside = false;
                    }
                    else
                    {
                        alpha = Mathf.Clamp01((cornerRadius - distance + 0.5f));
                    }
                }

                colors[y * width + x] = isInside ? new Color(1, 1, 1, alpha) : Color.clear;
            }
        }

        texture.SetPixels(colors);
        texture.Apply();

        return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f));
    }
}
