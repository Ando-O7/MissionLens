using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static MissionLens.MLPanel;
using static MissionLens.MLText;

namespace MissionLens
{
    // Things on the desk that are worked by taking hold of them rather than by pressing a panel. They are
    // experiments in how the briefing could be handled; each does something a button also does, so the app
    // works without them, and "Tools" on the legend sign hides them (and, shown again, puts them back in place).
    //
    //   The lens  - a glass ball. Hold it over the model and the ground under it shows a data layer instead of
    //               the image; twist the hand, or press its tag, to change the layer. Let go over the model and
    //               it stays there.
    //   The timer - a dial with the selected task written under it. Pick it up and it comes with the hand, so it
    //               can be held in view beside the model or the panels; turning the hand that holds it moves
    //               through the planned timeline, and the table follows. Let go and it stays where it was left.
    //               From a distance it can also be turned where it lies, by pointing at its rim and dragging round.
    public partial class MLApp
    {
        public bool tools = true;
        /// <summary>The lens is switched off: tried on the headset, it was hard to use and not missed. Its code stays for another try.</summary>
        public bool lensEnabled = false;

        // ---------- the lens ----------
        const float LensR = .1f, OrbR = .05f, LensNear = .13f;      // footprint on the table, the ball itself, and how near a hand takes it (metres)
        // Its place is on the model's near right corner, where it already shows what it does.
        static readonly Vector3 LensHome = new Vector3(.32f, .12f, -.29f);
        Vector3 lensPos = LensHome; string lensLayer = "slope"; float lensOn, lensTwist, lensToggledAt = -1; int lensShown = -999;
        MLRig.Pointer lensBy; bool lensNear, lensReach; Quaternion lensGrip;
        Transform orbT, stemT; Material orbMat, stemMat; MLPanel lensTag;

        // ---------- the timer ----------
        const float DialSweep = 300, DialUp = 140, TimerGain = 2f;   // degrees of the dial the timeline takes up; the dial's centre above the panel's (pixels); dial degrees per degree the hand turns
        const float TimerW = .16f, TimerSide = 150, TimerOff = 260;  // its width (metres: small enough to hold and to catch again); how near a hand picks it up, in the panel's pixels: 4 cm beyond its edges, 7 cm off its face
        static readonly Vector3 TimerHome = new Vector3(.355f, .07f, -.56f), TimerHomeEuler = new Vector3(54, 18, 0);
        MLPanel dial; Transform needleT;
        MLRig.Pointer dialBy; bool timerCarried, timerAway;          // dialBy: the pointer that has it. carried: picked up (else turned where it lies). away: left somewhere other than its place
        Vector3 timerPos = TimerHome, timerHandAt; Quaternion timerGrip; float timerGrabDist, timerSince, timerSpeed;
        float needleDeg, timerReach; bool timerInReach; int dialTask = -1;

        void BuildTools()
        {
            orbMat = new Material(Sh("MLHand"));
            orbT = Add("Lens", MLMesh.Ball(OrbR), orbMat, tableFrame).transform;
            lensTag = new MLPanel("Lens tag", 480, 120, .15f, null, true); lensTag.Snap(false); panels.Add(lensTag);
            stemMat = ColorMat(Amber, 3090); stemT = Add("Lens stem", MLMesh.Quad(false), stemMat, null).transform;   // ties the ball to its circle on the ground

            dial = new MLPanel("Timer", 600, 880, TimerW, tableFrame); dial.Snap(false); panels.Add(dial); dial.Order(10);
            dial.go.transform.localPosition = TimerHome; dial.go.transform.localRotation = Quaternion.Euler(TimerHomeEuler);
            var pivot = new GameObject("Timer needle").transform; pivot.SetParent(tableFrame, false); needleT = pivot;
            var barR = Add("Bar", MLMesh.Quad(false), ColorMat(Amber, 3100), pivot); barR.sortingOrder = 11;   // over the dial's face, which has a draw order of its own
            var bar = barR.transform; bar.localPosition = new Vector3(0, .0703f, -.0015f); bar.localScale = new Vector3(.0045f, .034f, 1);   // from outside the time in the middle to the arcs
        }

