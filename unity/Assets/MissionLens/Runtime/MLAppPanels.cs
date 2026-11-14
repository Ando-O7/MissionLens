using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using static MissionLens.MLPanel;

namespace MissionLens
{
    // The surfaces of the app: what is written on them and where they sit. Layout numbers are pixels on each
    // surface (x right, y down), carried over from the desktop mock in experiments/spatial/.
    public partial class MLApp
    {
        MLPanel board, boardR, strip, wrist, hud, title, banner, legend;
        readonly List<MLPanel> panels = new List<MLPanel>();   // every surface that can hold buttons
        TextMeshProUGUI hudHeading;
        readonly Dictionary<string, Texture2D> viewTex = new Dictionary<string, Texture2D>();
        bool wristPlaced; float restartArmedUntil;

        void BuildPanels()
        {
            // PLAN, in the room: a briefing wall just behind the model, close enough that its text is read without
            // strain: two board panels turned slightly inward (left: the objective and why this station; right: the
            // NASA evidence) with the task timeline under them. About 1.3 m from the eyes and 50 degrees across.
            board = new MLPanel("Board left", 1100, 900, .625f, tableFrame);
            board.go.transform.localPosition = new Vector3(-.308f, .75f, .465f); board.go.transform.localRotation = Quaternion.Euler(2, -13, 0);
            boardR = new MLPanel("Board right", 1100, 900, .625f, tableFrame);
            boardR.go.transform.localPosition = new Vector3(.308f, .75f, .465f); boardR.go.transform.localRotation = Quaternion.Euler(2, 13, 0);
            strip = new MLPanel("Timeline", 1600, 270, 1.25f, tableFrame);
            strip.go.transform.localPosition = new Vector3(0, .365f, .5f); strip.go.transform.localRotation = Quaternion.Euler(11, 0, 0);
            // A sign at the model's near left corner: north, a scale bar, and the controls for the table itself.
            legend = new MLPanel("Map legend", 600, rig.xr ? 452 : 376, .3f, tableFrame);
            legend.go.transform.localPosition = new Vector3(-.6f, .075f, -.2f); legend.go.transform.localRotation = Quaternion.Euler(46, -24, 0);
            // FIELD: a wrist panel for details, raised on demand, and a head-up strip that stays out of the scenery.
            wrist = new MLPanel("Wrist", 600, 860, rig.xr ? .26f : .215f, rig.xr ? null : rig.Head);
            hud = new MLPanel("HUD", 1200, 170, rig.xr ? .7f : .5f, rig.xr ? null : rig.Head);
            // Before the briefing: what this is and where its contents come from.
            title = new MLPanel("Title", 1500, 880, 1.05f, rig.xr ? null : tableFrame);
            // Short messages on arrival and at the end, drawn over everything.
            banner = new MLPanel("Banner", 1400, 400, rig.xr ? .95f : .74f, rig.xr ? null : rig.Head, true);
            if (!rig.xr)
            {
                wrist.go.transform.localPosition = new Vector3(-.33f, -.075f, .46f); wrist.go.transform.localRotation = Quaternion.Euler(5.7f, -28.6f, 0);
                hud.go.transform.localPosition = new Vector3(0, -.262f, .6f);
                title.go.transform.localPosition = new Vector3(0, .58f, .46f);
                banner.go.transform.localPosition = new Vector3(0, .1f, 1.0f);
                rig.lookPitch = -3;
            }
            foreach (var s in M.stations)
            {
                pins.Add(new Pin { st = s, local = D.Local(s.x, s.z, .3f), label = new MLPanel("Pin " + s.id, 640, 128, .34f, null, true) });
                viewTex[s.id] = Resources.Load<Texture2D>("ML/view_" + s.id);
            }
            panels.AddRange(new[] { board, boardR, strip, legend, wrist, hud, title, banner }); panels.AddRange(pins.Select(p => p.label));
            foreach (var p in panels) p.Snap(false);
            DrawTitle();
        }

        // Surfaces a pointer can act on right now.
        IEnumerable<MLPanel> Surfaces()
        {
            if (stage == Stage.Title) { yield return title; yield break; }
            if (stage == Stage.Table) { yield return board; yield return boardR; yield return strip; yield return legend; }
            else { yield return wrist; yield return hud; }
            if (banner.Show && bannerHasButton) yield return banner;
            foreach (var p in pins) yield return p.label;
        }
        bool IsLabel(MLPanel s) => s == banner || pins.Any(p => p.label == s);

        MLPanel MakeMarkLabel(int n)
        {
            // A numbered tag on a short stem; the foot of the stem (bottom centre) is the marked point.
            var p = new MLPanel("Mark " + n, 96, 160, .05f, null, true);
            p.Stroke(48, 84, 48, 146, 5, Amber); p.Box(39, 138, 18, 18, 9, Amber);
            p.Box(14, 14, 68, 68, 12, new Color(8 / 255f, 10 / 255f, 14 / 255f, .88f), Amber);
            p.Text(n.ToString(), 48, 60, 36, Amber, true, 0);
            return p;
        }

