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
                for (int i = 0; i < 100; i++) yield return new WaitForFixedUpdate();
                float end = Time.time + 2f;
                while (Time.time < end)
                {
                    // Sample on render frames, as the player's HUD does, after physics advances.
                    yield return null;
                    Assert.That(car.EngineRunning, Is.True);
                    Assert.That(car.ClutchEngagement, Is.GreaterThan(0.95f));
                    float shaft = (car.wheels[0].rpm + car.wheels[1].rpm) * 0.5f * car.spec.Ratio(car.Gear);
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
                foreach (WheelCollider wheel in car.wheels)
                    Assert.That(wheel.brakeTorque, Is.Zero, "Rolling resistance must not engage wheel brakes during launch.");
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
