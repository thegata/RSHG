using UnityEngine;
using UnityEngine.SceneManagement;

// Главное меню. Повесь на пустой GameObject в сцене меню.
// В сцене должна быть камера (для рендера фона).
public class MainMenu : MonoBehaviour
{
    [Header("Сцены")]
    [Tooltip("Имя сцены, которая грузится по кнопке 'Начать игру'. Должна быть в Build Settings.")]
    public string gameSceneName = "rd";

    [Header("Заголовок")]
    public string gameTitle = "RSHG";
    public string subtitle = "район советский";

    [Header("Оформление")]
    public Color accent = new Color(0.82f, 0.62f, 0.28f);
    public Color buttonColor = new Color(0.16f, 0.16f, 0.18f, 0.95f);
    public Color buttonHover = new Color(0.30f, 0.26f, 0.18f, 0.98f);
    [Tooltip("Затемнять фон сцены под меню.")]
    public bool darkenBackground = true;
    [Range(0f, 1f)] public float darkenAmount = 0.5f;

    private enum Page { Main, Settings }
    private Page page = Page.Main;

    private GUIStyle titleStyle, subStyle, btnStyle, labelStyle, smallStyle, sectionStyle;
    private Texture2D texDark, texBtn, texBtnHover, texPanel, texAccent;
    private bool stylesReady;

    // настройки
    private float masterVolume = 1f;
    private float mouseSensitivity = 1f;
    private bool fullscreen = true;
    private int qualityLevel = 2;

