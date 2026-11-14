using System;
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

        /// <summary>
        /// A flat band along a path. Both vertices of each cross-section sit on the path; the shader moves them
        /// apart along `normal` (the sideways direction) and lifts them, so one mesh serves every width.
        /// uv.x: metres along the path from u0; uv.y: 0 on one edge, 1 on the other.
        /// tint: an optional colour per path point (alpha 0 = none), carried as vertex colour.
        /// </summary>
        public static Mesh Ribbon(Vector3[] p, float u0 = 0, Color[] tint = null)
        {
            var v = new Vector3[p.Length * 2]; var side = new Vector3[v.Length]; var uv = new Vector2[v.Length]; var col = new Color[v.Length]; var t = new List<int>(); float u = u0;
            for (int i = 0; i < p.Length; i++)
            {
                var d = p[Mathf.Min(i + 1, p.Length - 1)] - p[Mathf.Max(i - 1, 0)]; var n = new Vector3(-d.z, 0, d.x).normalized;
                if (i > 0) u += Vector2.Distance(new Vector2(p[i].x, p[i].z), new Vector2(p[i - 1].x, p[i - 1].z));
                v[i * 2] = v[i * 2 + 1] = p[i]; side[i * 2] = side[i * 2 + 1] = n; uv[i * 2] = new Vector2(u, 0); uv[i * 2 + 1] = new Vector2(u, 1);
                col[i * 2] = col[i * 2 + 1] = tint != null ? tint[i] : Color.clear;
                if (i > 0) t.AddRange(new[] { 2 * i - 2, 2 * i - 1, 2 * i, 2 * i - 1, 2 * i + 1, 2 * i });
            }
            var m = new Mesh { name = "Ribbon" }; m.SetVertices(v); m.SetNormals(side); m.SetUVs(0, uv); m.SetColors(col); m.SetTriangles(t, 0); m.RecalculateBounds();
            var b = m.bounds; b.Expand(60); m.bounds = b;   // room for the width and lift the shader adds
            return m;
        }

        /// <summary>
        /// A sphere seen from its centre, for a panorama: uv.x = bearing from north (clockwise); uv.y runs 0..1
        /// from elMin to elMax (the mosaic) and goes on above 1 up to topDeg, where the shader fills in sky.
        /// </summary>
        public static Mesh PanoSphere(float elMaxDeg, float elMinDeg, float radius, float topDeg)
        {
            const int na = 180, ne = 80; var v = new Vector3[(na + 1) * (ne + 1)]; var uv = new Vector2[v.Length]; var t = new int[na * ne * 6];
            for (int j = 0, k = 0; j <= ne; j++) for (int i = 0; i <= na; i++, k++)
            {
                float az = i / (float)na * 2 * Mathf.PI, elDeg = topDeg - j / (float)ne * (topDeg - elMinDeg), el = elDeg * Mathf.Deg2Rad;
                v[k] = new Vector3(Mathf.Cos(el) * Mathf.Sin(az), Mathf.Sin(el), Mathf.Cos(el) * Mathf.Cos(az)) * radius;
                uv[k] = new Vector2(i / (float)na, (elDeg - elMinDeg) / (elMaxDeg - elMinDeg));
            }
            for (int j = 0, k = 0; j < ne; j++) for (int i = 0; i < na; i++)
            {
                int a = j * (na + 1) + i, b = a + 1, c = a + na + 1, d = c + 1;
                t[k++] = a; t[k++] = b; t[k++] = c; t[k++] = b; t[k++] = d; t[k++] = c;
            }
            var m = new Mesh { name = "Sphere" }; m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(t, 0);
            m.bounds = new Bounds(Vector3.zero, Vector3.one * radius * 2); return m;
        }

        /// <summary>A unit square (uv 0..1), lying flat (facing up) or upright facing -z.</summary>
        public static Mesh Quad(bool flat)
        {
            var v = flat ? new[] { new Vector3(-.5f, 0, -.5f), new Vector3(.5f, 0, -.5f), new Vector3(.5f, 0, .5f), new Vector3(-.5f, 0, .5f) }
                         : new[] { new Vector3(-.5f, -.5f, 0), new Vector3(.5f, -.5f, 0), new Vector3(.5f, .5f, 0), new Vector3(-.5f, .5f, 0) };
            var m = new Mesh { name = "Quad" }; m.SetVertices(v); m.SetUVs(0, new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) });
            m.SetTriangles(new[] { 0, 2, 1, 0, 3, 2 }, 0); m.RecalculateBounds(); return m;
        }

        /// <summary>Two crossed upright strips, 1 wide and 1 tall, standing on the origin. uv.y: 0 at the ground.</summary>
        public static Mesh Cross()
        {
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            foreach (var side in new[] { Vector3.right, Vector3.forward })
            {
                int i = v.Count; v.Add(-side * .5f); v.Add(side * .5f); v.Add(side * .5f + Vector3.up); v.Add(-side * .5f + Vector3.up);
                uv.Add(new Vector2(0, 0)); uv.Add(new Vector2(1, 0)); uv.Add(new Vector2(1, 1)); uv.Add(new Vector2(0, 1));
                t.AddRange(new[] { i, i + 2, i + 1, i, i + 3, i + 2 });
            }
            var m = new Mesh { name = "Cross" }; m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(t, 0); m.RecalculateBounds(); return m;
        }

        /// <summary>A flat rectangular outline on y = 0: half sizes hx, hz, line width w (inside the rectangle).</summary>
        public static Mesh Frame(float hx, float hz, float w)
        {
            var v = new List<Vector3>(); var t = new List<int>();
            void Strip(float x0, float z0, float x1, float z1) { int i = v.Count; v.Add(new Vector3(x0, 0, z0)); v.Add(new Vector3(x1, 0, z0)); v.Add(new Vector3(x1, 0, z1)); v.Add(new Vector3(x0, 0, z1)); t.AddRange(new[] { i, i + 2, i + 1, i, i + 3, i + 2 }); }
            Strip(-hx, -hz, hx, -hz + w); Strip(-hx, hz - w, hx, hz); Strip(-hx, -hz + w, -hx + w, hz - w); Strip(hx - w, -hz + w, hx, hz - w);
            var m = new Mesh { name = "Frame" }; m.SetVertices(v); m.SetTriangles(t, 0); m.RecalculateBounds(); return m;
        }

        /// <summary>
        /// Walls around a four-cornered patch of terrain, from the surface down to baseY, written into `m` (so the
        /// same mesh can be refilled as the patch moves). corners: Unity local x, z in order around the patch.
        /// height(x, z) gives the surface. uv.x: 1 on the top row, 0 on the bottom; uv.y: the wall's height there.
        /// </summary>
        public static void Skirt(Mesh m, Vector2[] corners, int perEdge, float baseY, Func<float, float, float> height)
        {
            int cols = perEdge + 1; var v = new Vector3[cols * 2 * 4]; var uv = new Vector2[v.Length]; var t = new int[perEdge * 6 * 4];
            for (int e = 0, k = 0, q = 0; e < 4; e++)
            {
                Vector2 a = corners[e], b = corners[(e + 1) % 4]; int i0 = k;
                for (int i = 0; i <= perEdge; i++, k += 2)
                {
                    var p = Vector2.Lerp(a, b, i / (float)perEdge); float y = height(p.x, p.y);
                    v[k] = new Vector3(p.x, y, p.y); uv[k] = new Vector2(1, y - baseY); v[k + 1] = new Vector3(p.x, baseY, p.y); uv[k + 1] = new Vector2(0, y - baseY);
                    if (i > 0) { int c = i0 + i * 2; t[q++] = c - 2; t[q++] = c - 1; t[q++] = c; t[q++] = c - 1; t[q++] = c + 1; t[q++] = c; }
                }
            }
            m.Clear(); m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(t, 0); m.RecalculateBounds();
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
