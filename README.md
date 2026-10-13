# 🚗 Rocko — Unity driving prototype 0.1

### Stronger handling and gearing tune

This iteration makes a deliberate, larger change after the first grip correction still felt too loose:

- Tire force scale increases 12%, with a further 8% lateral stiffness adjustment. Longitudinal-force blending retains at least 90% of that lateral stiffness. Surface differences remain: dirt still has less grip than asphalt.
- Steering travel decreases more strongly with speed: 33 degrees at rest, roughly 9.8 degrees at 20 m/s, and 5.2 degrees at 30 m/s. This is input sensitivity shaping for keyboard/gamepad, not yaw correction. Low-speed parking travel is retained.
- Service brakes use 1000 Nm front / 320 Nm rear per wheel, about 76% nominal front bias. Total commanded torque is 7% lower than the previous tune, reducing rear locking tendency. Brake pressure still builds progressively and releases immediately. There is no hidden ABS; wheels can still lock.
- Ratios are **3.455 / 1.950 / 1.280 / 0.950 / 0.720**, with first gear and the 3.667 final drive unchanged. At a constant road speed, synchronized upshifts from 6800 RPM now target approximately **3838 / 4464 / 5047 / 5154 RPM**. Taller upper gears reduce wheel torque; this is not an engine power increase or a guaranteed top-speed increase.

These remain provisional gameplay calibration values, not verified factory tire or gearbox specifications. The split tire solver is approximate; passing automated checks does not establish realistic handling. C# checks exercise launches, lock, shifts and braking with estimated forward load transfer, plus steering-response and grip bounds. Unity corner behavior still requires Editor driving verification.

The generated baseline picks up these defaults. If you assigned a custom VehicleSpec asset, update its ratios, brake torques, tireGripScale (1.12), lateralGripScale (1.08), and steeringSpeedScale (13) in the Inspector. Existing tune assets are preserved. Run all PlayMode tests, then compare the same Laguna bend and braking point.

### Laguna Seca reference circuit

Press **F4** in the Game view, or open **Esc / Options** and click **DRIVE LAGUNA SECA**. The existing Rocko is placed at start/finish in neutral, facing the driving direction. F4 restarts there; **R / Create** returns to the garage and **F2 / D-pad up** returns to the measured straight. Controller driving bindings are unchanged. The circuit is generated once on first entry and reused on resets.

This is a driveable **approximation**, not a laser-scanned or surveyed replica. Its closed centerline is scaled to **2.238 miles / 3601.712 m** and traced from the official racing-lines flyer. It includes the Andretti Hairpin, uphill section, Corkscrew, Rainey Curve, and final corner, with an estimated 54.86 m elevation range. The 11 m road width, detailed elevations, corner radii, runoff and surrounding hills are provisional. There is no calibrated camber, authentic pit complex, lap timer or track-limit enforcement. Do not compare its lap times to real-world records.

The road is a continuous mesh with collision, edge markings, corner boards, painted curbs, dry runoff and surrounding terrain. F5 records the existing vehicle telemetry; standing-start timing is disabled away from the proving-ground strip. The HUD replaces strip results with circuit information.

