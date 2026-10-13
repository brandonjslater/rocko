using UnityEngine;

namespace Rocko
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class RockoVehicle : MonoBehaviour
    {
        // 🚗 Nonzero actuation suppresses PhysX's separate low-speed sticky constraints.
        // Forward friction stays zero, so this does not supply chassis traction.
        public const float NativeWheelKeepAliveTorque = 0.001f;
        public VehicleSpec spec;
        public RockoInput controls;
        public WheelCollider[] wheels;
        public Transform[] wheelVisuals;
        public Transform seat, steeringWheel;
        public bool occupied, clutchAssist = true;
        public Rigidbody Body { get; private set; }
        public int Gear { get; private set; }
        public float RPM { get { return omega * 30f / Mathf.PI; } }
        public float WheelHP { get; private set; }
        public float AxleTorqueNm { get; private set; }
        public float CrankTorqueNm { get; private set; }
        public float LongitudinalG { get; private set; }
        public float LateralG { get; private set; }
        public float ClutchEngagement { get; private set; }
        public float MaxDrivenSlip { get; private set; }
        public int GroundedWheels { get; private set; }
        public float SampledEngineRPM { get; private set; }
        public float SampledShaftRPM { get; private set; }
        public float ReflectedInertia { get; private set; }
        public float ClutchTorqueNm { get; private set; }
        public float SpeedMPH { get { return Body.linearVelocity.magnitude * 2.2369363f; } }
        public float ForwardSpeed { get { return Vector3.Dot(Body.linearVelocity, transform.forward); } }
        public bool EngineRunning { get; private set; }
        public bool Shifting { get { return shiftTime > 0; } }
        public bool ClutchLocked { get { return Gear != 0 && !Shifting && ClutchEngagement > 0.95f && Mathf.Abs(RPM - ShaftRPM()) < 80f; } }
        public string Surface { get; private set; } = "Asphalt";
        public string Notice { get; private set; } = "Select 1st with Shift. W to accelerate.";
        private float omega, throttle, steering, shiftTime, noticeTime, brakePedal, releasedClutch;
        private int pendingGear;
        private bool assistingLaunch = true;
        private readonly LongitudinalDrivetrain drivetrain = new LongitudinalDrivetrain();
        private readonly Vector3[] tireDirections = new Vector3[4];
        private readonly float[] wheelAngles = new float[4];
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
            // Recalculate all four suspension loads after the final chassis setup.
            wheels[0].ResetSprungMasses();
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
                if (controls.Assist) { clutchAssist = !clutchAssist; SetNotice("Clutch assist " + (clutchAssist ? "ON" : "OFF — hold C / L1 to shift")); }
                if (controls.Restart && !EngineRunning)
                {
                    if (Gear == 0 || controls.Clutch > 0.8f || clutchAssist) { EngineRunning = true; omega = spec.idleRPM * Mathf.PI / 30f; drivetrain.EngineOmega = omega; }
                    else SetNotice("Depress clutch or select neutral to start.");
                }
            }
            for (int i = 0; i < wheels.Length; i++)
            {
                wheels[i].GetWorldPose(out Vector3 p, out Quaternion q);
                wheelVisuals[i].SetPositionAndRotation(p, transform.rotation * Quaternion.Euler(0, wheels[i].steerAngle, 0) * Quaternion.Euler(wheelAngles[i], 0, 0));
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
            float wheelRPM = (WheelRPM(0) + WheelRPM(1)) * 0.5f;
            if (!DriveMath.CanSelect(target, Body.linearVelocity.magnitude, wheelRPM, spec) || (target > 0 && ForwardSpeed < -0.5f))
            { SetNotice("Shift blocked: speed, RPM, or gear limit."); return; }
            if (!clutchAssist && controls.Clutch < 0.8f && target != 0)
            { SetNotice("Hold C / L1 to disengage the clutch first."); return; }
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
            float brakeInput = occupied ? Mathf.Clamp01(controls.Brake) : 0;
            // 🚗 Build pedal pressure over 0.25 seconds; releasing the pedal removes brakes immediately.
            brakePedal = brakeInput < brakePedal ? brakeInput
                : Mathf.MoveTowards(brakePedal, brakeInput, dt * spec.brakeApplyRate);
            float brake = (float)DrivingFeel.BrakeDemand(brakePedal);
            float releaseTarget = occupied ? 1 - Mathf.Clamp01(controls.Clutch) : 0;
            releasedClutch = releaseTarget < releasedClutch ? releaseTarget
                : Mathf.MoveTowards(releasedClutch, releaseTarget, dt * spec.manualClutchReleaseRate);
            // A parked chassis can sleep; player input must resume wheel simulation.
            if (occupied && (inputThrottle > 0.01f || brake > 0.01f ||
                Mathf.Abs(controls.Steer) > 0.01f || Shifting)) Body.WakeUp();
            throttle = Mathf.MoveTowards(throttle, inputThrottle, dt * 4f);
            steering = Mathf.MoveTowards(steering, occupied ? controls.Steer : 0, dt * (Mathf.Abs(ForwardSpeed) < 5 ? 2.3f : 1.25f));
            if (Shifting)
            {
                shiftTime -= dt;
                if (shiftTime <= 0) Gear = pendingGear;
            }
            ApplySteering();
            int groundCount = 0;
            float rollingForce = 0;
            for (int i = 0; i < 4; i++)
            {
                grounded[i] = wheels[i].GetGroundHit(out contacts[i]);
                float grip = 1, rolling = spec.rollingCoefficient;
                if (grounded[i])
                {
                    groundCount++;
                    RoadSurface surface = contacts[i].collider.GetComponent<RoadSurface>();
                    if (surface != null) { grip = surface.grip; rolling = surface.rollingResistance; Surface = surface.label; }
                }
                WheelFrictionCurve forward = wheels[i].forwardFriction;
                forward.stiffness = 0; // Longitudinal forces have exactly one owner: drivetrain. 🚗
                wheels[i].forwardFriction = forward;

                if (grounded[i]) rollingForce += rolling * Mathf.Max(0, contacts[i].force);
                // 🚗 Only the brake pedal and handbrake may command wheel brakes.
                drivetrain.BrakeTorque[i] = brake * (i < 2 ? spec.frontBrakeTorque : spec.rearBrakeTorque);
                if (i >= 2 && (!occupied || controls.Handbrake)) drivetrain.BrakeTorque[i] += 1600f;
                // With zero actuation PhysX can pin the chassis even at zero forward stiffness.
                // Keep all native wheels out of that mode; the shared solver owns drive/brakes.
                wheels[i].motorTorque = NativeWheelKeepAliveTorque;
                wheels[i].brakeTorque = 0;
                drivetrain.NormalLoad[i] = grounded[i] ? Mathf.Max(0, contacts[i].force) : 0;
                drivetrain.Grip[i] = grip;
                drivetrain.WheelInertia[i] = 0.5f * wheels[i].mass * spec.tireRadius * spec.tireRadius;
                tireDirections[i] = grounded[i] ? contacts[i].forwardDir.normalized
                    : transform.TransformDirection(Quaternion.Euler(0, wheels[i].steerAngle, 0) * Vector3.forward);
                Vector3 contact = grounded[i] ? contacts[i].point : wheels[i].transform.position;
                drivetrain.ContactSpeed[i] = Vector3.Dot(Body.GetPointVelocity(contact), tireDirections[i]);
            }
            if (groundCount == 0) Surface = "Airborne";
            GroundedWheels = groundCount;
            // Rolling resistance opposes existing motion and cannot hold a launch.
            float resistance = DriveMath.RollingResistance(ForwardSpeed, rollingForce, Body.mass, dt);
            Body.AddForce(transform.forward * resistance);

            float fuel = Shifting ? 0 : throttle;
            if (RPM > spec.redlineRPM) fuel = 0;
            float netTorque = EngineRunning ? spec.torqueCurve.Evaluate(RPM) * fuel : 0;
            // Supplied curve is net full-load torque: apply engine braking only as throttle closes.
            netTorque -= (1 - fuel) * (12f + RPM * 0.003f);
            if (EngineRunning && RPM < spec.idleRPM + 100 && fuel < 0.2f)
                netTorque += Mathf.Clamp((spec.idleRPM - RPM) * 0.6f + 16f, 0, 80f);
            CrankTorqueNm = netTorque;
            float engagement = Gear == 0 || Shifting || !occupied ? 0
                : clutchAssist ? 1 - controls.Clutch : (float)DrivingFeel.ClutchBite(releasedClutch);
            if (clutchAssist && engagement > 0)
            {
                float shaft = ShaftRPM();
                float roadRPM = Mathf.Abs(ForwardSpeed * spec.Ratio(Gear)) * 30f / (Mathf.PI * spec.tireRadius);
                // 🚗 Use road speed so spinning tires cannot prematurely finish the launch.
                if (roadRPM < spec.idleRPM + 100 && shaft < spec.idleRPM + 100) assistingLaunch = true;
                else if (roadRPM >= spec.idleRPM + 900) assistingLaunch = false;
                if (assistingLaunch)
                    engagement *= DriveMath.LaunchClutchEngagement(RPM, roadRPM, netTorque, throttle,
                        MaxDrivenSlip, spec.clutchCapacityNm, spec.idleRPM);
                if (brake > 0.2f && shaft < 1200) engagement = 0;
            }
            ClutchEngagement = Mathf.MoveTowards(ClutchEngagement, engagement, dt * (engagement < ClutchEngagement ? 20 : 3));
            if (Shifting || Gear == 0) ClutchEngagement = 0;
            float ratio = spec.Ratio(Gear);
            float shaftOmega = (WheelRPM(0) + WheelRPM(1)) * 0.5f * ratio * Mathf.PI / 30f;
            SampledEngineRPM = omega * 30f / Mathf.PI;
            SampledShaftRPM = shaftOmega * 30f / Mathf.PI;
            ReflectedInertia = ReflectedLoadInertia(ratio);
            for (int i = 0; i < 4; i++)
                for (int j = 0; j < 4; j++)
                    drivetrain.DirectionDot[i * 4 + j] = Vector3.Dot(tireDirections[i], tireDirections[j]);
            drivetrain.Step(dt, netTorque, spec.engineInertia, spec.clutchCapacityNm * ClutchEngagement,
                ratio, spec.drivelineEfficiency, Body.mass, spec.tireRadius);
            omega = (float)drivetrain.EngineOmega;
            ClutchTorqueNm = (float)drivetrain.ClutchTorque;
            AxleTorqueNm = (float)drivetrain.AxleTorque;
            MaxDrivenSlip = Mathf.Max(Mathf.Abs((float)drivetrain.Slip[0]), Mathf.Abs((float)drivetrain.Slip[1]));
            for (int i = 0; i < 4; i++)
            {
                // Bounded grip blending avoids abruptly removing native lateral authority.
                // This is an approximation across the separate PhysX lateral step, not a full combined tire solver.
                WheelFrictionCurve lateral = wheels[i].sidewaysFriction;
                double peakForce = drivetrain.NormalLoad[i] * drivetrain.Grip[i] * 1.03;
                lateral.stiffness = (float)(drivetrain.Grip[i] * DrivingFeel.LateralGrip(
                    drivetrain.ForceImpulse[i] / dt, peakForce));
                wheels[i].sidewaysFriction = lateral;
                if (grounded[i]) Body.AddForceAtPosition(tireDirections[i] * (float)(drivetrain.ForceImpulse[i] / dt), contacts[i].point);
                wheelAngles[i] = Mathf.Repeat(wheelAngles[i] + WheelRPM(i) * 6f * dt, 360f);
            }
            if (EngineRunning && RPM < 450) { EngineRunning = false; SetNotice("Engine stalled. I / Triangle to restart with clutch disengaged."); }
            WheelHP = DriveMath.Horsepower(AxleTorqueNm, (WheelRPM(0) + WheelRPM(1)) * 0.5f);

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
            return (WheelRPM(0) + WheelRPM(1)) * 0.5f * spec.Ratio(Gear);
        }

        //
        // @description - Read wheel speed from the shared longitudinal solver
        // @param {int} index - Wheel index: front left, front right, rear left, rear right
        // @return {float} - Signed wheel revolutions per minute
        //
        public float WheelRPM(int index)
        {
            return (float)(drivetrain.WheelOmega[index] * 30 / System.Math.PI);
        }

        //
        // @description - Read the actual brake command consumed by the shared wheel solver
        // @param {int} index - Wheel index from zero through three
        // @return {float} - Brake torque magnitude in Nm
        //
        public float WheelBrakeTorque(int index)
        {
            return (float)drivetrain.BrakeTorque[index];
        }

        //
        // @description - Report driven-wheel inertia reflected through the selected gear for diagnostics
        // @param {float} ratio - Signed overall gear ratio
        // @return {float} - Positive input-shaft inertia in kg m²
        //
        private float ReflectedLoadInertia(float ratio)
        {
            float radiusSquared = spec.tireRadius * spec.tireRadius;
            float wheelInertia = 0.5f * (wheels[0].mass + wheels[1].mass) * radiusSquared;
            return wheelInertia / Mathf.Max(0.01f, ratio * ratio * spec.drivelineEfficiency);
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
            assistingLaunch = true;
            drivetrain.Reset(spec.idleRPM);
            System.Array.Clear(wheelAngles, 0, wheelAngles.Length);
            shiftTime = throttle = steering = brakePedal = releasedClutch = ClutchEngagement = 0;
            AxleTorqueNm = WheelHP = LongitudinalG = LateralG = 0;
            previousVelocity = Vector3.zero;
            omega = spec.idleRPM * Mathf.PI / 30f;
            EngineRunning = true;
            Physics.SyncTransforms();
            Body.WakeUp();
            SetNotice("Neutral · Shift / Circle for 1st, Ctrl / Square for reverse.");
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
