using UnityEngine;

namespace Rocko
{
    public static class DriveMath
    {
        public const float MetersPerMile = 1609.344f;
        public const float NmToLbFt = 0.737562149f;
        public const float WattsPerHP = 745.699872f;

        //
        // @description - Blend slipping launch assistance into full clutch pressure as road speed rises
        // @param {float} rpm - Engine RPM
        // @param {float} roadRPM - Absolute engine-equivalent RPM calculated from road speed and selected gear
        // @param {float} netTorque - Net engine torque in newton meters
        // @param {float} throttle - Normalized throttle
        // @param {float} tireSlip - Absolute driven tire slip
        // @param {float} capacity - Fully engaged clutch capacity in newton meters
        // @param {float} idleRPM - Engine idle RPM
        // @return {float} - Clutch engagement from zero to one
        //
        public static float LaunchClutchEngagement(float rpm, float roadRPM, float netTorque,
            float throttle, float tireSlip, float capacity, float idleRPM)
        {
            float targetRPM = Mathf.Lerp(1200, 2600, throttle);
            float launchTorque = Mathf.Max(0, netTorque + (rpm - targetRPM) * 0.15f);
            float slipReduction = 1f / (1f + Mathf.Max(0, tireSlip - 0.08f) * 10f);
            float launchPressure = Mathf.Clamp01(launchTorque / Mathf.Max(1, capacity)) * slipReduction;
            float completion = Mathf.InverseLerp(idleRPM + 100, idleRPM + 900, roadRPM);
            // RPM difference and tire slip cannot keep the clutch open after launch.
            return Mathf.Lerp(launchPressure, 1f, completion);
        }

        //
        // @description - Oppose rolling motion without applying a wheel brake or reversing velocity
        // @param {float} speed - Signed forward speed in meters per second
        // @param {float} force - Nonnegative rolling resistance magnitude in newtons
        // @param {float} mass - Chassis mass in kilograms
        // @param {float} dt - Physics step in seconds
        // @return {float} - Signed longitudinal resistance in newtons, zero at rest
        //
        public static float RollingResistance(float speed, float force, float mass, float dt)
        {
            if (dt <= 0 || mass <= 0 || Mathf.Abs(speed) < 0.001f) return 0;
            return -Mathf.Sign(speed) * Mathf.Min(Mathf.Max(0, force), Mathf.Abs(speed) * mass / dt);
        }

        //
        // @description - Convert shaft torque and rotational speed to mechanical horsepower
        // @param {float} torqueNm - Signed shaft torque in newton meters
        // @param {float} rpm - Signed shaft speed in revolutions per minute
        // @return {float} - Signed mechanical horsepower
        //
        public static float Horsepower(float torqueNm, float rpm)
        {
            return torqueNm * rpm * Mathf.PI / 30f / WattsPerHP;
        }

        //
        // @description - Reject unsafe reverse selection and mechanical over-rev downshifts
        // @param {int} target - Requested gear
        // @param {float} speed - Vehicle speed magnitude in meters per second
        // @param {float} wheelRPM - Mean driven axle speed
        // @param {VehicleSpec} spec - Vehicle calibration
        // @return {bool} - True when selection is allowed
        //
        public static bool CanSelect(int target, float speed, float wheelRPM, VehicleSpec spec)
        {
            if (target < -1 || target > 5) return false;
            if (target == -1 && speed > 0.5f) return false;
            return target == 0 || Mathf.Abs(wheelRPM * spec.Ratio(target)) < spec.redlineRPM + 200f;
        }
    }
}
