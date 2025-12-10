using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;

public class RoomInfoPanelController : MonoBehaviour
{
    [Header("UI Document")]
    [SerializeField] private UIDocument uiDocument;

    [Header("MQTT Manager")]
    private mqttManager mqttManager;

    // UI Elements
    private Label roomNumberLabel;
    private Label roomNameLabel;
    private Label bookingPercentageLabel;
    private VisualElement timelineContainer;
    private Label statusIndicator;
    private Label statusText;
    private VisualElement bookedBar;
    private VisualElement freeBar;

    // Room data
    private string currentRoomId = "24380";
    private Dictionary<string, string> roomNames;
    private int[] timelineData = new int[24]; // 0=free, 1=booked

    void Start()
    {
        Debug.LogWarning("🔧 RoomInfoPanelController Start() called");

        // Find mqttManager by tag
        GameObject mqttManagerObj = GameObject.FindGameObjectWithTag("mqttmanager");
        if (mqttManagerObj != null)
        {
            Debug.LogWarning($"✅ Found mqttmanager GameObject: {mqttManagerObj.name}");
            mqttManager = mqttManagerObj.GetComponent<mqttManager>();
            if (mqttManager != null)
            {
                mqttManager.OnMessageArrived += HandleMqttMessage;
                Debug.LogWarning("✅ RoomInfoPanelController: Successfully subscribed to OnMessageArrived event");
            }
            else
            {
                Debug.LogError("❌ mqttManager component not found on tagged object!");
            }
        }
        else
        {
            Debug.LogError("❌ GameObject with tag 'mqttmanager' not found!");
        }

        // Initialize room names mapping
        InitializeRoomNames();

        // Get UI elements
        InitializeUIElements();

        // Initialize timeline slots (24 slots for 9:00-21:00)
        CreateTimelineSlots();

        // Set initial room display
        UpdateRoomDisplay(currentRoomId);
    }

    void InitializeRoomNames()
    {
        roomNames = new Dictionary<string, string>
        {
            { "24380", "Single Study Pod 216" },
            { "24381", "Single Study Pod 212" },
            { "24382", "4-Seater Study Pod 213" },
            { "24546", "4-Seater Study Pod 217" },
            { "24547", "8-Seater Study Pod 218" }
        };
    }

    void InitializeUIElements()
    {
        if (uiDocument == null)
        {
            Debug.LogError("❌ UIDocument is not assigned!");
            return;
        }

        VisualElement root = uiDocument.rootVisualElement;

        roomNumberLabel = root.Q<Label>("RoomNumber");
        roomNameLabel = root.Q<Label>("RoomName");
        bookingPercentageLabel = root.Q<Label>("BookingPercentage");
        timelineContainer = root.Q<VisualElement>("TimelineContainer");
        statusIndicator = root.Q<Label>("StatusIndicator");
        statusText = root.Q<Label>("StatusText");
        bookedBar = root.Q<VisualElement>("BookedBar");
        freeBar = root.Q<VisualElement>("FreeBar");

        Debug.Log("✅ UI Elements initialized");
    }

    void CreateTimelineSlots()
    {
        if (timelineContainer == null)
        {
            Debug.LogError("❌ Timeline container not found!");
            return;
        }

        timelineContainer.Clear();

        // Create 24 slots (9:00 - 21:00, 30-minute intervals)
        for (int i = 0; i < 24; i++)
        {
            VisualElement slot = new VisualElement();
            slot.name = $"TimelineSlot_{i}";
            slot.AddToClassList("timeline-slot");
            slot.AddToClassList("free"); // Default state

            // Add tooltip with time
            string time = GetTimeForSlot(i);
            slot.tooltip = time;

            timelineContainer.Add(slot);
        }

        Debug.Log("✅ Created 24 timeline slots");
    }

    string GetTimeForSlot(int index)
    {
        int hour = 9 + (index / 2);
        int minute = (index % 2) * 30;
        return $"{hour:D2}:{minute:D2}";
    }

