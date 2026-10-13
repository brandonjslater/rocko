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
        public bool ShowHelp { get; private set; }
        public bool Driving { get { return car.occupied; } }
        public string Prompt { get; private set; }
        public bool OnCircuit { get; private set; }
        public bool Crouched { get; private set; }
        private Vector3 seatedFeet;
        private const float StandingHeight = 1.75f, CrouchedHeight = 0.7f;
        private CarAccess access;
        private LagunaSeca circuit;
        private CharacterController capsule;
        private float yaw = 90, pitch = 12, verticalSpeed;

        //
        // @description - Initialize a walking character and the only gameplay camera
        // @return {Void} - Places the player outside Rocko in the garage
        //
        private void Start()
        {
            access = car.GetComponent<CarAccess>();
            capsule = GetComponent<CharacterController>();
            gameObject.layer = CarAccess.PlayerLayer;
            capsule.height = StandingHeight; capsule.radius = 0.19f;
            capsule.center = new Vector3(0, 0.875f, 0); capsule.stepOffset = 0.32f;
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
            if (controls.Circuit) { StageCircuit(); return; }
            if (controls.Straight)
            {
                OnCircuit = false;
                car.ResetAt(ProvingGround.StraightCar, Quaternion.identity);
                StagePlayer(); telemetry.ResetRun(); return;
            }
            if (controls.Log) telemetry.ToggleRecording();
            if (controls.Recenter) { yaw = Driving ? 0 : transform.eulerAngles.y; pitch = 4; }
            yaw += controls.Look.x;
            pitch = Mathf.Clamp(pitch - controls.Look.y, Driving ? -65 : -85, Driving ? 65 : 85);
            if (Driving)
            {
                yaw = Mathf.Clamp(yaw, -135, 135);
                Collider target = access.FindTarget(view, transform);
                CarPanel panel = target == null ? null : target.GetComponentInParent<CarPanel>();
                if (car.SpeedMPH >= 1.1f) Prompt = "Stop to open a door or exit · V / R3 · Center view";
                else if (panel == access.driverDoor || panel == access.passengerDoor)
                {
                    Prompt = "Mouse 1 / R1 · " + (panel.IsOpen ? "Close " : "Open ") + panel.Label;
                    if (controls.Interact) panel.SetOpen(!panel.IsOpen);
                }
                else
                {
                    Prompt = "Mouse 1 / R1 · Get up · Then move out through the open door";
                    if (controls.Interact) TryExit();
                }
            }
            else
            {
                if (controls.Crouch) SetCrouched(!Crouched);
                transform.rotation = Quaternion.Euler(0, yaw, 0);
                if (capsule.isGrounded && verticalSpeed < 0) verticalSpeed = -2;
                verticalSpeed += Physics.gravity.y * Time.deltaTime;
                Vector3 move = (transform.right * controls.Move.x + transform.forward * controls.Move.y) * (Crouched ? 1.1f : 3.2f);
                capsule.Move((move + Vector3.up * verticalSpeed) * Time.deltaTime);
                Collider target = access.FindTarget(view, transform);
                CarPanel panel = target == null ? null : target.GetComponentInParent<CarPanel>();
                Prompt = "Mouse 1 / R1 · Interact    C / L3 · Crouch    WASD / left stick · Move";
                if (car.SpeedMPH >= 1.1f) Prompt = "Wait for Rocko to stop";
                else if (panel != null)
                {
                    Prompt = "Mouse 1 / R1 · " + (panel.IsOpen ? "Close " : "Open ") + panel.Label;
                    if (controls.Interact) panel.SetOpen(!panel.IsOpen);
                }
                else if (target == access.seatTarget)
                {
                    Prompt = access.CanEnter(transform.position) ? "Mouse 1 / R1 · Sit in driver seat"
                        : "Open driver door · C / L3 to crouch · Move inside to the driver seat";
                    if (controls.Interact) TryEnter();
                }
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
        // @description - Sit only after the player has moved inside to the driver seat
        // @return {bool} - Whether driving controls became active
        //
        public bool TryEnter()
        {
            if (!access.CanEnter(transform.position)) return false;
            seatedFeet = car.transform.InverseTransformPoint(transform.position);
            Enter();
            return true;
        }

        //
        // @description - Get up inside the cabin; the player must move through the doorway manually
        // @return {Void} - Returns control to a crouched walking character at the seat
        //
        private void TryExit()
        {
            if (car.SpeedMPH >= 1.1f) return;
            Vector3 feet = car.transform.TransformPoint(seatedFeet);
            if (StandingBlocked(feet, CrouchedHeight))
            {
                car.SetNotice("No room to get up from the seat.");
                return;
            }
            car.occupied = false;
            transform.position = feet;
            yaw = car.transform.eulerAngles.y + yaw;
            transform.rotation = Quaternion.Euler(0, yaw, 0);
            view.transform.SetParent(transform, false);
            Crouched = true; ApplyPosture();
            capsule.enabled = true; verticalSpeed = 0;
        }

        //
        // @description - Toggle crouching without allowing standing through a roof or ceiling
        // @param {bool} crouched - Desired posture
        // @return {bool} - Whether the posture was applied
        //
        public bool SetCrouched(bool crouched)
        {
            if (Driving) return false;
            if (!crouched && StandingBlocked(transform.position, StandingHeight)) return false;
            Crouched = crouched; ApplyPosture();
            return true;
        }

        //
        // @description - Check body clearance while excluding the player's own capsule and coarse vehicle chassis
        // @param {Vector3} feet - Desired feet position
        // @param {float} height - Desired capsule height
        // @return {bool} - Whether a solid object prevents this posture
        //
        private bool StandingBlocked(Vector3 feet, float height)
        {
            foreach (Collider hit in Physics.OverlapCapsule(feet + Vector3.up * 0.21f,
                feet + Vector3.up * (height - 0.19f), 0.18f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit == capsule || access.IsChassis(hit)) continue;
                return true;
            }
            return false;
        }

        //
        // @description - Apply a compact crouch that fits the car and a matching eye height
        // @return {Void} - Updates walking body and camera without horizontal movement
        //
        private void ApplyPosture()
        {
            capsule.height = Crouched ? CrouchedHeight : StandingHeight;
            capsule.center = new Vector3(0, capsule.height / 2, 0);
            capsule.stepOffset = Crouched ? 0.4f : 0.32f;
            view.transform.localPosition = new Vector3(0, Crouched ? 0.62f : 1.63f, 0);
        }

        //
        // @description - Preserve a seated driver when staging, otherwise place the walker beside the car
        // @return {Void} - Prevents test-location shortcuts bypassing door entry
        //
        private void StagePlayer()
        {
            if (Driving) { Enter(); return; }
            Crouched = false; ApplyPosture();
            capsule.enabled = false;
            transform.position = car.transform.TransformPoint(new Vector3(-1.6f, 0.1f, -0.2f));
            yaw = car.transform.eulerAngles.y + 90; pitch = 12; verticalSpeed = 0;
            transform.rotation = Quaternion.Euler(0, yaw, 0);
            view.transform.SetParent(transform, false); view.transform.localPosition = new Vector3(0, 1.63f, 0);
            capsule.enabled = true;
        }

        //
        // @description - Build Laguna Seca on demand and stage the existing car at start/finish
        // @return {Void} - Stages car and player, preserving whether the player was already seated
        //
        public void StageCircuit()
        {
            if (circuit == null) circuit = new GameObject("Laguna Seca / approximate reference layout").AddComponent<LagunaSeca>();
            car.ResetAt(circuit.StartPosition, circuit.StartRotation);
            OnCircuit = true;
            StagePlayer();
            telemetry.ResetRun();
            SetPaused(false);
            car.SetNotice("Laguna Seca · approximate layout · F4 reset · R / Create return to garage");
        }

        //
        // @description - Restore the original garage spawn outside the car
        // @return {Void} - Resets player, vehicle, and timing data
        //
        public void ResetGarage()
        {
            Crouched = false; ApplyPosture();
            OnCircuit = false;
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