        // Positions that depend on the head or on the moving terrain, and which surfaces show.
        void UpdateSurfaces(float dt)
        {
            var head = rig.Head.position; bool table = stage == Stage.Table; var yaw = Quaternion.Euler(0, rig.HeadYaw, 0);
            title.Show = stage == Stage.Title && !busy;
            board.Show = boardR.Show = strip.Show = legend.Show = table && introDone && roomO > .6f;
            hud.Show = !OnTable;
            if (banner.Show && bannerUntil > 0 && Time.time > bannerUntil) banner.Show = false;
            if (rig.xr)
            {
                // Title: in front of the person, following their turn loosely until the briefing begins.
                if (stage == Stage.Title) Chase(title, ref titleChasing, head + yaw * new Vector3(0, -.06f, 1.25f), head, 30, 3, dt);
                // Wrist panel: floats just above the left hand and shows while you look at it; or, asked for from
                // the head-up strip, stays where it was called up, low and to the left.
                // "Looking at it" means the hand is raised near the face and is not itself pointing ahead at something.
                var l = rig.left; bool looking = l.active && !l.held && Vector3.Distance(l.origin, head) < .75f && Vector3.Angle(rig.Head.forward, l.origin - head) < 34
                    && Vector3.Dot(l.rotation * Vector3.forward, rig.Head.forward) < .5f;
                wrist.Show = !OnTable && (wristUp || looking);
                if (wristUp) { if (!wristPlaced) { wristPlaced = true; Follow(wrist, head + yaw * new Vector3(-.3f, -.2f, .55f), head, 1); } }
                else if (looking) Follow(wrist, l.origin + Vector3.up * .17f, head, 1);
                // Head-up strip: low in view. It stays put while you look around a little, so it can be pointed
                // at, and comes round once you have turned away from it.
                Chase(hud, ref hudChasing, head + yaw * new Vector3(0, -.38f, .93f), head, 20, 5, dt);
            }
            else wrist.Show = !OnTable && wristUp;

            // Station labels float above their station on a thin leader, so the ground there stays in view.
            // Two stations close together in view get their labels on opposite sides; a label that would still
            // cover one placed before it (nearer stations first) is raised above it.
            foreach (var p in pins)
            {
                var to = world.TransformPoint(p.local) - head; p.dist = Mathf.Max(.05f, to.magnitude);
                p.az = Mathf.DeltaAngle(rig.HeadYaw, Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg) * Mathf.Deg2Rad; p.el0 = to.y / p.dist;
                bool hide = !p.shown || (!OnTable && Vector2.Distance(new Vector2(view.ax, view.az), new Vector2(p.st.x, p.st.z)) < 6)
                    || (table && introDone && !OnPatch(p.st.x, p.st.z, .004f));   // slid off the table
                p.label.Show = !hide;
            }
            foreach (var p in pins)
            {
                Pin near = null; float best = .2f;
                foreach (var q in pins)
                {
                    float gap = new Vector2(q.az - p.az, q.el0 - p.el0).magnitude;
                    if (q != p && q.label.Show && gap < best) { best = gap; near = q; }
                }
                int want = near != null && p.az < near.az ? -1 : 1;
                if (want != p.side) { p.side = want; DrawPin(p); }
            }
            placedPins.Clear();
            foreach (var p in pins.OrderBy(q => q.dist))
            {
                bool draw = p.label.Show || p.label.alpha > 0;
                if (p.leaderT.gameObject.activeSelf != draw) p.leaderT.gameObject.SetActive(draw);
                if (!draw) continue;
                var ground = world.TransformPoint(p.local); float d = p.dist, unit = .34f * p.k / 640f;
                p.lo = p.side > 0 ? p.az - 46 * unit : p.az - (p.boxW - 46) * unit; p.hi = p.lo + p.boxW * unit; p.hA = 104 * unit; p.el = p.el0 + (OnTable ? .032f : .05f);
                for (int pass = 0; pass < 4; pass++)
                    foreach (var q in placedPins)
                        if (p.lo < q.hi + .008f && p.hi > q.lo - .008f && Mathf.Abs(p.el - q.el) < (p.hA + q.hA) / 2 + .003f) p.el = q.el + (p.hA + q.hA) / 2 + .006f;
                placedPins.Add(p);
                float lift = (p.el - p.el0) * d, s = unit * d; var at = ground + Vector3.up * lift; var rot = Quaternion.LookRotation(at - head, Vector3.up);
                p.label.SetWorldWidth(640 * s); p.label.go.transform.SetPositionAndRotation(at + rot * Vector3.right * (274 * s * p.side), rot);
                float len = Mathf.Max(0, lift - 22 * s); var flat = Vector3.ProjectOnPlane(ground - head, Vector3.up);
                p.leaderT.SetPositionAndRotation(ground + Vector3.up * (len / 2), Quaternion.LookRotation(flat.sqrMagnitude > 1e-6f ? flat : Vector3.forward, Vector3.up));
                p.leaderT.localScale = new Vector3(.0016f * d, len, 1);
                var lc = p.col; lc.a = .85f * p.label.alpha; p.leader.SetColor("_Color", lc);
            }
            foreach (var m in marks)
            {
                var at = world.TransformPoint(m.local); float d = Mathf.Max(.05f, Vector3.Distance(at, head)), pop = 1 + 1.1f * (1 - EaseOut(Mathf.Clamp01((Time.time - m.born) / .45f)));
                float mw = (OnTable ? .03f : .05f) * d * pop; var mrot = Quaternion.LookRotation(at - head, Vector3.up);
                m.label.SetWorldWidth(mw); m.label.go.transform.SetPositionAndRotation(at + mrot * Vector3.up * (mw * 67f / 96f), mrot);
                m.label.Show = !table || OnPatch(m.x, m.z, .004f); m.label.Tick(dt, 8);
            }
            foreach (var p in panels) p.Tick(dt, p == board || p == boardR || p == strip || p == legend || p == title ? 2.6f : 5);
            if (hudHeading != null) hudHeading.text = $"HDG {MLData.Deg3(HeadingNow)}°";
            if (TurnSide() != hudSide && !OnTable) DrawHud();   // the turn hint changed
        }
        // -1 / +1 when the station to go to is outside the view to the left / right, else 0.
        int TurnSide()
        {
            string target = Target; if (target == null || OnTable) return 0;
            float rel = Mathf.DeltaAngle(HeadingNow, BearingFromHere(target).az), half = rig.xr ? 40 : Mathf.Atan(Mathf.Tan(rig.cam.fieldOfView * Mathf.Deg2Rad / 2) * rig.cam.aspect) * Mathf.Rad2Deg;
            return Mathf.Abs(rel) > half - 6 ? (rel < 0 ? -1 : 1) : 0;
        }
        int hudSide;
        readonly List<Pin> placedPins = new List<Pin>();
        bool titleChasing, hudChasing;
        // Keeps a surface in front of the person without tying it to every small movement of the head: it starts
        // to move only once it is `startDeg` off to the side (or left behind), and then glides until it is back.
        static void Chase(MLPanel p, ref bool chasing, Vector3 want, Vector3 head, float startDeg, float rate, float dt)
        {
            var t = p.go.transform;
            if (p.alpha < .05f) { t.position = want; chasing = false; }
            else
            {
                float off = Vector3.Angle(Vector3.ProjectOnPlane(t.position - head, Vector3.up), Vector3.ProjectOnPlane(want - head, Vector3.up));
                if (off > startDeg || Vector3.Distance(t.position, want) > .4f) chasing = true;
                if (chasing) { t.position = Vector3.Lerp(t.position, want, 1 - Mathf.Exp(-rate * dt)); if (Vector3.Distance(t.position, want) < .015f) chasing = false; }
            }
            t.rotation = Quaternion.LookRotation(t.position - head, Vector3.up);
        }
        static void Follow(MLPanel p, Vector3 want, Vector3 head, float k)
        {
            var t = p.go.transform; t.position = Vector3.Lerp(t.position, want, k); t.rotation = Quaternion.LookRotation(t.position - head, Vector3.up);
        }

