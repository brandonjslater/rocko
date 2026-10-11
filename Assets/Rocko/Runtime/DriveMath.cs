using UnityEngine;

namespace Rocko
{
    public static class DriveMath
    {
        public const float MetersPerMile = 1609.344f;
        public const float NmToLbFt = 0.737562149f;
        public const float WattsPerHP = 745.699872f;

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
        // @description - Limit clutch coupling to capacity and avoid overshooting sampled shaft speed
        // @param {float} engineOmega - Engine angular velocity in radians per second
        // @param {float} shaftOmega - Gearbox input angular velocity in radians per second
        // @param {float} netTorque - Net engine torque in newton meters
        // @param {float} inertia - Engine rotational inertia in kg m²
        // @param {float} dt - Physics step in seconds
        // @param {float} capacity - Available clutch torque in newton meters
        // @return {float} - Signed torque transmitted into the gearbox
        //
        public static float ClutchTorque(float engineOmega, float shaftOmega, float netTorque, float inertia, float dt, float capacity)
        {
            return Mathf.Clamp(netTorque + (engineOmega - shaftOmega) * inertia / dt, -capacity, capacity);
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
