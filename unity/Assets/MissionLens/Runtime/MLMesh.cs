using System.Collections.Generic;
using UnityEngine;

namespace MissionLens
{
    /// <summary>Small procedural meshes, so the app needs no model assets.</summary>
    public static class MLMesh
    {
        public static Mesh Box(Vector3 size)
        {
            var h = size / 2; var v = new List<Vector3>(); var t = new List<int>();
            void Face(Vector3 o, Vector3 a, Vector3 b) { int i = v.Count; v.Add(o - a - b); v.Add(o + a - b); v.Add(o + a + b); v.Add(o - a + b); t.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 }); }
            Face(new Vector3(0, h.y, 0), new Vector3(h.x, 0, 0), new Vector3(0, 0, h.z)); Face(new Vector3(0, -h.y, 0), new Vector3(h.x, 0, 0), new Vector3(0, 0, h.z));
            Face(new Vector3(h.x, 0, 0), new Vector3(0, h.y, 0), new Vector3(0, 0, h.z)); Face(new Vector3(-h.x, 0, 0), new Vector3(0, h.y, 0), new Vector3(0, 0, h.z));
            Face(new Vector3(0, 0, h.z), new Vector3(h.x, 0, 0), new Vector3(0, h.y, 0)); Face(new Vector3(0, 0, -h.z), new Vector3(h.x, 0, 0), new Vector3(0, h.y, 0));
            var m = new Mesh { name = "Box" }; m.SetVertices(v); m.SetTriangles(t, 0); m.RecalculateBounds(); return m;
        }

        /// <summary>A flat band along a path, lifted above it.</summary>
        public static Mesh Ribbon(Vector3[] p, float halfWidth, float lift)
        {
            var v = new Vector3[p.Length * 2]; var t = new List<int>();
            for (int i = 0; i < p.Length; i++)
            {
                var d = p[Mathf.Min(i + 1, p.Length - 1)] - p[Mathf.Max(i - 1, 0)]; var side = new Vector3(-d.z, 0, d.x).normalized * halfWidth; var c = p[i] + Vector3.up * lift;
                v[i * 2] = c - side; v[i * 2 + 1] = c + side;
                if (i > 0) t.AddRange(new[] { 2 * i - 2, 2 * i - 1, 2 * i, 2 * i - 1, 2 * i + 1, 2 * i });
            }
            var m = new Mesh { name = "Ribbon" }; m.SetVertices(v); m.SetTriangles(t, 0); m.RecalculateBounds(); return m;
        }

        /// <summary>
        /// The part of a sphere a panorama covers: u = bearing from north (clockwise), v = elevation from
        /// elMin (bottom) to elMax (top). Seen from the centre.
        /// </summary>
        public static Mesh PanoSphere(float elMaxDeg, float elMinDeg, float radius)
        {
            const int na = 180, ne = 64; var v = new Vector3[(na + 1) * (ne + 1)]; var uv = new Vector2[v.Length]; var t = new int[na * ne * 6];
            for (int j = 0, k = 0; j <= ne; j++) for (int i = 0; i <= na; i++, k++)
            {
                float az = i / (float)na * 2 * Mathf.PI, el = (elMaxDeg - j / (float)ne * (elMaxDeg - elMinDeg)) * Mathf.Deg2Rad;
                v[k] = new Vector3(Mathf.Cos(el) * Mathf.Sin(az), Mathf.Sin(el), Mathf.Cos(el) * Mathf.Cos(az)) * radius; uv[k] = new Vector2(i / (float)na, 1 - j / (float)ne);
            }
            for (int j = 0, k = 0; j < ne; j++) for (int i = 0; i < na; i++)
            {
                int a = j * (na + 1) + i, b = a + 1, c = a + na + 1, d = c + 1;
                t[k++] = a; t[k++] = b; t[k++] = c; t[k++] = b; t[k++] = d; t[k++] = c;
            }
            var m = new Mesh { name = "Panorama" }; m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(t, 0);
            m.bounds = new Bounds(Vector3.zero, Vector3.one * radius * 2); return m;
        }

        /// <summary>An eight-sided dot.</summary>
        public static Mesh Octa()
        {
            var v = new[] { Vector3.up, Vector3.down, Vector3.left, Vector3.right, Vector3.forward, Vector3.back };
            for (int i = 0; i < v.Length; i++) v[i] *= .5f;
            var t = new[] { 0, 4, 3, 0, 3, 5, 0, 5, 2, 0, 2, 4, 1, 3, 4, 1, 5, 3, 1, 2, 5, 1, 4, 2 };
            var m = new Mesh { name = "Dot" }; m.SetVertices(v); m.SetTriangles(t, 0); m.RecalculateBounds(); return m;
        }
    }
}
