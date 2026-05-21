using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PhysicsGrabbable : MonoBehaviour
{
    [Header("Можно ли брать")]
    public bool canGrab = true;

    [Header("Масса / тяжесть")]
    [Tooltip("Переопределить массу Rigidbody этим значением при старте.")]
    public bool overrideMass = true;
    [Tooltip("Масса предмета (кг). Тяжёлые предметы держатся вяло и кидаются слабее.")]
    public float mass = 1f;

    [Header("Бросок")]
    [Tooltip("Множитель силы броска именно для этого предмета.")]
    public float throwMultiplier = 1f;

    [Header("Сопротивление при удержании")]
    [Tooltip("Гашение вращения пока держишь (чтобы не крутился как бешеный).")]
    public float heldAngularDamping = 6f;
    [Tooltip("Гашение движения пока держишь.")]
    public float heldLinearDamping = 6f;

    public Rigidbody Body { get; private set; }

    private void Awake()
    {
        Body = GetComponent<Rigidbody>();
        if (overrideMass) Body.mass = Mathf.Max(0.01f, mass);
    }

    private void OnValidate()
    {
        if (overrideMass)
        {
            var rb = GetComponent<Rigidbody>();
            if (rb != null) rb.mass = Mathf.Max(0.01f, mass);
        }
    }
}
