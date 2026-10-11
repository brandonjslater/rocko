using NUnit.Framework;
using UnityEngine;

namespace Rocko.Tests
{
    public sealed class PowertrainTests
    {
        //
        // @description - Capture synchronized shafts and account for flywheel acceleration while locked
        // @return {Void} - Verifies static torque balance and sustained coupling under changing engine torque
        //
        [Test]
        public void StaticClutchAccountsForEngineInertia()
        {
            ClutchCoupling clutch = new ClutchCoupling();
            Assert.That(clutch.Step(200, 200, 100, 0.28f, 0.01f, 260, true), Is.EqualTo(100));
            Assert.That(clutch.Locked, Is.True);
            float torque = 0;
            for (int i = 1; i <= 100; i++)
            {
                torque = clutch.Step(200 + i * 0.1f, 200 + i * 0.1f, 100, 0.28f, 0.01f, 260, true);
                Assert.That(clutch.Locked, Is.True);
            }
            Assert.That(torque, Is.EqualTo(100 - 0.28f * 10).Within(0.01f));
        }

        //
        // @description - Ensure a clutch cannot remain locked beyond torque capacity or with the pedal down
        // @return {Void} - Verifies overload, pedal release, gear/reset release, and large-slip rejection
        //
        [Test]
        public void StaticClutchReleasesForOverloadAndPedal()
        {
            ClutchCoupling clutch = new ClutchCoupling();
            clutch.Step(200, 200, 100, 0.28f, 0.01f, 260, true);
            clutch.Step(200, 200, 400, 0.28f, 0.01f, 260, true);
            Assert.That(clutch.Locked, Is.False);
            Assert.That(Mathf.Abs(clutch.Torque), Is.LessThanOrEqualTo(260));
            clutch.Step(200, 200, 100, 0.28f, 0.01f, 260, true);
            Assert.That(clutch.Locked, Is.True);
            Assert.That(clutch.Step(200, 200, 100, 0.28f, 0.01f, 0, false), Is.Zero);
            Assert.That(clutch.Locked, Is.False);
            clutch.Reset();
            clutch.Step(500, 200, 100, 0.28f, 0.01f, 260, true);
            Assert.That(clutch.Locked, Is.False, "Full pressure alone cannot assert lock with a large RPM gap.");
        }

        //
        // @description - Prevent the recorded full-capacity torque reversal while slipping
        // @return {Void} - Verifies bounded torque and passive sliding friction across speed reversals
        //
        [Test]
        public void SlidingClutchDoesNotAlternateFullCapacity()
        {
            ClutchCoupling clutch = new ClutchCoupling();
            for (int i = 0; i < 100; i++)
            {
                float shaft = i % 2 == 0 ? 200 : 600;
                float torque = clutch.Step(400, shaft, 30, 0.28f, 0.01f, 260, true);
                Assert.That(Mathf.Abs(torque), Is.LessThanOrEqualTo(8.001f));
                Assert.That(torque * (400 - shaft), Is.GreaterThanOrEqualTo(0), "Sliding friction cannot add relative kinetic energy.");
                Assert.That(clutch.Locked, Is.False);
            }
        }

        //
        // @description - Verify launch pressure completes engagement despite engine flare or tire slip
        // @return {Void} - NUnit assertions for standstill, transition, and completed launches
        //
        [Test]
        public void LaunchAssistanceCompletesClutchEngagement()
        {
            float launch = DriveMath.LaunchClutchEngagement(2600, 0, 134, 1, 0, 260, 900);
            float moving = DriveMath.LaunchClutchEngagement(2600, 1400, 134, 1, 0, 260, 900);
            Assert.That(launch, Is.InRange(0.5f, 0.6f));
            Assert.That(moving, Is.GreaterThan(launch).And.LessThan(1f));
            Assert.That(DriveMath.LaunchClutchEngagement(6000, 1800, 160, 1, 1, 260, 900), Is.EqualTo(1));
            Assert.That(DriveMath.LaunchClutchEngagement(900, 0, 0, 0, 0, 260, 900), Is.Zero);
        }

        //
        // @description - Ensure rolling resistance cannot pin a stopped car or propel it in reverse
        // @return {Void} - NUnit assertions for forward, reverse, and near-zero motion
        //
        [Test]
        public void RollingResistanceOpposesMotionWithoutHoldingLaunch()
        {
            Assert.That(DriveMath.RollingResistance(0, 130, 1025, 0.01f), Is.Zero);
            Assert.That(DriveMath.RollingResistance(10, 130, 1025, 0.01f), Is.EqualTo(-130));
            Assert.That(DriveMath.RollingResistance(-10, 130, 1025, 0.01f), Is.EqualTo(130));
            float speed = 0.002f;
            float force = DriveMath.RollingResistance(speed, 1300, 1025, 0.01f);
            Assert.That(speed + force / 1025 * 0.01f, Is.EqualTo(0).Within(0.000001f));
        }

