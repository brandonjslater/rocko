# Rocko calibration and implementation notes

## Evidence boundaries

The user's specification sheet supplies the engine's two numerical anchors, mass, external dimensions, wheelbase, drivetrain arrangement, gear count, and approximate top speed. It does **not** provide a full dyno trace, gearbox code, tire size, corner weights, suspension rates, alignment, or measured acceleration times.

The front photograph is labelled 1993 GT II while the mechanical sheet describes a 1985–1989 GTX 16V. Rocko intentionally uses the sheet for mechanics and both photos for broad appearance. No missing measurements have been silently presented as factory values.

## Supplied engine anchors

Power in watts is torque in Nm multiplied by angular speed in rad/s:

`P = T × RPM × π / 30`

Therefore, 102 kW at 6,100 rpm requires **159.677 Nm** at that point. The other supplied anchor is **165 Nm at 4,600 rpm**. Both are represented explicitly. Piecewise linear tangents avoid overshooting the supplied peak torque.

| RPM | Net crank torque, Nm | Provenance |
|---|---:|---|
| 900 | 72 | Estimate |
| 1,500 | 105 | Estimate |
| 2,500 | 132 | Estimate |
| 3,500 | 151 | Estimate |
| 4,600 | 165 | Supplied |
| 5,500 | 163 | Estimate |
| 6,100 | 159.677 | Derived from supplied 102 kW |
| 6,500 | 142 | Estimate |
| 6,800 | 123 | Estimate; fuel cut threshold |

Torque is a net full-load curve. Closed-throttle drag is blended in as throttle closes so full-load friction is not subtracted twice. A low-speed idle governor operates around 900 rpm. The model stalls below 450 rpm and cuts positive fueling above 6,800 rpm. An emergency numerical clamp at 10,000 rpm prevents unbounded angular state; it is not an engine durability model.

## Provisional chassis and drivetrain

| Setting | Initial value | Status |
|---|---:|---|
| Driver allowance | 75 kg | Fixed test ballast |
| Total simulated mass | 1,025 kg | Curb mass plus allowance |
| Gear ratios 1–5 | 3.455, 2.118, 1.444, 1.129, 0.912 | Assumed; verify gearbox code |
| Reverse ratio | 3.455 | Assumed |
| Final drive | 3.667 | Assumed |
| Tire radius | 0.288 m | Assumed |
| Track width | 1.39 m | Assumed |
| Drivetrain forward efficiency | 88% | Assumed, constant |
| Engine inertia | 0.28 kg m² | Assumed |
| Clutch capacity | 260 Nm | Assumed |
| Drag area, Cd × A | 0.62 m² | Assumed |
| Air density | 1.225 kg/m³ | Standard reference assumption |
| Asphalt rolling resistance | 0.013 | Assumed |
| Front / rear spring | 26 / 21 kN/m per wheel | Assumed |
| Damping | 2,700 N s/m | Assumed |
| Suspension travel | 0.22 m | Assumed |
| Anti-roll front / rear | 4,500 / 2,500 N scale | Simplified estimated model |
| Center of mass | (0, 0.48, +0.24) m in car frame | Assumed; roughly 60% static front load |
| Rigid-body inertia tensor | (1,250, 1,450, 410) kg m² | Estimated; not measured |
| Service brake front / rear | 900 / 520 Nm per wheel at full input | Assumed |
| Rear parking brake | 1,600 Nm per wheel | Assumed |

Reference coordinates: X right, Y up, Z forward. Front axle Z = +1.2 m, rear axle Z = −1.2 m. Unity's units are treated as SI. The photographic body width is nominal and excludes mirrors.

## Powertrain sequence

1. Sample input in Update; integrate physics at a fixed 0.01 s step.
2. Rate-limit throttle and steering; process an explicitly requested 0.24 s gear change.
3. Read tire contact, wheel speed, and front slip from the real scene's WheelColliders.
4. Evaluate engine torque and closed-throttle braking.
5. Choose clutch engagement from the pedal/button or assisted launch logic. Assistance targets roughly 1,200–2,600 rpm while launching and reduces engagement with excess front slip.
6. Compute capacity-limited clutch torque from engine/input-shaft speed difference. Integrate engine angular speed using its inertia.
7. Multiply transmitted torque by gear ratio, final drive, and direction-dependent drivetrain losses. Split it equally across the front wheels. Rear wheels receive no drive torque.
8. Apply braking, per-contact rolling resistance, anti-roll forces, and quadratic aerodynamic drag to the rigid body.
9. Read telemetry from these same physical states.

