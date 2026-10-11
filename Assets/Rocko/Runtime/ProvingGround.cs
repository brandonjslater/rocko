using System.Collections.Generic;
using UnityEngine;

namespace Rocko
{
    public static class ProvingGround
    {
        public static readonly Vector3 GarageCar = new Vector3(-14, 0.08f, -18);
        public static readonly Vector3 GaragePlayer = new Vector3(-16.3f, 0.12f, -18);
        public static readonly Vector3 StraightCar = new Vector3(0, 0.08f, -8);

        //
        // @description - Create a metric sandbox with a measured mile and independent handling routes
        // @return {Transform} - Generated environment root
        //
        public static Transform Build()
        {
            Transform root = new GameObject("Rocko proving ground — 1 unit = 1 meter").transform;
            Material asphalt = Geometry.Material("Asphalt", new Color(0.12f, 0.14f, 0.16f), 0.08f);
            Material soil = Geometry.Material("Packed dirt", new Color(0.36f, 0.25f, 0.15f), 0.05f);
            Material grass = Geometry.Material("Dry meadow", new Color(0.30f, 0.36f, 0.22f), 0.05f);
            Material stripe = Geometry.Material("Warm road markings", new Color(0.83f, 0.79f, 0.61f));
            Material dark = Geometry.Material("Garage charcoal", new Color(0.07f, 0.10f, 0.12f));
            Material orange = Geometry.Material("Safety orange", new Color(0.98f, 0.33f, 0.10f));
            Material concrete = Geometry.Material("Concrete", new Color(0.48f, 0.50f, 0.48f));
            Surface(Geometry.Box("Ground", root, new Vector3(0, -0.60f, 1020), new Vector3(1200, 1.08f, 2500), grass, true), "Grass", 0.52f, 0.04f);
            Surface(Geometry.Box("Garage apron", root, new Vector3(-8, -0.15f, -40), new Vector3(90, 0.30f, 80), asphalt, true), "Asphalt", 1, 0.013f);
            float roadLength = DriveMath.MetersPerMile + 520;
            Surface(Geometry.Box("Mile straight + 520 m braking runoff", root, new Vector3(0, -0.15f, roadLength / 2), new Vector3(16, 0.30f, roadLength), asphalt, true), "Asphalt", 1, 0.013f);
            foreach (float x in new[] { -7.4f, 7.4f }) Geometry.Box("Edge line", root, new Vector3(x, 0.004f, roadLength / 2), new Vector3(0.12f, 0.008f, roadLength), stripe);
            for (float z = 5; z < roadLength; z += 15)
                Geometry.Box("Dashed center line", root, new Vector3(0, 0.006f, z), new Vector3(0.10f, 0.008f, 5), stripe);
            for (int i = 0; i <= 8; i++)
            {
                float z = i * DriveMath.MetersPerMile / 8;
                Geometry.Box("Measured timing stripe", root, new Vector3(0, 0.012f, z), new Vector3(15, 0.012f, 0.25f), i == 0 || i == 8 ? orange : stripe);
                string name = i == 0 ? "START · 0 ft" : i == 8 ? "1 MILE · 5280 ft" : i + "/8 MILE · " + (i * 660) + " ft";
                Sign(root, new Vector3(-10, 0, z), name, dark, orange);
            }
            Sign(root, new Vector3(-10, 0, DriveMath.MetersPerMile + 20), "BRAKING RUNOFF\n520 m · no speed limiter", dark, orange);
            Sign(root, new Vector3(-8, 0, roadLength - 45), "END OF PAVEMENT\nTURN BACK", dark, orange);
            Geometry.Box("Runoff end barrier", root, new Vector3(0, 0.65f, roadLength + 8), new Vector3(25, 1.3f, 0.5f), orange, true);

            // Constant-height skidpad and wide connectors are separate from the timed lane.
            Surface(Geometry.Box("Flat maneuvering pad", root, new Vector3(92, -0.15f, 165), new Vector3(130, 0.30f, 130), asphalt, true), "Asphalt", 1, 0.013f);
            Ribbon(root, "Skidpad access", new[] { new Vector3(4, 0.008f, 80), new Vector3(25, 0.008f, 90), new Vector3(60, 0.008f, 130) }, 12, asphalt, "Asphalt", 1, 0.013f);
            for (int i = 0; i < 48; i++)
            {
                float a = i * Mathf.PI * 2 / 48;
                Geometry.Box("Skidpad radius marker — 40 m", root, new Vector3(92 + Mathf.Cos(a) * 40, 0.015f, 165 + Mathf.Sin(a) * 40), new Vector3(0.5f, 0.01f, 0.5f), stripe);
            }
            for (int i = 0; i < 9; i++) Cone(root, new Vector3(62 + (i % 2) * 6, 0, 120 + i * 10), orange, dark);
            Sign(root, new Vector3(29, 0, 115), "FLAT PAD\n40 m circle · slalom", dark, orange);

            List<Vector3> bends = new List<Vector3>();
            bends.Add(new Vector3(-5, 0.008f, 85));
            bends.Add(new Vector3(-50, 0.008f, 90));
            for (int i = 0; i <= 100; i++)
            {
                float s = i / 100f;
                bends.Add(new Vector3(-130 + 42 * Mathf.Sin(s * Mathf.PI * 4), 0.008f, 145 + s * 520));
            }
            bends.Add(new Vector3(-65, 0.008f, 725)); bends.Add(new Vector3(-5, 0.008f, 750));
            Ribbon(root, "Flat asphalt corner course", bends.ToArray(), 10, asphalt, "Asphalt", 1, 0.013f);
            Sign(root, new Vector3(-34, 0, 85), "HANDLING LOOP\nFlat bends · asphalt", dark, orange);

            List<Vector3> hills = new List<Vector3>();
            hills.Add(new Vector3(145, 0.008f, 210));
            for (int i = 0; i <= 160; i++)
            {
                float s = i / 160f;
                hills.Add(new Vector3(195 + 35 * Mathf.Sin(s * Mathf.PI * 3), 12 * Mathf.Pow(Mathf.Sin(s * Mathf.PI * 2), 2) + 0.008f, 260 + s * 650));
            }
            hills.Add(new Vector3(115, 0.008f, 960)); hills.Add(new Vector3(6, 0.008f, 960));
            Ribbon(root, "Elevation route", hills.ToArray(), 10, asphalt, "Asphalt", 1, 0.013f, soil);
            Sign(root, new Vector3(146, 0, 228), "HILL ROUTE\nTwo 12 m crests · asphalt", dark, orange);

            List<Vector3> dirt = new List<Vector3>();
            dirt.Add(new Vector3(145, 0.012f, 125));
            dirt.Add(new Vector3(240, 0.012f, 135));
            dirt.Add(new Vector3(290, 0.012f, 145));
            for (int i = 0; i <= 120; i++)
            {
                float a = i * Mathf.PI * 2 / 120;
                dirt.Add(new Vector3(375 - 105 * Mathf.Cos(a), 0.02f + 0.10f * Mathf.Pow(Mathf.Sin(a * 12), 2), 350 + 200 * Mathf.Sin(a)));
            }
            Ribbon(root, "Packed dirt loop", dirt.ToArray(), 13, soil, "Dirt", 0.60f, 0.035f, soil);
            Sign(root, new Vector3(170, 0, 165), "DIRT LOOP\nReduced grip · shallow undulations", dark, orange);

            // Open-front starter garage and a sheltered foot spawn.
            Geometry.Box("Garage rear wall", root, new Vector3(-14, 2.7f, -25), new Vector3(10, 5.4f, 0.30f), dark, true);
            Geometry.Box("Garage left wall", root, new Vector3(-19, 2.7f, -18), new Vector3(0.30f, 5.4f, 14), dark, true);
            Geometry.Box("Garage right wall", root, new Vector3(-9, 2.7f, -18), new Vector3(0.30f, 5.4f, 14), dark, true);
            Geometry.Box("Garage roof", root, new Vector3(-14, 5.4f, -18), new Vector3(10.3f, 0.25f, 14.3f), concrete, true);
            Geometry.Box("Garage lintel", root, new Vector3(-14, 4.75f, -11), new Vector3(10.2f, 1.1f, 0.3f), orange, true);
            Geometry.Label("ROCKO  /  DEVELOPMENT GARAGE", root, new Vector3(-14, 4.75f, -10.81f), 0.34f).transform.localRotation = Quaternion.Euler(0, 180, 0);
            Geometry.Box("Workbench", root, new Vector3(-17.8f, 0.8f, -23), new Vector3(2, 0.12f, 1.1f), concrete, true);
            Sign(root, new Vector3(-23, 0, -8), "ROCKO PROVING GROUND\nF1 / D-pad down · controls", dark, orange);
            GameObject garageLight = new GameObject("Garage work light"); garageLight.transform.position = new Vector3(-14, 4.8f, -18);
            Light light = garageLight.AddComponent<Light>(); light.type = LightType.Point; light.range = 17; light.intensity = 2.2f; light.color = new Color(1, 0.86f, 0.67f);
            return root;
        }