References: [official track information](https://weathertechraceway.com/pages/track-information) and [official racing-lines flyer](https://cdn.shopify.com/s/files/1/0678/1914/3354/files/WeatherTech_Raceway_Laguna_Seca_Racing_Lines_Flyer.pdf?v=1733112124). The flyer was used as a layout reference; its artwork is not included in the project. Generated geometry is original, with no official affiliation.

Validation: numerical centerline checks for lap length, finite samples, spacing, elevation and plan shape were run outside Unity. Run **LagunaCircuitHasContinuousRoadAndLaunches** in PlayMode for road collision, spawn, launch, reset reuse and garage return. Unity execution and a full driving lap still require Editor verification.

### Driving-feel tuning branch

This pass preserves engine power, gearing, the shared drivetrain, and the native wheel keep-alive fix. Full brake torque is unchanged, but pedal pressure builds over 0.25 seconds with a progressive response curve. Release is immediate. Manual clutch release now moves through a smooth bite curve; depressing the clutch still removes the requested capacity immediately, subject to the existing fast pressure decay. Assist retains its tested launch controller.

Cornering grip now decreases moderately as each tire uses longitudinal grip under acceleration or braking. This is a bounded stability approximation: PhysX still computes lateral force and suspension separately. It needs road testing for understeer, braking in corners, and recovery after lifting off. It is not yet a calibrated combined-slip tire model.

Run all PlayMode tests, including `BrakePressureBuildsAndReleases`, then compare a gentle launch, a full-throttle launch, partial brake input on a controller, and the same bend coasting versus accelerating. Settings are provisional; brake rise rate, manual clutch release rate, and axle brake torques are exposed on the runtime vehicle specification. No measured stopping-distance or handling claims are made by the standalone checks.

### Experimental shared drivetrain solver

The October 11 traces exposed feedback oscillation between independently integrated engine and WheelCollider speeds. Engine, clutch, wheel rotation, braking, and longitudinal tire forces now advance together in `LongitudinalDrivetrain.cs`. Clutch impulses are limited by friction capacity and act on both rotating sides in the same substep. The failed static-coupling controller and estimated chassis inertia correction have been removed.

WheelColliders retain suspension and lateral friction. Their brake torque and longitudinal friction are disabled to avoid applying drive forces twice. Each native wheel receives a 0.001 Nm keep-alive motor command to suppress PhysX's separate low-speed sticky constraints. With zero forward friction this command is not used for chassis traction; the shared solver still owns drive and braking. This adapter workaround is based on PhysX's nonzero-actuation check and needs verification in Unity's embedded implementation. `SettledNeutralChassisRespondsToExternalForce` checks neutral drift and whether an external force can move the car after two seconds at rest. Wheel visuals, HUD, and telemetry use the shared solver's wheel speeds. Combined cornering grip and suspension interaction still require Unity testing; the inner solver predicts chassis translation, while Unity applies contact forces and their rotational effects once per outer physics step.

The actual C# solver compiled and passed headless assisted/manual launch and sustained-lock checks at 5, 10, and 20 ms steps, plus reverse, reduced grip, braking, shifting, open-clutch, neutral, airborne, and recovery checks seeded from the two failing traces. Run these independently with `.NET 8`:

```
dotnet run --project Tools/DrivetrainChecks/DrivetrainChecks.csproj
```

The headless fixture uses flat-road loads and does not run Unity. Run all EditMode and PlayMode tests in the Editor, especially both `ClutchHoldsLockDuringCruise` tests and `RealSceneSpawnsOutsideAndLaunchesWithFiniteState`. The cruise tests retain the 80 RPM limit for two continuous seconds; their automatic CSV traces remain enabled.

CSV `solver_version` now identifies `shared_longitudinal_v1`. Engine `rpm` and all wheel RPM columns are sampled after the same shared solve; `engine_rpm_before_solve` and `shaft_rpm_before_solve` are sampled together before it. Chassis speed is sampled before the upcoming Unity physics step. `reflected_inertia_kgm2` now reports driven-wheel rotational inertia only and is diagnostic, not a solver input. Motor columns report shared-solver axle torque divided between the driven wheels.

A first-person Scirocco II sandbox focused on developing the driving model before adding car building or survival systems.

**Delivery status:** Unity project source, not a compiled game. C# syntax and project wiring were checked here. Unity is not installed in the authoring environment, so compilation, shaders, controller hardware, driving performance, and visual appearance have **not** been verified in the Unity Editor. The included Unity tests still need to be run.

## Open and drive

1. Extract this archive into a normal writable folder, such as `C:\Projects\Rocko`.
2. In **Unity Hub → Projects → Add**, select the **Rocko** directory containing `Assets`, `Packages`, and `ProjectSettings`.
3. Open with **Unity 6.0 / 6000.0**. The project version file uses `6000.0.23f1` as its API baseline; you can select your installed 6000.0 patch in Hub. Allow package restoration and any Input System restart prompt.
4. Open **Assets → Rocko → Scenes → Rocko**, or use **Rocko → Open Prototype Scene**.
5. Press **Play**, then click the Game view. The world and car are generated at runtime, so the scene is intentionally almost empty before Play.
6. You start on foot in the garage, beside the driver's door. Press **E**, then **Shift** for first gear. Use **W** to accelerate, **S** to brake, and **A/D** to steer. Look around with the mouse.

The project uses Unity's **Built-in Render Pipeline**. Do not convert it to URP/HDRP without replacing its two surface shaders. No Asset Store purchase or external vehicle model is required.

## Controls

Controller prompts default to DualSense button names. The underlying gamepad bindings are unchanged; other compatible controllers still work.

| Action | Keyboard / mouse | DualSense |
|---|---|---|
| Walk | WASD | Left stick |
| Look | Mouse | Right stick |
| Enter / exit car | E | R1 |
| Throttle | W | R2 |
| Service brake | S | L2 |
| Steer | A / D | Left stick horizontal |
| Gear up | Shift | Circle |
| Gear down | Ctrl | Square |
| Disengage clutch | Hold C | Hold L1 |
| Handbrake | Hold Space | Hold Cross |
| Restart stalled engine | I | Triangle |
| Reset outside car at garage | R | Create |
| Stage straight test, seated | F2 | D-pad up |
| Toggle clutch assistance | F3 | D-pad left |
| Start / stop CSV recording | F5 | D-pad right |
| Toggle help | F1 | D-pad down |
| Center view | V | R3 (press right stick) |
| Pause / release mouse | Esc | Options |

**Manual transmission only:** the sequence is **R → N → 1 → 2 → 3 → 4 → 5**. There are no automatic gear changes. From neutral, Ctrl selects reverse. W is the accelerator in reverse as well; S always brakes.

**Clutch assistance starts enabled.** It operates the clutch during launch and your requested shifts. For full clutch operation, press F3, hold C to select a gear, apply throttle, then release C. The digital keyboard clutch is smoothed but is still a binary input; controller L1 is also a button, not an analog pedal. The engine can stall. Over-rev downshifts and moving reverse selection are blocked as prototype usability guards.

## Included

- One gameplay camera: on-foot first person and seated first person. No chase camera.
- Driver-side proximity entry; stationary exit checks for ground and capsule clearance on either side.
- Runtime Scirocco-inspired blue body, wheel arches, rectangular lights, black trim, alloy spokes, sloping hatch, spoiler, and provisional left-hand-drive interior.
- FWD, a five-speed manual, separate engine speed, clutch capacity/slip, engine braking, idle control, rev limiter, and stalling.
- Four WheelColliders with suspension, damping, estimated anti-roll bars, Ackermann steering, drag, rolling resistance, and surface-dependent tire friction.
- Every requested HUD channel, plus lateral G, clutch status, tire slip in CSV, standing-start splits, and local telemetry recording.
- A mile straight, eighth-mile markers, 520 m braking runoff, flat skidpad/slalom, asphalt corners, two hill crests, dirt loop, and starter garage.
- A synthesized engine sound placeholder. It is not a recording of an EA827.

## Specification baseline

The supplied `Car-specs.txt` is the mechanical authority. The supplied photographs guide the appearance; their GT II/GTX trim differences are not treated as mechanical specifications.

| Item | Supplied value / treatment |
|---|---|
| Engine | 1.8 L EA827 16V inline four |
| Drive / gearbox | Front-wheel drive / five-speed manual |
| Peak crank torque | 165 Nm at 4,600 rpm |
| Rated crank power | 102 kW at 6,100 rpm |
| Curb mass | 950 kg |
| Wheelbase | 2.400 m |
| Body length × width × height | 4.050 × 1.625 × 1.280 m |
| Stated top speed | Approximately 210 km/h / 131 mph, a validation target, not a speed cap |

102 kW is about **136.8 mechanical hp**, or roughly **139 PS**. It is **crank** power. The provisional 88% drivetrain efficiency implies about **120.4 wheel hp** at the engine's rated power point with a locked clutch. This is an estimate, not a measured dyno result.

The simulation currently uses **1,025 kg operating mass**, including a fixed 75 kg driver allowance even while the player is outside. The 950 kg curb specification remains separate in the tune.

**Unverified settings:** gearbox ratios, final drive, tire size, redline, torque-curve intermediate points, clutch capacity/inertia, suspension, center of mass, aerodynamic drag, and grip. See [the calibration notes](Documentation/PHYSICS.md). The body dimensions are a fixed modeling baseline; changing dimension fields alone does not regenerate a differently shaped car.

## Test grounds

![Proving-ground layout](Documentation/Proving-ground-map.png)

The start stripe is at world **Z = 0**; the end-of-mile stripe is at **Z = 1609.344 m**. Each eighth-mile interval is exactly **201.168 m / 660 ft**. The road is flat, 16 m wide, and continues another 520 m for braking. F2 places the car eight meters before the stripe, facing forward.

Timing uses a **standing start from the F2 staging position**, not a rolling crossing of the painted start stripe. Distances are measured from that staging origin, so timing thresholds fall eight meters before the corresponding physical track markers. No rollout correction is applied. Leaving the straight invalidates the run. The displayed quarter-mile finish speed is instantaneous speed at the distance threshold, not a drag-strip averaged trap speed.

## HUD definitions

| Channel | Meaning |
|---|---|
| Gear | Selected R, N, or 1–5; changes after the shift interval |
| Speed / mph | Magnitude of rigid-body world velocity |
| Engine / RPM | Simulated crank rotational speed; not simply speed multiplied by gear ratio |
| Acceleration / G | Smoothed kinematic acceleration along the car's forward axis; negative under deceleration |
| Wheel output / hp | Signed power delivered to the driven axle: axle torque × mean front-wheel angular velocity |
| Axle torque / lb-ft | Total signed torque delivered to both front wheels after gearing and modeled losses |
| Crank torque / lb-ft | Net engine torque before clutch/transmission, displayed beneath axle torque |

Axle torque is much larger than crank torque in low gears. Torque can be present at zero road speed while wheel power is zero, or while spinning tires produce little vehicle acceleration. Wheel output is **shaft power**, not contact-patch useful power; it can be nonzero when airborne. Negative power indicates engine braking. Reverse axle torque is negative while reverse propulsion power is positive. Longitudinal G includes the kinematic effect of gravity on a slope; it is not a gravity-compensated accelerometer model.

## Save a drive

Press **F5** to begin and again to finish. Captures are written at 20 Hz to `Application.persistentDataPath/Telemetry`.

For this project's default company/product names on Windows, the folder is normally:

`%USERPROFILE%\AppData\LocalLow\RockoPrototype\Rocko\Telemetry`

The exact file path is printed in Unity's Console. The CSV includes time, position, mph, gear, RPM, longitudinal/lateral G, wheel hp, axle/crank lb-ft, clutch engagement, front slip, surface, and engine state. Recording continues across resets; use position jumps to identify them or start a separate file per pass.

## Tune and verify

1. Select **Rocko → Create Editable Baseline Tune** outside Play mode. This creates a new specification asset and assigns it to the bootstrap in the current scene.
2. Save the scene. Change the asset in the Inspector, then start a new Play session.
3. Open **Window → General → Test Runner**. Run the EditMode tests, then PlayMode tests.
4. Follow [VALIDATION.md](Documentation/VALIDATION.md) for visual, control, handling, and performance checks.
5. With the Windows Build Support module installed, choose **Rocko → Build Windows Prototype**. Output goes to `Builds/Windows/Rocko.exe` with its supporting files. Distribute the entire build folder.

## Current limits

This is a source prototype and calibration starting point, **not a verified simulator or photo-accurate vehicle asset**. WheelCollider tires are an approximation. There is no combined-slip/load-sensitive custom tire model, suspension linkage geometry, drivetrain elasticity, tire temperature/wear, damage, differential internals, working mirrors, door animation, or detailed instrument cluster. Steering is rate-limited and reduced at speed for keyboard usability. The interior is provisional because no interior photos were supplied.

All geometry is generated at Play. To replace the visual car later, retain the wheel colliders, drivetrain, eye anchor, and telemetry, and replace `CarAssembly`'s render geometry. Keep building/survival logic outside the driving classes so the same powertrain remains authoritative.

Source references and design boundaries: [PHYSICS.md](Documentation/PHYSICS.md). The original three supplied files are included under `Assets/Rocko/Reference` as development references; the game does not render those images as textures.