        void Refresh()
        {
            bool table = OnTable; var T = Task; int selLeg = M.tasks[selTask].leg; string target = Target;
            foreach (var p in pins)
            {
                var s = p.st; bool isNext = s.id == target; int mk = marks.Count(m => m.station == s.id);
                string sub = "";
                if (table) sub = string.Join(" · ", new[] { s.id == here ? (taskIndex == 0 ? "start" : Finished ? "finish" : "the Marswalk is here") : "", mk > 0 ? mk + " marked" : "" }.Where(x => x.Length > 0));
                else if (s.id != At) sub = $"{BearingFromHere(s.id).d:0} m" + (stage == Stage.Field && St(At).hidden.Contains(s.id) ? " · behind terrain" : "");
                bool full = table ? s.id == sel : isNext || preview != null || (Finished && s.id != here);
                var col = table && s.id == sel ? Cyan : isNext && !table ? Amber : Color.white;
                p.k = table && !full ? .72f : 1; p.text = full ? s.id + " " + s.name : s.id; p.sub = full ? sub : sub.Split(new[] { " · " }, System.StringSplitOptions.None)[0]; p.col = col;
                DrawPin(p);
            }
            foreach (var o in legs)
            {
                bool fwd = o.leg.from == here && o.leg.to == target, back = o.leg.to == here && o.leg.from == target;
                bool next = table ? o.index == selLeg : fwd || back;
                // Over the real image only the leg about to be walked is drawn, and not at the person's feet.
                o.go.SetActive(table || next || stage != Stage.Field); o.back = back;
                var c = next || !table ? Cyan : Color.white; c.a = table ? (next ? .98f : .42f) : next ? (stage == Stage.Field ? .5f : .7f) : .2f;
                o.mat.SetColor("_Color", c); o.mat.SetFloat("_Flow", next ? 1 : 0); o.mat.SetFloat("_Tint", next ? 1 : 0);
                o.mat.SetVector("_Near", table ? Vector4.zero : new Vector4(3, 14, 0, 0));
            }
            if (stage == Stage.Title) return;
            if (table) { DrawBoard(); DrawStrip(); DrawLegend(); } else { DrawWrist(); DrawHud(); }
        }

        void DrawPin(Pin p)
        {
            var l = p.label; l.Clear(); string text = p.text, sub = p.sub; var col = p.col; bool left = p.side < 0;
            float w = Mathf.Min(640, Mathf.Max(l.Measure(text, 38, true), sub.Length > 0 ? l.Measure(sub, 27) : 0) + 104), x0 = left ? 640 - w : 0; p.boxW = w;
            l.Box(x0, 14, w, 100, 20, new Color(8 / 255f, 10 / 255f, 14 / 255f, .86f), new Color(col.r, col.g, col.b, .35f));
            float ix = left ? 594 : 46; l.Box(ix - 20, 44, 40, 40, 20, new Color(0, 0, 0, 0), col); l.Box(ix - 8, 56, 16, 16, 8, col);
            float tx = left ? 560 : 80; int al = left ? 1 : -1;
            l.Text(text, tx, sub.Length > 0 ? 58 : 77, 38, col, true, al);
            if (sub.Length > 0) l.Text(sub, tx, 97, 27, Soft, false, al);
            string id = p.st.id;
            l.Hit("pin:" + id, x0, 14, w, 100, () => PressPin(id), 20);
        }

        // On the table a label selects its station. Inside the terrain it takes you to the surface image there.
        // In FIELD it starts the walk if that station is the next stop; looking ahead, it hops to that station.
        void PressPin(string id)
        {
            if (stage == Stage.Table) SelectStation(id);
            else if (stage != Stage.Field || preview != null) ToField(id);
            else if (id == Target) Travel(id);
            else { sfx.Play("deny"); Flash(Finished ? "The Marswalk is complete. Review it at the table." : Task.station == here ? $"Finish the task here first: {Task.phase}" : $"Not the next stop. The current task is at {Task.station} {St(Task.station).name}."); }
        }

