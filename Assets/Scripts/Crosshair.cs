using UnityEngine;

// Независимый прицел-кольцо, всегда виден (для стрельбы, поднятия предметов и т.д.).
// Повесь на любой GameObject (Player / Camera / отдельный объект).
public class Crosshair : MonoBehaviour
{
    [Header("Вид")]
    [SerializeField] private float size = 16f;
    [SerializeField] private float thickness = 2f;
    [SerializeField] private Color color = Color.white;
    [Tooltip("Сколько штрихов в кольце. 0 = сплошное кольцо.")]
    [SerializeField] private int dashes = 8;
    [Tooltip("Доля промежутка между штрихами (0..1).")]
    [SerializeField, Range(0f, 0.9f)] private float gapRatio = 0.5f;
    [Tooltip("Центральная точка в середине кольца.")]
    [SerializeField] private bool centerDot = false;
    [SerializeField] private float centerDotSize = 2f;

    [Header("Поведение")]
    [Tooltip("Прятать прицел при прицеливании из пистолета (ADS).")]
    [SerializeField] private bool hideWhileAiming = false;
    [Tooltip("Прятать прицел когда пистолет в руке (достан).")]
    [SerializeField] private bool hideWhenPistolDrawn = true;

    private Texture2D ringTex;
    private Texture2D dotTex;

    private Texture2D GetRing()
    {
        if (ringTex != null) return ringTex;

        int res = 128;
        ringTex = new Texture2D(res, res, TextureFormat.RGBA32, false);
        ringTex.filterMode = FilterMode.Bilinear;
        ringTex.wrapMode = TextureWrapMode.Clamp;

        float center = (res - 1) * 0.5f;
        float outerR = res * 0.46f;
        float ringPx = Mathf.Clamp(thickness, 1f, 24f) / Mathf.Max(1f, size) * res;
        float innerR = outerR - ringPx;
        float aa = 1.5f;
        float twoPi = Mathf.PI * 2f;

        for (int y = 0; y < res; y++)
        for (int x = 0; x < res; x++)
        {
            float dx = x - center, dy = y - center;
            float dist = Mathf.Sqrt(dx * dx + dy * dy);
            float outerEdge = Mathf.Clamp01((outerR - dist) / aa);
            float innerEdge = Mathf.Clamp01((dist - innerR) / aa);
            float alpha = Mathf.Clamp01(Mathf.Min(outerEdge, innerEdge));

            if (dashes > 0 && alpha > 0f)
            {
                float ang = Mathf.Atan2(dy, dx) + Mathf.PI;
                float seg = ang / twoPi * dashes;
                float frac = seg - Mathf.Floor(seg);
                float dashPart = 1f - gapRatio;
                float edge = 0.06f;
                float dashAlpha = Mathf.Clamp01(Mathf.Min(frac, dashPart - frac) / edge);
                alpha *= dashAlpha;
            }

            ringTex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
        }
        ringTex.Apply();
        return ringTex;
    }

    private Texture2D GetDot()
    {
        if (dotTex != null) return dotTex;
        dotTex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        dotTex.SetPixel(0, 0, Color.white);
        dotTex.Apply();
        return dotTex;
    }

    private void OnGUI()
    {
        if (hideWhileAiming && PistolWeapon.IsAiming) return;
        if (hideWhenPistolDrawn && PistolWeapon.IsPistolDrawn) return;

        int w = Screen.width, h = Screen.height;
        float cx = w * 0.5f, cy = h * 0.5f;

        var ring = GetRing();

        // тень для читаемости на светлом фоне
        GUI.color = new Color(0f, 0f, 0f, 0.55f);
        GUI.DrawTexture(new Rect(cx - size * 0.5f + 1f, cy - size * 0.5f + 1f, size, size), ring);

        GUI.color = color;
        GUI.DrawTexture(new Rect(cx - size * 0.5f, cy - size * 0.5f, size, size), ring);

        if (centerDot)
        {
            var dot = GetDot();
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(new Rect(cx - centerDotSize * 0.5f + 1f, cy - centerDotSize * 0.5f + 1f, centerDotSize, centerDotSize), dot);
            GUI.color = color;
            GUI.DrawTexture(new Rect(cx - centerDotSize * 0.5f, cy - centerDotSize * 0.5f, centerDotSize, centerDotSize), dot);
        }

        GUI.color = Color.white;
    }
}
