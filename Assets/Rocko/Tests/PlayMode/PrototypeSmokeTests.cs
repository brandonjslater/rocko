using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Rocko.Tests
{
    public sealed class PrototypeSmokeTests
    {
        //
        // @description - Require sustained lock in the real scene with launch assistance enabled
        // @return {IEnumerator} - PlayMode launch and cruise assertions
        //
        [UnityTest]
        public IEnumerator AssistedClutchHoldsLockDuringCruise()
        {
            yield return VerifySustainedLock(true);
        }

        //
        // @description - Require sustained lock after a manually released clutch pedal
        // @return {IEnumerator} - PlayMode launch and cruise assertions
        //
        [UnityTest]
        public IEnumerator ManualClutchHoldsLockDuringCruise()
        {
            yield return VerifySustainedLock(false);
        }

        //
        // @description - Drive the real powertrain and check every rendered frame during a two-second cruise
        // @param {bool} assist - Whether to use launch assistance or manually release the clutch
        // @return {IEnumerator} - Real physics scenario with sustained synchronization assertions
        //
        private IEnumerator VerifySustainedLock(bool assist)
        {
            yield return SceneManager.LoadSceneAsync("Rocko", LoadSceneMode.Single);
            yield return null;
            RockoVehicle car = Object.FindFirstObjectByType<RockoVehicle>();
            FirstPersonDriver driver = Object.FindFirstObjectByType<FirstPersonDriver>();
            TestTelemetry telemetry = Object.FindFirstObjectByType<TestTelemetry>();
            driver.SetPaused(false);
            car.controls.enabled = false;
            car.ResetAt(ProvingGround.StraightCar, Quaternion.identity);
            car.occupied = true;
            car.clutchAssist = assist;
            car.controls.Throttle = car.controls.Brake = 0;
            car.controls.Handbrake = false;
            car.controls.Clutch = assist ? 0 : 1;
            try
            {
                telemetry.RecordEveryPhysicsStep = true;
                telemetry.ToggleRecording();
                Assert.That(telemetry.Recording, Is.True, "Test could not create the clutch diagnostic CSV.");
                for (int i = 0; i < 100; i++) yield return new WaitForFixedUpdate();
                car.RequestShift(1);
                car.controls.Throttle = 1;
                for (int i = 0; i < 30; i++) yield return new WaitForFixedUpdate();
                if (!assist)
                    for (int i = 0; i < 100; i++)
                    {
                        car.controls.Clutch = 1f - (i + 1) / 100f;
                        yield return new WaitForFixedUpdate();
                    }
                for (int i = 0; i < 800 && car.ForwardSpeed < 5f; i++)
                    yield return new WaitForFixedUpdate();
                Assert.That(car.ForwardSpeed, Is.GreaterThanOrEqualTo(5f), "Launch must complete before testing cruise lock.");
                car.controls.Throttle = 0.3f;
                // Allow bounded convergence after the large throttle reduction, then demand continuous lock.
                for (int i = 0; i < 300 && !car.ClutchLocked; i++) yield return new WaitForFixedUpdate();
                float end = Time.time + 2f;
                while (Time.time < end)
                {
                    // Sample on render frames, as the player's HUD does, after physics advances.
                    yield return null;
                    Assert.That(car.EngineRunning, Is.True);
                    Assert.That(car.ClutchEngagement, Is.GreaterThan(0.95f));
                    Assert.That(Mathf.Abs(car.ClutchTorqueNm), Is.LessThanOrEqualTo(car.spec.clutchCapacityNm + 0.01f),
                        "Clutch torque must remain within the physical clutch capacity.");
                    float shaft = (car.WheelRPM(0) + car.WheelRPM(1)) * 0.5f * car.spec.Ratio(car.Gear);
                    string state = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                        "Engine and gearbox must stay synchronized. Mode={0}; engine={1:F1} RPM; shaft={2:F1} RPM; signed gap={3:F1}; " +
                        "clutch={4:P1}; torque={5:F1} Nm; capacity={6:F1} Nm; inertia={7:F5}; slip={8:F3}; speed={9:F2} m/s; contacts={10}/4. CSV: {11}",
                        assist ? "assist" : "manual", car.RPM, shaft, car.RPM - shaft, car.ClutchEngagement,
                        car.ClutchTorqueNm, car.spec.clutchCapacityNm * car.ClutchEngagement, car.ReflectedInertia,
                        car.MaxDrivenSlip, car.ForwardSpeed, car.GroundedWheels, telemetry.LastFile);
                    Assert.That(Mathf.Abs(car.RPM - shaft), Is.LessThan(80f), state);
                    Assert.That(car.ClutchLocked, Is.True);
                }
                car.controls.Clutch = 1;
                for (int i = 0; i < 10; i++) yield return new WaitForFixedUpdate();
                Assert.That(car.ClutchLocked, Is.False, "Pressing the clutch must still disengage it.");
            }
            finally
            {
                telemetry.StopRecording();
                Debug.Log("Rocko clutch trace (" + (assist ? "assist" : "manual") + "): " + telemetry.LastFile);
                driver.ResetGarage();
                car.controls.enabled = true;
            }
        }

        //
        // @description - Verify a settled neutral chassis can respond to external longitudinal force
        // @return {IEnumerator} - Detects native wheel constraints pinning the custom drivetrain
        //
        [UnityTest]
        public IEnumerator SettledNeutralChassisRespondsToExternalForce()
        {
            yield return SceneManager.LoadSceneAsync("Rocko", LoadSceneMode.Single);
            yield return null;
            RockoVehicle car = Object.FindFirstObjectByType<RockoVehicle>();
            FirstPersonDriver driver = Object.FindFirstObjectByType<FirstPersonDriver>();
            driver.SetPaused(false);
            car.controls.enabled = false;
            try
            {
                car.ResetAt(ProvingGround.StraightCar, Quaternion.identity);
                car.occupied = true;
                car.controls.Throttle = car.controls.Brake = car.controls.Clutch = car.controls.Steer = 0;
                car.controls.Handbrake = false;
                // Wait beyond PhysX's low-speed timer, with no engine drive request.
                for (float t = 0; t < 2; t += Time.fixedDeltaTime)
                    yield return new WaitForFixedUpdate();
                Assert.That(car.GroundedWheels, Is.EqualTo(4));
                Assert.That(car.SpeedMPH, Is.LessThan(0.5f), "Keep-alive torque must not propel neutral.");
                Vector3 start = car.Body.position;
                for (float t = 0; t < 1; t += Time.fixedDeltaTime)
                {
                    car.Body.AddForce(Vector3.forward * 3000, ForceMode.Force);
                    yield return new WaitForFixedUpdate();
                }
                Assert.That(car.ForwardSpeed, Is.GreaterThan(1f),
                    "A neutral chassis must respond to external force after settling; native sticky constraints may be pinning it.");
                Assert.That(car.Body.position.z - start.z, Is.GreaterThan(0.5f));
            }
            finally
            {
                driver.ResetGarage();
                car.controls.enabled = true;
            }
        }

        //
        // @description - Exercise progressive brake application and immediate release in the real vehicle
        // @return {IEnumerator} - Verifies keyboard pedal steps retain full braking authority
        //
        [UnityTest]
        public IEnumerator BrakePressureBuildsAndReleases()
        {
            yield return SceneManager.LoadSceneAsync("Rocko", LoadSceneMode.Single);
            yield return null;
            RockoVehicle car = Object.FindFirstObjectByType<RockoVehicle>();
            FirstPersonDriver driver = Object.FindFirstObjectByType<FirstPersonDriver>();
            driver.SetPaused(false);
            car.controls.enabled = false;
            try
            {
                car.ResetAt(ProvingGround.StraightCar, Quaternion.identity);
                car.occupied = true;
                car.controls.Throttle = car.controls.Clutch = car.controls.Steer = 0;
                car.controls.Handbrake = false;
                car.controls.Brake = 1;
                yield return new WaitForFixedUpdate();
                Assert.That(car.WheelBrakeTorque(0), Is.GreaterThan(0).And.LessThan(car.spec.frontBrakeTorque));
                for (float t = 0; t < 0.4f; t += Time.fixedDeltaTime)
                    yield return new WaitForFixedUpdate();
                Assert.That(car.WheelBrakeTorque(0), Is.EqualTo(car.spec.frontBrakeTorque).Within(0.1f));
                car.controls.Brake = 0;
                yield return new WaitForFixedUpdate();
                Assert.That(car.WheelBrakeTorque(0), Is.Zero);
                Assert.That(car.WheelBrakeTorque(2), Is.Zero);
            }
            finally
            {
                driver.ResetGarage();
                car.controls.enabled = true;
            }
        }

        //
        // @description - Verify Laguna's closed road collision, spawn, launch, and return to garage
        // @return {IEnumerator} - Exercises the circuit through its actual gameplay entry point
        //
        [UnityTest]
        public IEnumerator LagunaCircuitHasContinuousRoadAndLaunches()
        {
            yield return SceneManager.LoadSceneAsync("Rocko", LoadSceneMode.Single);
            yield return null;
            FirstPersonDriver driver = Object.FindFirstObjectByType<FirstPersonDriver>();
            RockoVehicle car = driver.car;
            driver.StageCircuit();
            car.controls.enabled = false;
            try
            {
                car.controls.Throttle = car.controls.Brake = car.controls.Clutch = car.controls.Steer = 0;
                car.controls.Handbrake = false;
                LagunaSeca track = Object.FindFirstObjectByType<LagunaSeca>();
                Assert.That(track.Length, Is.EqualTo(3601.712f).Within(0.2f));
                MeshCollider road = track.transform.Find("Closed asphalt circuit").GetComponent<MeshCollider>();
                Physics.SyncTransforms();
                Vector3[] roadVertices=road.sharedMesh.vertices;
                int[] roadTriangles=road.sharedMesh.triangles;
                for(int t=0;t<roadTriangles.Length;t+=3)
                {
                    Vector3 a=roadVertices[roadTriangles[t]], b=roadVertices[roadTriangles[t+1]], c=roadVertices[roadTriangles[t+2]];
                    Assert.That(Vector3.Cross(b-a,c-a).normalized.y,Is.GreaterThan(.9f),
                        "Excessively steep or folded driving triangle " + t/3);
                }
                for (int i=0;i<track.Points.Length;i+=10)
                {
                    Vector3 point=track.Points[i];
                    Assert.That(road.Raycast(new Ray(point+Vector3.up*2,Vector3.down),out RaycastHit hit,3), Is.True, "Road collision missing at sample " + i);
                    Assert.That(hit.normal.y, Is.GreaterThan(.85f), "Unexpected road grade at sample " + i);
                    // Query the scene, not just the asphalt collider: runoff/terrain can obstruct it.
                    Vector3 across=Vector3.Cross(Vector3.up,track.Direction(i)).normalized;
                    foreach(float offset in new[]{-4.8f,-2.4f,0f,2.4f,4.8f})
                    {
                        Vector3 lane=point+across*offset;
                        RaycastHit[] hits=Physics.RaycastAll(lane+Vector3.up*80,Vector3.down,100);
                        foreach(RaycastHit obstruction in hits)
                        {
                            if(!obstruction.collider.transform.IsChildOf(track.transform)) continue;
                            Assert.That(obstruction.point.y, Is.LessThanOrEqualTo(lane.y+.08f),
                                "Track mesh obstructs lane at sample " + i + ", offset " + offset + ": " + obstruction.collider.name);
                        }
                    }
                }
                Assert.That(Vector3.Distance(track.Points[0],track.Points[track.Points.Length-1]), Is.LessThan(2));
                for(int i=0;i<100;i++) yield return new WaitForFixedUpdate();
                Assert.That(car.GroundedWheels, Is.EqualTo(4));
                car.clutchAssist=true;
                car.RequestShift(1);car.controls.Throttle=1;
                for(int i=0;i<300;i++) yield return new WaitForFixedUpdate();
                Assert.That(car.ForwardSpeed, Is.GreaterThan(2));
                driver.StageCircuit();
                Assert.That(Object.FindObjectsByType<LagunaSeca>(FindObjectsSortMode.None).Length, Is.EqualTo(1), "Reset must reuse the circuit.");
                driver.ResetGarage();
                Assert.That(driver.OnCircuit, Is.False);
                Assert.That(driver.Driving, Is.False);
            }
            finally
            {
                driver.ResetGarage();car.controls.enabled=true;
            }
        }

        //
        // @description - Brake through a moderate flat-pad turn without runaway yaw or rollover
        // @return {IEnumerator} - Checks the actual shared drivetrain and PhysX lateral integration
        //
        [UnityTest]
        public IEnumerator ModerateCornerBrakingRemainsStable()
        {
            yield return SceneManager.LoadSceneAsync("Rocko", LoadSceneMode.Single);
            yield return null;
            FirstPersonDriver driver = Object.FindFirstObjectByType<FirstPersonDriver>();
            RockoVehicle car = driver.car;
            driver.SetPaused(false);
            car.controls.enabled=false;
            try
            {
                car.ResetAt(new Vector3(92,.08f,125),Quaternion.identity);
                car.occupied=true;car.clutchAssist=true;
                car.controls.Throttle=car.controls.Brake=car.controls.Steer=car.controls.Clutch=0;
                car.controls.Handbrake=false;
                for(int i=0;i<100;i++) yield return new WaitForFixedUpdate();
                car.RequestShift(1);car.controls.Throttle=1;
                for(int i=0;i<400 && car.ForwardSpeed<9;i++) yield return new WaitForFixedUpdate();
                Assert.That(car.ForwardSpeed,Is.GreaterThan(8),"Reach the test entry speed.");
                car.controls.Throttle=0;car.controls.Steer=.12f;
                car.controls.Clutch=1;car.controls.Brake=.6f;
                float initialSpeed=car.ForwardSpeed;
                for(int i=0;i<150;i++)
                {
                    yield return new WaitForFixedUpdate();
                    Assert.That(Mathf.Abs(car.Body.angularVelocity.y),Is.LessThan(1.2f),"Moderate corner braking must not create runaway yaw.");
                    Assert.That(Vector3.Dot(car.transform.up,Vector3.up),Is.GreaterThan(.9f));
                    for(int w=0;w<4;w++)
                        if(car.wheels[w].GetGroundHit(out WheelHit contact) && contact.force>100)
                            Assert.That(car.wheels[w].sidewaysFriction.stiffness,Is.GreaterThanOrEqualTo(.74f),"Asphalt lateral authority must not collapse.");
                }
                Assert.That(car.SpeedMPH/2.2369363f,Is.LessThan(initialSpeed*.75f),"Stability must not come from disabling brakes.");
            }
            finally
            {
                driver.ResetGarage();car.controls.enabled=true;
            }
        }

        //
        // @description - Load the real scene, verify on-foot spawn, then exercise its actual launch powertrain
        // @return {IEnumerator} - Unity coroutine performing physics smoke assertions
        //
        [UnityTest]
        public IEnumerator RealSceneSpawnsOutsideAndLaunchesWithFiniteState()
        {
            yield return SceneManager.LoadSceneAsync("Rocko", LoadSceneMode.Single);
            yield return null;
            RockoVehicle car = Object.FindFirstObjectByType<RockoVehicle>();
            FirstPersonDriver driver = Object.FindFirstObjectByType<FirstPersonDriver>();
            Assert.That(car, Is.Not.Null);
            Assert.That(driver.Driving, Is.False);
            Assert.That(Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
            Assert.That(car.wheels.Length, Is.EqualTo(4));
            Assert.That(car.Body.mass, Is.EqualTo(1025));
            driver.SetPaused(false);
            car.controls.enabled = false;
            car.ResetAt(ProvingGround.StraightCar, Quaternion.identity);
            car.occupied = true;
            car.controls.Clutch = 0;
            car.controls.Brake = 0;
            car.controls.Handbrake = false;
            car.controls.Throttle = 0;
            for (int i = 0; i < 100; i++) yield return new WaitForFixedUpdate();
            Assert.That(car.SpeedMPH, Is.LessThan(0.5f), "Neutral should not propel the car.");
            float supportedMass = 0;
            foreach (WheelCollider wheel in car.wheels)
            {
                Assert.That(wheel.isGrounded, Is.True, "Every tire should contact the flat staging apron.");
                Assert.That(wheel.sprungMass, Is.GreaterThan(100f), "Suspension must use the loaded car mass.");
                supportedMass += wheel.sprungMass;
            }
            Assert.That(supportedMass, Is.EqualTo(car.Body.mass).Within(1f));
            car.Body.Sleep();
            Assert.That(car.Body.IsSleeping(), Is.True);
            car.RequestShift(1);
            car.controls.Throttle = 1;
            bool lockedAfterLaunch = false;
            for (int i = 0; i < 400; i++)
            {
                yield return new WaitForFixedUpdate();
                lockedAfterLaunch |= car.ClutchLocked;
                for (int w = 0; w < 4; w++)
                {
                    Assert.That(car.WheelBrakeTorque(w), Is.Zero, "Released pedals must not apply drivetrain brakes.");
                    Assert.That(car.wheels[w].motorTorque, Is.EqualTo(RockoVehicle.NativeWheelKeepAliveTorque), "Native wheels must suppress their independent sticky constraints.");
                    Assert.That(car.wheels[w].forwardFriction.stiffness, Is.Zero, "Longitudinal tire force must have only one owner.");
                }
            }
            Assert.That(car.Body.IsSleeping(), Is.False, "Driving input should wake the parked chassis.");
            Assert.That(car.Gear, Is.EqualTo(1));
            Assert.That(float.IsNaN(car.RPM) || float.IsInfinity(car.RPM), Is.False);
            Assert.That(car.ForwardSpeed, Is.GreaterThan(2f), "Assisted first-gear launch should move forward.");
            Assert.That(car.EngineRunning, Is.True, "The assisted launch should not stall the engine.");
            Assert.That(lockedAfterLaunch, Is.True, "Launch assistance must reach a synchronized, fully engaged clutch.");
            Assert.That(car.ClutchEngagement, Is.GreaterThan(0.95f), "Clutch pressure must stay fully engaged after launch.");
            Assert.That(car.transform.position.y, Is.InRange(-0.1f, 1f), "Car should stay on the track.");
            car.controls.Throttle = 0;
            car.controls.Brake = 1;
            for (int i = 0; i < 300; i++) yield return new WaitForFixedUpdate();
            Assert.That(car.SpeedMPH, Is.LessThan(1f), "Service brakes should stop the first-gear run.");
            car.controls.Brake = 0;
            car.controls.Throttle = 1;
            for (int i = 0; i < 300; i++) yield return new WaitForFixedUpdate();
            Assert.That(car.ForwardSpeed, Is.GreaterThan(2f), "Releasing the brake must allow another launch.");
            driver.ResetGarage();
            Assert.That(driver.Driving, Is.False);
            car.controls.enabled = true;
        }
    }
}