    void HandleMqttMessage(mqttObj message)
    {
        string topic = message.topic;
        string payload = message.msg;

        Debug.LogWarning($"🔔 MQTT Message Received:\n  Topic: {topic}\n  Payload: {payload}");

        // Handle current room change
        if (topic == "student/CASA0019/Gilang/studyspace/current")
        {
            Debug.LogWarning($"📍 Processing current room topic");
            string newRoomId = ExtractStringValue(payload, "current_room");
            Debug.LogWarning($"📍 Extracted room ID: '{newRoomId}', Current room: '{currentRoomId}'");

            if (string.IsNullOrEmpty(newRoomId))
            {
                Debug.LogWarning("⚠️ Failed to extract room ID from payload!");
            }
            else if (newRoomId != currentRoomId)
            {
                currentRoomId = newRoomId;
                UpdateRoomDisplay(currentRoomId);
                ClearTimelineData();
                Debug.Log($"✅ Room changed to: {currentRoomId}");
            }
            else
            {
                Debug.LogWarning($"⚠️ Room ID unchanged: {currentRoomId}");
            }
        }
        // Handle timeline data
        else if (topic.Contains("/timeline"))
        {
            Debug.LogWarning($"📊 Processing timeline topic");
            string messageRoomId = ExtractStringValue(payload, "room");
            Debug.LogWarning($"📊 Timeline room ID: '{messageRoomId}', Current room: '{currentRoomId}'");

            if (string.IsNullOrEmpty(messageRoomId))
            {
                Debug.LogWarning("⚠️ Failed to extract room ID from timeline payload!");
            }
            else if (messageRoomId == currentRoomId)
            {
                Debug.LogWarning($"✅ Timeline data matches current room, parsing...");
                ParseTimelineData(payload);
                UpdateConnectionStatus(true);
            }
            else
            {
                Debug.LogWarning($"⚠️ Timeline room mismatch: got '{messageRoomId}', expected '{currentRoomId}'");
            }
        }
        // Handle status/condition data (just log it for now)
        else if (topic.Contains("/status"))
        {
            Debug.LogWarning($"🌡️ Processing status topic");
            string messageRoomId = ExtractStringValue(payload, "room");
            Debug.LogWarning($"🌡️ Status room ID: '{messageRoomId}', Current room: '{currentRoomId}'");

            if (string.IsNullOrEmpty(messageRoomId))
            {
                Debug.LogWarning("⚠️ Failed to extract room ID from status payload!");
            }
            else if (messageRoomId == currentRoomId)
            {
                Debug.LogWarning($"✅ Status data matches current room (condition data received)");
                // Note: This panel focuses on bookings/timeline only
                // Status data could be used for additional features in the future
            }
            else
            {
                Debug.LogWarning($"⚠️ Status room mismatch: got '{messageRoomId}', expected '{currentRoomId}'");
            }
        }
        // Handle encoder messages (rotation/button)
        else if (topic.Contains("/encoder"))
        {
            Debug.LogWarning($"🎮 Processing encoder topic (rotation/button events)");
            // These messages are handled by other controllers
        }
        else
        {
            Debug.LogWarning($"⚠️ Unhandled topic: {topic}");
        }
    }

    void UpdateRoomDisplay(string roomId)
    {
        if (roomNumberLabel != null)
        {
            roomNumberLabel.text = roomId;
        }

        if (roomNameLabel != null && roomNames.ContainsKey(roomId))
        {
            roomNameLabel.text = roomNames[roomId];
        }
    }

    void ParseTimelineData(string json)
    {
        Debug.LogWarning($"🔍 Parsing timeline data from JSON: {json}");

        // Reset timeline data
        for (int i = 0; i < 24; i++)
        {
            timelineData[i] = 0;
        }

        // Extract timeline array - support both formats with/without space
        string searchKey1 = "\"timeline\":[";
        string searchKey2 = "\"timeline\": [";
        
        int keyStartIndex = json.IndexOf(searchKey1);
        int keyLength = searchKey1.Length;
        
        if (keyStartIndex == -1)
        {
            keyStartIndex = json.IndexOf(searchKey2);
            keyLength = searchKey2.Length;
        }
        
        if (keyStartIndex == -1)
        {
            Debug.LogWarning("⚠️ Could not find 'timeline' key in JSON!");
            return;
        }

        int startIndex = keyStartIndex + keyLength;
        int endIndex = json.IndexOf("]", startIndex);
        
        Debug.LogWarning($"🔍 Timeline array indices - keyStart: {keyStartIndex}, dataStart: {startIndex}, end: {endIndex}");

        if (endIndex > startIndex)
        {
            string arrayContent = json.Substring(startIndex, endIndex - startIndex);
            Debug.LogWarning($"🔍 Extracted array content: [{arrayContent}]");
            
            // Split by comma and process each value
            string[] values = arrayContent.Split(',');
            Debug.LogWarning($"🔍 Split into {values.Length} values");

            int bookedCount = 0;
            for (int i = 0; i < values.Length && i < 24; i++)
            {
                // Clean the value: remove quotes, spaces, etc.
                string cleanValue = values[i].Trim().Trim('"').Trim();
                Debug.LogWarning($"🔍 Slot {i}: '{cleanValue}'");
                
                // Check if it's "booked" (1) or "free" (0)
                if (cleanValue == "booked")
                {
                    timelineData[i] = 1;
                    bookedCount++;
                }
                else
                {
                    timelineData[i] = 0;
                }
            }

            Debug.LogWarning($"✅ Timeline parsed: {bookedCount} booked slots out of {values.Length}");

            // Update UI
            UpdateTimelineSlots();
            UpdateBookingPercentage(bookedCount);
            UpdateBarChart(bookedCount);
        }
        else
        {
            Debug.LogWarning($"⚠️ Failed to find timeline array closing bracket! startIndex={startIndex}, endIndex={endIndex}");
        }
    }

