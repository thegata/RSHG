using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class PeepholeDoor : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private VisitorSpawner spawner;
    [SerializeField] private FirstPersonController controller;
    [SerializeField] private Transform cameraTransform;
    [Tooltip("Точка, куда телепортируется камера при подглядывании. Поставь рядом с дверью с твоей стороны и направь на спавн-точку посетителя.")]
    [SerializeField] private Transform peepholeAnchor;
    [Tooltip("Опционально: затвор/крышка глазка. Будет ехать вниз при открытии и обратно при закрытии.")]
    [SerializeField] private Transform hatchTransform;
    [Tooltip("Опционально: точка куда впущенный посетитель заходит. Если пусто — просто исчезает.")]
    [SerializeField] private Transform comeInPoint;
    [Tooltip("Точка, относительно которой меряется расстояние до игрока. По умолчанию — этот объект.")]
    [SerializeField] private Transform interactPoint;

    [Header("Interaction")]
    [SerializeField] private float interactRange = 3.5f;
    [SerializeField] private string prompt = "[E] Посмотреть в глазок";

    [Header("Camera Transition")]
    [SerializeField] private float transitionTime = 0.45f;
    [SerializeField] private AnimationCurve transitionEase = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Hatch Animation")]
    [SerializeField] private Vector3 hatchClosedLocalPos = Vector3.zero;
    [SerializeField] private Vector3 hatchOpenLocalPos = new Vector3(0f, -0.4f, 0f);
    [SerializeField] private float hatchAnimTime = 0.35f;

    [Header("Debug")]
    [SerializeField] private bool verbose = true;

    private InputAction interactAction;
    private bool playerNearby;
    private bool inPeephole;

    private void Awake()
    {
        if (controller == null) controller = FindObjectOfType<FirstPersonController>();
        if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
        if (interactPoint == null) interactPoint = transform;
        if (spawner == null) spawner = FindObjectOfType<VisitorSpawner>();

        if (hatchTransform != null) hatchTransform.localPosition = hatchClosedLocalPos;

        if (spawner == null)
            Debug.LogError("[PeepholeDoor] Не назначен VisitorSpawner — добавь его в сцену.", this);
        if (peepholeAnchor == null)
            Debug.LogError("[PeepholeDoor] Не назначен Peephole Anchor — поставь пустой GameObject у двери, направь его на посетителя.", this);
        if (controller == null)
            Debug.LogWarning("[PeepholeDoor] FirstPersonController не найден.", this);

        interactAction = new InputAction("PeepholeInteract", InputActionType.Button, binding: "<Keyboard>/e");
        interactAction.AddBinding("<Gamepad>/buttonNorth");
    }

    private void OnEnable()
    {
        interactAction?.Enable();
        if (spawner != null) spawner.OnVisitorArrived += HandleVisitorArrived;
    }

    private void OnDisable()
    {
        interactAction?.Disable();
        if (spawner != null) spawner.OnVisitorArrived -= HandleVisitorArrived;
    }

    private void OnDestroy() => interactAction?.Dispose();

    private void HandleVisitorArrived(GameObject visitor)
    {
        VisitorUI.Instance.ShowNotification("Кто-то ломится в дверь...", 5f);
        if (verbose) Debug.Log("[PeepholeDoor] Уведомление: кто-то ломится.");
    }

    private void Update()
    {
        if (inPeephole) return;

        bool canInteract = spawner != null && spawner.HasVisitor && controller != null;
        if (!canInteract) { playerNearby = false; DialogueUI.Instance.HidePrompt(); return; }

        float dist = Vector3.Distance(controller.transform.position, interactPoint.position);
        playerNearby = dist <= interactRange;

        if (playerNearby)
        {
            DialogueUI.Instance.ShowPrompt(prompt);
            if (interactAction.WasPressedThisFrame())
                StartCoroutine(PeepholeRoutine());
        }
        else
        {
            DialogueUI.Instance.HidePrompt();
        }
    }

    private IEnumerator PeepholeRoutine()
    {
        if (peepholeAnchor == null || cameraTransform == null || controller == null) yield break;

        inPeephole = true;
        DialogueUI.Instance.HidePrompt();
        controller.InputLocked = true;
        controller.enabled = false;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        Transform origParent = cameraTransform.parent;
        Vector3 origWorldPos = cameraTransform.position;
        Quaternion origWorldRot = cameraTransform.rotation;
        cameraTransform.SetParent(null, true);

        if (hatchTransform != null) StartCoroutine(AnimateHatch(true));
        yield return MoveCameraTo(peepholeAnchor.position, peepholeAnchor.rotation, transitionTime);

        VisitorUI.Instance.ClearChoice();
        VisitorUI.Instance.ShowChoice();

        while (VisitorUI.Instance.PendingChoice == VisitorUI.Choice.None)
            yield return null;

        var choice = VisitorUI.Instance.PendingChoice;
        VisitorUI.Instance.HideChoice();

        if (hatchTransform != null) StartCoroutine(AnimateHatch(false));
        yield return MoveCameraTo(origWorldPos, origWorldRot, transitionTime);

        cameraTransform.SetParent(origParent, false);
        cameraTransform.localPosition = origParent != null
            ? origParent.InverseTransformPoint(origWorldPos)
            : origWorldPos;
        cameraTransform.localRotation = origParent != null
            ? Quaternion.Inverse(origParent.rotation) * origWorldRot
            : origWorldRot;

        controller.enabled = true;
        controller.InputLocked = false;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (choice == VisitorUI.Choice.Accept) spawner.AcceptVisitor(comeInPoint);
        else if (choice == VisitorUI.Choice.Reject) spawner.RejectVisitor();

        inPeephole = false;
    }

    private IEnumerator MoveCameraTo(Vector3 targetPos, Quaternion targetRot, float duration)
    {
        Vector3 startPos = cameraTransform.position;
        Quaternion startRot = cameraTransform.rotation;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = transitionEase.Evaluate(Mathf.Clamp01(t / duration));
            cameraTransform.position = Vector3.Lerp(startPos, targetPos, k);
            cameraTransform.rotation = Quaternion.Slerp(startRot, targetRot, k);
            yield return null;
        }
        cameraTransform.position = targetPos;
        cameraTransform.rotation = targetRot;
    }

    private IEnumerator AnimateHatch(bool opening)
    {
        if (hatchTransform == null) yield break;
        Vector3 start = hatchTransform.localPosition;
        Vector3 end = opening ? hatchOpenLocalPos : hatchClosedLocalPos;
        float t = 0f;
        while (t < hatchAnimTime)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, t / hatchAnimTime);
            hatchTransform.localPosition = Vector3.Lerp(start, end, k);
            yield return null;
        }
        hatchTransform.localPosition = end;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.7f, 0.3f, 0.4f);
        Vector3 p = interactPoint != null ? interactPoint.position : transform.position;
        Gizmos.DrawWireSphere(p, interactRange);

        if (peepholeAnchor != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(peepholeAnchor.position, 0.1f);
            Gizmos.DrawRay(peepholeAnchor.position, peepholeAnchor.forward * 0.5f);
        }
    }
}
