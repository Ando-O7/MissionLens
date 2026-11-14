using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MissionLens
{
    public enum Stage { Table, Giant, Walk, Field }

    /// <summary>
    /// One Marswalk, two contexts over one state. PLAN: a tabletop terrain model in the room with a briefing
    /// board behind it and the task timeline along its near edge. FIELD: the terrain at true scale and the
    /// real surface image at each station, with a head-up strip and a wrist panel on demand.
    /// The person never moves through the room; the terrain is placed around them.
    /// This file holds the state, the terrain and the transitions; MLAppPanels.cs draws the surfaces.
    /// </summary>
    public partial class MLApp : MonoBehaviour
    {
        public static MLApp I;

        // Where a terrain point sits in the room. ax/az are map metres (x east, z south); w is the room position
        // of that point; yaw turns the terrain so that map north points along room bearing `yaw`.
        struct View { public float S, ex, ax, az, yaw; public bool ground; public Vector3 w; }

        class Mark { public int n; public string station, task; public float x, z, d, az, el; public Vector3 local; public MLPanel label; }
        class LegObj { public int index; public Leg leg; public Material ribbon, wide; public GameObject ribbonGo, wideGo; }
        class Pin { public Station st; public MLPanel label; public Vector3 local; public bool left; public float k = 1; }

        const float SGiant = 1f / 25f, EyeAbovePano = 2.0f;
        static readonly Color MarsSky = new Color(.79f, .65f, .49f), MockRoom = new Color(.137f, .149f, .169f);

        MLData D; Mission M; MLRig rig;
        Transform world, tableFrame, mockRoom;
        Material terrainMat, panoMat; MeshRenderer panoR; Transform panoT; MeshFilter panoF;
        readonly Dictionary<string, Mesh> panoMeshes = new Dictionary<string, Mesh>();
        readonly Dictionary<string, Color> panoSky = new Dictionary<string, Color>();
        readonly List<LegObj> legs = new List<LegObj>();
        readonly List<Pin> pins = new List<Pin>();
        readonly List<Mark> marks = new List<Mark>();
        int markSeq;

        // window of the area shown on the table (map metres, z south) and the table scale
        float winCx, winCz, winHx, winHz, baseY, STable; string scaleText;

        View view;
        Stage stage = Stage.Table;
        string here = "BASE", sel, travelling;
        int taskIndex, selTask;
        readonly HashSet<int> done = new HashSet<int>();
        string layer = "none";
        bool busy, wristUp, placed;
        float panoOpacity, panoTarget, oSlope, oCrism, clipG, roomO = 1, roomTarget = 1;
        Color sky = MarsSky; string pendingPano, shownPano;
        string flashMsg = ""; float flashUntil;

        MTask Task => M.tasks[Mathf.Min(taskIndex, M.tasks.Length - 1)];
        bool Finished => taskIndex >= M.tasks.Length;
        float Pet => M.tasks.Take(taskIndex).Sum(t => t.plannedMin);
        Station St(string id) => D.St(id);
        float HeadingNow => ((rig.HeadYaw - view.yaw) % 360 + 360) % 360;

        void Awake()
        {
            I = this;
            D = MLData.Load(); M = D.M;
            rig = MLRig.Create();
            selTask = 0; sel = M.tasks[0].station;
            BuildWorld();
            BuildPanels();
            view = TableView();
            ApplyView();
            Refresh();
        }

        // ---------- terrain ----------
        void BuildWorld()
        {
            world = new GameObject("World").transform;
            tableFrame = new GameObject("Table frame").transform;

            // The table shows only the part of the area this Marswalk uses: the route's bounding box plus a margin,
            // in the proportions of the model's footprint (0.86 x 0.79 m).
            var pts = new List<Vector2>();
            foreach (var l in M.legs) for (int i = 0; i < l.route.Length; i += 2) pts.Add(new Vector2(l.route[i], l.route[i + 1]));
            foreach (var s in M.stations) pts.Add(new Vector2(s.x, s.z));
            float x0 = pts.Min(p => p.x), x1 = pts.Max(p => p.x), z0 = pts.Min(p => p.y), z1 = pts.Max(p => p.y), ar = .86f / .79f;
            winHx = Mathf.Max((x1 - x0) / 2 + 60, ((z1 - z0) / 2 + 60) * ar); winHz = winHx / ar;
            winCx = Mathf.Clamp((x0 + x1) / 2, -D.sx / 2 + winHx, D.sx / 2 - winHx); winCz = Mathf.Clamp((z0 + z1) / 2, -D.sz / 2 + winHz, D.sz / 2 - winHz);
            float lo = float.MaxValue;
            for (float z = winCz - winHz; z <= winCz + winHz; z += 4) for (float x = winCx - winHx; x <= winCx + winHx; x += 4) lo = Mathf.Min(lo, D.Elev(x, z));
            baseY = lo - 3; STable = .43f / winHx; scaleText = "1 : " + Mathf.RoundToInt(1 / STable / 10) * 10;

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
            terrainMat.SetVector("_Win", new Vector4(winCx, -winCz, winHx, winHz)); terrainMat.SetVector("_Full", new Vector4(D.sx / 2, D.sz / 2, 0, 0));
            Add("Terrain", mesh, terrainMat, world);

            var slab = Add("Slab", MLMesh.Box(new Vector3(winHx * 2, 3, winHz * 2)), ColorMat(new Color(.08f, .09f, .1f, 1), 2450), world);
            slab.transform.localPosition = new Vector3(winCx, baseY + 1.4f, -winCz);

            // Planned route: a 0.5 m ribbon on the ground (true scale) and a 3 m one for the table model.
            for (int li = 0; li < M.legs.Length; li++)
            {
                var leg = M.legs[li]; int n = leg.route.Length / 2; var p = new Vector3[n];
                for (int i = 0; i < n; i++) p[i] = D.Local(leg.route[i * 2], leg.route[i * 2 + 1]);
                var o = new LegObj { index = li, leg = leg, ribbon = ColorMat(MLPanel.Cyan, 3000), wide = ColorMat(Color.white, 3000) };
                o.ribbonGo = Add("Route " + leg.from + "-" + leg.to, MLMesh.Ribbon(p, .25f, .3f), o.ribbon, world).gameObject;
                o.wideGo = Add("Route wide " + leg.from + "-" + leg.to, MLMesh.Ribbon(p, 1.6f, 2.2f), o.wide, world).gameObject;
                legs.Add(o);
            }

            // Surface image: a sphere section around the head, turned with the terrain.
            panoMat = new Material(Resources.Load<Shader>("MLShaders/MLPano"));
            var pg = new GameObject("Surface image"); panoT = pg.transform; panoF = pg.AddComponent<MeshFilter>(); panoR = pg.AddComponent<MeshRenderer>(); panoR.sharedMaterial = panoMat; panoR.enabled = false;

            if (!rig.xr) BuildMockRoom();
        }

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

        // Desktop stand-in for what passthrough shows: a floor and a desk under the model.
        void BuildMockRoom()
        {
            mockRoom = new GameObject("Mock room").transform; mockRoom.SetParent(tableFrame, false);
            var floor = Add("Floor", MLMesh.Box(new Vector3(9, .02f, 9)), ColorMat(new Color(.2f, .215f, .24f, 1), 2400), mockRoom); floor.transform.localPosition = new Vector3(0, -.772f, 0);
            var top = Add("Desk", MLMesh.Box(new Vector3(1.5f, .04f, .95f)), ColorMat(new Color(.42f, .4f, .37f, 1), 2401), mockRoom); top.transform.localPosition = new Vector3(0, -.022f, 0);
            foreach (var (x, z) in new[] { (-.68f, -.42f), (.68f, -.42f), (-.68f, .42f), (.68f, .42f) })
            { var l = Add("Leg", MLMesh.Box(new Vector3(.05f, .72f, .05f)), ColorMat(new Color(.33f, .32f, .29f, 1), 2401), mockRoom); l.transform.localPosition = new Vector3(x, -.402f, z); }
        }

        // ---------- placing the terrain ----------
        View TableView() => new View { S = STable, ex = 1, ax = winCx, az = winCz, ground = false, yaw = tableFrame.eulerAngles.y, w = tableFrame.position };
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
        static IEnumerator Tween(float dur, Action<float> fn)
        {
            for (float t = 0; t < 1;) { t = Mathf.Min(1, t + Time.deltaTime / dur); fn(t); yield return null; }
        }

        /// <summary>Puts the table model in front of the person, at desk height below their eyes.</summary>
        public void PlaceTable()
        {
            var h = rig.Head; var fwd = Vector3.ProjectOnPlane(h.forward, Vector3.up); if (fwd.sqrMagnitude < .01f) fwd = Vector3.forward; fwd.Normalize();
            float y = rig.xr ? Mathf.Max(.35f, h.position.y - .84f) : .762f;
            tableFrame.position = new Vector3(h.position.x, y, h.position.z) + fwd * (rig.xr ? .95f : 1.05f);
            tableFrame.rotation = Quaternion.LookRotation(fwd, Vector3.up);
            if (stage == Stage.Table) { view = TableView(); ApplyView(); }
            placed = true; Refresh();
        }

        // ---------- stage changes ----------
        public void SelectStation(string id)
        {
            sel = id; var t = M.tasks.FirstOrDefault(x => x.station == id && x.index >= taskIndex) ?? M.tasks.FirstOrDefault(x => x.station == id);
            if (t != null) selTask = t.index; Refresh();
        }
        public void SelectTask(int i) { selTask = i; sel = M.tasks[i].station; Refresh(); }

        public void ToTable() { if (!busy) StartCoroutine(CoToTable()); }
        IEnumerator CoToTable()
        {
            busy = true; panoTarget = 0; roomTarget = 1; stage = Stage.Table; selTask = Mathf.Min(taskIndex, M.tasks.Length - 1); sel = M.tasks[selTask].station; Refresh();
            if (!rig.xr) StartCoroutine(Look(2.2f, tableFrame.eulerAngles.y, -30));
            yield return FlyTo(TableView());
            busy = false; Refresh();
        }

        public void EnterTerrain(string id, float S) { if (!busy) StartCoroutine(CoEnterTerrain(id, S)); }
        IEnumerator CoEnterTerrain(string id, float S)
        {
            busy = true; panoTarget = 0; roomTarget = 0; stage = S == 1 ? Stage.Walk : Stage.Giant; sel = id; wristUp = true; Refresh();
            float dur = stage == Stage.Giant ? 2.4f : 3.2f;
            if (!rig.xr) StartCoroutine(Look(dur, rig.lookYaw, S == 1 ? -6 : -14));
            var target = AtStation(id, 0, view.yaw); target.S = S;
            yield return FlyTo(target, dur);
            busy = false; Refresh();
        }

        public void ToField(string id) { if (!busy) StartCoroutine(CoToField(id)); }
        IEnumerator CoToField(string id)
        {
            busy = true; roomTarget = 0; bool fromTable = stage == Stage.Table; sel = here = id; wristUp = false; ShowPano(id); stage = Stage.Field; Refresh();
            float dur = view.S == 1 ? 1.2f : 3.0f;
            // The eye sits 2 m above the ground here, the height of the camera that took the image. Coming from the
            // table you arrive facing the same way as the picture on the briefing board.
            float yaw = fromTable ? rig.HeadYaw - St(id).viewAz : view.yaw;
            if (!rig.xr) StartCoroutine(Look(dur, rig.lookYaw, -6));
            yield return FlyTo(AtStation(id, rig.Head.position.y - EyeAbovePano, yaw), dur);
            panoTarget = 1; busy = false; Refresh();
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

        public void Travel(string to) { if (!busy && to != here) StartCoroutine(CoTravel(to)); }
        IEnumerator CoTravel(string to)
        {
            busy = true; var pts = RouteBetween(here, to); var cum = new float[pts.Count];
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
            busy = false; Refresh();
        }

        public void Rescale(float S) { if (!busy) StartCoroutine(CoRescale(S)); }
        IEnumerator CoRescale(float S)
        {
            busy = true; stage = S == 1 ? Stage.Walk : Stage.Giant; Refresh();
            var target = view; target.S = S; target.ex = 1; target.ground = true; target.w = new Vector3(rig.Head.position.x, 0, rig.Head.position.z);
            yield return FlyTo(target, 1.8f);
            busy = false; Refresh();
        }

        public void ToggleHeight() { if (!busy) StartCoroutine(CoHeight()); }
        IEnumerator CoHeight() { var target = view; target.ex = view.ex > 1 ? 1 : 2; yield return FlyTo(target, .6f); Refresh(); }

        public void SetLayer(string name) { layer = layer == name ? "none" : name; Refresh(); }
        public void CompleteTask() { if (Finished) return; taskIndex++; done.Clear(); Refresh(); }
        public void ToggleCheck(int i) { if (!done.Remove(i)) done.Add(i); Refresh(); }
        public void ToggleWrist() { if (stage != Stage.Table) { wristUp = !wristUp; Refresh(); } }

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
            float x = stage == Stage.Field ? St(here).x : view.ax, z = stage == Stage.Field ? St(here).z : view.az; var t = St(to);
            return (MLData.Bearing(x, z, t.x, t.z), Vector2.Distance(new Vector2(x, z), new Vector2(t.x, t.z)));
        }

        // ---------- surface image ----------
        void ShowPano(string id)
        {
            var st = St(id);
            if (!panoMeshes.TryGetValue(id, out var mesh)) { mesh = MLMesh.PanoSphere(st.panoElMax, st.panoElMin, 400); panoMeshes[id] = mesh; }
            panoF.sharedMesh = mesh; panoMat.SetTexture("_MainTex", Resources.Load<Texture2D>("ML/pano_" + id)); shownPano = id; panoR.enabled = true; sky = MarsSky;
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
            var m = new Mark { n = ++markSeq, station = here, task = Task.phase, x = local.x, z = -local.z, d = dist, local = local + Vector3.up * .15f,
                az = (Mathf.Atan2(dirLocal.x, dirLocal.z) * Mathf.Rad2Deg + 360) % 360, el = Mathf.Asin(Mathf.Clamp(dirLocal.y, -1, 1)) * Mathf.Rad2Deg };
            m.label = MakeMarkLabel(m.n); marks.Add(m);
            if (Task.recordCheck >= 0) done.Add(Task.recordCheck);
            Refresh(); Flash($"Candidate {m.n} marked · {dist:0} m · bearing {MLData.Deg3(m.az)}° · simulated record");
        }
        public void UndoMark()
        {
            int i = marks.FindLastIndex(m => m.station == here); if (i < 0) return;
            Destroy(marks[i].label.go); marks.RemoveAt(i);
            if (!marks.Any(m => m.station == here) && Task.recordCheck >= 0) done.Remove(Task.recordCheck);
            Refresh();
        }
        void Flash(string msg, float seconds = 3.5f) { flashMsg = msg; flashUntil = Time.time + seconds; DrawHud(); }

        // ---------- per frame ----------
        void Update()
        {
            if (!placed && (!rig.xr || rig.Head.position.y > .3f || Time.time > 2)) PlaceTable();
            if (!rig.xr && rig.wristToggle) ToggleWrist();

            float dt = Time.deltaTime;
            roomO = Mathf.MoveTowards(roomO, roomTarget, dt * 1.2f);
            panoOpacity = Mathf.MoveTowards(panoOpacity, panoTarget, dt * 1.4f);
            if (pendingPano != null && panoTarget == 0 && panoOpacity < .02f) { ShowPano(pendingPano); pendingPano = null; }
            string lay = stage == Stage.Table ? layer : "none";
            oSlope = Mathf.MoveTowards(oSlope, lay == "slope" ? 1 : 0, dt * 4); oCrism = Mathf.MoveTowards(oCrism, lay == "crism" ? 1 : 0, dt * 4);
            clipG = Mathf.MoveTowards(clipG, stage == Stage.Table ? 0 : 1, dt * 1.6f);
            terrainMat.SetFloat("_OSlope", oSlope); terrainMat.SetFloat("_OCrism", oCrism); terrainMat.SetFloat("_ClipG", clipG); terrainMat.SetFloat("_Dim", panoOpacity * .85f);
            panoMat.SetFloat("_Opacity", panoOpacity); panoR.enabled = shownPano != null && panoOpacity > .01f;
            panoT.position = rig.Head.position; panoT.rotation = Quaternion.Euler(0, view.yaw, 0);
            rig.SetBackground(roomO > .5f, sky, MockRoom);
            if (mockRoom != null) mockRoom.gameObject.SetActive(roomO > .5f);

            UpdateSurfaces();
            Point();
            if (flashMsg.Length > 0 && Time.time > flashUntil) { flashMsg = ""; DrawHud(); }
        }

        // What each pointer is aimed at, nearest surface first; a press acts on it.
        void Point()
        {
            foreach (var p in rig.xr ? new[] { rig.right, rig.left } : new[] { rig.mouse })
            {
                p.hitDistance = -1; if (!p.active) continue;
                var ray = p.Ray; MLPanel hit = null; MLPanel.Btn btn = null; float best = float.MaxValue;
                foreach (var s in Surfaces())
                {
                    if (!s.Raycast(ray, out float d, out Vector2 px) || d >= best) continue;
                    var b = s.ButtonAt(px); if (s.buttons.Count > 0 && b == null && IsLabel(s)) continue;   // labels only count where their box is
                    best = d; hit = s; btn = b;
                }
                if (hit != null) { p.hitDistance = best; if (p.down && !busy && btn != null) btn.fn(); continue; }
                bool canMark = stage == Stage.Field && !Finished && Task.station == here && Task.recordCheck >= 0;
                if (canMark && PointOnTerrain(ray, out var local, out float dist))
                {
                    p.hitDistance = Vector3.Distance(ray.origin, world.TransformPoint(local));
                    if (p.down && !busy) AddMark(local, dist, world.InverseTransformDirection(ray.direction).normalized);
                }
                else if (canMark && p.down && !busy) Flash("No terrain model in that direction");
            }
        }

        // ---------- hooks for checking from the Editor (unity command eval) ----------
        public string State() => JsonUtility.ToJson(new StateDump
        {
            stage = stage.ToString(), here = here, sel = sel, selTask = selTask, taskIndex = taskIndex, busy = busy, layer = layer, wrist = wristUp, xr = rig.xr,
            pano = panoOpacity, room = roomO, S = view.S, ax = view.ax, az = view.az, yaw = view.yaw, heading = HeadingNow, marks = marks.Count, done = done.OrderBy(i => i).ToArray(),
            buttons = Surfaces().SelectMany(s => s.buttons.Select(b => b.key)).ToArray(), scale = scaleText, placed = placed,
        });
        [Serializable] class StateDump { public string stage, here, sel, layer, scale; public int selTask, taskIndex, marks; public bool busy, wrist, xr, placed; public float pano, room, S, ax, az, yaw, heading; public int[] done; public string[] buttons; }

        /// <summary>Press the button with this key on whichever visible surface has it.</summary>
        public string Press(string key)
        {
            if (busy) return "busy";
            foreach (var s in Surfaces()) { var b = s.Find(key); if (b != null) { b.fn(); return "pressed " + b.key; } }
            return "no button " + key;
        }
        /// <summary>Desktop check: aim the mouse pointer through a viewport point (0..1, y up) and press.</summary>
        public string PressAt(float vx, float vy)
        {
            var ray = rig.cam.ViewportPointToRay(new Vector3(vx, vy, 0)); bool canMark = stage == Stage.Field && !Finished && Task.station == here && Task.recordCheck >= 0;
            if (canMark && PointOnTerrain(ray, out var local, out float dist)) { AddMark(local, dist, world.InverseTransformDirection(ray.direction).normalized); return "marked at " + dist.ToString("0.0") + " m"; }
            return canMark ? "no terrain" : "marking not active";
        }
        public void LookAt(float yaw, float pitch) { rig.lookYaw = yaw; rig.lookPitch = pitch; }
    }
}
