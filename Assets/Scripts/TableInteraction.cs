using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class TableInteraction : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private Camera tableCamera;
    [SerializeField] private FirstPersonController controller;

    [Header("Detection")]
    [SerializeField] private float interactRange = 6f;
    [SerializeField] private LayerMask hitMask = ~0;

    [Header("Outline")]
    [SerializeField] private Color outlineColor = Color.white;
    [Tooltip("HDR-яркость контура. >1 даёт свечение с Bloom.")]
    [SerializeField] private float outlineBrightness = 4f;
    [Tooltip("Толщина контура. 1.02 — тонкий, 1.06 — жирный.")]
    [SerializeField, Range(1.0f, 1.15f)] private float outlineScale = 1.03f;
    [SerializeField] private float highlightSmoothing = 14f;
    [SerializeField] private bool keepHighlightWhileAtTable = true;

    [Header("Body Glow (optional)")]
    [Tooltip("Дополнительная эмиссия по всему телу. По умолчанию выключена — только контур.")]
    [SerializeField] private bool enableBodyEmission = false;
    [SerializeField] private float bodyEmissionIntensity = 1.5f;

    [Header("Prompt")]
    [SerializeField] private string prompt = "[E] Подойти к столу";
    [SerializeField] private string returnPrompt = "[E] Назад";

    [Header("Cursor While At Table")]
    [SerializeField] private bool unlockCursorAtTable = false;

    [Header("Debug")]
    [SerializeField] private bool verbose = true;

    private InputAction interactAction;

    private Renderer[] cachedRenderers;
    private List<Material> instancedMaterials = new List<Material>();
    private List<GameObject> outlineHulls = new List<GameObject>();
    private List<Material> outlineMaterials = new List<Material>();

    private float currentGlow;
    private bool isHovered;
    private bool isAtTable;

    private AudioListener playerListener;
    private AudioListener tableListener;

    private RaycastHit[] hitBuffer = new RaycastHit[16];

    private void Awake()
    {
        if (controller == null) controller = FindObjectOfType<FirstPersonController>();
        if (playerCamera == null && Camera.main != null) playerCamera = Camera.main;
        if (tableCamera == null)
        {
            var go = GameObject.Find("TableCam");
            if (go != null) tableCamera = go.GetComponent<Camera>();
        }

        if (playerCamera != null) playerListener = playerCamera.GetComponent<AudioListener>();
        if (tableCamera != null)
        {
            tableListener = tableCamera.GetComponent<AudioListener>();
            tableCamera.enabled = false;
            if (tableListener != null) tableListener.enabled = false;
        }
        else Debug.LogError("[TableInteraction] Не найдена Table Cam! Перетащи её в поле Table Camera или назови 'TableCam'.", this);

        CacheRenderersAndMaterials();
        BuildOutlineHulls();

        interactAction = new InputAction("TableInteract", InputActionType.Button, binding: "<Keyboard>/e");
        interactAction.AddBinding("<Gamepad>/buttonNorth");

        if (verbose) Debug.Log($"[TableInteraction] Готов. Рендереров: {cachedRenderers?.Length ?? 0}, контурных копий: {outlineHulls.Count}.");
    }

    private void CacheRenderersAndMaterials()
    {
        cachedRenderers = GetComponentsInChildren<Renderer>();
        if (!enableBodyEmission) return;

        foreach (var r in cachedRenderers)
        {
            if (r == null) continue;
            var mats = r.materials;
            for (int i = 0; i < mats.Length; i++)
            {
                var m = mats[i];
                if (m == null) continue;
                m.EnableKeyword("_EMISSION");
                m.EnableKeyword("_EMISSIVE_COLOR_MAP");
                if (m.HasProperty("_EmissiveColor")) m.SetColor("_EmissiveColor", Color.black);
                if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", Color.black);
                instancedMaterials.Add(m);
            }
            r.materials = mats;
        }
    }

    private static Mesh GetMeshFromRenderer(Renderer r)
    {
        var mf = r.GetComponent<MeshFilter>();
        if (mf != null && mf.sharedMesh != null) return mf.sharedMesh;
        var smr = r as SkinnedMeshRenderer;
        if (smr != null) return smr.sharedMesh;
        return null;
    }

    private void BuildOutlineHulls()
    {
        Shader unlit = Shader.Find("HDRP/Unlit");
        if (unlit == null) unlit = Shader.Find("Universal Render Pipeline/Unlit");
        if (unlit == null) unlit = Shader.Find("Unlit/Color");
        if (unlit == null) { Debug.LogWarning("[TableInteraction] Не нашёл Unlit-шейдер для контура."); return; }

        foreach (var r in cachedRenderers)
        {
            if (r == null) continue;
            Mesh mesh = GetMeshFromRenderer(r);
            if (mesh == null) continue;

            var hullGO = new GameObject(r.name + "_Outline");
            hullGO.transform.SetParent(r.transform, false);
            hullGO.transform.localPosition = Vector3.zero;
            hullGO.transform.localRotation = Quaternion.identity;
            hullGO.transform.localScale = Vector3.one * outlineScale;

            if (r is SkinnedMeshRenderer srcSmr)
            {
                var smr = hullGO.AddComponent<SkinnedMeshRenderer>();
                smr.sharedMesh = mesh;
                smr.bones = srcSmr.bones;
                smr.rootBone = srcSmr.rootBone;
                smr.updateWhenOffscreen = true;
                smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                smr.receiveShadows = false;
                SetupOutlineMaterial(smr, unlit);
            }
            else
            {
                var hmf = hullGO.AddComponent<MeshFilter>();
                hmf.sharedMesh = mesh;
                var hmr = hullGO.AddComponent<MeshRenderer>();
                hmr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                hmr.receiveShadows = false;
                SetupOutlineMaterial(hmr, unlit);
            }

            hullGO.SetActive(false);
            outlineHulls.Add(hullGO);
        }
    }

    private void SetupOutlineMaterial(Renderer rend, Shader shader)
    {
        var mat = new Material(shader);
        Color hdrColor = outlineColor * outlineBrightness;

        if (mat.HasProperty("_UnlitColor")) mat.SetColor("_UnlitColor", hdrColor);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", hdrColor);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", hdrColor);

        if (mat.HasProperty("_CullMode")) mat.SetFloat("_CullMode", 1f);
        if (mat.HasProperty("_CullModeForward")) mat.SetFloat("_CullModeForward", 1f);
        if (mat.HasProperty("_DoubleSidedEnable")) mat.SetFloat("_DoubleSidedEnable", 0f);
        mat.DisableKeyword("_DOUBLESIDED_ON");
        mat.renderQueue = 3010;

        if (mat.HasProperty("_EmissiveColor"))
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissiveColor", hdrColor);
            if (mat.HasProperty("_EmissiveIntensity")) mat.SetFloat("_EmissiveIntensity", 1f);
            if (mat.HasProperty("_EmissiveExposureWeight")) mat.SetFloat("_EmissiveExposureWeight", 1f);
        }

        rend.material = mat;
        outlineMaterials.Add(mat);
    }

    private void OnEnable() => interactAction?.Enable();
    private void OnDisable() => interactAction?.Disable();
    private void OnDestroy() => interactAction?.Dispose();

    private void Update()
    {
        if (playerCamera == null) return;

        if (isAtTable)
        {
            float t = keepHighlightWhileAtTable ? 1f : 0f;
            currentGlow = Mathf.Lerp(currentGlow, t, highlightSmoothing * Time.deltaTime);
            ApplyHighlight(currentGlow);

            DialogueUI.Instance.ShowPrompt(returnPrompt);
            if (interactAction.WasPressedThisFrame()) SwitchToPlayer();
            return;
        }

        isHovered = CheckCrosshairOnSelf();

        if (isHovered)
        {
            DialogueUI.Instance.ShowPrompt(prompt);
            if (interactAction.WasPressedThisFrame()) SwitchToTable();
        }
        else
        {
            DialogueUI.Instance.HidePrompt();
        }

        float target = isHovered ? 1f : 0f;
        currentGlow = Mathf.Lerp(currentGlow, target, highlightSmoothing * Time.deltaTime);
        ApplyHighlight(currentGlow);
    }

    private bool CheckCrosshairOnSelf()
    {
        Vector3 origin = playerCamera.transform.position;
        Vector3 dir = playerCamera.transform.forward;

        int count = Physics.RaycastNonAlloc(origin, dir, hitBuffer, interactRange, hitMask,
                                            QueryTriggerInteraction.Ignore);
        if (count == 0) return false;

        System.Array.Sort(hitBuffer, 0, count, RaycastHitComparer.Instance);

        for (int i = 0; i < count; i++)
        {
            var h = hitBuffer[i];
            if (h.collider == null) continue;
            if (h.transform.IsChildOf(playerCamera.transform)) continue;
            if (IsOurOutlineHull(h.transform)) continue;

            if (h.transform == transform || h.transform.IsChildOf(transform)) return true;
            return false;
        }
        return false;
    }

    private bool IsOurOutlineHull(Transform t)
    {
        for (int i = 0; i < outlineHulls.Count; i++)
            if (outlineHulls[i] != null && (t == outlineHulls[i].transform || t.IsChildOf(outlineHulls[i].transform)))
                return true;
        return false;
    }

    private void ApplyHighlight(float strength)
    {
        if (enableBodyEmission)
        {
            Color emit = outlineColor * bodyEmissionIntensity * strength;
            foreach (var m in instancedMaterials)
            {
                if (m == null) continue;
                if (m.HasProperty("_EmissiveColor")) m.SetColor("_EmissiveColor", emit);
                if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", emit);
            }
        }

        Color outlineCol = outlineColor * outlineBrightness * strength;
        foreach (var m in outlineMaterials)
        {
            if (m == null) continue;
            if (m.HasProperty("_UnlitColor")) m.SetColor("_UnlitColor", outlineCol);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", outlineCol);
            if (m.HasProperty("_Color")) m.SetColor("_Color", outlineCol);
            if (m.HasProperty("_EmissiveColor")) m.SetColor("_EmissiveColor", outlineCol);
        }

        bool showOutline = strength > 0.01f;
        foreach (var hull in outlineHulls)
            if (hull != null && hull.activeSelf != showOutline)
                hull.SetActive(showOutline);
    }

    private void SwitchToTable()
    {
        if (isAtTable) return;
        isAtTable = true;

        if (controller != null) controller.InputLocked = true;
        if (playerCamera != null) playerCamera.enabled = false;
        if (tableCamera != null) tableCamera.enabled = true;
        if (playerListener != null) playerListener.enabled = false;
        if (tableListener != null) tableListener.enabled = true;

        if (unlockCursorAtTable)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        if (verbose) Debug.Log("[TableInteraction] Переход к столу.");
    }

    private void SwitchToPlayer()
    {
        if (!isAtTable) return;
        isAtTable = false;

        if (playerCamera != null) playerCamera.enabled = true;
        if (tableCamera != null) tableCamera.enabled = false;
        if (playerListener != null) playerListener.enabled = true;
        if (tableListener != null) tableListener.enabled = false;
        if (controller != null) controller.InputLocked = false;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        DialogueUI.Instance.HidePrompt();

        if (verbose) Debug.Log("[TableInteraction] Возврат к игроку.");
    }

    private class RaycastHitComparer : IComparer<RaycastHit>
    {
        public static readonly RaycastHitComparer Instance = new RaycastHitComparer();
        public int Compare(RaycastHit a, RaycastHit b) => a.distance.CompareTo(b.distance);
    }
}
