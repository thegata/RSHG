using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PhysicsGrabber : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private FirstPersonController controller;

    [Header("Grab")]
    [SerializeField] private float grabRange = 3.5f;
    [SerializeField] private float holdDistance = 2.2f;
    [SerializeField] private float minHoldDistance = 1.2f;
    [SerializeField] private float maxHoldDistance = 4f;
    [Tooltip("Максимальная масса, которую можно поднять.")]
    [SerializeField] private float maxGrabMass = 35f;
    [SerializeField] private LayerMask grabMask = ~0;

    [Header("Hold Physics")]
    [Tooltip("Сила притягивания к точке удержания. Больше = жёстче держит.")]
    [SerializeField] private float followStrength = 12f;
    [SerializeField] private float maxHoldSpeed = 14f;
    [SerializeField] private float rotationStiffness = 12f;
    [SerializeField] private float maxAngularSpeed = 18f;
    [Tooltip("Если предмет улетел дальше этого — выронить.")]
    [SerializeField] private float breakDistance = 3f;
    [Tooltip("Тяжёлые держатся вяло: насколько масса замедляет следование.")]
    [SerializeField] private float massSluggishness = 0.04f;

    [Header("Throw")]
    [Tooltip("Сила броска (импульс). Тяжёлые летят медленнее.")]
    [SerializeField] private float throwForce = 12f;

    [Header("Debug")]
    [SerializeField] private bool verbose = false;

    public static bool IsAnyHeld { get; private set; }
    public bool IsHolding => held != null;

    private InputAction grabAction;
    private InputAction throwAction;
    private InputAction scrollAction;

    private Rigidbody held;
    private PhysicsGrabbable heldGrabbable;
    private float currentHoldDistance;
    private Quaternion heldLocalRotation;

    private bool savedGravity;
    private float savedLinearDamping, savedAngularDamping;
    private RigidbodyInterpolation savedInterpolation;
    private readonly List<Collider> ignoredColliders = new List<Collider>();
    private Collider playerCollider;

    private void Awake()
    {
        if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
        if (controller == null) controller = GetComponentInParent<FirstPersonController>();
        if (controller == null) controller = FindObjectOfType<FirstPersonController>();
        if (controller != null) playerCollider = controller.GetComponent<Collider>();

        grabAction = new InputAction("Grab", InputActionType.Button, binding: "<Keyboard>/e");
        grabAction.AddBinding("<Keyboard>/g");
        grabAction.AddBinding("<Gamepad>/buttonNorth");

        throwAction = new InputAction("Throw", InputActionType.Button, binding: "<Mouse>/leftButton");
        throwAction.AddBinding("<Gamepad>/rightTrigger");

        scrollAction = new InputAction("HoldDistance", InputActionType.Value, binding: "<Mouse>/scroll/y");

        currentHoldDistance = holdDistance;
    }

    private void OnEnable()
    {
        grabAction?.Enable();
        throwAction?.Enable();
        scrollAction?.Enable();
        if (grabAction != null) grabAction.performed += OnGrabPressed;
        if (throwAction != null) throwAction.performed += OnThrowPressed;
    }

    private void OnDisable()
    {
        if (grabAction != null) grabAction.performed -= OnGrabPressed;
        if (throwAction != null) throwAction.performed -= OnThrowPressed;
        grabAction?.Disable();
        throwAction?.Disable();
        scrollAction?.Disable();
        if (held != null) Drop();
    }

    private void OnDestroy()
    {
        grabAction?.Dispose();
        throwAction?.Dispose();
        scrollAction?.Dispose();
    }

    private bool IsLocked() => controller != null && controller.InputLocked;

    private void OnGrabPressed(InputAction.CallbackContext _)
    {
        if (IsLocked()) return;
        if (held != null) Drop();
        else TryGrab();
    }

    private void OnThrowPressed(InputAction.CallbackContext _)
    {
        if (held != null) Throw();
    }

    private void Update()
    {
        if (held == null || cameraTransform == null) return;

        float scroll = scrollAction.ReadValue<float>();
        if (Mathf.Abs(scroll) > 0.01f)
            currentHoldDistance = Mathf.Clamp(currentHoldDistance + Mathf.Sign(scroll) * 0.25f,
                                              minHoldDistance, maxHoldDistance);
    }

    private void FixedUpdate()
    {
        if (held == null || cameraTransform == null) return;

        Vector3 holdPoint = cameraTransform.position + cameraTransform.forward * currentHoldDistance;
        Vector3 toHold = holdPoint - held.worldCenterOfMass;

        if (toHold.magnitude > breakDistance)
        {
            Drop();
            return;
        }

        float massFactor = 1f / (1f + held.mass * massSluggishness);
        Vector3 targetVel = toHold * followStrength * massFactor;
        if (targetVel.magnitude > maxHoldSpeed) targetVel = targetVel.normalized * maxHoldSpeed;
        held.linearVelocity = targetVel;

        Quaternion targetRot = cameraTransform.rotation * heldLocalRotation;
        Quaternion delta = targetRot * Quaternion.Inverse(held.rotation);
        delta.ToAngleAxis(out float angleDeg, out Vector3 axis);
        if (float.IsInfinity(axis.x) || float.IsNaN(axis.x))
        {
            held.angularVelocity = Vector3.zero;
            return;
        }
        if (angleDeg > 180f) angleDeg -= 360f;
        Vector3 angVel = axis.normalized * (angleDeg * Mathf.Deg2Rad) * rotationStiffness * massFactor;
        if (!float.IsNaN(angVel.x) && !float.IsInfinity(angVel.x))
            held.angularVelocity = Vector3.ClampMagnitude(angVel, maxAngularSpeed);
    }

    private void TryGrab()
    {
        if (cameraTransform == null) return;

        if (!Physics.Raycast(cameraTransform.position, cameraTransform.forward,
                             out RaycastHit hit, grabRange, grabMask, QueryTriggerInteraction.Ignore))
            return;

        if (hit.rigidbody == null) return;

        // PhysicsProp нельзя брать в руки, даже если на нём есть Grabbable
        if (hit.rigidbody.GetComponent<PhysicsProp>() != null) return;

        var grabbable = hit.rigidbody.GetComponent<PhysicsGrabbable>();
        if (grabbable == null || !grabbable.canGrab) return;

        if (hit.rigidbody.mass > maxGrabMass)
        {
            if (verbose) Debug.Log("[PhysicsGrabber] Слишком тяжёлый: " + hit.rigidbody.name);
            return;
        }

        held = hit.rigidbody;
        heldGrabbable = grabbable;
        currentHoldDistance = Mathf.Clamp(holdDistance, minHoldDistance, maxHoldDistance);
        heldLocalRotation = Quaternion.Inverse(cameraTransform.rotation) * held.rotation;

        savedGravity = held.useGravity;
        savedLinearDamping = held.linearDamping;
        savedAngularDamping = held.angularDamping;
        savedInterpolation = held.interpolation;

        held.useGravity = false;
        held.linearDamping = grabbable.heldLinearDamping;
        held.angularDamping = grabbable.heldAngularDamping;
        held.interpolation = RigidbodyInterpolation.Interpolate;
        held.maxAngularVelocity = maxAngularSpeed;

        IgnorePlayerCollision(true);
        IsAnyHeld = true;

        if (verbose) Debug.Log("[PhysicsGrabber] Взял: " + held.name + " (масса " + held.mass + ")");
    }

    private void Drop()
    {
        if (held == null) return;

        held.useGravity = savedGravity;
        held.linearDamping = savedLinearDamping;
        held.angularDamping = savedAngularDamping;
        held.interpolation = savedInterpolation;

        IgnorePlayerCollision(false);

        held = null;
        heldGrabbable = null;
        IsAnyHeld = false;
    }

    private void Throw()
    {
        if (held == null) return;

        Rigidbody rb = held;
        float mult = heldGrabbable != null ? heldGrabbable.throwMultiplier : 1f;

        Drop();

        rb.linearVelocity = Vector3.zero;
        rb.AddForce(cameraTransform.forward * throwForce * mult, ForceMode.Impulse);

        if (verbose) Debug.Log("[PhysicsGrabber] Бросок: " + rb.name);
    }

    private void IgnorePlayerCollision(bool ignore)
    {
        if (playerCollider == null) return;

        if (ignore)
        {
            ignoredColliders.Clear();
            if (held != null)
            {
                held.GetComponentsInChildren(ignoredColliders);
                foreach (var c in ignoredColliders)
                    if (c != null && c.enabled) Physics.IgnoreCollision(c, playerCollider, true);
            }
        }
        else
        {
            foreach (var c in ignoredColliders)
                if (c != null) Physics.IgnoreCollision(c, playerCollider, false);
            ignoredColliders.Clear();
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (cameraTransform == null) return;
        Gizmos.color = new Color(0.4f, 0.9f, 1f, 0.6f);
        Gizmos.DrawRay(cameraTransform.position, cameraTransform.forward * grabRange);
    }
}