        // Before the briefing. Says what the app is and, first of all, which of its contents are NASA's and which are ours.
        void DrawTitle()
        {
            var s = title; s.Clear(); const float X = 90; float W = s.w - 2 * X;
            s.Box(1.5f, 1.5f, s.w - 3, s.h - 3, 44, Back, Line);
            s.Text("MISSION LENS", X, 156, 108, Ink, true);
            s.Text("From science objectives to Marswalk execution", X, 222, 42, Cyan, true);
            float y = s.Wrap("One hypothetical crewed Marswalk at the front of Jezero Crater's fan. Plan it on a tabletop terrain model, then stand at each station inside the panorama Perseverance took there.", X, 306, W, 50, 37, Ink);
            y += 30;
            foreach (var (tag, col, text) in new[] {
                ("NASA OBSERVATION", Ink, "HiRISE image and terrain model · Mastcam-Z panoramas · CRISM spectral product · MEDA weather · Mars 2020 sampling record"),
                ("OUR CALCULATION", Cyan, "Route, slopes, lines of sight, walking times, dataset alignment"),
                ("SIMULATED", Amber, "The Marswalk itself, its tasks and timeline, and whatever you mark") })
            {
                s.Box(X, y - 30, 280, 44, 22, new Color(1, 1, 1, .07f), new Color(col.r, col.g, col.b, .7f)); s.Text(tag, X + 140, y, 22, col, true, 0);
                y = s.Wrap(text, X + 312, y, W - 312, 38, 28, Soft) + 22;
            }
            s.Button("Begin briefing", X, 724, 440, 92, Begin, primary: true, px: 38);
            s.Text(rig.xr ? "Point with a hand and pinch, or pull a controller trigger" : "Click, or press Enter · drag to look around", X + 476, rig.xr ? 764 : 782, 26, Gray);
            if (rig.xr) s.Text("No hand or controller in view? Look at a button and hold still.", X + 476, 800, 22, Dim);
            s.Text("Data: NASA/JPL-Caltech/UArizona (HiRISE) · NASA/JPL-Caltech/ASU/MSSS (Mastcam-Z) · NASA/JPL-Caltech/JHUAPL (CRISM) · NASA/JPL-Caltech/CAB (MEDA)", X, 850, 19, Dim);
            // which build this is, so that it can be told apart from the one before it
            var stamp = Resources.Load<TextAsset>("ML/build");
            if (stamp != null) s.Text("Build " + stamp.text.Trim(), X + W, 68, 22, Gray, false, 1);
        }

        // The sign beside the model: which way is north on the table, a scale bar, the scale, and the table's own
        // controls. Redrawn as the map is slid, stretched or turned.
        void DrawLegend()
        {
            var s = legend; s.Clear(); const float X = 28; float W = s.w - 2 * X, pxPerM = tv.S / (.3f / s.w);
            s.Box(1.5f, 1.5f, s.w - 3, s.h - 3, 26, Back, Line);
            // north: an arrow in a ring, turned as the map is turned
            float cx = X + 46, cy = 74, r = 40, th = tv.yawRel * Mathf.Deg2Rad; Vector2 up = new Vector2(Mathf.Sin(th), -Mathf.Cos(th)), side = new Vector2(-up.y, up.x);
            s.Box(cx - r, cy - r, 2 * r, 2 * r, r, new Color(0, 0, 0, 0), new Color(1, 1, 1, .25f));
            Vector2 c = new Vector2(cx, cy), tip = c + up * (r - 8), tail = c - up * (r - 12), barb = tip - up * 20;
            s.Stroke(tail.x, tail.y, tip.x, tip.y, 6, Ink); s.Stroke(tip.x, tip.y, barb.x + side.x * 13, barb.y + side.y * 13, 6, Ink); s.Stroke(tip.x, tip.y, barb.x - side.x * 13, barb.y - side.y * 13, 6, Ink);
            Vector2 n = c + up * (r + 20); s.Text("N", n.x, n.y + 11, 30, Ink, true, 0);
            // scale bar: the longest round length that fits
            float len = 10; foreach (var l in new[] { 20f, 50f, 100f, 200f, 500f }) if (l * pxPerM <= 360) len = l;
            float bar = len * pxPerM, bx = X + 150; const float by = 78;
            s.Box(bx, by - 6, bar, 12, 3, Ink); s.Box(bx, by - 6, bar / 2, 12, 3, Gray);
            s.Text("0", bx, by - 20, 23, Soft, false, 0); s.Text($"{len / 2:0}", bx + bar / 2, by - 20, 23, Soft, false, 0); s.Text($"{len:0} m", bx + bar, by - 20, 23, Soft, false, 0);
            s.Text($"{ScaleText} · height {(tableEx > 1 ? "×2" : "as measured")}", bx, by + 44, 29, Ink, true);
            s.Text("MRO HiRISE terrain model, 2 m grid · walls and base drawn by us", X, 168, 18, Gray);
            float bw = (W - 14) / 2;
            s.Button("Whole route", X, 190, bw, 64, WholeRoute, px: 25);
            s.Button(followTasks ? "Follow tasks: on" : "Follow tasks: off", X + bw + 14, 190, bw, 64, ToggleFollow, followTasks, px: 25, key: "Follow tasks");
            float y = 286;
            if (rig.xr)
            {
                s.Button("Bring table here", X, 268, bw, 64, PlaceTable, px: 25);
                s.Button(studio ? "Room: studio" : "Room: yours", X + bw + 14, 268, bw, 64, ToggleStudio, studio, px: 25, key: "Room");
                y = 364;
            }
            s.Wrap(rig.xr ? "Pinch the model and move to slide it. Pinch with both hands to stretch and turn it. \"Follow tasks\" brings each task's place onto the table."
                          : "Drag the model to slide it, wheel to zoom, Q / E to turn. \"Follow tasks\" brings each task's place onto the table.", X, y, W, 24, 19, Gray);
        }

