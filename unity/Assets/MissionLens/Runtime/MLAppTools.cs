using System.Linq;
using UnityEngine;
using static MissionLens.MLPanel;

namespace MissionLens
{
    // Things on the desk that are worked by taking hold of them rather than by pressing a panel. They are
    // experiments in how the briefing could be handled; each does something a button also does, so the app
    // works without them, and "Tools" on the legend sign hides them.
    //
    //   The lens  - a glass ball. Hold it over the model and the ground under it shows a data layer instead of
    //               the image; twist the hand to change the layer. Let go over the model and it stays there.
    //   The timer - a dial. Take hold and turn it to move through the planned timeline; the table follows.
    public partial class MLApp
    {
        public bool tools = true;

        // ---------- the lens ----------
        const float LensR = .1f, OrbR = .043f;                       // footprint on the table, and the ball itself (metres)
        // Its place is on the model's near right corner, where it already shows what it does.
        static readonly Vector3 LensHome = new Vector3(.32f, .12f, -.29f);
        Vector3 lensPos = LensHome; string lensLayer = "slope"; float lensOn, lensRoll, lensToggledAt = -1; int lensShown = -999;
        MLRig.Pointer lensBy; bool lensNear, lensGoHome;
        Transform orbT, stemT; Material orbMat, stemMat; MLPanel lensTag;

        // ---------- the timer ----------
        const float DialSweep = 300;                                  // degrees of the dial the timeline takes up
        MLPanel dial; Transform needleT; MLRig.Pointer dialBy; float needleDeg; int dialTask = -1;

        void BuildTools()
        {
            orbMat = new Material(Sh("MLHand"));
            orbT = Add("Lens", MLMesh.Ball(OrbR), orbMat, tableFrame).transform;
            lensTag = new MLPanel("Lens tag", 420, 120, .15f, null, true); lensTag.Snap(false);
            stemMat = ColorMat(Amber, 3090); stemT = Add("Lens stem", MLMesh.Quad(false), stemMat, null).transform;   // ties the ball to its circle on the ground

            dial = new MLPanel("Timer", 600, 600, .2f, tableFrame); dial.Snap(false); panels.Add(dial);
            var pivot = new GameObject("Timer needle").transform; pivot.SetParent(dial.go.transform.parent, false); needleT = pivot;
            var bar = Add("Bar", MLMesh.Quad(false), ColorMat(Amber, 3100), pivot).transform; bar.localPosition = new Vector3(0, .066f, -.0015f); bar.localScale = new Vector3(.0045f, .058f, 1);
        }

        float TimeToDeg(float min) => -DialSweep / 2 + DialSweep * min / M.plannedTotalMin;
        int TaskAtDeg(float deg)
        {
            float min = Mathf.Clamp01((deg + DialSweep / 2) / DialSweep) * M.plannedTotalMin;
            foreach (var t in M.tasks) if (min < t.endMin) return t.index;
            return M.tasks.Length - 1;
        }

