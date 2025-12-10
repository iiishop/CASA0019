using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Makes a UIDocument panel follow a 3D object's screen position
/// Attach this to the same GameObject as your UIDocument
/// </summary>
public class WorldSpaceUIFollower : MonoBehaviour
{
    [Header("Target Settings")]
    [Tooltip("The 3D object this UI should follow")]
    public Transform targetObject;
    
    [Tooltip("Offset from target position in world space")]
    public Vector3 worldOffset = new Vector3(2f, 0f, 0f);
    
    [Header("UI Settings")]
    [Tooltip("The root visual element of your UI (will be positioned)")]
    public string rootElementName = "RoomInfoPanel";
    
    [Tooltip("Update every frame or only when target moves")]
    public bool continuousUpdate = true;
    
    [Tooltip("Minimum distance to update position (to avoid jitter)")]
    public float updateThreshold = 0.01f;
    
    [Header("AR Settings")]
    [Tooltip("Hide UI when object is too far (meters, 0 = disabled)")]
    public float maxVisibleDistance = 5f;
    
    [Tooltip("Hide UI when object is too close (meters)")]
    public float minVisibleDistance = 0.3f;
    
    [Tooltip("Smoothly interpolate UI position for AR stability")]
    public bool smoothPosition = true;
    
    [Tooltip("Position smoothing speed")]
    public float smoothSpeed = 10f;
    
    private UIDocument uiDocument;
    private Vector2 targetScreenPosition;
    private Vector2 currentScreenPosition;
    private VisualElement rootElement;
    private Camera mainCamera;
    private Vector3 lastTargetPosition;
    
    void Start()
    {
        // Get UIDocument component
        uiDocument = GetComponent<UIDocument>();
        if (uiDocument == null)
        {
            Debug.LogError("[WorldSpaceUIFollower] UIDocument component not found!");
            enabled = false;
            return;
        }
        
        // Get root visual element
        rootElement = uiDocument.rootVisualElement.Q<VisualElement>(rootElementName);
        if (rootElement == null)
        {
            Debug.LogError($"[WorldSpaceUIFollower] Root element '{rootElementName}' not found!");
            enabled = false;
            return;
        }
        
        // Get camera
        mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Debug.LogError("[WorldSpaceUIFollower] Main camera not found!");
            enabled = false;
            return;
        }
        
        // Set initial position
        if (targetObject != null)
        {
            lastTargetPosition = targetObject.position;
            Vector3 screenPos = mainCamera.WorldToScreenPoint(targetObject.position + worldOffset);
            currentScreenPosition = new Vector2(screenPos.x, Screen.height - screenPos.y);
            UpdateUIPosition();
        }
        
        Debug.Log("[WorldSpaceUIFollower] Initialized successfully (AR-optimized)");
    }
    
    void Update()
    {
        if (targetObject == null) return;
        
        // Check if we should update
        bool shouldUpdate = continuousUpdate;
        
        if (!continuousUpdate)
        {
            // Only update if target moved significantly
            float distance = Vector3.Distance(targetObject.position, lastTargetPosition);
            shouldUpdate = distance > updateThreshold;
        }
        
        if (shouldUpdate)
        {
            UpdateUIPosition();
            lastTargetPosition = targetObject.position;
        }
    }
    
    void UpdateUIPosition()
    {
        // Calculate world position with offset
        Vector3 worldPosition = targetObject.position + worldOffset;
        
        // Check distance from camera (AR optimization)
        float distance = Vector3.Distance(mainCamera.transform.position, worldPosition);
        
        // Convert to screen position
        Vector3 screenPosition = mainCamera.WorldToScreenPoint(worldPosition);
        
        // Visibility checks for AR
        bool shouldShow = true;
        
        // Check if behind camera
        if (screenPosition.z < 0)
        {
            shouldShow = false;
        }
        
        // Check distance range (useful for AR)
        if (maxVisibleDistance > 0 && distance > maxVisibleDistance)
        {
            shouldShow = false;
        }
        
        if (distance < minVisibleDistance)
        {
            shouldShow = false;
        }
        
        // Update visibility
        if (!shouldShow)
        {
            rootElement.style.display = DisplayStyle.None;
            return;
        }
        else
        {
            rootElement.style.display = DisplayStyle.Flex;
        }
        
        // Convert to UI coordinates (invert Y axis for UI)
        targetScreenPosition = new Vector2(screenPosition.x, Screen.height - screenPosition.y);
        
        // Smooth position for AR stability
        if (smoothPosition)
        {
            currentScreenPosition = Vector2.Lerp(
                currentScreenPosition, 
                targetScreenPosition, 
                Time.deltaTime * smoothSpeed
            );
        }
        else
        {
            currentScreenPosition = targetScreenPosition;
        }
        
        // Set position
        rootElement.style.left = currentScreenPosition.x;
        rootElement.style.top = currentScreenPosition.y;
        
        // Optional: Scale based on distance (useful for AR depth perception)
        float scale = Mathf.Clamp(10f / distance, 0.5f, 2f); // Adjust these values as needed
        rootElement.style.scale = new Scale(new Vector3(scale, scale, 1f));
    }
    
    /// <summary>
    /// Set a new target object at runtime
    /// </summary>
    public void SetTarget(Transform newTarget)
    {
        targetObject = newTarget;
        if (targetObject != null)
        {
            lastTargetPosition = targetObject.position;
            UpdateUIPosition();
        }
    }
    
    /// <summary>
    /// Set world offset at runtime
    /// </summary>
    public void SetWorldOffset(Vector3 offset)
    {
        worldOffset = offset;
        if (targetObject != null)
        {
            UpdateUIPosition();
        }
    }
}
