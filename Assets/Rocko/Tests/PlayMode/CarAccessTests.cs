using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Rocko.Tests
{
    public sealed class CarAccessTests
    {
        //
        // @description - Walk the real capsule through the open doorway; clicking from outside must never seat it
        // @return {IEnumerator} - PlayMode collision, manual entry and manual exit checks
        //
        [UnityTest]
        public IEnumerator PlayerMustMoveThroughDoorBeforeSitting()
        {
            yield return SceneManager.LoadSceneAsync("Rocko", LoadSceneMode.Single);
            yield return null;
            FirstPersonDriver driver = Object.FindFirstObjectByType<FirstPersonDriver>();
            RockoVehicle car = driver.car;
            CarAccess access = car.GetComponent<CarAccess>();
            CharacterController capsule = driver.GetComponent<CharacterController>();
            driver.enabled = false; car.controls.enabled = false;
            car.ResetAt(ProvingGround.StraightCar, Quaternion.identity);
            car.Body.isKinematic = true;
            capsule.enabled = false;
            driver.transform.position = car.transform.TransformPoint(new Vector3(-1.5f, 0.02f, 0.04f));
            driver.SetCrouched(true);
            capsule.enabled = true;
            Physics.SyncTransforms();
            try
            {
                Assert.That(driver.TryEnter(), Is.False);
                // 🚶 Move the same CharacterController used by gameplay, not the Transform.
                for (int i = 0; i < 80; i++)
                {
                    capsule.Move(new Vector3(0.012f, -0.02f, 0));
                    yield return new WaitForFixedUpdate();
                }
                Assert.That(car.transform.InverseTransformPoint(driver.transform.position).x,
                    Is.LessThan(-0.9f), "A closed door must block the walking body.");
                access.driverDoor.SetOpen(true);
                yield return new WaitForSeconds(0.75f);
                Vector3 before = driver.transform.position;
                Assert.That(driver.TryEnter(), Is.False, "An open door must not teleport an outside player.");
                Assert.That(driver.transform.position, Is.EqualTo(before));
                for (int i = 0; i < 110; i++)
                {
                    if (car.transform.InverseTransformPoint(driver.transform.position).x < -0.36f)
                        capsule.Move(new Vector3(0.009f, -0.02f, 0));
                    else capsule.Move(Vector3.down * 0.02f);
                    yield return new WaitForFixedUpdate();
                }
                Assert.That(access.CanEnter(driver.transform.position), Is.True,
                    "Crouched movement must fit through the doorway and reach the driver footwell.");
                Assert.That(driver.SetCrouched(false), Is.False, "The roof must block standing.");
                driver.view.transform.LookAt(access.seatTarget.bounds.center);
                Physics.SyncTransforms();
                Assert.That(access.FindTarget(driver.view, driver.transform), Is.EqualTo(access.seatTarget));
                Assert.That(driver.TryEnter(), Is.True);
                Assert.That(driver.Driving, Is.True);
                Assert.That(capsule.enabled, Is.False);
                Assert.That(driver.view.transform.parent, Is.EqualTo(car.seat));
                driver.SendMessage("TryExit");
                Assert.That(driver.Driving, Is.False);
                Assert.That(driver.Crouched, Is.True);
                Assert.That(capsule.enabled, Is.True);
                Assert.That(car.transform.InverseTransformPoint(driver.transform.position).x,
                    Is.GreaterThan(-0.6f), "Getting up must leave the player inside the car.");
                for (int i = 0; i < 140; i++)
                {
                    capsule.Move(new Vector3(-0.012f, -0.02f, 0));
                    yield return new WaitForFixedUpdate();
                }
                Assert.That(car.transform.InverseTransformPoint(driver.transform.position).x, Is.LessThan(-1.5f));
                Assert.That(driver.SetCrouched(false), Is.True);
                driver.ResetGarage();
                Assert.That(access.driverDoor.IsOpen, Is.False);
                Assert.That(driver.Driving, Is.False);
                Assert.That(driver.Crouched, Is.False);
            }
            finally
            {
                car.Body.isKinematic = false; driver.ResetGarage();
                driver.enabled = true; car.controls.enabled = true;
            }
        }

        //
        // @description - Verify hinge directions, player-only collision and closed reset without changing inertia
        // @return {IEnumerator} - PlayMode panel assertions
        //
        [UnityTest]
        public IEnumerator PanelsOpenOutwardAndResetClosed()
        {
            yield return SceneManager.LoadSceneAsync("Rocko", LoadSceneMode.Single);
            yield return null;
            FirstPersonDriver driver = Object.FindFirstObjectByType<FirstPersonDriver>();
            CarAccess access = driver.car.GetComponent<CarAccess>();
            Vector3 inertia = driver.car.Body.inertiaTensor;
            CarPanel[] panels = { access.driverDoor, access.passengerDoor, access.hood, access.hatch };
            foreach (CarPanel panel in panels) panel.SetOpen(true);
            yield return new WaitForSeconds(0.75f);
            Assert.That((access.driverDoor.transform.localRotation * Vector3.back).x, Is.LessThan(0));
            Assert.That((access.passengerDoor.transform.localRotation * Vector3.back).x, Is.GreaterThan(0));
            Assert.That((access.hood.transform.localRotation * Vector3.forward).y, Is.GreaterThan(0));
            Assert.That((access.hatch.transform.localRotation * Vector3.back).y, Is.GreaterThan(0));
            Assert.That(access.CanEnter(driver.car.transform.TransformPoint(new Vector3(1.6f, 0, 0))), Is.False);
            Assert.That(driver.car.Body.inertiaTensor, Is.EqualTo(inertia));
            foreach (Collider collider in access.driverDoor.GetComponentsInChildren<Collider>())
                if (!collider.isTrigger)
                    Assert.That(collider.excludeLayers.value, Is.EqualTo(~(1 << CarAccess.PlayerLayer)));
            driver.ResetGarage();
            foreach (CarPanel panel in panels)
            {
                Assert.That(panel.IsOpen, Is.False);
                Assert.That(Quaternion.Angle(panel.transform.localRotation, Quaternion.identity), Is.LessThan(0.01f));
            }
        }
    }
}