        //
        // @description - Attach explicit tire-surface metadata to a collidable object
        // @param {GameObject} ground - Collidable mesh or box
        // @param {string} label - HUD surface name
        // @param {float} grip - Tire friction multiplier
        // @param {float} rolling - Rolling resistance coefficient
        // @return {Void} - Adds the RoadSurface component
        //
        private static void Surface(GameObject ground, string label, float grip, float rolling)
        {
            RoadSurface s = ground.AddComponent<RoadSurface>(); s.label = label; s.grip = grip; s.rollingResistance = rolling;
        }

        //
        // @description - Build a smooth drivable ribbon with optional earth embankments
        // @param {Transform} parent - Environment root
        // @param {string} name - Route label
        // @param {Vector3[]} controls - Ordered world-space centerline control points
        // @param {float} width - Road width in meters
        // @param {Material} material - Road appearance
        // @param {string} label - Tire surface name
        // @param {float} grip - Tire friction multiplier
        // @param {float} rolling - Rolling resistance coefficient
        // @param {Material} shoulder - Optional embankment material
        // @return {Void} - Adds static drivable mesh and shoulder colliders
        //
        private static void Ribbon(Transform parent, string name, Vector3[] controls, float width, Material material, string label, float grip, float rolling, Material shoulder = null)
        {
            List<Vector3> points = new List<Vector3>();
            for (int i = 0; i < controls.Length - 1; i++)
            {
                Vector3 p0 = controls[Mathf.Max(0, i - 1)], p1 = controls[i], p2 = controls[i + 1], p3 = controls[Mathf.Min(controls.Length - 1, i + 2)];
                int steps = Mathf.Max(2, Mathf.CeilToInt(Vector3.Distance(p1, p2) / 2));
                for (int j = 0; j < steps; j++)
                {
                    float u = j / (float)steps;
                    Vector3 p = 0.5f * ((2 * p1) + (-p0 + p2) * u + (2 * p0 - 5 * p1 + 4 * p2 - p3) * u * u + (-p0 + 3 * p1 - 3 * p2 + p3) * u * u * u);
                    p.y = Mathf.Max(0.008f, p.y); points.Add(p);
                }
            }
            points.Add(controls[controls.Length - 1]);
            Vector3[] vertices = new Vector3[points.Count * 2];
            List<int> triangles = new List<int>();
            for (int i = 0; i < points.Count; i++)
            {
                Vector3 tangent = points[Mathf.Min(i + 1, points.Count - 1)] - points[Mathf.Max(i - 1, 0)];
                Vector3 right = Vector3.Cross(Vector3.up, tangent).normalized;
                vertices[i * 2] = points[i] - right * width / 2;
                vertices[i * 2 + 1] = points[i] + right * width / 2;
                if (i < points.Count - 1)
                {
                    int n = i * 2; triangles.AddRange(new[] { n, n + 2, n + 1, n + 1, n + 2, n + 3 });
                }
            }
            Surface(Geometry.Mesh(name, parent, vertices, triangles.ToArray(), material, true), label, grip, rolling);
            if (shoulder == null) return;
            for (int side = 0; side < 2; side++)
            {
                Vector3[] bank = new Vector3[vertices.Length];
                for (int i = 0; i < points.Count; i++)
                {
                    Vector3 edge = vertices[i * 2 + side];
                    Vector3 outward = (edge - points[i]).normalized;
                    Vector3 foot = edge + outward * (5 + edge.y * 1.6f); foot.y = -0.05f;
                    bank[i * 2] = side == 0 ? foot : edge;
                    bank[i * 2 + 1] = side == 0 ? edge : foot;
                }
                Surface(Geometry.Mesh(name + " earth bank", parent, bank, triangles.ToArray(), shoulder, true), "Dirt shoulder", 0.60f, 0.035f);
            }
        }

