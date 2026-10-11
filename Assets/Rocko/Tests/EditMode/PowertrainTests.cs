using NUnit.Framework;
using UnityEngine;

namespace Rocko.Tests
{
    public sealed class PowertrainTests
    {
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
        // @description - Check clutch disengagement, torque capacity, and synchronization without overshoot
        // @return {Void} - NUnit clutch integration assertions
        //
        [Test]
        public void ClutchRespectsCapacityAndSynchronizesSampledShaft()
        {
            Assert.That(DriveMath.ClutchTorque(200, 0, 160, 0.28f, 0.01f, 0), Is.Zero);
            Assert.That(DriveMath.ClutchTorque(200, 0, 160, 0.28f, 0.01f, 120), Is.EqualTo(120));
            Assert.That(DriveMath.ClutchTorque(100, 200, 0, 0.28f, 0.01f, 120), Is.EqualTo(-120));
            float torque = DriveMath.ClutchTorque(200, 199, 100, 0.28f, 0.01f, 260);
            Assert.That(200 + (100 - torque) / 0.28f * 0.01f, Is.EqualTo(199).Within(0.0001));
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
