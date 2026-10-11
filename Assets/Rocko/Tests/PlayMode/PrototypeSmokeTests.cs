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
            for (int i = 0; i < 400; i++) yield return new WaitForFixedUpdate();
            Assert.That(car.Body.IsSleeping(), Is.False, "Driving input should wake the parked chassis.");
            Assert.That(car.Gear, Is.EqualTo(1));
            Assert.That(float.IsNaN(car.RPM) || float.IsInfinity(car.RPM), Is.False);
            Assert.That(car.ForwardSpeed, Is.GreaterThan(2f), "Assisted first-gear launch should move forward.");
            Assert.That(car.EngineRunning, Is.True, "The assisted launch should not stall the engine.");
            Assert.That(car.transform.position.y, Is.InRange(-0.1f, 1f), "Car should stay on the track.");
            car.controls.Throttle = 0;
            car.controls.Brake = 1;
            for (int i = 0; i < 300; i++) yield return new WaitForFixedUpdate();
            Assert.That(car.SpeedMPH, Is.LessThan(1f), "Service brakes should stop the first-gear run.");
            driver.ResetGarage();
            Assert.That(driver.Driving, Is.False);
            car.controls.enabled = true;
        }
    }
}
