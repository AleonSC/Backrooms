using UnityEngine;

namespace Backrooms
{
    /// <summary>
    /// CharacterController based first person controller with mouse look, walking,
    /// stamina-limited sprinting, crouching, jumping, head bob, footsteps and a flashlight.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class FirstPersonController : MonoBehaviour
    {
        [Header("Look")]
        public float mouseSensitivity = 2f;
        public float maxPitch = 85f;
        public float fieldOfView = 75f;

        [Header("Movement")]
        public float walkSpeed = 2.4f;
        public float sprintSpeed = 4.8f;
        public float crouchSpeed = 1.3f;
        public float acceleration = 12f;
        [Range(0f, 1f)] public float airControl = 0.3f;
        public float gravity = 20f;
        public float jumpHeight = 0.8f;

        [Header("Stamina")]
        public float staminaDrain = 0.18f;
        public float staminaRegen = 0.12f;
        public float staminaRegenDelay = 1f;
        [Tooltip("After running out, sprinting is locked until stamina recovers to this level.")]
        public float exhaustedRecoverLevel = 0.3f;

        [Header("Body")]
        public float radius = 0.3f;
        public float standingHeight = 1.75f;
        public float crouchHeight = 1.05f;
        public float eyeFromTop = 0.12f;
        public float crouchTransitionSpeed = 8f;

        [Header("Head Bob")]
        public float bobVertical = 0.045f;
        public float bobHorizontal = 0.03f;
        public float strafeRoll = 1.2f;

        [Header("Audio")]
        public AudioClip[] footstepClips;
        public float footstepVolume = 0.35f;

        [Header("References (created if empty)")]
        public Camera playerCamera;
        public Light flashlight;
        public BackroomsWorld world;

        public float Stamina { get; private set; } = 1f;
        public bool IsCursorLocked => Cursor.lockState == CursorLockMode.Locked;

        CharacterController controller;
        AudioSource footstepSource;
        Vector3 horizontalVelocity;
        float verticalVelocity;
        float yaw, pitch, roll;
        float currentHeight;
        float staminaCooldown;
        bool exhausted;
        bool crouching;
        bool wasGrounded = true;
        float bobPhase, bobBlend;
        int lastStep;
        float landingOffset, landingVelocity;
        bool showDebug;
        float smoothedDeltaTime = 1f / 60f;
        Texture2D white;

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            controller.radius = radius;
            controller.height = standingHeight;
            controller.center = new Vector3(0f, standingHeight * 0.5f, 0f);
            controller.stepOffset = 0.3f;
            controller.slopeLimit = 45f;
            controller.skinWidth = 0.03f;
            controller.minMoveDistance = 0f;
            currentHeight = standingHeight;

            if (playerCamera == null)
            {
                var camGo = new GameObject("Player Camera");
                camGo.tag = "MainCamera";
                camGo.transform.SetParent(transform, false);
                playerCamera = camGo.AddComponent<Camera>();
                camGo.AddComponent<AudioListener>();
            }
            playerCamera.fieldOfView = fieldOfView;
            playerCamera.nearClipPlane = 0.05f;
            playerCamera.transform.localPosition = new Vector3(0f, standingHeight - eyeFromTop, 0f);

            if (flashlight == null)
            {
                var lightGo = new GameObject("Flashlight");
                lightGo.transform.SetParent(playerCamera.transform, false);
                lightGo.transform.localPosition = new Vector3(0.15f, -0.15f, 0.1f);
                flashlight = lightGo.AddComponent<Light>();
                flashlight.type = LightType.Spot;
                flashlight.spotAngle = 55f;
                flashlight.innerSpotAngle = 25f;
                flashlight.range = 18f;
                flashlight.intensity = 2.5f;
                flashlight.color = new Color(1f, 0.96f, 0.88f);
                flashlight.shadows = LightShadows.None;
                flashlight.renderMode = LightRenderMode.ForcePixel;
                flashlight.enabled = false;
            }

            footstepSource = gameObject.AddComponent<AudioSource>();
            footstepSource.playOnAwake = false;
            footstepSource.spatialBlend = 0f;

            yaw = transform.eulerAngles.y;
            white = new Texture2D(1, 1);
            white.SetPixel(0, 0, Color.white);
            white.Apply();
        }

