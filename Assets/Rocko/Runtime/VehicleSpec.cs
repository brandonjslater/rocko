using UnityEngine;

namespace Rocko
{
    [CreateAssetMenu(menuName = "Rocko/Vehicle specification")]
    public sealed class VehicleSpec : ScriptableObject
    {
        [Header("Supplied specifications — SI units")]
        public float curbMass = 950f;
        public float wheelbase = 2.4f;
        public Vector3 bodyDimensions = new Vector3(1.625f, 1.28f, 4.05f);
        public float peakTorqueNm = 165f;
        public float ratedPowerKW = 102f;
        public float ratedPowerRPM = 6100f;
        [Header("Estimated; verify against gearbox code / tire size")]
        public float driverMass = 75f;
        public float tireRadius = 0.288f;
        public float track = 1.39f;
        public float finalDrive = 3.667f;
        public float reverseRatio = 3.455f;
        public float[] forwardRatios = { 3.455f, 2.118f, 1.444f, 1.129f, 0.912f };
        public float drivelineEfficiency = 0.88f;
        public float dragArea = 0.62f;
        public float rollingCoefficient = 0.013f;
        public float idleRPM = 900f;
        public float redlineRPM = 6800f;
        public float engineInertia = 0.28f;
        public float clutchCapacityNm = 260f;
        [Header("Provisional pedal feel calibration")]
        [Min(0.1f)] public float brakeApplyRate = 4f;
        [Min(0.1f)] public float manualClutchReleaseRate = 3f;
        [Min(0f)] public float frontBrakeTorque = 900f;
        [Min(0f)] public float rearBrakeTorque = 520f;
        public float frontSpring = 26000f;
        public float rearSpring = 21000f;
        public float damper = 2700f;
        public float frontAntiRoll = 4500f;
        public float rearAntiRoll = 2500f;
        [Tooltip("Full-load NET crank torque, not wheel torque. Intermediate points are estimates.")]
        public AnimationCurve torqueCurve = AnimationCurve.Linear(0, 0, 1, 1);

        //
        // @description - Create the supplied engine anchors and estimated intermediate curve. 🚗
        // @return {VehicleSpec} - Runtime specification, editable in the Inspector
        //
        public static VehicleSpec CreateBaseline()
        {
            VehicleSpec s = CreateInstance<VehicleSpec>();
            s.name = "Rocko GTX 16V — provisional calibration";
            s.torqueCurve = new AnimationCurve(
                new Keyframe(0, 0), new Keyframe(900, 72), new Keyframe(1500, 105),
                new Keyframe(2500, 132), new Keyframe(3500, 151),
                new Keyframe(4600, 165), new Keyframe(5500, 163),
                new Keyframe(6100, 102000f / (6100f * Mathf.PI / 30f)),
                new Keyframe(6500, 142), new Keyframe(6800, 123), new Keyframe(7200, 0));
            // Linear tangents avoid invented overshoots between measured anchors.
            for (int i = 0; i < s.torqueCurve.length; i++)
            {
                Keyframe k = s.torqueCurve.keys[i];
                k.inTangent = i == 0 ? 0 : (k.value - s.torqueCurve.keys[i - 1].value) / (k.time - s.torqueCurve.keys[i - 1].time);
                k.outTangent = i == s.torqueCurve.length - 1 ? 0 : (s.torqueCurve.keys[i + 1].value - k.value) / (s.torqueCurve.keys[i + 1].time - k.time);
                s.torqueCurve.MoveKey(i, k);
            }
            return s;
        }

        //
        // @description - Return signed total transmission ratio for R, N, or 1–5
        // @param {int} gear - Minus one for reverse, zero for neutral, one through five forward
        // @return {float} - Gearbox ratio multiplied by final drive
        //
        public float Ratio(int gear)
        {
            if (gear == 0) return 0;
            return (gear < 0 ? -reverseRatio : forwardRatios[Mathf.Clamp(gear, 1, 5) - 1]) * finalDrive;
        }
    }
}
