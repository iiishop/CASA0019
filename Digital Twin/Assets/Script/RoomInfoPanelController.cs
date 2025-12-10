using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

[Serializable]
public class CurrentRoomData
{
    public string current_room;
}

[Serializable]
public class TimelineData
{
    public string room;
    public List<string> timeline;
}

[Serializable]
public class StatusData
{
    public string timestamp;
    public string room;
    public float occupancy;
    public float noise;
    public float temperature;
    public float light;
    public string state;
}

public class RoomInfoPanelController : MonoBehaviour
{
    private UIDocument uiDocument;
    private VisualElement rootElement;

    // UI元素引用
    private Label roomNumberLabel;
    private Label roomNameLabel;
    private Label bookingPercentageLabel;
    private VisualElement bookedBar;
    private VisualElement freeBar;
    private VisualElement timelineContainer;
    private Label statusText;
    private Label statusIndicator;

    // MQTT Manager引用
    private mqttManager mqttManager;

    // 当前房间号
    private string currentRoom = "";

    // 房间名称映射(可以根据实际情况扩展)
    private Dictionary<string, string> roomNames = new Dictionary<string, string>()
    {
        { "24380", "Single Study Pod 216" },
        { "24381", "Group Study Room A" },
        { "24382", "Quiet Study Area" },
        { "24383", "Collaboration Space" }
    };

    void Start()
    {
        // 初始化UI
        InitializeUI();

        // 查找并连接MQTT Manager
        ConnectToMqttManager();
    }

    private void InitializeUI()
    {
        uiDocument = GetComponent<UIDocument>();
        if (uiDocument == null)
        {
            Debug.LogError("[RoomInfoPanelController] UIDocument component not found!");
            return;
        }

        rootElement = uiDocument.rootVisualElement;

        // 获取UI元素引用
        roomNumberLabel = rootElement.Q<Label>("RoomNumber");
        roomNameLabel = rootElement.Q<Label>("RoomName");
        bookingPercentageLabel = rootElement.Q<Label>("BookingPercentage");
        bookedBar = rootElement.Q<VisualElement>("BookedBar");
        freeBar = rootElement.Q<VisualElement>("FreeBar");
        timelineContainer = rootElement.Q<VisualElement>("TimelineContainer");
        statusText = rootElement.Q<Label>("StatusText");
        statusIndicator = rootElement.Q<Label>("StatusIndicator");

        // 设置初始状态
        UpdateStatusUI("Connecting to MQTT...", false);

        Debug.Log("[RoomInfoPanelController] UI initialized");
    }

    private void ConnectToMqttManager()
    {
        // 通过Tag查找MQTT Manager
        GameObject mqttManagerObj = GameObject.FindGameObjectWithTag("mqttmanager");

        if (mqttManagerObj == null)
        {
            Debug.LogError("[RoomInfoPanelController] MQTT Manager object with tag 'mqttmanager' not found!");
            UpdateStatusUI("MQTT Manager not found", false);
            return;
        }

        mqttManager = mqttManagerObj.GetComponent<mqttManager>();

        if (mqttManager == null)
        {
            Debug.LogError("[RoomInfoPanelController] mqttManager component not found on the object!");
            UpdateStatusUI("MQTT Manager component missing", false);
            return;
        }

        // 订阅MQTT事件
        mqttManager.OnMessageArrived += HandleMqttMessage;
        mqttManager.OnConnectionSucceeded += HandleConnectionStatus;

        // 订阅current主题
        SubscribeToCurrentTopic();

        Debug.Log("[RoomInfoPanelController] Connected to MQTT Manager");
    }

    private void SubscribeToCurrentTopic()
    {
        string currentTopic = "student/CASA0019/Gilang/studyspace/current";

        // 如果主题不在订阅列表中,添加它
        if (!mqttManager.topicSubscribe.Contains(currentTopic))
        {
            mqttManager.topicSubscribe.Add(currentTopic);
            Debug.Log($"[RoomInfoPanelController] Added subscription to: {currentTopic}");
        }
    }

    private void HandleConnectionStatus(bool connected)
    {
        if (connected)
        {
            UpdateStatusUI("Connected - Waiting for data...", true);
            Debug.Log("[RoomInfoPanelController] MQTT connected");
        }
        else
        {
            UpdateStatusUI("Disconnected", false);
            Debug.Log("[RoomInfoPanelController] MQTT disconnected");
        }
    }