        //
        // @description - Add a readable sign board outside the driving lane
        // @param {Transform} root - World root
        // @param {Vector3} point - Sign ground position
        // @param {string} text - Sign wording
        // @param {Material} board - Board material
        // @param {Material} accent - Support color
        // @return {Void} - Adds a sign and support
        //
        private static void Sign(Transform root, Vector3 point, string text, Material board, Material accent)
        {
            Geometry.Box("Sign post", root, point + new Vector3(0, 1.1f, 0), new Vector3(0.08f, 2.2f, 0.08f), accent);
            Geometry.Box("Sign board", root, point + new Vector3(0, 2.05f, 0), new Vector3(5.6f, 1.3f, 0.08f), board);
            Geometry.Label(text, root, point + new Vector3(0, 2.05f, -0.052f), 0.30f);
        }

        //
        // @description - Place a non-colliding slalom marker
        // @param {Transform} root - World root
        // @param {Vector3} point - Marker ground position
        // @param {Material} orange - High-visibility material
        // @param {Material} black - Base material
        // @return {Void} - Adds a marker
        //
        private static void Cone(Transform root, Vector3 point, Material orange, Material black)
        {
            Geometry.Box("Slalom base", root, point + Vector3.up * 0.03f, new Vector3(0.45f, 0.06f, 0.45f), black);
            Geometry.Box("Slalom bollard", root, point + Vector3.up * 0.33f, new Vector3(0.18f, 0.60f, 0.18f), orange);
        }
    }
}
