using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

// Настраивает направленный свет как солнце и вешает на него HDRP Lens Flare (SRP).
// Блики появляются когда солнце в кадре и гаснут когда его заслоняют.
// Повесь на GameObject "Sun" (направленный свет) или на новый объект.
public class SunLensFlare : MonoBehaviour
{
    [Header("Солнце")]
    public bool configureSun = true;
    public Color sunColor = new Color(1f, 0.96f, 0.84f);
    [Tooltip("Поворот солнца. X = высота над горизонтом, Y = направление.")]
    public Vector3 sunAngles = new Vector3(48f, -28f, 0f);
    [Tooltip("Яркость в люксах (HDRP). Ясный день ~100000.")]
    public float sunIntensityLux = 95000f;
    public float sunTemperatureK = 6200f;

    [Header("Блики (Lens Flare)")]
    public float flareScale = 1.3f;
    public Color flareTint = new Color(1f, 0.92f, 0.78f);
    [Tooltip("Добавлять тонкие блики-кружочки по линии взгляда.")]
    public bool showFlareDots = true;

    [Header("Плавное появление")]
    [Tooltip("Максимальная яркость, когда смотришь прямо на солнце.")]
    public float maxIntensity = 2.2f;
    [Tooltip("Доля пути до края экрана, на которой блики уже погасли (0.6 = гаснут не доходя до края, без щелчка).")]
    [Range(0.2f, 1f)] public float edgeFade = 0.65f;
    [Tooltip("Резкость нарастания к центру. Больше = блик копится у самого солнца.")]
    public float fadeCurvePower = 2.5f;
    [Tooltip("Плавность во времени.")]
    public float fadeSpeed = 8f;

    [Header("Окклюзия (прятать за объектами)")]
    public bool useOcclusion = true;
    [Range(0f, 1f)] public float occlusionRadius = 0.25f;
    [Range(1, 64)] public int sampleCount = 32;

    private Light sun;
    private LensFlareComponentSRP flareComp;
    private LensFlareDataSRP flareData;
    private Camera cachedCam;
    private float currentIntensity;

    private void Start()
    {
        SetupSun();
        SetupFlare();
    }

    private void Update()
    {
        if (flareComp == null) return;
        if (cachedCam == null) cachedCam = Camera.main;
        if (cachedCam == null) return;

        // Точка "солнца" далеко по направлению, откуда светит свет
        Vector3 sunWorld = cachedCam.transform.position - transform.forward * 5000f;
        Vector3 vp = cachedCam.WorldToViewportPoint(sunWorld);

        float target = 0f;
        if (vp.z > 0f)
        {
            // расстояние от центра экрана: 0 в центре, 1 у края
            float dx = Mathf.Abs(vp.x - 0.5f) * 2f;
            float dy = Mathf.Abs(vp.y - 0.5f) * 2f;
            float edgeDist = Mathf.Max(dx, dy);

            // 1 в центре -> 0 на edgeFade (до края экрана)
            float t = 1f - Mathf.InverseLerp(0f, edgeFade, edgeDist);
            t = Mathf.Pow(Mathf.Clamp01(t), Mathf.Max(0.1f, fadeCurvePower));
            target = t * maxIntensity;
        }

        currentIntensity = Mathf.Lerp(currentIntensity, target, fadeSpeed * Time.deltaTime);
        flareComp.intensity = currentIntensity;
    }

    private void SetupSun()
    {
        sun = GetComponent<Light>();
        if (sun == null)
        {
            sun = gameObject.AddComponent<Light>();
            sun.type = LightType.Directional;
        }

        if (!configureSun) return;

        sun.type = LightType.Directional;
        sun.color = sunColor;
        sun.useColorTemperature = true;
        sun.colorTemperature = sunTemperatureK;
        sun.shadows = LightShadows.Soft;
        transform.rotation = Quaternion.Euler(sunAngles);

        var hd = GetComponent<HDAdditionalLightData>();
        if (hd == null) hd = gameObject.AddComponent<HDAdditionalLightData>();
        hd.lightUnit = LightUnit.Lux;
        hd.intensity = sunIntensityLux;
        hd.EnableShadows(true);
    }