    private void OnEnable()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Time.timeScale = 1f;
        LoadSettings();
    }

    private void LoadSettings()
    {
        masterVolume = PlayerPrefs.GetFloat("MasterVolume", 1f);
        mouseSensitivity = PlayerPrefs.GetFloat("MouseSensitivity", 1f);
        fullscreen = PlayerPrefs.GetInt("Fullscreen", Screen.fullScreen ? 1 : 0) == 1;
        qualityLevel = PlayerPrefs.GetInt("Quality", QualitySettings.GetQualityLevel());
        ApplySettings();
    }

    private void ApplySettings()
    {
        AudioListener.volume = masterVolume;
        Screen.fullScreen = fullscreen;
        qualityLevel = Mathf.Clamp(qualityLevel, 0, QualitySettings.names.Length - 1);
        QualitySettings.SetQualityLevel(qualityLevel, true);
    }

    private void SaveSettings()
    {
        PlayerPrefs.SetFloat("MasterVolume", masterVolume);
        PlayerPrefs.SetFloat("MouseSensitivity", mouseSensitivity);
        PlayerPrefs.SetInt("Fullscreen", fullscreen ? 1 : 0);
        PlayerPrefs.SetInt("Quality", qualityLevel);
        PlayerPrefs.Save();
    }

    private static Texture2D Solid(Color c)
    {
        var t = new Texture2D(1, 1);
        t.SetPixel(0, 0, c); t.Apply();
        return t;
    }

    private void BuildStyles()
    {
        texDark = Solid(new Color(0f, 0f, 0f, darkenAmount));
        texBtn = Solid(buttonColor);
        texBtnHover = Solid(buttonHover);
        texPanel = Solid(new Color(0.10f, 0.10f, 0.12f, 0.96f));
        texAccent = Solid(accent);

        titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 64, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter
        };
        titleStyle.normal.textColor = Color.white;

        subStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 20, fontStyle = FontStyle.Italic, alignment = TextAnchor.MiddleCenter
        };
        subStyle.normal.textColor = accent;

        btnStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 24, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter
        };
        btnStyle.normal.background = texBtn;
        btnStyle.hover.background = texBtnHover;
        btnStyle.active.background = texBtnHover;
        btnStyle.normal.textColor = new Color(0.92f, 0.9f, 0.85f);
        btnStyle.hover.textColor = Color.white;
        btnStyle.active.textColor = Color.white;

        labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 20, alignment = TextAnchor.MiddleLeft };
        labelStyle.normal.textColor = Color.white;

        smallStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.MiddleCenter };
        smallStyle.normal.textColor = new Color(0.7f, 0.7f, 0.75f);

        sectionStyle = new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        sectionStyle.normal.textColor = accent;

        stylesReady = true;
    }

    private void OnGUI()
    {
        if (!stylesReady) BuildStyles();

        int w = Screen.width, h = Screen.height;
        if (darkenBackground) GUI.DrawTexture(new Rect(0, 0, w, h), texDark);

        if (page == Page.Main) DrawMain(w, h);
        else DrawSettings(w, h);
    }

    private void DrawMain(int w, int h)
    {
        GUI.Label(new Rect(0, h * 0.16f, w, 90), gameTitle, titleStyle);
        GUI.DrawTexture(new Rect(w * 0.5f - 90, h * 0.16f + 96, 180, 3), texAccent);
        GUI.Label(new Rect(0, h * 0.16f + 102, w, 30), subtitle, subStyle);

        float bw = 300f, bh = 60f, gap = 18f;
        float x = (w - bw) * 0.5f;
        float y = h * 0.45f;

        if (GUI.Button(new Rect(x, y, bw, bh), "НАЧАТЬ ИГРУ", btnStyle))
            StartGame();
        y += bh + gap;
        if (GUI.Button(new Rect(x, y, bw, bh), "НАСТРОЙКИ", btnStyle))
            page = Page.Settings;
        y += bh + gap;
        if (GUI.Button(new Rect(x, y, bw, bh), "ВЫЙТИ ИЗ ИГРЫ", btnStyle))
            QuitGame();

        GUI.Label(new Rect(0, h - 40, w, 24), "v1.0", smallStyle);
    }

    private void DrawSettings(int w, int h)
    {
        float pw = 520f, ph = 460f;
        var panel = new Rect((w - pw) * 0.5f, (h - ph) * 0.5f, pw, ph);
        GUI.DrawTexture(panel, texPanel);
        GUI.DrawTexture(new Rect(panel.x, panel.y, panel.width, 4), texAccent);

        GUI.Label(new Rect(panel.x, panel.y + 18, panel.width, 40), "НАСТРОЙКИ", sectionStyle);

        float ix = panel.x + 40;
        float iw = panel.width - 80;
        float y = panel.y + 90;

        // Громкость
        GUI.Label(new Rect(ix, y, iw, 26), "Громкость: " + Mathf.RoundToInt(masterVolume * 100) + "%", labelStyle);
        y += 30;
        masterVolume = GUI.HorizontalSlider(new Rect(ix, y, iw, 24), masterVolume, 0f, 1f);
        y += 48;

        // Чувствительность мыши
        GUI.Label(new Rect(ix, y, iw, 26), "Чувствительность мыши: " + mouseSensitivity.ToString("0.00"), labelStyle);
        y += 30;
        mouseSensitivity = GUI.HorizontalSlider(new Rect(ix, y, iw, 24), mouseSensitivity, 0.2f, 3f);
        y += 48;

        // Полный экран
        GUI.Label(new Rect(ix, y, iw - 120, 36), "Полный экран", labelStyle);
        if (GUI.Button(new Rect(ix + iw - 120, y, 120, 36), fullscreen ? "ВКЛ" : "ВЫКЛ", btnStyle))
            fullscreen = !fullscreen;
        y += 52;

        // Качество
        GUI.Label(new Rect(ix, y, iw - 200, 36), "Качество", labelStyle);
        if (GUI.Button(new Rect(ix + iw - 200, y, 50, 36), "◀", btnStyle))
            qualityLevel = Mathf.Max(0, qualityLevel - 1);
        GUI.Label(new Rect(ix + iw - 150, y, 100, 36),
            QualitySettings.names[Mathf.Clamp(qualityLevel, 0, QualitySettings.names.Length - 1)],
            new GUIStyle(labelStyle) { alignment = TextAnchor.MiddleCenter });
        if (GUI.Button(new Rect(ix + iw - 50, y, 50, 36), "▶", btnStyle))
            qualityLevel = Mathf.Min(QualitySettings.names.Length - 1, qualityLevel + 1);
        y += 64;

        // Кнопки внизу
        float bw = (iw - 20) * 0.5f;
        if (GUI.Button(new Rect(ix, panel.yMax - 64, bw, 48), "ПРИМЕНИТЬ", btnStyle))
        {
            ApplySettings();
            SaveSettings();
        }
        if (GUI.Button(new Rect(ix + bw + 20, panel.yMax - 64, bw, 48), "НАЗАД", btnStyle))
        {
            ApplySettings();
            SaveSettings();
            page = Page.Main;
        }
    }

    private void StartGame()
    {
        if (string.IsNullOrEmpty(gameSceneName))
        {
            Debug.LogError("[MainMenu] Не задано имя игровой сцены.");
            return;
        }
        if (Application.CanStreamedLevelBeLoaded(gameSceneName))
            SceneManager.LoadScene(gameSceneName);
        else
            Debug.LogError($"[MainMenu] Сцена '{gameSceneName}' не найдена в Build Settings. " +
                           "Добавь её: File → Build Settings → Add Open Scenes.");
    }

    private void QuitGame()
    {
        SaveSettings();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
