using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static MissionLens.MLText;

namespace MissionLens
{
    public enum Stage { Title, Table, Giant, Walk, Field }

    /// <summary>
    /// One Marswalk, two contexts over one state. PLAN: a tabletop terrain model in the room with a briefing
    /// board behind it and the task timeline along its near edge. FIELD: the terrain at true scale and the
    /// real surface image at each station, with a head-up strip and a wrist panel on demand.
    /// The person never moves through the room; the terrain is placed around them.
    /// This file holds the state, the terrain and the transitions; MLAppPanels.cs draws the surfaces and
    /// MLAppFx.cs holds the opening, the markers and the other things that only help the eye.
    /// </summary>
    public partial class MLApp : MonoBehaviour
    {
        public static MLApp I;

        // Where a terrain point sits in the room. ax/az are map metres (x east, z south); w is the room position
        // of that point; yaw turns the terrain so that map north points along room bearing `yaw`.
        struct View { public float S, ex, ax, az, yaw; public bool ground; public Vector3 w; }

        class Mark { public int n, ti; public string station, task; public float x, z, d, az, el, born; public Vector3 local; public MLPanel label; }   // ti: index of the task it was recorded in
        class LegObj { public int index; public Leg leg; public Material mat; public GameObject go; public bool back; }
        class Pin
        {
            public Station st; public MLPanel label; public Vector3 local; public bool shown; public float k = 1, routeM; public Transform ringT, leaderT; public Material ring, leader;
            // how the label is drawn and where it sits as seen from the head (radians): set each frame
            public string text = "", sub = ""; public Color col = Color.white; public int side = 1; public float boxW = 640, dist, az, el0, el, lo, hi, hA;
        }

        const float SGiant = 1f / 25f, MastHeight = 2.0f;   // MastHeight: how high above the ground the surface images were taken from (the rover's mast)
        float FloorY => rig.transform.position.y;            // the floor the person stands on; in FIELD the terrain's ground at the station is put there
        static readonly Color MarsSky = new Color(.79f, .65f, .49f), MockRoom = new Color(.137f, .149f, .169f);

        MLData D; Mission M; MLRig rig; MLAudio sfx;
        Transform world, tableFrame;
        Material terrainMat, panoMat; MeshRenderer terrainR, panoR; Transform panoT; MeshFilter panoF;
        readonly Dictionary<string, Mesh> panoMeshes = new Dictionary<string, Mesh>();
        readonly List<LegObj> legs = new List<LegObj>();
        readonly List<Pin> pins = new List<Pin>();
        readonly List<Mark> marks = new List<Mark>();
        int markSeq, selMark = -1, layerTask = -1; float lastMarkAt = -10;   // selMark: the candidate picked on the map. layerTask: the task whose own layer was last put on the map

        // height of the model's base in map metres (follows what is on the table), and the route's length
        float baseY, routeTotal;

        View view;
        Stage stage = Stage.Title;
        // here: where the Marswalk has got to. preview: a station being looked at from the briefing, ahead of the
        // Marswalk reaching it (null when the person is standing where the Marswalk is).
        string here = "BASE", sel, travelling, preview;
        int taskIndex, selTask;
        readonly HashSet<int> done = new HashSet<int>();
        string layer = "none";
        bool busy, moving, wristUp, placed, introDone;
        /// <summary>In the headset: show a plain studio room around the table instead of the real room.</summary>
        public bool studio; float studioK;
        float panoOpacity, panoTarget, oSlope, oCrism, clipG, roomO = 1, roomTarget = 1;
        string pendingPano, shownPano;
        string flashMsg = ""; float flashUntil, lastPlaced = -10;
        float clipHold = -1;   // 0..1 while a transition drives how far the table's cut is open; -1: it follows the stage

        MTask Task => M.tasks[Mathf.Min(taskIndex, M.tasks.Length - 1)];
        bool Finished => taskIndex >= M.tasks.Length;
        float Pet => M.tasks.Take(taskIndex).Sum(t => t.plannedMin);
        Station St(string id) => D.St(id);
        float HeadingNow => ((rig.HeadYaw - view.yaw) % 360 + 360) % 360;
        bool OnTable => stage == Stage.Table || stage == Stage.Title;
        string At => preview ?? here;   // the station the person stands at in FIELD
        bool CanMark => stage == Stage.Field && preview == null && !Finished && Task.station == here && Task.recordCheck >= 0;

        void Awake()
        {
            I = this;
            MLText.lang = PlayerPrefs.GetString("ml.lang", "en") == "ja" ? "ja" : "en";
            D = MLData.Load(); M = D.M;
            rig = MLRig.Create(); sfx = new MLAudio(rig.gameObject);
            selTask = 0; sel = M.tasks[0].station;
            BuildWorld();
            BuildPanels();
            BuildFx();
            BuildTools();
            BuildGlobe();
            BuildPlan();
            ApplyLayout();
            view = TableView();
            ApplyView();
            Refresh();
            StartCoroutine(Preload());
        }