        // PLAN: left, what the Marswalk is for and why the selected station serves it; right, the NASA evidence.
        // After the last task the left side becomes the review of the run.
        void DrawBoard()
        {
            var b = board; b.Clear(); var st = St(sel); const float X1 = 48, W1 = 990, X2 = 30, W2 = 1040; float y;
            b.Box(1.5f, 1.5f, b.w - 3, b.h - 3, 36, Back, Line);
            b.Text("MISSION LENS", X1, 62, 24, Gray, true); b.Text(Finished ? "PLAN · REVIEW" : "PLAN", X1 + 196, 62, 24, Cyan, true);
            b.Text(Finished ? "SIMULATED RUN" : "HYPOTHETICAL MARSWALK", X1 + W1, 62, 24, Amber, true, 1);
            int mk = marks.Count(m => m.station == st.id);
            if (!Finished)
            {
                b.Text(M.title, X1, 120, 46, Ink, true); b.Text($"{M.subtitle} · {M.routeTotalM:0} m on foot · {MLData.Hm(M.plannedTotalMin)} planned", X1, 160, 27, Gray);
                b.Text("SCIENCE OBJECTIVE", X1, 216, 24, Gray, true); y = b.Wrap(M.objective, X1, 258, W1, 41, 32, Ink);
                y = b.Wrap("NASA record · " + M.objectiveBasis, X1, y - 2, W1, 32, 25, Gray);
                b.Text($"WHY STATION {st.id} · {st.name.ToUpper()}", X1, y + 32, 24, Cyan, true); y = b.Wrap(st.why, X1, y + 72, W1, 41, 32, Ink);
                y = b.Wrap($"Elevation {st.elev:0.0} m (MOLA datum) · ground slope {st.slopeDeg}° · in view from here: {(st.sees.Length > 0 ? string.Join(", ", st.sees) : "none")}{(st.hidden.Length > 0 ? " · hidden: " + string.Join(", ", st.hidden) : "")} (our calculation from the terrain model)", X1, y, W1, 32, 25, Gray);
                if (st.id == M.medaStation) { b.Text("NASA observation · MEDA · " + M.medaLine, X1, y, 25, Soft); y += 32; }
                if (mk > 0) b.Text($"Recorded in FIELD · simulated: {mk} candidate{(mk > 1 ? "s" : "")}, marked on the map", X1, y + 2, 25, Amber, true);
                // The way into FIELD proper: at the station the Marswalk has reached, with the task that is due.
                var T = Task; string go = taskIndex == 0 ? "Begin Marswalk" : "Resume Marswalk";
                b.Button(go, X1, 752, 330, 72, () => ToField(here), primary: true, px: 30, key: "Marswalk");
                b.Text($"at {here} {St(here).name} · task {taskIndex + 1} of {M.tasks.Length}: {T.phase}{(T.station != here ? " " + T.station : "")}", X1 + 356, 782, 25, Soft);
                b.Text("FIELD · real surface imagery at each station", X1 + 356, 814, 21, Dim);
                b.Wrap(M.scenarioNote, X1, 862, W1, 24, 20, Dim);
            }
            else
            {
                b.Text("Marswalk complete", X1, 120, 46, Ink, true);
                b.Text($"{M.title} · {M.tasks.Length} tasks stepped through · {M.routeTotalM:0} m on foot · {MLData.Hm(M.plannedTotalMin)} planned", X1, 160, 27, Gray);
                b.Text("WHAT WAS RECORDED · SIMULATED", X1, 216, 24, Amber, true); y = 262;
                if (marks.Count == 0) y = b.Wrap("No candidates were marked on this run.", X1, y, W1, 41, 32, Ink);
                foreach (var g in marks.GroupBy(m => m.station))
                {
                    b.Text($"{g.Key} {St(g.Key).name} · {g.Count()} candidate{(g.Count() > 1 ? "s" : "")}", X1, y, 32, Ink, true); y += 38;
                    string line = string.Join("   ", g.Take(6).Select(m => $"#{m.n}  {m.d:0} m at {MLData.Deg3(m.az)}°")) + (g.Count() > 6 ? "   …" : "");
                    y = b.Wrap(line, X1, y, W1, 34, 26, Soft) + 10;
                }
                y = b.Wrap(M.recordNote, X1, y + 4, W1, 31, 23, Gray);
                b.Text("WHAT THIS RUN DID NOT MEASURE", X1, y + 30, 24, Gray, true);
                b.Wrap("No time was measured: T+ follows the planned timeline, which keeps the task structure and durations of the BASALT-2 planned EVA. Nothing here was checked against a real crew, suit or surface operation.", X1, y + 70, W1, 34, 26, Soft);
                b.Button("Start over", X1, 800, 240, 64, Restart, px: 26);
                b.Text("Clears the run and returns to the title", X1 + 262, 841, 22, Dim);
            }

            // right panel: the NASA evidence for the selected station, and what can be done with it
            b = boardR; b.Clear(); b.Box(1.5f, 1.5f, b.w - 3, b.h - 3, 36, Back, Line);
            const float ih = 300; b.Box(X2, 48, W2, ih, 16, new Color(.1f, .12f, .14f, 1));
            if (viewTex.TryGetValue(sel, out var tex) && tex != null) { float vh = (tex.width * ih / W2) / tex.height; b.Picture(tex, X2, 48, W2, ih, new Rect(0, (1 - vh) / 2, 1, vh)); }
            b.Tag("NASA OBSERVATION", X2 + 14, 60, Ink); b.Text($"Mastcam-Z panorama, {st.panoSols}, looking {MLData.Deg3(st.viewAz)}° · {st.panoCredit}", X2, 48 + ih + 30, 23, Gray);
            y = 48 + ih + 78; b.Text(st.hasSample ? "NASA SAMPLING RECORD" : "NASA ROVER RECORD", X2, y, 24, Gray, true);
            y = b.Wrap(st.hasSample ? $"Rock cores {st.sampleName}: {st.sampleRock.ToLower()}, {st.sampleLocation.ToLower()}. Taken {st.sampleDate} (sols {st.sampleSol})." : st.note, X2, y + 38, W2, 37, 29, Ink);
            int p = st.crismRank; b.Text($"SPECTRAL CONTEXT · MRO CRISM · {M.crismMPerPx:0} M PER PIXEL", X2, y + 24, 24, Gray, true);
            b.Wrap($"{M.crismBands[0]} band here: {(p >= 50 ? $"stronger than in {p}%" : $"weaker than in {100 - p}%")} of this area (our reading). NASA label: Mg/Fe-OH bearing minerals may appear red; spectra are needed for positive identification.", X2, y + 60, W2, 35, 27, Ink);
            float x = X2; const float by = 762; string s = sel;
            b.Text("ON THE MODEL", x, by - 14, 19, Dim, true); b.Text("LOOK AHEAD AT " + s, x + 584, by - 14, 19, Dim, true);
            b.Button("Slope", x, by, 150, 64, () => SetLayer("slope"), layer == "slope", px: 26); x += 166;
            b.Button("Spectral", x, by, 180, 64, () => SetLayer("crism"), layer == "crism", px: 26); x += 196;
            b.Button("Height ×2", x, by, 190, 64, ToggleHeight, tableEx > 1, px: 26); x += 222;
            b.Button("Preview 1 : 25", x, by, 216, 64, () => EnterTerrain(s, SGiant), px: 25); x += 232;
            b.Button("Stand at " + s, x, by, 224, 64, () => ToField(s), px: 26);
            const float ly = 866;
            if (layer == "slope")
            {
                float lx = X2;
                foreach (var (c, t) in new[] { (new Color(1, .84f, .25f), "10–15°"), (new Color(1, .55f, .16f), "15–20°"), (new Color(.92f, .2f, .2f), "over 20°") })
                { b.Box(lx, ly - 19, 22, 22, 4, c); b.Text(t, lx + 30, ly, 23, Ink); lx += 46 + b.Measure(t, 23); }
                b.Text("· slope and 5 m contours: HiRISE terrain model; classes ours", lx, ly, 23, Gray);
            }
            else if (layer == "crism") b.Text($"{M.crismObservation} PHY: R {M.crismBands[0]}, G {M.crismBands[1]}, B {M.crismBands[2]} · moved {M.crismShiftM:0} m by us to fit HiRISE · brightness ×{M.crismGain}", X2, ly, 21, Gray);
            else b.Text("On the table: MRO HiRISE image draped over the HiRISE terrain model", X2, ly, 23, Gray);
        }

