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
        public bool CanStartEngine { get { return occupied && (Gear == 0 || controls.Clutch > 0.8f); } }
        private RockoIgnition ignition;
        public bool Shifting { get { return shiftTime > 0; } }
        public bool ClutchLocked { get { return Gear != 0 && !Shifting && ClutchEngagement > 0.95f && Mathf.Abs(RPM - ShaftRPM()) < 80f; } }
        public string Surface { get; private set; } = "Asphalt";
        public string Notice { get; private set; } = "Select 1st with Shift. W to accelerate.";
        private readonly Vector3[] contactAxes = new Vector3[8];
        private readonly Vector3[] contactArms = new Vector3[8];
        private readonly Vector3[] inverseAngularArms = new Vector3[8];
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
            ignition = GetComponent<CarAccess>()?.ignition;
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
            // Build pedal pressure using the active input response; releasing removes brakes immediately.
            brakePedal = brakeInput < brakePedal ? brakeInput
                : Mathf.MoveTowards(brakePedal, brakeInput, dt * (controls.ControllerBrake ? controls.brakeApplyRate : spec.brakeApplyRate));
            float brake = (float)DrivingFeel.BrakeDemand(brakePedal);
            float releaseTarget = occupied ? 1 - Mathf.Clamp01(controls.Clutch) : 0;
            releasedClutch = releaseTarget < releasedClutch ? releaseTarget
                : Mathf.MoveTowards(releasedClutch, releaseTarget, dt * spec.manualClutchReleaseRate);
            // A parked chassis can sleep; player input must resume wheel simulation.
            if (occupied && (inputThrottle > 0.01f || brake > 0.01f ||
                Mathf.Abs(controls.Steer) > 0.01f || Shifting)) Body.WakeUp();
            float throttleRate = controls.ControllerThrottle
                ? (inputThrottle < throttle ? controls.throttleReleaseRate : controls.throttleApplyRate) : 4f;
            throttle = Mathf.MoveTowards(throttle, inputThrottle, dt * throttleRate);
            steering = controls.ControllerSteering
                ? (float)ControllerResponse.Steering(steering, occupied ? controls.Steer : 0, dt,
                    controls.steeringApplyRate, controls.steeringUnwindRate)
                : Mathf.MoveTowards(steering, occupied ? controls.Steer : 0,
                    dt * (Mathf.Abs(ForwardSpeed) < 5 ? 2.3f : 1.25f));
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
                WheelFrictionCurve sideways = wheels[i].sidewaysFriction;
                sideways.stiffness = 0; // 🚗 Both tire axes now belong to the shared solver.
                wheels[i].sidewaysFriction = sideways;

                if (grounded[i]) rollingForce += rolling * Mathf.Max(0, contacts[i].force);
                // 🚗 Only the brake pedal and handbrake may command wheel brakes.
                drivetrain.BrakeTorque[i] = brake * (i < 2 ? spec.frontBrakeTorque : spec.rearBrakeTorque);
                if (i >= 2 && (!occupied || controls.Handbrake)) drivetrain.BrakeTorque[i] += 1600f;
                // With zero actuation PhysX can pin the chassis even at zero forward stiffness.
                // Keep all native wheels out of that mode; the shared solver owns drive/brakes.
                wheels[i].motorTorque = NativeWheelKeepAliveTorque;
                wheels[i].brakeTorque = 0;
                drivetrain.NormalLoad[i] = grounded[i] ? Mathf.Max(0, contacts[i].force) : 0;
                drivetrain.Grip[i] = grip * spec.tireGripScale;
                drivetrain.WheelInertia[i] = 0.5f * wheels[i].mass * spec.tireRadius * spec.tireRadius;
                tireDirections[i] = grounded[i] ? contacts[i].forwardDir.normalized
                    : transform.TransformDirection(Quaternion.Euler(0, wheels[i].steerAngle, 0) * Vector3.forward);
                Vector3 contact = grounded[i] ? contacts[i].point : wheels[i].transform.position;
                Vector3 right = grounded[i] ? Vector3.Cross(contacts[i].normal, tireDirections[i]).normalized
                    : Vector3.Cross(transform.up, tireDirections[i]).normalized;
                contactAxes[i] = tireDirections[i]; contactAxes[i + 4] = right;
                contactArms[i] = Vector3.Cross(contact - Body.worldCenterOfMass, contactAxes[i]);
                contactArms[i + 4] = Vector3.Cross(contact - Body.worldCenterOfMass, right);
                Vector3 velocity = Body.GetPointVelocity(contact);
                if (grounded[i] && contacts[i].collider.attachedRigidbody != null)
                    velocity -= contacts[i].collider.attachedRigidbody.GetPointVelocity(contact);
                drivetrain.ContactSpeed[i] = Vector3.Dot(velocity, tireDirections[i]);
                drivetrain.LateralContactSpeed[i] = Vector3.Dot(velocity, right);
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
            if (ignition != null && ignition.StarterEngaged)
                netTorque += Mathf.Clamp((300 - RPM) * 0.4f, 0, 45);
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
            PrepareContactResponse();
            drivetrain.LateralStiffness = spec.lateralGripScale;
            drivetrain.Step(dt, netTorque, spec.engineInertia, spec.clutchCapacityNm * ClutchEngagement,
                ratio, spec.drivelineEfficiency, Body.mass, spec.tireRadius);
            omega = (float)drivetrain.EngineOmega;
            ClutchTorqueNm = (float)drivetrain.ClutchTorque;
            AxleTorqueNm = (float)drivetrain.AxleTorque;
            MaxDrivenSlip = Mathf.Max(Mathf.Abs((float)drivetrain.Slip[0]), Mathf.Abs((float)drivetrain.Slip[1]));
            for (int i = 0; i < 4; i++)
            {
                if (grounded[i])
                {
                    Vector3 force = contactAxes[i] * (float)(drivetrain.ForceImpulse[i] / dt)
                        + contactAxes[i + 4] * (float)(drivetrain.LateralImpulse[i] / dt);
                    Body.AddForceAtPosition(force, contacts[i].point);
                }
                wheelAngles[i] = Mathf.Repeat(wheelAngles[i] + WheelRPM(i) * 6f * dt, 360f);
            }
            if (EngineRunning && RPM < 450) { EngineRunning = false; SetNotice("Engine stalled · hold the ignition key with neutral selected or clutch down."); }
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
        // @description - Read the sampled shared friction limit used for the last tire solve
        // @param {int} wheel - Wheel index from zero to three
        // @return {float} - Peak combined contact force in N
        //
        public float TireCapacity(int wheel)
        {
            return (float)(drivetrain.NormalLoad[wheel] * drivetrain.Grip[wheel] * 1.03);
        }

        //
        // @description - Read the applied mean tire force for telemetry and integration checks
        // @param {int} wheel - Wheel index from zero to three
        // @param {bool} lateral - True for sideways force, false for drive/braking force
        // @return {float} - Signed force in N during the last physics step
        //
        public float TireForce(int wheel, bool lateral)
        {
            return (float)((lateral ? drivetrain.LateralImpulse[wheel] : drivetrain.ForceImpulse[wheel]) / Time.fixedDeltaTime);
        }

        //
        // @description - Build the shared contact effective-mass matrix including yaw, pitch and roll
        // @return {Void} - Predicts each contact's response to forces at all four tires
        //
        private void PrepareContactResponse()
        {
            Quaternion principal = Body.rotation * Body.inertiaTensorRotation;
            Quaternion inverse = Quaternion.Inverse(principal);
            Vector3 inertia = Body.inertiaTensor;
            for (int j = 0; j < 8; j++)
            {
                Vector3 arm = inverse * contactArms[j];
                inverseAngularArms[j] = principal * new Vector3(arm.x / Mathf.Max(0.001f, inertia.x),
                    arm.y / Mathf.Max(0.001f, inertia.y), arm.z / Mathf.Max(0.001f, inertia.z));
            }
            for (int i = 0; i < 8; i++)
                for (int j = 0; j < 8; j++)
                    drivetrain.ContactResponse[i * 8 + j] = Vector3.Dot(contactAxes[i], contactAxes[j]) / Body.mass
                        + Vector3.Dot(contactArms[i], inverseAngularArms[j]);
            drivetrain.UseContactResponse = true;
        }

        //
        // @description - Apply Ackermann steering with a speed-dependent keyboard-friendly steering range
        // @return {Void} - Sets the two front wheel steering angles
        //
        private void ApplySteering()
        {
            float angle = steering * (float)DrivingFeel.SteeringLimit(ForwardSpeed, spec.steeringSpeedScale);
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
            GetComponent<CarAccess>()?.ResetClosed();
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
            drivetrain.Reset(0);
            System.Array.Clear(wheelAngles, 0, wheelAngles.Length);
            shiftTime = throttle = steering = brakePedal = releasedClutch = ClutchEngagement = 0;
            AxleTorqueNm = WheelHP = LongitudinalG = LateralG = 0;
            previousVelocity = Vector3.zero;
            omega = 0;
            EngineRunning = false;
            GetComponent<CarAccess>()?.ignition?.ResetIgnition();
            Physics.SyncTransforms();
            Body.WakeUp();
            SetNotice("Engine off · sit down, insert the key, then hold the ignition to crank.");
        }

        //
        // @description - Catch the engine after ignition cranking with a neutral or disengaged-clutch interlock
        // @return {bool} - Whether the engine started
        //
        public bool StartEngine()
        {
            if (!CanStartEngine) return false;
            EngineRunning = true;
            omega = spec.idleRPM * Mathf.PI / 30f;
            drivetrain.EngineOmega = omega;
            return true;
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
