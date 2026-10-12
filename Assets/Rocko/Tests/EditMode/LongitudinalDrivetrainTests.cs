using NUnit.Framework;
using System;

namespace Rocko.Tests
{
    public sealed class LongitudinalDrivetrainTests
    {
        //
        // @description - Build a four-wheel rotor fixture without contact forces
        // @return {LongitudinalDrivetrain} - Pure game solver ready for conservation and capacity checks
        //
        private LongitudinalDrivetrain Create()
        {
            var model = new LongitudinalDrivetrain();
            model.Reset(2000);
            for (int i = 0; i < 4; i++) model.WheelInertia[i] = 0.663552;
            return model;
        }

        //
        // @description - Verify actual engine and wheel integration stays synchronized under changing torque
        // @return {Void} - Tests the runtime solver rather than a lock flag or independent formula
        //
        [Test]
        public void SharedIntegrationSustainsSynchronization()
        {
            var model = Create();
            model.WheelOmega[0] = model.WheelOmega[1] = model.EngineOmega / 12.67;
            for (int step = 0; step < 300; step++)
            {
                model.Step(0.01, step < 150 ? 100 : -20, 0.28, 260, 12.67, 0.88, 1025, 0.288);
                double shaft = (model.WheelOmega[0] + model.WheelOmega[1]) * 0.5 * 12.67;
                Assert.That(Math.Abs(model.EngineOmega - shaft), Is.LessThan(0.00001));
                Assert.That(Math.Abs(model.ClutchTorque), Is.LessThanOrEqualTo(260.00001));
            }
        }

        //
        // @description - Ensure clutch overload causes real slip and an open clutch transfers no torque
        // @return {Void} - Verifies the model cannot force engine RPM to wheel RPM beyond capacity
        //
        [Test]
        public void CapacityAndNeutralPreserveIndependentSpeeds()
        {
            var model = Create();
            model.Step(0.01, 100, 0.28, 1, 12.67, 0.88, 1025, 0.288);
            Assert.That(Math.Abs(model.ClutchTorque), Is.LessThanOrEqualTo(1.00001));
            Assert.That(model.EngineOmega - (model.WheelOmega[0] + model.WheelOmega[1]) * 12.67 / 2, Is.GreaterThan(100));
            model.Step(0.01, 100, 0.28, 0, 12.67, 0.88, 1025, 0.288);
            Assert.That(model.ClutchTorque, Is.Zero);
            model.Step(0.01, 100, 0.28, 260, 0, 0.88, 1025, 0.288);
            Assert.That(model.ClutchTorque, Is.Zero);
            Assert.That(model.AxleTorque, Is.Zero);
        }

        //
        // @description - Prevent invisible traction from airborne tires
        // @return {Void} - Confirms wheels spin while chassis contact impulses stay zero
        //
        [Test]
        public void AirborneWheelsCannotPropelChassis()
        {
            var model = Create();
            model.Step(0.1, 100, 0.28, 260, 12.67, 0.88, 1025, 0.288);
            Assert.That(model.WheelOmega[0], Is.GreaterThan(0));
            foreach (double impulse in model.ForceImpulse) Assert.That(impulse, Is.Zero);
        }
    }
}
