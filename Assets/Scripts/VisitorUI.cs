using UnityEngine;

public class VisitorUI : MonoBehaviour
{
    public enum Choice { None, Accept, Reject }

    private static VisitorUI _instance;
    public static VisitorUI Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject("[VisitorUI]");
                _instance = go.AddComponent<VisitorUI>();
            }
            return _instance;
        }
    }

    public Choice PendingChoice { get; private set; } = Choice.None;

    private string notificationText;
    private float notificationTimer;
    private float notificationFadeIn;
    private bool showButtons;

    private GUIStyle notifBoxStyle;
    private GUIStyle notifTextStyle;
    private GUIStyle btnStyle;
    private GUIStyle btnAcceptStyle;
    private GUIStyle btnRejectStyle;
    private Texture2D darkBg;

    private void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void ShowNotification(string text, float duration = 4f)
    {
        notificationText = text;
        notificationTimer = duration;
        notificationFadeIn = 0f;
    }

    public void ShowChoice()
    {
        PendingChoice = Choice.None;
        showButtons = true;
    }

    public void HideChoice()
    {
        showButtons = false;
    }

    public void ClearChoice()
    {
        PendingChoice = Choice.None;
    }

    private void Update()
    {
        if (notificationTimer > 0f)
        {
            notificationFadeIn = Mathf.MoveTowards(notificationFadeIn, 1f, Time.deltaTime * 4f);
            notificationTimer -= Time.deltaTime;
        }
        else
        {
            notificationFadeIn = Mathf.MoveTowards(notificationFadeIn, 0f, Time.deltaTime * 3f);
        }
    }

    private void EnsureStyles()
    {
        if (darkBg == null)
        {
            darkBg = new Texture2D(1, 1);
            darkBg.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.85f));
            darkBg.Apply();
        }

        if (notifBoxStyle == null)
        {
            notifBoxStyle = new GUIStyle();
            notifBoxStyle.normal.background = darkBg;
            notifBoxStyle.border = new RectOffset(4, 4, 4, 4);
            notifBoxStyle.padding = new RectOffset(20, 20, 14, 14);
        }
        if (notifTextStyle == null)
        {
            notifTextStyle = new GUIStyle(GUI.skin.label) {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                wordWrap = true,
            };
        }
        if (btnStyle == null)
        {
            btnStyle = new GUIStyle(GUI.skin.button) {
                fontSize = 24, fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
            };
        }
        if (btnAcceptStyle == null) btnAcceptStyle = new GUIStyle(btnStyle);
        if (btnRejectStyle == null) btnRejectStyle = new GUIStyle(btnStyle);
        btnAcceptStyle.normal.textColor = new Color(0.5f, 1f, 0.6f);
        btnAcceptStyle.hover.textColor = new Color(0.8f, 1f, 0.8f);
        btnRejectStyle.normal.textColor = new Color(1f, 0.55f, 0.5f);
        btnRejectStyle.hover.textColor = new Color(1f, 0.8f, 0.75f);
    }

    private void OnGUI()
    {
        EnsureStyles();
        int w = Screen.width, h = Screen.height;

        if (notificationFadeIn > 0.01f && !string.IsNullOrEmpty(notificationText))
        {
            var prevColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, notificationFadeIn);

            float boxW = 440f, boxH = 90f;
            var boxRect = new Rect(w - boxW - 30f, 30f, boxW, boxH);
            GUI.Box(boxRect, GUIContent.none, notifBoxStyle);

            notifTextStyle.normal.textColor = new Color(1f, 0.9f, 0.4f, notificationFadeIn);
            GUI.Label(new Rect(boxRect.x + 20f, boxRect.y, 24f, boxH), "!", notifTextStyle);

            notifTextStyle.normal.textColor = new Color(1f, 1f, 1f, notificationFadeIn);
            GUI.Label(new Rect(boxRect.x + 50f, boxRect.y, boxW - 70f, boxH),
                      notificationText, notifTextStyle);

            GUI.color = prevColor;
        }

        if (showButtons)
        {
            GUI.color = new Color(0f, 0f, 0f, 0.5f);
            GUI.DrawTexture(new Rect(0, 0, w, h), darkBg);
            GUI.color = Color.white;

            var titleStyle = new GUIStyle(GUI.skin.label) {
                fontSize = 30, fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            titleStyle.normal.textColor = Color.white;
            GUI.Label(new Rect(0, h * 0.55f, w, 50f), "Что будем делать?", titleStyle);

            float bw = 220f, bh = 80f, gap = 30f;
            float total = bw * 2f + gap;
            float startX = (w - total) * 0.5f;
            float y = h * 0.65f;

            if (GUI.Button(new Rect(startX, y, bw, bh), "ВПУСТИТЬ", btnAcceptStyle))
                PendingChoice = Choice.Accept;
            if (GUI.Button(new Rect(startX + bw + gap, y, bw, bh), "НЕ ВПУСКАТЬ", btnRejectStyle))
                PendingChoice = Choice.Reject;
        }
    }
}