        /// <summary>Puts the lens and the timer back where they start.</summary>
        void ResetTools()
        {
            foreach (var p in new[] { rig.right, rig.left, rig.mouse }) p.tool = null;
            lensBy = null; dialBy = null; timerCarried = false; timerAway = false; lensPos = LensHome; timerPos = TimerHome;
        }

        float TimeToDeg(float min) => -DialSweep / 2 + DialSweep * min / M.plannedTotalMin;
        int TaskAtDeg(float deg)
        {
            float min = Mathf.Clamp01((deg + DialSweep / 2) / DialSweep) * M.plannedTotalMin;
            foreach (var t in M.tasks) if (min < t.endMin) return t.index;
            return M.tasks.Length - 1;
        }

        // The dial is built when the Marswalk moves on (or the language changes); turning it only recolours the arcs
        // and rewrites the texts, so that it stays light while it is being turned.
        readonly List<Image>[] dialArcs = new List<Image>[32];
        readonly TextMeshProUGUI[] dialLetters = new TextMeshProUGUI[32]; TextMeshProUGUI dialTime, dialWhat, cardName, cardPhase, cardWhen, cardA, cardB; int dialBuiltFor = -1;
        void DrawDial()
        {
            var s = dial; s.Clear(); const float C = 300;
            var back = new Color(8 / 255f, 10 / 255f, 14 / 255f, .93f);   // nearly solid: it is held up against the room or the model
            s.Box(6, 6, 588, 588, 294, back); s.Circle(C, C, 292, 3, Line);
            foreach (var t in M.tasks)
            {   // each task is an arc of short strokes, as long as its planned time
                float a0 = TimeToDeg(t.startMin) + 1.2f, a1 = TimeToDeg(t.endMin) - 1.2f; var arc = dialArcs[t.index] = new List<Image>();
                int n = Mathf.Max(1, Mathf.CeilToInt((a1 - a0) / 2.5f));
                for (int i = 0; i < n; i++)
                {
                    float b0 = Mathf.Lerp(a0, a1, i / (float)n) * Mathf.Deg2Rad, b1 = Mathf.Lerp(a0, a1, (i + 1f) / n) * Mathf.Deg2Rad;
                    arc.Add(s.Stroke(C + Mathf.Sin(b0) * 250, C - Mathf.Cos(b0) * 250, C + Mathf.Sin(b1) * 250, C - Mathf.Cos(b1) * 250, 28, Color.white));
                }
                dialLetters[t.index] = null;
                if (t.plannedMin >= 15)
                {
                    float m = (TimeToDeg(t.startMin) + TimeToDeg(t.endMin)) / 2 * Mathf.Deg2Rad;
                    dialLetters[t.index] = s.Text((t.leg >= 0 ? "→" : "") + t.station, C + Mathf.Sin(m) * 198, C - Mathf.Cos(m) * 198 + 9, 24, Soft, true, 0);
                }
            }
            s.Text(L("MISSION TIMER · PLANNED", "ミッションタイマー · 計画"), C, 222, 17, Gray, true, 0);
            dialTime = s.Text("", C, 300, 66, Ink, true, 0); dialWhat = s.Text("", C, 346, 26, Cyan, true, 0);
            s.Text(rig.xr ? L("pick up · turn your hand", "手に取って · 手を回す") : L("rim: turn · middle: carry", "縁: 回す · 中央: 持ち運ぶ"), C, 540, 19, Dim, false, 0);
            // the selected task, written under the dial so that it travels with it
            s.Box(6, 606, 588, 268, 26, back, Line);
            cardName = s.Text("", 32, 656, 32, Ink, true); cardPhase = s.Text("", 32, 698, 27, Cyan, true); cardWhen = s.Text("", 32, 736, 23, Gray);
            cardA = s.Text("", 32, 794, 23, Soft); cardB = s.Text("", 32, 832, 23, Soft);
            s.Hit("timer", 0, 0, 600, 880, () => { }, 40, true);   // so the pointer rests on it instead of taking hold of the map behind
            dialBuiltFor = Finished ? -2 : taskIndex;
        }
        void DialShowSelection()
        {
            if (dialBuiltFor != (Finished ? -2 : taskIndex) || dialTime == null) DrawDial();
            var Tk = M.tasks[selTask];
            foreach (var t in M.tasks)
            {
                bool on = t.index == selTask; var col = on ? Cyan : t.index == taskIndex && !Finished ? Amber : new Color(1, 1, 1, t.index < taskIndex ? .5f : .22f);
                foreach (var im in dialArcs[t.index]) im.color = col;
                if (dialLetters[t.index] != null) dialLetters[t.index].color = on ? Cyan : Soft;
            }
            dialTime.text = "T+ " + MLData.Hm(Tk.startMin); dialWhat.text = $"{Tk.station} · {ShortPhase(Tk)}";
            Fit(cardName, $"{Tk.station} · {St(Tk.station).name}", 32); Fit(cardPhase, T(Tk.phase), 27);
            Fit(cardWhen, L($"{Tk.plannedMin} min", $"{Tk.plannedMin} 分") + $" · T+ {MLData.Hm(Tk.startMin)} – {MLData.Hm(Tk.endMin)}"
                + (Finished || Tk.index < taskIndex ? L(" · done", " · 完了") : Tk.index == taskIndex ? L(" · current task", " · 現在の作業") : ""), 23);
            if (Tk.leg >= 0)
            {
                var leg = M.legs[Tk.leg];
                Fit(cardA, L($"Walk {leg.distM:0} m from {leg.from} · climb {leg.ascentM:0} m · steepest {leg.slopeMaxDeg:0}°", $"{leg.from} から徒歩 {leg.distM:0} m · 登り {leg.ascentM:0} m · 最大傾斜 {leg.slopeMaxDeg:0}°"), 23);
                Fit(cardB, L($"about {Mathf.RoundToInt(leg.estWalkMin)} min at {M.walkSpeed} m/s (our assumption)", $"約 {Mathf.RoundToInt(leg.estWalkMin)} 分({M.walkSpeed} m/s、こちらの仮定)"), 23);
            }
            else { Fit(cardA, Tk.checks.Length > 0 ? "· " + T(Tk.checks[0]) : "", 23); Fit(cardB, Tk.checks.Length > 1 ? "· " + T(Tk.checks[1]) : "", 23); }
        }
        // One line on the card: written smaller if it would not fit.
        void Fit(TextMeshProUGUI t, string s, float px)
        {
            float w = s.Length > 0 ? dial.Measure(s, px, (t.fontStyle & FontStyles.Bold) != 0) : 0;
            t.fontSize = w > 536 ? Mathf.Max(15, px * 536 / w) : px; t.text = s;
        }