        // ---------- terrain ----------
        void BuildWorld()
        {
            world = new GameObject("World").transform;
            tableFrame = new GameObject("Table frame").transform;

            // To begin with the table shows the part of the area this Marswalk uses: the route's bounding box plus a
            // margin, fitted to the table's patch. From there the person can slide, stretch and turn it (MLAppTable.cs).
            var pts = new List<Vector2>();
            foreach (var l in M.legs) for (int i = 0; i < l.route.Length; i += 2) pts.Add(new Vector2(l.route[i], l.route[i + 1]));
            foreach (var s in M.stations) pts.Add(new Vector2(s.x, s.z));
            float x0 = pts.Min(p => p.x), x1 = pts.Max(p => p.x), z0 = pts.Min(p => p.y), z1 = pts.Max(p => p.y), ar = FootHx / FootHz;
            float hx = Mathf.Max((x1 - x0) / 2 + 60, ((z1 - z0) / 2 + 60) * ar), hz = hx / ar;
            SMin = Mathf.Min(FootHx / (D.sx / 2), FootHz / (D.sz / 2));
            tvDefault = new TView { ax = Mathf.Clamp((x0 + x1) / 2, -D.sx / 2 + hx, D.sx / 2 - hx), az = Mathf.Clamp((z0 + z1) / 2, -D.sz / 2 + hz, D.sz / 2 - hz), S = FootHx / hx, yawRel = 0 };
            tv = tvDefault; baseY = BaseFor(tv);

            // Terrain mesh: every second DTM sample (4 m) is a vertex; unlit, textured with the HiRISE image.
            const int step = 1;
            int nx = Mathf.CeilToInt((D.gw - 1) / (float)step) + 1, nz = Mathf.CeilToInt((D.gh - 1) / (float)step) + 1;
            var v = new Vector3[nx * nz]; var uv = new Vector2[nx * nz]; var idx = new int[(nx - 1) * (nz - 1) * 6];
            for (int r = 0, k = 0; r < nz; r++) for (int c = 0; c < nx; c++, k++)
            {
                int gc = Mathf.Min(c * step, D.gw - 1), gr = Mathf.Min(r * step, D.gh - 1);
                float fx = gc / (float)(D.gw - 1), fz = gr / (float)(D.gh - 1);
                v[k] = new Vector3((fx - .5f) * D.sx, D.dtm[gr * D.gw + gc], -(fz - .5f) * D.sz); uv[k] = new Vector2(fx, 1 - fz);
            }
            for (int r = 0, k = 0; r < nz - 1; r++) for (int c = 0; c < nx - 1; c++)
            {
                int a = r * nx + c, b = a + 1, d = a + nx, e = d + 1;
                idx[k++] = a; idx[k++] = b; idx[k++] = d; idx[k++] = b; idx[k++] = e; idx[k++] = d;
            }
            var mesh = new Mesh { name = "Terrain", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(v); mesh.SetUVs(0, uv); mesh.SetTriangles(idx, 0); mesh.RecalculateBounds();
            terrainMat = new Material(Resources.Load<Shader>("MLShaders/MLTerrain"));
            terrainMat.SetTexture("_Ortho", Resources.Load<Texture2D>("ML/ortho")); terrainMat.SetTexture("_Slope", Resources.Load<Texture2D>("ML/slope")); terrainMat.SetTexture("_Crism", Resources.Load<Texture2D>("ML/crism_phy"));
            terrainR = Add("Terrain", mesh, terrainMat, world);

            // Planned route: one ribbon per leg. Its width follows the scale (MLAppFx.cs): half a metre on the
            // ground at true scale, a few millimetres on the table however far the map is stretched.
            // uv.x counts metres from the start of the Marswalk, so the opening can draw the route on in order.
            var routeShader = Resources.Load<Shader>("MLShaders/MLRoute"); float u0 = 0;
            for (int li = 0; li < M.legs.Length; li++)
            {
                var leg = M.legs[li]; int n = leg.route.Length / 2; var p = new Vector3[n]; var tint = new Color[n];
                for (int i = 0; i < n; i++)
                {
                    p[i] = D.Local(leg.route[i * 2], leg.route[i * 2 + 1]);
                    tint[i] = SlopeClass(D.SlopeDeg(leg.route[i * 2], leg.route[i * 2 + 1]));   // where the leg is steep, in the slope layer's colours
                }
                var o = new LegObj { index = li, leg = leg, mat = new Material(routeShader) };
                o.go = Add("Route " + leg.from + "-" + leg.to, MLMesh.Ribbon(p, u0, tint), o.mat, world).gameObject;
                legs.Add(o);
                for (int i = 1; i < n; i++) u0 += Vector2.Distance(new Vector2(p[i].x, p[i].z), new Vector2(p[i - 1].x, p[i - 1].z));
            }
            routeTotal = u0;

            // Surface image: a sphere around the head, turned with the terrain.
            panoMat = new Material(Resources.Load<Shader>("MLShaders/MLPano"));
            var pg = new GameObject("Surface image"); panoT = pg.transform; panoF = pg.AddComponent<MeshFilter>(); panoR = pg.AddComponent<MeshRenderer>(); panoR.sharedMaterial = panoMat; panoR.enabled = false;

        }

        // The slope layer's classes (our class breaks): 10-15, 15-20, over 20 degrees. Below 10: no colour.
        public static readonly Color Slope10 = new Color(1, .84f, .25f), Slope15 = new Color(1, .55f, .16f), Slope20 = new Color(.92f, .2f, .2f);
        static Color SlopeClass(float deg) => deg >= 20 ? Slope20 : deg >= 15 ? Slope15 : deg >= 10 ? Slope10 : Color.clear;

        MeshRenderer Add(string name, Mesh mesh, Material mat, Transform parent)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh; var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            return r;
        }
        static Material ColorMat(Color c, int queue, bool always = false)
        {
            var m = new Material(Resources.Load<Shader>("MLShaders/MLColor")); m.SetColor("_Color", c); m.renderQueue = queue;
            m.SetFloat("_ZTest", (float)(always ? UnityEngine.Rendering.CompareFunction.Always : UnityEngine.Rendering.CompareFunction.LessEqual));
            return m;
        }

        // ---------- placing the terrain ----------
        View TableView() => new View { S = tv.S, ex = tableEx, ax = tv.ax, az = tv.az, ground = false, yaw = tableFrame.eulerAngles.y + tv.yawRel, w = tableFrame.position };
        View AtStation(string id, float groundY, float yaw) => new View { S = 1, ex = 1, ax = St(id).x, az = St(id).z, ground = true, yaw = yaw, w = new Vector3(rig.Head.position.x, groundY, rig.Head.position.z) };
        Vector3 AnchorLocal(View v) => new Vector3(v.ax, v.ground ? D.Elev(v.ax, v.az) : baseY, -v.az);

        void ApplyView() => Place(view.S, view.ex, view.yaw, AnchorLocal(view), view.w);
        void Place(float S, float ex, float yaw, Vector3 local, Vector3 at)
        {
            var scale = new Vector3(S, S * ex, S); var rot = Quaternion.Euler(0, yaw, 0);
            world.localScale = scale; world.rotation = rot; world.position = at - rot * Vector3.Scale(scale, local);
        }

        // Keeps the destination point as the pivot, so the model grows (or shrinks) around where you are going.
        // With `pivot` (a point of the terrain) the model changes size around that point instead, which travels from
        // where it is now to where the target view puts it.
        IEnumerator FlyTo(View target, float dur = 2.6f, Vector3? pivot = null, Action<float> during = null)
        {
            var lp = pivot ?? AnchorLocal(target); Vector3 w0 = world.TransformPoint(lp), w1 = pivot.HasValue ? WorldOf(target, lp) : target.w;
            float s0 = Mathf.Log(view.S), s1 = Mathf.Log(target.S), e0 = view.ex, y0 = view.yaw;
            yield return Tween(dur, k => { float e = Ease(k); Place(Mathf.Exp(s0 + (s1 - s0) * e), e0 + (target.ex - e0) * e, Mathf.LerpAngle(y0, target.yaw, e), lp, Vector3.Lerp(w0, w1, e)); during?.Invoke(e); });
            view = target; ApplyView();
        }
        /// <summary>Where a point of the terrain lies in the room under a given view.</summary>
        Vector3 WorldOf(View v, Vector3 local) => v.w + Quaternion.Euler(0, v.yaw, 0) * Vector3.Scale(new Vector3(v.S, v.S * v.ex, v.S), local - AnchorLocal(v));
        static float Ease(float k) => k * k * (3 - 2 * k);
        static float EaseOut(float k) => 1 - (1 - k) * (1 - k) * (1 - k);
        static IEnumerator Tween(float dur, Action<float> fn)
        {
            for (float t = 0; t < 1;) { t = Mathf.Min(1, t + Time.deltaTime / dur); fn(t); yield return null; }
        }

