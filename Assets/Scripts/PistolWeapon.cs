using System.Collections;
using System.Collections.Generic;
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

    [Header("Holster (убрать в карман)")]
    [Tooltip("Смещение позиции при убранном пистолете (поверх Hip Position).")]
    [SerializeField] private Vector3 holsterOffset = new Vector3(0f, -0.45f, -0.18f);
    [Tooltip("Доворот при убранном пистолете (поверх Hip Euler).")]
    [SerializeField] private Vector3 holsterRotationOffset = new Vector3(72f, 0f, 0f);
    [Tooltip("Скорость доставания/убирания.")]
    [SerializeField] private float holsterSmoothing = 12f;
    [Tooltip("Убран ли пистолет на старте.")]
    [SerializeField] private bool startHolstered = false;

    [Header("Firing")]
    [SerializeField] private float fireRate = 7f;
    [SerializeField] private float bulletRange = 100f;
    [SerializeField] private LayerMask hitMask = ~0;
    [Tooltip("Сила толчка, которую пуля придаёт физ-объектам (импульс).")]
    [SerializeField] private float bulletImpactForce = 6f;

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

    [Header("Look Sway (отставание при повороте камеры)")]
    [Tooltip("Насколько ствол смещается вбок/вверх при повороте камеры.")]
    [SerializeField] private float swayPositionAmount = 0.0016f;
    [SerializeField] private float swayPositionMax = 0.03f;
    [Tooltip("Насколько ствол доворачивается при повороте камеры (градусы).")]
    [SerializeField] private float swayRotationAmount = 0.5f;
    [SerializeField] private float swayRotationMax = 5f;
    [Tooltip("Скорость возврата к центру. Меньше = ленивее, больше = жёстче.")]
    [SerializeField] private float swaySmoothing = 9f;
    [Tooltip("Во сколько раз слабее sway при прицеливании.")]
    [SerializeField, Range(0f, 1f)] private float adsSwayMultiplier = 0.25f;
    [Tooltip("Доворот ствола вбок при стрейфе (А/D).")]
    [SerializeField] private float strafeSwayRoll = 2.2f;

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

    [Header("Bullet Holes")]
    [SerializeField] private bool spawnBulletHoles = true;
    [SerializeField] private float bulletHoleSize = 0.05f;
    [SerializeField] private Color bulletHoleColor = Color.black;
    [Tooltip("Максимум дырок одновременно. Старые удаляются.")]
    [SerializeField] private int maxBulletHoles = 80;

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

    private Vector3 lastCamEuler;
    private bool hasCamEuler;
    private Vector3 swayPos;
    private Vector3 swayRot;
    private bool isHolstered;
    private InputAction holsterAction;

    private Light muzzleLight;
    private HDAdditionalLightData muzzleLightData;

    public int CurrentAmmo => currentAmmo;
    public int MagazineSize => magazineSize;
    public bool IsReloading => reloading;
    public bool IsAds => isAds;
    public static bool IsAiming { get; private set; }
    public static bool IsPistolDrawn { get; private set; }

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

        holsterAction = new InputAction("Holster", InputActionType.Button, binding: "<Keyboard>/h");
        holsterAction.AddBinding("<Gamepad>/dpad/down");

        isHolstered = startHolstered;
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
        holsterAction?.Enable();
        if (fireAction != null) fireAction.performed += OnFirePressed;
        if (reloadAction != null) reloadAction.performed += OnReloadPressed;
        if (holsterAction != null) holsterAction.performed += OnHolsterPressed;
    }

    private void OnDisable()
    {
        if (fireAction != null) fireAction.performed -= OnFirePressed;
        if (reloadAction != null) reloadAction.performed -= OnReloadPressed;
        if (holsterAction != null) holsterAction.performed -= OnHolsterPressed;
        fireAction?.Disable();
        adsAction?.Disable();
        reloadAction?.Disable();
        holsterAction?.Disable();
        IsPistolDrawn = false;
        IsAiming = false;
    }

    private void OnDestroy()
    {
        fireAction?.Dispose();
        adsAction?.Dispose();
        reloadAction?.Dispose();
        holsterAction?.Dispose();
    }

    private bool IsLocked() => controller != null && controller.InputLocked;

    public bool IsHolstered => isHolstered;
    public void SetHolstered(bool value) => isHolstered = value;
    public void ToggleHolster() => isHolstered = !isHolstered;

    private void OnHolsterPressed(InputAction.CallbackContext _)
    {
        if (IsLocked()) return;
        isHolstered = !isHolstered;
    }

    private void OnFirePressed(InputAction.CallbackContext _)
    {
        if (IsLocked() || reloading || isHolstered) return;
        if (PhysicsGrabber.IsAnyHeld) return;
        TryFire();
    }

    private void OnReloadPressed(InputAction.CallbackContext _)
    {
        if (IsLocked() || reloading || isHolstered) return;
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
        {
            SpawnImpact(hit.point, hit.normal);
            SpawnBulletHole(hit);

            if (hit.rigidbody != null && !hit.rigidbody.isKinematic)
                hit.rigidbody.AddForceAtPosition(dir * bulletImpactForce, hit.point, ForceMode.Impulse);
        }

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

    private static Material sharedBulletHoleMat;
    private static readonly Queue<GameObject> bulletHoles = new Queue<GameObject>();

    private void SpawnBulletHole(RaycastHit hit)
    {
        if (!spawnBulletHoles) return;

        if (sharedBulletHoleMat == null)
        {
            Shader sh = Shader.Find("HDRP/Unlit");
            if (sh == null) sh = Shader.Find("Universal Render Pipeline/Unlit");
            if (sh == null) sh = Shader.Find("Unlit/Color");
            sharedBulletHoleMat = new Material(sh);
            if (sharedBulletHoleMat.HasProperty("_UnlitColor")) sharedBulletHoleMat.SetColor("_UnlitColor", bulletHoleColor);
            if (sharedBulletHoleMat.HasProperty("_BaseColor")) sharedBulletHoleMat.SetColor("_BaseColor", bulletHoleColor);
            if (sharedBulletHoleMat.HasProperty("_Color")) sharedBulletHoleMat.SetColor("_Color", bulletHoleColor);
        }

        var hole = GameObject.CreatePrimitive(PrimitiveType.Quad);
        hole.name = "BulletHole";
        var col = hole.GetComponent<Collider>();
        if (col != null) Destroy(col);

        hole.transform.position = hit.point + hit.normal * 0.012f;
        hole.transform.rotation = Quaternion.LookRotation(-hit.normal);

        var rend = hole.GetComponent<Renderer>();
        if (rend != null)
        {
            rend.sharedMaterial = sharedBulletHoleMat;
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rend.receiveShadows = false;
        }

        if (hit.collider != null)
        {
            hole.transform.SetParent(hit.collider.transform, true);
            Vector3 ls = hit.collider.transform.lossyScale;
            hole.transform.localScale = new Vector3(
                bulletHoleSize / Mathf.Max(0.0001f, ls.x),
                bulletHoleSize / Mathf.Max(0.0001f, ls.y),
                bulletHoleSize / Mathf.Max(0.0001f, ls.z));
        }
        else
        {
            hole.transform.localScale = Vector3.one * bulletHoleSize;
        }

        bulletHoles.Enqueue(hole);
        while (bulletHoles.Count > maxBulletHoles)
        {
            var old = bulletHoles.Dequeue();
            if (old != null) Destroy(old);
        }
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

    private void ComputeSway()
    {
        Vector3 camEuler = cameraTransform.eulerAngles;
        if (!hasCamEuler) { lastCamEuler = camEuler; hasCamEuler = true; }

        float dYaw = Mathf.DeltaAngle(lastCamEuler.y, camEuler.y);
        float dPitch = Mathf.DeltaAngle(lastCamEuler.x, camEuler.x);
        lastCamEuler = camEuler;

        float dt = Mathf.Max(0.0001f, Time.deltaTime);
        float yawV = dYaw / dt;
        float pitchV = dPitch / dt;

        float mult = isAds ? adsSwayMultiplier : 1f;

        float strafe = 0f;
        if (playerCC != null)
        {
            Vector3 localVel = cameraTransform.InverseTransformDirection(playerCC.velocity);
            strafe = localVel.x;
        }

        Vector3 targetPos = new Vector3(
            Mathf.Clamp(-yawV * swayPositionAmount, -swayPositionMax, swayPositionMax),
            Mathf.Clamp(-pitchV * swayPositionAmount, -swayPositionMax, swayPositionMax),
            0f) * mult;

        Vector3 targetRot = new Vector3(
            Mathf.Clamp(pitchV * swayRotationAmount, -swayRotationMax, swayRotationMax),
            Mathf.Clamp(yawV * swayRotationAmount, -swayRotationMax, swayRotationMax),
            Mathf.Clamp(-yawV * swayRotationAmount, -swayRotationMax, swayRotationMax) - strafe * strafeSwayRoll
        ) * mult;

        float s = 1f - Mathf.Exp(-swaySmoothing * Time.deltaTime);
        swayPos = Vector3.Lerp(swayPos, targetPos, s);
        swayRot = Vector3.Lerp(swayRot, targetRot, s);
    }

    private void Update()
    {
        if (cameraTransform == null) return;

        isAds = !IsLocked() && !reloading && !isHolstered && adsAction.IsPressed();
        IsAiming = isAds;
        IsPistolDrawn = !isHolstered;

        Vector3 targetPos;
        Quaternion targetRot;
        float k;

        if (isHolstered)
        {
            targetPos = hipPosition + holsterOffset;
            targetRot = Quaternion.Euler(hipEuler + holsterRotationOffset);
            k = 1f - Mathf.Exp(-holsterSmoothing * Time.deltaTime);
        }
        else
        {
            targetPos = isAds ? adsPosition : hipPosition;
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

            ComputeSway();

            targetPos += new Vector3(0f, 0f, -kickZ) + swayPos;
            targetRot = Quaternion.Euler(targetEul)
                        * Quaternion.Euler(-kickX, 0f, 0f)
                        * Quaternion.Euler(swayRot);
            k = 1f - Mathf.Exp(-poseSmoothing * Time.deltaTime);
        }

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

    [Header("Crosshair (прицел пистолета)")]
    [SerializeField] private float crosshairSize = 16f;
    [SerializeField] private float crosshairThickness = 2f;
    [Tooltip("Сколько штрихов в кольце. 0 = сплошное кольцо.")]
    [SerializeField] private int crosshairDashes = 8;
    [Tooltip("Доля промежутка между штрихами (0..1).")]
    [SerializeField, Range(0f, 0.9f)] private float crosshairGapRatio = 0.5f;
    private Texture2D crosshairTex;

    private Texture2D GetCrosshairTexture()
    {
        if (crosshairTex != null) return crosshairTex;

        int size = 128;
        crosshairTex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        crosshairTex.filterMode = FilterMode.Bilinear;
        crosshairTex.wrapMode = TextureWrapMode.Clamp;

        float center = (size - 1) * 0.5f;
        float outerR = size * 0.46f;
        float ringPx = Mathf.Clamp(crosshairThickness, 1f, 20f) / Mathf.Max(1f, crosshairSize) * size;
        float innerR = outerR - ringPx;
        float aa = 1.5f;
        float twoPi = Mathf.PI * 2f;

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = x - center, dy = y - center;
            float dist = Mathf.Sqrt(dx * dx + dy * dy);
            float outerEdge = Mathf.Clamp01((outerR - dist) / aa);
            float innerEdge = Mathf.Clamp01((dist - innerR) / aa);
            float alpha = Mathf.Clamp01(Mathf.Min(outerEdge, innerEdge));

            if (crosshairDashes > 0 && alpha > 0f)
            {
                float ang = Mathf.Atan2(dy, dx) + Mathf.PI;
                float seg = ang / twoPi * crosshairDashes;
                float frac = seg - Mathf.Floor(seg);
                float dashPart = 1f - crosshairGapRatio;
                float edge = 0.06f;
                float dashAlpha = Mathf.Clamp01(Mathf.Min(frac, dashPart - frac) / edge);
                alpha *= dashAlpha;
            }

            crosshairTex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
        }
        crosshairTex.Apply();
        return crosshairTex;
    }

    private void OnGUI()
    {
        int w = Screen.width, h = Screen.height;

        if (!isAds && !isHolstered)
        {
            var tex = GetCrosshairTexture();
            float sz = crosshairSize;
            float cx = w * 0.5f, cy = h * 0.5f;

            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(new Rect(cx - sz * 0.5f + 1f, cy - sz * 0.5f + 1f, sz, sz), tex);

            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(cx - sz * 0.5f, cy - sz * 0.5f, sz, sz), tex);
            GUI.color = Color.white;
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
        ammoShadowStyle.normal.textColor = new Color(0f, 0f, 0f, 0.7f);
        hintStyle.normal.textColor = new Color(1f, 1f, 1f, 0.55f);

        if (isHolstered)
        {
            ammoStyle.normal.textColor = new Color(1f, 1f, 1f, 0.5f);
            var rh = new Rect(0, 0, w - 40, h - 30);
            GUI.Label(new Rect(2, 2, w - 40, h - 30), "убран", ammoShadowStyle);
            GUI.Label(rh, "убран", ammoStyle);
            GUI.Label(new Rect(0, 0, w - 40, h - 80), "[H] достать пистолет", hintStyle);
            return;
        }

        ammoStyle.normal.textColor = reloading ? new Color(1f, 0.6f, 0.3f) : Color.white;

        string ammoText = reloading ? "ПЕРЕЗАРЯДКА" : $"{currentAmmo} / {magazineSize}";
        var r = new Rect(0, 0, w - 40, h - 30);
        var rs = new Rect(2, 2, w - 40, h - 30);
        GUI.Label(rs, ammoText, ammoShadowStyle);
        GUI.Label(r, ammoText, ammoStyle);

        if (!reloading)
            GUI.Label(new Rect(0, 0, w - 40, h - 80), "[ПКМ] прицел   [R] перезарядка   [H] убрать", hintStyle);
    }
}