        //
        // @description - Check supplied torque and power anchors and prevent interpolation overshoot
        // @return {Void} - NUnit assertions for the baseline engine curve
        //
        [Test]
        public void BaselineMatchesSuppliedEngineAnchors()
        {
            VehicleSpec spec = VehicleSpec.CreateBaseline();
            try
            {
                Assert.That(spec.torqueCurve.Evaluate(4600), Is.EqualTo(165).Within(0.001));
                float watts = spec.torqueCurve.Evaluate(6100) * 6100 * Mathf.PI / 30;
                Assert.That(watts, Is.EqualTo(102000).Within(1));
                for (int rpm = 900; rpm <= 6800; rpm += 10)
                    Assert.That(spec.torqueCurve.Evaluate(rpm), Is.InRange(0f, 165.001f));
            }
            finally { Object.DestroyImmediate(spec); }
        }

        //
        // @description - Verify shaft power units, reverse sign conventions, and zero-speed power
        // @return {Void} - NUnit conversion assertions
        //
        [Test]
        public void ShaftPowerUsesMechanicalHPAndSignedRotation()
        {
            Assert.That(DriveMath.Horsepower(165, 4600), Is.EqualTo(106.59).Within(0.03));
            Assert.That(DriveMath.Horsepower(-165, -4600), Is.EqualTo(DriveMath.Horsepower(165, 4600)).Within(0.001));
            Assert.That(DriveMath.Horsepower(165, 0), Is.Zero);
            Assert.That(165 * DriveMath.NmToLbFt, Is.EqualTo(121.69775).Within(0.001));
        }

        //
        // @description - Check clutch limits and synchronization while both engine and load accelerate
        // @return {Void} - NUnit clutch integration assertions
        //
        [Test]
        public void ClutchRespectsCapacityAndSynchronizesBothInertias()
        {
            Assert.That(DriveMath.ClutchTorque(200, 0, 160, 0.28f, 0.01f, 0, 1), Is.Zero);
            Assert.That(DriveMath.ClutchTorque(200, 0, 160, 0.28f, 0.01f, 120, 1), Is.EqualTo(120));
            Assert.That(DriveMath.ClutchTorque(100, 200, 0, 0.28f, 0.01f, 120, 1), Is.EqualTo(-120));
            float torque = DriveMath.ClutchTorque(200, 199, 100, 0.28f, 0.01f, 260, 1);
            float engineNext = 200 + (100 - torque) / 0.28f * 0.01f;
            float shaftNext = 199 + torque * 0.01f;
            Assert.That(engineNext, Is.EqualTo(shaftNext).Within(0.0001));
        }

        //
        // @description - Require sustained synchronization for light wheels and a heavier grounded driveline
        // @param {float} load - Reflected driveline inertia in kg m²
        // @return {Void} - Checks speed agreement and angular momentum over changing engine torque
        //
        [TestCase(0.01f)]
        [TestCase(0.6f)]
        [TestCase(5f)]
        public void CoupledClutchStaysSynchronizedUnderChangingTorque(float load)
        {
            float engine = 200, shaft = 200;
            for (int step = 0; step < 500; step++)
            {
                float net = step < 200 ? 160 : step < 350 ? -30 : 80;
                float momentumBefore = engine * 0.28f + shaft * load;
                float torque = DriveMath.ClutchTorque(engine, shaft, net, 0.28f, 0.01f, 260, load);
                engine += (net - torque) / 0.28f * 0.01f;
                shaft += torque / load * 0.01f;
                Assert.That(engine, Is.EqualTo(shaft).Within(0.001f));
                Assert.That(engine * 0.28f + shaft * load,
                    Is.EqualTo(momentumBefore + net * 0.01f).Within(0.005f));
            }
        }

        //
        // @description - Reject moving reverse selection and dangerous over-rev downshifts
        // @return {Void} - NUnit gear-selection assertions
        //
        [Test]
        public void SelectionGuardsProtectReverseAndDownshifts()
        {
            VehicleSpec spec = VehicleSpec.CreateBaseline();
            try
            {
                Assert.That(DriveMath.CanSelect(-1, 12, 400, spec), Is.False);
                Assert.That(DriveMath.CanSelect(-1, 0, 0, spec), Is.True);
                Assert.That(DriveMath.CanSelect(1, 30, 1000, spec), Is.False);
                Assert.That(DriveMath.CanSelect(0, 30, 1000, spec), Is.True);
                Assert.That(DriveMath.CanSelect(6, 0, 0, spec), Is.False);
                Assert.That(spec.Ratio(-1), Is.LessThan(0));
                Assert.That(spec.Ratio(0), Is.Zero);
            }
            finally { Object.DestroyImmediate(spec); }
        }

        //
        // @description - Ensure the measured strip uses an exact international mile
        // @return {Void} - NUnit distance assertions
        //
        [Test]
        public void StraightMarkersAreExactEighthMiles()
        {
            Assert.That(DriveMath.MetersPerMile, Is.EqualTo(1609.344f));
            Assert.That(DriveMath.MetersPerMile / 8f, Is.EqualTo(201.168f).Within(0.0001));
            Assert.That(DriveMath.MetersPerMile / 4f, Is.EqualTo(402.336f).Within(0.0001));
        }
    }
}
