using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// TFT ST7735 Display Controller - Digital Twin
/// Mimics the physical 128x160 TFT display showing room information
/// 
/// Two Display Modes (toggled via MQTT button press):
/// 1. Bookings Mode: Shows room details + booking percentage
/// 2. Condition Mode: Shows emoji expression based on room state
/// 
/// MQTT Driven:
/// - Subscribes to encoder button messages to toggle mode
/// - Subscribes to timeline messages for booking data
/// - Subscribes to status messages for condition data
/// </summary>
public class TFTDisplayController : MonoBehaviour
{
    [Header("Display Mode")]
    [Tooltip("Current display mode: true = Bookings, false = Condition")]
    public bool timelineMode = true;

    [Header("UI References - Bookings Mode")]
    [Tooltip("TextMeshPro for header (blue bar at top)")]
    public TextMeshProUGUI headerText;

    [Tooltip("TextMeshPro for room details (middle section)")]
    public TextMeshProUGUI roomDetailsText;

    [Tooltip("TextMeshPro for booking percentage (bottom, large yellow text)")]
    public TextMeshProUGUI bookingPercentText;

    [Tooltip("Background Image for header bar")]
    public Image headerBackground;

    [Header("UI References - Condition Mode")]
    [Tooltip("Image component for emoji/expression display")]
    public Image emojiImage;

    [Tooltip("TextMeshPro for state caption at bottom")]
    public TextMeshProUGUI stateCaptionText;

    [Tooltip("Background Image for caption bar")]
    public Image captionBackground;

    [Header("Emoji Sprites")]
    [Tooltip("Assign sprites for each state: perfect, good, calm, neutral, busy, noisy, warm, cold, dim, overloaded")]
    public Sprite spritePerfect;
    public Sprite spriteGood;
    public Sprite spriteCalm;
    public Sprite spriteNeutral;
    public Sprite spriteBusy;
    public Sprite spriteNoisy;
    public Sprite spriteWarm;
    public Sprite spriteCold;
    public Sprite spriteDim;
    public Sprite spriteOverloaded;

    [Header("MQTT Integration")]
    [Tooltip("Reference to MQTT Manager")]
    public mqttManager mqttManager;

    [Header("Room Data")]
    [Tooltip("Current selected room index (0-4)")]
    public int currentRoomIndex = 0;

    // Room metadata
    private readonly string[] ROOM_IDS = { "24380", "24381", "24382", "24546", "24547" };
    private readonly string[] ROOM_NAMES = { "Pod 216", "Pod 217", "Group 218", "Pod 212A", "Pod 212B" };

    private readonly string[] ROOM_DETAILS = {
        "Single Study Pod 216\nCapacity: 1\nFacilities: Plug, PC,\nheight adj. desk\nBuilding: UCL East\nLibrary 2nd Floor\nFeatures:\nLaptop charging\nEnclosed pod",
        "Single Study Pod 217\nCapacity: 1\nFacilities: Plug, PC,\nheight adj. desk\nBuilding: UCL East\nLibrary 2nd Floor\nFeatures:\nLaptop charging\nEnclosed pod",
        "Group Study Room 218\nCapacity: 6\nFacilities: Plug,\nmonitor for laptop\nBuilding: UCL East\nLibrary 2nd Floor\nFeatures:\nLaptop charging\nEnclosed room",
        "Single Study Pod 212A\nCapacity: 1\nFacilities: Plug\nBuilding: UCL East\nLibrary 2nd Floor\nFeatures:\nLaptop charging\nEnclosed pod",
        "Single Study Pod 212B\nCapacity: 1\nFacilities: Plug\nBuilding: UCL East\nLibrary 2nd Floor\nFeatures:\nLaptop charging\nEnclosed pod"
    };

    // Room data storage
    private class RoomData
    {
        public bool hasTimeline = false;
        public int timelineLen = 0;
        public bool[] slotBooked = new bool[24];

        public bool hasStatus = false;
        public float occupancy = 0;
        public float noise = 0;
        public float temperature = 0;
        public float light = 0;
        public string state = "neutral";
    }

    private RoomData[] rooms = new RoomData[5];

    void Start()
    {
        // Initialize room data
        for (int i = 0; i < 5; i++)
        {
            rooms[i] = new RoomData();
        }

        // Subscribe to MQTT messages
        if (mqttManager != null)
        {
            mqttManager.OnMessageArrived += HandleMQTTMessage;
            Debug.Log("[TFTDisplay] 📺 Subscribed to MQTT messages");
        }
        else
        {
            Debug.LogError("[TFTDisplay] ⚠️ MQTT Manager reference not set!");
        }

        // Initial display
        UpdateDisplay();
    }

