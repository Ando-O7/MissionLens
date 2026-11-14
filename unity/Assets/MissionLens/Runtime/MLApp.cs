using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

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

        class Mark { public int n; public string station, task; public float x, z, d, az, el, born; public Vector3 local; public MLPanel label; }
        class LegObj { public int index; public Leg leg; public Material mat; public GameObject go; public bool back; }
        class Pin
        {
            public Station st; public MLPanel label; public Vector3 local; public bool shown; public float k = 1, routeM; public Transform ringT, leaderT; public Material ring, leader;
            // how the label is drawn and where it sits as seen from the head (radians): set each frame
            public string text = "", sub = ""; public Color col = Color.white; public int side = 1; public float boxW = 640, dist, az, el0, el, lo, hi, hA;
        }

        const float SGiant = 1f / 25f, EyeAbovePano = 2.0f;
        static readonly Color MarsSky = new Color(.79f, .65f, .49f), MockRoom = new Color(.137f, .149f, .169f);

        MLData D; Mission M; MLRig rig; MLAudio sfx;
        Transform world, tableFrame;
        Material terrainMat, panoMat; MeshRenderer terrainR, panoR; Transform panoT; MeshFilter panoF;
        readonly Dictionary<string, Mesh> panoMeshes = new Dictionary<string, Mesh>();
        readonly List<LegObj> legs = new List<LegObj>();
        readonly List<Pin> pins = new List<Pin>();
        readonly List<Mark> marks = new List<Mark>();
        int markSeq;

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
            D = MLData.Load(); M = D.M;
            rig = MLRig.Create(); sfx = new MLAudio(rig.gameObject);
            selTask = 0; sel = M.tasks[0].station;
            BuildWorld();
            BuildPanels();
            BuildFx();
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
            const int step = 2;
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
        IEnumerator FlyTo(View target, float dur = 2.6f)
        {
            var lp = AnchorLocal(target); Vector3 w0 = world.TransformPoint(lp), w1 = target.w;
            float s0 = Mathf.Log(view.S), s1 = Mathf.Log(target.S), e0 = view.ex, y0 = view.yaw;
            yield return Tween(dur, k => { float e = Ease(k); Place(Mathf.Exp(s0 + (s1 - s0) * e), e0 + (target.ex - e0) * e, Mathf.LerpAngle(y0, target.yaw, e), lp, Vector3.Lerp(w0, w1, e)); });
            view = target; ApplyView();
        }
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
        const float EyeAboveTable = .64f, TableAhead = .84f, TableLookPitch = -21;   // the last: where the desktop camera looks at the table

        // ---------- stage changes ----------
        public void SelectStation(string id)
        {
            sel = id; var t = M.tasks.FirstOrDefault(x => x.station == id && x.index >= taskIndex) ?? M.tasks.FirstOrDefault(x => x.station == id);
            if (t != null) selTask = t.index; Refresh();
        }
        public void SelectTask(int i)
        {
            selTask = i; sel = M.tasks[i].station; Refresh();
            if (followTasks && stage == Stage.Table && introDone && !busy) TableTo(FramingFor(M.tasks[i]));   // bring its leg or station onto the table
        }

        public void ToTable() { if (!busy) StartCoroutine(CoToTable()); }
        IEnumerator CoToTable()
        {
            busy = moving = true; HideBanner(); sfx.Play("whoosh", .8f);
            panoTarget = 0; roomTarget = 1; stage = Stage.Table; preview = null; selTask = Mathf.Min(taskIndex, M.tasks.Length - 1); sel = M.tasks[selTask].station; Refresh();
            if (!rig.xr) StartCoroutine(Look(2.2f, tableFrame.eulerAngles.y, TableLookPitch));
            yield return FlyTo(TableView());
            busy = moving = false; Refresh();
            if (Finished) sfx.Play("ready", .7f);
        }

        public void EnterTerrain(string id, float S) { if (!busy) StartCoroutine(CoEnterTerrain(id, S)); }
        IEnumerator CoEnterTerrain(string id, float S)
        {
            busy = moving = true; HideBanner(); sfx.Play("whoosh", .8f);
            panoTarget = 0; roomTarget = 0; stage = S == 1 ? Stage.Walk : Stage.Giant; sel = id; wristUp = true; Refresh();
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
            float yaw = fromTable ? rig.HeadYaw - St(id).viewAz : view.yaw;
            if (!rig.xr) StartCoroutine(Look(dur, rig.lookYaw, -6));
            yield return FlyTo(AtStation(id, rig.Head.position.y - EyeAbovePano, yaw), dur);
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
        IEnumerator CoTravel(string to)
        {
            busy = moving = true; HideBanner(); sfx.Play("travel");
            var pts = RouteBetween(here, to); var cum = new float[pts.Count];
            for (int i = 1; i < pts.Count; i++) cum[i] = cum[i - 1] + Vector2.Distance(pts[i], pts[i - 1]);
            float total = cum[pts.Count - 1], eye = rig.Head.position.y;
            panoTarget = 0; pendingPano = to; stage = Stage.Walk; wristUp = false; travelling = to; Refresh();
            float g0 = view.w.y;
            yield return Tween(.7f, k => { view.w.y = Mathf.Lerp(g0, 0, k); ApplyView(); });
            yield return Tween(Mathf.Clamp(total / 45f, 3, 8), k =>
            {
                float d = Ease(k) * total; int i = 1; while (i < pts.Count - 1 && cum[i] < d) i++;
                float u = (d - cum[i - 1]) / Mathf.Max(1e-6f, cum[i] - cum[i - 1]); var p = Vector2.Lerp(pts[i - 1], pts[i], u);
                view.ax = p.x; view.az = p.y; view.w = new Vector3(rig.Head.position.x, 0, rig.Head.position.z); ApplyView();
                if (!rig.xr && Time.time - rig.lastDragTime > 1.5f)
                {   // desktop: face the way you are walking unless the user is looking around
                    float want = MLData.Bearing(pts[i - 1].x, pts[i - 1].y, pts[i].x, pts[i].y) + view.yaw;
                    rig.lookYaw = Mathf.LerpAngle(rig.lookYaw, want, .06f); rig.lookPitch += (-6 - rig.lookPitch) * .05f;
                }
            });
            float g1 = eye - EyeAbovePano;
            yield return Tween(.6f, k => { view.w.y = Mathf.Lerp(0, g1, k); ApplyView(); });
            here = to; sel = to; stage = Stage.Field; panoTarget = 1; travelling = null;
            if (!Finished && Task.leg >= 0 && Task.station == to) { taskIndex++; done.Clear(); }   // an Approach task ends on arrival
            busy = moving = false; Refresh(); Arrived(to);
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
        IEnumerator CoHeight() { busy = true; tableEx = tableEx > 1 ? 1 : 2; yield return FlyTo(TableView(), .6f); busy = false; Refresh(); }

        public void SetLayer(string name) { layer = layer == name ? "none" : name; sfx.Play("toggle", .7f); Refresh(); }
        public void CompleteTask()
        {
            if (Finished) return;
            taskIndex++; done.Clear(); sfx.Play("done"); Refresh();
            if (!Finished) Flash($"Task complete · T+ {MLData.Hm(Pet)} on the planned timeline", 3); else MissionComplete();
        }
        public void ToggleCheck(int i) { if (!done.Remove(i)) done.Add(i); sfx.Play("toggle"); Refresh(); }
        public void ToggleWrist() { if (!OnTable) { wristUp = !wristUp; sfx.Play("toggle", .6f); wristPlaced = false; Refresh(); } }

        /// <summary>Back to the title with nothing done and nothing recorded.</summary>
        public void Restart()
        {
            if (busy) return;
            StopAllCoroutines(); HideBanner();
            foreach (var m in marks) Destroy(m.label.go);
            marks.Clear(); markSeq = 0; done.Clear(); taskIndex = 0; selTask = 0; sel = M.tasks[0].station; here = "BASE"; layer = "none";
            wristUp = false; travelling = null; pendingPano = null; preview = null; flashMsg = ""; restartArmedUntil = 0;
            panoTarget = 0; roomTarget = 1; introDone = false; frontR = 0; routeDraw = 0; plateK = 0; moving = false;
            foreach (var p in pins) p.shown = false;
            if (framing != null) framing = null;
            tv = tvDefault; tableEx = 1; baseY = BaseFor(tv); grabs.Clear();
            stage = Stage.Title; view = TableView(); ApplyView();
            if (!rig.xr) StartCoroutine(Look(1.2f, tableFrame.eulerAngles.y, -3));
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
            if (!panoMeshes.TryGetValue(id, out var mesh)) { mesh = MLMesh.PanoSphere(st.panoElMax, st.panoElMin, 400, 90); panoMeshes[id] = mesh; }
            panoF.sharedMesh = mesh; panoMat.SetTexture("_MainTex", panoTex.TryGetValue(id, out var tex) && tex != null ? tex : Resources.Load<Texture2D>("ML/pano_" + id));
            panoMat.SetFloat("_VTop", (90 - st.panoElMin) / (st.panoElMax - st.panoElMin));
            var top = st.panoSky != null && st.panoSky.Length == 3 ? new Color(st.panoSky[0], st.panoSky[1], st.panoSky[2]) : MarsSky;
            panoMat.SetColor("_SkyTop", top * .86f);
            shownPano = id; panoR.enabled = true;
        }

        // ---------- records ----------
        // A mark is where the pointing ray first meets the terrain model. Approximate: the model is a 2 m grid.
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
        void AddMark(Vector3 local, float dist, Vector3 dirLocal)
        {
            var m = new Mark { n = ++markSeq, station = here, task = Task.phase, x = local.x, z = -local.z, d = dist, local = local + Vector3.up * .15f, born = Time.time,
                az = (Mathf.Atan2(dirLocal.x, dirLocal.z) * Mathf.Rad2Deg + 360) % 360, el = Mathf.Asin(Mathf.Clamp(dirLocal.y, -1, 1)) * Mathf.Rad2Deg };
            m.label = MakeMarkLabel(m.n); marks.Add(m);
            if (Task.recordCheck >= 0) done.Add(Task.recordCheck);
            sfx.Play("mark"); Ripple(world.TransformPoint(m.local), MLPanel.Amber, Mathf.Clamp(dist * .16f, .3f, 12f));
            Refresh(); Flash($"Candidate {m.n} marked · {dist:0} m · bearing {MLData.Deg3(m.az)}° · simulated record");
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
            // Both hands (or both triggers) held for a moment: bring the table to where you are looking.
            // (Not while the hands are on the model: there, two held pinches are stretching the map.)
            if (rig.xr && stage == Stage.Table && !busy && grabs.Count == 0 && rig.right.HeldFor > 1.2f && rig.left.HeldFor > 1.2f && Time.time - lastPlaced > 2.5f) { PlaceTable(); sfx.Play("ready", .6f); }

            float dt = Time.deltaTime;
            roomO = Mathf.MoveTowards(roomO, roomTarget, dt * .9f);
            panoOpacity = Mathf.MoveTowards(panoOpacity, panoTarget, dt * 1.4f);
            if (pendingPano != null && panoTarget == 0 && panoOpacity < .02f) { ShowPano(pendingPano); pendingPano = null; }
            string lay = stage == Stage.Table ? layer : "none";
            oSlope = Mathf.MoveTowards(oSlope, lay == "slope" ? 1 : 0, dt * 3); oCrism = Mathf.MoveTowards(oCrism, lay == "crism" ? 1 : 0, dt * 3);
            clipG = Mathf.MoveTowards(clipG, OnTable ? 0 : 1, dt * 1.6f);
            // On the table the terrain is cut to the table's patch; leaving it, the cut opens out to the whole area.
            float open = Mathf.Exp(clipG * 8.5f);
            Shader.SetGlobalMatrix("_MLTableInv", tableFrame.worldToLocalMatrix); Shader.SetGlobalVector("_MLTableClip", new Vector4(FootHx * open, FootHz * open, 0, 0));
            terrainMat.SetFloat("_OSlope", oSlope); terrainMat.SetFloat("_OCrism", oCrism); terrainMat.SetFloat("_Dim", panoOpacity);
            terrainMat.SetFloat("_Contour", oSlope);
            terrainMat.SetFloat("_Fog", clipG * (1 - panoOpacity)); terrainMat.SetFloat("_InvS", 1 / world.localScale.x);
            panoMat.SetFloat("_Opacity", panoOpacity); panoR.enabled = shownPano != null && panoOpacity > .01f;
            panoT.position = rig.Head.position; panoT.rotation = Quaternion.Euler(0, view.yaw, 0);
            // What is behind the table: the real room (passthrough) or, if chosen, the plain studio room.
            studioK = Mathf.MoveTowards(studioK, rig.xr && !studio ? 0 : 1, dt * 1.5f);
            float realRoom = roomO * (1 - studioK);
            rig.SetBackground(realRoom > .01f, MockRoom); rig.handPresence = 1 - realRoom;

            // Each of these is kept apart: on a headset this code has not been run on, a fault in one of them
            // should cost that part and be logged once, not stop the frame for the others.
            Guard(0, () => UpdateFx(dt)); Guard(1, () => UpdateSurfaces(dt)); Guard(2, Point); Guard(3, () => UpdateTable(dt));
            if (flashMsg.Length > 0 && Time.time > flashUntil) { flashMsg = ""; DrawHud(); }
        }
        readonly bool[] guardLogged = new bool[4];
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
                var ray = p.Ray; MLPanel hit = null; MLPanel.Btn btn = null; float best = float.MaxValue;
                foreach (var s in Surfaces())
                {
                    if (!s.Raycast(ray, out float d, out Vector2 px) || d >= best) continue;
                    var b = s.ButtonAt(px, reach); if (b == null && IsLabel(s)) continue;   // labels only count where their box is
                    best = d; hit = s; btn = b;
                }
                // Where the pointer has rested for a moment. In the headset a press goes there even if the pinch
                // itself knocked the ray off it.
                string key = btn?.key;
                if (!ReferenceEquals(hit, p.hoverPanel) || key != p.hoverKey) { p.hoverPanel = hit; p.hoverKey = key; p.hoverSince = Time.time; gazeArmed = true; }
                if (btn != null && Time.time - p.hoverSince > .1f) { p.restPanel = hit; p.restKey = key; p.restAt = Time.time; }
                if (gaze)
                {   // looking at a button for a moment presses it, once
                    float dwell = btn != null && !btn.quiet && !busy && gazeArmed ? Mathf.Clamp01((Time.time - p.hoverSince) / GazeDwell) : 0;
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
                    if (btn != null && !busy)
                    {
                        hovered[hit] = btn;
                        if (p.down) { hit.Pressed(); sfx.Play("press"); rig.Haptic(p, .6f, .05f); btn.fn(); }
                    }
                    continue;
                }
                // A ray that only just misses the strip or the panel was probably meant for a button, not the ground.
                bool nearPanel = false;
                foreach (var s in new[] { hud, wrist }) if (s.Raycast(ray, out _, out _, 70)) nearPanel = true;
                if (nearPanel) continue;
                if (CanMark && !busy && PointOnTerrain(ray, out var local, out float dist))
                {
                    var at = world.TransformPoint(local); p.hitDistance = Vector3.Distance(ray.origin, at);
                    if (!reticle) { reticle = true; PlaceReticle(at, p.hitDistance); }
                    if (p.down) { rig.Haptic(p, .8f, .08f); AddMark(local, dist, world.InverseTransformDirection(ray.direction).normalized); }
                }
                else if (CanMark && p.down && !busy) { sfx.Play("deny"); Flash("No terrain model in that direction"); }
            }
            if (!reticle) HideReticle();
            if (!rig.gaze.active) HideGazeDot();
            foreach (var s in panels)
            {
                hovered.TryGetValue(s, out var b);
                if (s.Hover(b) && !b.quiet) { sfx.Play("hover", .5f); foreach (var p in new[] { rig.right, rig.left }) if (p.active && p.hitDistance > 0) rig.Haptic(p, .15f, .015f); }
            }
        }

        // ---------- hooks for checking from the Editor (unity command eval) ----------
        public string State() => JsonUtility.ToJson(new StateDump
        {
            stage = stage.ToString(), here = here, sel = sel, selTask = selTask, taskIndex = taskIndex, busy = busy, layer = layer, wrist = wristUp, xr = rig.xr,
            pano = panoOpacity, room = roomO, S = view.S, ax = view.ax, az = view.az, yaw = view.yaw, heading = HeadingNow, marks = marks.Count, done = done.OrderBy(i => i).ToArray(),
            buttons = Surfaces().SelectMany(s => s.buttons.Select(b => b.key)).ToArray(), scale = ScaleText, placed = placed, intro = introDone, banner = banner.Show, finished = Finished, preview = preview ?? "",
        });
        [Serializable] class StateDump { public string stage, here, sel, layer, scale, preview; public int selTask, taskIndex, marks; public bool busy, wrist, xr, placed, intro, banner, finished; public float pano, room, S, ax, az, yaw, heading; public int[] done; public string[] buttons; }

        /// <summary>Press the button with this key on whichever visible surface has it.</summary>
        public string Press(string key)
        {
            if (busy) return "busy";
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
            if (CanMark && PointOnTerrain(ray, out var local, out float dist)) { AddMark(local, dist, world.InverseTransformDirection(ray.direction).normalized); return "marked at " + dist.ToString("0.0") + " m"; }
            return CanMark ? "no terrain" : "marking not active";
        }
        public void LookAt(float yaw, float pitch) { rig.lookYaw = yaw; rig.lookPitch = pitch; }
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
