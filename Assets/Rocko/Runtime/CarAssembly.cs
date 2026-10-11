using UnityEngine;

namespace Rocko
{
    public static class CarAssembly
    {
        //
        // @description - Assemble a dimensioned Scirocco II-inspired mesh and its separate physics chassis
        // @param {VehicleSpec} spec - Engine, dimensions, and chassis calibration
        // @param {RockoInput} input - Shared player input
        // @param {Vector3} spawn - World position, with local ground at y zero
        // @return {RockoVehicle} - Fully wired prototype car
        //
        public static RockoVehicle Build(VehicleSpec spec, RockoInput input, Vector3 spawn)
        {
            GameObject root = new GameObject("Rocko · Scirocco II GTX 16V");
            root.transform.position = spawn;
            Rigidbody body = root.AddComponent<Rigidbody>();
            // 🚗 WheelCollider calculates supported mass when it is created.
            body.mass = spec.curbMass + spec.driverMass;
            body.centerOfMass = new Vector3(0, 0.48f, 0.24f);
            RockoVehicle car = root.AddComponent<RockoVehicle>();
            car.spec = spec; car.controls = input;
            Transform t = root.transform;
            Material blue = Geometry.Material("Ink blue paint", new Color(0.055f, 0.10f, 0.22f), 0.73f);
            Material black = Geometry.Material("Charcoal trim", new Color(0.035f, 0.038f, 0.042f));
            Material rubber = Geometry.Material("Tire rubber", new Color(0.02f, 0.022f, 0.025f), 0.07f);
            Material silver = Geometry.Material("Alloy", new Color(0.6f, 0.64f, 0.66f), 0.7f);
            Material red = Geometry.Material("Tail lamps", new Color(0.55f, 0.022f, 0.025f), 0.7f);
            Material amber = Geometry.Material("Indicators", new Color(0.95f, 0.35f, 0.018f), 0.6f);
            Material white = Geometry.Material("Headlamp lenses", new Color(0.83f, 0.89f, 0.87f), 0.8f);
            Material cloth = Geometry.Material("Grey seat fabric", new Color(0.16f, 0.18f, 0.19f), 0.05f);
            Material glass = new Material(Resources.Load<Shader>("RockoGlass"));
            glass.color = new Color(0.25f, 0.42f, 0.45f, 0.09f);

            // Colliders deliberately separate from the visual mesh and wheel cut-outs.
            BoxCollider chassis = root.AddComponent<BoxCollider>();
            chassis.center = new Vector3(0, 0.44f, 0); chassis.size = new Vector3(1.24f, 0.38f, 3.85f);
            BoxCollider cabin = root.AddComponent<BoxCollider>();
            cabin.center = new Vector3(0, 0.94f, -0.48f); cabin.size = new Vector3(1.30f, 0.53f, 1.4f);
            Geometry.Box("Cabin floor", t, new Vector3(0, 0.26f, -0.3f), new Vector3(1.28f, 0.06f, 2.65f), black);

            // Side panels have actual open wheel arches, rather than wheels buried in a box. 🚗
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * 0.8125f;
                for (int j = 0; j < 160; j++)
                {
                    float z0 = -2.0f + j * 0.025f, z1 = z0 + 0.025f;
                    float low0 = ArchHeight(z0), low1 = ArchHeight(z1);
                    float top0 = BeltHeight(z0), top1 = BeltHeight(z1);
                    Geometry.Quad("Body panel", t, new Vector3(x, low0, z0), new Vector3(x, top0, z0),
                        new Vector3(x, top1, z1), new Vector3(x, low1, z1), blue);
                }
                Geometry.Box("Door rub strip", t, new Vector3(x * 1.004f, 0.59f, 0), new Vector3(0.023f, 0.065f, 1.65f), black);
                Geometry.Box("Door handle", t, new Vector3(x * 1.01f, 0.735f, -0.48f), new Vector3(0.035f, 0.035f, 0.16f), black);
                Geometry.Box("Side skirt", t, new Vector3(side * 0.8f, 0.20f, 0), new Vector3(0.045f, 0.075f, 1.62f), blue);
                Geometry.Box("Mirror shell", t, new Vector3(side * 0.87f, 0.84f, 0.48f), new Vector3(0.16f, 0.12f, 0.23f), blue);
                Geometry.Box("Mirror insert (static)", t, new Vector3(side * 0.87f, 0.84f, 0.359f), new Vector3(0.12f, 0.09f, 0.006f), silver);
                Vector3 a = new Vector3(side * 0.765f, 0.79f, 0.70f);
                Vector3 b = new Vector3(side * 0.615f, 1.24f, 0.05f);
                Vector3 c = new Vector3(side * 0.615f, 1.24f, -0.88f);
                Vector3 d = new Vector3(side * 0.77f, 0.80f, -1.89f);
                Geometry.Strut("A pillar", t, a, b, 0.055f, blue);
                Geometry.Strut("Roof rail", t, b, c, 0.06f, blue);
                Geometry.Strut("C pillar", t, c, d, 0.09f, blue);
                Geometry.Strut("Window sill", t, a, d, 0.045f, black);
                Geometry.Strut("B pillar", t, new Vector3(side * 0.765f, 0.795f, -0.55f), new Vector3(side * 0.615f, 1.24f, -0.55f), 0.055f, black);
                Geometry.Quad("Side glass", t, a, b, c, d, glass);
                Geometry.Box("Interior door card", t, new Vector3(side * 0.73f, 0.58f, -0.1f), new Vector3(0.04f, 0.30f, 1.20f), black);
            }
            Geometry.Quad("Sloping bonnet", t, new Vector3(-0.8f, 0.79f, 0.70f), new Vector3(0.8f, 0.79f, 0.70f), new Vector3(0.8f, 0.635f, 2.0f), new Vector3(-0.8f, 0.635f, 2.0f), blue);
            Geometry.Quad("Windscreen", t, new Vector3(-0.765f, 0.79f, 0.70f), new Vector3(0.765f, 0.79f, 0.70f), new Vector3(0.615f, 1.24f, 0.05f), new Vector3(-0.615f, 1.24f, 0.05f), glass);
            Geometry.Box("Roof", t, new Vector3(0, 1.255f, -0.415f), new Vector3(1.26f, 0.05f, 0.99f), blue);
            Geometry.Quad("Hatch glass", t, new Vector3(-0.615f, 1.24f, -0.88f), new Vector3(0.615f, 1.24f, -0.88f), new Vector3(0.77f, 0.80f, -1.89f), new Vector3(-0.77f, 0.80f, -1.89f), glass);
            Geometry.Box("Hatch lip", t, new Vector3(0, 0.77f, -1.95f), new Vector3(1.58f, 0.09f, 0.15f), blue);
            Geometry.Box("Rear spoiler", t, new Vector3(0, 0.93f, -1.68f), new Vector3(1.62f, 0.045f, 0.20f), black);
            foreach (float x in new[] { -0.67f, 0.67f }) Geometry.Box("Spoiler pedestal", t, new Vector3(x, 0.875f, -1.69f), new Vector3(0.07f, 0.10f, 0.12f), black);
            Geometry.Box("Front fascia", t, new Vector3(0, 0.525f, 1.985f), new Vector3(1.60f, 0.22f, 0.03f), black);
            Geometry.Box("Rear panel", t, new Vector3(0, 0.575f, -1.985f), new Vector3(1.60f, 0.34f, 0.03f), blue);
            foreach (float z in new[] { -1.99f, 1.99f }) Geometry.Box("Black bumper", t, new Vector3(0, 0.35f, z), new Vector3(1.625f, 0.11f, 0.07f), black);
            for (int side = -1; side <= 1; side += 2)
            {
                Geometry.Box("Rectangular headlamp", t, new Vector3(side * 0.52f, 0.53f, 2.008f), new Vector3(0.39f, 0.16f, 0.012f), white);
                Geometry.Box("Front indicator", t, new Vector3(side * 0.765f, 0.53f, 2.008f), new Vector3(0.07f, 0.17f, 0.012f), amber);
                Geometry.Box("Rear red lamp", t, new Vector3(side * 0.49f, 0.59f, -2.008f), new Vector3(0.37f, 0.19f, 0.012f), red);
                Geometry.Box("Rear indicator", t, new Vector3(side * 0.72f, 0.59f, -2.008f), new Vector3(0.10f, 0.19f, 0.012f), amber);
                Geometry.Box("Reverse lamp", t, new Vector3(side * 0.275f, 0.59f, -2.008f), new Vector3(0.06f, 0.19f, 0.012f), white);
            }
            for (int j = 0; j < 5; j++) Geometry.Box("Grille slat", t, new Vector3(0, 0.46f + j * 0.032f, 2.012f), new Vector3(0.58f, 0.01f, 0.008f), silver);
            Geometry.Ring("Grille roundel", t, new Vector3(0, 0.535f, 2.023f), 0.04f, 0.005f, silver, 24);
            Geometry.Box("Rear plate", t, new Vector3(0, 0.59f, -2.016f), new Vector3(0.43f, 0.15f, 0.012f), white);
            Geometry.Label("ROCKO", t, new Vector3(0, 0.59f, -2.025f), 0.075f).GetComponent<TextMesh>().color = Color.black;
            Geometry.Strut("Wiper", t, new Vector3(-0.45f, 0.805f, 0.68f), new Vector3(0.06f, 0.82f, 0.66f), 0.012f, black);
            Geometry.Strut("Wiper", t, new Vector3(0.1f, 0.805f, 0.68f), new Vector3(0.61f, 0.82f, 0.66f), 0.012f, black);