    void UpdateTimelineSlots()
    {
        if (timelineContainer == null) return;

        for (int i = 0; i < 24; i++)
        {
            VisualElement slot = timelineContainer.Q<VisualElement>($"TimelineSlot_{i}");
            if (slot != null)
            {
                slot.RemoveFromClassList("booked");
                slot.RemoveFromClassList("free");

                if (timelineData[i] == 1)
                {
                    slot.AddToClassList("booked");
                }
                else
                {
                    slot.AddToClassList("free");
                }
            }
        }
    }

    void UpdateBookingPercentage(int bookedCount)
    {
        if (bookingPercentageLabel != null)
        {
            float percentage = (bookedCount / 24f) * 100f;
            bookingPercentageLabel.text = $"{percentage:F0}%";
        }
    }

    void UpdateBarChart(int bookedCount)
    {
        if (bookedBar != null && freeBar != null)
        {
            int freeCount = 24 - bookedCount;

            // Calculate flex-grow values
            float bookedFlex = bookedCount;
            float freeFlex = freeCount;

            // Apply styles using USS
            bookedBar.style.flexGrow = bookedFlex;
            freeBar.style.flexGrow = freeFlex;
        }
    }

    void UpdateConnectionStatus(bool connected)
    {
        Debug.LogWarning($"🔌 Updating connection status to: {(connected ? "CONNECTED" : "DISCONNECTED")}");

        if (statusIndicator != null && statusText != null)
        {
            statusIndicator.RemoveFromClassList("connected");
            statusIndicator.RemoveFromClassList("disconnected");

            if (connected)
            {
                statusIndicator.AddToClassList("connected");
                statusText.text = "Connected - Data received";
                Debug.LogWarning("✅ UI Status updated to: Connected");
            }
            else
            {
                statusIndicator.AddToClassList("disconnected");
                statusText.text = "Waiting for data...";
                Debug.LogWarning("⚠️ UI Status updated to: Waiting");
            }
        }
        else
        {
            Debug.LogWarning("⚠️ Status UI elements are null!");
        }
    }

    void ClearTimelineData()
    {
        for (int i = 0; i < 24; i++)
        {
            timelineData[i] = 0;
        }
        UpdateTimelineSlots();
        UpdateBookingPercentage(0);
        UpdateBarChart(0);
    }

    string ExtractStringValue(string json, string key)
    {
        // Support both "key":"value" and "key": "value" (with space)
        string searchKey1 = $"\"{key}\":\"";
        string searchKey2 = $"\"{key}\": \"";

        int startIndex = json.IndexOf(searchKey1);
        int keyLength = searchKey1.Length;

        if (startIndex == -1)
        {
            startIndex = json.IndexOf(searchKey2);
            keyLength = searchKey2.Length;
        }

        if (startIndex == -1)
        {
            Debug.LogWarning($"⚠️ Could not find key '{key}' in JSON");
            return "";
        }

        startIndex += keyLength;
        int endIndex = json.IndexOf("\"", startIndex);
        if (endIndex == -1)
        {
            Debug.LogWarning($"⚠️ Could not find closing quote for key '{key}'");
            return "";
        }

        string value = json.Substring(startIndex, endIndex - startIndex);
        Debug.LogWarning($"✅ Extracted '{key}' = '{value}'");
        return value;
    }

    void OnDestroy()
    {
        if (mqttManager != null)
        {
            mqttManager.OnMessageArrived -= HandleMqttMessage;
        }
    }
}
