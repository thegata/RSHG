using UnityEngine;

// Физический предмет, который участвует в физике (толкает и толкается),
// но который игрок НЕ может взять в руки (в отличие от PhysicsGrabbable).
[RequireComponent(typeof(Rigidbody))]
public class PhysicsProp : MonoBehaviour
{
    [Header("Масса / тяжесть")]
    public bool overrideMass = true;
    [Tooltip("Масса предмета (кг).")]
    public float mass = 5f;

    [Header("Сопротивление")]
    [Tooltip("Гашение движения (linear damping).")]
    public float linearDamping = 0.05f;
    [Tooltip("Гашение вращения (angular damping).")]
    public float angularDamping = 0.05f;

    [Header("Физика")]
    public bool useGravity = true;
    public CollisionDetectionMode collisionDetection = CollisionDetectionMode.Discrete;
    public RigidbodyInterpolation interpolation = RigidbodyInterpolation.Interpolate;

    public Rigidbody Body { get; private set; }

    private void Awake()
    {
        Body = GetComponent<Rigidbody>();
        Apply();
    }

    private void Apply()
    {
        if (Body == null) Body = GetComponent<Rigidbody>();
        if (Body == null) return;
        if (overrideMass) Body.mass = Mathf.Max(0.01f, mass);
        Body.linearDamping = linearDamping;
        Body.angularDamping = angularDamping;
        Body.useGravity = useGravity;
        Body.collisionDetectionMode = collisionDetection;
        Body.interpolation = interpolation;
    }

    private void OnValidate()
    {
        var rb = GetComponent<Rigidbody>();
        if (rb != null && overrideMass) rb.mass = Mathf.Max(0.01f, mass);
    }
}