        void OnDestroy()
        {
            if (white != null) Destroy(white);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            smoothedDeltaTime = Mathf.Lerp(smoothedDeltaTime, Time.unscaledDeltaTime, 0.05f);

            HandleCursor();
            bool active = IsCursorLocked;

            if (active) Look();
            Move(active, dt);
            UpdateHeight(dt);
            UpdateCamera(active, dt);

            if (active && BackroomsInput.FlashlightPressed) flashlight.enabled = !flashlight.enabled;
            if (BackroomsInput.DebugPressed) showDebug = !showDebug;
        }

        void HandleCursor()
        {
            if (BackroomsInput.EscapePressed)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else if (!IsCursorLocked && BackroomsInput.ClickPressed)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        void Look()
        {
            Vector2 delta = BackroomsInput.Look * mouseSensitivity;
            yaw += delta.x;
            pitch = Mathf.Clamp(pitch - delta.y, -maxPitch, maxPitch);
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        }

        bool CanStand()
        {
            if (currentHeight >= standingHeight - 0.01f) return true;
            // Cast from inside the capsule's bottom sphere so our own collider is ignored.
            Vector3 origin = transform.position + Vector3.up * radius;
            float distance = standingHeight - radius * 2f + 0.05f;
            return !Physics.SphereCast(origin, radius * 0.95f, Vector3.up, out _, distance,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        }

        void Move(bool active, float dt)
        {
            Vector2 input = active ? BackroomsInput.Move : Vector2.zero;
            bool grounded = controller.isGrounded;

            crouching = (active && BackroomsInput.Crouch) || (crouching && !CanStand());

            if (Stamina <= 0f) exhausted = true;
            else if (exhausted && Stamina >= exhaustedRecoverLevel) exhausted = false;

            bool moving = input.sqrMagnitude > 0.01f;
            bool sprinting = active && BackroomsInput.Sprint && input.y > 0.1f && !crouching && !exhausted;
            float targetSpeed = crouching ? crouchSpeed : sprinting ? sprintSpeed : walkSpeed;

            Vector3 wish = (transform.right * input.x + transform.forward * input.y) * targetSpeed;
            float control = grounded ? 1f : airControl;
            horizontalVelocity = Vector3.Lerp(horizontalVelocity, wish, 1f - Mathf.Exp(-acceleration * control * dt));

            if (sprinting && moving)
            {
                Stamina = Mathf.Max(0f, Stamina - staminaDrain * dt);
                staminaCooldown = staminaRegenDelay;
            }
            else if ((staminaCooldown -= dt) <= 0f)
            {
                Stamina = Mathf.Min(1f, Stamina + staminaRegen * dt);
            }

            if (grounded && verticalVelocity < 0f) verticalVelocity = -2f; // keep snapped to the floor
            if (grounded && active && !crouching && BackroomsInput.JumpPressed)
                verticalVelocity = Mathf.Sqrt(2f * gravity * jumpHeight);
            verticalVelocity -= gravity * dt;

            float fallSpeed = verticalVelocity;
            var flags = controller.Move((horizontalVelocity + Vector3.up * verticalVelocity) * dt);

            if ((flags & CollisionFlags.Above) != 0 && verticalVelocity > 0f) verticalVelocity = 0f;
            if ((flags & CollisionFlags.Sides) != 0)
            {
                // Bleed off velocity into walls so we don't stay "pressed" against them.
                Vector3 v = controller.velocity;
                horizontalVelocity = new Vector3(v.x, 0f, v.z);
            }

            bool nowGrounded = controller.isGrounded;
            if (nowGrounded && !wasGrounded && fallSpeed < -3f)
                landingVelocity = Mathf.Clamp(fallSpeed * 0.3f, -2.5f, 0f);
            wasGrounded = nowGrounded;
        }

        void UpdateHeight(float dt)
        {
            float target = crouching ? crouchHeight : standingHeight;
            currentHeight = Mathf.MoveTowards(currentHeight, target, crouchTransitionSpeed * dt * (standingHeight - crouchHeight));
            controller.height = currentHeight;
            controller.center = new Vector3(0f, currentHeight * 0.5f, 0f);
        }

        void UpdateCamera(bool active, float dt)
        {
            Vector3 v = controller.velocity;
            float speed = new Vector2(v.x, v.z).magnitude;
            bool grounded = controller.isGrounded;

            float targetBlend = grounded && speed > 0.2f ? Mathf.Clamp01(speed / walkSpeed) : 0f;
            bobBlend = Mathf.Lerp(bobBlend, targetBlend, 1f - Mathf.Exp(-10f * dt));

            // Half a sine period per footstep; stride lengthens with speed.
            float stride = 0.6f + 0.07f * speed;
            if (grounded) bobPhase += speed / stride * Mathf.PI * dt;

            int step = Mathf.FloorToInt(bobPhase / Mathf.PI);
            if (step != lastStep)
            {
                lastStep = step;
                if (bobBlend > 0.3f) PlayFootstep(speed);
            }

            // Critically damped spring for the landing dip.
            landingVelocity += (-landingOffset * 150f - landingVelocity * 24.5f) * dt;
            landingOffset += landingVelocity * dt;

            float vertical = (Mathf.Abs(Mathf.Sin(bobPhase)) - 0.5f) * 2f * bobVertical * bobBlend;
            float lateral = Mathf.Sin(bobPhase) * bobHorizontal * bobBlend;
            float eye = currentHeight - eyeFromTop;
            playerCamera.transform.localPosition = new Vector3(lateral, eye + vertical + landingOffset, 0f);

            float strafe = active ? BackroomsInput.Move.x : 0f;
            roll = Mathf.Lerp(roll, -strafe * strafeRoll, 1f - Mathf.Exp(-6f * dt));
            playerCamera.transform.localRotation = Quaternion.Euler(pitch, 0f, roll);
        }

        void PlayFootstep(float speed)
        {
            if (footstepClips == null || footstepClips.Length == 0) return;
            var clip = footstepClips[Random.Range(0, footstepClips.Length)];
            float loudness = crouching ? 0.4f : speed > walkSpeed + 0.5f ? 1.25f : 1f;
            footstepSource.pitch = Random.Range(0.9f, 1.1f);
            footstepSource.PlayOneShot(clip, footstepVolume * loudness);
        }

        void OnGUI()
        {
            float w = Screen.width, h = Screen.height;

            if (!IsCursorLocked)
            {
                GUI.color = new Color(0f, 0f, 0f, 0.6f);
                GUI.DrawTexture(new Rect(0, 0, w, h), white);
                GUI.color = Color.white;
                var title = new GUIStyle(GUI.skin.label) { fontSize = 42, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
                var body = new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.MiddleCenter };
                GUI.Label(new Rect(0, h * 0.5f - 110, w, 60), "THE BACKROOMS", title);
                GUI.Label(new Rect(0, h * 0.5f - 40, w, 30), "Click to enter", body);
                GUI.Label(new Rect(0, h * 0.5f + 10, w, 120),
                    "WASD move   Shift sprint   Ctrl / C crouch   Space jump\n" +
                    "F flashlight   F3 debug info   Esc release cursor", body);
            }
            else
            {
                GUI.color = new Color(1f, 1f, 1f, 0.5f);
                GUI.DrawTexture(new Rect(w * 0.5f - 2, h * 0.5f - 2, 4, 4), white);

                if (Stamina < 0.999f)
                {
                    float bw = 220, bh = 6, x = (w - bw) * 0.5f, y = h - 48;
                    GUI.color = new Color(0f, 0f, 0f, 0.5f);
                    GUI.DrawTexture(new Rect(x, y, bw, bh), white);
                    GUI.color = exhausted ? new Color(0.85f, 0.35f, 0.25f, 0.8f) : new Color(0.95f, 0.9f, 0.7f, 0.8f);
                    GUI.DrawTexture(new Rect(x, y, bw * Stamina, bh), white);
                }
            }

            if (showDebug)
            {
                GUI.color = Color.white;
                Vector3 p = transform.position;
                int cx = Mathf.FloorToInt(p.x / BackroomsGenerator.Cell);
                int cz = Mathf.FloorToInt(p.z / BackroomsGenerator.Cell);
                string text = string.Format("FPS {0:0}\nPos {1:0.0}, {2:0.0}, {3:0.0}\nCell {4}, {5}",
                    1f / smoothedDeltaTime, p.x, p.y, p.z, cx, cz);
                if (world != null && world.Generator != null)
                {
                    text += string.Format("\nZone {0}\nSeed {1}\nChunks {2}",
                        world.Generator.GetZone(cx, cz), world.Generator.Seed, world.LoadedChunkCount);
                }
                GUI.Label(new Rect(10, 10, 400, 140), text);
            }
            GUI.color = Color.white;
        }
    }
}
