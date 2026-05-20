using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.HighDefinition;

public class PistolWeapon : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private FirstPersonController controller;
    [SerializeField] private Transform muzzlePoint;

    [Header("Hipfire / ADS Pose")]
    [SerializeField] private Vector3 hipPosition = new Vector3(0.28f, -0.24f, 0.45f);
    [SerializeField] private Vector3 hipEuler = new Vector3(0f, -4f, 0f);
    [SerializeField] private Vector3 adsPosition = new Vector3(0f, -0.13f, 0.32f);
    [SerializeField] private Vector3 adsEuler = Vector3.zero;
    [SerializeField] private float poseSmoothing = 16f;
    [SerializeField] private float adsFovMultiplier = 0.7f;

    [Header("Firing")]
    [SerializeField] private float fireRate = 7f;
    [SerializeField] private float bulletRange = 100f;
    [SerializeField] private LayerMask hitMask = ~0;

    [Header("Spread")]
    [SerializeField] private float hipSpread = 1.4f;
    [SerializeField] private float adsSpread = 0.12f;
    [SerializeField] private float movementSpreadMultiplier = 1.8f;

    [Header("Recoil (actual aim shift)")]
    [SerializeField] private float recoilPitch = 1.6f;
    [SerializeField] private float recoilYawRandom = 0.45f;
    [SerializeField] private float recoilRecoveryDelay = 0.16f;
    [SerializeField] private float recoilRecoverySpeed = 9f;
    [SerializeField, Range(0f, 1f)] private float recoilRecoveryAmount = 0.7f;

    [Header("View Kick (transient shake)")]
    [SerializeField] private float viewKickPitch = 1.5f;
    [SerializeField] private float viewKickRecovery = 22f;

    [Header("Weapon Kick (visual)")]
    [SerializeField] private float kickBackDistance = 0.07f;
    [SerializeField] private float kickUpAngle = 7f;
    [SerializeField] private float kickRecovery = 14f;

    [Header("Sway / Bob")]
    [SerializeField] private float bobAmplitude = 0.009f;
    [SerializeField] private float bobFrequency = 8f;

    [Header("Ammo")]
    [SerializeField] private int magazineSize = 12;
    [SerializeField] private float reloadTime = 1.3f;
    [SerializeField] private bool autoReload = true;

    [Header("Muzzle Flash")]
    [SerializeField] private float muzzleFlashIntensity = 12000f;
    [SerializeField] private Color muzzleFlashColor = new Color(1f, 0.82f, 0.45f);
    [SerializeField] private float muzzleFlashDuration = 0.055f;

    [Header("Impact")]
    [SerializeField] private float impactLightIntensity = 800f;
    [SerializeField] private Color impactLightColor = new Color(1f, 0.7f, 0.3f);
    [SerializeField] private float impactDuration = 0.18f;

    [Header("Audio")]
    [SerializeField] private AudioClip fireClip;
    [SerializeField] private AudioClip emptyClip;
    [SerializeField] private AudioClip reloadClip;
    [SerializeField] private float fireVolume = 0.55f;

    private InputAction fireAction;
    private InputAction adsAction;
    private InputAction reloadAction;

    private CharacterController playerCC;
    private Camera cam;
    private float defaultFov;

    private float lastFireTime = -999f;
    private float accumulatedRecoilPitch;
    private float kickZ, kickX;
    private float viewKickPitchCurrent;
    private float viewKickYawCurrent;
    private float bobTimer;
    private bool isAds;
    private int currentAmmo;
    private bool reloading;

    private Light muzzleLight;
    private HDAdditionalLightData muzzleLightData;

    public int CurrentAmmo => currentAmmo;
    public int MagazineSize => magazineSize;
    public bool IsReloading => reloading;
    public bool IsAds => isAds;

    private void Awake()
    {
        if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
        if (controller == null && cameraTransform != null)
            controller = cameraTransform.GetComponentInParent<FirstPersonController>();
        if (controller != null) playerCC = controller.GetComponent<CharacterController>();
        if (cameraTransform != null) cam = cameraTransform.GetComponent<Camera>();
        if (cam != null) defaultFov = cam.fieldOfView;

        if (cameraTransform != null)
        {
            transform.SetParent(cameraTransform, false);
            transform.localPosition = hipPosition;
            transform.localRotation = Quaternion.Euler(hipEuler);
        }

        SetupMuzzle();

        currentAmmo = magazineSize;

        fireAction = new InputAction("Fire", InputActionType.Button, binding: "<Mouse>/leftButton");
        fireAction.AddBinding("<Gamepad>/rightTrigger");

        adsAction = new InputAction("ADS", InputActionType.Button, binding: "<Mouse>/rightButton");
        adsAction.AddBinding("<Gamepad>/leftTrigger");

        reloadAction = new InputAction("Reload", InputActionType.Button, binding: "<Keyboard>/r");
        reloadAction.AddBinding("<Gamepad>/buttonWest");
    }

    private void SetupMuzzle()
    {
        if (muzzlePoint == null)
        {
            var mp = new GameObject("MuzzlePoint");
            mp.transform.SetParent(transform, false);
            mp.transform.localPosition = new Vector3(0f, 0f, 0.6f);
            mp.transform.localRotation = Quaternion.identity;
            muzzlePoint = mp.transform;
        }

        var lightGO = new GameObject("MuzzleFlashLight");
        lightGO.transform.SetParent(muzzlePoint, false);
        muzzleLight = lightGO.AddComponent<Light>();
        muzzleLight.type = LightType.Point;
        muzzleLight.color = muzzleFlashColor;
        muzzleLight.range = 6f;
        muzzleLight.shadows = LightShadows.None;
        muzzleLight.useColorTemperature = false;

        muzzleLightData = lightGO.AddComponent<HDAdditionalLightData>();
        muzzleLightData.lightUnit = UnityEngine.Rendering.LightUnit.Lumen;
        muzzleLightData.intensity = 0f;
        muzzleLightData.range = 6f;
        muzzleLightData.color = muzzleFlashColor;

        muzzleLight.enabled = false;
    }

    private void OnEnable()
    {
        fireAction?.Enable();
        adsAction?.Enable();
        reloadAction?.Enable();
        if (fireAction != null) fireAction.performed += OnFirePressed;
        if (reloadAction != null) reloadAction.performed += OnReloadPressed;
    }

    private void OnDisable()
    {
        if (fireAction != null) fireAction.performed -= OnFirePressed;
        if (reloadAction != null) reloadAction.performed -= OnReloadPressed;
        fireAction?.Disable();
        adsAction?.Disable();
        reloadAction?.Disable();
    }

    private void OnDestroy()
    {
        fireAction?.Dispose();
        adsAction?.Dispose();
        reloadAction?.Dispose();
    }

    private bool IsLocked() => controller != null && controller.InputLocked;

    private void OnFirePressed(InputAction.CallbackContext _)
    {
        if (IsLocked() || reloading) return;
        TryFire();
    }

    private void OnReloadPressed(InputAction.CallbackContext _)
    {
        if (IsLocked() || reloading) return;
        if (currentAmmo < magazineSize) StartCoroutine(ReloadRoutine());
    }

    private void TryFire()
    {
        if (Time.time - lastFireTime < 1f / fireRate) return;

        if (currentAmmo <= 0)
        {
            if (emptyClip != null) AudioSource.PlayClipAtPoint(emptyClip, cameraTransform.position, fireVolume * 0.5f);
            if (autoReload) StartCoroutine(ReloadRoutine());
            return;
        }

        currentAmmo--;
        lastFireTime = Time.time;

        float spread = isAds ? adsSpread : hipSpread;
        if (playerCC != null)
        {
            float horizSpeed = new Vector3(playerCC.velocity.x, 0f, playerCC.velocity.z).magnitude;
            if (horizSpeed > 1f) spread *= movementSpreadMultiplier;
        }

        Vector3 dir = cameraTransform.forward;
        dir = Quaternion.Euler(
            Random.Range(-spread, spread),
            Random.Range(-spread, spread),
            0f) * dir;

        Vector3 origin = cameraTransform.position;
        if (Physics.Raycast(origin, dir, out RaycastHit hit, bulletRange, hitMask, QueryTriggerInteraction.Ignore))
            SpawnImpact(hit.point, hit.normal);

        float pitchKick = recoilPitch * (isAds ? 0.55f : 1f);
        float yawKick = Random.Range(-recoilYawRandom, recoilYawRandom) * (isAds ? 0.5f : 1f);

        if (controller != null)
        {
            controller.Pitch -= pitchKick;
            controller.Yaw += yawKick;
        }
        accumulatedRecoilPitch += pitchKick;

        viewKickPitchCurrent += viewKickPitch * (isAds ? 0.6f : 1f);
        viewKickYawCurrent += yawKick * 0.4f;

        kickZ = kickBackDistance;
        kickX = kickUpAngle;

        StopCoroutine(nameof(MuzzleFlash));
        StartCoroutine(MuzzleFlash());

        if (fireClip != null)
            AudioSource.PlayClipAtPoint(fireClip, cameraTransform.position, fireVolume);

        if (currentAmmo == 0 && autoReload) StartCoroutine(ReloadRoutine());
    }

    private IEnumerator MuzzleFlash()
    {
        muzzleLight.enabled = true;
        float t = 0f;
        while (t < muzzleFlashDuration)
        {
            float k = 1f - (t / muzzleFlashDuration);
            if (muzzleLightData != null)
                muzzleLightData.intensity = muzzleFlashIntensity * k * k;
            t += Time.deltaTime;
            yield return null;
        }
        if (muzzleLightData != null) muzzleLightData.intensity = 0f;
        muzzleLight.enabled = false;
    }

    private void SpawnImpact(Vector3 point, Vector3 normal)
    {
        var go = new GameObject("Impact");
        go.transform.position = point + normal * 0.04f;

        var light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = impactLightColor;
        light.range = 2.5f;
        light.shadows = LightShadows.None;
        light.useColorTemperature = false;

        var hd = go.AddComponent<HDAdditionalLightData>();
        hd.lightUnit = UnityEngine.Rendering.LightUnit.Lumen;
        hd.range = 2.5f;
        hd.color = impactLightColor;
        hd.intensity = impactLightIntensity;

        StartCoroutine(FadeImpact(go, hd, impactDuration));
    }

    private IEnumerator FadeImpact(GameObject go, HDAdditionalLightData hd, float duration)
    {
        float t = 0f;
        float start = hd.intensity;
        while (t < duration && go != null)
        {
            float k = 1f - (t / duration);
            if (hd != null) hd.intensity = start * k * k;
            t += Time.deltaTime;
            yield return null;
        }
        if (go != null) Destroy(go);
    }

    private IEnumerator ReloadRoutine()
    {
        if (reloading) yield break;
        reloading = true;
        if (reloadClip != null) AudioSource.PlayClipAtPoint(reloadClip, cameraTransform.position, fireVolume * 0.8f);
        yield return new WaitForSeconds(reloadTime);
        currentAmmo = magazineSize;
        reloading = false;
    }

    private void Update()
    {
        if (cameraTransform == null) return;

        isAds = !IsLocked() && !reloading && adsAction.IsPressed();

        Vector3 targetPos = isAds ? adsPosition : hipPosition;
        Vector3 targetEul = isAds ? adsEuler : hipEuler;

        if (playerCC != null && playerCC.isGrounded && !isAds)
        {
            float speed = new Vector3(playerCC.velocity.x, 0f, playerCC.velocity.z).magnitude;
            if (speed > 0.3f)
            {
                bobTimer += Time.deltaTime * bobFrequency;
                targetPos += new Vector3(
                    Mathf.Sin(bobTimer) * bobAmplitude * 0.5f,
                    Mathf.Cos(bobTimer * 2f) * bobAmplitude,
                    0f);
            }
            else bobTimer = Mathf.Lerp(bobTimer, 0f, Time.deltaTime * 5f);
        }

        targetPos += new Vector3(0f, 0f, -kickZ);
        Quaternion targetRot = Quaternion.Euler(targetEul) * Quaternion.Euler(-kickX, 0f, 0f);

        float k = 1f - Mathf.Exp(-poseSmoothing * Time.deltaTime);
        transform.localPosition = Vector3.Lerp(transform.localPosition, targetPos, k);
        transform.localRotation = Quaternion.Slerp(transform.localRotation, targetRot, k);

        kickZ = Mathf.Lerp(kickZ, 0f, kickRecovery * Time.deltaTime);
        kickX = Mathf.Lerp(kickX, 0f, kickRecovery * Time.deltaTime);

        if (cam != null && isAds)
        {
            float targetFov = defaultFov * adsFovMultiplier;
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFov, 12f * Time.deltaTime);
        }

        if (Time.time - lastFireTime > recoilRecoveryDelay && accumulatedRecoilPitch > 0.001f
            && controller != null && !IsLocked())
        {
            float recover = accumulatedRecoilPitch * recoilRecoverySpeed * Time.deltaTime;
            recover = Mathf.Min(recover, accumulatedRecoilPitch);
            accumulatedRecoilPitch -= recover;
            controller.Pitch += recover * recoilRecoveryAmount;
            if (accumulatedRecoilPitch < 0.001f) accumulatedRecoilPitch = 0f;
        }
    }

    private void LateUpdate()
    {
        if (cameraTransform == null) return;

        viewKickPitchCurrent = Mathf.Lerp(viewKickPitchCurrent, 0f, viewKickRecovery * Time.deltaTime);
        viewKickYawCurrent = Mathf.Lerp(viewKickYawCurrent, 0f, viewKickRecovery * Time.deltaTime);

        if (Mathf.Abs(viewKickPitchCurrent) > 0.001f || Mathf.Abs(viewKickYawCurrent) > 0.001f)
        {
            cameraTransform.localRotation = cameraTransform.localRotation *
                Quaternion.Euler(-viewKickPitchCurrent, viewKickYawCurrent, 0f);
        }
    }

    private GUIStyle ammoStyle;
    private GUIStyle ammoShadowStyle;
    private GUIStyle hintStyle;

    private void OnGUI()
    {
        int w = Screen.width, h = Screen.height;

        if (!isAds)
        {
            float s = 3f;
            GUI.color = new Color(0f, 0f, 0f, 0.7f);
            GUI.DrawTexture(new Rect(w * 0.5f - s - 1f, h * 0.5f - s - 1f, (s + 1f) * 2f, (s + 1f) * 2f), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(w * 0.5f - s, h * 0.5f - s, s * 2f, s * 2f), Texture2D.whiteTexture);
        }

        if (ammoStyle == null)
        {
            ammoStyle = new GUIStyle(GUI.skin.label) {
                fontSize = 44, fontStyle = FontStyle.Bold,
                alignment = TextAnchor.LowerRight
            };
            ammoShadowStyle = new GUIStyle(ammoStyle);
            hintStyle = new GUIStyle(GUI.skin.label) {
                fontSize = 18, alignment = TextAnchor.LowerRight,
            };
        }
        ammoStyle.normal.textColor = reloading ? new Color(1f, 0.6f, 0.3f) : Color.white;
        ammoShadowStyle.normal.textColor = new Color(0f, 0f, 0f, 0.7f);
        hintStyle.normal.textColor = new Color(1f, 1f, 1f, 0.55f);

        string ammoText = reloading ? "ПЕРЕЗАРЯДКА" : $"{currentAmmo} / {magazineSize}";
        var r = new Rect(0, 0, w - 40, h - 30);
        var rs = new Rect(2, 2, w - 40, h - 30);
        GUI.Label(rs, ammoText, ammoShadowStyle);
        GUI.Label(r, ammoText, ammoStyle);

        if (!reloading)
            GUI.Label(new Rect(0, 0, w - 40, h - 80), "[ПКМ] прицел   [R] перезарядка", hintStyle);
    }
}
