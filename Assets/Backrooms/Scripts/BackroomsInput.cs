using UnityEngine;
#if ENABLE_INPUT_SYSTEM && BACKROOMS_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Backrooms
{
    /// <summary>
    /// Thin input wrapper that works with either the new Input System package or the
    /// legacy Input Manager, whichever the project has enabled.
    /// BACKROOMS_INPUT_SYSTEM is defined by Backrooms.Runtime.asmdef only when the
    /// com.unity.inputsystem package is installed, because ENABLE_INPUT_SYSTEM alone
    /// only reflects the player setting.
    /// </summary>
    public static class BackroomsInput
    {
#if ENABLE_INPUT_SYSTEM && BACKROOMS_INPUT_SYSTEM
        // Scale raw pixel deltas to roughly match the legacy "Mouse X/Y" axes.
        const float MouseScale = 0.1f;

        static bool Held(Key key) => Keyboard.current != null && Keyboard.current[key].isPressed;
        static bool Pressed(Key key) => Keyboard.current != null && Keyboard.current[key].wasPressedThisFrame;

        public static Vector2 Move
        {
            get
            {
                var v = Vector2.zero;
                if (Held(Key.W) || Held(Key.UpArrow)) v.y += 1;
                if (Held(Key.S) || Held(Key.DownArrow)) v.y -= 1;
                if (Held(Key.D) || Held(Key.RightArrow)) v.x += 1;
                if (Held(Key.A) || Held(Key.LeftArrow)) v.x -= 1;
                return Vector2.ClampMagnitude(v, 1f);
            }
        }

        public static Vector2 Look => Mouse.current != null ? Mouse.current.delta.ReadValue() * MouseScale : Vector2.zero;
        public static bool Sprint => Held(Key.LeftShift) || Held(Key.RightShift);
        public static bool Crouch => Held(Key.LeftCtrl) || Held(Key.C);
        public static bool JumpPressed => Pressed(Key.Space);
        public static bool FlashlightPressed => Pressed(Key.F);
        public static bool DebugPressed => Pressed(Key.F3);
        public static bool ClickPressed => Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        public static bool EscapePressed => Pressed(Key.Escape);
#elif ENABLE_LEGACY_INPUT_MANAGER
        public static Vector2 Move
        {
            get
            {
                var v = Vector2.zero;
                if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) v.y += 1;
                if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) v.y -= 1;
                if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) v.x += 1;
                if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) v.x -= 1;
                return Vector2.ClampMagnitude(v, 1f);
            }
        }

        public static Vector2 Look => new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y"));
        public static bool Sprint => Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        public static bool Crouch => Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.C);
        public static bool JumpPressed => Input.GetKeyDown(KeyCode.Space);
        public static bool FlashlightPressed => Input.GetKeyDown(KeyCode.F);
        public static bool DebugPressed => Input.GetKeyDown(KeyCode.F3);
        public static bool ClickPressed => Input.GetMouseButtonDown(0);
        public static bool EscapePressed => Input.GetKeyDown(KeyCode.Escape);
#else
        // The project uses the new Input System, but the package isn't installed.
        static bool warned;

        static bool Missing()
        {
            if (!warned)
            {
                warned = true;
                Debug.LogError("Backrooms: Active Input Handling is set to the Input System, but the " +
                               "com.unity.inputsystem package is not installed. Install it from the Package Manager, " +
                               "or set Project Settings > Player > Active Input Handling to 'Both'.");
            }
            return false;
        }

        public static Vector2 Move => Vector2.zero;
        public static Vector2 Look => Vector2.zero;
        public static bool Sprint => false;
        public static bool Crouch => false;
        public static bool JumpPressed => false;
        public static bool FlashlightPressed => false;
        public static bool DebugPressed => false;
        public static bool ClickPressed => Missing();
        public static bool EscapePressed => false;
#endif
    }
}
