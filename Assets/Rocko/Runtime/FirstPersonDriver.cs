using UnityEngine;

namespace Rocko
{
    [DefaultExecutionOrder(100)]
    [RequireComponent(typeof(CharacterController))]
    public sealed class FirstPersonDriver : MonoBehaviour
    {
        public RockoInput controls;
        public RockoVehicle car;
        public Camera view;
        public TestTelemetry telemetry;
        public bool Paused { get; private set; }
        public bool ShowHelp { get; private set; } = true;
        public bool Driving { get { return car.occupied; } }
        public string Prompt { get; private set; }
        private CharacterController capsule;
        private float yaw = 90, pitch = 12, verticalSpeed;

        //
        // @description - Initialize a walking character and the only gameplay camera
        // @return {Void} - Places the player outside Rocko in the garage
        //
        private void Start()
        {
            capsule = GetComponent<CharacterController>();
            capsule.height = 1.75f; capsule.radius = 0.25f;
            capsule.center = new Vector3(0, 0.875f, 0); capsule.stepOffset = 0.25f;
            capsule.slopeLimit = 48; capsule.skinWidth = 0.025f;
            transform.position = ProvingGround.GaragePlayer;
            view.transform.SetParent(transform, false);
            view.transform.localPosition = new Vector3(0, 1.63f, 0);
            SetPaused(false);
        }

        //
        // @description - Handle walking, entering, exiting, reset, and pause controls
        // @return {Void} - Updates player state and context prompt
        //
        private void Update()
        {
            if (controls.Pause) SetPaused(!Paused);
            if (controls.Help) ShowHelp = !ShowHelp;
            if (Paused) return;
            if (controls.Reset) { ResetGarage(); return; }
            if (controls.Straight)
            {
                car.ResetAt(ProvingGround.StraightCar, Quaternion.identity);
                Enter(); telemetry.ResetRun(); return;
            }
            if (controls.Log) telemetry.ToggleRecording();
            if (controls.Recenter) { yaw = Driving ? 0 : transform.eulerAngles.y; pitch = 4; }
            yaw += controls.Look.x;
            pitch = Mathf.Clamp(pitch - controls.Look.y, Driving ? -65 : -85, Driving ? 65 : 85);
            if (Driving)
            {
                yaw = Mathf.Clamp(yaw, -135, 135);
                Prompt = car.SpeedMPH < 1.1f ? "E / A  ·  Exit Rocko" : "Stop to exit  ·  V / R-stick  ·  Center view";
                if (controls.Interact && car.SpeedMPH < 1.1f) TryExit();
            }
            else
            {
                transform.rotation = Quaternion.Euler(0, yaw, 0);
                if (capsule.isGrounded && verticalSpeed < 0) verticalSpeed = -2;
                verticalSpeed += Physics.gravity.y * Time.deltaTime;
                Vector3 move = (transform.right * controls.Move.x + transform.forward * controls.Move.y) * 3.2f;
                capsule.Move((move + Vector3.up * verticalSpeed) * Time.deltaTime);
                bool nearDoor = Vector3.Distance(transform.position, car.transform.TransformPoint(new Vector3(-1.1f, 0, -0.25f))) < 2.1f;
                Prompt = nearDoor ? "E / A  ·  Enter Rocko" : "Walk to the driver's door  ·  WASD / left stick";
                if (nearDoor && controls.Interact && car.SpeedMPH < 1.1f) Enter();
            }
            if (transform.position.y < -20 || car.transform.position.y < -20) ResetGarage();
        }

        //
        // @description - Apply view rotation after the physics-interpolated car pose
        // @return {Void} - Updates the first-person view only
        //
        private void LateUpdate()
        {
            if (Paused) return;
            view.transform.localRotation = Quaternion.Euler(pitch, Driving ? yaw : 0, 0);
        }

        //
        // @description - Seat the player without spawning another camera
        // @return {Void} - Disables walking collision and parents the view to the driver's eye
        //
        private void Enter()
        {
            capsule.enabled = false;
            car.occupied = true;
            view.transform.SetParent(car.seat, false);
            view.transform.localPosition = Vector3.zero;
            yaw = 0; pitch = 4;
            ShowHelp = false;
        }

        //
        // @description - Find a grounded, unobstructed exit position on either side of the car
        // @return {Void} - Returns to walking or reports blocked exits
        //
        private void TryExit()
        {
            foreach (float side in new[] { -1f, 1f })
            {
                Vector3 p = car.transform.TransformPoint(new Vector3(side * 1.6f, 1.5f, -0.2f));
                if (!Physics.Raycast(p, Vector3.down, out RaycastHit hit, 4f, ~0, QueryTriggerInteraction.Ignore)) continue;
                if (hit.collider.GetComponentInParent<RockoVehicle>() != null) continue;
                Vector3 feet = hit.point + Vector3.up * 0.06f;
                if (Physics.CheckCapsule(feet + Vector3.up * 0.3f, feet + Vector3.up * 1.48f, 0.26f, ~0, QueryTriggerInteraction.Ignore)) continue;
                car.occupied = false;
                transform.position = feet;
                yaw = car.transform.eulerAngles.y + (side < 0 ? 90 : -90); pitch = 12;
                view.transform.SetParent(transform, false); view.transform.localPosition = new Vector3(0, 1.63f, 0);
                capsule.enabled = true; verticalSpeed = 0;
                return;
            }
            car.SetNotice("Both exits are obstructed. Move to a clear, level area.");
        }

        //
        // @description - Restore the original garage spawn outside the car
        // @return {Void} - Resets player, vehicle, and timing data
        //
        public void ResetGarage()
        {
            car.occupied = false;
            car.ResetAt(ProvingGround.GarageCar, Quaternion.identity);
            capsule.enabled = false;
            transform.position = ProvingGround.GaragePlayer;
            view.transform.SetParent(transform, false); view.transform.localPosition = new Vector3(0, 1.63f, 0);
            yaw = 90; pitch = 12; verticalSpeed = 0;
            capsule.enabled = true;
            telemetry.ResetRun();
        }

        //
        // @description - Pause simulation and release or capture the pointer
        // @param {bool} value - Desired pause state
        // @return {Void} - Sets simulation time and cursor state
        //
        public void SetPaused(bool value)
        {
            Paused = value;
            Time.timeScale = value ? 0 : 1;
            Cursor.lockState = value ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = value;
        }

        //
        // @description - Pause safely when the game window loses keyboard focus
        // @param {bool} focused - Whether the application has focus
        // @return {Void} - Pauses on focus loss
        //
        private void OnApplicationFocus(bool focused)
        {
            if (!focused) SetPaused(true);
        }

        //
        // @description - Restore editor timing and pointer when leaving Play mode
        // @return {Void} - Cleans up global state
        //
        private void OnDestroy()
        {
            Time.timeScale = 1; Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        }
    }
}