The simple equal-torque split approximates one consequence of an open differential, but it does not model a differential carrier and spider gears dynamically. A spinning wheel and transient coupling can therefore differ from a real open differential. The clutch solves against sampled input-shaft speed, not a full simultaneous driveline constraint; oscillation and launch behavior must be checked in Unity before treating any performance as calibrated.

WheelCollider substeps improve the underlying suspension/tire integration; they do not substep the custom engine code. The physics rate must remain stable for a useful calibration comparison. Unity documents WheelCollider as a slip-based vehicle collider and provides vehicle substepping through `ConfigureVehicleSubsteps`.

## Surfaces

| Surface | Tire grip multiplier | Rolling coefficient |
|---|---:|---:|
| Asphalt | 1.00 | 0.013 |
| Dirt / shoulder | 0.60 | 0.035 |
| Grass | 0.52 | 0.040 |

Friction curves are assigned on each wheel from the actual ground collider. A road's ordinary physics material alone would not implement this WheelCollider tire behavior. The model does not yet use a combined-slip friction ellipse or measured tire curves. Do not infer real wet-weather grip from these settings.

## Tuning order

1. Check scale, wheel contact, rest height, axle locations, and weight before performance.
2. Verify supplied torque/power anchors and neutral/clutch behavior. Keep full-load power unchanged unless source evidence changes.
3. Measure tire radius and gearbox/final-drive ratios. RPM versus steady speed should then follow from geometry.
4. Test clutch engagement, wheelspin, low-speed maneuvering, reverse, and starts on both surfaces.
5. Tune suspension and damping using a single repeatable bump and the hill route.
6. Test the 40 m skid circle and slalom for understeer, transient yaw, and brake stability.
7. Record repeated acceleration and coastdown passes, both directions when possible. Use measured loss/drag evidence before changing them.
8. Compare sustained top speed with the supplied approximate 210 km/h target. There is no hard-coded speed cap or direct velocity acceleration boost.

A single acceleration time cannot identify all unknown parameters. The source provides no 0–60 or quarter-mile targets, so none are asserted as acceptance criteria. The mile strip may be too short to settle completely at aerodynamic terminal speed; report the observed maximum separately from an actual steady top-speed measurement.

## Architecture

| File | Responsibility |
|---|---|
| VehicleSpec | Editable engine/chassis baseline |
| DriveMath | Unit conversions, clutch torque limit, shift guards |
| RockoVehicle | Authoritative engine, drivetrain, and chassis controller |
| RockoInput | Keyboard/mouse and standardized gamepad sampling |
| FirstPersonDriver | Walking, seated view, interaction, safe exit, reset, pause |
| CarAssembly / Geometry | Replaceable procedural body and cockpit visuals |
| ProvingGround / RoadSurface | Sandbox geometry and tire contact metadata |
| TestTelemetry | Run measurements and local CSV capture |
| RockoHUD | Testing display and control reference |
| EngineAudio | Synthesized audio placeholder |
| RockoBootstrap | Runtime scene assembly |
| RockoTools | Scene opening, tune creation, Windows build menu |

Future survival/building systems should change validated component specifications or vehicle state through explicit interfaces. They should not introduce a second movement model or bypass the physical powertrain.

## Primary API references consulted

- [Unity 6 vehicle/WheelCollider tutorial](https://docs.unity3d.com/6000.0/Documentation/Manual/WheelColliderTutorial.html)
- [ConfigureVehicleSubsteps](https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/wheelcollider/configurevehiclesubsteps)
- [Unity 6 Rigidbody.linearVelocity](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Rigidbody-linearVelocity.html)
- [Input System 1.11 gamepad controls](https://docs.unity3d.com/Packages/com.unity.inputsystem@1.11/manual/Gamepad.html)
- [Unity Test Framework](https://docs.unity3d.com/Packages/com.unity.test-framework@1.4/manual/index.html)

These references support API choices. They do not validate Rocko's particular implementation or driving fidelity.
