using System;
using System.Collections;
using UnityEngine;

public class VisitorSpawner : MonoBehaviour
{
    [Header("Spawn")]
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private float spawnInterval = 5f;
    [SerializeField] private float firstSpawnDelay = 2f;
    [SerializeField] private Vector3 visitorScale = Vector3.one;

    [Header("Look")]
    [SerializeField] private bool randomColor = true;
    [SerializeField] private Color fixedColor = Color.white;

    [Header("Debug")]
    [SerializeField] private bool verbose = true;

    public GameObject CurrentVisitor { get; private set; }
    public bool HasVisitor => CurrentVisitor != null;

    public event Action<GameObject> OnVisitorArrived;
    public event Action OnVisitorLeft;

    private void Awake()
    {
        if (spawnPoint == null) spawnPoint = transform;
    }

    private void Start()
    {
        StartCoroutine(SpawnLoop());
    }

    private IEnumerator SpawnLoop()
    {
        yield return new WaitForSeconds(firstSpawnDelay);
        while (true)
        {
            if (!HasVisitor) SpawnOne();
            yield return new WaitForSeconds(spawnInterval);
        }
    }

    private void SpawnOne()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "Visitor";
        go.transform.position = spawnPoint.position;
        go.transform.rotation = spawnPoint.rotation;
        go.transform.localScale = visitorScale;

        var rend = go.GetComponent<Renderer>();
        if (rend != null)
        {
            var color = randomColor
                ? UnityEngine.Random.ColorHSV(0f, 1f, 0.45f, 0.85f, 0.55f, 0.95f)
                : fixedColor;
            rend.material.color = color;
        }

        CurrentVisitor = go;
        if (verbose) Debug.Log("[VisitorSpawner] Пришёл новый посетитель: " + go.name);
        OnVisitorArrived?.Invoke(go);
    }

    public void AcceptVisitor(Transform comeInPoint = null)
    {
        if (!HasVisitor) return;
        var go = CurrentVisitor;
        CurrentVisitor = null;

        if (comeInPoint != null)
            StartCoroutine(WalkAndDespawn(go, comeInPoint.position, 0.9f));
        else
            Destroy(go);

        OnVisitorLeft?.Invoke();
        if (verbose) Debug.Log("[VisitorSpawner] Посетитель впущен.");
    }

    public void RejectVisitor(Vector3? leaveDirection = null)
    {
        if (!HasVisitor) return;
        var go = CurrentVisitor;
        CurrentVisitor = null;

        Vector3 dir = leaveDirection ?? -spawnPoint.forward;
        StartCoroutine(WalkAndDespawn(go, go.transform.position + dir * 4f, 0.8f));

        OnVisitorLeft?.Invoke();
        if (verbose) Debug.Log("[VisitorSpawner] Посетитель отклонён.");
    }

    private IEnumerator WalkAndDespawn(GameObject go, Vector3 target, float duration)
    {
        if (go == null) yield break;
        Vector3 start = go.transform.position;
        float t = 0f;
        while (t < duration && go != null)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, t / duration);
            go.transform.position = Vector3.Lerp(start, target, k);
            yield return null;
        }
        if (go != null) Destroy(go);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.4f, 1f, 0.5f, 0.7f);
        Vector3 p = spawnPoint != null ? spawnPoint.position : transform.position;
        Gizmos.DrawWireCube(p, Vector3.one);
    }
}