        // PLAN: the task timeline. Each segment selects its task, station and leg.
        void DrawStrip()
        {
            var s = strip; s.Clear(); var T = M.tasks[selTask]; const float X0 = 30; float W = s.w - 60, k = W / M.plannedTotalMin; var leg = T.leg >= 0 ? M.legs[T.leg] : null;
            s.Box(1.5f, 1.5f, s.w - 3, s.h - 3, 28, Back, Line);
            s.Text($"{T.station} {St(T.station).name} · {T.phase}", X0, 52, 30, Ink, true);
            s.Text($"{T.plannedMin} min · {MLData.Hm(T.startMin)}–{MLData.Hm(T.endMin)}{(T.index == taskIndex ? " · current task" : T.index < taskIndex ? " · done" : "")}", X0 + W - 150, 52, 25, T.index == taskIndex ? Amber : Gray, false, 1);
            // step through the tasks in order, for hands that find the short segments hard to hit
            int ti = selTask;
            s.Button("‹", X0 + W - 128, 18, 58, 48, () => SelectTask(ti - 1), dim: ti == 0, px: 30, key: "PrevTask");
            s.Button("›", X0 + W - 58, 18, 58, 48, () => SelectTask(ti + 1), dim: ti == M.tasks.Length - 1, px: 30, key: "NextTask");
            string line = leg != null ? $"Walk {leg.distM:0} m from {leg.from} · climb {leg.ascentM:0} m · steepest {leg.slopeMaxDeg:0}° · {Mathf.RoundToInt(leg.pctOnTrack * 100)}% on Perseverance's track · about {Mathf.RoundToInt(leg.estWalkMin)} min at {M.walkSpeed} m/s" : string.Join("  ·  ", T.checks);
            s.Text(line, X0, 90, 23, Gray);
            if (leg != null)
            {   // what the colours on the selected leg mean (the slope layer's classes)
                float lx = X0 + W, textEnd = X0 + s.Measure(line, 23);
                foreach (var (c, t) in new[] { (Slope20, "over 20°"), (Slope15, "15–20°"), (Slope10, "10–15°") })
                { lx -= s.Measure(t, 20); s.Text(t, lx, 90, 20, Soft); lx -= 26; s.Box(lx, 74, 18, 18, 4, c); lx -= 16; }
                if (lx - s.Measure("slope on the route", 20) > textEnd + 24) s.Text("slope on the route", lx, 90, 20, Dim, false, 1);
            }
            foreach (var t in M.tasks)
            {
                float x = X0 + t.startMin * k, w = t.plannedMin * k - 4; bool on = t.index == selTask; int i = t.index;
                s.Box(x, 114, w, 62, 8, on ? Cyan : t.station == sel ? new Color(95 / 255f, 212 / 255f, 1, .32f) : new Color(1, 1, 1, t.index < taskIndex ? .27f : .12f), t.index == taskIndex && !Finished ? Amber : (Color?)null);
                // station, and under it what kind of task, where the segment is wide enough to hold the word
                bool two = w >= 74;
                if (w > 44) s.Text((t.leg >= 0 ? "→ " : "") + t.station, x + w / 2, two ? 143 : 154, 24, on ? OnCyan : Ink, true, 0);
                if (two) s.Text(ShortPhase(t), x + w / 2, 165, 16, on ? OnCyan : Soft, false, 0);
                s.Hit("task:" + i, x, 106, w + 4, 78, () => SelectTask(i), 10);
            }
            for (int m = 0; m < M.plannedTotalMin; m += 60) s.Text(MLData.Hm(m), X0 + m * k, 212, 20, Gray);
            s.Text(MLData.Hm(M.plannedTotalMin), X0 + W, 212, 20, Gray, false, 1);
            s.Text("Timeline after the BASALT-2 planned EVA (Beaton et al. 2019, Table 2) · walking times and slope classes are ours · amber outline: current task", X0, 248, 20, Dim);
            bool armed = Time.time < restartArmedUntil; float rw = armed ? 250 : 120;
            s.Button(armed ? "Press again to restart" : "Restart", s.w - 30 - rw, 222, rw, 40, () => { if (Time.time < restartArmedUntil) Restart(); else { restartArmedUntil = Time.time + 3; Refresh(); } }, armed, px: 19, key: "Restart");
        }

        static string ShortPhase(MTask t) => t.leg >= 0 ? (t.phase == "Return" ? "return" : "walk") : t.phase.StartsWith("Contextual") ? "survey" : t.phase.StartsWith("Candidate") ? "candidate search"
            : t.phase.StartsWith("Presampling") ? "presampling survey" : t.phase.StartsWith("Sampling") ? "sampling" : t.phase.ToLower();

