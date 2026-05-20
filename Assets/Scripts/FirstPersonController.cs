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

    [Header("Lean (Q / E)")]
    [SerializeField] private float leanAngle = 14f;
    [SerializeField] private float leanOffset = 0.42f;
    [SerializeField] private float leanSmoothing = 10f;
    [SerializeField] private bool leanCollisionCheck = true;
    [SerializeField] private float leanCollisionRadius = 0.2f;
    [SerializeField] private LayerMask leanCollisionMask = ~0;

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
    private InputAction leanLeftAction;
    private InputAction leanRightAction;

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
    private float currentLean;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        controller.height = standHeight;
        controller.center = new Vector3(0f, standHeight * 0.5f, 0f);

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

        leanLeftAction = new InputAction("LeanLeft", InputActionType.Button, binding: "<Keyboard>/q");
        leanLeftAction.AddBinding("<Gamepad>/leftShoulder");

        leanRightAction = new InputAction("LeanRight", InputActionType.Button, binding: "<Keyboard>/e");
        leanRightAction.AddBinding("<Gamepad>/rightShoulder");
    }

    private void OnEnable()
    {
        moveAction?.Enable();
        lookAction?.Enable();
        jumpAction?.Enable();
        sprintAction?.Enable();
        crouchAction?.Enable();
        leanLeftAction?.Enable();
        leanRightAction?.Enable();
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
        leanLeftAction?.Disable();
        leanRightAction?.Disable();
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
        wantsCrouch = !InputLocked && crouchAction.IsPressed();
        if (!wantsCrouch && isCrouching && HasCeilingAbove()) wantsCrouch = true;
        isCrouching = wantsCrouch;

        float target = isCrouching ? crouchHeight : standHeight;
        controller.height = Mathf.Lerp(controller.height, target, crouchTransition * Time.deltaTime);
        controller.center = new Vector3(0f, controller.height * 0.5f, 0f);
    }

    private bool HasCeilingAbove()
    {
        float rayLength = standHeight - controller.height + 0.1f;
        Vector3 origin = transform.position + Vector3.up * controller.height;
        return Physics.SphereCast(origin, controller.radius * 0.9f, Vector3.up,
                                  out _, rayLength, ceilingMask, QueryTriggerInteraction.Ignore);
    }

    private void HandleMove()
    {
        Vector2 input = InputLocked ? Vector2.zero : moveAction.ReadValue<Vector2>();
        Vector3 wishDir = transform.right * input.x + transform.forward * input.y;
        if (wishDir.sqrMagnitude > 1f) wishDir.Normalize();

        bool wantsSprint = !InputLocked && sprintAction.IsPressed() && !isCrouching && input.y > 0.1f;
        float sprintTarget = wantsSprint ? 1f : 0f;
        sprintBlend = Mathf.MoveTowards(sprintBlend, sprintTarget, Time.deltaTime / Mathf.Max(0.01f, sprintRampTime));

        float baseSpeed = isCrouching ? crouchSpeed : Mathf.Lerp(walkSpeed, sprintSpeed, sprintBlend);
        Vector3 targetVel = wishDir * baseSpeed;

        bool grounded = controller.isGrounded;
        float accel = (targetVel.sqrMagnitude > 0.01f) ? groundAccel : groundDecel;
        if (!grounded) accel *= airControl;
        horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, targetVel, accel * Time.deltaTime);

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
        if (canJump && jumpBuffered && !isCrouching && !InputLocked)
        {
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

        float leanInput = 0f;
        if (!InputLocked)
        {
            if (leanLeftAction != null && leanLeftAction.IsPressed()) leanInput -= 1f;
            if (leanRightAction != null && leanRightAction.IsPressed()) leanInput += 1f;
        }

        if (leanCollisionCheck && Mathf.Abs(leanInput) > 0.01f)
        {
            Vector3 worldOrigin = transform.TransformPoint(cameraBasePos + Vector3.up * crouchY);
            Vector3 worldDir = transform.right * Mathf.Sign(leanInput);
            float maxDist = Mathf.Abs(leanInput) * leanOffset;
            if (Physics.SphereCast(worldOrigin, leanCollisionRadius, worldDir,
                                   out RaycastHit hit, maxDist + leanCollisionRadius,
                                   leanCollisionMask, QueryTriggerInteraction.Ignore))
            {
                float allowed = Mathf.Max(0f, hit.distance - leanCollisionRadius) / leanOffset;
                leanInput = Mathf.Sign(leanInput) * Mathf.Clamp01(allowed);
            }
        }

        currentLean = Mathf.Lerp(currentLean, leanInput, leanSmoothing * Time.deltaTime);
        float leanX = currentLean * leanOffset;
        float leanY = -Mathf.Abs(currentLean) * leanOffset * 0.12f;
        float leanZRoll = -currentLean * leanAngle;

        cameraTransform.localPosition = cameraBasePos + bobOffset + swayOffset + idleOffset
                                        + Vector3.up * (landingOffset + crouchY + leanY)
                                        + Vector3.right * leanX;

        float targetTilt = -input.x * strafeTiltAngle;
        if (!controller.isGrounded) targetTilt *= 0.4f;
        currentTilt = Mathf.Lerp(currentTilt, targetTilt, tiltSmoothing * Time.deltaTime);
        cameraTransform.localRotation = Quaternion.Euler(pitch, 0f, currentTilt + leanZRoll);

        if (cam != null)
        {
            float targetFov = Mathf.Lerp(defaultFov, sprintFov, sprintBlend * (isMoving ? 1f : 0f));
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFov, fovSmoothing * Time.deltaTime);
        }
    }
}
