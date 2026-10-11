using UnityEngine;
using UnityEngine.Rendering;

namespace Rocko
{
    public sealed class RockoBootstrap : MonoBehaviour
    {
        public VehicleSpec specification;

        //
        // @description - Build the complete prototype from the included minimal scene
        // @return {Void} - Creates environment, one vehicle, one player, and one gameplay camera
        //
        private void Awake()
        {
            Time.fixedDeltaTime = 0.01f;
            Time.maximumDeltaTime = 0.10f;
            Physics.gravity = new Vector3(0, -9.80665f, 0);
            QualitySettings.vSyncCount = 1;
            QualitySettings.antiAliasing = 4;
            QualitySettings.shadowDistance = 110;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.51f, 0.56f, 0.60f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.00042f;
            RenderSettings.fogColor = new Color(0.61f, 0.72f, 0.78f);
            GameObject sun = new GameObject("Afternoon sun");
            sun.transform.rotation = Quaternion.Euler(43, -32, 0);
            Light light = sun.AddComponent<Light>(); light.type = LightType.Directional;
            light.intensity = 1.35f; light.color = new Color(1, 0.91f, 0.79f); light.shadows = LightShadows.Soft;
            RenderSettings.sun = light;
            RockoInput input = gameObject.AddComponent<RockoInput>();
            ProvingGround.Build();
            VehicleSpec spec = specification != null ? specification : VehicleSpec.CreateBaseline();
            RockoVehicle car = CarAssembly.Build(spec, input, ProvingGround.GarageCar);
            car.gameObject.AddComponent<EngineAudio>().car = car;
            TestTelemetry telemetry = gameObject.AddComponent<TestTelemetry>(); telemetry.car = car;
            GameObject player = new GameObject("Player — first person");
            FirstPersonDriver driver = player.AddComponent<FirstPersonDriver>();
            GameObject camera = new GameObject("First-person camera");
            camera.tag = "MainCamera";
            Camera view = camera.AddComponent<Camera>(); view.nearClipPlane = 0.025f;
            view.farClipPlane = 2800; view.fieldOfView = 70;
            view.clearFlags = CameraClearFlags.SolidColor; view.backgroundColor = RenderSettings.fogColor;
            camera.AddComponent<AudioListener>();
            driver.controls = input; driver.car = car; driver.view = view; driver.telemetry = telemetry;
            RockoHUD hud = gameObject.AddComponent<RockoHUD>(); hud.car = car; hud.driver = driver; hud.telemetry = telemetry;
        }
    }
}