        void DrawWrist()
        {
            var s = wrist; s.Clear(); const float PW = 600, PH = 860; var T = Task; float y = 54, yb = PH - 196;
            s.Box(1.5f, 1.5f, PW - 3, PH - 3, 34, new Color(12 / 255f, 15 / 255f, 20 / 255f, .95f), new Color(1, 1, 1, .22f));
            s.Text("MISSION LENS", 34, y, 20, Gray, true); s.Text(stage == Stage.Giant ? "PLAN · ROUTE PREVIEW" : stage == Stage.Walk ? "TERRAIN MODEL" : preview != null ? "PLAN · LOOKING AHEAD" : "FIELD", PW - 34, y, 20, Cyan, true, 1); y += 44;
            s.Text(M.title, 34, y, 34, Ink, true); y += 36;
            s.Text($"T+ {MLData.Hm(Pet)} of {MLData.Hm(M.plannedTotalMin)} planned · task {Mathf.Min(taskIndex + 1, M.tasks.Length)}/{M.tasks.Length}", 34, y, 22, Soft); y += 18;
            s.Box(34, y, PW - 68, 6, 3, new Color(1, 1, 1, .12f)); if (Pet > 0) s.Box(34, y, (PW - 68) * Pet / M.plannedTotalMin, 6, 3, Cyan); y += 44;
            if (stage == Stage.Giant || stage == Stage.Walk)
            {
                var n = Nearest(); string nid = n.id;
                s.Text(stage == Stage.Giant ? "Inside the terrain model, 1 : 25" : "Inside the terrain model, true scale", 34, y, 26, Ink, true); y += 40;
                y = s.Wrap("Orbital data only: shape from the HiRISE terrain model, colour from the HiRISE image. No rover imagery here. Sky and haze are drawn by us.", 34, y, PW - 68, 28, 21, Soft) + 8;
                s.Wrap($"Planned route is the blue path. Nearest station: {n.id} {St(n.id).name}, {n.d:0} m.", 34, y, PW - 68, 28, 21, Ink);
                s.Button("1 : 25", 34, yb, 258, 62, () => Rescale(SGiant), stage == Stage.Giant); s.Button("True scale", 308, yb, 258, 62, () => Rescale(1), stage == Stage.Walk);
                s.Button("Surface image at " + nid, 34, yb + 76, 340, 62, () => ToField(nid)); s.Button("Table", 390, yb + 76, 176, 62, ToTable);
            }
            else
            {
                var st = St(At); int mine = marks.Count(m => m.station == At); bool rec = CanMark;
                s.Text($"{st.id}  {st.name}", 34, y, 30, Ink, true); y += 32;
                s.Text($"Mastcam-Z, {st.panoSols} · {st.panoCredit}", 34, y, 19, Soft); y += 25;
                y = s.Wrap("Ours: its direction, the sky above it, and the terrain model with a 1 m grid where it has no pixels.", 34, y, PW - 68, 24, 19, Gray) + 3;
                if (st.hasSample) y = s.Wrap($"NASA record: rock cores {st.sampleName} were taken within about {Mathf.CeilToInt(st.sampleDistM)} m of this stop.", 34, y, PW - 68, 26, 20, Soft);
                if (st.id == M.medaStation) { s.Text(M.medaLine, 34, y, 20, Soft); y += 26; }
                y += 20;
                if (preview != null)
                {   // looking ahead from the briefing: what is planned here, and nothing to do yet
                    s.Text("PLANNED HERE", 34, y, 21, Gray, true); y += 34;
                    foreach (var t in M.tasks.Where(t => t.station == st.id).Take(5))
                    {
                        s.Text(MLData.Hm(t.startMin), 34, y, 22, t.index < taskIndex ? Dim : Soft); s.Text(t.phase, 104, y, 22, t.index < taskIndex ? Dim : Ink, t.index == taskIndex);
                        s.Text(t.plannedMin + " min", PW - 34, y, 22, Soft, false, 1); y += 30;
                    }
                    s.Wrap($"Looking ahead from the briefing. The Marswalk is at {here} {St(here).name}; nothing is recorded here yet.", 34, y + 24, PW - 68, 26, 20, Amber);
                    s.Button("Back to the table", 34, yb, PW - 68, 62, ToTable, primary: true);
                    string ph = st.id; s.Button("Step out to terrain", 34, yb + 76, PW - 68, 62, () => EnterTerrain(ph, 1));
                }
                else
                {
                    if (Finished) { s.Text("All tasks complete", 34, y, 26, Cyan, true); s.Wrap("Go back to the table to review what was recorded.", 34, y + 36, PW - 68, 28, 21, Soft); }
                    else if (T.station == here && T.leg < 0)
                    {
                        s.Text($"TASK · {T.plannedMin} MIN", 34, y, 21, Gray, true);
                        if (rec) s.Text(mine > 0 ? $"{mine} marked · simulated record" : rig.xr ? "point and pinch to mark" : "point and click to mark", PW - 34, y, 20, Amber, mine > 0, 1);
                        y += 36; s.Text(T.phase, 34, y, 28, Ink, true); y += 22;
                        for (int i = 0; i < T.checks.Length; i++)
                        {
                            bool on = done.Contains(i); float y0 = y; int ci = i;
                            s.Box(36, y + 6, 30, 30, 7, on ? Cyan : new Color(0, 0, 0, 0), on ? Cyan : Hex("8b9097")); if (on) s.Check(36, y + 6, 30, OnCyan);
                            y = s.Wrap(T.checks[i], 84, y + 30, PW - 124, 28, 22, on ? Hex("8b9097") : Ink) + 6;
                            s.Hit("check:" + i, 28, y0 - 2, PW - 56, y - y0, () => ToggleCheck(ci), 10);
                        }
                        if (rec) s.Wrap("A mark's position comes from the terrain model and is approximate.", 34, y + 22, PW - 68, 24, 19, Gray);
                    }
                    else
                    {
                        var b = BearingFromHere(T.station); bool seen = st.sees.Contains(T.station);
                        s.Text("NEXT", 34, y, 21, Gray, true); y += 36; s.Text($"{T.phase} · {St(T.station).name}", 34, y, 28, Ink, true); y += 36;
                        s.Text($"{b.d:0} m straight · bearing {MLData.Deg3(b.az)}° · {(seen ? "in view" : "behind terrain")}", 34, y, 23, seen ? Ink : Amber); y += 40;
                        var leg = M.legs.FirstOrDefault(l => (l.from == here && l.to == T.station) || (l.to == here && l.from == T.station));
                        if (leg != null) s.Wrap($"Planned route {leg.distM:0} m, steepest {leg.slopeMaxDeg:0}°, about {Mathf.RoundToInt(leg.estWalkMin)} min at {M.walkSpeed} m/s (our assumption).", 34, y, PW - 68, 26, 20, Soft);
                    }
                    bool go = !Finished && T.station != here; float cw = rec && mine > 0 ? 350 : PW - 68; string to = T.station;
                    if (Finished) s.Button("Review at the table", 34, yb, PW - 68, 62, ToTable, primary: true);
                    else if (go) s.Button($"Walk to {to} {St(to).name}", 34, yb, PW - 68, 62, () => Travel(to), primary: true);
                    else s.Button("Complete task", 34, yb, cw, 62, CompleteTask, done.Count == T.checks.Length);
                    if (!Finished && !go && rec && mine > 0) s.Button("Undo mark", 34 + cw + 14, yb, PW - 68 - cw - 14, 62, UndoMark);
                    string h = here; s.Button("Table", 34, yb + 76, 170, 62, ToTable); s.Button("Step out to terrain", 220, yb + 76, 346, 62, () => EnterTerrain(h, 1));
                }
            }
            s.Text("Hypothetical Marswalk · data: NASA", 34, PH - 22, 18, Dim);
        }

