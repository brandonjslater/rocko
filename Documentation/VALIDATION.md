# Validation status and first-drive checks

## Performed in the authoring environment

- Read the supplied spec sheet and visually inspected both supplied photos.
- Parsed all 16 C# source files with the tree-sitter C# grammar: no syntax-error nodes.
- Parsed package/assembly JSON and checked the scene → bootstrap script and build-settings → scene GUID references.
- Independently checked numerical conversions: 102 kW = 136.784 hp; 165 Nm = 121.698 lb-ft; 102 kW at 6,100 rpm = 159.677 Nm; 1/8 mile = 201.168 m.
- Reviewed the source for one gameplay camera, manual-only gear selection, front-only motor torque, HUD units, surface metadata, and input bindings.

**Not performed:** Unity import/compilation, shader compilation, editor rendering, gamepad hardware testing, PlayMode simulation, standalone build, or measured performance. Syntax parsing is not a C# type check and is not a substitute for Unity execution. No screenshots or benchmark numbers are presented as Unity results.

## Included automated Unity tests — not yet executed

Run **Window → General → Test Runner**.

EditMode:

1. Supplied torque/power anchors and no interpolation overshoot.
2. Mechanical horsepower conversion, reverse sign, and zero-speed power.
3. Clutch disengagement/capacity/sampled-shaft synchronization.
4. Reverse, gear-range, and over-rev guards.
5. Mile/eighth-mile/quarter-mile distances.

PlayMode:

- Loads the actual Rocko scene, verifies on-foot spawn and exactly one camera.
- Checks neutral does not propel the car.
- Requests first gear and applies throttle through the real RockoVehicle controller for four seconds.
- Checks finite RPM, forward movement, road-height bounds, braking to a stop, and reset back outside.

The PlayMode test loads the scene in Single mode; save your editor work before running tests. This is a physics smoke test, not a real-car fidelity certification.

## First import

- [ ] Package Manager resolves Input System and Test Framework without errors.
- [ ] Console has no C# compiler errors.
- [ ] Project uses the Built-in pipeline; car/road materials are visible rather than pink.
- [ ] Play generates the world, blue car, one camera, and all HUD fields.
- [ ] Player starts outside the car; garage floor and walls block the character appropriately.
- [ ] Mouse capture and Esc work in Game view; focus loss pauses simulation.
- [ ] Signs are readable from their intended approach direction.

## Interaction and controls

- [ ] E enters from near the driver's door; the camera remains inside the cabin when looking around.
- [ ] Mouse and gamepad look work while steering independently.
- [ ] Shift/Ctrl and RB/LB select every gear in sequence including R and N.
- [ ] W/RT drives in the selected direction; S/LT always brakes, including reverse.
- [ ] Reverse selection while moving is blocked.
- [ ] Excessive-RPM downshift requests are blocked.
- [ ] F3 disables clutch assistance; releasing the clutch badly can stall the engine.
- [ ] I/Y restarts with clutch disengaged, neutral, or assistance enabled.
- [ ] Exit is blocked while moving; stationary exit finds a clear location or reports blocked sides.
- [ ] R/Back returns to the garage on foot; F2/D-pad up stages the car seated.
- [ ] A reset after high wheelspin clears wheel rotational/contact state. If toggling WheelCollider does not clear its native state on your Unity patch, rebuild the wheel components on reset before benchmarking repeatability.
- [ ] Controller deadzones, button names, and focus behavior match the actual target hardware.

## Physics and geometry

- [ ] All four wheels touch the ground without chassis-floor collision at rest.
- [ ] Car rolls and brakes without persistent vibration or left/right drift.
- [ ] No sustained oscillation of engine RPM / clutch torque once the driveline is engaged.
- [ ] Full-throttle first-gear launch moves the car without unbounded wheel RPM or repeated stalls.
- [ ] Wheel visuals rotate around their axles and follow steering/suspension poses.
- [ ] Chassis collides with garage walls; player can walk around the complete vehicle.
- [ ] Uphill/downhill motion follows gravity; hills are reachable without abrupt mesh steps.
- [ ] Dirt reduces lateral/longitudinal grip and increases rolling losses relative to asphalt.
- [ ] Car can coast in neutral and engine-brake in gear.
- [ ] Inspect keyboard steering range at high speed. Current rate/range assistance is a usability tune, not measured steering geometry.

## Performance evidence to collect

Use the same tune, fixed step, test mass, surface, and input method for every comparison. Start a separate CSV for each run.

| Test | Measurement | Current result |
|---|---|---|
| Neutral idle | RPM stability and no creeping | Not run |
| Repeatable first-gear launch | RPM, front slip, clutch engagement | Not run |
| Acceleration | 0–60 mph, 0–100 km/h, eighth/quarter elapsed time | Not run |
| Power | Wheel hp versus RPM when clutch is locked | Not run |
| Speed / gearing | RPM at several steady road speeds in each gear | Not run |
| Braking | Stop time/distance, lateral drift | Not run |
| Coastdown | Speed loss with clutch open / neutral | Not run |
| Skid circle | Speed, lateral G, understeer onset | Not run |
| Top speed | Sustained speed with acceleration near zero | Not run |
| Frame-rate independence | Repeat at 30, 60, and 120 render fps | Not run |

The user supplied an approximate 210 km/h top speed, but no acceleration benchmark. Do not tune power to make an invented acceleration target pass. Verify the clutch, traction, gearing, mass, and drag assumptions first.

## Telemetry sanity checks

- [ ] All requested HUD values are finite and update while driving.
- [ ] Neutral/open clutch gives zero driven-axle output.
- [ ] At zero wheel RPM, wheel power is zero even if torque is nonzero.
- [ ] Reverse propulsion has negative axle torque and negative wheel RPM, yielding positive shaft power.
- [ ] Engine braking may produce negative wheel power.
- [ ] CSV file closes and remains readable after F5, reset, pause, and stopping Play mode.
- [ ] Standing-start timing does not get confused with rolling crossings of the painted markers.
- [ ] Leaving the measured lane marks the run invalid rather than displaying a valid full-mile result.

## Before expanding the game

Stabilize import/play, entry/exit, clutch engagement, tire contact, braking, and repeatability first. Then obtain the specific gearbox code, tire size, measured dyno curve, suspension details, and interior references. Replace the body/interior with a properly authored asset while keeping the same validated controller and telemetry.