    private void SetupFlare()
    {
        flareComp = GetComponent<LensFlareComponentSRP>();
        if (flareComp == null) flareComp = gameObject.AddComponent<LensFlareComponentSRP>();

        flareData = ScriptableObject.CreateInstance<LensFlareDataSRP>();
        flareData.elements = BuildElements();

        flareComp.lensFlareData = flareData;
        flareComp.intensity = 0f; // яркость рулит Update по углу
        flareComp.scale = flareScale;
        flareComp.attenuationByLightShape = false;
        flareComp.useOcclusion = useOcclusion;
        flareComp.occlusionRadius = occlusionRadius;
        flareComp.sampleCount = (uint)sampleCount;
        flareComp.allowOffScreen = false;
        flareComp.enabled = true;

        Vector3 sunDir = -transform.forward; // куда смотреть, чтобы увидеть солнце
        Debug.Log($"[SunLensFlare] Готово. Чтобы увидеть блик — смотри в сторону {sunDir} " +
                  $"(в небо, ОТКУДА светит солнце). Тип света: {(sun != null ? sun.type.ToString() : "нет")}, " +
                  $"окклюзия: {useOcclusion}.", this);

        if (sun != null && sun.type != LightType.Directional)
            Debug.LogWarning("[SunLensFlare] Свет не Directional — для солнца поставь тип Directional.", this);
    }

    private Texture2D glowTex;
    private Texture2D ghostTex;

    private Texture2D MakeRadialGlow(int size, float power)
    {
        var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
        t.wrapMode = TextureWrapMode.Clamp;
        t.filterMode = FilterMode.Bilinear;
        float c = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = (x - c) / c, dy = (y - c) / c;
            float dist = Mathf.Sqrt(dx * dx + dy * dy);
            float a = Mathf.Clamp01(1f - dist);
            a = Mathf.Pow(a, power);
            t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
        }
        t.Apply();
        return t;
    }

    private LensFlareDataElementSRP[] BuildElements()
    {
        if (glowTex == null) glowTex = MakeRadialGlow(256, 2.2f);   // мягкий ореол
        if (ghostTex == null) ghostTex = MakeRadialGlow(128, 1.4f); // более ровные ghost'ы

        var list = new List<LensFlareDataElementSRP>();

        // 1) Большой мягкий ореол вокруг солнца (текстурный градиент — натурально)
        list.Add(new LensFlareDataElementSRP
        {
            flareType = SRPLensFlareType.Image,
            lensFlareTexture = glowTex,
            position = 0f,
            uniformScale = 7f,
            localIntensity = 0.9f,
            tint = flareTint,
            blendMode = SRPLensFlareBlendMode.Screen,
            modulateByLightColor = true,
            preserveAspectRatio = true
        });

        // 2) Плотное яркое ядро (сам диск)
        list.Add(new LensFlareDataElementSRP
        {
            flareType = SRPLensFlareType.Image,
            lensFlareTexture = glowTex,
            position = 0f,
            uniformScale = 2.2f,
            localIntensity = 1.6f,
            tint = new Color(1f, 0.97f, 0.88f),
            blendMode = SRPLensFlareBlendMode.Screen,
            modulateByLightColor = true,
            preserveAspectRatio = true
        });

        // 3) Тонкие ghost'ы по линии взгляда (опционально)
        if (showFlareDots)
        {
            list.Add(new LensFlareDataElementSRP
            {
                flareType = SRPLensFlareType.Image,
                lensFlareTexture = ghostTex,
                allowMultipleElement = true,
                count = 4,
                position = 1.0f,
                lengthSpread = 2.4f,
                uniformScale = 0.45f,
                localIntensity = 0.18f,
                tint = new Color(1f, 0.9f, 0.78f),
                blendMode = SRPLensFlareBlendMode.Screen,
                preserveAspectRatio = true
            });

            list.Add(new LensFlareDataElementSRP
            {
                flareType = SRPLensFlareType.Image,
                lensFlareTexture = ghostTex,
                allowMultipleElement = true,
                count = 2,
                position = -0.6f,
                lengthSpread = 1.4f,
                uniformScale = 0.9f,
                localIntensity = 0.12f,
                tint = new Color(0.8f, 0.88f, 1f),
                blendMode = SRPLensFlareBlendMode.Screen,
                preserveAspectRatio = true
            });
        }

        return list.ToArray();
    }
}
