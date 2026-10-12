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
        // @description - Reserve lateral grip after longitudinal tire force uses the friction budget
        // @param {double} longitudinalForce - Mean signed tire force in newtons
        // @param {double} peakForce - Available peak longitudinal force in newtons
        // @return {double} - Remaining lateral stiffness multiplier from zero to one
        //
        public static double LateralGrip(double longitudinalForce, double peakForce)
        {
            if (peakForce <= 0) return 0;
            double usage = Math.Min(1, Math.Abs(longitudinalForce) / peakForce);
            return Math.Sqrt(Math.Max(0, 1 - usage * usage));
        }
    }
}