        /// <summary>
        /// Puts the table in front of the person: the model within easy reach, its base about 64 cm below the eyes
        /// (waist height standing, desk height seated), with the briefing wall just behind it.
        /// </summary>
        public void PlaceTable()
        {
            var h = rig.Head; var fwd = Vector3.ProjectOnPlane(h.forward, Vector3.up); if (fwd.sqrMagnitude < .01f) fwd = Vector3.forward; fwd.Normalize();
            float y = rig.xr ? Mathf.Clamp(h.position.y - EyeAboveTable, .68f, 1.02f) : 1.6f - EyeAboveTable;
            tableFrame.position = new Vector3(h.position.x, y, h.position.z) + fwd * TableAhead;
            tableFrame.rotation = Quaternion.LookRotation(fwd, Vector3.up);
            if (OnTable) { view = TableView(); ApplyView(); }
            placed = true; lastPlaced = Time.time; PlaceStudio(); Refresh();
        }
        const float EyeAboveTable = .64f, TableAhead = .84f;
        float TableLookPitch => layout == "desk" ? -36 : -24;   // where the desktop camera looks at the table

        // ---------- stage changes ----------
        public void SelectStation(string id)
        {
            sel = id; var t = M.tasks.FirstOrDefault(x => x.station == id && x.index >= taskIndex) ?? M.tasks.FirstOrDefault(x => x.station == id);
            if (t != null) selTask = t.index; selMark = -1; selCap = -1; PlanPicked(id); TaskLayer(); Refresh();
        }

        // Each kind of task opens with what serves it on the map, so the person does not have to go looking for it:
        // a walk shows the slope classes; work at a station shows the plain image with contours, so that the ground
        // to be looked at is not covered (the evidence for the station is on the panel beside it). It is put on when
        // another task is chosen. A layer changed by hand stays as it was set for as long as the same task is looked at.
        // The spectral layer is not put on by itself: at 18 m per pixel it would bury the image of an outcrop under a
        // few coloured squares. Its reading for the station is on the evidence panel, and the layer is on the menu.
        static string LayerFor(MTask t) => t.leg >= 0 ? "slope" : "none";
        void TaskLayer(bool force = false)
        {
            if (selTask == layerTask && !force) return;
            // While the board shows the observation plan the map keeps the plain image: the plan is about the outcrops,
            // and a walk's slope classes would cover them (picking an outcrop there selects the walk to it). On the
            // board's other face each task has its own layer, as before. The layers stay on the menu on both.
            layerTask = selTask; string want = planTab ? "none" : LayerFor(M.tasks[selTask]);
            if (want != layer) { layer = want; if (introDone && stage == Stage.Table) DrawMenu(); }
        }
        static string LayerNote(string l) => l == "slope" ? L("slope classes, from the terrain model (a reading aid, not a clearance to walk)", "傾斜の区分(地形モデルから計算。読み取りの補助で、通行の可否の判定ではない)")
            : l == "crism" ? L("regional spectral context (CRISM), 18 m/pixel. Not a ranking of individual outcrops, and not the minerals of any one rock", "周辺の分光データ(CRISM、1 画素 18 m)。個々の露頭の順位でも、個々の岩石の鉱物の特定でもない")
            : L("the HiRISE image with contour lines", "HiRISE の画像と等高線");

        /// <summary>
        /// Sets the simulated Marswalk to begin at the task that is being looked at (for a demonstration that starts
        /// part way through). This is the only way looking at a task changes where the Marswalk is; records are kept.
        /// </summary>
        public void StartFromSelected()
        {
            if (busy || stage != Stage.Table) return;
            var t = M.tasks[selTask]; taskIndex = selTask; done.Clear(); here = t.leg >= 0 ? M.legs[t.leg].from : t.station; preview = null;
            sfx.Play("ready", .6f); Log($"simulated start at task {selTask} ({t.station} {t.phase}), station {here}"); Refresh();
        }

        /// <summary>A candidate picked on the table's map: its station and the task it was recorded in come up, and the map makes room for it.</summary>
        public void SelectMark(int n)
        {
            var m = marks.Find(x => x.n == n); if (m == null || stage != Stage.Table) return;
            selMark = n; selTask = Mathf.Clamp(m.ti, 0, M.tasks.Length - 1); sel = m.station; TaskLayer(); sfx.Play("toggle", .6f); Log("candidate " + n + " selected"); Refresh();
            foreach (var x in marks) DrawMarkLabel(x);
            if (introDone && !busy) TableTo(FramingMarks(m.station));
        }
        // The view that holds a station and the candidates recorded from it.
        TView FramingMarks(string station)
        {
            var s = St(station); var pts = new List<Vector2> { new Vector2(s.x, s.z) }; pts.AddRange(marks.Where(m => m.station == station).Select(m => new Vector2(m.x, m.z)));
            return Framing(pts, 28);
        }
        /// <param name="settling">More selections are about to follow (the timer is being turned): the big panels wait until it stops.</param>
        public void SelectTask(int i, bool settling = false)
        {
            selTask = i; sel = M.tasks[i].station; selCap = -1; pairView = false; if (selMark >= 0) { selMark = -1; foreach (var x in marks) DrawMarkLabel(x); }
            TaskLayer(); Refresh(settling);
            if (followTasks && stage == Stage.Table && introDone && !busy) TableTo(FramingFor(M.tasks[i]));   // bring its leg or station onto the table
        }

