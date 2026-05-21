using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class FirstPersonController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform cameraTransform;

    [Header("Movement")]
    [SerializeField] private float walkSpeed = 4.5f;
    [SerializeField] private float sprintSpeed = 7.5f;
    [SerializeField] private float crouchSpeed = 2.2f;
    [SerializeField] private float groundAccel = 18f;
    [SerializeField] private float groundDecel = 14f;
    [SerializeField] private float airControl = 0.35f;
    [SerializeField] private float sprintRampTime = 0.25f;

    [Header("Jump & Gravity")]
    [SerializeField] private float jumpHeight = 1.5f;
    [SerializeField] private float gravity = -22f;
    [SerializeField] private float fallMultiplier = 1.6f;
    [SerializeField] private float lowJumpMultiplier = 2.2f;
    [SerializeField] private float groundedStickForce = -2.5f;
    [SerializeField] private float coyoteTime = 0.12f;
    [SerializeField] private float jumpBuffer = 0.15f;

    [Header("Look")]
    [SerializeField] private float mouseSensitivity = 0.11f;
    [SerializeField] private float gamepadSensitivity = 140f;
    [SerializeField] private float lookSmoothing = 0.04f;
    [SerializeField] private float minPitch = -85f;
    [SerializeField] private float maxPitch = 85f;

    [Header("Crouch")]
    [SerializeField] private float standHeight = 1.8f;
    [SerializeField] private float crouchHeight = 1.05f;
    [SerializeField] private float crouchTransition = 12f;
    [SerializeField] private LayerMask ceilingMask = ~0;
    [Tooltip("На сколько метров опускается камера при приседе.")]
    [SerializeField] private float crouchCameraDip = 0.7f;
    [SerializeField] private float crouchCameraSmoothing = 9f;

    [Header("Slide (подкат на C во время бега)")]
    [Tooltip("Начальная скорость подката.")]
    [SerializeField] private float slideSpeed = 9f;
    [Tooltip("Максимальная длительность подката (сек).")]
    [SerializeField] private float slideDuration = 0.9f;
    [Tooltip("Замедление подката со временем.")]
    [SerializeField] private float slideFriction = 5f;
    [Tooltip("Минимальная скорость, чтобы подкат начался.")]
    [SerializeField] private float slideMinSpeed = 4f;
    [Tooltip("Пауза между подкатами (сек).")]
    [SerializeField] private float slideCooldown = 0.5f;
    [Tooltip("Насколько можно подруливать в подкате.")]
    [SerializeField] private float slideSteer = 2.5f;
    [Tooltip("Наклон камеры в подкате (градусы).")]
    [SerializeField] private float slideCameraTilt = 7f;
    [Tooltip("Доп. опускание камеры в подкате.")]
    [SerializeField] private float slideCameraDrop = 0.15f;

    [Header("Start")]
    [Tooltip("Прижать игрока к земле при старте (убирает падение капсулы в начале).")]
    [SerializeField] private bool snapToGroundOnStart = true;
    [SerializeField] private float snapMaxDistance = 6f;
    [SerializeField] private LayerMask groundMask = ~0;

    [Header("Push Physics (толкать предметы телом)")]
    [SerializeField] private bool pushRigidbodies = true;
    [Tooltip("Сила толчка предметов телом игрока.")]
    [SerializeField] private float pushForce = 2.2f;
    [Tooltip("Какие предметы можно толкать.")]
    [SerializeField] private LayerMask pushMask = ~0;

    [Header("Head Bob")]
    [SerializeField] private float walkBobFrequency = 8.5f;
    [SerializeField] private float sprintBobFrequency = 12f;
    [SerializeField] private float crouchBobFrequency = 5.5f;
    [SerializeField] private float walkBobAmplitude = 0.045f;
    [SerializeField] private float sprintBobAmplitude = 0.075f;
    [SerializeField] private float crouchBobAmplitude = 0.025f;
    [SerializeField] private float bobSmoothing = 12f;
    [SerializeField] private float bobLateralRatio = 0.55f;

    [Header("Idle Sway")]
    [SerializeField] private float idleSwayAmplitude = 0.008f;
    [SerializeField] private float idleSwayFrequency = 1.2f;

    [Header("Mouse Sway")]
    [SerializeField] private float swayAmount = 0.015f;
    [SerializeField] private float swayMaxOffset = 0.04f;
    [SerializeField] private float swaySmoothing = 9f;

    [Header("Camera Feel")]
    [SerializeField] private float strafeTiltAngle = 1.8f;
    [SerializeField] private float tiltSmoothing = 9f;
    [SerializeField] private float defaultFov = 60f;
    [SerializeField] private float sprintFov = 68f;
    [SerializeField] private float fovSmoothing = 7f;
    [SerializeField] private float minLandingDip = 0.04f;
    [SerializeField] private float maxLandingDip = 0.18f;
    [SerializeField] private float landingRecovery = 7f;
    [SerializeField] private float landingFallReference = 6f;

    public bool InputLocked { get; set; }
    public float Yaw { get => yaw; set => yaw = value; }
    public float Pitch { get => pitch; set => pitch = Mathf.Clamp(value, minPitch, maxPitch); }

    private CharacterController controller;
    private Camera cam;

    private InputAction moveAction;
    private InputAction lookAction;
    private InputAction jumpAction;
    private InputAction sprintAction;
    private InputAction crouchAction;

    private Vector3 horizontalVelocity;
    private float verticalVelocity;
    private float pitch;
    private float yaw;
    private Vector2 smoothedLookDelta;
    private Vector2 lookVelocity;
    private bool isCrouching;
    private bool wantsCrouch;
    private bool wasGrounded;
    private float lastGroundedTime = -999f;
    private float lastJumpPressedTime = -999f;
    private float jumpStartTime;
    private float peakFallSpeed;
    private float sprintBlend;

    private Vector3 cameraBasePos;
    private Vector3 bobOffset;
    private Vector3 swayOffset;
    private float bobTimer;
    private float idleTimer;
    private float currentTilt;
    private float landingOffset;
    private float crouchBlend;
    private float feetOffsetY;

    private bool isSliding;
    private float slideTimer;
    private float slideCooldownTimer;
    private Vector3 slideDir;
    private float currentSlideSpeed;
    private float slideTiltCurrent;
    private float slideDropCurrent;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        // Запоминаем, где «ноги» (низ коллайдера) у настроенного контроллера,
        // чтобы не сдвигать капсулу при старте независимо от origin меша.
        feetOffsetY = controller.center.y - controller.height * 0.5f;
        controller.height = standHeight;
        controller.center = new Vector3(controller.center.x, feetOffsetY + standHeight * 0.5f, controller.center.z);

        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

        if (cameraTransform != null)
        {
            cameraBasePos = cameraTransform.localPosition;
            cam = cameraTransform.GetComponent<Camera>();
            if (cam != null) cam.fieldOfView = defaultFov;
        }

        yaw = transform.eulerAngles.y;

        CreateInputActions();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Start()
    {
        if (snapToGroundOnStart) SnapToGround();
    }

    private void SnapToGround()
    {
        Vector3 origin = transform.position + Vector3.up * 1f;
        bool wasEnabled = controller.enabled;
        controller.enabled = false;
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit,
                            snapMaxDistance + 1f, groundMask, QueryTriggerInteraction.Ignore))
        {
            // низ коллайдера в мире = transform.position.y + feetOffsetY,
            // ставим его ровно на землю
            float targetY = hit.point.y - feetOffsetY + controller.skinWidth;
            transform.position = new Vector3(transform.position.x, targetY, transform.position.z);
        }
        controller.enabled = wasEnabled;
        verticalVelocity = 0f;
    }

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (!pushRigidbodies) return;

        Rigidbody body = hit.collider.attachedRigidbody;
        if (body == null || body.isKinematic) return;
        if ((pushMask.value & (1 << hit.collider.gameObject.layer)) == 0) return;

        // не толкаем то, на чём стоим
        if (hit.moveDirection.y < -0.3f) return;

        Vector3 pushDir = new Vector3(hit.moveDirection.x, 0f, hit.moveDirection.z);
        if (pushDir.sqrMagnitude < 0.001f) return;
        pushDir.Normalize();

        float playerSpeed = new Vector3(horizontalVelocity.x, 0f, horizontalVelocity.z).magnitude;
        if (playerSpeed < 0.1f) return;

        body.AddForceAtPosition(pushDir * pushForce * playerSpeed, hit.point, ForceMode.Impulse);
    }

    private void CreateInputActions()
    {
        moveAction = new InputAction("Move", InputActionType.Value, expectedControlType: "Vector2");
        moveAction.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/w")
            .With("Down", "<Keyboard>/s")
            .With("Left", "<Keyboard>/a")
            .With("Right", "<Keyboard>/d");
        moveAction.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/upArrow")
            .With("Down", "<Keyboard>/downArrow")
            .With("Left", "<Keyboard>/leftArrow")
            .With("Right", "<Keyboard>/rightArrow");
        moveAction.AddBinding("<Gamepad>/leftStick");

        lookAction = new InputAction("Look", InputActionType.Value, expectedControlType: "Vector2");
        lookAction.AddBinding("<Mouse>/delta");
        lookAction.AddBinding("<Gamepad>/rightStick");

        jumpAction = new InputAction("Jump", InputActionType.Button, binding: "<Keyboard>/space");
        jumpAction.AddBinding("<Gamepad>/buttonSouth");

        sprintAction = new InputAction("Sprint", InputActionType.Button, binding: "<Keyboard>/leftShift");
        sprintAction.AddBinding("<Gamepad>/leftStickPress");

        crouchAction = new InputAction("Crouch", InputActionType.Button, binding: "<Keyboard>/c");
        crouchAction.AddBinding("<Gamepad>/buttonEast");
    }

    private void OnEnable()
    {
        moveAction?.Enable();
        lookAction?.Enable();
        jumpAction?.Enable();
        sprintAction?.Enable();
        crouchAction?.Enable();
        jumpAction.performed += OnJumpPressed;
    }

    private void OnDisable()
    {
        if (jumpAction != null) jumpAction.performed -= OnJumpPressed;
        moveAction?.Disable();
        lookAction?.Disable();
        jumpAction?.Disable();
        sprintAction?.Disable();
        crouchAction?.Disable();
    }

    private void OnJumpPressed(InputAction.CallbackContext ctx)
    {
        lastJumpPressedTime = Time.time;
    }

    private void Update()
    {
        HandleLook();
        HandleCrouch();
        HandleMove();
        HandleCameraEffects();
    }

    private void HandleLook()
    {
        Vector2 raw = InputLocked ? Vector2.zero : lookAction.ReadValue<Vector2>();
        bool isGamepad = !InputLocked && lookAction.activeControl != null &&
                         lookAction.activeControl.device is Gamepad;
        float scale = isGamepad ? gamepadSensitivity * Time.deltaTime : mouseSensitivity;
        Vector2 target = raw * scale;

        smoothedLookDelta = Vector2.SmoothDamp(smoothedLookDelta, target, ref lookVelocity,
                                               lookSmoothing, Mathf.Infinity, Time.deltaTime);

        if (!InputLocked)
        {
            yaw += smoothedLookDelta.x;
            pitch = Mathf.Clamp(pitch - smoothedLookDelta.y, minPitch, maxPitch);
        }
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
    }

    private void HandleCrouch()
    {
        wantsCrouch = isSliding || (!InputLocked && crouchAction.IsPressed());
        if (!wantsCrouch && isCrouching && HasCeilingAbove()) wantsCrouch = true;
        isCrouching = wantsCrouch;

        float target = isCrouching ? crouchHeight : standHeight;
        controller.height = Mathf.Lerp(controller.height, target, crouchTransition * Time.deltaTime);
        // ноги остаются на месте: низ = feetOffsetY
        controller.center = new Vector3(controller.center.x, feetOffsetY + controller.height * 0.5f, controller.center.z);
    }

    private bool HasCeilingAbove()
    {
        float rayLength = standHeight - controller.height + 0.1f;
        // макушка коллайдера в мире
        Vector3 origin = transform.position + Vector3.up * (feetOffsetY + controller.height);
        return Physics.SphereCast(origin, controller.radius * 0.9f, Vector3.up,
                                  out _, rayLength, ceilingMask, QueryTriggerInteraction.Ignore);
    }

    private void HandleMove()
    {
        Vector2 input = InputLocked ? Vector2.zero : moveAction.ReadValue<Vector2>();
        Vector3 wishDir = transform.right * input.x + transform.forward * input.y;
        if (wishDir.sqrMagnitude > 1f) wishDir.Normalize();

        bool grounded = controller.isGrounded;

        if (slideCooldownTimer > 0f) slideCooldownTimer -= Time.deltaTime;

        // --- Старт подката: бежишь + жмёшь присесть ---
        float horizSpeed = new Vector3(horizontalVelocity.x, 0f, horizontalVelocity.z).magnitude;
        if (!isSliding && grounded && !InputLocked && slideCooldownTimer <= 0f
            && crouchAction.WasPressedThisFrame()
            && sprintAction.IsPressed() && horizSpeed >= slideMinSpeed)
        {
            StartSlide();
        }

        bool wantsSprint = !InputLocked && sprintAction.IsPressed() && !isCrouching && !isSliding && input.y > 0.1f;
        float sprintTarget = wantsSprint ? 1f : 0f;
        sprintBlend = Mathf.MoveTowards(sprintBlend, sprintTarget, Time.deltaTime / Mathf.Max(0.01f, sprintRampTime));

        if (isSliding)
        {
            UpdateSlide(input, grounded);
        }
        else
        {
            float baseSpeed = isCrouching ? crouchSpeed : Mathf.Lerp(walkSpeed, sprintSpeed, sprintBlend);
            Vector3 targetVel = wishDir * baseSpeed;

            float accel = (targetVel.sqrMagnitude > 0.01f) ? groundAccel : groundDecel;
            if (!grounded) accel *= airControl;
            horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, targetVel, accel * Time.deltaTime);
        }

        if (grounded)
        {
            lastGroundedTime = Time.time;
            if (!wasGrounded)
            {
                float impact = Mathf.InverseLerp(0f, landingFallReference, peakFallSpeed);
                landingOffset = -Mathf.Lerp(minLandingDip, maxLandingDip, impact);
                peakFallSpeed = 0f;
            }
            if (verticalVelocity < 0f) verticalVelocity = groundedStickForce;
        }

        bool canJump = (Time.time - lastGroundedTime) <= coyoteTime;
        bool jumpBuffered = (Time.time - lastJumpPressedTime) <= jumpBuffer;
        if (canJump && jumpBuffered && !InputLocked && (!isCrouching || isSliding))
        {
            if (isSliding) EndSlide();
            verticalVelocity = Mathf.Sqrt(-2f * gravity * jumpHeight);
            lastJumpPressedTime = -999f;
            lastGroundedTime = -999f;
            jumpStartTime = Time.time;
        }

        if (!grounded)
        {
            float g = gravity;
            if (verticalVelocity < 0f) g *= fallMultiplier;
            else if (verticalVelocity > 0f && !jumpAction.IsPressed()) g *= lowJumpMultiplier;
            verticalVelocity += g * Time.deltaTime;
            if (verticalVelocity < 0f) peakFallSpeed = Mathf.Max(peakFallSpeed, -verticalVelocity);
        }

        wasGrounded = grounded;

        Vector3 motion = horizontalVelocity;
        motion.y = verticalVelocity;
        controller.Move(motion * Time.deltaTime);
    }

    private void StartSlide()
    {
        isSliding = true;
        slideTimer = slideDuration;
        slideDir = new Vector3(horizontalVelocity.x, 0f, horizontalVelocity.z).normalized;
        if (slideDir.sqrMagnitude < 0.01f) slideDir = transform.forward;
        currentSlideSpeed = Mathf.Max(slideSpeed, horizontalVelocity.magnitude);
        sprintBlend = 0f;
    }

    private void UpdateSlide(Vector2 input, bool grounded)
    {
        slideTimer -= Time.deltaTime;
        currentSlideSpeed -= slideFriction * Time.deltaTime;

        // лёгкое подруливание
        Vector3 steer = transform.right * input.x;
        slideDir = Vector3.Slerp(slideDir, (slideDir + steer * 0.5f).normalized, slideSteer * Time.deltaTime);

        horizontalVelocity = slideDir * currentSlideSpeed;

        if (slideTimer <= 0f || currentSlideSpeed <= crouchSpeed || !grounded)
            EndSlide();
    }

    private void EndSlide()
    {
        if (!isSliding) return;
        isSliding = false;
        slideCooldownTimer = slideCooldown;
        // плавно гасим скорость до обычной
        float keep = Mathf.Min(currentSlideSpeed, crouchSpeed);
        horizontalVelocity = slideDir * keep;
    }

    private void HandleCameraEffects()
    {
        if (cameraTransform == null) return;

        Vector2 input = moveAction.ReadValue<Vector2>();
        float speed = horizontalVelocity.magnitude;
        bool isMoving = controller.isGrounded && speed > 0.3f && input.sqrMagnitude > 0.01f;

        float freq, amp;
        if (isCrouching)
        {
            freq = crouchBobFrequency;
            amp = crouchBobAmplitude;
        }
        else
        {
            freq = Mathf.Lerp(walkBobFrequency, sprintBobFrequency, sprintBlend);
            amp = Mathf.Lerp(walkBobAmplitude, sprintBobAmplitude, sprintBlend);
        }

        Vector3 targetBob = Vector3.zero;
        if (isMoving)
        {
            bobTimer += Time.deltaTime * freq;
            float intensity = Mathf.Clamp01(speed / sprintSpeed);
            targetBob.y = Mathf.Sin(bobTimer * 2f) * amp * intensity;
            targetBob.x = Mathf.Sin(bobTimer) * amp * bobLateralRatio * intensity;
        }
        bobOffset = Vector3.Lerp(bobOffset, targetBob, bobSmoothing * Time.deltaTime);

        idleTimer += Time.deltaTime * idleSwayFrequency;
        Vector3 idleOffset = new Vector3(
            Mathf.Sin(idleTimer * 0.7f) * idleSwayAmplitude,
            Mathf.Sin(idleTimer) * idleSwayAmplitude,
            0f);
        float idleWeight = isMoving ? 0f : 1f;
        idleOffset *= Mathf.Lerp(0f, 1f, idleWeight);

        Vector3 targetSway = new Vector3(
            Mathf.Clamp(-smoothedLookDelta.x * swayAmount, -swayMaxOffset, swayMaxOffset),
            Mathf.Clamp(-smoothedLookDelta.y * swayAmount, -swayMaxOffset, swayMaxOffset),
            0f);
        swayOffset = Vector3.Lerp(swayOffset, targetSway, swaySmoothing * Time.deltaTime);

        landingOffset = Mathf.Lerp(landingOffset, 0f, landingRecovery * Time.deltaTime);

        float targetCrouchBlend = isCrouching ? 1f : 0f;
        crouchBlend = Mathf.Lerp(crouchBlend, targetCrouchBlend, crouchCameraSmoothing * Time.deltaTime);
        float crouchY = -crouchCameraDip * crouchBlend;

        // Подкат: наклон камеры + доп. опускание
        float slideTiltTarget = isSliding ? slideCameraTilt : 0f;
        float slideDropTarget = isSliding ? -slideCameraDrop : 0f;
        slideTiltCurrent = Mathf.Lerp(slideTiltCurrent, slideTiltTarget, 8f * Time.deltaTime);
        slideDropCurrent = Mathf.Lerp(slideDropCurrent, slideDropTarget, 8f * Time.deltaTime);

        cameraTransform.localPosition = cameraBasePos + bobOffset + swayOffset + idleOffset
                                        + Vector3.up * (landingOffset + crouchY + slideDropCurrent);

        float targetTilt = -input.x * strafeTiltAngle;
        if (!controller.isGrounded) targetTilt *= 0.4f;
        currentTilt = Mathf.Lerp(currentTilt, targetTilt, tiltSmoothing * Time.deltaTime);
        cameraTransform.localRotation = Quaternion.Euler(pitch, 0f, currentTilt + slideTiltCurrent);

        if (cam != null)
        {
            float targetFov = Mathf.Lerp(defaultFov, sprintFov, sprintBlend * (isMoving ? 1f : 0f));
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFov, fovSmoothing * Time.deltaTime);
        }
    }
}
