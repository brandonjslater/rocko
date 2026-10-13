using UnityEngine;
using UnityEngine.InputSystem;

namespace Rocko
{
    [DefaultExecutionOrder(-200)]
    public sealed class RockoInput : MonoBehaviour
    {
        public float Throttle, Brake, Steer, Clutch;
        public Vector2 Move, Look;
        public bool Up, Down, Interact, Reset, Straight, Restart, Assist, Log, Pause, Handbrake;
        public bool Help, Recenter, Circuit;

        //
        // @description - Sample keyboard, mouse, and standardized gamepad controls without legacy axes
        // @return {Void} - Updates this frame's input state
        //
        private void Update()
        {
            Keyboard k = Keyboard.current;
            Mouse m = Mouse.current;
            Gamepad g = Gamepad.current;
            float x = k == null ? 0 : (k.dKey.isPressed ? 1 : 0) - (k.aKey.isPressed ? 1 : 0);
            float y = k == null ? 0 : (k.wKey.isPressed ? 1 : 0) - (k.sKey.isPressed ? 1 : 0);
            Vector2 stick = g == null ? Vector2.zero : g.leftStick.ReadValue();
            Move = Vector2.ClampMagnitude(new Vector2(x, y) + stick, 1);
            Steer = Mathf.Abs(x) > 0 ? x : stick.x;
            Throttle = Mathf.Max(k != null && k.wKey.isPressed ? 1 : 0, g == null ? 0 : g.rightTrigger.ReadValue());
            Brake = Mathf.Max(k != null && k.sKey.isPressed ? 1 : 0, g == null ? 0 : g.leftTrigger.ReadValue());
            Clutch = (k != null && k.cKey.isPressed) || (g != null && g.leftShoulder.isPressed) ? 1 : 0;
            Look = (m == null ? Vector2.zero : m.delta.ReadValue() * 0.09f) +
                (g == null ? Vector2.zero : g.rightStick.ReadValue() * (110f * Time.unscaledDeltaTime));
            Up = (k != null && (k.leftShiftKey.wasPressedThisFrame || k.rightShiftKey.wasPressedThisFrame)) || (g != null && g.buttonEast.wasPressedThisFrame);
            Down = (k != null && (k.leftCtrlKey.wasPressedThisFrame || k.rightCtrlKey.wasPressedThisFrame)) || (g != null && g.buttonWest.wasPressedThisFrame);
            Interact = (k != null && k.eKey.wasPressedThisFrame) || (g != null && g.rightShoulder.wasPressedThisFrame);
            Handbrake = (k != null && k.spaceKey.isPressed) || (g != null && g.buttonSouth.isPressed);
            Reset = (k != null && k.rKey.wasPressedThisFrame) || (g != null && g.selectButton.wasPressedThisFrame);
            Straight = (k != null && k.f2Key.wasPressedThisFrame) || (g != null && g.dpad.up.wasPressedThisFrame);
            Restart = (k != null && k.iKey.wasPressedThisFrame) || (g != null && g.buttonNorth.wasPressedThisFrame);
            Assist = (k != null && k.f3Key.wasPressedThisFrame) || (g != null && g.dpad.left.wasPressedThisFrame);
            Log = (k != null && k.f5Key.wasPressedThisFrame) || (g != null && g.dpad.right.wasPressedThisFrame);
            Pause = (k != null && k.escapeKey.wasPressedThisFrame) || (g != null && g.startButton.wasPressedThisFrame);
            Circuit = k != null && k.f4Key.wasPressedThisFrame;
            Help = (k != null && k.f1Key.wasPressedThisFrame) || (g != null && g.dpad.down.wasPressedThisFrame);
            Recenter = (k != null && k.vKey.wasPressedThisFrame) || (g != null && g.rightStickButton.wasPressedThisFrame);
        }
    }
}