        // Where a pointer is on the timer. byHand: the hand itself is at it. Otherwise where its ray meets the panel:
        // degrees clockwise from the top of the dial, and how far out from the dial's centre (pixels).
        bool TimerAt(MLRig.Pointer p, out float deg, out float r, out bool byHand)
        {
            deg = 0; r = 0; var t = dial.go.transform; var l = t.InverseTransformPoint(p.origin);   // the panel's pixels from its centre, y up
            byHand = rig.xr && Mathf.Abs(l.x) < 300 + TimerSide && Mathf.Abs(l.y) < 440 + TimerSide && Mathf.Abs(l.z) < TimerOff;
            if (byHand) return true;
            var plane = new Plane(t.forward, t.position); var ray = p.Ray;
            if (!plane.Raycast(ray, out float d) || d <= 0) return false;
            l = t.InverseTransformPoint(ray.GetPoint(d)); if (Mathf.Abs(l.x) > 300 || Mathf.Abs(l.y) > 440) return false;
            l.y -= DialUp; r = new Vector2(l.x, l.y).magnitude; deg = Mathf.Atan2(l.x, l.y) * Mathf.Rad2Deg; return true;
        }
        bool LensAt(MLRig.Pointer p, out bool near)
        {
            var orb = orbT.position; near = rig.xr && Vector3.Distance(p.origin, orb) < LensNear; if (near) return true;
            if (p.overSurface) return false;
            var ray = p.Ray; float along = Vector3.Dot(orb - ray.origin, ray.direction); return along > 0 && Vector3.Distance(ray.GetPoint(along), orb) < .09f;
        }
        bool ToolsLive => stage == Stage.Table && introDone && tools && !busy && dial.alpha > .6f;
        /// <summary>The hand itself is at the lens or the timer (or holds one): a pinch there picks the thing up.</summary>
        bool HandAtTool(MLRig.Pointer p)
        {
            if (!rig.xr || !ToolsLive) return false;
            if (p.tool != null) return true;
            return (lensEnabled && Vector3.Distance(p.origin, orbT.position) < LensNear) || (TimerAt(p, out _, out _, out bool byHand) && byHand);
        }

