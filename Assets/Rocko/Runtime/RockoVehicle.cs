using UnityEngine;

namespace Rocko
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class RockoVehicle : MonoBehaviour
    {
        public VehicleSpec spec;
        public RockoInput controls;
        public WheelCollider[] wheels;
        public Transform[] wheelVisuals;
        public Transform seat, steeringWheel;
        public bool occupied, clutchAssist = true;
        public Rigidbody Body { get; private set; }
        public int Gear { get; private set; }
        public float RPM { get; private set; }
        public float WheelHP { get; private set; }
        public float AxleTorqueNm { get; private set; }
        public float CrankTorqueNm { get; private set; }
        public float LongitudinalG { get; private set; }
        public float LateralG { get; private set; }
        public float ClutchEngagement { get; private set; }
        public float MaxDrivenSlip { get; private set; }
        public float SpeedMPH { get { return Body.linearVelocity.magnitude * 2.2369363f; } }
        public float ForwardSpeed { get { return Vector3.Dot(Body.linearVelocity, transform.forward); } }
        public bool EngineRunning { get; private set; }
        public bool Shifting { get { return shiftTime > 0; } }
        public bool ClutchLocked { get { return Gear != 0 && ClutchEngagement > 0.95f && Mathf.Abs(RPM - ShaftRPM()) < 80f; } }
        public string Surface { get; private set; } = "Asphalt";
        public string Notice { get; private set; } = "Select 1st with Shift. W to accelerate.";
        private float omega, throttle, steering, shiftTime, noticeTime;
        private int pendingGear;
        private Vector3 previousVelocity;
        private readonly bool[] grounded = new bool[4];
        private readonly WheelHit[] contacts = new WheelHit[4];

        //
        // @description - Initialize vehicle integration after procedural assembly
        // @return {Void} - Configures mass, center of gravity, and initial idle
        //
        private void Start()
        {
            Body = GetComponent<Rigidbody>();
            Body.mass = spec.curbMass + spec.driverMass;
            Body.centerOfMass = new Vector3(0, 0.48f, 0.24f);
            Body.inertiaTensor = new Vector3(1250, 1450, 410);
            Body.interpolation = RigidbodyInterpolation.Interpolate;
            Body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            Body.linearDamping = 0;
            Body.angularDamping = 0.08f;
            Body.maxAngularVelocity = 15f;
            Body.solverIterations = 12;
            wheels[0].ConfigureVehicleSubsteps(8, 12, 6);
            ResetAt(transform.position, transform.rotation);
        }

        //
        // @description - Consume discrete shift and engine controls on render frames
        // @return {Void} - Updates shift requests and synchronizes wheel rendering
        //
        private void Update()
        {
            if (Time.timeScale == 0 || Body == null) return;
            if (occupied)
            {
                if (controls.Up) RequestShift(1);
                if (controls.Down) RequestShift(-1);
                if (controls.Assist) { clutchAssist = !clutchAssist; SetNotice("Clutch assist " + (clutchAssist ? "ON" : "OFF — hold C / X to shift")); }
                if (controls.Restart && !EngineRunning)
                {
                    if (Gear == 0 || controls.Clutch > 0.8f || clutchAssist) { EngineRunning = true; omega = spec.idleRPM * Mathf.PI / 30f; }
                    else SetNotice("Depress clutch or select neutral to start.");
                }
            }
            for (int i = 0; i < wheels.Length; i++)
            {
                wheels[i].GetWorldPose(out Vector3 p, out Quaternion q);
                wheelVisuals[i].SetPositionAndRotation(p, q);
            }
            if (steeringWheel != null) steeringWheel.localRotation = Quaternion.Euler(22, 0, -steering * 380f);
            if (noticeTime > 0) noticeTime -= Time.deltaTime;
            else Notice = "";
        }

        //
        // @description - Request one manually selected gear with clutch, reverse, and over-rev guards
        // @param {int} direction - Plus one for up, minus one for down
        // @return {Void} - Begins a shift or reports the reason for refusal
        //
        public void RequestShift(int direction)
        {
            if (Shifting) return;
            int target = Gear + direction;
            float wheelRPM = (wheels[0].rpm + wheels[1].rpm) * 0.5f;
            if (!DriveMath.CanSelect(target, Body.linearVelocity.magnitude, wheelRPM, spec) || (target > 0 && ForwardSpeed < -0.5f))
            { SetNotice("Shift blocked: speed, RPM, or gear limit."); return; }
            if (!clutchAssist && controls.Clutch < 0.8f && target != 0)
            { SetNotice("Hold C / X to disengage the clutch first."); return; }
            pendingGear = target;
            shiftTime = 0.24f;
        }

        //
        // @description - Integrate clutch and engine, apply FWD axle torque, tires, brakes, and aerodynamics
        // @return {Void} - Advances the physical simulation by one fixed step
        //
        private void FixedUpdate()
        {
            if (Body == null) return;
            float dt = Time.fixedDeltaTime;
            float inputThrottle = occupied ? controls.Throttle : 0;
            float brake = occupied ? controls.Brake : 0;
            throttle = Mathf.MoveTowards(throttle, inputThrottle, dt * 4f);
            steering = Mathf.MoveTowards(steering, occupied ? controls.Steer : 0, dt * (Mathf.Abs(ForwardSpeed) < 5 ? 2.3f : 1.25f));
            if (Shifting)
            {
                shiftTime -= dt;
                if (shiftTime <= 0) Gear = pendingGear;
            }
            MaxDrivenSlip = 0;
            int groundCount = 0;
            for (int i = 0; i < 4; i++)
            {
                grounded[i] = wheels[i].GetGroundHit(out contacts[i]);
                float grip = 1, rolling = spec.rollingCoefficient;
                if (grounded[i])
                {
                    groundCount++;
                    RoadSurface surface = contacts[i].collider.GetComponent<RoadSurface>();
                    if (surface != null) { grip = surface.grip; rolling = surface.rollingResistance; Surface = surface.label; }
                    if (i < 2) MaxDrivenSlip = Mathf.Max(MaxDrivenSlip, Mathf.Abs(contacts[i].forwardSlip));
                }
                WheelFrictionCurve forward = wheels[i].forwardFriction;
                forward.stiffness = grip;
                wheels[i].forwardFriction = forward;
                WheelFrictionCurve lateral = wheels[i].sidewaysFriction;
                lateral.stiffness = grip;
                wheels[i].sidewaysFriction = lateral;
                float rollingBrake = grounded[i] ? rolling * contacts[i].force * spec.tireRadius : 0;
                wheels[i].brakeTorque = brake * (i < 2 ? 900f : 520f) + rollingBrake;
                if (i >= 2 && (!occupied || controls.Handbrake)) wheels[i].brakeTorque += 1600f;
            }
            if (groundCount == 0) Surface = "Airborne";

            RPM = omega * 30f / Mathf.PI;
            float fuel = Shifting ? 0 : throttle;
            if (RPM > spec.redlineRPM) fuel = 0;
            float netTorque = EngineRunning ? spec.torqueCurve.Evaluate(RPM) * fuel : 0;
            // Supplied curve is net full-load torque: apply engine braking only as throttle closes.
            netTorque -= (1 - fuel) * (12f + RPM * 0.003f);
            if (EngineRunning && RPM < spec.idleRPM + 100 && fuel < 0.2f)
                netTorque += Mathf.Clamp((spec.idleRPM - RPM) * 0.6f + 16f, 0, 80f);
            CrankTorqueNm = netTorque;
            float engagement = Gear == 0 || Shifting || !occupied ? 0 : 1 - controls.Clutch;
            if (clutchAssist && engagement > 0)
            {
                float shaft = ShaftRPM();
                if (shaft < 1500 || RPM - shaft > 350)
                {
                    float targetRPM = Mathf.Lerp(1200, 2600, throttle);
                    float capacity = Mathf.Max(0, netTorque + (RPM - targetRPM) * 0.15f);
                    float slipReduction = 1f / (1f + Mathf.Max(0, MaxDrivenSlip - 0.08f) * 10f);
                    engagement *= Mathf.Clamp01(capacity / spec.clutchCapacityNm) * slipReduction;
                }
                if (brake > 0.2f && shaft < 1200) engagement = 0;
            }
            ClutchEngagement = Mathf.MoveTowards(ClutchEngagement, engagement, dt * (engagement < ClutchEngagement ? 20 : 3));
            if (Shifting || Gear == 0) ClutchEngagement = 0;
            float ratio = spec.Ratio(Gear);
            float shaftOmega = (wheels[0].rpm + wheels[1].rpm) * 0.5f * ratio * Mathf.PI / 30f;
            float transmitted = DriveMath.ClutchTorque(omega, shaftOmega, netTorque, spec.engineInertia, dt, spec.clutchCapacityNm * ClutchEngagement);
            omega = Mathf.Clamp(omega + (netTorque - transmitted) / spec.engineInertia * dt, 0, 10000f * Mathf.PI / 30f);
            RPM = omega * 30f / Mathf.PI;
            if (EngineRunning && RPM < 450) { EngineRunning = false; SetNotice("Engine stalled. I / Y to restart with clutch disengaged."); }
            AxleTorqueNm = transmitted * ratio * (transmitted >= 0 ? spec.drivelineEfficiency : 1f / spec.drivelineEfficiency);
            wheels[0].motorTorque = AxleTorqueNm * 0.5f;
            wheels[1].motorTorque = AxleTorqueNm * 0.5f;
            wheels[2].motorTorque = wheels[3].motorTorque = 0;
            WheelHP = DriveMath.Horsepower(AxleTorqueNm, (wheels[0].rpm + wheels[1].rpm) * 0.5f);

            ApplySteering();
            AntiRoll(0, 1, spec.frontAntiRoll);
            AntiRoll(2, 3, spec.rearAntiRoll);
            Vector3 v = Body.linearVelocity;
            Body.AddForce(-0.5f * 1.225f * spec.dragArea * v.magnitude * v);
            Vector3 acceleration = (v - previousVelocity) / dt;
            LongitudinalG = Mathf.Lerp(LongitudinalG, Vector3.Dot(acceleration, transform.forward) / 9.80665f, 1 - Mathf.Exp(-dt * 8));
            LateralG = Mathf.Lerp(LateralG, Vector3.Dot(acceleration, transform.right) / 9.80665f, 1 - Mathf.Exp(-dt * 8));
            previousVelocity = v;
        }

        //
        // @description - Apply Ackermann steering with a speed-dependent keyboard-friendly steering range
        // @return {Void} - Sets the two front wheel steering angles
        //
        private void ApplySteering()
        {
            float angle = steering * Mathf.Lerp(33, 12, Mathf.Clamp01(Mathf.Abs(ForwardSpeed) / 45f));
            if (Mathf.Abs(angle) < 0.01f) { wheels[0].steerAngle = wheels[1].steerAngle = 0; return; }
            float radius = spec.wheelbase / Mathf.Tan(Mathf.Abs(angle) * Mathf.Deg2Rad);
            float inner = Mathf.Atan(spec.wheelbase / (radius - spec.track / 2)) * Mathf.Rad2Deg;
            float outer = Mathf.Atan(spec.wheelbase / (radius + spec.track / 2)) * Mathf.Rad2Deg;
            wheels[0].steerAngle = angle > 0 ? outer : -inner;
            wheels[1].steerAngle = angle > 0 ? inner : -outer;
        }

        //
        // @description - Transfer suspension load across one axle using an estimated anti-roll bar
        // @param {int} left - Left wheel index
        // @param {int} right - Right wheel index
        // @param {float} rate - Anti-roll force scale in newtons
        // @return {Void} - Applies paired vertical forces where tires are grounded
        //
        private void AntiRoll(int left, int right, float rate)
        {
            float a = grounded[left] ? (-wheels[left].transform.InverseTransformPoint(contacts[left].point).y - spec.tireRadius) / wheels[left].suspensionDistance : 1;
            float b = grounded[right] ? (-wheels[right].transform.InverseTransformPoint(contacts[right].point).y - spec.tireRadius) / wheels[right].suspensionDistance : 1;
            float force = (Mathf.Clamp01(a) - Mathf.Clamp01(b)) * rate;
            if (grounded[left]) Body.AddForceAtPosition(-transform.up * force, wheels[left].transform.position);
            if (grounded[right]) Body.AddForceAtPosition(transform.up * force, wheels[right].transform.position);
        }

        //
        // @description - Read engine-equivalent driven wheel speed
        // @return {float} - Signed input shaft RPM
        //
        private float ShaftRPM()
        {
            return (wheels[0].rpm + wheels[1].rpm) * 0.5f * spec.Ratio(Gear);
        }

        //
        // @description - Reset the complete driveline and rigid body at a known spawn
        // @param {Vector3} position - World-space spawn position
        // @param {Quaternion} rotation - World-space spawn orientation
        // @return {Void} - Returns the car to neutral at idle
        //
        public void ResetAt(Vector3 position, Quaternion rotation)
        {
            if (Body == null) Body = GetComponent<Rigidbody>();
            Body.linearVelocity = Vector3.zero;
            Body.angularVelocity = Vector3.zero;
            Body.position = position;
            Body.rotation = rotation;
            transform.SetPositionAndRotation(position, rotation);
            // Re-enable each wheel to discard stale contact / rotational state after teleporting.
            foreach (WheelCollider w in wheels) { w.enabled = false; w.motorTorque = 0; w.brakeTorque = 0; w.steerAngle = 0; w.enabled = true; }
            Gear = pendingGear = 0;
            shiftTime = throttle = steering = ClutchEngagement = 0;
            AxleTorqueNm = WheelHP = LongitudinalG = LateralG = 0;
            previousVelocity = Vector3.zero;
            omega = spec.idleRPM * Mathf.PI / 30f;
            RPM = spec.idleRPM;
            EngineRunning = true;
            Physics.SyncTransforms();
            Body.WakeUp();
            SetNotice("Neutral · Shift / RB for 1st, Ctrl / LB for reverse.");
        }

        //
        // @description - Display a short vehicle status message
        // @param {string} message - Human-readable status
        // @return {Void} - Updates timed status text
        //
        public void SetNotice(string message)
        {
            Notice = message;
            noticeTime = 4f;
        }
    }
}
