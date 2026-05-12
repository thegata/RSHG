using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class DialogueInteractor : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private FirstPersonController controller;
    [SerializeField] private Transform cameraTransform;

    [Header("Detection")]
    [SerializeField] private float interactRange = 4.5f;
    [SerializeField] private float minViewDot = 0.1f;
    [SerializeField] private LayerMask occlusionMask = ~0;
    [SerializeField] private bool requireLineOfSight = false;

    [Header("Focus")]
    [SerializeField] private float lookFocusDuration = 0.45f;
    [SerializeField] private AnimationCurve focusEase =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Debug")]
    [SerializeField] private bool verbose = true;

    private InputAction interactAction;
    private DialogueNPC currentTarget;
    private DialogueNPC lastTarget;
    private bool inDialogue;

    private void Awake()
    {
        if (controller == null) controller = GetComponent<FirstPersonController>();

        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;
        if (cameraTransform == null)
        {
            var anyCam = FindObjectOfType<Camera>();
            if (anyCam != null) cameraTransform = anyCam.transform;
        }

        if (controller == null)
            Debug.LogWarning("[DialogueInteractor] FirstPersonController не найден на этом объекте.", this);
        if (cameraTransform == null)
            Debug.LogError("[DialogueInteractor] Камера не найдена! Поставь тег MainCamera на Main Camera или перетащи камеру в поле Camera Transform.", this);

        var _ = DialogueUI.Instance;

        interactAction = new InputAction("Interact", InputActionType.Button, binding: "<Keyboard>/e");
        interactAction.AddBinding("<Gamepad>/buttonNorth");

        int npcCount = FindObjectsOfType<DialogueNPC>().Length;
        Debug.Log($"[DialogueInteractor] Готов. Радиус взаимодействия: {interactRange}м. " +
                  $"NPC в сцене: {npcCount}. Жми E около них.");
        if (npcCount == 0)
            Debug.LogWarning("[DialogueInteractor] В сцене нет ни одного DialogueNPC. Добавь компонент DialogueNPC на капсулу-человека.", this);
    }

    private void OnEnable() => interactAction?.Enable();
    private void OnDisable() => interactAction?.Disable();
    private void OnDestroy() => interactAction?.Dispose();

    private void Update()
    {
        if (inDialogue) return;

        currentTarget = FindNearestNPC();
        var ui = DialogueUI.Instance;

        if (currentTarget != null)
        {
            ui.ShowPrompt($"[E] Поговорить с {currentTarget.npcName}");
            if (interactAction.WasPressedThisFrame())
                StartCoroutine(DialogueRoutine(currentTarget));
        }
        else
        {
            ui.HidePrompt();
        }

        if (verbose && currentTarget != lastTarget)
        {
            if (currentTarget != null)
                Debug.Log($"[DialogueInteractor] В зоне: {currentTarget.npcName}");
            else if (lastTarget != null)
                Debug.Log("[DialogueInteractor] Никого рядом.");
            lastTarget = currentTarget;
        }
    }

    private DialogueNPC FindNearestNPC()
    {
        if (cameraTransform == null) return null;

        Vector3 viewOrigin = cameraTransform.position;
        Vector3 viewDir = cameraTransform.forward;

        var hits = Physics.OverlapSphere(transform.position, interactRange,
                                         ~0, QueryTriggerInteraction.Collide);
        DialogueNPC best = null;
        float bestDot = minViewDot;

        for (int i = 0; i < hits.Length; i++)
        {
            var npc = hits[i].GetComponentInParent<DialogueNPC>();
            if (npc == null) continue;

            Vector3 lookPoint = npc.GetLookPoint();
            Vector3 to = lookPoint - viewOrigin;
            float dist = to.magnitude;
            if (dist > interactRange + 1f) continue;

            Vector3 dir = to / Mathf.Max(0.0001f, dist);
            float dot = Vector3.Dot(viewDir, dir);
            if (dot < bestDot) continue;

            if (requireLineOfSight &&
                Physics.Raycast(viewOrigin, dir, out RaycastHit hit, dist,
                                occlusionMask, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.GetComponentInParent<DialogueNPC>() != npc &&
                    !hit.collider.transform.IsChildOf(transform.root))
                    continue;
            }

            bestDot = dot;
            best = npc;
        }

        return best;
    }

    private IEnumerator DialogueRoutine(DialogueNPC npc)
    {
        inDialogue = true;
        var ui = DialogueUI.Instance;
        ui.HidePrompt();

        if (controller != null) controller.InputLocked = true;

        yield return FocusOn(npc);

        ui.ShowDialogue(npc.npcName, npc.dialogueLine);

        yield return null;
        while (!interactAction.WasPressedThisFrame()) yield return null;

        ui.HideDialogue();

        if (controller != null) controller.InputLocked = false;
        inDialogue = false;
    }

    private IEnumerator FocusOn(DialogueNPC npc)
    {
        if (controller == null || cameraTransform == null) yield break;

        Vector3 dir = (npc.GetLookPoint() - cameraTransform.position).normalized;
        if (dir.sqrMagnitude < 0.0001f) yield break;

        float targetYaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
        float targetPitch = -Mathf.Asin(Mathf.Clamp(dir.y, -1f, 1f)) * Mathf.Rad2Deg;

        float startYaw = controller.Yaw;
        float startPitch = controller.Pitch;
        float yawDelta = Mathf.DeltaAngle(startYaw, targetYaw);

        float t = 0f;
        while (t < lookFocusDuration)
        {
            t += Time.deltaTime;
            float k = focusEase.Evaluate(Mathf.Clamp01(t / lookFocusDuration));
            controller.Yaw = startYaw + yawDelta * k;
            controller.Pitch = Mathf.Lerp(startPitch, targetPitch, k);
            yield return null;
        }

        controller.Yaw = startYaw + yawDelta;
        controller.Pitch = targetPitch;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.4f, 0.9f, 1f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, interactRange);
    }
}
