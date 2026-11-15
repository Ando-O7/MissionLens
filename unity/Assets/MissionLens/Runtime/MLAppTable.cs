using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static MissionLens.MLPanel;
using static MissionLens.MLText;

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
        class Grab { public MLRig.Pointer p; public Vector2 map; public bool byHand; }   // byHand: held under the hand itself, not where its ray lands

        const float FootHx = .43f, FootHz = .395f;      // half size of the table's patch, metres
        const float SMax = 1f / 80f;                    // closest: 1 : 80, where the patch is 69 x 63 m (candidates a few metres from a station then stand clear of it; the image is soft at this scale, 25 cm to a pixel)
        TView tv, tvDefault; float SMin, tableEx = 1; bool followTasks = true;
        readonly List<Grab> grabs = new List<Grab>();
        Coroutine framing; Mesh skirtMesh; TView skirtFor; float skirtBaseY = float.NaN; int scaleShown;
        Vector2 glide, glideNow, lastHold; MLRig.Pointer lastHoldBy; float menuDrawnAt;

        string ScaleText => "1 : " + Mathf.RoundToInt(1 / tv.S / 10) * 10;
        /// <summary>Metres between contour lines: closer together the nearer the map is looked at. Off the table, 5 m.</summary>
        float ContourStep => !OnTable ? 5 : tv.S >= 1 / 320f ? 1 : tv.S >= 1 / 800f ? 2 : 5;

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
                // the sign's scale and north arrow follow the map, but not on every frame of a move: redrawing a sign is not free
                int shown = Mathf.RoundToInt(1 / tv.S / 10) * 1000 + Mathf.RoundToInt(Mathf.Repeat(tv.yawRel, 360) / 3) + (int)ContourStep * 1000000;
                if (shown != scaleShown && Time.time - menuDrawnAt > .25f) { scaleShown = shown; menuDrawnAt = Time.time; DrawMenu(); }
            }
            if (OnTable && frontR > 0 && (!Same(skirtFor, tv) || skirtBaseY != baseY)) RebuildSkirt();
            UpdateProbe(dt);
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

        // Two ways to take hold of the map, both in table metres. With the hand itself: the point straight under
        // it, when the hand is over the table or just in front of it (where hands are when standing at it), so that
        // the map moves exactly as the hand does. Or from a distance: the point where the pointer's ray meets the table.
        Vector2 UnderHand(MLRig.Pointer p) { var o = tableFrame.InverseTransformPoint(p.origin); return new Vector2(o.x, o.z); }
        bool HandAtTable(MLRig.Pointer p)
        {
            var o = tableFrame.InverseTransformPoint(p.origin);
            return rig.xr && Mathf.Abs(o.x) < FootHx + .22f && o.z > -FootHz - .32f && o.z < FootHz + .1f && o.y > -.1f && o.y < .5f;
        }
        bool HoldPoint(MLRig.Pointer p, out Vector2 t)
        {
            var o = tableFrame.InverseTransformPoint(p.origin); var d = tableFrame.InverseTransformDirection(p.rotation * Vector3.forward);
            t = default; if (d.y > -.04f) return false;
            float k = (.03f - o.y) / d.y; if (k <= 0) return false;
            var hit = o + d * k; t = new Vector2(hit.x, hit.z); return true;
        }
        bool HoldNow(Grab g, out Vector2 t) { if (g.byHand) { t = UnderHand(g.p); return true; } return HoldPoint(g.p, out t); }

        void Manipulate(float dt)
        {
            grabs.RemoveAll(g => !g.p.active || !g.p.held);
            foreach (var p in rig.xr ? new[] { rig.right, rig.left } : new[] { rig.mouse })
            {
                if (!p.active || !p.grabDown || p.overSurface || p.tool != null || grabs.Count >= 2 || grabs.Any(g => g.p == p)) continue;
                // While one hand holds the map, a pinch of the other hand joins in from wherever it is: both then hold
                // the map under the hands themselves, and pulling them apart stretches it.
                bool second = rig.xr && grabs.Count == 1, hand = second || HandAtTable(p); Vector2 t0 = UnderHand(p);
                if (!hand && !(HoldPoint(p, out t0) && OnPatch(t0, -.03f))) continue;
                if (second && !grabs[0].byHand) { grabs[0].byHand = true; grabs[0].map = TableToMap(UnderHand(grabs[0].p), tv); }
                grabs.Add(new Grab { p = p, map = TableToMap(t0, tv), byHand = hand }); glide = Vector2.zero; lastHoldBy = null;
                sfx.Play("hover", grabs.Count == 2 ? 1 : .7f); rig.Haptic(p, .3f, .03f); Log("map taken (" + grabs.Count + (hand ? ", by hand)" : ", by ray)"));
            }
            rig.lookLocked = grabs.Count > 0 || lensBy != null || dialBy != null;

            var v = tv; bool changed = false;
            if (grabs.Count >= 2 && HoldNow(grabs[0], out var a) && HoldNow(grabs[1], out var b) && (b - a).magnitude > .03f)
            {   // two holds: the two map points stay under the two hands, which fixes scale and turn
                Vector2 m1 = grabs[0].map, m2 = grabs[1].map, mv = new Vector2(m2.x - m1.x, -(m2.y - m1.y)), tt = b - a;
                if (mv.magnitude > .5f)
                {
                    v.S = Mathf.Clamp(tt.magnitude / mv.magnitude, SMin, SMax);
                    v.yawRel = (Mathf.Atan2(mv.y, mv.x) - Mathf.Atan2(tt.y, tt.x)) * Mathf.Rad2Deg;
                    Hold(ref v, m1, a); changed = true;
                }
            }
            else if (grabs.Count >= 1 && HoldNow(grabs[0], out var h))
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

        // ---------- what the table says about its map ----------
        // The neatline: north (the arrow turns with the map), a scale bar that keeps to round lengths, the scale,
        // and where the model comes from. It lies on the table at the model's near edge, so it is read with the map.
        void DrawNeatline()
        {
            var s = neat; s.Clear(); float pxPerM = tv.S * s.w / (FootHx * 2);
            s.Box(0, 6, s.w, s.h - 12, 10, new Color(8 / 255f, 10 / 255f, 14 / 255f, .82f));
            float cx = 50, cy = 42, r = 24, th = tv.yawRel * Mathf.Deg2Rad; Vector2 up = new Vector2(Mathf.Sin(th), -Mathf.Cos(th)), side = new Vector2(-up.y, up.x);
            Vector2 c = new Vector2(cx, cy), tip = c + up * r, tail = c - up * r, barb = tip - up * 15;
            s.Stroke(tail.x, tail.y, tip.x, tip.y, 5, Ink); s.Stroke(tip.x, tip.y, barb.x + side.x * 10, barb.y + side.y * 10, 5, Ink); s.Stroke(tip.x, tip.y, barb.x - side.x * 10, barb.y - side.y * 10, 5, Ink);
            s.Text("N", 92, 54, Size.Heading, Ink, true);
            float len = 10; foreach (var l in new[] { 20f, 50f, 100f, 200f, 500f }) if (l * pxPerM <= 420) len = l;
            float bar = len * pxPerM, bx = 170; const float by = 50;
            s.Box(bx, by, bar, 9, 2, Ink); s.Box(bx, by, bar / 2, 9, 2, Gray);
            for (int i = 0; i <= 2; i++) s.Stroke(bx + bar * i / 2, by - 8, bx + bar * i / 2, by + 9, 3, Ink);
            s.Text("0", bx, by - 14, Size.Label, Soft, false, 0); s.Text($"{len / 2:0}", bx + bar / 2, by - 14, Size.Label, Soft, false, 0); s.Text($"{len:0} m", bx + bar, by - 14, Size.Label, Soft, false, 0);
            string ratio = $"{ScaleText} · {(tableEx > 1 ? L("height ×2", "高さ ×2") : L("height as measured", "高さ 実寸"))}";
            s.Text(ratio, bx + 470, 55, Size.Body, Ink, true);
            // where the map comes from; the shorter form where the long one would run off the strip
            float sx = bx + 470 + s.Measure(ratio, Size.Body, true) + 40; string contours = L($"contours every {ContourStep:0} m", $"等高線 {ContourStep:0} m ごと"), source = L("HiRISE image + terrain model (2 m grid) · ", "HiRISE の画像 + 地形モデル(2 m 格子)· ") + contours;
            s.Text(s.Measure(source, Size.Label) <= 1380 - sx ? source : "HiRISE · " + contours, sx, 54, Size.Label, Gray);   // 1380: the timer lies over the strip's right end
        }

        // ---------- where on Mars the model is ----------
        // A small globe beside the model: the Viking Orbiter mosaic of the whole planet, with a ring round the place
        // the model shows (the place is far smaller than the ring: on this globe it would be 0.03 mm across). It is
        // there to be glanced at, at the table only; it takes no input and plays no part in any transition. The
        // marked place faces where the person stands; the globe does not turn by itself.
        const float GlobeR = .065f, GlobeRingDeg = 5, GlobeRingMm = 1;
        Transform globeT; MeshRenderer globeR; Material globeMat; MLPanel globeTag;
        void BuildGlobe()
        {
            globeMat = new Material(Sh("MLGlobe")); globeMat.SetTexture("_MainTex", Resources.Load<Texture2D>("ML/globe"));
            globeMat.SetVector("_Mark", MLMesh.GlobePoint((float)((M.lonMin + M.lonMax) / 2), (float)((M.latMin + M.latMax) / 2)));
            globeMat.SetColor("_RingColor", Ink); globeMat.SetFloat("_RingR", GlobeRingDeg * Mathf.Deg2Rad); globeMat.SetFloat("_RingW", GlobeRingMm * .001f / GlobeR);
            globeR = Add("Globe", MLMesh.Globe(GlobeR), globeMat, tableFrame); globeT = globeR.transform; globeR.enabled = false;
            globeTag = new MLPanel("Globe tag", 380, 132, .212f, tableFrame); globeTag.Snap(false); panels.Add(globeTag);   // wide enough for the Japanese name; its text is as large to the eye as the notes on the briefing panels
            DrawGlobeTag();
        }
        // Right of the model (the menu comes on the left hand), clear of the timeline's end when the panels stand as a
        // wall, and further back when they lie on the desk, where the right-hand panel is nearer the person.
        void PlaceGlobe()
        {
            var at = layout == "wall" ? new Vector3(.60f, .10f, .03f) : new Vector3(.60f, .10f, .20f);
            var toEye = (new Vector3(0, EyeAboveTable, -TableAhead) - at).normalized; var mark = (Vector3)globeMat.GetVector("_Mark");
            globeT.localPosition = at; globeT.localRotation = Quaternion.LookRotation(toEye, Vector3.up) * Quaternion.Inverse(Quaternion.LookRotation(mark, Vector3.up));
            globeTag.go.transform.localPosition = at + new Vector3(0, -.095f, -.05f); globeTag.go.transform.localRotation = Quaternion.Euler(50, 30, 0);
        }
        void DrawGlobeTag()
        {
            var s = globeTag; s.Clear();
            s.Box(0, 0, s.w, s.h, 16, new Color(8 / 255f, 10 / 255f, 14 / 255f, .86f), Line);
            s.Text(L("Jezero Crater, Mars", "火星 ジェゼロ・クレーター"), 22, 42, Size.Label, Ink, true);
            s.Text(L("NASA OBSERVATION", "NASA の観測"), 22, 80, Size.Note, Ink, true);
            s.Text(L("Viking Orbiter mosaic · USGS", "Viking Orbiter のモザイク · USGS"), 22, 108, Size.Note, Gray);
        }

        // The probe: point at the model and, after a moment, a tag gives the elevation of that point and how steep the
        // ground is there. Both are read from the terrain model; the slope is our calculation from it.
        int probeShown = int.MinValue; float probeFor; Vector3 probeAt;
        void UpdateProbe(float dt)
        {
            bool live = stage == Stage.Table && introDone && !busy && grabs.Count == 0 && dialBy == null && framing == null, hit = false; Vector3 local = default; MLRig.Pointer by = null;
            if (live) foreach (var p in rig.xr ? new[] { rig.right, rig.left } : new[] { rig.mouse })
            {
                if (!p.active || p.held || p.fingerOnly || p.overSurface || p.tool != null || (rig.xr && p == rig.left && menuDock.on) || HandAtTool(p)) continue;
                if (PointOnTerrain(p.Ray, out local, out _) && OnPatch(local.x, -local.z, .004f)) { hit = true; by = p; break; }
            }
            probeFor = hit ? probeFor + dt : 0; probe.Show = hit && probeFor > .3f;
            if (!hit) return;
            var at = world.TransformPoint(local); var head = rig.Head.position; float d = Mathf.Max(.2f, Vector3.Distance(at, head));
            by.hitDistance = Vector3.Distance(by.origin, at); PlaceReticle(at, d * .45f);
            int elev = Mathf.RoundToInt(M.elevMin + local.y), slope = Mathf.RoundToInt(D.SlopeDeg(local.x, -local.z)), key = elev * 100 + slope;
            if (key != probeShown)
            {
                probeShown = key; var s = probe; s.Clear(); var sc = SlopeClass(slope); if (sc.a == 0) sc = Ink;
                s.Box(0, 0, s.w, s.h, 18, new Color(8 / 255f, 10 / 255f, 14 / 255f, .88f), new Color(1, 1, 1, .16f));
                string e = L($"{elev} m", $"標高 {elev} m"), g = L($"slope {slope}°", $"傾斜 {slope}°");
                s.Text(e, 24, 52, Size.Heading, Ink, true); s.Text(g, 24 + s.Measure(e, Size.Heading, true) + 26, 52, Size.Heading, sc, true);
                s.Text(L("HiRISE terrain model, MOLA datum", "HiRISE 地形モデル(MOLA 基準)"), 24, 92, Size.Label, Soft); s.Text(L("slope computed by us", "傾斜はこちらで計算"), 24, 124, Size.Label, Soft);
            }
            probeAt = probe.alpha < .05f ? at : Vector3.Lerp(probeAt, at, 1 - Mathf.Exp(-18 * dt));
            var rot = Quaternion.LookRotation(probeAt - head, Vector3.up); float w = .23f * d;
            probe.SetWorldWidth(w); probe.go.transform.SetPositionAndRotation(probeAt + rot * new Vector3(w * .56f, w * .2f, 0), rot);   // beside the point, up and to the right, so the point itself stays in view
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
        public void ToggleFollow() { followTasks = !followTasks; sfx.Play("toggle", .7f); DrawMenu(); }
    }
}
