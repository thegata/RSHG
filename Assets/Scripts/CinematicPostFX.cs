using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

// Чистый реалистичный пост-процесс для HDRP (без «глитчей»).
// Повесь на Main Camera или отдельный GameObject "PostFX".
public class CinematicPostFX : MonoBehaviour
{
    [Header("Эффекты — вкл/выкл")]
    public bool tonemapping = true;
    public bool ambientOcclusion = true;
    public bool bloom = true;
    public bool vignette = true;
    public bool colorGrading = true;
    public bool whiteBalance = true;

    [Header("Глитчевые (выкл по умолчанию)")]
    public bool filmGrain = false;
    public bool chromaticAberration = false;
    public bool motionBlur = false;
    public bool lensDistortion = false;
    public bool paniniProjection = false;
    public bool depthOfField = false;

    [Header("Яркость")]
    [Tooltip("Если темно — подними. Если пересвет — опусти.")]
    [Range(-3f, 3f)] public float postExposure = 0.35f;

    [Header("Tonemapping")]
    [Tooltip("Neutral = мягко и светло, ACES = контрастнее и кинематографичнее.")]
    public TonemappingMode tonemappingMode = TonemappingMode.Neutral;

    [Header("Ambient Occlusion (мягкие тени в углах)")]
    [Range(0f, 2f)] public float aoIntensity = 0.6f;
    [Range(0.25f, 5f)] public float aoRadius = 0.5f;

    [Header("Bloom")]
    [Range(0f, 1f)] public float bloomIntensity = 0.12f;
    [Range(0f, 1f)] public float bloomThreshold = 0.9f;
    [Range(0f, 1f)] public float bloomScatter = 0.6f;

    [Header("Vignette (лёгкое)")]
    [Range(0f, 1f)] public float vignetteIntensity = 0.16f;
    [Range(0f, 1f)] public float vignetteSmoothness = 0.5f;

    [Header("Color Adjustments")]
    [Range(-100f, 100f)] public float contrast = 6f;
    [Range(-100f, 100f)] public float saturation = 5f;
    public Color colorFilter = Color.white;

    [Header("White Balance")]
    [Range(-100f, 100f)] public float temperature = 5f;
    [Range(-100f, 100f)] public float whiteTint = 0f;

    [Header("Прочее (если включишь)")]
    [Range(0f, 1f)] public float grainIntensity = 0.08f;
    [Range(0f, 1f)] public float aberrationIntensity = 0.05f;
    [Range(0f, 1f)] public float motionBlurIntensity = 0.15f;
    [Range(0f, 1f)] public float paniniDistance = 0.15f;
    [Range(-1f, 1f)] public float lensDistortionAmount = -0.08f;

    private Volume volume;
    private VolumeProfile profile;

    private void OnEnable() => Build();
    private void OnDisable() { if (volume != null) volume.enabled = false; }

    [ContextMenu("Rebuild Post FX")]
    public void Build()
    {
        volume = GetComponent<Volume>();
        if (volume == null) volume = gameObject.AddComponent<Volume>();
        volume.enabled = true;
        volume.isGlobal = true;
        volume.priority = 100f;

        if (profile == null) profile = ScriptableObject.CreateInstance<VolumeProfile>();
        volume.sharedProfile = profile;
        volume.profile = profile;

        ClearProfile();

        if (tonemapping)
        {
            var t = profile.Add<Tonemapping>(false);
            t.mode.Override(tonemappingMode);
        }

        if (ambientOcclusion)
        {
            var ao = profile.Add<ScreenSpaceAmbientOcclusion>(false);
            ao.intensity.Override(aoIntensity);
            ao.radius.Override(aoRadius);
        }

        if (bloom)
        {
            var b = profile.Add<Bloom>(false);
            b.intensity.Override(bloomIntensity);
            b.threshold.Override(bloomThreshold);
            b.scatter.Override(bloomScatter);
        }

        if (vignette)
        {
            var v = profile.Add<Vignette>(false);
            v.intensity.Override(vignetteIntensity);
            v.smoothness.Override(vignetteSmoothness);
        }

        if (colorGrading)
        {
            var c = profile.Add<ColorAdjustments>(false);
            c.contrast.Override(contrast);
            c.saturation.Override(saturation);
            c.postExposure.Override(postExposure);
            c.colorFilter.Override(colorFilter);
        }

        if (whiteBalance)
        {
            var wb = profile.Add<WhiteBalance>(false);
            wb.temperature.Override(temperature);
            wb.tint.Override(whiteTint);
        }

        if (filmGrain)
        {
            var g = profile.Add<FilmGrain>(false);
            g.type.Override(FilmGrainLookup.Thin1);
            g.intensity.Override(grainIntensity);
            g.response.Override(0.8f);
        }

        if (chromaticAberration)
        {
            var ca = profile.Add<ChromaticAberration>(false);
            ca.intensity.Override(aberrationIntensity);
        }

        if (motionBlur)
        {
            var mb = profile.Add<MotionBlur>(false);
            mb.intensity.Override(motionBlurIntensity);
        }

        if (paniniProjection)
        {
            var pp = profile.Add<PaniniProjection>(false);
            pp.distance.Override(paniniDistance);
        }

        if (lensDistortion)
        {
            var ld = profile.Add<LensDistortion>(false);
            ld.intensity.Override(lensDistortionAmount);
        }

        if (depthOfField)
        {
            var dof = profile.Add<DepthOfField>(false);
            dof.focusMode.Override(DepthOfFieldMode.UsePhysicalCamera);
        }
    }

    private void ClearProfile()
    {
        if (profile == null) return;
        for (int i = profile.components.Count - 1; i >= 0; i--)
        {
            var comp = profile.components[i];
            if (comp != null) DestroyImmediate(comp, true);
        }
        profile.components.Clear();
    }
}