    void OnDestroy()
    {
        // Unsubscribe from MQTT
        if (mqttManager != null)
        {
            mqttManager.OnMessageArrived -= HandleMQTTMessage;
        }
    }

    /// <summary>
    /// Handle incoming MQTT messages
    /// </summary>
    void HandleMQTTMessage(mqttObj mqttObject)
    {
        string topic = mqttObject.topic;
        string message = mqttObject.msg;

        Debug.Log($"[TFTDisplay] 📨 MQTT: {topic} = {message}");

        // Handle encoder button press (mode toggle)
        if (topic.Contains("encoder"))
        {
            if (message.Contains("\"button\"") && message.Contains("\"pressed\":true"))
            {
                ToggleMode();
            }
            return;
        }

        // Handle timeline data (bookings)
        if (topic.Contains("timeline"))
        {
            HandleTimelineMessage(topic, message);
        }

        // Handle status data (condition)
        if (topic.Contains("status"))
        {
            HandleStatusMessage(topic, message);
        }
    }

    /// <summary>
    /// Parse timeline/booking data from MQTT
    /// </summary>
    void HandleTimelineMessage(string topic, string message)
    {
        try
        {
            // Extract room ID from topic
            string roomId = ExtractRoomIdFromTopic(topic);
            int roomIndex = GetRoomIndex(roomId);
            if (roomIndex < 0) return;

            // Parse JSON timeline array
            // Expected format: {"room":"24380","timeline":["booked","free",...]}
            int timelineStart = message.IndexOf("\"timeline\"");
            if (timelineStart < 0) return;

            int arrayStart = message.IndexOf('[', timelineStart);
            int arrayEnd = message.IndexOf(']', arrayStart);
            if (arrayStart < 0 || arrayEnd < 0) return;

            string arrayContent = message.Substring(arrayStart + 1, arrayEnd - arrayStart - 1);
            string[] slots = arrayContent.Split(',');

            RoomData room = rooms[roomIndex];
            room.hasTimeline = true;
            room.timelineLen = Mathf.Min(slots.Length, 24);

            for (int i = 0; i < room.timelineLen; i++)
            {
                string slot = slots[i].Trim().Trim('"');
                room.slotBooked[i] = (slot == "booked");
            }

            Debug.Log($"[TFTDisplay] 📅 Timeline updated for {ROOM_NAMES[roomIndex]}: {room.timelineLen} slots");

            // Update display if this is current room
            if (roomIndex == currentRoomIndex && timelineMode)
            {
                UpdateDisplay();
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[TFTDisplay] ⚠️ Failed to parse timeline: {e.Message}");
        }
    }

    /// <summary>
    /// Parse status/condition data from MQTT
    /// </summary>
    void HandleStatusMessage(string topic, string message)
    {
        try
        {
            // Extract room ID from topic
            string roomId = ExtractRoomIdFromTopic(topic);
            int roomIndex = GetRoomIndex(roomId);
            if (roomIndex < 0) return;

            RoomData room = rooms[roomIndex];
            room.hasStatus = true;

            // Parse JSON fields
            room.occupancy = ExtractFloatValue(message, "occupancy");
            room.noise = ExtractFloatValue(message, "noise");
            room.temperature = ExtractFloatValue(message, "temperature");
            room.light = ExtractFloatValue(message, "light");
            room.state = ExtractStringValue(message, "state");

            Debug.Log($"[TFTDisplay] 🎭 Status updated for {ROOM_NAMES[roomIndex]}: {room.state}");

            // Update display if this is current room
            if (roomIndex == currentRoomIndex && !timelineMode)
            {
                UpdateDisplay();
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[TFTDisplay] ⚠️ Failed to parse status: {e.Message}");
        }
    }

    /// <summary>
    /// Toggle between Bookings and Condition mode
    /// </summary>
    public void ToggleMode()
    {
        timelineMode = !timelineMode;
        Debug.Log($"[TFTDisplay] 🔄 Mode toggled to: {(timelineMode ? "BOOKINGS" : "CONDITION")}");
        UpdateDisplay();
    }

    /// <summary>
    /// Update the display based on current mode and room data
    /// </summary>
    void UpdateDisplay()
    {
        if (timelineMode)
        {
            ShowBookingsMode();
        }
        else
        {
            ShowConditionMode();
        }
    }

    /// <summary>
    /// Display Bookings Mode UI
    /// </summary>
    void ShowBookingsMode()
    {
        // Show bookings UI elements
        if (headerText != null) headerText.gameObject.SetActive(true);
        if (headerBackground != null) headerBackground.gameObject.SetActive(true);
        if (roomDetailsText != null) roomDetailsText.gameObject.SetActive(true);
        if (bookingPercentText != null) bookingPercentText.gameObject.SetActive(true);

        // Hide condition UI elements
        if (emojiImage != null) emojiImage.gameObject.SetActive(false);
        if (stateCaptionText != null) stateCaptionText.gameObject.SetActive(false);
        if (captionBackground != null) captionBackground.gameObject.SetActive(false);

        // Update header
        if (headerText != null)
        {
            headerText.text = $"Bookings | {ROOM_NAMES[currentRoomIndex]}";
        }

        // Update room details
        if (roomDetailsText != null)
        {
            roomDetailsText.text = ROOM_DETAILS[currentRoomIndex];
        }

        // Update booking percentage
        if (bookingPercentText != null)
        {
            RoomData room = rooms[currentRoomIndex];
            if (room.hasTimeline && room.timelineLen > 0)
            {
                int bookedCount = 0;
                for (int i = 0; i < room.timelineLen; i++)
                {
                    if (room.slotBooked[i]) bookedCount++;
                }
                int percent = (bookedCount * 100) / room.timelineLen;
                bookingPercentText.text = $"Today: {percent}%";
            }
            else
            {
                bookingPercentText.text = "Today: --";
            }
        }

        Debug.Log($"[TFTDisplay] 📅 Showing Bookings for {ROOM_NAMES[currentRoomIndex]}");
    }

    /// <summary>
    /// Display Condition Mode UI
    /// </summary>
    void ShowConditionMode()
    {
        // Hide bookings UI elements
        if (roomDetailsText != null) roomDetailsText.gameObject.SetActive(false);
        if (bookingPercentText != null) bookingPercentText.gameObject.SetActive(false);

        // Show condition UI elements
        if (headerText != null) headerText.gameObject.SetActive(true);
        if (headerBackground != null) headerBackground.gameObject.SetActive(true);
        if (emojiImage != null) emojiImage.gameObject.SetActive(true);
        if (stateCaptionText != null) stateCaptionText.gameObject.SetActive(true);
        if (captionBackground != null) captionBackground.gameObject.SetActive(true);

        // Update header
        if (headerText != null)
        {
            headerText.text = $"Condition | {ROOM_NAMES[currentRoomIndex]}";
        }

        // Update emoji and caption
        RoomData room = rooms[currentRoomIndex];
        string state = room.hasStatus ? room.state : "neutral";

        if (emojiImage != null)
        {
            emojiImage.sprite = GetEmojiSprite(state);
        }

        if (stateCaptionText != null)
        {
            stateCaptionText.text = char.ToUpper(state[0]) + state.Substring(1);
        }

        Debug.Log($"[TFTDisplay] 🎭 Showing Condition for {ROOM_NAMES[currentRoomIndex]}: {state}");
    }

    /// <summary>
    /// Get emoji sprite for a given state
    /// </summary>
    Sprite GetEmojiSprite(string state)
    {
        switch (state.ToLower())
        {
            case "perfect": return spritePerfect;
            case "good": return spriteGood;
            case "calm": return spriteCalm;
            case "busy": return spriteBusy;
            case "noisy": return spriteNoisy;
            case "warm": return spriteWarm;
            case "cold": return spriteCold;
            case "dim": return spriteDim;
            case "overloaded": return spriteOverloaded;
            default: return spriteNeutral;
        }
    }

    /// <summary>
    /// Public method to change room (called by encoder rotation)
    /// </summary>
    public void SetRoom(int roomIndex)
    {
        if (roomIndex >= 0 && roomIndex < 5)
        {
            currentRoomIndex = roomIndex;
            Debug.Log($"[TFTDisplay] 🔄 Room changed to: {ROOM_NAMES[roomIndex]}");
            UpdateDisplay();
        }
    }

    // Helper methods for parsing MQTT messages

    string ExtractRoomIdFromTopic(string topic)
    {
        // Extract from: student/CASA0019/Gilang/studyspace/24380/timeline
        string[] parts = topic.Split('/');
        if (parts.Length >= 2)
        {
            return parts[parts.Length - 2];
        }
        return "";
    }

    int GetRoomIndex(string roomId)
    {
        for (int i = 0; i < ROOM_IDS.Length; i++)
        {
            if (ROOM_IDS[i] == roomId) return i;
        }
        return -1;
    }

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
}
