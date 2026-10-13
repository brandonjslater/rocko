using UnityEngine;
using UnityEngine.InputSystem;

namespace Rocko
{
    [DefaultExecutionOrder(-200)]
    public sealed class RockoInput : MonoBehaviour
    {
        [Header("Controller response")]
        [Range(0, 0.3f)] public float steeringDeadzone = 0.08f;
        [Range(1, 3)] public float steeringExponent = 1.6f;
        [Range(1, 15)] public float steeringApplyRate = 4.5f;
        [Range(1, 15)] public float steeringUnwindRate = 8f;
        [Range(0, 0.2f)] public float triggerDeadzone = 0.02f;
        [Range(1, 2)] public float throttleExponent = 1.2f;
        [Range(1, 2)] public float brakeExponent = 1f;
        [Range(1, 20)] public float throttleApplyRate = 8f;
        [Range(1, 20)] public float throttleReleaseRate = 12f;
        [Range(1, 20)] public float brakeApplyRate = 12f;
        [HideInInspector] public bool ControllerSteering, ControllerThrottle, ControllerBrake;
        public float Throttle, Brake, Steer, Clutch;
        public Vector2 Move, Look;
        public bool Up, Down, Interact, Reset, Straight, Restart, Assist, Log, Pause, Handbrake;
        public bool Help, Recenter, Circuit, Crouch;

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
            ControllerSteering = g != null && x == 0;
            ControllerThrottle = g != null && !(k != null && k.wKey.isPressed);
            ControllerBrake = g != null && !(k != null && k.sKey.isPressed);
            // Bypass the stick processor so the custom deadzone is applied exactly once.
            Steer = x != 0 ? x : g == null ? 0 : (float)ControllerResponse.Shape(
                g.leftStick.ReadUnprocessedValue().x, steeringDeadzone, steeringExponent);
            Throttle = k != null && k.wKey.isPressed ? 1 : g == null ? 0 :
                (float)ControllerResponse.Shape(g.rightTrigger.ReadUnprocessedValue(), triggerDeadzone, throttleExponent);
            Brake = k != null && k.sKey.isPressed ? 1 : g == null ? 0 :
                (float)ControllerResponse.Shape(g.leftTrigger.ReadUnprocessedValue(), triggerDeadzone, brakeExponent);
            Clutch = (k != null && k.cKey.isPressed) || (g != null && g.leftShoulder.isPressed) ? 1 : 0;
            Look = (m == null ? Vector2.zero : m.delta.ReadValue() * 0.09f) +
                (g == null ? Vector2.zero : g.rightStick.ReadValue() * (110f * Time.unscaledDeltaTime));
            Up = (k != null && (k.leftShiftKey.wasPressedThisFrame || k.rightShiftKey.wasPressedThisFrame)) || (g != null && g.buttonEast.wasPressedThisFrame);
            Down = (k != null && (k.leftCtrlKey.wasPressedThisFrame || k.rightCtrlKey.wasPressedThisFrame)) || (g != null && g.buttonWest.wasPressedThisFrame);
            Interact = (m != null && m.leftButton.wasPressedThisFrame) || (g != null && g.rightShoulder.wasPressedThisFrame);
            Crouch = (k != null && k.cKey.wasPressedThisFrame) || (g != null && g.leftStickButton.wasPressedThisFrame);
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
