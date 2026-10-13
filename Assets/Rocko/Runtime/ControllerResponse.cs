using System;

namespace Rocko
{
    public static class ControllerResponse
    {
        // @description - Shape one normalized axis with a single deadzone and full endpoint travel
        // @param {double} value - Raw signed control value
        // @param {double} deadzone - Fraction of center travel ignored
        // @param {double} exponent - Response curvature; one is linear
        // @return {double} - Signed shaped demand in [-1, 1]
        public static double Shape(double value, double deadzone, double exponent)
        {
            deadzone = Math.Max(0, Math.Min(0.95, deadzone));
            double magnitude = Math.Min(1, Math.Abs(value));
            return Math.Sign(value) * Math.Pow(Math.Max(0, (magnitude - deadzone) / (1 - deadzone)),
                Math.Max(0.1, exponent));
        }

        // @description - Slew steering with faster centering and a time-correct center crossing
        // @param {double} current - Applied normalized steering
        // @param {double} target - Desired normalized steering
        // @param {double} dt - Physics step seconds
        // @param {double} apply - Increasing lock rate per second
        // @param {double} unwind - Decreasing lock rate per second
        // @return {double} - Updated steering without overshoot
        public static double Steering(double current, double target, double dt,
            double apply, double unwind)
        {
            dt = Math.Max(0, dt);
            apply = Math.Max(0.01, apply);
            unwind = Math.Max(0.01, unwind);
            bool opposite = current * target < 0;
            if (opposite)
            {
                double timeToCenter = Math.Abs(current) / unwind;
                if (dt <= timeToCenter) return Move(current, 0, unwind * dt);
                return Move(0, target, apply * (dt - timeToCenter));
            }
            return Move(current, target, dt * (Math.Abs(target) < Math.Abs(current) ? unwind : apply));
        }

        // @description - Move toward a target without overshoot
        // @param {double} current - Previous value
        // @param {double} target - Desired value
        // @param {double} step - Maximum positive change
        // @return {double} - Bounded updated value
        private static double Move(double current, double target, double step)
        {
            return current + Math.Sign(target - current) * Math.Min(Math.Abs(target - current), step);
        }
    }
}
