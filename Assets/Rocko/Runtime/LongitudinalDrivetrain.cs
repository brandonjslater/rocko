using System;

namespace Rocko
{
    // Pure C#: the game and headless regression runner execute this same solver.
    public sealed class LongitudinalDrivetrain
    {
        public double EngineOmega;
        public readonly double[] WheelOmega = new double[4];
        public readonly double[] ContactSpeed = new double[4];
        public readonly double[] NormalLoad = new double[4];
        public readonly double[] Grip = new double[4];
        public readonly double[] BrakeTorque = new double[4];
        public readonly double[] WheelInertia = new double[4];
        public readonly double[] ForceImpulse = new double[4];
        public readonly double[] Slip = new double[4];
        public readonly double[] DirectionDot = new double[16];
        public double ClutchTorque, AxleTorque;
        private readonly double[] force = new double[4];
        private readonly double[] freeWheel = new double[4];

        //
        // @description - Reset engine and wheel rotational state without discarding contact configuration
        // @param {double} engineRPM - Initial engine speed in RPM
        // @return {Void} - Clears wheel rotation and accumulated output
        //
        public void Reset(double engineRPM)
        {
            EngineOmega = engineRPM * Math.PI / 30;
            ClutchTorque = AxleTorque = 0;
            Array.Clear(WheelOmega, 0, 4);
            Array.Clear(ForceImpulse, 0, 4);
            Array.Clear(Slip, 0, 4);
        }

        //
        // @description - Evaluate the longitudinal tire curve using relative contact speed
        // @param {double} slip - Signed longitudinal slip ratio
        // @return {double} - Signed friction coefficient, before surface scaling
        //
        public static double TireCoefficient(double slip)
        {
            double s = Math.Abs(slip);
            double coefficient = s <= 0.10 ? 1.03 * s / 0.10
                : s < 0.55 ? 1.03 - 0.27 * (s - 0.10) / 0.45 : 0.76;
            return Math.Sign(slip) * coefficient;
        }

        //
        // @description - Advance engine, clutch, wheels and tire impulses in the same substeps
        // @param {double} dt - Outer physics step in seconds
        // @param {double} engineTorque - Net crank torque in Nm during this outer step
        // @param {double} engineInertia - Flywheel and engine inertia in kg m²
        // @param {double} capacity - Available clutch torque in Nm
        // @param {double} ratio - Signed gearbox times final-drive ratio; zero means neutral
        // @param {double} efficiency - Driveline mechanical efficiency from zero to one
        // @param {double} mass - Chassis mass in kg
        // @param {double} radius - Tire radius in meters
        // @return {Void} - Updates angular speeds, mean torques and per-contact force impulses
        //
        public void Step(double dt, double engineTorque, double engineInertia, double capacity,
            double ratio, double efficiency, double mass, double radius)
        {
            if (dt <= 0) return;
            // Resolve the stiff low-speed tire response inside the same clutch integration.
            int count = Math.Max(1, (int)Math.Ceiling(dt / 0.0001));
            double h = dt / count;
            double engineI = Math.Max(0.001, engineInertia);
            double vehicleMass = Math.Max(1, mass);
            double loss = Math.Max(0.01, Math.Min(1, efficiency));
            double limit = ratio == 0 ? 0 : Math.Max(0, capacity);
            ClutchTorque = AxleTorque = 0;
            Array.Clear(ForceImpulse, 0, 4);
            for (int step = 0; step < count; step++)
            {
                for (int i = 0; i < 4; i++)
                {
                    double surfaceSpeed = WheelOmega[i] * radius;
                    double denominator = Math.Max(0.5, Math.Max(Math.Abs(ContactSpeed[i]), Math.Abs(surfaceSpeed)));
                    Slip[i] = (surfaceSpeed - ContactSpeed[i]) / denominator;
                    force[i] = NormalLoad[i] > 0 ? TireCoefficient(Slip[i]) * NormalLoad[i] * Grip[i] : 0;
                    double inertia = Math.Max(0.01, WheelInertia[i]);
                    // Brake impulses are projected below so they cannot reverse a stopped wheel.
                    freeWheel[i] = WheelOmega[i] - (force[i] * radius + 0.3 * WheelOmega[i]) / inertia * h;
                }
                double engineFree = EngineOmega + engineTorque / engineI * h;
                double shaftFree = (freeWheel[0] + freeWheel[1]) * 0.5 * ratio;
                double delta = engineFree - shaftFree;
                double transfer = delta >= 0 ? ratio * loss : ratio / loss;
                double inverseLoad = ratio * transfer * 0.25 *
                    (1 / Math.Max(0.01, WheelInertia[0]) + 1 / Math.Max(0.01, WheelInertia[1]));
                // Equal-and-opposite clutch impulse solves the next two speeds, not stale samples.
                double impulse = delta / (1 / engineI + inverseLoad);
                impulse = Math.Max(-limit * h, Math.Min(limit * h, impulse));
                EngineOmega = Math.Max(0, engineFree - impulse / engineI);
                for (int i = 0; i < 4; i++)
                {
                    double inertia = Math.Max(0.01, WheelInertia[i]);
                    double beforeBrake = freeWheel[i] + (i < 2 ? impulse * transfer * 0.5 / inertia : 0);
                    double brakeDelta = Math.Max(0, BrakeTorque[i]) * h / inertia;
                    WheelOmega[i] = Math.Sign(beforeBrake) * Math.Max(0, Math.Abs(beforeBrake) - brakeDelta);
                    ForceImpulse[i] += force[i] * h;
                }
                // Predict translation within this outer tick. Unity applies these exact impulses
                // at the contact points afterward, including their pitching/yawing moments.
                for (int i = 0; i < 4; i++)
                {
                    double acceleration = 0;
                    for (int j = 0; j < 4; j++) acceleration += DirectionDot[i * 4 + j] * force[j] / vehicleMass;
                    ContactSpeed[i] += acceleration * h;
                }
                ClutchTorque += impulse / dt;
                AxleTorque += impulse * transfer / dt;
            }
        }
    }
}