        // ---------- per frame ----------
        void UpdateTools(float dt)
        {
            bool table = stage == Stage.Table && introDone, live = ToolsLive; var head = rig.Head.position;
            bool show = table && tools && roomO > .6f, showLens = show && lensEnabled;
            dial.Show = show; lensTag.Show = showLens;
            if (needleT.gameObject.activeSelf != dial.alpha > .5f) needleT.gameObject.SetActive(dial.alpha > .5f);
            if (orbT.gameObject.activeSelf != showLens) orbT.gameObject.SetActive(showLens);

            var ptrs = rig.xr ? new[] { rig.right, rig.left } : new[] { rig.mouse }; lensReach = false;
            if (!live) { lensBy = null; dialBy = null; timerCarried = false; foreach (var p in ptrs) p.tool = null; }
            else
            {
                // let go
                if (lensBy != null && (!lensBy.active || !lensBy.held)) { lensBy.tool = null; lensBy = null; sfx.Play("toggle", .5f); Log("lens released" + (LensOverModel ? " over the model" : " (returns)")); }
                if (dialBy != null && (!dialBy.active || !dialBy.held))
                {
                    if (timerCarried) { timerAway = Vector3.Distance(timerPos, TimerHome) > .14f; Log("timer put down " + (timerAway ? "away from its place" : "in its place") + ", task " + selTask); }
                    dialBy.tool = null; dialBy = null; timerCarried = false; sfx.Play("toggle", .5f);
                }
                // take hold
                foreach (var p in ptrs)
                {
                    if (!p.active || p.tool != null) continue;
                    bool lensHere = lensEnabled && lensBy == null && LensAt(p, out bool near0); if (lensHere) lensReach = true;
                    if (!p.grabDown) continue;
                    if (dialBy == null && TimerAt(p, out _, out float r0, out bool byHand))
                    {   // by the hand, by its middle or by its card: picked up. By its rim from a distance: turned where it lies.
                        dialBy = p; p.tool = dial; timerCarried = byHand || r0 < 150 || r0 > 320; timerGrip = p.grip; timerHandAt = p.gripAt; timerSince = Time.time; timerSpeed = 0;
                        timerGrabDist = Vector3.Distance(p.origin, dial.go.transform.position);
                        sfx.Play("hover", .8f); rig.Haptic(p, .4f, .03f); Log(timerCarried ? "timer picked up " + (byHand ? "by hand" : "by ray") : "timer turned where it lies"); continue;
                    }
                    if (lensHere && LensAt(p, out bool near)) { lensBy = p; lensNear = near; p.tool = orbT; lensGrip = p.grip; lensTwist = 0; sfx.Play("hover", .8f); rig.Haptic(p, .4f, .03f); Log("lens taken " + (near ? "by hand" : "by ray")); }
                }
            }

            // ----- the lens follows the hand that holds it; released away from the model it goes back to its place
            if (lensBy != null)
            {
                Vector3 want = lensPos; var o = tableFrame.InverseTransformPoint(lensBy.origin);
                if (lensNear) want = new Vector3(o.x, Mathf.Max(.07f, o.y), o.z);
                else
                {
                    var d = tableFrame.InverseTransformDirection(lensBy.rotation * Vector3.forward);
                    if (d.y < -.03f) { float k = (.12f - o.y) / d.y; if (k > 0) { var h = o + d * k; want = new Vector3(Mathf.Clamp(h.x, -.8f, .8f), .12f, Mathf.Clamp(h.z, -.7f, .7f)); } }
                }
                lensPos = Vector3.Lerp(lensPos, want, 1 - Mathf.Exp(-22 * dt));
                // A quick twist of the hand (or the wheel, on a desktop) changes what the lens shows. The twist is
                // forgotten within a second, so the slow turning of a hand that is only carrying it does not count.
                lensTwist = lensTwist * Mathf.Exp(-dt / .8f) + Twist(lensGrip, lensBy.grip, lensBy.gripAxis); lensGrip = lensBy.grip;
                bool twist = Mathf.Abs(lensTwist) > 38, wheel = !rig.xr && rig.wheel != 0;
                if ((twist || wheel) && Time.time - lensToggledAt > .3f) { lensTwist = 0; LensNextLayer(); rig.Haptic(lensBy, .7f, .05f); }
            }
            else if (!LensOverModel) lensPos = Vector3.Lerp(lensPos, LensHome, 1 - Mathf.Exp(-6 * dt));
            orbT.localPosition = lensPos;
            lensOn = Mathf.MoveTowards(lensOn, showLens && LensOverModel ? 1 : 0, dt * 5);
            Shader.SetGlobalVector("_MLLens", new Vector4(lensPos.x, lensPos.z, LensR, lensOn));
            terrainMat.SetFloat("_LensSlope", lensLayer == "slope" ? 1 : 0); terrainMat.SetFloat("_LensCrism", lensLayer == "crism" ? 1 : 0);
            var lc = lensLayer == "slope" ? Amber : new Color(1f, .42f, .5f);
            orbMat.SetColor("_Color", lc); orbMat.SetFloat("_Body", lensBy != null ? .34f : lensReach ? .27f : .16f); orbMat.SetFloat("_Rim", .95f);   // brighter when a pinch would take it
            if (showLens)
            {   // a tag by the ball: what it shows, and for slope the value under its centre. Pressing the tag changes the layer too.
                var m = TableToMap(new Vector2(lensPos.x, lensPos.z), tv); int slope = LensOverModel && lensLayer == "slope" ? Mathf.RoundToInt(D.SlopeDeg(m.x, m.y)) : -1;
                int shown = (lensLayer == "slope" ? 0 : 1000) + slope + (LensOverModel ? 0 : 500);
                if (shown != lensShown)
                {
                    lensShown = shown; var s = lensTag; s.Clear();
                    s.Box(1, 1, 478, 118, 22, new Color(8 / 255f, 10 / 255f, 14 / 255f, .86f), new Color(lc.r, lc.g, lc.b, .5f));
                    s.Text(lensLayer == "slope" ? L("LENS · SLOPE", "レンズ · 傾斜") : L("LENS · SPECTRAL", "レンズ · 分光"), 24, 48, 30, lc, true);
                    s.Text("⇄", 456, 48, 30, lc, true, 1);
                    string how = rig.xr ? L("twist or press to change", "ひねる / 押すで切替") : L("wheel or click to change", "ホイール / クリックで切替");
                    s.Text(!LensOverModel ? (rig.xr ? L("pinch and carry over the model", "ピンチして模型の上へ運ぶ") : L("drag over the model", "ドラッグして模型の上へ"))
                         : lensLayer == "slope" ? L($"{slope}° here · ", $"ここは {slope}° · ") + how : "CRISM PHY · " + how, 24, 92, 23, Soft);
                    s.Hit("lens:layer", 0, 0, 480, 120, LensNextLayer, 22);
                }
                var at = orbT.position + Vector3.up * (OrbR + .045f); float d = Mathf.Max(.2f, Vector3.Distance(at, head));
                lensTag.SetWorldWidth(.22f * d); lensTag.go.transform.SetPositionAndRotation(at, Quaternion.LookRotation(at - head, Vector3.up));
            }
            bool stem = showLens && lensOn > .05f; if (stemT.gameObject.activeSelf != stem) stemT.gameObject.SetActive(stem);
            if (stem)
            {
                var m = TableToMap(new Vector2(lensPos.x, lensPos.z), tv); Vector3 foot = world.TransformPoint(D.Local(m.x, m.y)), top = orbT.position - Vector3.up * OrbR; float len = Mathf.Max(0, top.y - foot.y);
                var flat = Vector3.ProjectOnPlane(foot - head, Vector3.up);
                stemT.SetPositionAndRotation(new Vector3(top.x, foot.y + len / 2, top.z), Quaternion.LookRotation(flat.sqrMagnitude > 1e-6f ? flat : Vector3.forward, Vector3.up));
                stemT.localScale = new Vector3(.0025f, len, 1); var sc = lc; sc.a = .7f * lensOn; stemMat.SetColor("_Color", sc);
            }

            // ----- the timer
            // It swells a little, with a soft sound, when a hand comes within reach: a pinch now would pick it up.
            bool reach = false;
            if (live && rig.xr && dialBy == null) foreach (var p in ptrs) if (p.active && p.tool == null && TimerAt(p, out _, out _, out bool at) && at) reach = true;
            if (reach && !timerInReach) sfx.Play("hover", .5f);
            timerInReach = reach; timerReach = Mathf.MoveTowards(timerReach, reach || dialBy != null ? 1 : 0, dt * 8);
            dial.SetWorldWidth(TimerW * (1 + .05f * timerReach));
            var dialT = dial.go.transform; float before = needleDeg;
            if (dialBy != null && timerCarried)
            {
                // Picked up: it rides above the hand, turned to face the eyes, and turning the hand turns the time.
                Vector3 want = rig.xr ? dialBy.gripAt + Vector3.up * .175f : dialBy.Ray.GetPoint(timerGrabDist);
                timerPos = Vector3.Lerp(timerPos, tableFrame.InverseTransformPoint(want), 1 - Mathf.Exp(-18 * dt));
                float turned = Twist(timerGrip, dialBy.grip, dialBy.gripAxis); timerGrip = dialBy.grip;
                // Carrying it somewhere is not turning it: while the hand is travelling its turn counts for less,
                // and the jolt of the pinch itself does not count at all.
                timerSpeed = Mathf.Lerp(timerSpeed, Vector3.Distance(dialBy.gripAt, timerHandAt) / Mathf.Max(dt, 1e-4f), .3f); timerHandAt = dialBy.gripAt;
                turned *= Time.time - timerSince < .2f ? 0 : 1 - Mathf.InverseLerp(.25f, .7f, timerSpeed);
                // clockwise, as the person sees it, is forward in time
                needleDeg = Mathf.Clamp(needleDeg - turned * TimerGain + (rig.xr ? 0 : rig.wheel * 10), -DialSweep / 2, DialSweep / 2);
            }
            else if (dialBy != null) { if (TimerAt(dialBy, out float deg, out _, out bool hand) && !hand) needleDeg = Mathf.Clamp(deg, -DialSweep / 2, DialSweep / 2); }
            else
            {
                var Tk = M.tasks[selTask]; needleDeg = Mathf.LerpAngle(needleDeg, TimeToDeg((Tk.startMin + Tk.endMin) / 2f), 1 - Mathf.Exp(-10 * dt));
                if (!timerAway) timerPos = Vector3.Lerp(timerPos, TimerHome, 1 - Mathf.Exp(-12 * dt));
            }
            if (dialBy != null && needleDeg != before)
            {   // A task is chosen once the needle is properly inside it, so that it does not flicker on a boundary.
                // The briefing panels catch up when the turning stops.
                var cur = M.tasks[selTask];
                if (needleDeg < TimeToDeg(cur.startMin) - 1.5f || needleDeg > TimeToDeg(cur.endMin) + 1.5f)
                {
                    int i = TaskAtDeg(needleDeg);
                    if (i != selTask) { SelectTask(i, true); sfx.Play("hover", 1); rig.Haptic(dialBy, .5f, .02f); }
                }
            }
            if (dialTask != selTask * 100 + taskIndex) { dialTask = selTask * 100 + taskIndex; DialShowSelection(); }
            // where it is: lying in its place on the desk, or wherever it was carried to, there turned toward the eyes
            dialT.localPosition = timerPos;
            bool up = timerAway || (dialBy != null && timerCarried);
            var facing = up ? Quaternion.LookRotation(dialT.position - head, Vector3.up) : tableFrame.rotation * Quaternion.Euler(TimerHomeEuler);
            dialT.rotation = Quaternion.Slerp(dialT.rotation, facing, 1 - Mathf.Exp(-12 * dt));
            needleT.SetPositionAndRotation(dialT.TransformPoint(0, DialUp, 0), dialT.rotation * Quaternion.Euler(0, 0, -needleDeg)); needleT.localScale = Vector3.one * (dial.alpha * (2 - dial.alpha) * TimerW / .2f * (1 + .05f * timerReach));
        }
        bool LensOverModel => OnPatch(new Vector2(lensPos.x, lensPos.z), -.02f) && lensPos.y < .45f;
        void LensNextLayer()
        {
            lensLayer = lensLayer == "slope" ? "crism" : "slope"; lensToggledAt = Time.time; lensShown = -999; sfx.Play("toggle"); Log("lens shows " + lensLayer);
        }
        // How far a hand has turned about its own long axis between two readings, in degrees. Positive is
        // anticlockwise as the person sees it, looking along the hand.
        static float Twist(Quaternion from, Quaternion to, Vector3 along)
        {
            (to * Quaternion.Inverse(from)).ToAngleAxis(out float angle, out Vector3 axis);
            if (float.IsNaN(axis.x) || float.IsInfinity(axis.x)) return 0;
            if (angle > 180) angle -= 360;
            return angle * Vector3.Dot(axis, along);
        }

        /// <summary>For checks from the Editor: where the timer and the lens are on screen, and what they are doing.</summary>
        public string ToolState()
        {
            Vector3 v = rig.cam.WorldToViewportPoint(dial.go.transform.TransformPoint(0, DialUp, 0)), o = rig.cam.WorldToViewportPoint(orbT.position);
            return $"timer vp=({v.x:F3},{v.y:F3}) held={dialBy != null} carried={timerCarried} away={timerAway} needle={needleDeg:F1} pos={timerPos.ToString("F2")} | lens vp=({o.x:F3},{o.y:F3}) held={lensBy != null} layer={lensLayer} over={LensOverModel} | selTask={selTask}";
        }

        public void ToggleTools() { tools = !tools; if (tools) ResetTools(); sfx.Play("toggle", .7f); Log("timer " + (tools ? "on" : "off")); DrawMenu(); }

        /// <summary>A line in the device log for what the person did, so a session on the headset can be followed from outside.</summary>
        static void Log(string what) => Debug.Log("[ML] " + what);
    }
}
