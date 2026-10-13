using System;

namespace Rocko
{
    public static class DrivingFeel
    {
        //
        // @description - Map released clutch travel to a smooth bite and full engagement
        // @param {double} released - Normalized released pedal travel
        // @return {double} - Clutch capacity fraction, zero to one
        //
        public static double ClutchBite(double released)
        {
            double x = Math.Max(0, Math.Min(1, (released - 0.15) / 0.75));
            return x * x * (3 - 2 * x);
        }

        //
        // @description - Provide fine brake modulation while retaining full pedal authority
        // @param {double} pedal - Normalized filtered pedal position
        // @return {double} - Normalized hydraulic brake demand
        //
        public static double BrakeDemand(double pedal)
        {
            double x = Math.Max(0, Math.Min(1, pedal));
            return 0.35 * x + 0.65 * x * x;
        }

        //
        // @description - Reduce steering travel progressively as road speed rises
        // @param {double} speed - Signed road speed in metres per second
        // @param {double} speedScale - Speed at which steering travel halves
        // @return {double} - Maximum central steering angle in degrees
        //
        public static double SteeringLimit(double speed, double speedScale)
        {
            double normalized = speed / Math.Max(1, speedScale);
            return Math.Max(4, 33 / (1 + normalized * normalized));
        }

        //
        // @description - Bias fast cornering toward front saturation, with bounded trail-brake rotation
        // @param {bool} front - Whether this tire belongs to the driven front axle
        // @param {double} speed - Signed road speed in metres per second
        // @param {double} driveUsage - Positive axle drive demand divided by front tire capacity
        // @param {double} brake - Applied service brake demand from zero to one
        // @param {double} decelerationG - Measured forward deceleration, positive under braking
        // @return {double} - Axle lateral multiplier; exactly one below 30 mph
        //
        public static double FwdBalance(bool front, double speed, double driveUsage, double brake, double decelerationG)
        {
            double blend = Math.Max(0, Math.Min(1, (Math.Abs(speed) - 13.4112) / 8.9408));
            blend = blend * blend * (3 - 2 * blend); // Smooth transition from 30 to 50 mph. 🚗
            double drive = Math.Max(0, Math.Min(1, driveUsage));
            double lightBrake = Math.Max(0, Math.Min(1, brake / 0.18));
            double heavyBrake = Math.Max(0, Math.Min(1, (brake - 0.4) / 0.3));
            double slowing = Math.Max(0, Math.Min(1, decelerationG / 0.3));
            double correction = lightBrake * (1 - heavyBrake) * slowing;
            // Tire-response calibration only: no added yaw torque or velocity manipulation.
            return front ? 1 - blend * (0.12 + 0.18 * drive) * (1 - 0.55 * correction)
                : 1 + blend * 0.08 * (1 - correction);
        }

        //
        // @description - Apply a bounded lateral-grip reduction for the split longitudinal/PhysX model
        // @param {double} longitudinalForce - Mean signed tire force in newtons
        // @param {double} peakForce - Available peak longitudinal force in newtons
        // @return {double} - Lateral multiplier from 0.90 to one for a loaded tire; zero without load
        //
        public static double LateralGrip(double longitudinalForce, double peakForce)
        {
            if (peakForce <= 0) return 0;
            double usage = Math.Min(1, Math.Abs(longitudinalForce) / peakForce);
            // 🚗 A strict friction-circle multiplier drove PhysX stiffness to zero.
            // This bounded approximation preserves steering authority during peak braking.
            return 1 - 0.10 * usage * usage;
        }
    }
}
