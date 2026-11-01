using UnityEngine;
using UnityEngine.InputSystem;
using Skyforge.Simulation;
// The Input System has its own PlayerInput component; ours is the simulation's input struct.
using PlayerInput = Skyforge.Simulation.PlayerInput;

namespace Skyforge.Game
{
    /// <summary>
    /// Reads keyboard, mouse, gamepad and touch each frame and turns them into a
    /// <see cref="PlayerInput"/> for the simulation (docs/DESIGN.md §8). Firing is automatic.
    /// <list type="bullet">
    /// <item>Keyboard: WASD / arrows move, Shift = precision, Space = core, Esc / P = pause.</item>
    /// <item>Mouse: the ship follows the cursor; right button = precision, left click = core.</item>
    /// <item>Gamepad: left stick / d-pad move, LT = precision, A or RT = core, Start = pause.</item>
    /// <item>Touch: drag anywhere to move the ship by the finger's movement; tap with a second finger = core.</item>
    /// </list>
    /// Priority when several are used at once: touch, then keys/stick, then mouse.
    /// </summary>
    public sealed class PlayerControls
    {
        const float StickDeadZone = 0.15f;
        /// <summary>Mouse movement (pixels) that hands control back to the mouse after keys/stick.</summary>
        const float MouseWakePixels = 3f;

        bool _mouseActive;
        bool _secondFingerDown;

        /// <summary>True when the last input came from a touchscreen (the HUD shows touch hints).</summary>
        public bool UsingTouch { get; private set; }

        /// <summary>Read this frame's input. <paramref name="drag"/> is the touch movement this frame.</summary>
        public PlayerInput Read(PlayfieldCamera cam)
        {
            var input = new PlayerInput();
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            var pad = Gamepad.current;
            var touch = Touchscreen.current;

            // ------------------------------------------------------------ direction (keys + stick)
            Vector2 dir = Vector2.zero;
            if (kb != null)
            {
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) dir.x -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) dir.x += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) dir.y -= 1f;
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) dir.y += 1f;
                if (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed) input.Precision = true;
                if (kb.spaceKey.wasPressedThisFrame) input.Core = true;
            }
            if (pad != null)
            {
                var stick = pad.leftStick.ReadValue();
                if (stick.magnitude > StickDeadZone) dir += stick;
                dir += pad.dpad.ReadValue();
                if (pad.leftTrigger.ReadValue() > 0.5f) input.Precision = true;
                if (pad.buttonSouth.wasPressedThisFrame || pad.rightTrigger.wasPressedThisFrame) input.Core = true;
            }
            if (dir.sqrMagnitude > 1f) dir.Normalize();

            // ------------------------------------------------------------ touch: relative drag
            if (touch != null)
            {
                var primary = touch.primaryTouch;
                if (primary.press.isPressed)
                {
                    UsingTouch = true;
                    _mouseActive = false;
                    var delta = primary.delta.ReadValue();
                    input.Mode = MoveMode.Drag;
                    input.Drag = cam.ScreenDeltaToField(primary.position.ReadValue(), delta);

                    // A second finger touching down fires the core.
                    bool second = false;
                    var touches = touch.touches;
                    for (int i = 0; i < touches.Count; i++)
                        if (touches[i].press.isPressed && touches[i].touchId.ReadValue() != primary.touchId.ReadValue())
                            second = true;
                    if (second && !_secondFingerDown) input.Core = true;
                    _secondFingerDown = second;
                    return input;
                }
                _secondFingerDown = false;
            }

            if (dir != Vector2.zero)
            {
                _mouseActive = false;
                UsingTouch = false;
                input.Mode = MoveMode.Direction;
                input.Direction = new Vec2(dir.x, dir.y);
                return input;
            }

            // ------------------------------------------------------------ mouse: follow the cursor
            if (mouse != null)
            {
                if (mouse.delta.ReadValue().magnitude > MouseWakePixels || mouse.leftButton.wasPressedThisFrame)
                {
                    _mouseActive = true;
                    UsingTouch = false;
                }
                if (mouse.rightButton.isPressed) input.Precision = true;
                if (mouse.leftButton.wasPressedThisFrame) input.Core = true;
                if (_mouseActive && cam.TryScreenToField(mouse.position.ReadValue(), out var target))
                {
                    input.Mode = MoveMode.Target;
                    input.Target = target;
                }
            }
            return input;
        }

        /// <summary>Pause toggle pressed this frame (Esc / P / Start).</summary>
        public static bool PausePressed()
        {
            var kb = Keyboard.current;
            var pad = Gamepad.current;
            return (kb != null && (kb.escapeKey.wasPressedThisFrame || kb.pKey.wasPressedThisFrame)) ||
                   (pad != null && pad.startButton.wasPressedThisFrame);
        }

        /// <summary>Test-flight shortcut: number key 1–5 pressed this frame, else 0.</summary>
        public static int LevelKeyPressed()
        {
            var kb = Keyboard.current;
            if (kb == null) return 0;
            if (kb.digit1Key.wasPressedThisFrame) return 1;
            if (kb.digit2Key.wasPressedThisFrame) return 2;
            if (kb.digit3Key.wasPressedThisFrame) return 3;
            if (kb.digit4Key.wasPressedThisFrame) return 4;
            if (kb.digit5Key.wasPressedThisFrame) return 5;
            return 0;
        }

        public static bool RestartPressed() => Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame;

        /// <summary>Test keys: 6 = stage 1-1, 7 = Armada Sampler, 8 = practice flight; else 0.</summary>
        public static int StageKeyPressed()
        {
            var kb = Keyboard.current;
            if (kb == null) return 0;
            if (kb.digit6Key.wasPressedThisFrame) return 6;
            if (kb.digit7Key.wasPressedThisFrame) return 7;
            if (kb.digit8Key.wasPressedThisFrame) return 8;
            return 0;
        }

        /// <summary>Any click, tap, Space, Enter or gamepad A this frame (e.g. "fly again").</summary>
        public static bool ConfirmPressed()
        {
            var kb = Keyboard.current; var mouse = Mouse.current; var pad = Gamepad.current; var touch = Touchscreen.current;
            return (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame)) ||
                   (mouse != null && mouse.leftButton.wasPressedThisFrame) ||
                   (pad != null && pad.buttonSouth.wasPressedThisFrame) ||
                   (touch != null && touch.primaryTouch.press.wasPressedThisFrame);
        }
    }
}
