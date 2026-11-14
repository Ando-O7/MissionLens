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
        MLPanel board, strip, wrist, hud;
        TextMeshProUGUI hudHeading;
        readonly Dictionary<string, Texture2D> viewTex = new Dictionary<string, Texture2D>();

        void BuildPanels()
        {
            // PLAN, in the room: a briefing board behind the table and the task timeline along its near edge.
            board = new MLPanel("Board", 2200, 860, 2.2f, tableFrame);
            board.go.transform.localPosition = new Vector3(0, .453f, .61f); board.go.transform.localRotation = Quaternion.Euler(15, 0, 0);
            strip = new MLPanel("Timeline", 1600, 270, 1.0f, tableFrame);
            strip.go.transform.localPosition = new Vector3(0, .048f, -.52f); strip.go.transform.localRotation = Quaternion.Euler(55.6f, 0, 0);
            // FIELD: a wrist panel for details, raised on demand, and a head-up strip that stays out of the scenery.
            wrist = new MLPanel("Wrist", 600, 780, .215f, rig.xr ? null : rig.Head);
            hud = new MLPanel("HUD", 1200, 170, rig.xr ? .7f : .5f, rig.xr ? null : rig.Head);
            if (!rig.xr)
            {
                wrist.go.transform.localPosition = new Vector3(-.33f, -.1f, .46f); wrist.go.transform.localRotation = Quaternion.Euler(5.7f, -28.6f, 0);
                hud.go.transform.localPosition = new Vector3(0, -.262f, .6f);
            }
            foreach (var s in M.stations)
            {
                pins.Add(new Pin { st = s, left = s.id == "C", local = D.Local(s.x, s.z, 1.5f), label = new MLPanel("Pin " + s.id, 640, 128, .34f, null, true) });
                viewTex[s.id] = Resources.Load<Texture2D>("ML/view_" + s.id);
            }
        }

        IEnumerable<MLPanel> Surfaces()
        {
            if (stage == Stage.Table) { if (roomO > .5f) { yield return board; yield return strip; } }
            else { if (wrist.Visible) yield return wrist; if (!rig.xr && hud.Visible) yield return hud; }
            foreach (var p in pins) if (p.label.Visible) yield return p.label;
        }
        bool IsLabel(MLPanel s) => pins.Any(p => p.label == s);

        MLPanel MakeMarkLabel(int n)
        {
            var p = new MLPanel("Mark " + n, 96, 96, .05f, null, true);
            p.Box(14, 14, 68, 68, 12, new Color(8 / 255f, 10 / 255f, 14 / 255f, .78f), Amber);
            p.Text(n.ToString(), 48, 60, 36, Amber, true, 0);
            return p;
        }

        // Positions that depend on the head or on the moving terrain.
        void UpdateSurfaces()
        {
            var head = rig.Head.position; bool table = stage == Stage.Table;
            board.Visible = strip.Visible = roomO > .02f;
            hud.Visible = !table;
            if (rig.xr)
            {
                // Wrist panel: floats just above the left hand and shows while you look at it.
                var l = rig.left; bool looking = l.active && Vector3.Distance(l.origin, head) < .75f && Vector3.Angle(rig.Head.forward, l.origin - head) < 34;
                wrist.Visible = !table && looking;
                if (wrist.Visible) { var at = l.origin + Vector3.up * .16f; wrist.go.transform.SetPositionAndRotation(at, Quaternion.LookRotation(at - head, Vector3.up)); }
                // Head-up strip: low in view, following the head's turn without being glued to it.
                var yaw = Quaternion.Euler(0, rig.HeadYaw, 0); var want = head + yaw * new Vector3(0, -.38f, .93f);
                var ht = hud.go.transform; ht.position = Vector3.Lerp(ht.position, want, 1 - Mathf.Exp(-6 * Time.deltaTime)); ht.rotation = Quaternion.LookRotation(ht.position - head, Vector3.up);
            }
            else wrist.Visible = !table && wristUp;

            foreach (var p in pins)
            {
                var at = world.TransformPoint(p.local); float d = Mathf.Max(.05f, Vector3.Distance(at, head));
                bool hide = !table && p.st.id == here && Vector2.Distance(new Vector2(view.ax, view.az), new Vector2(p.st.x, p.st.z)) < 6;
                p.label.Visible = !hide; if (hide) continue;
                float width = .34f * d * p.k, s = width / 640f; var rot = Quaternion.LookRotation(at - head, Vector3.up);
                p.label.SetWorldWidth(width); p.label.go.transform.SetPositionAndRotation(at + rot * Vector3.right * (274 * s) * (p.left ? -1 : 1), rot);
            }
            foreach (var m in marks)
            {
                var at = world.TransformPoint(m.local); float d = Mathf.Max(.05f, Vector3.Distance(at, head));
                m.label.SetWorldWidth((table ? .03f : .058f) * d); m.label.go.transform.SetPositionAndRotation(at, Quaternion.LookRotation(at - head, Vector3.up));
            }
            if (hudHeading != null) hudHeading.text = $"HDG {MLData.Deg3(HeadingNow)}°";
        }

        void Refresh()
        {
            bool table = stage == Stage.Table; var T = Task; int selLeg = M.tasks[selTask].leg;
            foreach (var p in pins)
            {
                var s = p.st; bool isNext = !Finished && T.station == s.id && s.id != here; int mk = marks.Count(m => m.station == s.id);
                string sub = "";
                if (table) sub = string.Join(" · ", new[] { s.id == here ? "you are here" : "", mk > 0 ? mk + " marked" : "" }.Where(x => x.Length > 0));
                else if (s.id != here) sub = $"{BearingFromHere(s.id).d:0} m" + (stage == Stage.Field && St(here).hidden.Contains(s.id) ? " · behind terrain" : "");
                bool full = table ? s.id == sel : isNext || (Finished && s.id != here);
                var col = table && s.id == sel ? Cyan : isNext && !table ? Amber : Color.white;
                p.k = table && !full ? .72f : 1;
                DrawPin(p, full ? s.id + " " + s.name : s.id, full ? sub : sub.Split(new[] { " · " }, System.StringSplitOptions.None)[0], col);
            }
            foreach (var o in legs)
            {
                bool next = table ? o.index == selLeg : !Finished && ((o.leg.from == here && o.leg.to == T.station) || (o.leg.to == here && o.leg.from == T.station));
                o.ribbonGo.SetActive(!table); o.wideGo.SetActive(table);
                var c = MLPanel.Cyan; c.a = next ? .42f : .14f; o.ribbon.SetColor("_Color", c);
                var wc = next ? MLPanel.Cyan : Color.white; wc.a = next ? .95f : .4f; o.wide.SetColor("_Color", wc);
            }
            if (table) { DrawBoard(); DrawStrip(); } else { DrawWrist(); DrawHud(); }
        }

        void DrawPin(Pin p, string text, string sub, Color col)
        {
            var l = p.label; l.Clear();
            float w = Mathf.Min(640, Mathf.Max(l.Measure(text, 38, true), sub.Length > 0 ? l.Measure(sub, 27) : 0) + 96), x0 = p.left ? 640 - w : 0;
            l.Box(x0, 14, w, 100, 20, new Color(8 / 255f, 10 / 255f, 14 / 255f, .74f));
            l.Box((p.left ? 594 : 46) - 20, 44, 40, 40, 20, new Color(0, 0, 0, 0), col);
            float tx = p.left ? 560 : 80; int al = p.left ? 1 : -1;
            l.Text(text, tx, sub.Length > 0 ? 58 : 77, 38, col, true, al);
            if (sub.Length > 0) l.Text(sub, tx, 97, 27, Soft, false, al);
            string id = p.st.id;
            l.Hit("pin:" + id, x0, 14, w, 100, () => { if (stage == Stage.Table) SelectStation(id); else if (stage == Stage.Field && id != here) Travel(id); else if (stage != Stage.Field) ToField(id); });
        }

        // PLAN: left, what the Marswalk is for and why the selected station serves it; right, the NASA evidence.
        void DrawBoard()
        {
            var b = board; b.Clear(); var st = St(sel); const float X1 = 48, W1 = 980, X2 = 1090, W2 = 1062;
            b.Box(1.5f, 1.5f, b.w - 3, b.h - 3, 36, Back, Line);
            b.Text("MISSION LENS", X1, 62, 24, Gray, true); b.Text("PLAN", X1 + 196, 62, 24, Cyan, true); b.Text("HYPOTHETICAL MARSWALK", X1 + W1, 62, 24, Amber, true, 1);
            b.Text(M.title, X1, 120, 46, Ink, true); b.Text($"{M.subtitle} · {M.routeTotalM:0} m on foot · {MLData.Hm(M.plannedTotalMin)} planned", X1, 160, 27, Gray);
            b.Text("SCIENCE OBJECTIVE", X1, 216, 24, Gray, true); float y = b.Wrap(M.objective, X1, 258, W1, 41, 32, Ink);
            y = b.Wrap("NASA record · " + M.objectiveBasis, X1, y - 2, W1, 32, 25, Gray);
            b.Text($"WHY STATION {st.id} · {st.name.ToUpper()}", X1, y + 32, 24, Cyan, true); y = b.Wrap(st.why, X1, y + 72, W1, 41, 32, Ink);
            int mk = marks.Count(m => m.station == st.id);
            y = b.Wrap($"Elevation {st.elev:0.0} m · ground slope {st.slopeDeg}° · in view from here: {(st.sees.Length > 0 ? string.Join(", ", st.sees) : "none")}{(st.hidden.Length > 0 ? " · hidden: " + string.Join(", ", st.hidden) : "")} (our calculation from the terrain model)", X1, y, W1, 32, 25, Gray);
            if (mk > 0) b.Text($"Recorded in FIELD · simulated: {mk} candidate{(mk > 1 ? "s" : "")}, marked on the map", X1, y + 2, 25, Amber, true);
            b.Wrap(M.scenarioNote, X1, 816, W1, 26, 21, Dim);

            const float ih = 270; b.Box(X2, 48, W2, ih, 16, new Color(.1f, .12f, .14f, 1));
            if (viewTex.TryGetValue(sel, out var tex) && tex != null) { float vh = (tex.width * ih / W2) / tex.height; b.Picture(tex, X2, 48, W2, ih, new Rect(0, (1 - vh) / 2, 1, vh)); }
            b.Tag("NASA OBSERVATION", X2 + 14, 60, Ink); b.Text($"Mastcam-Z panorama, {st.panoSols}, looking {MLData.Deg3(st.viewAz)}° · {st.panoCredit}", X2, 48 + ih + 30, 23, Gray);
            y = 48 + ih + 78; b.Text(st.hasSample ? "NASA SAMPLING RECORD" : "NASA ROVER RECORD", X2, y, 24, Gray, true);
            y = b.Wrap(st.hasSample ? $"Rock cores {st.sampleName}: {st.sampleRock.ToLower()}, {st.sampleLocation.ToLower()}. Taken {st.sampleDate} (sols {st.sampleSol})." : st.note, X2, y + 38, W2, 37, 29, Ink);
            int p = st.crismRank; b.Text($"SPECTRAL CONTEXT · MRO CRISM · {M.crismMPerPx:0} M PER PIXEL", X2, y + 24, 24, Gray, true);
            b.Wrap($"{M.crismBands[0]} band here: {(p >= 50 ? $"stronger than in {p}%" : $"weaker than in {100 - p}%")} of this area (our reading). NASA label: Mg/Fe-OH bearing minerals may appear red; spectra are needed for positive identification.", X2, y + 60, W2, 35, 27, Ink);
            float x = X2; const float by = 724; string s = sel;
            b.Button("Slope", x, by, 150, 64, () => SetLayer("slope"), layer == "slope", px: 26); x += 166;
            b.Button("Spectral", x, by, 180, 64, () => SetLayer("crism"), layer == "crism", px: 26); x += 196;
            b.Button("Height ×2", x, by, 190, 64, ToggleHeight, view.ex > 1, px: 26); x += 206;
            b.Button("Preview 1 : 25", x, by, 230, 64, () => EnterTerrain(s, SGiant), px: 26); x += 246;
            b.Button("Stand at " + s, x, by, 240, 64, () => ToField(s), primary: true, px: 26);
            const float ly = 826;
            if (layer == "slope")
            {
                float lx = X2;
                foreach (var (c, t) in new[] { (new Color(1, .84f, .25f), "10–15°"), (new Color(1, .55f, .16f), "15–20°"), (new Color(.92f, .2f, .2f), "over 20°") })
                { b.Box(lx, ly - 19, 22, 22, 4, c); b.Text(t, lx + 30, ly, 23, Ink); lx += 46 + b.Measure(t, 23); }
                b.Text("· slope from the HiRISE terrain model; class breaks are ours", lx, ly, 23, Gray);
            }
            else if (layer == "crism") b.Text($"{M.crismObservation} PHY: R {M.crismBands[0]}, G {M.crismBands[1]}, B {M.crismBands[2]} · moved {M.crismShiftM:0} m by us to fit HiRISE · brightness ×{M.crismGain}", X2, ly, 23, Gray);
            else b.Text($"On the table: MRO HiRISE image over the HiRISE terrain model, {scaleText}", X2, ly, 23, Gray);
        }

        // PLAN: the task timeline. Each segment selects its task, station and leg.
        void DrawStrip()
        {
            var s = strip; s.Clear(); var T = M.tasks[selTask]; const float X0 = 30; float W = s.w - 60, k = W / M.plannedTotalMin; var leg = T.leg >= 0 ? M.legs[T.leg] : null;
            s.Box(1.5f, 1.5f, s.w - 3, s.h - 3, 28, Back, Line);
            s.Text($"{T.station} {St(T.station).name} · {T.phase}", X0, 52, 30, Ink, true);
            s.Text($"{T.plannedMin} min · {MLData.Hm(T.startMin)}–{MLData.Hm(T.endMin)}{(T.index == taskIndex ? " · current task" : T.index < taskIndex ? " · done" : "")}", X0 + W, 52, 25, T.index == taskIndex ? Amber : Gray, false, 1);
            s.Text(leg != null ? $"Walk {leg.distM:0} m from {leg.from}: climb {leg.ascentM:0} m, steepest {leg.slopeMaxDeg:0}°, {Mathf.RoundToInt(leg.pctOnTrack * 100)}% on ground Perseverance drove. About {Mathf.RoundToInt(leg.estWalkMin)} min at {M.walkSpeed} m/s (our assumption)." : string.Join("  ·  ", T.checks), X0, 90, 23, Gray);
            foreach (var t in M.tasks)
            {
                float x = X0 + t.startMin * k, w = t.plannedMin * k - 4; bool on = t.index == selTask; int i = t.index;
                s.Box(x, 114, w, 62, 8, on ? Cyan : t.station == sel ? new Color(95 / 255f, 212 / 255f, 1, .32f) : new Color(1, 1, 1, t.index < taskIndex ? .27f : .12f), t.index == taskIndex ? Amber : (Color?)null);
                if (w > 44) s.Text((t.leg >= 0 ? "→ " : "") + t.station, x + w / 2, 154, 24, on ? OnCyan : Ink, true, 0);
                s.Hit("task:" + i, x, 106, w + 4, 78, () => SelectTask(i));
            }
            for (int m = 0; m < M.plannedTotalMin; m += 60) s.Text(MLData.Hm(m), X0 + m * k, 212, 20, Gray);
            s.Text(MLData.Hm(M.plannedTotalMin), X0 + W, 212, 20, Gray, false, 1);
            s.Text("Task structure and planned durations follow the BASALT-2 planned EVA timeline (Beaton et al. 2019, Table 2) · amber outline: current task", X0, 248, 20, Dim);
            if (rig.xr) s.Button("Bring table here", s.w - 250, 222, 220, 40, PlaceTable, px: 20);
        }

        void DrawWrist()
        {
            var s = wrist; s.Clear(); const float PW = 600, PH = 780; var T = Task; float y = 54, yb = PH - 196;
            s.Box(1.5f, 1.5f, PW - 3, PH - 3, 34, new Color(12 / 255f, 15 / 255f, 20 / 255f, .9f), new Color(1, 1, 1, .22f));
            s.Text("MISSION LENS", 34, y, 20, Gray, true); s.Text(stage == Stage.Giant ? "PLAN · ROUTE PREVIEW" : "FIELD", PW - 34, y, 20, Cyan, true, 1); y += 44;
            s.Text(M.title, 34, y, 34, Ink, true); y += 36;
            s.Text($"T+ {MLData.Hm(Pet)} of {MLData.Hm(M.plannedTotalMin)} planned · task {Mathf.Min(taskIndex + 1, M.tasks.Length)}/{M.tasks.Length}", 34, y, 22, Soft); y += 18;
            s.Box(34, y, PW - 68, 6, 3, new Color(1, 1, 1, .12f)); if (Pet > 0) s.Box(34, y, (PW - 68) * Pet / M.plannedTotalMin, 6, 3, Cyan); y += 44;
            if (stage == Stage.Giant || stage == Stage.Walk)
            {
                var n = Nearest(); string nid = n.id;
                s.Text(stage == Stage.Giant ? "Inside the terrain model, 1 : 25" : "Inside the terrain model, true scale", 34, y, 26, Ink, true); y += 40;
                y = s.Wrap("Orbital data only: shape from the HiRISE terrain model, colour from the HiRISE image. No rover imagery here.", 34, y, PW - 68, 28, 21, Soft) + 8;
                s.Wrap($"Planned route is the blue path. Nearest station: {n.id} {St(n.id).name}, {n.d:0} m.", 34, y, PW - 68, 28, 21, Ink);
                s.Button("1 : 25", 34, yb, 258, 62, () => Rescale(SGiant), stage == Stage.Giant); s.Button("True scale", 308, yb, 258, 62, () => Rescale(1), stage == Stage.Walk);
                s.Button("Surface image at " + nid, 34, yb + 76, 340, 62, () => ToField(nid)); s.Button("Table", 390, yb + 76, 176, 62, ToTable);
            }
            else
            {
                var st = St(here); int mine = marks.Count(m => m.station == here); bool rec = !Finished && T.station == here && T.recordCheck >= 0;
                s.Text($"{st.id}  {st.name}", 34, y, 30, Ink, true); y += 32;
                y = s.Wrap($"Mastcam-Z panorama, {st.panoSols}. {st.panoCredit}. Direction assigned by us.", 34, y, PW - 68, 26, 20, Gray);
                if (st.hasSample) y = s.Wrap($"NASA record: rock cores {st.sampleName} were taken within about {Mathf.CeilToInt(st.sampleDistM)} m of this stop.", 34, y, PW - 68, 26, 20, Soft);
                y += 22;
                if (Finished) s.Text("All tasks complete", 34, y, 26, Cyan, true);
                else if (T.station == here && T.leg < 0)
                {
                    s.Text($"TASK · {T.plannedMin} MIN", 34, y, 21, Gray, true); y += 36; s.Text(T.phase, 34, y, 28, Ink, true); y += 22;
                    for (int i = 0; i < T.checks.Length; i++)
                    {
                        bool on = done.Contains(i); float y0 = y; int ci = i;
                        s.Box(36, y + 6, 30, 30, 7, on ? Cyan : new Color(0, 0, 0, 0), on ? Cyan : Hex("8b9097"));
                        y = s.Wrap(T.checks[i], 84, y + 30, PW - 124, 28, 22, on ? Hex("8b9097") : Ink) + 6;
                        s.Hit("check:" + i, 30, y0, PW - 60, y - y0, () => ToggleCheck(ci));
                    }
                    if (rec) s.Wrap(mine > 0 ? $"{mine} candidate{(mine > 1 ? "s" : "")} marked here. Simulated record; position from the terrain model, approximate." : (rig.xr ? "Point at the scene and pinch to mark a candidate." : "Lower this panel (Space), then point at the scene and click to mark a candidate."), 34, y + 26, PW - 68, 26, 20, Amber);
                }
                else
                {
                    var b = BearingFromHere(T.station); bool seen = st.sees.Contains(T.station);
                    s.Text("NEXT", 34, y, 21, Gray, true); y += 36; s.Text($"{T.phase} · {St(T.station).name}", 34, y, 28, Ink, true); y += 36;
                    s.Text($"{b.d:0} m straight · bearing {MLData.Deg3(b.az)}° · {(seen ? "in view" : "behind terrain")}", 34, y, 23, seen ? Ink : Amber);
                }
                if (here == M.medaStation) s.Text(M.medaLine, 34, PH - 222, 20, Gray);
                bool go = !Finished && T.station != here; float cw = rec && mine > 0 ? 350 : PW - 68; string to = T.station;
                if (go) s.Button($"Walk to {to} {St(to).name}", 34, yb, PW - 68, 62, () => Travel(to), true);
                else s.Button(Finished ? "Mission complete" : "Complete task", 34, yb, cw, 62, CompleteTask, !Finished && done.Count == T.checks.Length, Finished);
                if (!go && rec && mine > 0) s.Button("Undo mark", 34 + cw + 14, yb, PW - 68 - cw - 14, 62, UndoMark);
                string h = here; s.Button("Table", 34, yb + 76, 170, 62, ToTable); s.Button("Step out to terrain", 220, yb + 76, 346, 62, () => EnterTerrain(h, 1));
            }
            s.Text("Hypothetical Marswalk · data: NASA", 34, PH - 22, 18, Dim);
        }

        void DrawHud()
        {
            if (hud == null) return;
            var s = hud; s.Clear(); var T = Task; float W = s.w; bool fin = Finished; string top, main, sub; var col = Ink; string target = fin ? null : T.station;
            if (stage == Stage.Field)
            {
                var st = St(here); top = $"{st.id} · {st.name.ToUpper()}";
                if (fin) { main = "All tasks complete"; sub = "Go back to the table to review the Marswalk"; }
                else if (T.station == here && T.leg < 0) { target = null; main = T.phase; sub = $"{done.Count}/{T.checks.Length} checks · {T.plannedMin} min planned{(T.recordCheck >= 0 ? " · point and pinch to mark a candidate" : "")}"; }
                else { var b = BearingFromHere(T.station); main = $"Next: {T.station} {St(T.station).name}"; col = Amber; sub = $"{T.phase} · {b.d:0} m · bearing {MLData.Deg3(b.az)}° · {(st.sees.Contains(T.station) ? "in view" : "behind terrain")}"; }
            }
            else
            {
                var n = Nearest(); top = stage == Stage.Giant ? "ROUTE PREVIEW · 1 : 25" : "TERRAIN MODEL · TRUE SCALE"; if (travelling != null) target = travelling;
                main = travelling != null ? $"Walking to {travelling} {St(travelling).name}" : "Orbital data only"; sub = $"Nearest station: {n.id} {St(n.id).name}, {n.d:0} m";
            }
            if (flashMsg.Length > 0) sub = flashMsg;
            s.Box(0, 0, W, s.h, 26, new Color(10 / 255f, 12 / 255f, 16 / 255f, .8f));
            s.Text(top, 40, 48, 23, Gray, true); s.Text($"T+ {MLData.Hm(Pet)} / {MLData.Hm(M.plannedTotalMin)}", W / 2, 48, 23, Gray, true, 0);
            hudHeading = s.Text($"HDG {MLData.Deg3(HeadingNow)}°", W - 40, 48, 23, Gray, true, 1);
            s.Text(main, 40, 104, 38, col, true); s.Text(sub, 40, 146, 24, flashMsg.Length > 0 ? Amber : Soft);
            if (target != null)
            {   // which way to turn when the next station is outside the view
                float rel = Mathf.DeltaAngle(HeadingNow, BearingFromHere(target).az), half = rig.xr ? 40 : Mathf.Atan(Mathf.Tan(rig.cam.fieldOfView * Mathf.Deg2Rad / 2) * rig.cam.aspect) * Mathf.Rad2Deg;
                if (Mathf.Abs(rel) > half - 6) s.Text(rel < 0 ? $"« {target}" : $"{target} »", W - 40, 104, 34, Amber, true, 1);
            }
            if (!rig.xr) { s.Text(wristUp ? "Space: lower wrist panel" : "Space: wrist panel", W - 40, 146, 20, Dim, false, 1); s.Hit("hud", 0, 0, W, s.h, ToggleWrist); }
            else s.Text("Look at your left hand for the panel", W - 40, 146, 20, Dim, false, 1);
        }
    }
}