        public void ToTable() { if (!busy) StartCoroutine(CoToTable()); }
        IEnumerator CoToTable()
        {
            busy = moving = true; HideBanner(); sfx.Play("whoosh", .8f);
            // The terrain shrinks back into the model around the place where the person stands: that place goes from
            // under the feet to its spot on the table. (It used to shrink around the map point at the table's centre,
            // which at true scale can be hundreds of metres away: the model then stayed a speck in the distance and
            // arrived all at once at the end.) The table shows the part of the map the person is coming back from.
            var standing = new Vector3(view.ax, D.Elev(view.ax, view.az), -view.az); var from = new Vector2(view.ax, view.az); string At0 = At;
            // Coming back from a look ahead, the briefing goes on where it was: the station that was looked at stays
            // selected, with the task that was selected for it (or its first task), not the task the Marswalk is at.
            // The same holds when a photo was taken during the Marswalk itself: the place and the photo that were
            // worked on are what the table opens with, whatever task the Marswalk has moved on to.
            bool ahead = preview != null && !Finished, photos = !Finished && PhotosThisStay(At0);
            panoTarget = 0; roomTarget = 1; stage = Stage.Table; preview = null;
            if (ahead || photos)
            {
                sel = At0;
                if (!ahead && Task.station == At0 && Task.leg < 0) selTask = taskIndex;
                else if (M.tasks[selTask].station != At0 || M.tasks[selTask].leg >= 0) selTask = (M.tasks.FirstOrDefault(x => x.station == At0 && x.leg < 0) ?? M.tasks[selTask]).index;
                PlanCameBack(At0);
            }
            else { selTask = Mathf.Min(taskIndex, M.tasks.Length - 1); sel = M.tasks[selTask].station; }
            if (framing != null) { StopCoroutine(framing); framing = null; }
            string cameFrom = At0; var brought = marks.Where(m => m.station == cameFrom).ToList();
            // The table shows what was brought back: the regions marked there, or the stations where views were framed for the plan.
            var framed = caps.Select(c => c.station).Append(cameFrom).Distinct().Select(id => new Vector2(St(id).x, St(id).z)).ToList();
            if (followTasks) { tv = brought.Count > 0 ? FramingMarks(cameFrom) : (ahead || photos) && caps.Count > 0 ? Framing(framed, 30) : FramingFor(M.tasks[selTask]); if (!OnPatch(from.x, from.y, .03f)) tv = Framing(new[] { from }, 60); }
            baseY = BaseFor(tv); selMark = -1; TaskLayer(true); Refresh();
            if (!rig.xr) StartCoroutine(Look(2.2f, tableFrame.eulerAngles.y, TableLookPitch));
            clipHold = 1;   // the table's edge closes in on the model as it lands, not before
            yield return FlyTo(TableView(), 2.6f, standing, e => clipHold = 1 - e);
            clipHold = -1; clipG = 0;
            busy = moving = false; Refresh();
            if (Finished) sfx.Play("ready", .7f);
            else if (brought.Count > 0)
            {   // what was recorded out there is on the map now: say so, and let each mark show itself
                foreach (var m in brought) { m.born = Time.time; Ripple(world.TransformPoint(m.local), MLPanel.Ink, .09f); }
                sfx.Play("mark", .7f);
                ShowBanner(L("SIMULATED RECORD · NEEDS LOCAL VERIFICATION", "模擬の記録 · 現地での確認が必要"),
                    L($"{brought.Count} observation region{(brought.Count > 1 ? "s" : "")} on the map", $"観測領域 {brought.Count} 件を地図に記録"),
                    L($"Pointed at in the {cameFrom} {St(cameFrom).name} panorama: {string.Join(", ", brought.Select(m => "#" + m.n.ToString("00")))}. Each mark is an approximate terrain intersection, not a surveyed sample site.", $"{cameFrom} {St(cameFrom).name} のパノラマで指した領域: {string.Join("、", brought.Select(m => "#" + m.n.ToString("00")))}。印は地形モデルとのおおよその交点で、測量した採取位置ではありません。"), 3.2f);
            }
        }

        public void EnterTerrain(string id, float S) { if (!busy) StartCoroutine(CoEnterTerrain(id, S)); }
        IEnumerator CoEnterTerrain(string id, float S)
        {
            busy = moving = true; HideBanner(); sfx.Play("whoosh", .8f);
            panoTarget = 0; roomTarget = 0; stage = S == 1 ? Stage.Walk : Stage.Giant; sel = id; wristUp = !rig.xr; wristPlaced = false; Refresh();   // on the headset the panel is on the opened left hand, as in FIELD: it is not left standing in the view
            float dur = stage == Stage.Giant ? 2.4f : 3.2f;
            if (!rig.xr) StartCoroutine(Look(dur, rig.lookYaw, S == 1 ? -6 : -14));
            var target = AtStation(id, 0, view.yaw); target.S = S;
            yield return FlyTo(target, dur);
            busy = moving = false; Refresh();
        }

        /// <summary>
        /// Stand at a station, inside its surface image. At the station the Marswalk has reached this is FIELD proper;
        /// at any other it is a look ahead from the briefing, and the Marswalk stays where it was.
        /// </summary>
        public void ToField(string id) { if (!busy) StartCoroutine(CoToField(id)); }
        IEnumerator CoToField(string id)
        {
            busy = moving = true; HideBanner(); sfx.Play("whoosh");
            roomTarget = 0; bool fromTable = stage == Stage.Table; preview = id == here ? null : id; sel = id; wristUp = false; wristPlaced = false;
            if (panoOpacity < .02f || shownPano == id) ShowPano(id); else { panoTarget = 0; pendingPano = id; }
            stage = Stage.Field; Refresh();
            float dur = view.S == 1 ? 1.2f : 3.0f;
            // The eye sits 2 m above the ground here, the height of the camera that took the image. Coming from the
            // table you arrive facing the same way as the picture on the briefing board.
            // Going back to a saved capture (MLAppPlan.cs) you arrive facing the way that capture looked.
            float yaw = faceAz.HasValue ? rig.HeadYaw - faceAz.Value : fromTable ? rig.HeadYaw - St(id).viewAz : view.yaw;
            if (!rig.xr) StartCoroutine(Look(dur, rig.lookYaw, faceAz.HasValue ? faceEl : -6));
            faceAz = null;
            yield return FlyTo(AtStation(id, FloorY, yaw), dur);
            if (pendingPano != null) { ShowPano(pendingPano); pendingPano = null; }
            panoTarget = 1; busy = moving = false; Refresh(); Arrived(id);
        }

        List<Vector2> RouteBetween(string a, string b)
        {
            var f = M.legs.FirstOrDefault(l => l.from == a && l.to == b); var r = M.legs.FirstOrDefault(l => l.from == b && l.to == a);
            var src = f ?? r; var pts = new List<Vector2>();
            if (src == null) { pts.Add(new Vector2(St(a).x, St(a).z)); pts.Add(new Vector2(St(b).x, St(b).z)); return pts; }
            for (int i = 0; i < src.route.Length; i += 2) pts.Add(new Vector2(src.route[i], src.route[i + 1]));
            if (f == null) pts.Reverse();
            return pts;
        }

        public void Travel(string to) { if (!busy && to != here && preview == null) StartCoroutine(CoTravel(to)); }
        // On the way the walk stops once, where the ground is steepest, and waits to be told to go on.
        class Checkpoint { public float slope, ahead, toGo; public Vector3 local; }
        Checkpoint checkpoint;
        public void ConfirmCheckpoint() { if (checkpoint == null) return; Log($"route check confirmed: slope {checkpoint.slope:0} deg"); checkpoint = null; sfx.Play("mark", .7f); }
        bool CanPress(MLPanel.Btn b) => !busy || (checkpoint != null && b.key == "HudAction");
        static Vector2 RouteAt(List<Vector2> pts, float[] cum, float d, out int i)
        {
            i = 1; while (i < pts.Count - 1 && cum[i] < d) i++;
            return Vector2.Lerp(pts[i - 1], pts[i], Mathf.Clamp01((d - cum[i - 1]) / Mathf.Max(1e-6f, cum[i] - cum[i - 1])));
        }

        IEnumerator CoTravel(string to)
        {
            busy = moving = true; HideBanner(); sfx.Play("travel");
            var pts = RouteBetween(here, to); var cum = new float[pts.Count];
            for (int i = 1; i < pts.Count; i++) cum[i] = cum[i - 1] + Vector2.Distance(pts[i], pts[i - 1]);
            float total = cum[pts.Count - 1], eye = rig.Head.position.y;
            panoTarget = 0; pendingPano = to; stage = Stage.Walk; wristUp = false; travelling = to; Refresh();
            float g0 = view.w.y;
            yield return Tween(.7f, k => { view.w.y = Mathf.Lerp(g0, 0, k); ApplyView(); });
            // The walk is shown many times faster than it is planned to take. To keep it a walk all the same: soft
            // steps at an even stride (so they quicken and slow with the pace), the distance left counting down,
            // and the planned clock running through the minutes the leg is given.
            float dur = Mathf.Clamp(total / 45f, 3, 8); int strides = Mathf.RoundToInt(dur * 2.1f), stepped = 0;
            var legTask = !Finished && Task.leg >= 0 && Task.station == to ? Task : null;
            IEnumerator Walk(float d0, float d1) => Tween(Mathf.Max(1.6f, dur * (d1 - d0) / total), k =>
            {
                float d = Mathf.Lerp(d0, d1, Ease(k)), e = d / total;
                if (e * strides >= stepped + .5f) { sfx.Play((stepped & 1) == 0 ? "stepA" : "stepB", .5f); stepped++; }
                if (hudSub != null && flashMsg.Length == 0)
                    hudSub.text = L($"{total - d:0} m to go", $"残り {total - d:0} m") + (legTask != null ? L($" · {legTask.plannedMin} min on the plan, shown faster", $" · 計画では {legTask.plannedMin} 分(早送りで表示)") : "");
                if (hudClock != null && legTask != null) hudClock.text = HudClock(Mathf.Lerp(legTask.startMin, legTask.endMin, e));
                var p = RouteAt(pts, cum, d, out int i);
                view.ax = p.x; view.az = p.y; view.w = new Vector3(rig.Head.position.x, 0, rig.Head.position.z); ApplyView();
                if (!rig.xr && Time.time - rig.lastDragTime > 1.5f)
                {   // desktop: face the way you are walking unless the user is looking around
                    float want = MLData.Bearing(pts[i - 1].x, pts[i - 1].y, pts[i].x, pts[i].y) + view.yaw;
                    rig.lookYaw = Mathf.LerpAngle(rig.lookYaw, want, .06f); rig.lookPitch += (-6 - rig.lookPitch) * .05f;
                }
            });
            // The route check. Where on this leg is the ground steepest (slope of the terrain model along the planned
            // route)? If it reaches a slope class (10 degrees or more) the walk stops a few metres short of it, the
            // slope classes are painted on the ground, and it goes on when the person has looked and confirmed.
            float steep = 0, dSteep = 0;
            for (float d = 0; d <= total; d += 2) { var q = RouteAt(pts, cum, d, out _); float sl = D.SlopeDeg(q.x, q.y); if (sl > steep) { steep = sl; dSteep = d; } }
            bool check = steep >= 10 && total > 40; float dStop = check ? Mathf.Clamp(dSteep - 8, total * .2f, total * .8f) : total;
            yield return Walk(0, dStop);
            if (check)
            {
                var sp = RouteAt(pts, cum, dSteep, out _);
                checkpoint = new Checkpoint { slope = steep, ahead = dSteep - dStop, toGo = total - dStop, local = D.Local(sp.x, sp.y, .4f) };
                moving = false; sfx.Play("ready", .5f); Log($"route check: slope {steep:0} deg, {total - dStop:0} m to go"); Refresh();
                float nextRing = 0; var ringCol = SlopeClass(steep);
                while (checkpoint != null)
                {   // a ring keeps marking the place
                    if (Time.time > nextRing) { nextRing = Time.time + 1.4f; var at = world.TransformPoint(checkpoint.local); Ripple(at, ringCol, Mathf.Clamp(Vector3.Distance(at, rig.Head.position) * .3f, 1.5f, 8f)); }
                    yield return null;
                }
                moving = true; Refresh();
                yield return Walk(dStop, total);
            }
            float g1 = FloorY;
            yield return Tween(.6f, k => { view.w.y = Mathf.Lerp(0, g1, k); ApplyView(); });
            here = to; sel = to; stage = Stage.Field; panoTarget = 1; travelling = null;
            if (!Finished && Task.leg >= 0 && Task.station == to) { taskIndex++; done.Clear(); }   // an Approach task ends on arrival
            busy = moving = false; Refresh(); Arrived(to);
            var feet = rig.Head.position; feet.y = rig.transform.position.y + .03f; Ripple(feet, MLPanel.Ink, 9f);   // a ring spreading over the ground from where you have come to stand
        }

        public void Rescale(float S) { if (!busy) StartCoroutine(CoRescale(S)); }
        IEnumerator CoRescale(float S)
        {
            busy = moving = true; sfx.Play("travel", .8f); stage = S == 1 ? Stage.Walk : Stage.Giant; Refresh();
            var target = view; target.S = S; target.ex = 1; target.ground = true; target.w = new Vector3(rig.Head.position.x, 0, rig.Head.position.z);
            yield return FlyTo(target, 1.8f);
            busy = moving = false; Refresh();
        }

        public void ToggleHeight() { if (!busy) StartCoroutine(CoHeight()); }
        IEnumerator CoHeight() { busy = true; tableEx = tableEx > 1 ? 1 : 2; yield return FlyTo(TableView(), .6f); busy = false; Refresh(); DrawMenu(); }

        public void SetLayer(string name) { layer = layer == name ? "none" : name; sfx.Play("toggle", .7f); Refresh(); DrawMenu(); }
        public void CompleteTask()
        {
            if (Finished) return;
            taskIndex++; done.Clear(); sfx.Play("done"); Refresh();
            if (!Finished) Flash(L($"Task complete · T+ {MLData.Hm(Pet)} on the planned timeline", $"作業を完了 · 計画上の T+ {MLData.Hm(Pet)}"), 3); else MissionComplete();
        }
        public void ToggleCheck(int i) { if (!done.Remove(i)) done.Add(i); sfx.Play("toggle"); Refresh(); }
        public void ToggleWrist() { if (!OnTable) { wristUp = !wristUp; sfx.Play("toggle", .6f); wristPlaced = false; Refresh(); } }

        /// <summary>Back to the title with nothing done and nothing recorded.</summary>
        public void Restart()
        {
            if (busy) return;
            StopAllCoroutines(); HideBanner();
            foreach (var m in marks) Destroy(m.label.go);
            marks.Clear(); markSeq = 0; selMark = -1; layerTask = -1; done.Clear(); taskIndex = 0; selTask = 0; sel = M.tasks[0].station; here = "BASE"; layer = "none";
            wristUp = false; travelling = null; pendingPano = null; preview = null; flashMsg = ""; restartArmedUntil = 0; checkpoint = null; clipHold = -1;
            panoTarget = 0; roomTarget = 1; introDone = false; frontR = 0; routeDraw = 0; plateK = 0; moving = false;
            foreach (var p in pins) p.shown = false;
            if (framing != null) framing = null;
            tv = tvDefault; tableEx = 1; baseY = BaseFor(tv); grabs.Clear(); ResetTools(); menuIntro = rig.xr; menuPinned = false;
            stage = Stage.Title; view = TableView(); ApplyView();
            if (!rig.xr) StartCoroutine(Look(1.2f, tableFrame.eulerAngles.y, -3));
            NewPlan();   // the observation plan too starts empty (a plan that was saved stays on disk)
            Refresh();
        }

        // Desktop only: turn the camera as part of a transition (where a person would naturally look).
        IEnumerator Look(float dur, float yaw, float pitch)
        {
            float y0 = rig.lookYaw, p0 = rig.lookPitch;
            yield return Tween(dur, k => { float e = Ease(k); rig.lookYaw = Mathf.LerpAngle(y0, yaw, e); rig.lookPitch = Mathf.Lerp(p0, pitch, e); });
        }

        (string id, float d) Nearest()
        {
            string best = null; float bd = float.MaxValue;
            foreach (var s in M.stations) { float d = Vector2.Distance(new Vector2(s.x, s.z), new Vector2(view.ax, view.az)); if (d < bd) { bd = d; best = s.id; } }
            return (best, bd);
        }
        (float az, float d) BearingFromHere(string to)
        {
            float x = stage == Stage.Field ? St(At).x : view.ax, z = stage == Stage.Field ? St(At).z : view.az; var t = St(to);
            return (MLData.Bearing(x, z, t.x, t.z), Vector2.Distance(new Vector2(x, z), new Vector2(t.x, t.z)));
        }
        /// <summary>The station the person is being sent to next, if it is not where they are.</summary>
        string Target
        {
            get
            {
                if (travelling != null) return travelling;
                if (Finished || OnTable) return null;
                string s = Task.station; if (stage == Stage.Field) return preview == null && s != here ? s : null;
                return Vector2.Distance(new Vector2(view.ax, view.az), new Vector2(St(s).x, St(s).z)) > 8 ? s : null;
            }
        }

        // ---------- surface image ----------
        // The panoramas are large; they are read in the background while the title is up, so that standing at a
        // station does not stall a frame in the headset.
        readonly Dictionary<string, Texture2D> panoTex = new Dictionary<string, Texture2D>();
        IEnumerator Preload()
        {
            foreach (var s in M.stations) { var r = Resources.LoadAsync<Texture2D>("ML/pano_" + s.id); yield return r; panoTex[s.id] = r.asset as Texture2D; }
        }
        void ShowPano(string id)
        {
            var st = St(id);
            if (!panoMeshes.TryGetValue(id, out var mesh)) { mesh = MLMesh.PanoSphere(st.panoElMax, st.panoElMin, 400, 90, -90); panoMeshes[id] = mesh; }
            panoF.sharedMesh = mesh; panoMat.SetTexture("_MainTex", panoTex.TryGetValue(id, out var tex) && tex != null ? tex : Resources.Load<Texture2D>("ML/pano_" + id));
            panoMat.SetTexture("_Mask", Resources.Load<Texture2D>("ML/panomask_" + id));
            panoMat.SetFloat("_VTop", (90 - st.panoElMin) / (st.panoElMax - st.panoElMin));
            panoMat.SetFloat("_ElMin", st.panoElMin); panoMat.SetFloat("_ElMax", st.panoElMax); panoMat.SetFloat("_CamH", MastHeight);
            var top = st.panoSky != null && st.panoSky.Length == 3 ? new Color(st.panoSky[0], st.panoSky[1], st.panoSky[2]) : MarsSky;
            panoMat.SetColor("_SkyTop", top * .86f);
            if (st.panoGround != null && st.panoGround.Length == 3) panoMat.SetColor("_Ground", new Color(st.panoGround[0], st.panoGround[1], st.panoGround[2]));
            shownPano = id; panoR.enabled = true;
        }

        // ---------- records ----------
        // An observation region: a part of the panorama that was pointed at, to be looked at on site. What is recorded
        // is the image and the direction from the place the image was taken (az, el: MLAppPlan.cs, PanoAim). The mark
        // on the map is a second, approximate reading: where the pointing ray first meets the terrain model (a 2 m
        // grid, which knows nothing of the rocks in the image). It is not a surveyed position of anything.
        bool PointOnTerrain(Ray ray, out Vector3 local, out float dist)
        {
            Vector3 o = world.InverseTransformPoint(ray.origin), d = world.InverseTransformDirection(ray.direction).normalized;
            for (float t = .5f; t < 900; t += t < 60 ? .1f : .5f)
            {
                var p = o + d * t; if (Mathf.Abs(p.x) > D.sx / 2 - 1 || Mathf.Abs(p.z) > D.sz / 2 - 1) break;
                float y = D.Elev(p.x, -p.z); if (p.y <= y) { local = new Vector3(p.x, y, p.z); dist = t; return true; }
            }
            local = default; dist = 0; return false;
        }
        void TryMark(MLRig.Pointer p, Vector3 local, float dist, Vector3 dirLocal)
        {
            if (Time.time - lastMarkAt < .6f) return;   // a pinch that is read twice is still one record
            lastMarkAt = Time.time;
            var same = marks.Find(m => m.station == here && Vector2.Distance(new Vector2(m.x, m.z), new Vector2(local.x, -local.z)) < 1.5f);
            if (same != null) { sfx.Play("deny"); Flash(L($"Already recorded here as region {same.n:00}", $"ここは領域 {same.n:00} として記録済みです")); return; }
            rig.Haptic(p, .8f, .08f); AddMark(local, dist, dirLocal);
        }
        void AddMark(Vector3 local, float dist, Vector3 dirLocal)
        {
            var m = new Mark { n = ++markSeq, ti = taskIndex, station = here, task = Task.phase, x = local.x, z = -local.z, d = dist, local = local + Vector3.up * .15f, born = Time.time,
                az = (Mathf.Atan2(dirLocal.x, dirLocal.z) * Mathf.Rad2Deg + 360) % 360, el = Mathf.Asin(Mathf.Clamp(dirLocal.y, -1, 1)) * Mathf.Rad2Deg };
            // The direction that is kept is the one from the panorama's centre to the part of the image pointed at,
            // not the direction of the hand's own ray: the hand is beside the head, and the image has no depth to correct for that.
            var dirW = world.TransformDirection(dirLocal).normalized;
            if (PanoAim(new Ray(world.TransformPoint(local) - dirW * dist * world.localScale.x, dirW), out float paz, out float pel)) { m.az = paz; m.el = pel; }
            m.label = MakeMarkLabel(m.n); marks.Add(m); DrawMarkLabel(m);
            if (Task.recordCheck >= 0) done.Add(Task.recordCheck);
            sfx.Play("mark"); Ripple(world.TransformPoint(m.local), MLPanel.Ink, Mathf.Clamp(dist * .16f, .3f, 12f));
            Log($"observation region {m.n} recorded at {here}: image direction az {m.az:0.0} el {m.el:0.0}; terrain intersection {dist:0} m (approximate)");
            Refresh(); Flash(L($"Observation region {m.n:00} · needs local verification · image direction {MLData.Deg3(m.az)}°", $"観測領域 {m.n:00} · 現地での確認が必要 · 画像の方向 {MLData.Deg3(m.az)}°"), 4.5f);
        }
        public void UndoMark()
        {
            int i = marks.FindLastIndex(m => m.station == here); if (i < 0) return;
            Destroy(marks[i].label.go); marks.RemoveAt(i); sfx.Play("undo");
            if (!marks.Any(m => m.station == here) && Task.recordCheck >= 0) done.Remove(Task.recordCheck);
            Refresh();
        }
        void Flash(string msg, float seconds = 3.5f) { flashMsg = msg; flashUntil = Time.time + seconds; DrawHud(); }

        // ---------- per frame ----------
        void Update()
        {
            if (!placed && !rig.xr) PlaceTable();   // in the headset the table is placed when the briefing begins
            if (!rig.xr && rig.wristToggle) ToggleWrist();
            if (stage == Stage.Title && !busy && rig.anyKey) Begin();

            float dt = Time.deltaTime;
            roomO = Mathf.MoveTowards(roomO, roomTarget, dt * .9f);
            panoOpacity = Mathf.MoveTowards(panoOpacity, panoTarget, dt * 1.4f);
            if (pendingPano != null && panoTarget == 0 && panoOpacity < .02f) { ShowPano(pendingPano); pendingPano = null; }
            string lay = stage == Stage.Table ? layer : checkpoint != null ? "slope" : "none";   // at a route check the slope classes are painted on the ground
            oSlope = Mathf.MoveTowards(oSlope, lay == "slope" ? 1 : 0, dt * 3); oCrism = Mathf.MoveTowards(oCrism, lay == "crism" ? 1 : 0, dt * 3);
            clipG = clipHold >= 0 ? clipHold : Mathf.MoveTowards(clipG, OnTable ? 0 : 1, dt * 1.6f);
            // On the table the terrain is cut to the table's patch; leaving it, the cut opens out to the whole area.
            float open = Mathf.Exp(clipG * 8.5f);
            Shader.SetGlobalMatrix("_MLTableInv", tableFrame.worldToLocalMatrix); Shader.SetGlobalVector("_MLTableClip", new Vector4(FootHx * open, FootHz * open, 0, 0));
            terrainMat.SetFloat("_OSlope", oSlope); terrainMat.SetFloat("_OCrism", oCrism); terrainMat.SetFloat("_Dim", panoOpacity);
            // Contour lines are always on the model, lightly: they are what makes its relief readable. Full strength with the slope layer.
            terrainMat.SetFloat("_Contour", Mathf.Max(oSlope, .55f)); terrainMat.SetFloat("_Step", ContourStep);
            terrainMat.SetFloat("_Fog", clipG * (1 - panoOpacity)); terrainMat.SetFloat("_InvS", 1 / world.localScale.x);
            panoMat.SetFloat("_Opacity", panoOpacity); panoR.enabled = shownPano != null && panoOpacity > .01f;
            // The surface image is drawn from where it was taken: the rover's mast over the station, not the person's
            // head. Its lower half lies on the floor there (see MLPano.shader), so the ground is at the feet.
            var foot = shownPano != null ? world.TransformPoint(D.Local(St(shownPano).x, St(shownPano).z)) : new Vector3(rig.Head.position.x, FloorY, rig.Head.position.z);
            if (panoR.enabled) { panoMat.SetMatrix("_ToMap", world.worldToLocalMatrix); panoMat.SetFloat("_FloorY", foot.y); }
            panoT.position = foot + Vector3.up * MastHeight; panoT.rotation = Quaternion.Euler(0, view.yaw, 0);
            // What is behind the table: the real room (passthrough) or, if chosen, the plain studio room.
            studioK = Mathf.MoveTowards(studioK, rig.xr && !studio ? 0 : 1, dt * 1.5f);
            float realRoom = roomO * (1 - studioK);
            rig.SetBackground(realRoom > .01f, MockRoom); rig.handPresence = 1 - realRoom;

            // Each of these is kept apart: on a headset this code has not been run on, a fault in one of them
            // should cost that part and be logged once, not stop the frame for the others.
            if (heavyDue > 0 && Time.time >= heavyDue) { heavyDue = 0; if (stage == Stage.Table) { DrawBoard(); DrawStrip(); } }
            Guard(0, () => UpdateFx(dt)); Guard(1, () => UpdateSurfaces(dt)); Guard(2, Point); Guard(3, () => UpdateTools(dt)); Guard(4, () => UpdateTable(dt)); Guard(5, () => UpdatePlan(dt));
            if (flashMsg.Length > 0 && Time.time > flashUntil) { flashMsg = ""; DrawHud(); }
        }
        readonly bool[] guardLogged = new bool[6];
        void Guard(int i, Action step)
        {
            try { step(); }
            catch (Exception e) { if (!guardLogged[i]) { guardLogged[i] = true; Debug.LogException(e); } }
        }

        // What each pointer is aimed at, nearest surface first; a press acts on it.
        readonly Dictionary<MLPanel, MLPanel.Btn> hovered = new Dictionary<MLPanel, MLPanel.Btn>();
        const float GazeDwell = 1.4f; bool gazeArmed = true;
        void Point()
        {
            hovered.Clear(); bool reticle = false; float reach = rig.xr ? 16 : 0;
            if (debugGaze && !rig.xr) { rig.gaze.active = true; rig.gaze.origin = rig.Head.position; rig.gaze.rotation = rig.Head.rotation; }
            foreach (var p in rig.xr ? new[] { rig.right, rig.left, rig.gaze } : debugGaze ? new[] { rig.gaze } : new[] { rig.mouse })
            {
                bool gaze = p == rig.gaze;
                p.hitDistance = -1; p.overSurface = false; if (!p.active) { p.hoverPanel = null; p.hoverKey = null; continue; }
                // A hand that is at the lens or the timer is there to pick it up, not to press what lies beyond it.
                if (!gaze && (HandAtTool(p) || p.fingerOnly)) { p.hoverPanel = null; p.hoverKey = null; continue; }   // nor does a hand with only its forefinger out: that one touches
                var ray = p.Ray; MLPanel hit = null; MLPanel.Btn btn = null; float best = float.MaxValue;
                foreach (var s in Surfaces())
                {
                    if (p == rig.left && ((s == menu && menuDock.on) || (s == wrist && wristDock.on))) continue;   // the hand that carries a panel does not point at it
                    if (!s.Raycast(ray, out float d, out Vector2 px)) continue;
                    var b = s.ButtonAt(px, reach); if (b == null && IsLabel(s)) continue;   // labels only count where their box is
                    if (b != null && b.key != null && b.key.StartsWith("mark:")) d -= .08f; // a candidate's tag is small and often stands by a station's label: it is taken first
                    if (d >= best) continue;
                    best = d; hit = s; btn = b;
                }
                // Where the pointer has rested for a moment. In the headset a press goes there even if the pinch
                // itself knocked the ray off it.
                string key = btn?.key;
                if (!ReferenceEquals(hit, p.hoverPanel) || key != p.hoverKey) { p.hoverPanel = hit; p.hoverKey = key; p.hoverSince = Time.time; gazeArmed = true; }
                if (btn != null && Time.time - p.hoverSince > .1f) { p.restPanel = hit; p.restKey = key; p.restAt = Time.time; }
                if (gaze)
                {   // looking at a button for a moment presses it, once
                    float dwell = btn != null && !btn.quiet && CanPress(btn) && gazeArmed ? Mathf.Clamp01((Time.time - p.hoverSince) / GazeDwell) : 0;
                    p.down = dwell >= 1; if (p.down) gazeArmed = false;
                    PlaceGazeDot(ray.GetPoint(hit != null ? best : 1.5f), hit != null ? best : 1.5f, dwell);
                }
                else if (p.down && rig.xr && Time.time - p.restAt < .35f && p.restPanel is MLPanel rp && (!ReferenceEquals(hit, rp) || key != p.restKey) && rp.alpha >= .6f)
                {
                    var rb = rp.buttons.Find(x => x.key == p.restKey);
                    if (rb != null) { hit = rp; btn = rb; if (rp.Raycast(ray, out float rd, out _)) best = rd; }
                }
                if (hit != null)
                {
                    p.hitDistance = best; p.overSurface = true;
                    if (btn != null && CanPress(btn))
                    {
                        hovered[hit] = btn;
                        if (p.down)
                        {
                            if (!btn.quiet) { hit.Pressed(); sfx.Play("press"); rig.Haptic(p, .6f, .05f); Log("press " + btn.key + (gaze ? " (gaze)" : p == rig.mouse ? "" : p.controller ? " (controller)" : " (hand)")); }
                            btn.fn();
                        }
                    }
                    continue;
                }
                // A ray that only just misses the strip or the panel was probably meant for a button, not the ground.
                bool nearPanel = false;
                foreach (var s in new[] { hud, wrist }) if (s.Raycast(ray, out _, out _, 70)) nearPanel = true;
                if (nearPanel) continue;
                // The camera is up: a pinch, a trigger or a click that is on no button is the shutter (MLAppPlan.cs), and marks nothing.
                if (camOn && stage == Stage.Field) { if (p.down && !busy && !gaze) Shoot(p); continue; }
                if (CanMark && !busy && PointOnTerrain(ray, out var local, out float dist))
                {
                    var at = world.TransformPoint(local); p.hitDistance = Vector3.Distance(ray.origin, at);
                    if (!reticle) { reticle = true; PlaceReticle(at, p.hitDistance); }
                    if (p.down) TryMark(p, local, dist, world.InverseTransformDirection(ray.direction).normalized);
                }
                else if (CanMark && p.down && !busy) { sfx.Play("deny"); Flash(L("No terrain model in that direction", "その方向には地形モデルがありません")); }
            }
            if (!reticle) HideReticle();
            if (!rig.gaze.active) HideGazeDot();
            TouchPanels();
            foreach (var m in marks) { hovered.TryGetValue(m.label, out var mb); m.label.Hover(mb); }   // the candidates' tags light up under the pointer too
            foreach (var s in panels)
            {
                hovered.TryGetValue(s, out var b);
                if (s.Hover(b) && !b.quiet) { sfx.Play("hover", .5f); foreach (var p in new[] { rig.right, rig.left }) if (p.active && p.hitDistance > 0) rig.Haptic(p, .15f, .015f); }
            }
        }

        // ---------- hooks for checking from the Editor (unity command eval) ----------
        public string State() => JsonUtility.ToJson(new StateDump
        {
            stage = stage.ToString(), here = here, sel = sel, selTask = selTask, taskIndex = taskIndex, selMark = selMark, busy = busy && checkpoint == null, layer = layer, wrist = wristUp, xr = rig.xr,
            pano = panoOpacity, room = roomO, S = view.S, ax = view.ax, az = view.az, yaw = view.yaw, heading = HeadingNow, marks = marks.Count, done = done.OrderBy(i => i).ToArray(),
            buttons = Surfaces().SelectMany(s => s.buttons.Select(b => b.key)).ToArray(), scale = ScaleText, placed = placed, intro = introDone, banner = banner.Show, finished = Finished, preview = preview ?? "",
        });
        [Serializable] class StateDump { public string stage, here, sel, layer, scale, preview; public int selTask, taskIndex, marks, selMark; public bool busy, wrist, xr, placed, intro, banner, finished; public float pano, room, S, ax, az, yaw, heading; public int[] done; public string[] buttons; }

        /// <summary>Press the button with this key on whichever visible surface has it.</summary>
        public string Press(string key)
        {
            if (busy && !(checkpoint != null && key == "HudAction")) return "busy";
            foreach (bool exact in new[] { true, false })
                foreach (var s in Surfaces())
                {
                    if (s.alpha < .6f) continue;
                    var b = exact ? s.buttons.Find(x => x.key == key) : s.buttons.Find(x => x.key != null && x.key.StartsWith(key));
                    if (b != null) { s.Pressed(); sfx.Play("press"); b.fn(); return "pressed " + b.key; }
                }
            return "no button " + key;
        }
        /// <summary>Desktop check: aim the mouse pointer through a viewport point (0..1, y up) and press.</summary>
        public string PressAt(float vx, float vy)
        {
            var ray = rig.cam.ViewportPointToRay(new Vector3(vx, vy, 0));
            if (!CanMark) return "marking not active";
            if (!PointOnTerrain(ray, out var local, out float dist)) return "no terrain";
            int before = marks.Count; lastMarkAt = -10; TryMark(rig.mouse, local, dist, world.InverseTransformDirection(ray.direction).normalized);   // the same way in as a pinch, without the wait between two
            return marks.Count > before ? "marked at " + dist.ToString("0.0") + " m" : "refused: already recorded there";
        }
        public void LookAt(float yaw, float pitch) { rig.lookYaw = yaw; rig.lookPitch = pitch; }
        public string TestHandModels() => rig.TestHandModels();
        /// <summary>Desktop check: aim the pointer through a viewport point as the mouse would, and optionally click. Aim(-1, -1) gives the mouse back.</summary>
        public void Aim(float vx, float vy, bool click = false) { rig.debugAim = vx < 0 ? (Vector2?)null : new Vector2(vx, vy); rig.debugClick = click; if (vx < 0) rig.debugHold = false; }
        /// <summary>Desktop check: hold (or let go of) the pointer's button while it is aimed with Aim, to drag the table's map.</summary>
        public void Hold(bool on) { rig.debugHold = on; }
        /// <summary>The table view, for checks: map point at the table's centre, scale and turn.</summary>
        public string TableState() => $"ax={tv.ax:0.0} az={tv.az:0.0} scale=1:{1 / tv.S:0} yaw={tv.yawRel:0.0} baseY={baseY:0.0} grabs={grabs.Count} framing={(framing != null)}";
        /// <summary>Desktop check: show the headset's comfort vignette, which a desktop never triggers.</summary>
        public bool debugVignette;
        /// <summary>Desktop check: point with the direction of the camera and press by waiting, as the headset does when no hand is tracked.</summary>
        public bool debugGaze;
    }
}