    private void HandleMqttMessage(mqttObj mqttObject)
    {
        string topic = mqttObject.topic;
        string message = mqttObject.msg;

        Debug.Log($"[RoomInfoPanelController] Received message from {topic}: {message}");

        try
        {
            // 处理current主题
            if (topic == "student/CASA0019/Gilang/studyspace/current")
            {
                HandleCurrentRoomMessage(message);
            }
            // 处理timeline主题
            else if (topic.Contains("/timeline"))
            {
                HandleTimelineMessage(message);
            }
            // 处理status主题(预留,暂时不用)
            else if (topic.Contains("/status"))
            {
                HandleStatusMessage(message);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[RoomInfoPanelController] Error handling message: {e.Message}");
        }
    }

    private void HandleCurrentRoomMessage(string jsonMessage)
    {
        try
        {
            CurrentRoomData data = JsonUtility.FromJson<CurrentRoomData>(jsonMessage);

            if (data != null && !string.IsNullOrEmpty(data.current_room))
            {
                string newRoom = data.current_room;

                // 如果房间改变,更新UI
                // 注意: 不需要重新订阅,因为mqttManager已通过通配符(+)订阅了所有房间
                if (newRoom != currentRoom)
                {
                    currentRoom = newRoom;
                    UpdateRoomInfo(currentRoom);
                    SubscribeToRoomTopics(currentRoom); // 仅用于日志记录

                    Debug.Log($"[RoomInfoPanelController] Current room changed to: {currentRoom}");
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[RoomInfoPanelController] Error parsing current room data: {e.Message}");
        }
    }

    private void SubscribeToRoomTopics(string roomNumber)
    {
        // 注意: mqttManager已经通过通配符订阅了所有房间的timeline和status
        // student/CASA0019/Gilang/studyspace/+/timeline
        // student/CASA0019/Gilang/studyspace/+/status
        // 因此不需要动态订阅，只需等待该房间的数据到达即可

        Debug.Log($"[RoomInfoPanelController] Switched to room {roomNumber}, waiting for timeline/status data...");
        Debug.Log($"[RoomInfoPanelController] (Already subscribed via wildcard topics)");
    }

    private void HandleTimelineMessage(string jsonMessage)
    {
        try
        {
            TimelineData data = JsonUtility.FromJson<TimelineData>(jsonMessage);

            if (data != null && data.timeline != null && data.timeline.Count > 0)
            {
                // 只更新当前房间的数据
                if (data.room == currentRoom)
                {
                    UpdateBookingTimeline(data.timeline);
                    UpdateStatusUI($"Room {currentRoom} - Data updated", true);

                    Debug.Log($"[RoomInfoPanelController] Timeline updated for room {data.room}");
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[RoomInfoPanelController] Error parsing timeline data: {e.Message}");
        }
    }

    private void HandleStatusMessage(string jsonMessage)
    {
        // 暂时不使用status数据,但保留解析逻辑以备后用
        try
        {
            StatusData data = JsonUtility.FromJson<StatusData>(jsonMessage);

            if (data != null && data.room == currentRoom)
            {
                Debug.Log($"[RoomInfoPanelController] Status data received for room {data.room}: " +
                         $"Occupancy={data.occupancy}%, Noise={data.noise}dB, " +
                         $"Temp={data.temperature}°C, Light={data.light}lux, State={data.state}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[RoomInfoPanelController] Error parsing status data: {e.Message}");
        }
    }

    private void UpdateRoomInfo(string roomNumber)
    {
        // 更新房间号
        if (roomNumberLabel != null)
        {
            roomNumberLabel.text = roomNumber;
        }

        // 更新房间名称
        if (roomNameLabel != null)
        {
            string roomName = roomNames.ContainsKey(roomNumber)
                ? roomNames[roomNumber]
                : "Study Room";
            roomNameLabel.text = roomName;
        }
    }

    private void UpdateBookingTimeline(List<string> timeline)
    {
        if (timeline == null || timeline.Count == 0)
        {
            Debug.LogWarning("[RoomInfoPanelController] Empty timeline data");
            return;
        }

        // 计算预定百分比
        int bookedCount = 0;
        foreach (string slot in timeline)
        {
            if (slot.ToLower() == "booked")
            {
                bookedCount++;
            }
        }

        float bookedPercentage = (float)bookedCount / timeline.Count * 100f;

        // 更新百分比标签
        if (bookingPercentageLabel != null)
        {
            bookingPercentageLabel.text = $"{bookedPercentage:F0}%";
        }

        // 更新预定条
        if (bookedBar != null && freeBar != null)
        {
            bookedBar.style.width = Length.Percent(bookedPercentage);
            freeBar.style.width = Length.Percent(100f - bookedPercentage);
        }

        // 更新时间线显示
        UpdateTimelineVisual(timeline);
    }

    private void UpdateTimelineVisual(List<string> timeline)
    {
        if (timelineContainer == null) return;

        // 清空现有时间线
        timelineContainer.Clear();

        // 时间线从9:00到21:00,共24个时间槽(每个30分钟)
        // timeline数组应该有24个元素
        for (int i = 0; i < timeline.Count && i < 24; i++)
        {
            VisualElement slot = new VisualElement();
            slot.AddToClassList("timeline-slot");

            // 根据状态设置样式
            string status = timeline[i].ToLower();
            if (status == "booked")
            {
                slot.AddToClassList("timeline-booked");
            }
            else
            {
                slot.AddToClassList("timeline-free");
            }

            // 添加tooltip显示时间
            int hour = 9 + (i / 2);
            int minute = (i % 2) * 30;
            slot.tooltip = $"{hour:D2}:{minute:D2} - {status}";

            timelineContainer.Add(slot);
        }

        Debug.Log($"[RoomInfoPanelController] Timeline visual updated with {timeline.Count} slots");
    }

    private void UpdateStatusUI(string message, bool connected)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }

        if (statusIndicator != null)
        {
            // 移除旧状态类
            statusIndicator.RemoveFromClassList("connected");
            statusIndicator.RemoveFromClassList("disconnected");

            // 添加新状态类
            if (connected)
            {
                statusIndicator.AddToClassList("connected");
            }
            else
            {
                statusIndicator.AddToClassList("disconnected");
            }
        }
    }

    private void OnDestroy()
    {
        // 取消订阅事件
        if (mqttManager != null)
        {
            mqttManager.OnMessageArrived -= HandleMqttMessage;
            mqttManager.OnConnectionSucceeded -= HandleConnectionStatus;
        }

        Debug.Log("[RoomInfoPanelController] Controller destroyed and events unsubscribed");
    }
}
