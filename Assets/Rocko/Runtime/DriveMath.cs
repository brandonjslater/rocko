using UnityEngine;

namespace Rocko
{
    // Separates sliding friction from static clutch coupling across PhysX steps.
    public sealed class ClutchCoupling
    {
        public bool Locked { get; private set; }
        public float Torque { get; private set; }
        private bool sampled;
        private float previousShaft, shaftAcceleration;

        //
        // @description - Release coupling and discard history when shifting or resetting
        // @return {Void} - Clears lock, torque, and shaft acceleration history
        //
        public void Reset()
        {
            Locked = sampled = false;
            Torque = previousShaft = shaftAcceleration = 0;
        }

        //
        // @description - Solve sliding or static clutch torque using the observed gearbox response
        // @param {float} engine - Engine angular speed in radians per second
        // @param {float} shaft - Gearbox input angular speed in radians per second
        // @param {float} net - Net engine torque in newton meters
        // @param {float} inertia - Engine inertia in kg m²
        // @param {float} dt - Physics timestep in seconds
        // @param {float} capacity - Available friction torque in newton meters
        // @param {bool} fullPressure - Whether the pedal permits static coupling
        // @return {float} - Capacity-limited clutch torque; Locked selects a shared shaft speed
        //
        public float Step(float engine, float shaft, float net, float inertia, float dt, float capacity, bool fullPressure)
        {
            dt = Mathf.Max(0.0001f, dt);
            capacity = Mathf.Max(0, capacity);
            float acceleration = sampled ? (shaft - previousShaft) / dt : 0;
            shaftAcceleration = Mathf.Lerp(shaftAcceleration, acceleration, 1f - Mathf.Exp(-dt / 0.08f));
            previousShaft = shaft;
            sampled = true;
            // Static friction must accelerate the flywheel as well as transmit engine torque.
            float required = net - inertia * shaftAcceleration;
            bool canHold = fullPressure && capacity > 0 && shaft >= 0 && Mathf.Abs(required) <= capacity;
            float gap = engine - shaft;
            if (!canHold) Locked = false;
            else if (Mathf.Abs(gap) < 40f * Mathf.PI / 30f) Locked = true;
            if (Locked)
            {
                Torque = required;
                return Torque;
            }
            // Correct slip over 0.35 s instead of chasing a stale wheel speed in one tick.
            float target = net + inertia * gap / 0.35f;
            target = Mathf.Clamp(target, -capacity, capacity);
            Torque = Mathf.MoveTowards(Torque, target, 800f * dt);
            Torque = Mathf.Clamp(Torque, -capacity, capacity);
            // Sliding friction must oppose relative rotation, including after a sign change.
            if (gap > 0) Torque = Mathf.Max(0, Torque);
            else if (gap < 0) Torque = Mathf.Min(0, Torque);
            return Torque;
        }
    }

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
        // @description - Solve clutch coupling against two finite inertias, limited by friction capacity
        // @param {float} engineOmega - Engine angular velocity in radians per second
        // @param {float} shaftOmega - Gearbox input angular velocity in radians per second
        // @param {float} netTorque - Net engine torque in newton meters
        // @param {float} inertia - Engine rotational inertia in kg m²
        // @param {float} dt - Physics step in seconds
        // @param {float} capacity - Available clutch torque in newton meters
        // @param {float} loadInertia - Gearbox load inertia reflected to the crank in kg m²
        // @return {float} - Signed torque transmitted into the gearbox
        //
        public static float ClutchTorque(float engineOmega, float shaftOmega, float netTorque, float inertia, float dt, float capacity, float loadInertia)
        {
            float engine = Mathf.Max(0.0001f, inertia);
            float load = Mathf.Max(0.0001f, loadInertia);
            float reducedInertia = engine * load / (engine + load);
            float torque = netTorque * load / (engine + load)
                + (engineOmega - shaftOmega) * reducedInertia / Mathf.Max(0.0001f, dt);
            return Mathf.Clamp(torque, -Mathf.Max(0, capacity), Mathf.Max(0, capacity));
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