        // The dial is built when the Marswalk moves on; turning it only recolours the arcs and rewrites the middle,
        // so that it stays light while it is being turned.
        readonly System.Collections.Generic.List<UnityEngine.UI.Image>[] dialArcs = new System.Collections.Generic.List<UnityEngine.UI.Image>[32];
        readonly TMPro.TextMeshProUGUI[] dialLetters = new TMPro.TextMeshProUGUI[32]; TMPro.TextMeshProUGUI dialTime, dialWhat, dialMin; int dialBuiltFor = -1;
        void DrawDial()
        {
            var s = dial; s.Clear(); const float C = 300;
            s.Box(6, 6, 588, 588, 294, Back, Line);
            foreach (var t in M.tasks)
            {   // each task is an arc of short strokes, as long as its planned time
                float a0 = TimeToDeg(t.startMin) + 1.2f, a1 = TimeToDeg(t.endMin) - 1.2f; var arc = dialArcs[t.index] = new System.Collections.Generic.List<UnityEngine.UI.Image>();
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
            s.Text("MISSION TIMER · PLANNED", C, 212, 17, Gray, true, 0);
            dialTime = s.Text("", C, 286, 62, Ink, true, 0); dialWhat = s.Text("", C, 328, 26, Cyan, true, 0); dialMin = s.Text("", C, 362, 22, Soft, false, 0);
            s.Text(rig.xr ? "take hold and turn" : "drag around the dial", C, 536, 19, Dim, false, 0);
            s.Hit("timer", 0, 0, 600, 600, () => { }, 294, true);   // so the pointer rests on it instead of taking hold of the map behind
            dialBuiltFor = Finished ? -2 : taskIndex;
        }
        void DialShowSelection()
        {
            if (dialBuiltFor != (Finished ? -2 : taskIndex) || dialTime == null) DrawDial();
            var T = M.tasks[selTask];
            foreach (var t in M.tasks)
            {
                bool on = t.index == selTask; var col = on ? Cyan : t.index == taskIndex && !Finished ? Amber : new Color(1, 1, 1, t.index < taskIndex ? .5f : .22f);
                foreach (var im in dialArcs[t.index]) im.color = col;
                if (dialLetters[t.index] != null) dialLetters[t.index].color = on ? Cyan : Soft;
            }
            dialTime.text = "T+ " + MLData.Hm(T.startMin); dialWhat.text = $"{T.station} · {ShortPhase(T)}"; dialMin.text = $"{T.plannedMin} min";
        }

        // Where on the dial a pointer is, as degrees clockwise from the top: under the hand when the hand is at
        // the dial, otherwise where its ray meets the dial.
        bool DialDeg(MLRig.Pointer p, out float deg)
        {
            deg = 0; var t = dial.go.transform; Vector3 at;
            if (rig.xr && !p.controller && Vector3.Distance(p.origin, t.position) < .17f) at = p.origin;
            else
            {
                var plane = new Plane(t.forward, t.position); var ray = p.Ray;
                if (!plane.Raycast(ray, out float d) || d <= 0) return false; at = ray.GetPoint(d);
            }
            var l = t.InverseTransformPoint(at); float r = new Vector2(l.x, l.y).magnitude;   // canvas units (pixels)
            if (r < 40 || r > 420) return false;
            deg = Mathf.Atan2(l.x, l.y) * Mathf.Rad2Deg; return true;
        }

        // ---------- per frame ----------
        void UpdateTools(float dt)
        {
            bool table = stage == Stage.Table && introDone, live = table && tools && !busy; var head = rig.Head.position;
            dial.Show = table && tools && roomO > .6f;
            if (needleT.gameObject.activeSelf != dial.alpha > .5f) needleT.gameObject.SetActive(dial.alpha > .5f);
            bool orbShow = table && tools && roomO > .6f;
            if (orbT.gameObject.activeSelf != orbShow) orbT.gameObject.SetActive(orbShow);
            lensTag.Show = orbShow;

            var ptrs = rig.xr ? new[] { rig.right, rig.left } : new[] { rig.mouse };
            if (!live) { lensBy = null; dialBy = null; }
            else
            {
                // let go
                if (lensBy != null && (!lensBy.active || !lensBy.held)) { lensBy.tool = null; lensBy = null; sfx.Play("toggle", .5f); Log("lens released at " + lensPos.ToString("F2") + (LensOverModel ? " (over the model)" : " (returns)")); }
                if (dialBy != null && (!dialBy.active || !dialBy.held)) { dialBy.tool = null; dialBy = null; }
                // take hold
                foreach (var p in ptrs)
                {
                    if (!p.active || !p.grabDown || p.tool != null) continue;
                    if (dialBy == null && DialDeg(p, out _)) { dialBy = p; p.tool = dial; rig.Haptic(p, .4f, .03f); Log("timer taken"); continue; }
                    if (lensBy == null && !p.overSurface)
                    {
                        var orb = orbT.position; bool near = rig.xr && !p.controller && Vector3.Distance(p.origin, orb) < .12f;
                        var ray = p.Ray; float along = Vector3.Dot(orb - ray.origin, ray.direction); bool far = along > 0 && Vector3.Distance(ray.GetPoint(along), orb) < .065f;
                        if (near || far) { lensBy = p; lensNear = near; p.tool = orbT; lensRoll = Roll(p); sfx.Play("hover", .8f); rig.Haptic(p, .4f, .03f); Log("lens taken " + (near ? "by hand" : "by ray")); }
                    }
                }
            }

            // The lens follows the hand that holds it; released away from the model it goes back to its place.
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
                // a twist of the hand (or the wheel, on a desktop) changes what the lens shows
                float roll = Roll(lensBy); bool twist = Mathf.Abs(Mathf.DeltaAngle(lensRoll, roll)) > 50, wheel = !rig.xr && rig.wheel != 0;
                if ((twist || wheel) && Time.time - lensToggledAt > .3f)
                {
                    lensLayer = lensLayer == "slope" ? "crism" : "slope"; lensRoll = roll; lensToggledAt = Time.time; lensShown = -999;
                    sfx.Play("toggle"); rig.Haptic(lensBy, .7f, .05f); Log("lens shows " + lensLayer);
                }
            }
            else if (!LensOverModel || lensGoHome) { lensPos = Vector3.Lerp(lensPos, LensHome, 1 - Mathf.Exp(-6 * dt)); if (Vector3.Distance(lensPos, LensHome) < .005f) lensGoHome = false; }
            orbT.localPosition = lensPos;
            lensOn = Mathf.MoveTowards(lensOn, orbShow && LensOverModel ? 1 : 0, dt * 5);
            Shader.SetGlobalVector("_MLLens", new Vector4(lensPos.x, lensPos.z, LensR, lensOn));
            terrainMat.SetFloat("_LensSlope", lensLayer == "slope" ? 1 : 0); terrainMat.SetFloat("_LensCrism", lensLayer == "crism" ? 1 : 0);
            var lc = lensLayer == "slope" ? Amber : new Color(1f, .42f, .5f);
            orbMat.SetColor("_Color", lc); orbMat.SetFloat("_Body", lensBy != null ? .3f : .16f); orbMat.SetFloat("_Rim", .95f);
            if (orbShow)
            {   // a tag by the ball: what it shows, and for slope the value under its centre
                var m = TableToMap(new Vector2(lensPos.x, lensPos.z), tv); int slope = LensOverModel && lensLayer == "slope" ? Mathf.RoundToInt(D.SlopeDeg(m.x, m.y)) : -1;
                int shown = (lensLayer == "slope" ? 0 : 1000) + slope + (LensOverModel ? 0 : 500);
                if (shown != lensShown)
                {
                    lensShown = shown; var s = lensTag; s.Clear();
                    s.Box(0, 0, 420, 120, 22, new Color(8 / 255f, 10 / 255f, 14 / 255f, .86f), new Color(lc.r, lc.g, lc.b, .5f));
                    s.Text(lensLayer == "slope" ? "LENS · SLOPE" : "LENS · SPECTRAL", 24, 48, 30, lc, true);
                    s.Text(!LensOverModel ? (rig.xr ? "pinch and carry over the model" : "drag over the model") : lensLayer == "slope" ? $"{slope}° here · {(rig.xr ? "carry me · twist to change" : "drag me · wheel to change")}" : $"CRISM PHY · {(rig.xr ? "twist to change" : "wheel to change")}", 24, 92, 22, Soft);
                }
                var at = orbT.position + Vector3.up * (OrbR + .045f); float d = Mathf.Max(.2f, Vector3.Distance(at, head));
                lensTag.SetWorldWidth(.17f * d); lensTag.go.transform.SetPositionAndRotation(at, Quaternion.LookRotation(at - head, Vector3.up));
            }
            lensTag.Tick(dt, 6);
            bool stem = orbShow && lensOn > .05f; if (stemT.gameObject.activeSelf != stem) stemT.gameObject.SetActive(stem);
            if (stem)
            {
                var m = TableToMap(new Vector2(lensPos.x, lensPos.z), tv); Vector3 foot = world.TransformPoint(D.Local(m.x, m.y)), top = orbT.position - Vector3.up * OrbR; float len = Mathf.Max(0, top.y - foot.y);
                var flat = Vector3.ProjectOnPlane(foot - head, Vector3.up);
                stemT.SetPositionAndRotation(new Vector3(top.x, foot.y + len / 2, top.z), Quaternion.LookRotation(flat.sqrMagnitude > 1e-6f ? flat : Vector3.forward, Vector3.up));
                stemT.localScale = new Vector3(.0025f, len, 1); var sc = lc; sc.a = .7f * lensOn; stemMat.SetColor("_Color", sc);
            }

            // The timer's needle follows the hand; crossing into another task selects it (and the table follows).
            if (dialBy != null && DialDeg(dialBy, out float deg))
            {
                needleDeg = Mathf.Clamp(deg, -DialSweep / 2, DialSweep / 2); int i = TaskAtDeg(needleDeg);
                if (i != selTask) { SelectTask(i, true); sfx.Play("hover", 1); rig.Haptic(dialBy, .5f, .02f); }
            }
            else
            {
                var T = M.tasks[selTask]; float rest = TimeToDeg((T.startMin + T.endMin) / 2f);
                needleDeg = Mathf.LerpAngle(needleDeg, rest, 1 - Mathf.Exp(-10 * dt));
            }
            if (dialTask != selTask * 100 + taskIndex) { dialTask = selTask * 100 + taskIndex; DialShowSelection(); }
            var dt_ = dial.go.transform; needleT.SetPositionAndRotation(dt_.position, dt_.rotation * Quaternion.Euler(0, 0, -needleDeg)); needleT.localScale = Vector3.one * (dial.alpha * (2 - dial.alpha));
        }
        bool LensOverModel => OnPatch(new Vector2(lensPos.x, lensPos.z), -.02f) && lensPos.y < .45f;
        // How far the pointing hand is rolled about its own pointing direction, in degrees.
        static float Roll(MLRig.Pointer p)
        {
            var f = p.rotation * Vector3.forward; var up = Vector3.ProjectOnPlane(p.rotation * Vector3.up, f); var refUp = Vector3.ProjectOnPlane(Vector3.up, f);
            return refUp.sqrMagnitude < 1e-4f ? 0 : Vector3.SignedAngle(refUp, up, f);
        }

        public void ToggleTools() { tools = !tools; sfx.Play("toggle", .7f); Log("tools " + (tools ? "on" : "off")); DrawLegend(); }

        /// <summary>A line in the device log for what the person did, so a session on the headset can be followed from outside.</summary>
        static void Log(string what) => Debug.Log("[ML] " + what);
    }
}