            BuildCabin(car, black, cloth, silver);
            car.wheels = new WheelCollider[4]; car.wheelVisuals = new Transform[4];
            for (int i = 0; i < 4; i++)
            {
                int side = i % 2 == 0 ? -1 : 1;
                GameObject w = new GameObject(i < 2 ? (side < 0 ? "Front left wheel" : "Front right wheel") : (side < 0 ? "Rear left wheel" : "Rear right wheel"));
                w.transform.SetParent(t, false);
                w.transform.localPosition = new Vector3(side * spec.track / 2, spec.tireRadius + 0.11f, i < 2 ? spec.wheelbase / 2 : -spec.wheelbase / 2);
                WheelCollider wheel = w.AddComponent<WheelCollider>();
                wheel.radius = spec.tireRadius; wheel.mass = 16; wheel.suspensionDistance = 0.22f;
                wheel.forceAppPointDistance = 0.12f; wheel.wheelDampingRate = 0.3f;
                JointSpring spring = wheel.suspensionSpring;
                spring.spring = i < 2 ? spec.frontSpring : spec.rearSpring;
                spring.damper = spec.damper; spring.targetPosition = 0.5f; wheel.suspensionSpring = spring;
                wheel.forwardFriction = new WheelFrictionCurve { extremumSlip = 0.10f, extremumValue = 1.03f, asymptoteSlip = 0.55f, asymptoteValue = 0.76f, stiffness = 1 };
                wheel.sidewaysFriction = new WheelFrictionCurve { extremumSlip = 0.12f, extremumValue = 1.0f, asymptoteSlip = 0.5f, asymptoteValue = 0.72f, stiffness = 1 };
                car.wheels[i] = wheel;
                Transform visual = new GameObject("Wheel visual").transform; visual.SetParent(t, false);
                Transform rim = new GameObject("Tire and alloy").transform; rim.SetParent(visual, false); rim.localRotation = Quaternion.Euler(0, 90, 0);
                GameObject tire = Geometry.Ring("Tire", rim, Vector3.zero, spec.tireRadius - 0.064f, 0.064f, rubber);
                tire.transform.localScale = new Vector3(1, 1, 1.42f);
                for (int face = -1; face <= 1; face += 2)
                {
                    Geometry.Ring("Alloy rim", rim, new Vector3(0, 0, face * 0.07f), 0.164f, 0.014f, silver);
                    Geometry.Ring("Hub ring", rim, new Vector3(0, 0, face * 0.071f), 0.05f, 0.014f, silver, 24);
                    for (int j = 0; j < 12; j++)
                    {
                        float a = j * Mathf.PI * 2 / 12;
                        Geometry.Strut("Alloy spoke", rim, new Vector3(Mathf.Cos(a) * 0.062f, Mathf.Sin(a) * 0.062f, face * 0.072f), new Vector3(Mathf.Cos(a) * 0.15f, Mathf.Sin(a) * 0.15f, face * 0.072f), 0.018f, silver);
                    }
                }
                car.wheelVisuals[i] = visual;
            }
            return car;
        }

