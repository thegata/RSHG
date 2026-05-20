using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Camera))]
public class TopDownStrategyCamera : MonoBehaviour
{
    [Header("View")]
    [Tooltip("Центр, на который смотрит камера.")]
    public Vector3 focusPoint = Vector3.zero;
    [Tooltip("Угол наклона. 90 = строго сверху, 45 = классический изометрический вид.")]
    [Range(30f, 90f)] public float angle = 65f;
    [Tooltip("Высота камеры (зум).")]
    [Range(3f, 80f)] public float height = 14f;

    [Header("Pan (WASD / Стрелки)")]
    public float panSpeed = 14f;
    public bool useEdgeScroll = false;
    public float edgeMargin = 25f;

    [Header("Drag Pan (ПКМ / СКМ)")]
    public bool useDragPan = true;
    public float dragSensitivity = 0.03f;

    [Header("Zoom (Колесо мыши)")]
    public float zoomSpeed = 4f;
    public float minHeight = 5f;
    public float maxHeight = 45f;

    [Header("Bounds")]
    public bool useBounds = true;
    public Vector2 boundsMin = new Vector2(-30f, -30f);
    public Vector2 boundsMax = new Vector2(30f, 30f);

    [Header("Smoothing")]
    public float positionSmoothing = 16f;
    public float zoomSmoothing = 14f;

    private Vector3 desiredFocus;
    private float desiredHeight;

    private void Reset()
    {
        var cam = GetComponent<Camera>();
        if (cam != null)
        {
            cam.orthographic = false;
            cam.fieldOfView = 50f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 500f;
        }
    }

    private void Start()
    {
        desiredFocus = focusPoint;
        desiredHeight = height;
        ApplyTransform();
    }

    private void Update()
    {
        HandleKeyboardPan();
        HandleDragPan();
        HandleEdgeScroll();
        HandleZoom();
        ClampBounds();

        float kp = 1f - Mathf.Exp(-positionSmoothing * Time.deltaTime);
        float kz = 1f - Mathf.Exp(-zoomSmoothing * Time.deltaTime);
        focusPoint = Vector3.Lerp(focusPoint, desiredFocus, kp);
        height = Mathf.Lerp(height, desiredHeight, kz);

        ApplyTransform();
    }

    private void HandleKeyboardPan()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        Vector2 pan = Vector2.zero;
        if (kb.wKey.isPressed || kb.upArrowKey.isPressed) pan.y += 1f;
        if (kb.sKey.isPressed || kb.downArrowKey.isPressed) pan.y -= 1f;
        if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) pan.x -= 1f;
        if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) pan.x += 1f;

        if (pan.sqrMagnitude > 1f) pan.Normalize();
        float scale = panSpeed * (height / 14f);
        desiredFocus.x += pan.x * scale * Time.deltaTime;
        desiredFocus.z += pan.y * scale * Time.deltaTime;
    }

    private void HandleDragPan()
    {
        if (!useDragPan) return;
        var mouse = Mouse.current;
        if (mouse == null) return;
        if (!(mouse.middleButton.isPressed || mouse.rightButton.isPressed)) return;

        Vector2 delta = mouse.delta.ReadValue();
        float scale = dragSensitivity * (height / 14f);
        desiredFocus.x -= delta.x * scale;
        desiredFocus.z -= delta.y * scale;
    }

    private void HandleEdgeScroll()
    {
        if (!useEdgeScroll) return;
        var mouse = Mouse.current;
        if (mouse == null) return;

        Vector2 mp = mouse.position.ReadValue();
        Vector2 pan = Vector2.zero;
        if (mp.x < edgeMargin) pan.x -= 1f;
        if (mp.x > Screen.width - edgeMargin) pan.x += 1f;
        if (mp.y < edgeMargin) pan.y -= 1f;
        if (mp.y > Screen.height - edgeMargin) pan.y += 1f;

        if (pan.sqrMagnitude < 0.01f) return;
        pan.Normalize();
        float scale = panSpeed * (height / 14f);
        desiredFocus.x += pan.x * scale * Time.deltaTime;
        desiredFocus.z += pan.y * scale * Time.deltaTime;
    }

    private void HandleZoom()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;
        float scroll = mouse.scroll.ReadValue().y / 120f;
        if (Mathf.Abs(scroll) < 0.01f) return;
        desiredHeight = Mathf.Clamp(desiredHeight - scroll * zoomSpeed, minHeight, maxHeight);
    }

    private void ClampBounds()
    {
        if (!useBounds) return;
        desiredFocus.x = Mathf.Clamp(desiredFocus.x, boundsMin.x, boundsMax.x);
        desiredFocus.z = Mathf.Clamp(desiredFocus.z, boundsMin.y, boundsMax.y);
    }

    private void ApplyTransform()
    {
        float a = angle * Mathf.Deg2Rad;
        float distBack = Mathf.Cos(a) * height;
        float distUp = Mathf.Sin(a) * height;
        Vector3 offset = new Vector3(0f, distUp, -distBack);
        transform.position = focusPoint + offset;
        transform.LookAt(focusPoint);
    }

    private void OnDrawGizmosSelected()
    {
        if (!useBounds) return;
        Gizmos.color = new Color(1f, 0.85f, 0.3f, 0.5f);
        Vector3 c = new Vector3((boundsMin.x + boundsMax.x) * 0.5f, 0f, (boundsMin.y + boundsMax.y) * 0.5f);
        Vector3 s = new Vector3(boundsMax.x - boundsMin.x, 0.1f, boundsMax.y - boundsMin.y);
        Gizmos.DrawWireCube(c, s);
    }
}