        void DrawHud()
        {
            if (hud == null || OnTable) return;
            var s = hud; s.Clear(); var T = Task; float W = s.w; bool fin = Finished; string top, main, sub; var col = Ink; string target = Target;
            if (stage == Stage.Field && preview != null)
            {
                var st = St(preview); var here2 = M.tasks.Where(t => t.station == st.id && t.leg < 0).ToArray();
                top = $"LOOKING AHEAD · {st.id} · {st.name.ToUpper()}"; col = Ink; main = "What you will see on arrival";
                sub = $"{here2.Length} task{(here2.Length == 1 ? "" : "s")} planned here, {here2.Sum(t => t.plannedMin)} min in all · the Marswalk is at {here} {St(here).name}";
            }
            else if (stage == Stage.Field)
            {
                var st = St(here); top = $"{st.id} · {st.name.ToUpper()}";
                if (fin) { main = "All tasks complete"; sub = "Go back to the table to review the Marswalk"; }
                else if (T.station == here && T.leg < 0) { main = T.phase; sub = $"{done.Count}/{T.checks.Length} checks · {T.plannedMin} min planned{(T.recordCheck >= 0 ? (rig.xr ? " · point and pinch to mark a candidate" : " · point and click to mark a candidate") : "")}"; }
                else { var b = BearingFromHere(T.station); main = $"Next: {T.station} {St(T.station).name}"; col = Amber; sub = $"{T.phase} · {b.d:0} m · bearing {MLData.Deg3(b.az)}° · {(st.sees.Contains(T.station) ? "in view" : "behind terrain")}"; }
            }
            else
            {
                var n = Nearest(); top = stage == Stage.Giant ? "ROUTE PREVIEW · 1 : 25" : "TERRAIN MODEL · TRUE SCALE";
                main = travelling != null ? $"Walking to {travelling} {St(travelling).name}" : "Orbital data only"; sub = $"Nearest station: {n.id} {St(n.id).name}, {n.d:0} m";
            }
            if (flashMsg.Length > 0) sub = flashMsg;
            // Left: where you are and what is due. Right: the step that is due as one button, then the panel and the table.
            float R = W - 304, bx = W - 280;
            s.Box(0, 0, W, s.h, 26, new Color(10 / 255f, 12 / 255f, 16 / 255f, .88f));
            if (!rig.xr) s.Hit("hud", 0, 0, W, s.h, ToggleWrist, 26, true);
            s.Text(top, 40, 48, 23, stage == Stage.Field && preview != null ? Cyan : Gray, true);
            s.Text($"T+ {MLData.Hm(Pet)} / {MLData.Hm(M.plannedTotalMin)}", R - 132, 48, 23, Gray, true, 1);
            hudHeading = s.Text($"HDG {MLData.Deg3(HeadingNow)}°", R, 48, 23, Gray, true, 1);
            s.Text(main, 40, 104, 38, col, true); s.Text(sub, 40, 146, 24, flashMsg.Length > 0 ? Amber : Soft);
            hudSide = TurnSide();   // which way to turn when the next station is outside the view
            if (hudSide != 0) s.Text(hudSide < 0 ? $"« {target}" : $"{target} »", R, 104, 34, Amber, true, 1);
            s.Box(W - 292, 18, 1.5f, s.h - 36, 1, new Color(1, 1, 1, .14f));
            string act = null; System.Action fn = null; bool on = false;
            if (stage == Stage.Field && preview != null) { act = "Back to the table"; fn = ToTable; }
            else if (stage == Stage.Field && fin) { act = "Review at the table"; fn = ToTable; }
            else if (stage == Stage.Field && T.station != here) { string to = T.station; act = "Walk to " + to; fn = () => Travel(to); }
            else if (stage == Stage.Field) { act = "Complete task"; fn = CompleteTask; on = done.Count == T.checks.Length; }
            else if (travelling == null) { string nid = Nearest().id; act = "Surface image at " + nid; fn = () => ToField(nid); }
            if (act != null) s.Button(act, bx, 20, 252, 60, fn, on, primary: true, px: 23, key: "HudAction");
            else s.Button("Walking…", bx, 20, 252, 60, () => { }, dim: true, px: 23);
            s.Button(wristUp ? "Close" : "Panel", bx, 92, 120, 56, ToggleWrist, wristUp, px: 22, key: "Panel");
            s.Button("Table", bx + 132, 92, 120, 56, ToTable, px: 22, key: "HudTable");
        }
    }
}
