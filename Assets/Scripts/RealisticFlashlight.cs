using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.HighDefinition;

public class RealisticFlashlight : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform followTarget;

    [Header("Light")]
    [SerializeField] private float intensity = 8000f;
    [SerializeField] private float range = 30f;
    [SerializeField] private float outerAngle = 48f;
    [SerializeField] private float innerAngle = 22f;
    [SerializeField] private Color color = new Color(1f, 0.96f, 0.86f);
    [SerializeField] private LightShadows shadows = LightShadows.Soft;

    [Header("Hand-Held Feel")]
    [SerializeField] private Vector3 localOffset = new Vector3(0.18f, -0.08f, 0.15f);
    [SerializeField] private float positionFollowSpeed = 16f;
    [SerializeField] private float rotationFollowSpeed = 11f;
    [SerializeField] private float aimDriftAmount = 0.4f;
    [SerializeField] private float aimDriftFrequency = 0.7f;

    [Header("Power")]
    [SerializeField] private float turnOnSpeed = 22f;
    [SerializeField] private float turnOffSpeed = 14f;
    [SerializeField] private float startupOvershoot = 1.35f;
    [SerializeField] private float startupDuration = 0.13f;

    [Header("Flicker")]
    [SerializeField] private float flickerStrength = 0.05f;
    [SerializeField] private float flickerSpeed = 14f;
    [SerializeField] private float lowBatteryFlicker = 0f;

    private Light spotLight;
    private HDAdditionalLightData lightData;
    private InputAction toggleAction;

    private bool isOn;
    private float currentIntensity;
    private float startupTimer;
    private Quaternion smoothedRotation;
    private Vector3 smoothedPosition;
    private float driftSeed;

    private void Awake()
    {
        if (followTarget == null && Camera.main != null)
            followTarget = Camera.main.transform;

        CreateLight();

        toggleAction = new InputAction("Flashlight", InputActionType.Button, binding: "<Keyboard>/f");
        toggleAction.AddBinding("<Gamepad>/dpad/up");

        driftSeed = Random.value * 100f;
    }

    private void CreateLight()
    {
        var go = new GameObject("FlashlightBeam");
        spotLight = go.AddComponent<Light>();
        spotLight.type = LightType.Spot;
        spotLight.color = color;
        spotLight.range = range;
        spotLight.spotAngle = outerAngle;
        spotLight.innerSpotAngle = innerAngle;
        spotLight.shadows = shadows;
        spotLight.useColorTemperature = false;

        lightData = go.AddComponent<HDAdditionalLightData>();
        lightData.lightUnit = UnityEngine.Rendering.LightUnit.Lumen;
        lightData.intensity = 0f;
        lightData.range = range;
        lightData.color = color;
        lightData.SetSpotAngle(outerAngle, Mathf.Clamp01(innerAngle / Mathf.Max(0.01f, outerAngle)));
        lightData.EnableShadows(shadows != LightShadows.None);
        lightData.affectDiffuse = true;
        lightData.affectSpecular = true;

        if (followTarget != null)
        {
            go.transform.position = followTarget.TransformPoint(localOffset);
            go.transform.rotation = followTarget.rotation;
            smoothedPosition = go.transform.position;
            smoothedRotation = go.transform.rotation;
        }
    }

    private void OnEnable()
    {
        toggleAction?.Enable();
        if (toggleAction != null) toggleAction.performed += OnToggle;
    }

    private void OnDisable()
    {
        if (toggleAction != null) toggleAction.performed -= OnToggle;
        toggleAction?.Disable();
    }

    private void OnDestroy()
    {
        if (spotLight != null) Destroy(spotLight.gameObject);
        toggleAction?.Dispose();
    }

    private void OnToggle(InputAction.CallbackContext _)
    {
        isOn = !isOn;
        if (isOn) startupTimer = startupDuration;
    }

    private void LateUpdate()
    {
        if (spotLight == null || followTarget == null) return;

        float posLerp = 1f - Mathf.Exp(-positionFollowSpeed * Time.deltaTime);
        float rotLerp = 1f - Mathf.Exp(-rotationFollowSpeed * Time.deltaTime);

        Vector3 targetPos = followTarget.TransformPoint(localOffset);
        smoothedPosition = Vector3.Lerp(smoothedPosition, targetPos, posLerp);
        spotLight.transform.position = smoothedPosition;

        float t = (Time.time + driftSeed) * aimDriftFrequency;
        Vector3 drift = new Vector3(
            (Mathf.PerlinNoise(t, 0f) - 0.5f) * aimDriftAmount,
            (Mathf.PerlinNoise(0f, t) - 0.5f) * aimDriftAmount,
            0f);

        Quaternion targetRot = followTarget.rotation * Quaternion.Euler(drift);
        smoothedRotation = Quaternion.Slerp(smoothedRotation, targetRot, rotLerp);
        spotLight.transform.rotation = smoothedRotation;

        float target = isOn ? intensity : 0f;
        if (startupTimer > 0f && isOn)
        {
            float k = startupTimer / startupDuration;
            target *= 1f + (startupOvershoot - 1f) * k;
            startupTimer -= Time.deltaTime;
        }

        float speed = isOn ? turnOnSpeed : turnOffSpeed;
        currentIntensity = Mathf.Lerp(currentIntensity, target, 1f - Mathf.Exp(-speed * Time.deltaTime));

        float flicker = 1f;
        if (isOn && currentIntensity > 1f)
        {
            float noise = Mathf.PerlinNoise(Time.time * flickerSpeed, driftSeed);
            flicker -= (noise - 0.5f) * flickerStrength * 2f;
            if (lowBatteryFlicker > 0f)
            {
                float lb = Mathf.PerlinNoise(Time.time * flickerSpeed * 3f, driftSeed + 7f);
                if (lb > 0.85f) flicker *= Mathf.Lerp(1f, 0.15f, lowBatteryFlicker);
            }
        }

        if (lightData != null)
            lightData.intensity = Mathf.Max(0f, currentIntensity * flicker);

        spotLight.enabled = currentIntensity > 0.5f;
    }
}
