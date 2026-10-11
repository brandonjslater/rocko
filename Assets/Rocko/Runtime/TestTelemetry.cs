using System;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace Rocko
{
    [DefaultExecutionOrder(200)]
    public sealed class TestTelemetry : MonoBehaviour
    {
        public RockoVehicle car;
        public float ZeroTo60 { get; private set; } = -1;
        public float ZeroTo100Kph { get; private set; } = -1;
        public float Eighth { get; private set; } = -1;
        public float Quarter { get; private set; } = -1;
        public float QuarterFinishMPH { get; private set; }
        public float Mile { get; private set; } = -1;
        public float MaximumMPH { get; private set; }
        public float Elapsed { get; private set; }
        public float Distance { get; private set; }
        public bool Running { get; private set; }
        public bool Armed { get; private set; }
        public bool Recording { get { return writer != null; } }
        public string Status { get; private set; } = "F2 / D-pad up: stage a standing-start run";
        public string LastFile { get; private set; } = "";
        private Vector3 origin;
        private float previousSpeed, previousDistance, previousElapsed, sampleTime, flushTime;
        private StreamWriter writer;

        //
        // @description - Clear previous measurements and arm timing at the staging position
        // @return {Void} - Clears every split and resets interpolation history
        //
        public void ResetRun()
        {
            ZeroTo60 = ZeroTo100Kph = Eighth = Quarter = Mile = -1;
            Elapsed = Distance = MaximumMPH = QuarterFinishMPH = 0;
            previousSpeed = previousDistance = previousElapsed = 0;
            Running = false;
            origin = car.transform.position;
            Armed = Mathf.Abs(origin.x) < 5 && origin.z >= -20 && origin.z <= 0;
            Status = Armed ? "ARMED · timing starts at first forward movement" : "F2 / D-pad up: stage a standing-start run";
        }

        //
        // @description - Measure standing-start splits on the straight and record physical channels at 20 Hz
        // @return {Void} - Updates run results and optional CSV output
        //
        private void FixedUpdate()
        {
            if (car.Body == null) return;
            float speed = car.SpeedMPH;
            MaximumMPH = Mathf.Max(MaximumMPH, speed);
            if (Armed && !Running && car.ForwardSpeed > 0.05f)
            { Running = true; Armed = false; Status = "RUNNING · no rollout correction"; }
            if (Running)
            {
                Elapsed += Time.fixedDeltaTime;
                Distance = car.transform.position.z - origin.z;
                if (Mathf.Abs(car.transform.position.x) > 7.2f || car.ForwardSpeed < -0.5f)
                {
                    Running = false; Status = "INVALID · left straight or reversed; F2 to retry";
                    ZeroTo60 = ZeroTo100Kph = Eighth = Quarter = Mile = -1;
                }
                else
                {
                    if (ZeroTo60 < 0 && speed >= 60) ZeroTo60 = Crossing(previousSpeed, speed, 60);
                    if (ZeroTo100Kph < 0 && speed >= 62.137119f) ZeroTo100Kph = Crossing(previousSpeed, speed, 62.137119f);
                    if (Eighth < 0 && Distance >= DriveMath.MetersPerMile / 8) Eighth = Crossing(previousDistance, Distance, DriveMath.MetersPerMile / 8);
                    if (Quarter < 0 && Distance >= DriveMath.MetersPerMile / 4)
                    {
                        Quarter = Crossing(previousDistance, Distance, DriveMath.MetersPerMile / 4);
                        float u = Mathf.InverseLerp(previousDistance, Distance, DriveMath.MetersPerMile / 4);
                        QuarterFinishMPH = Mathf.Lerp(previousSpeed, speed, u);
                    }
                    if (Mile < 0 && Distance >= DriveMath.MetersPerMile)
                    { Mile = Crossing(previousDistance, Distance, DriveMath.MetersPerMile); Running = false; Status = "MILE COMPLETE · brake into runoff"; }
                }
                previousSpeed = speed; previousDistance = Distance; previousElapsed = Elapsed;
            }
            if (writer == null) return;
            sampleTime += Time.fixedDeltaTime; flushTime += Time.fixedDeltaTime;
            if (sampleTime < 0.05f) return;
            sampleTime -= 0.05f;
            try
            {
                writer.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "{0:F3},{1:F3},{2:F3},{3:F3},{4:F3},{5},{6:F1},{7:F3},{8:F3},{9:F2},{10:F2},{11:F2},{12:F3},{13:F3},{14},{15}",
                    Time.fixedTime, car.transform.position.x, car.transform.position.y, car.transform.position.z, speed,
                    car.Gear, car.RPM, car.LongitudinalG, car.LateralG, car.WheelHP, car.AxleTorqueNm * DriveMath.NmToLbFt,
                    car.CrankTorqueNm * DriveMath.NmToLbFt, car.ClutchEngagement, car.MaxDrivenSlip, car.Surface, car.EngineRunning));
                if (flushTime >= 1) { writer.Flush(); flushTime = 0; }
            }
            catch (IOException ex) { StopRecording(); car.SetNotice("Telemetry write failed: " + ex.Message); }
        }

        //
        // @description - Interpolate a split crossing between adjacent physics samples
        // @param {float} before - Previous sampled value
        // @param {float} after - Current sampled value
        // @param {float} target - Threshold value
        // @return {float} - Estimated crossing time in seconds
        //
        private float Crossing(float before, float after, float target)
        {
            return Mathf.Lerp(previousElapsed, Elapsed, Mathf.InverseLerp(before, after, target));
        }

        //
        // @description - Open or close a local CSV capture without requiring an external service
        // @return {Void} - Updates recording state and communicates the output path
        //
        public void ToggleRecording()
        {
            if (writer != null) { StopRecording(); car.SetNotice("CSV saved: " + LastFile); return; }
            try
            {
                string directory = Path.Combine(Application.persistentDataPath, "Telemetry");
                Directory.CreateDirectory(directory);
                LastFile = Path.Combine(directory, "Rocko_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".csv");
                writer = new StreamWriter(LastFile, false);
                writer.WriteLine("time_s,x_m,y_m,z_m,speed_mph,gear,rpm,longitudinal_g,lateral_g,wheel_hp,axle_lbft,crank_lbft,clutch_engagement,front_slip,surface,engine_running");
                sampleTime = flushTime = 0;
                car.SetNotice("Recording CSV · F5 / D-pad right to stop");
                Debug.Log("Rocko telemetry: " + LastFile);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            { writer = null; car.SetNotice("Cannot create telemetry file: " + ex.Message); }
        }

        //
        // @description - Flush and release the CSV file safely
        // @return {Void} - Ends recording
        //
        private void StopRecording()
        {
            StreamWriter openWriter = writer; writer = null;
            if (openWriter == null) return;
            try { openWriter.Dispose(); }
            catch (IOException ex) { Debug.LogWarning("Rocko telemetry close: " + ex.Message); }
        }

        //
        // @description - Persist pending samples when the scene or application closes
        // @return {Void} - Releases the recording file
        //
        private void OnDisable()
        {
            StopRecording();
        }
    }
}
