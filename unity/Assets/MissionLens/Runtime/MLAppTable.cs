using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MissionLens
{
    // The table as a map you work with your hands. The table itself is a fixed patch of the room (0.86 x 0.79 m);
    // which part of the terrain lies on it, at what scale and turned which way, is the "table view" below.
    // Pinch on the model and move to slide the map; pinch with both hands to stretch and turn it. Choosing a
    // task on the timeline brings its leg or its station onto the table.
    public partial class MLApp
    {
        // ax/az: the map point at the table's centre (x east, z south). S: scale. yawRel: how far the map is turned
        // on the table, in degrees (0 = north away from the person).
        struct TView { public float ax, az, S, yawRel; }
        class Grab { public MLRig.Pointer p; public Vector2 map; }

        const float FootHx = .43f, FootHz = .395f;      // half size of the table's patch, metres
        const float SMax = 1f / 200f;                   // closest: 1 : 200, where the patch is 172 x 158 m
        TView tv, tvDefault; float SMin, tableEx = 1; bool followTasks = true;
        readonly List<Grab> grabs = new List<Grab>();
        Coroutine framing; Mesh skirtMesh; TView skirtFor; float skirtBaseY = float.NaN; int scaleShown;
        Vector2 glide, glideNow, lastHold; MLRig.Pointer lastHoldBy;

        string ScaleText => "1 : " + Mathf.RoundToInt(1 / tv.S / 10) * 10;

        // Table metres (x across, y away from the person) <-> map metres (x east, y = z south).
        static Vector2 TableToMap(Vector2 t, TView v)
        {
            float th = v.yawRel * Mathf.Deg2Rad, c = Mathf.Cos(th), s = Mathf.Sin(th);
            return new Vector2(v.ax + (t.x * c - t.y * s) / v.S, v.az - (t.x * s + t.y * c) / v.S);
        }
        static Vector2 MapToTable(Vector2 m, TView v)
        {
            float lx = (m.x - v.ax) * v.S, lz = -(m.y - v.az) * v.S, th = v.yawRel * Mathf.Deg2Rad, c = Mathf.Cos(th), s = Mathf.Sin(th);
            return new Vector2(lx * c + lz * s, -lx * s + lz * c);
        }
        bool OnPatch(Vector2 t, float margin = 0) => Mathf.Abs(t.x) <= FootHx - margin && Mathf.Abs(t.y) <= FootHz - margin;
        bool OnPatch(float x, float zSouth, float margin = 0) => OnPatch(MapToTable(new Vector2(x, zSouth), tv), margin);

        // The model stands on the table: its base sits a little under the lowest ground on the patch.
        float BaseFor(TView v)
        {
            float lo = float.MaxValue;
            for (int j = 0; j <= 20; j++) for (int i = 0; i <= 20; i++)
            {
                var m = TableToMap(new Vector2((i / 10f - 1) * FootHx, (j / 10f - 1) * FootHz), v); lo = Mathf.Min(lo, D.Elev(m.x, m.y));
            }
            return lo - BaseMargin(v);
        }
        static float BaseMargin(TView v) => .006f / v.S;   // 6 mm on the table

        TView Clamped(TView v)
        {
            v.S = Mathf.Clamp(v.S, SMin, SMax);
            v.ax = Mathf.Clamp(v.ax, -D.sx / 2 + 20, D.sx / 2 - 20); v.az = Mathf.Clamp(v.az, -D.sz / 2 + 20, D.sz / 2 - 20);
            return v;
        }

        // ---------- per frame ----------
        void UpdateTable(float dt)
        {
            bool live = stage == Stage.Table && introDone && !busy;
            if (!live) { grabs.Clear(); rig.lookLocked = false; glide = Vector2.zero; lastHoldBy = null; }
            else
            {
                if (framing == null) Manipulate(dt);
                float want = BaseFor(tv); baseY = Mathf.Abs(want - baseY) < .01f ? want : Mathf.Lerp(baseY, want, 1 - Mathf.Exp(-10 * dt));
                view = TableView(); ApplyView();
                int shown = Mathf.RoundToInt(1 / tv.S / 10) * 1000 + Mathf.RoundToInt(Mathf.Repeat(tv.yawRel, 360) / 3); if (shown != scaleShown) { scaleShown = shown; DrawLegend(); }
            }
            if (OnTable && frontR > 0 && (!Same(skirtFor, tv) || skirtBaseY != baseY)) RebuildSkirt();
        }
        static bool Same(TView a, TView b) => a.ax == b.ax && a.az == b.az && a.S == b.S && a.yawRel == b.yawRel;

        void RebuildSkirt()
        {
            var c = new Vector2[4]; int k = 0;
            foreach (var t in new[] { new Vector2(-FootHx, -FootHz), new Vector2(FootHx, -FootHz), new Vector2(FootHx, FootHz), new Vector2(-FootHx, FootHz) })
            { var m = TableToMap(t, tv); c[k++] = new Vector2(m.x, -m.y); }
            MLMesh.Skirt(skirtMesh, c, 110, baseY, (x, z) => D.Elev(x, -z));
            skirtMat.SetFloat("_BaseY", baseY); skirtFor = tv; skirtBaseY = baseY;
        }

        // Where a pointer takes hold of the table: under the hand when the hand is over the model, otherwise
        // where its ray meets the table. In table metres.
        bool HoldPoint(MLRig.Pointer p, out Vector2 t)
        {
            var o = tableFrame.InverseTransformPoint(p.origin); var d = tableFrame.InverseTransformDirection(p.rotation * Vector3.forward);
            if (rig.xr && Mathf.Abs(o.x) < FootHx + .08f && Mathf.Abs(o.z) < FootHz + .08f && o.y > -.06f && o.y < .42f) { t = new Vector2(o.x, o.z); return true; }
            t = default; if (d.y > -.04f) return false;
            float k = (.03f - o.y) / d.y; if (k <= 0) return false;
            var hit = o + d * k; t = new Vector2(hit.x, hit.z); return true;
        }

        void Manipulate(float dt)
        {
            grabs.RemoveAll(g => !g.p.active || !g.p.held);
            foreach (var p in rig.xr ? new[] { rig.right, rig.left } : new[] { rig.mouse })
                if (p.active && p.grabDown && !p.overSurface && p.tool == null && grabs.All(g => g.p != p) && HoldPoint(p, out var t0) && OnPatch(t0, -.03f))
                { grabs.Add(new Grab { p = p, map = TableToMap(t0, tv) }); glide = Vector2.zero; sfx.Play("hover", .7f); rig.Haptic(p, .3f, .03f); Log("map taken (" + grabs.Count + ")"); }
            rig.lookLocked = grabs.Count > 0 || lensBy != null || dialBy != null;

            var v = tv; bool changed = false;
            if (grabs.Count >= 2 && HoldPoint(grabs[0].p, out var a) && HoldPoint(grabs[1].p, out var b) && (b - a).magnitude > .03f)
            {   // two holds: the two map points stay under the two hands, which fixes scale and turn
                Vector2 m1 = grabs[0].map, m2 = grabs[1].map, mv = new Vector2(m2.x - m1.x, -(m2.y - m1.y)), tt = b - a;
                if (mv.magnitude > .5f)
                {
                    v.S = Mathf.Clamp(tt.magnitude / mv.magnitude, SMin, SMax);
                    v.yawRel = (Mathf.Atan2(mv.y, mv.x) - Mathf.Atan2(tt.y, tt.x)) * Mathf.Rad2Deg;
                    Hold(ref v, m1, a); changed = true;
                }
            }
            else if (grabs.Count >= 1 && HoldPoint(grabs[0].p, out var h))
            {
                Hold(ref v, grabs[0].map, h); changed = true;
                // how fast the map is being slid, for the glide after letting go
                if (lastHoldBy == grabs[0].p && dt > 0) glideNow = Vector2.Lerp(glideNow, (h - lastHold) / dt, .35f);
                lastHold = h; lastHoldBy = grabs[0].p;
            }
            else if (grabs.Count == 0)
            {   // let go while sliding: the map carries on and comes to rest
                if (lastHoldBy != null) { glide = Vector2.ClampMagnitude(glideNow, 1.2f); lastHoldBy = null; glideNow = Vector2.zero; }
                if (glide.sqrMagnitude > .0004f)
                {
                    var centre = TableToMap(Vector2.zero, v); Hold(ref v, centre, glide * dt); glide *= Mathf.Exp(-3.5f * dt); changed = true;
                }
            }

            // Desktop: wheel to zoom about the pointer, Q / E to turn.
            if (!rig.xr && (rig.wheel != 0 || rig.turn != 0) && HoldPoint(rig.mouse, out var w))
            {
                var keep = TableToMap(OnPatch(w) ? w : Vector2.zero, v); var at = OnPatch(w) ? w : Vector2.zero;
                v.S = Mathf.Clamp(v.S * Mathf.Exp(rig.wheel * .18f), SMin, SMax); v.yawRel += rig.turn * 70 * dt;
                Hold(ref v, keep, at); changed = true;
            }
            if (changed) tv = Clamped(v);
        }
        // Moves the view so that map point m lies at table point t.
        static void Hold(ref TView v, Vector2 m, Vector2 t)
        {
            var zero = v; zero.ax = 0; zero.az = 0; var off = TableToMap(t, zero);
            v.ax = m.x - off.x; v.az = m.y - off.y;
        }

        // ---------- bringing things onto the table ----------
        /// <summary>The view that puts these map points on the table with some room around them, keeping the current turn.</summary>
        TView Framing(IEnumerable<Vector2> pts, float marginM)
        {
            var list = pts.ToList(); var v = tv; v.ax = v.az = 0;
            // fit in the table's own axes, so a turned map is still used fully
            float x0 = float.MaxValue, x1 = float.MinValue, y0 = float.MaxValue, y1 = float.MinValue;
            var unit = v; unit.S = 1;
            foreach (var p in list) { var t = MapToTable(p, unit); x0 = Mathf.Min(x0, t.x); x1 = Mathf.Max(x1, t.x); y0 = Mathf.Min(y0, t.y); y1 = Mathf.Max(y1, t.y); }
            v.S = Mathf.Clamp(Mathf.Min(FootHx / ((x1 - x0) / 2 + marginM), FootHz / ((y1 - y0) / 2 + marginM)), SMin, SMax);
            var centre = TableToMap(new Vector2((x0 + x1) / 2, (y0 + y1) / 2), unit); v.ax = centre.x; v.az = centre.y;
            return Clamped(v);
        }
        TView FramingFor(MTask t)
        {
            if (t.leg >= 0) { var r = M.legs[t.leg].route; return Framing(Enumerable.Range(0, r.Length / 2).Select(i => new Vector2(r[i * 2], r[i * 2 + 1])), 28); }
            var s = St(t.station); return Framing(new[] { new Vector2(s.x, s.z) }, 60);
        }
        void TableTo(TView target, float dur = .9f)
        {
            if (framing != null) StopCoroutine(framing);
            framing = StartCoroutine(CoTableTo(target, dur));
        }
        IEnumerator CoTableTo(TView target, float dur)
        {
            var from = tv; grabs.Clear();
            yield return Tween(dur, k =>
            {
                float e = Ease(k);
                tv = new TView { ax = Mathf.Lerp(from.ax, target.ax, e), az = Mathf.Lerp(from.az, target.az, e), S = Mathf.Exp(Mathf.Lerp(Mathf.Log(from.S), Mathf.Log(target.S), e)), yawRel = Mathf.LerpAngle(from.yawRel, target.yawRel, e) };
            });
            tv = target; framing = null;
        }
        public void WholeRoute() { sfx.Play("toggle", .7f); TableTo(tvDefault); }
        public void ToggleFollow() { followTasks = !followTasks; sfx.Play("toggle", .7f); DrawLegend(); }
    }
}
