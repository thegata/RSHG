using UnityEngine;
using UnityEngine.UI;

public class DialogueUI : MonoBehaviour
{
    private static DialogueUI _instance;
    public static DialogueUI Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject("[DialogueUI]");
                _instance = go.AddComponent<DialogueUI>();
            }
            return _instance;
        }
    }

    private CanvasGroup promptGroup;
    private Text promptText;

    private CanvasGroup dialogueGroup;
    private Text nameText;
    private Text lineText;

    private float promptTargetAlpha;
    private float dialogueTargetAlpha;

    private string fallbackPrompt;
    private string fallbackName;
    private string fallbackLine;
    private bool fallbackPromptShown;
    private bool fallbackDialogueShown;

    private void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        DontDestroyOnLoad(gameObject);
        BuildUI();
        Debug.Log("[DialogueUI] UI создан.");
    }

    private void Update()
    {
        if (promptGroup != null)
            promptGroup.alpha = Mathf.MoveTowards(promptGroup.alpha, promptTargetAlpha, Time.deltaTime * 8f);
        if (dialogueGroup != null)
            dialogueGroup.alpha = Mathf.MoveTowards(dialogueGroup.alpha, dialogueTargetAlpha, Time.deltaTime * 5f);
    }

    public void ShowPrompt(string text)
    {
        fallbackPrompt = text;
        fallbackPromptShown = true;
        if (promptText != null) promptText.text = text;
        promptTargetAlpha = 1f;
    }

    public void HidePrompt()
    {
        fallbackPromptShown = false;
        promptTargetAlpha = 0f;
    }

    public void ShowDialogue(string npcName, string line)
    {
        fallbackName = npcName;
        fallbackLine = line;
        fallbackDialogueShown = true;
        if (nameText != null) nameText.text = npcName;
        if (lineText != null) lineText.text = line;
        dialogueTargetAlpha = 1f;
    }

    public void HideDialogue()
    {
        fallbackDialogueShown = false;
        dialogueTargetAlpha = 0f;
    }

    private void OnGUI()
    {
        if (promptText != null && promptText.font != null && lineText != null && lineText.font != null)
            return;

        int w = Screen.width;
        int h = Screen.height;

        if (fallbackPromptShown && !string.IsNullOrEmpty(fallbackPrompt))
        {
            var style = new GUIStyle(GUI.skin.label) {
                fontSize = 22, alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
            var size = style.CalcSize(new GUIContent(fallbackPrompt));
            var rect = new Rect((w - size.x - 40) * 0.5f, h * 0.5f + 100f, size.x + 40f, size.y + 16f);
            GUI.Box(rect, GUIContent.none);
            GUI.Label(rect, fallbackPrompt, style);
        }

        if (fallbackDialogueShown)
        {
            var panel = new Rect(0, h - 260, w, 260);
            GUI.Box(panel, GUIContent.none);

            var nameStyle = new GUIStyle(GUI.skin.label) {
                fontSize = 24, fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 0.83f, 0.5f) }
            };
            var lineStyle = new GUIStyle(GUI.skin.label) {
                fontSize = 26, wordWrap = true,
                normal = { textColor = Color.white }
            };
            var hintStyle = new GUIStyle(GUI.skin.label) {
                fontSize = 16,
                normal = { textColor = new Color(1f, 1f, 1f, 0.6f) },
                alignment = TextAnchor.MiddleRight
            };

            GUI.Label(new Rect(60, h - 220, w - 120, 40), fallbackName ?? "", nameStyle);
            GUI.Label(new Rect(60, h - 170, w - 120, 110), fallbackLine ?? "", lineStyle);
            GUI.Label(new Rect(w - 220, h - 50, 180, 30), "[E] Дальше", hintStyle);
        }
    }

    private void BuildUI()
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        if (font == null) font = Font.CreateDynamicFontFromOSFont("Arial", 14);
        if (font == null) {
            var os = Font.GetOSInstalledFontNames();
            if (os != null && os.Length > 0)
                font = Font.CreateDynamicFontFromOSFont(os, 14);
        }
        if (font == null)
            Debug.LogWarning("[DialogueUI] Шрифт не загрузился, текст будет показан через IMGUI.");

        var canvasGO = new GameObject("Canvas", typeof(RectTransform));
        canvasGO.transform.SetParent(transform, false);
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();

        BuildPrompt(canvasGO.transform, font);
        BuildDialogue(canvasGO.transform, font);
    }

    private static RectTransform NewUI(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.localPosition = Vector3.zero;
        rt.localScale = Vector3.one;
        return rt;
    }

    private void BuildPrompt(Transform parent, Font font)
    {
        var rt = NewUI("Prompt", parent);
        promptGroup = rt.gameObject.AddComponent<CanvasGroup>();
        promptGroup.alpha = 0f;
        promptGroup.blocksRaycasts = false;
        promptGroup.interactable = false;

        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, -140f);
        rt.sizeDelta = new Vector2(700f, 70f);

        var bgRT = NewUI("BG", rt);
        bgRT.anchorMin = Vector2.zero; bgRT.anchorMax = Vector2.one;
        bgRT.offsetMin = Vector2.zero; bgRT.offsetMax = Vector2.zero;
        var bgImg = bgRT.gameObject.AddComponent<Image>();
        bgImg.color = new Color(0f, 0f, 0f, 0.45f);
        bgImg.raycastTarget = false;

        var trt = NewUI("Text", rt);
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = new Vector2(20f, 0f);
        trt.offsetMax = new Vector2(-20f, 0f);

        promptText = trt.gameObject.AddComponent<Text>();
        promptText.font = font;
        promptText.fontSize = 30;
        promptText.alignment = TextAnchor.MiddleCenter;
        promptText.color = new Color(1f, 1f, 1f, 0.95f);
        promptText.raycastTarget = false;
        promptText.text = "[E] Поговорить";

        var shadow = trt.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.9f);
        shadow.effectDistance = new Vector2(2f, -2f);
    }

    private void BuildDialogue(Transform parent, Font font)
    {
        var rt = NewUI("Dialogue", parent);
        dialogueGroup = rt.gameObject.AddComponent<CanvasGroup>();
        dialogueGroup.alpha = 0f;
        dialogueGroup.blocksRaycasts = false;
        dialogueGroup.interactable = false;

        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(0f, 340f);

        var bgRT = NewUI("Gradient", rt);
        bgRT.anchorMin = Vector2.zero; bgRT.anchorMax = Vector2.one;
        bgRT.offsetMin = Vector2.zero; bgRT.offsetMax = Vector2.zero;
        var bgImg = bgRT.gameObject.AddComponent<Image>();
        bgImg.sprite = CreateGradientSprite();
        bgImg.color = Color.white;
        bgImg.raycastTarget = false;

        var nrt = NewUI("Name", rt);
        nrt.anchorMin = new Vector2(0f, 1f);
        nrt.anchorMax = new Vector2(1f, 1f);
        nrt.pivot = new Vector2(0f, 1f);
        nrt.offsetMin = new Vector2(280f, -80f);
        nrt.offsetMax = new Vector2(-280f, -30f);

        nameText = nrt.gameObject.AddComponent<Text>();
        nameText.font = font;
        nameText.fontSize = 30;
        nameText.fontStyle = FontStyle.Bold;
        nameText.alignment = TextAnchor.MiddleLeft;
        nameText.color = new Color(1f, 0.83f, 0.5f, 1f);
        nameText.raycastTarget = false;

        var nShadow = nrt.gameObject.AddComponent<Shadow>();
        nShadow.effectColor = new Color(0f, 0f, 0f, 0.9f);
        nShadow.effectDistance = new Vector2(2f, -2f);

        var lrt = NewUI("Line", rt);
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = new Vector2(280f, 60f);
        lrt.offsetMax = new Vector2(-280f, -90f);

        lineText = lrt.gameObject.AddComponent<Text>();
        lineText.font = font;
        lineText.fontSize = 34;
        lineText.alignment = TextAnchor.UpperLeft;
        lineText.color = Color.white;
        lineText.raycastTarget = false;
        lineText.horizontalOverflow = HorizontalWrapMode.Wrap;
        lineText.verticalOverflow = VerticalWrapMode.Overflow;

        var lShadow = lrt.gameObject.AddComponent<Shadow>();
        lShadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
        lShadow.effectDistance = new Vector2(2f, -2f);

        var hrt = NewUI("ContinueHint", rt);
        hrt.anchorMin = new Vector2(1f, 0f);
        hrt.anchorMax = new Vector2(1f, 0f);
        hrt.pivot = new Vector2(1f, 0f);
        hrt.anchoredPosition = new Vector2(-60f, 30f);
        hrt.sizeDelta = new Vector2(280f, 36f);

        var hint = hrt.gameObject.AddComponent<Text>();
        hint.font = font;
        hint.fontSize = 22;
        hint.alignment = TextAnchor.MiddleRight;
        hint.color = new Color(1f, 1f, 1f, 0.55f);
        hint.raycastTarget = false;
        hint.text = "[E] Дальше";
    }

    private Sprite CreateGradientSprite()
    {
        int h = 128;
        var tex = new Texture2D(2, h, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        for (int y = 0; y < h; y++)
        {
            float t = (float)y / (h - 1);
            float a = Mathf.SmoothStep(0f, 0.92f, 1f - t);
            var c = new Color(0f, 0f, 0f, a);
            tex.SetPixel(0, y, c);
            tex.SetPixel(1, y, c);
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 2, h), new Vector2(0.5f, 0.5f));
    }
}