        //
        // @description - Cut semicircular wheel clearances into the lower side panels
        // @param {float} z - Longitudinal body coordinate
        // @return {float} - Lower panel edge height
        //
        private static float ArchHeight(float z)
        {
            float distance = Mathf.Min(Mathf.Abs(z - 1.2f), Mathf.Abs(z + 1.2f));
            return distance < 0.355f ? 0.288f + Mathf.Sqrt(0.355f * 0.355f - distance * distance) : 0.23f;
        }

        //
        // @description - Define the sloping bonnet and beltline silhouette
        // @param {float} z - Longitudinal body coordinate
        // @return {float} - Upper panel height
        //
        private static float BeltHeight(float z)
        {
            return z <= 0.7f ? 0.79f : Mathf.Lerp(0.79f, 0.635f, (z - 0.7f) / 1.3f);
        }

        //
        // @description - Build a provisional left-hand-drive first-person interior
        // @param {RockoVehicle} car - Car receiving the seat and steering pivots
        // @param {Material} black - Dashboard material
        // @param {Material} cloth - Seat material
        // @param {Material} silver - Trim material
        // @return {Void} - Adds cabin geometry and view anchor
        //
        private static void BuildCabin(RockoVehicle car, Material black, Material cloth, Material silver)
        {
            Transform t = car.transform;
            Geometry.Box("Dashboard", t, new Vector3(0, 0.70f, 0.49f), new Vector3(1.39f, 0.17f, 0.38f), black);
            Geometry.Box("Instrument binnacle", t, new Vector3(-0.34f, 0.79f, 0.37f), new Vector3(0.49f, 0.14f, 0.15f), black);
            Geometry.Box("Console", t, new Vector3(0.10f, 0.48f, 0.18f), new Vector3(0.25f, 0.32f, 0.40f), black);
            Geometry.Strut("Gear stick", t, new Vector3(0.10f, 0.41f, -0.05f), new Vector3(0.10f, 0.59f, -0.04f), 0.022f, silver);
            Geometry.Box("Shift knob", t, new Vector3(0.10f, 0.60f, -0.04f), new Vector3(0.055f, 0.055f, 0.055f), black);
            foreach (float x in new[] { -0.35f, 0.35f })
            {
                Geometry.Box("Front seat cushion", t, new Vector3(x, 0.40f, -0.39f), new Vector3(0.49f, 0.13f, 0.49f), cloth);
                GameObject back = Geometry.Box("Front seat back", t, new Vector3(x, 0.64f, -0.66f), new Vector3(0.49f, 0.47f, 0.13f), cloth);
                back.transform.localRotation = Quaternion.Euler(-12, 0, 0);
                Geometry.Box("Headrest", t, new Vector3(x, 0.95f, -0.71f), new Vector3(0.28f, 0.17f, 0.10f), cloth);
            }
            Geometry.Box("Rear bench", t, new Vector3(0, 0.46f, -1.24f), new Vector3(1.23f, 0.18f, 0.48f), cloth);
            Geometry.Box("Rear backrest", t, new Vector3(0, 0.66f, -1.48f), new Vector3(1.23f, 0.35f, 0.15f), cloth);
            car.steeringWheel = new GameObject("Steering wheel pivot").transform;
            car.steeringWheel.SetParent(t, false); car.steeringWheel.localPosition = new Vector3(-0.34f, 0.77f, 0.23f);
            Geometry.Ring("Steering rim", car.steeringWheel, Vector3.zero, 0.165f, 0.014f, black);
            Geometry.Box("Steering hub", car.steeringWheel, Vector3.zero, new Vector3(0.12f, 0.08f, 0.055f), black);
            Geometry.Box("Wheel spoke", car.steeringWheel, Vector3.zero, new Vector3(0.31f, 0.03f, 0.023f), black);
            Geometry.Box("Wheel spoke", car.steeringWheel, new Vector3(0, -0.08f, 0), new Vector3(0.035f, 0.16f, 0.023f), black);
            car.seat = new GameObject("Driver eye — first person only").transform;
            car.seat.SetParent(t, false); car.seat.localPosition = new Vector3(-0.34f, 1.055f, -0.29f);
        }
    }
}
