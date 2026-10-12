using UnityEngine;

namespace Rocko
{
    public sealed class RockoHUD : MonoBehaviour
    {
        public RockoVehicle car;
        public FirstPersonDriver driver;
        public TestTelemetry telemetry;
        private GUIStyle small, body, heading, number, gearStyle;
        private readonly Color ink = new Color(0.025f, 0.045f, 0.06f, 0.93f);
        private readonly Color mint = new Color(0.55f, 0.93f, 0.78f);
        private readonly Color orange = new Color(1, 0.48f, 0.24f);

        //
        // @description - Draw a responsive development HUD and keyboard/gamepad reference
        // @return {Void} - Renders first-person telemetry, help, and pause controls
        //
        private void OnGUI()
        {
            if (car == null || car.Body == null) return;
            if (small == null) InitializeStyles();
            Matrix4x4 old = GUI.matrix;
            float scale = Mathf.Min(Screen.width / 1600f, Screen.height / 900f);
            float offsetX = (Screen.width - 1600 * scale) * 0.5f;
            float offsetY = (Screen.height - 900 * scale) * 0.5f;
            GUI.matrix = Matrix4x4.TRS(new Vector3(offsetX, offsetY, 0), Quaternion.identity, Vector3.one * scale);
            Fill(new Rect(28, 25, 374, 65), ink);
            Text(new Rect(46, 35, 330, 25), "ROCKO  /  PROVING GROUND", heading, mint);
            Text(new Rect(46, 65, 330, 20), "GTX 16V   ·   DEVELOPMENT BUILD 0.1", small, Color.white);
            Fill(new Rect(1180, 25, 392, 65), ink);
            Text(new Rect(1198, 37, 355, 24), car.Surface.ToUpperInvariant() + "   /   " + (car.clutchAssist ? "CLUTCH ASSIST" : "MANUAL CLUTCH"), body, Color.white);
            Text(new Rect(1198, 65, 350, 20), telemetry.Recording ? "● CSV RECORDING   ·   F5 to stop" : "F1  Controls     F2  Stage run     ESC  Pause", small, telemetry.Recording ? orange : mint);

            if (driver.Driving)
            {
                Fill(new Rect(28, 731, 1544, 141), ink);
                Fill(new Rect(28, 731, 1544 * Mathf.Clamp01(car.RPM / car.spec.redlineRPM), 5), car.RPM > 6200 ? orange : mint);
                Text(new Rect(48, 749, 105, 23), "GEAR", small, mint);
                Text(new Rect(47, 770, 100, 81), car.Gear < 0 ? "R" : car.Gear == 0 ? "N" : car.Gear.ToString(), gearStyle, Color.white);
                Metric(176, "SPEED / MPH", car.SpeedMPH.ToString("0.0"), "Rigidbody ground speed");
                Metric(414, "ENGINE / RPM", car.RPM.ToString("0"), car.EngineRunning ? (car.Shifting ? "Shifting" : car.ClutchLocked ? "Clutch locked" : "Clutch slipping / open") : "STALLED · I / Triangle to restart");
                Metric(652, "ACCELERATION / G", car.LongitudinalG.ToString("+0.00;-0.00;0.00"), "Lateral " + car.LateralG.ToString("+0.00;-0.00;0.00") + " G");
                Metric(890, "WHEEL OUTPUT / HP", car.WheelHP.ToString("0.0"), "Signed driven-axle shaft power");
                Metric(1192, "AXLE TORQUE / LB-FT", (car.AxleTorqueNm * DriveMath.NmToLbFt).ToString("0"), "Crank " + (car.CrankTorqueNm * DriveMath.NmToLbFt).ToString("0") + " lb-ft");
                Fill(new Rect(28, 111, 355, 210), ink);
                Text(new Rect(46, 126, 320, 23), "STANDING-START TEST", body, mint);
                Text(new Rect(46, 157, 320, 25), "0–60 mph       " + Seconds(telemetry.ZeroTo60), body, Color.white);
                Text(new Rect(46, 187, 320, 25), "0–100 km/h    " + Seconds(telemetry.ZeroTo100Kph), body, Color.white);
                Text(new Rect(46, 217, 320, 25), "⅛ mile             " + Seconds(telemetry.Eighth), body, Color.white);
                Text(new Rect(46, 247, 320, 25), "¼ mile             " + Seconds(telemetry.Quarter), body, Color.white);
                Text(new Rect(46, 278, 320, 22), "¼ finish " + telemetry.QuarterFinishMPH.ToString("0.0") + " mph   ·   Max " + telemetry.MaximumMPH.ToString("0.0"), small, mint);
                Fill(new Rect(28, 333, 355, 59), ink);
                Text(new Rect(46, 343, 320, 43), telemetry.Status, small, Color.white);
                Fill(new Rect(28, 404, 355, 140), ink);
                Text(new Rect(46, 414, 320, 22), "WHEEL DIAGNOSTICS · contact " + car.GroundedWheels + "/4", small, mint);
                Text(new Rect(46, 440, 320, 22), "Front RPM  " + car.WheelRPM(0).ToString("0") + " / " + car.WheelRPM(1).ToString("0"), small, Color.white);
                Text(new Rect(46, 466, 320, 22), "Brake Nm F/R  " + car.WheelBrakeTorque(0).ToString("0") + " / " + car.WheelBrakeTorque(2).ToString("0"), small, Color.white);
                Text(new Rect(46, 490, 320, 22), "Throttle " + car.controls.Throttle.ToString("0%") + " · Brake " + car.controls.Brake.ToString("0%"), small, Color.white);
                float shaftRPM = (car.WheelRPM(0) + car.WheelRPM(1)) * 0.5f * car.spec.Ratio(car.Gear);
                Text(new Rect(46, 516, 320, 22), "Clutch " + car.ClutchEngagement.ToString("0%") + " · RPM gap " + Mathf.Abs(car.RPM - shaftRPM).ToString("0"), small, Color.white);
            }
            Fill(new Rect(420, 675, 760, 38), ink);
            Text(new Rect(435, 683, 730, 25), string.IsNullOrEmpty(car.Notice) ? driver.Prompt : car.Notice, body, Color.white);
            if (!driver.Driving)
            {
                Fill(new Rect(796, 446, 8, 8), mint);
                Fill(new Rect(28, 804, 730, 68), ink);
                Text(new Rect(48, 818, 690, 24), "FIRST PERSON ONLY  ·  WALK → ENTER → DRIVE", heading, mint);
                Text(new Rect(48, 849, 690, 20), "R / Create resets to garage     ·     F2 / D-pad up stages the car at the straight", small, Color.white);
            }
            if (driver.ShowHelp || driver.Paused) DrawHelp();
            GUI.matrix = old;
        }

