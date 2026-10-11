using System.Collections.Generic;
using UnityEngine;

namespace Rocko
{
    public static class Geometry
    {
        private static Font labelFont;
        private static Material labelMaterial;
        //
        // @description - Create a shared opaque material for the built-in render pipeline
        // @param {string} name - Material label
        // @param {Color} color - Albedo
        // @param {float} smoothness - Surface smoothness from zero to one
        // @return {Material} - New runtime material
        //
        public static Material Material(string name, Color color, float smoothness = 0.25f)
        {
            Material m = new Material(Resources.Load<Shader>("RockoSurface"));
            m.name = name;
            m.color = color;
            m.SetFloat("_Glossiness", smoothness);
            return m;
        }

        //
        // @description - Add a transformed box with optional solid collision
        // @param {string} name - Object label
        // @param {Transform} parent - Parent coordinate system
        // @param {Vector3} position - Local center
        // @param {Vector3} size - Local dimensions in meters
        // @param {Material} material - Shared surface material
        // @param {bool} solid - Whether to retain collision
        // @return {GameObject} - Created box
        //
        public static GameObject Box(string name, Transform parent, Vector3 position, Vector3 size, Material material, bool solid = false)
        {
            GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            g.name = name;
            g.transform.SetParent(parent, false);
            g.transform.localPosition = position;
            g.transform.localScale = size;
            g.GetComponent<Renderer>().sharedMaterial = material;
            if (!solid) { g.GetComponent<Collider>().enabled = false; Object.Destroy(g.GetComponent<Collider>()); }
            return g;
        }

        //
        // @description - Create a mesh with double-sided triangles and optional static mesh collision
        // @param {string} name - Object label
        // @param {Transform} parent - Parent transform
        // @param {Vector3[]} vertices - Local-space mesh points
        // @param {int[]} triangles - Triangle indices
        // @param {Material} material - Shared rendering material
        // @param {bool} solid - Whether to add static mesh collision
        // @return {GameObject} - Created mesh object
        //
        public static GameObject Mesh(string name, Transform parent, Vector3[] vertices, int[] triangles, Material material, bool solid = false)
        {
            GameObject g = new GameObject(name);
            g.transform.SetParent(parent, false);
            Mesh mesh = new Mesh { name = name };
            mesh.vertices = vertices;
            // Surface shader uses Cull Off; triangles are supplied once so normals remain well-defined.
            mesh.triangles = triangles;
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            g.AddComponent<MeshFilter>().sharedMesh = mesh;
            g.AddComponent<MeshRenderer>().sharedMaterial = material;
            if (solid) g.AddComponent<MeshCollider>().sharedMesh = mesh;
            return g;
        }

        //
        // @description - Add a four-corner panel
        // @param {string} name - Panel label
        // @param {Transform} parent - Parent coordinate system
        // @param {Vector3} a - First corner
        // @param {Vector3} b - Second corner
        // @param {Vector3} c - Third corner
        // @param {Vector3} d - Fourth corner
        // @param {Material} material - Rendering material
        // @return {GameObject} - Created panel
        //
        public static GameObject Quad(string name, Transform parent, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Material material)
        {
            return Mesh(name, parent, new[] { a, b, c, d }, new[] { 0, 1, 2, 0, 2, 3 }, material);
        }

        //
        // @description - Place a rectangular strut between two local points
        // @param {string} name - Strut label
        // @param {Transform} parent - Parent coordinate system
        // @param {Vector3} a - Start
        // @param {Vector3} b - End
        // @param {float} width - Strut thickness
        // @param {Material} material - Surface material
        // @return {GameObject} - Created strut
        //
        public static GameObject Strut(string name, Transform parent, Vector3 a, Vector3 b, float width, Material material)
        {
            GameObject g = Box(name, parent, (a + b) / 2, new Vector3(width, width, Vector3.Distance(a, b)), material);
            g.transform.localRotation = Quaternion.LookRotation(b - a);
            return g;
        }

        //
        // @description - Build a circular tube for tires, wheel arches, and the steering wheel
        // @param {string} name - Object label
        // @param {Transform} parent - Parent coordinate system
        // @param {Vector3} center - Local center
        // @param {float} radius - Ring centerline radius
        // @param {float} tube - Tube radius
        // @param {Material} material - Surface material
        // @param {int} segments - Segments around the ring
        // @return {GameObject} - Torus lying in the local XY plane
        //
        public static GameObject Ring(string name, Transform parent, Vector3 center, float radius, float tube, Material material, int segments = 48)
        {
            List<Vector3> vertices = new List<Vector3>();
            List<int> indices = new List<int>();
            for (int i = 0; i <= segments; i++)
                for (int j = 0; j <= 10; j++)
                {
                    float a = i * Mathf.PI * 2 / segments, b = j * Mathf.PI * 2 / 10;
                    vertices.Add(center + new Vector3(Mathf.Cos(a) * (radius + tube * Mathf.Cos(b)), Mathf.Sin(a) * (radius + tube * Mathf.Cos(b)), tube * Mathf.Sin(b)));
                    if (i < segments && j < 10)
                    {
                        int n = i * 11 + j;
                        indices.AddRange(new[] { n, n + 11, n + 1, n + 1, n + 11, n + 12 });
                    }
                }
            return Mesh(name, parent, vertices.ToArray(), indices.ToArray(), material);
        }

        //
        // @description - Create an inexpensive world-space sign facing the approaching driver
        // @param {string} text - Sign wording
        // @param {Transform} parent - Parent transform
        // @param {Vector3} position - Local position
        // @param {float} size - Character size in meters
        // @return {GameObject} - Created text sign
        //
        public static GameObject Label(string text, Transform parent, Vector3 position, float size = 0.35f)
        {
            GameObject g = new GameObject(text);
            g.transform.SetParent(parent, false);
            g.transform.localPosition = position;
            g.transform.localRotation = Quaternion.identity;
            TextMesh t = g.AddComponent<TextMesh>();
            if (labelFont == null)
            {
                labelFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                Font.textureRebuilt -= RefreshLabelAtlas;
                Font.textureRebuilt += RefreshLabelAtlas;
            }
            t.font = labelFont;
            if (labelMaterial == null)
                labelMaterial = new Material(Resources.Load<Shader>("RockoWorldText")) { name = "Depth-tested world labels" };
            MeshRenderer renderer = g.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = labelMaterial;
            // Use the font's live atlas, including after Unity rebuilds its glyphs.
            labelMaterial.mainTexture = labelFont.material.mainTexture;
            t.text = text; t.fontSize = 64; t.characterSize = size;
            t.anchor = TextAnchor.MiddleCenter; t.alignment = TextAlignment.Center;
            t.color = new Color(0.86f, 0.94f, 0.94f);
            return g;
        }

        //
        // @description - Keep world labels connected to the rebuilt dynamic font atlas
        // @param {Font} font - Font whose glyph texture changed
        // @return {Void} - Updates the shared world-text material texture
        //
        private static void RefreshLabelAtlas(Font font)
        {
            if (font == labelFont && labelMaterial != null)
                labelMaterial.mainTexture = font.material.mainTexture;
        }
    }
}
