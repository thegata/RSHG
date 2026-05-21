using UnityEngine;

public class MovingTarget : MonoBehaviour
{
    public enum PathMode { LocalOffset, TransformPoints }

    [Header("Path")]
    [SerializeField] private PathMode mode = PathMode.LocalOffset;
    [Tooltip("Смещение влево от стартовой точки (режим Local Offset).")]
    [SerializeField] private Vector3 offsetA = new Vector3(-3f, 0f, 0f);
    [Tooltip("Смещение вправо от стартовой точки (режим Local Offset).")]
    [SerializeField] private Vector3 offsetB = new Vector3(3f, 0f, 0f);
    [Tooltip("Точки A и B (режим Transform Points).")]
    [SerializeField] private Transform pointA;
    [SerializeField] private Transform pointB;

    [Header("Motion")]
    [SerializeField] private float speed = 2f;
    [Tooltip("Сглаживать на концах (замедление у точек).")]
    [SerializeField] private bool easeEnds = true;
    [Tooltip("Случайный сдвиг фазы, чтобы мишени не двигались синхронно.")]
    [SerializeField] private bool randomizePhase = true;

    private Vector3 startPos;
    private float phase;

    private void Start()
    {
        startPos = transform.position;
        if (randomizePhase) phase = Random.value * 1000f;
    }

    private void Update()
    {
        Vector3 a, b;
        if (mode == PathMode.TransformPoints && pointA != null && pointB != null)
        {
            a = pointA.position;
            b = pointB.position;
        }
        else
        {
            a = startPos + offsetA;
            b = startPos + offsetB;
        }

        float t = Mathf.PingPong((Time.time + phase) * speed, 1f);
        if (easeEnds) t = Mathf.SmoothStep(0f, 1f, t);
        transform.position = Vector3.Lerp(a, b, t);
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 a, b;
        if (mode == PathMode.TransformPoints && pointA != null && pointB != null)
        {
            a = pointA.position; b = pointB.position;
        }
        else
        {
            Vector3 origin = Application.isPlaying ? startPos : transform.position;
            a = origin + offsetA; b = origin + offsetB;
        }

        Gizmos.color = new Color(1f, 0.4f, 0.3f, 0.9f);
        Gizmos.DrawLine(a, b);
        Gizmos.DrawWireCube(a, Vector3.one * 0.3f);
        Gizmos.DrawWireCube(b, Vector3.one * 0.3f);
    }
}