        //
        // @description - Create readable GUI styles once
        // @return {Void} - Initializes font sizes and line wrapping
        //
        private void InitializeStyles()
        {
            small = new GUIStyle(GUI.skin.label) { fontSize = 15, wordWrap = true };
            body = new GUIStyle(GUI.skin.label) { fontSize = 19, wordWrap = true };
            heading = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold };
            number = new GUIStyle(GUI.skin.label) { fontSize = 46, fontStyle = FontStyle.Bold };
            gearStyle = new GUIStyle(number) { fontSize = 66 };
        }

        //
        // @description - Draw one metric with units and a short definition
        // @param {float} x - Horizontal placement in the reference canvas
        // @param {string} label - Metric name and units
        // @param {string} value - Formatted current value
        // @param {string} detail - Clarifying label
        // @return {Void} - Renders the metric
        //
        private void Metric(float x, string label, string value, string detail)
        {
            Text(new Rect(x, 752, 340, 23), label, small, mint);
            Text(new Rect(x, 776, 270, 60), value, number, Color.white);
            Text(new Rect(x, 846, 280, 23), detail, small, new Color(0.72f, 0.78f, 0.79f));
        }

        //
        // @description - Render control bindings and prototype information in one readable panel
        // @return {Void} - Draws help and clickable pause actions
        //
        private void DrawHelp()
        {
            Fill(new Rect(450, 110, 700, 544), new Color(0.025f, 0.045f, 0.06f, 0.98f));
            Text(new Rect(480, 134, 630, 30), driver.Paused ? "PAUSED / ROCKO" : "WELCOME TO ROCKO", heading, mint);
            string[] lines = {
                "Walk / throttle / brake     WASD                         Left stick / R2 / L2",
                "Look around                    Mouse                         Right stick",
                "Enter / exit                       E                                   Cross",
                "Gear up / down               Shift / Ctrl                  R1 / L1",
                "Clutch / handbrake          C / Space                    Square / Circle",
                "Restart stalled engine     I                                    Triangle",
                "Garage reset                    R                                   Create",
                "Stage straight run            F2                                D-pad up",
                "Toggle clutch assistance  F3                                D-pad left",
                "Record telemetry CSV      F5                                D-pad right",
                "Help / center view            F1 / V                          D-pad down / R3"
            };
            for (int i = 0; i < lines.Length; i++) Text(new Rect(480, 184 + i * 29, 638, 28), lines[i], body, Color.white);
            Text(new Rect(480, 516, 636, 47), "Manual gears only. Start in neutral. Shift once for 1st; Ctrl once for R. Clutch assistance is on by default. Specs still require handling validation.", small, mint);
            if (driver.Paused)
            {
                if (GUI.Button(new Rect(480, 582, 298, 42), "RESUME")) driver.SetPaused(false);
                if (GUI.Button(new Rect(800, 582, 298, 42), "RESET TO GARAGE")) { driver.ResetGarage(); driver.SetPaused(false); }
            }
            else Text(new Rect(480, 587, 620, 35), "F1 / D-pad down: help. Esc / Options: pause.", body, orange);
        }

        //
        // @description - Format an unmeasured or recorded split
        // @param {float} value - Time in seconds, negative if absent
        // @return {string} - Readable elapsed time
        //
        private static string Seconds(float value)
        {
            return value < 0 ? "—" : value.ToString("0.00") + " s";
        }

        //
        // @description - Draw a solid rectangle without creating temporary textures
        // @param {Rect} rect - Canvas rectangle
        // @param {Color} color - Fill color
        // @return {Void} - Draws the panel
        //
        private static void Fill(Rect rect, Color color)
        {
            Color old = GUI.color; GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture); GUI.color = old;
        }

        //
        // @description - Draw a text label with temporary tint
        // @param {Rect} rect - Label bounds
        // @param {string} text - Text content
        // @param {GUIStyle} style - Font and layout configuration
        // @param {Color} color - Text color
        // @return {Void} - Draws the label
        //
        private static void Text(Rect rect, string text, GUIStyle style, Color color)
        {
            Color old = GUI.contentColor; GUI.contentColor = color;
            GUI.Label(rect, text, style); GUI.contentColor = old;
        }
    }
}
