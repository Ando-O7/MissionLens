using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static MissionLens.MLPanel;
using static MissionLens.MLText;

namespace MissionLens
{
    // The observation plan of the briefing. One goal: connect the record of an ancient water environment with
    // evidence that could constrain its timing, by planning what to observe at the two sampled outcrops (B and C).
    //
    // PLAN: each outcrop's science card (published rover study, NASA's sampling record) is opened and the outcrop is
    // given the role it serves. FIELD: a camera frames a view of the historical panorama. The view takes its
    // purpose from the task that is selected (it is asked for only when that task has none), and is kept with its
    // station, direction and field of view. From the photo the person chooses what to observe next (a close-up
    // with scale, a wider context image, another viewpoint): each choice adds an observation that is still to be
    // made. Back in PLAN the photos, their purposes and the planned observations of both outcrops stand side by
    // side, the relationship of the two is added, and the plan is saved (MLPlanRecord.cs).
    //
    // Three things are kept apart everywhere: existing evidence (past NASA data that was opened), rehearsal captures
    // (views of a past panorama framed here) and planned observations (not yet collected). Framing a view, choosing
    // a next observation or saving the plan never marks an observation as made. Nothing is measured from the
    // panorama: a capture is cut from the image by direction, from the place the image was taken, and no age,
    // mineral or grain size is read from it. Whatever the plan says was chosen by a button; nothing is filled in.
    public partial class MLApp
    {
        // ---------- vocabulary (the ids are what the saved plan holds) ----------
        const string GoalId = "water_history_and_timing", ScenarioId = "eva01_hawksbill_gap_earth_briefing";
        static string GoalName => L("Water history and timing", "水の歴史と年代");
        static string GoalQuestion => L("What should we observe at both outcrops to connect an ancient water environment with evidence that could constrain its timing?",
                                        "古い水環境の記録と、その年代を制約しうる手掛かりを結び付けるために、2 つの露頭で何を観測するか。");
        static string GoalLine => L("Goal: ", "目的: ") + GoalName + " — " + GoalQuestion;
        static readonly string[] Roles = { "timing_context", "environmental_record" };
        // What a view is for: a label for what the person wants to look into, not something read from the photo.
        static readonly string[] Purposes = { "inspect_grain_texture", "document_outcrop_context", "document_surrounding_layers" };
        // What can be planned from a photo. Each is an observation still to be made.
        static readonly string[] NextTypes = { "close_up_with_scale", "wider_context_image", "additional_viewpoint" };
        static string Word(string id)
        {
            switch (id)
            {
                case "timing_context": return L("Timing context", "年代の手掛かり");
                case "environmental_record": return L("Environmental record", "水環境の記録");
                case "inspect_grain_texture": return L("Inspect grain texture", "粒子の組織を調べる");
                case "document_outcrop_context": return L("Document outcrop context", "露頭の全体像を記録");
                case "document_surrounding_layers": return L("Document surrounding layers", "周囲の層を記録");
                case "close_up_with_scale": return L("Close-up with scale", "接写＋スケール");
                case "wider_context_image": return L("Wider context", "周囲を含む写真");
                case "additional_viewpoint": return L("Another viewpoint", "別の視点");
                case "detail_and_size_at_closeup_scale": return L("detail and size", "細部と大きさ");
                case "position_relative_to_surrounding_layers": return L("position among the layers", "周囲の層との位置関係");
                case "parts_not_seen_from_this_viewpoint": return L("parts not seen from here", "この視点で見えない部分");
                case "geological_relationship_on_site": return L("the relationship on site", "現地での地質関係");
                case "laboratory_analysis_for_timing": return L("laboratory analysis for any age", "年代には室内分析");
                default: return id;
            }
        }
        static string PlanButton(string type) => type == "close_up_with_scale" ? L("Plan close-up with scale", "接写＋スケールを予定") : type == "wider_context_image" ? L("Plan wider context", "周囲を含む写真を予定") : L("Plan another viewpoint", "別の視点を予定");
        // What a planned observation leaves to be verified: it follows from the button that was pressed, nothing more.
        static string Unresolved(string type) => type == "close_up_with_scale" ? "detail_and_size_at_closeup_scale" : type == "wider_context_image" ? "position_relative_to_surrounding_layers" : "parts_not_seen_from_this_viewpoint";
        static readonly string[] PairUnresolved = { "geological_relationship_on_site", "laboratory_analysis_for_timing" };
        // The purpose a task carries: taking a photo while it is selected gives the photo that purpose.
        static string PurposeOfTask(MTask t) => t == null || t.leg >= 0 ? "" : t.phase.StartsWith("Contextual") ? "document_outcrop_context" : t.phase.StartsWith("Candidate") ? "inspect_grain_texture" : t.phase.StartsWith("Presampling") ? "document_surrounding_layers" : "";
        static string TaskId(MTask t) => $"task_{t.index + 1:00}_{t.station}_{t.phase.ToLower().Replace(' ', '_')}";
        // What an opened piece of evidence is called. The id says which; its last part is the station.
        static string EvidenceName(string id) => id.StartsWith("bosak2024") ? L("Published rover study", "ローバー観測の論文") : id.StartsWith("m20_sample_record") ? L("NASA sample record", "NASA の試料記録")
            : id.StartsWith("mcz_panorama") ? L("Historical image", "過去の画像") : id.StartsWith("pia24926") ? L("Existing rover close-up", "既存の接写") : id;
        // The outcrops' science cards: the published interpretation of the rover's observations (Bosak et al. 2024,
        // AGU Advances, doi 10.1029/2024AV001241), in few words. Nothing here was identified from the panorama.
        static string GeoCard(string id) => id == "B"
            ? L("Layered sandstone with diverse grains. These samples may help constrain depositional timing through future laboratory analysis. NASA cores: Swift Run, Skyland.",
                "多様な粒子を含む層状の砂岩。将来の室内分析で、堆積の時期を制約する手掛かりになりうる。NASA のコア: Swift Run、Skyland。")
            : L("Fine-grained, sulfate- and clay-bearing sediment. Investigate the record of an ancient water environment and its preservation potential. NASA cores: Hazeltop, Bearwallow.",
                "細粒で、硫酸塩と粘土を含む堆積物。古い水環境の記録と、その保存の可能性を調べる。NASA のコア: Hazeltop、Bearwallow。");
        static string PanoSource(Station s) => "mcz_" + s.name.ToLower().Replace(' ', '_');

        // ---------- state ----------
        class Next { public string type, createdUtc; }
        class Cap
        {
            public int n; public string id, station, purpose = "", purposeSource = "", sourceTask = "", createdUtc, file = ""; public float az, el, hfov, vfov; public Texture2D tex; public bool missing, needsReview;
            public readonly List<Next> next = new List<Next>();
            public MLPanel tag; public Transform fanT; public Material fanMat;
        }
        readonly Dictionary<string, string> planRole = new Dictionary<string, string>();   // station -> the role it was given
        readonly Dictionary<string, int> planShown = new Dictionary<string, int>();        // station -> which of its photos the plan shows
        readonly List<Cap> caps = new List<Cap>();
        readonly List<string> viewedEvidence = new List<string>(), visitedStations = new List<string>();
        readonly Dictionary<int, LineRenderer> markLines = new Dictionary<int, LineRenderer>();
        // planTab: the board shows the observation plan (the briefing's first face) rather than the Marswalk's tasks
        bool relOn, planTab = true, planDirty, planLoaded, camOn; string planSession, planCreatedUtc, planSavedUtc = "", planSaveError = "", planSavedPath = "", camPick = "", camPickFor = "";
        int capSeq, selCap = -1; Cap shotCap, reviewCap; float? faceAz; float faceEl, lastShotAt = -10, flashAt = -10;
        bool pairView; string fieldAt; int fieldCapSeq;   // pairView: both outcrops' photos are up side by side. fieldAt, fieldCapSeq: where the person stands in FIELD, and the last photo taken before they came there
        MLPanel finder, shot, card, pairTag, refCard; TextMeshProUGUI finderInfo; RawImage finderFlash; string finderShown = "", finderFor = ""; bool shotPlaced;
        Material capMat, pairMat, markLineMat, markLineDim, fanRimMat; LineRenderer pairLine; Mesh fanMesh, fanRim;

        /// <summary>How far in front of the person the photo's card comes up (metres). A recording holds it nearer, so that it can be read in a film without the person moving toward it.</summary>
        public float shotDist = .92f;
        const int CapW = 960, CapH = 640; const float CapFov = 50, PanoFar = 400, FinderDist = 1f, FinderStrip = 136;   // a capture is 3 : 2 and 50 degrees wide: an outcrop with what stands round it
        static float CapVFov => 2 * Mathf.Atan(Mathf.Tan(CapFov * Mathf.Deg2Rad / 2) * CapH / CapW) * Mathf.Rad2Deg;
        IEnumerable<Station> PlanStations => M.stations.Where(s => s.hasSample);
        bool CanCamera => stage == Stage.Field && St(At).hasSample;
        bool InPlan(string station) => planRole.ContainsKey(station) || caps.Any(c => c.station == station);
        int PlannedCount => caps.Sum(c => c.next.Count) + (relOn ? 1 : 0);
        static string UtcNow() => DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
        static string UtcShort(string utc) => utc.Replace("T", " ").Replace("Z", " UTC");

        void BuildPlan()
        {
            // The viewfinder: a frame held in front of the eyes, as wide as the view it takes (CapFov at FinderDist),
            // with the purpose of the view under it.
            float fw = 2 * FinderDist * Mathf.Tan(CapFov * Mathf.Deg2Rad / 2);
            finder = new MLPanel("Viewfinder", CapW, CapH + (int)FinderStrip, fw, rig.xr ? null : rig.Head, true);
            if (!rig.xr) finder.go.transform.localPosition = new Vector3(0, FinderStrip / 2 * fw / CapW, FinderDist);
            // What was just framed, and what to observe next.
            shot = new MLPanel("Photo card", 1040, 520, rig.xr ? .62f : .5f, rig.xr ? null : rig.Head, true); shot.Order(15);
            if (!rig.xr) shot.go.transform.localPosition = new Vector3(0, -.02f, .62f);
            // The saved plan, in front of the person; and an existing rover close-up, shown as the separate record it is.
            card = new MLPanel("Plan card", 1500, 900, rig.xr ? 1.05f : .98f, rig.xr ? null : rig.Head, true); card.Order(25);
            refCard = new MLPanel("Rover close-up", 1000, 760, rig.xr ? .6f : .5f, rig.xr ? null : rig.Head, true); refCard.Order(30);
            if (!rig.xr) { card.go.transform.localPosition = new Vector3(0, .03f, .86f); refCard.go.transform.localPosition = new Vector3(0, 0, .62f); }
            pairTag = new MLPanel("Pair tag", 430, 70, .1f, null, true);
            panels.AddRange(new[] { finder, shot, card, refCard, pairTag });
            foreach (var p in new[] { finder, shot, card, refCard, pairTag }) p.Snap(false);

            pairMat = ColorMat(Amber, 3101, true); markLineMat = ColorMat(new Color(Amber.r, Amber.g, Amber.b, .9f), 3100, true); markLineDim = ColorMat(new Color(Amber.r, Amber.g, Amber.b, .28f), 3100, true);
            pairLine = NewLine("Observation pair", pairMat);
            // a capture's field of view on the map: a sector of unit radius about north, turned to where the view looked
            fanMesh = new Mesh { name = "Capture fan" }; int n = 10; var v = new Vector3[n + 2]; var idx = new int[n * 3]; float half = CapFov * Mathf.Deg2Rad / 2;
            for (int i = 0; i <= n; i++) { float a = -half + 2 * half * i / n; v[i + 1] = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)); }
            for (int i = 0; i < n; i++) { idx[i * 3] = 0; idx[i * 3 + 1] = i + 1; idx[i * 3 + 2] = i + 2; }
            fanMesh.SetVertices(v); fanMesh.SetTriangles(idx, 0); fanMesh.RecalculateBounds();
            // its rim: the two edges and the arc as a narrow band, drawn solid
            fanRim = new Mesh { name = "Capture fan rim" }; var rv = new List<Vector3>(); var ri = new List<int>(); const float rw = .045f;
            void Band(Vector3 a0, Vector3 a1, Vector3 b0, Vector3 b1) { int k = rv.Count; rv.Add(a0); rv.Add(a1); rv.Add(b0); rv.Add(b1); ri.AddRange(new[] { k, k + 1, k + 2, k + 1, k + 3, k + 2 }); }
            for (int i = 0; i < n; i++) Band(v[i + 1] * (1 - rw), v[i + 1], v[i + 2] * (1 - rw), v[i + 2]);
            foreach (int e in new[] { 1, n + 1 }) { var side = Vector3.Cross(Vector3.up, v[e]) * (rw / 2); Band(-side, side, v[e] - side, v[e] + side); }
            fanRim.SetVertices(rv); fanRim.SetTriangles(ri, 0); fanRim.RecalculateBounds();

            NewPlanSession(); DrawPairTag();
            LoadSavedPlan();
        }
        LineRenderer NewLine(string name, Material mat)
        {
            var go = new GameObject(name); var lr = go.AddComponent<LineRenderer>(); lr.sharedMaterial = mat; lr.positionCount = 2; lr.useWorldSpace = true; lr.numCapVertices = 2;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; lr.receiveShadows = false; lr.enabled = false; return lr;
        }
        void NewPlanSession()
        {
            planSession = "plan_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss") + "_" + UnityEngine.Random.Range(0, 65536).ToString("x4");
            planCreatedUtc = UtcNow(); planSavedUtc = ""; planSaveError = ""; planSavedPath = ""; planDirty = false; planLoaded = false;
        }

        // ---------- what the person does to the plan ----------
        void Viewed(string id) { if (!viewedEvidence.Contains(id)) { viewedEvidence.Add(id); Log("evidence opened: " + id); } }
        string[] EvidenceFor(string station) => viewedEvidence.Where(e => e.EndsWith("_" + station)).OrderBy(e => e).ToArray();

        /// <summary>Gives an outcrop the role it serves in the plan, or changes it. Either role can be given to either outcrop.</summary>
        public void PlanAdd(string station, string role)
        {
            planRole[station] = role; planTab = true; planDirty = true; sfx.Play("toggle", .7f); Log($"plan: {station} as {role}"); Refresh();
        }
        /// <summary>Takes an outcrop's role back. Its photos stay until they are discarded themselves.</summary>
        public void PlanRemove(string station)
        {
            if (!planRole.Remove(station)) return;
            if (PlanStations.Any(s => !InPlan(s.id))) relOn = false;
            planDirty = true; sfx.Play("undo"); Log("plan: role of " + station + " removed"); Refresh();
        }
        public void PlanRelationship()
        {
            if (!relOn && PlanStations.Any(s => !InPlan(s.id))) { sfx.Play("deny"); return; }
            relOn = !relOn; planDirty = true; sfx.Play(relOn ? "toggle" : "undo", .7f); Log("plan: relationship " + (relOn ? "added" : "removed")); Refresh();
        }
        public void PlanTab(bool on) { planTab = on; pairView = false; sfx.Play("toggle", .6f); TaskLayer(true); Refresh(); }

        /// <summary>Plans an observation from a photo. Asked for twice, it is still one observation.</summary>
        public void CapNext(int n, string type)
        {
            var c = caps.Find(x => x.n == n); if (c == null) return;
            if (c.next.Any(x => x.type == type)) { sfx.Play("hover"); return; }
            c.next.Add(new Next { type = type, createdUtc = UtcNow() }); planDirty = true; sfx.Play("mark", .6f); Log($"plan: {type} from {c.id} (planned, not collected)");
            DrawCapTag(c); if (shotCap == c) DrawShot(); Refresh();
        }
        /// <summary>Takes a planned observation back out of the plan.</summary>
        public void CapNextUndo(int n, string type)
        {
            var c = caps.Find(x => x.n == n); if (c == null || c.next.RemoveAll(x => x.type == type) == 0) return;
            planDirty = true; sfx.Play("undo"); Log($"plan: {type} from {c.id} removed"); DrawCapTag(c); if (shotCap == c) DrawShot(); Refresh();
        }
        /// <summary>Sets a photo aside to be looked at again. It says nothing about what the photo shows or what to do next.</summary>
        public void CapReview(int n)
        {
            var c = caps.Find(x => x.n == n); if (c == null) return;
            c.needsReview = !c.needsReview; planDirty = true; sfx.Play("toggle", .6f); Log($"{c.id} review later: {c.needsReview}"); DrawCapTag(c); if (shotCap == c) DrawShot(); Refresh();
        }
        /// <summary>Changes what a photo is for (to the next of the short list). The observations planned from it follow.</summary>
        public void CapPurposeCycle(int n)
        {
            var c = caps.Find(x => x.n == n); if (c == null) return;
            c.purpose = Purposes[(Array.IndexOf(Purposes, c.purpose) + 1) % Purposes.Length]; c.purposeSource = "chosen_by_user"; planDirty = true; sfx.Play("toggle", .6f);
            Log($"{c.id} purpose changed to {c.purpose}"); if (shotCap == c) DrawShot(); Refresh();
        }
        public void CapNextToggle(int n, string type) { var c = caps.Find(x => x.n == n); if (c == null) return; if (c.next.Any(x => x.type == type)) CapNextUndo(n, type); else CapNext(n, type); }
        /// <summary>The next of an outcrop's photos, on the plan: however many there are, the plan shows one of them at a time.</summary>
        public void PlanShowNext(string station)
        {
            int k = caps.Count(c => c.station == station); if (k < 2) return;
            bool follow = SelectedCapture(station) != null; planShown[station] = (ShownIndex(station) + 1) % k; if (follow) selCap = ShownCap(station).n;
            sfx.Play("toggle", .5f); foreach (var x in caps) DrawCapTag(x); Refresh();
        }
        // Back at the table from a look ahead at an outcrop: the plan is the face that shows, with the photo taken there
        // last picked, so that it, its purpose and what was planned from it are what the person sees first.
        void PlanCameBack(string station)
        {
            pairView = false; var last = caps.LastOrDefault(c => c.station == station); if (last == null) return;
            planTab = true; selCap = last.n; planShown.Remove(station); foreach (var x in caps) DrawCapTag(x);
        }
        /// <summary>Whether a photo was taken at this station since the person came to stand there.</summary>
        bool PhotosThisStay(string station) => fieldAt == station && caps.Any(c => c.station == station && c.n > fieldCapSeq);
        // An outcrop picked at the table (its row on the plan, or its label on the map) while the plan is the face
        // that shows: its record comes up, that is the photo the plan shows for it. Its evidence is one press away
        // ("Evidence picture"), and comes up by itself where there is no photo yet.
        void PlanPicked(string station)
        {
            pairView = false;
            if (planTab && stage == Stage.Table) { var c = ShownCap(station); if (c != null) selCap = c.n; }
            foreach (var x in caps) DrawCapTag(x);
        }
        /// <summary>Both outcrops' photos side by side, for the row that pairs them. The map shows the two views.</summary>
        public void PlanPairView()
        {
            if (stage != Stage.Table) return;
            pairView = !pairView; if (pairView) selCap = -1; sfx.Play("toggle", .6f); foreach (var x in caps) DrawCapTag(x); Refresh();
        }
        int ShownIndex(string station) { int k = caps.Count(c => c.station == station); return planShown.TryGetValue(station, out int i) && i < k ? i : Mathf.Max(0, k - 1); }
        Cap ShownCap(string station) { var mine = caps.Where(c => c.station == station).ToList(); return mine.Count == 0 ? null : mine[ShownIndex(station)]; }

        /// <summary>An empty plan under a new session. What was saved before stays on disk.</summary>
        public void NewPlan()
        {
            foreach (var c in caps) DestroyCap(c);
            caps.Clear(); planRole.Clear(); planShown.Clear(); viewedEvidence.Clear(); visitedStations.Clear(); relOn = false; pairView = false; fieldCapSeq = 0; capSeq = 0; selCap = -1; shotCap = null; reviewCap = null; camOn = false; camPick = "";
            card.Show = false; refCard.Show = false; planTab = true; NewPlanSession(); Log("plan: new, empty");
            if (stage != Stage.Title) Refresh();
        }

        // ---------- saving and reading back ----------
        PlanRecord BuildRecord()
        {
            var items = new List<PlanItem>(); string Role(string st) => planRole.TryGetValue(st, out var r) ? r : "";
            foreach (var c in caps.OrderBy(c => c.station).ThenBy(c => c.n))
                foreach (var x in c.next)
                    items.Add(new PlanItem { id = $"{x.type}_from_{c.id}", stationId = c.station, roleId = Role(c.station), observationType = x.type, purposeId = c.purpose, status = "planned",
                        originCaptureId = c.id, createdUtc = x.createdUtc, sourceIds = EvidenceFor(c.station), unresolvedIds = new[] { Unresolved(x.type) } });
            var ids = PlanStations.Select(s => s.id).OrderBy(x => x).ToArray();
            var stamp = Resources.Load<TextAsset>("ML/build"); string mission = Resources.Load<TextAsset>("ML/mission").text;
            using var md5 = System.Security.Cryptography.MD5.Create();
            return new PlanRecord
            {
                note = "Simulated example. A rehearsal plan made in the Mission Lens prototype during a hypothetical Earth-based briefing, from historical NASA data. "
                     + "Every planned observation is not yet collected; rehearsal captures are views of past Mastcam-Z panoramas framed by the user, not new observations. "
                     + "Purposes and next observations are the user's button choices; nothing was identified or measured from the images.",
                sessionId = planSession, createdUtc = planCreatedUtc, savedUtc = UtcNow(),
                buildId = Application.isEditor ? "Unity Editor, working tree (not a device build)" : stamp != null ? stamp.text.Trim() : "unknown",
                runtime = Application.platform + " · Unity " + Application.unityVersion + (rig.sim ? " · scripted head and hands (recording)" : rig.xr ? " · headset" : " · desktop"),
                inputHash = "md5:" + BitConverter.ToString(md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(mission))).Replace("-", "").ToLower(),
                goalId = GoalId, scenarioId = ScenarioId, initialPlanItemIds = new string[0], savedPlanItemIds = items.Select(i => i.id).Concat(relOn ? new[] { "relationship_" + string.Join("_", ids) } : new string[0]).ToArray(),
                stationRoles = planRole.OrderBy(p => p.Key).Select(p => new StationRole { stationId = p.Key, roleId = p.Value }).ToArray(),
                planItems = items.ToArray(),
                relationshipPlan = relOn
                    ? new RelationshipPlan { stationIds = ids, purposeId = "document_geological_relationship", status = "planned", sourceIds = viewedEvidence.Where(e => e.StartsWith("bosak2024") || e.StartsWith("mcz_panorama")).OrderBy(e => e).ToArray(), unresolvedIds = PairUnresolved }
                    : new RelationshipPlan { stationIds = new string[0], purposeId = "", status = "not_in_plan", sourceIds = new string[0], unresolvedIds = new string[0] },
                viewedEvidenceIds = viewedEvidence.OrderBy(e => e).ToArray(), visitedStationIds = visitedStations.OrderBy(e => e).ToArray(),
                losAssumptions = new LosAssumptions { eyeHeightM = 2, targetHeightM = 1, toleranceM = .3f, gridDescription = "HiRISE-derived terrain model, approximately 2 m grid; a point target at each station, not the whole outcrop" },
                photoObservations = marks.Select(m => new PhotoObservation { stationId = m.station, photoId = PanoSource(St(m.station)), photoSolRange = St(m.station).panoSols, azDeg = m.az, elDeg = m.el,
                    coordinateType = "direction_from_panorama_centre", reasonId = "observation_region_needs_local_verification", hasApproximateMap = true, approximateMapX = m.x, approximateMapZ = m.z }).ToArray(),
                captures = caps.Select(c => new CaptureRecord { id = c.id, stationId = c.station, panoramaSourceId = PanoSource(St(c.station)), sourceSolRange = St(c.station).panoSols, azDeg = c.az, elDeg = c.el,
                    horizontalFovDeg = c.hfov, verticalFovDeg = c.vfov, projection = "rectilinear, from the panorama's centre", imageRelativePath = c.file, purposeId = c.purpose, purposeSource = c.purposeSource, sourceTaskId = c.sourceTask,
                    planItemId = c.next.Count > 0 ? $"{c.next[0].type}_from_{c.id}" : "", needsReview = c.needsReview, createdUtc = c.createdUtc, kind = "rehearsal_capture" }).ToArray(),
            };
        }

        /// <summary>Writes the plan to disk. Says it is saved only after it has been written and read back.</summary>
        public string SavePlan()
        {
            if (!PlanStations.Any(s => InPlan(s.id))) { sfx.Play("deny"); planSaveError = L("nothing in the plan yet", "計画がまだ空です"); Refresh(); return "nothing to save"; }
            try
            {
                var r = BuildRecord(); planSavedPath = MLPlanStore.Save(r);
                planSavedUtc = r.savedUtc; planSaveError = ""; planDirty = false; planLoaded = false; sfx.Play("done"); Log("plan saved: " + planSavedPath);
                Refresh(); ShowPlanCard(); return "saved " + planSavedPath;
            }
            catch (Exception e)
            {   // nothing is lost: the plan stays as it is in the app, and the board says it was not saved
                planSaveError = e.Message; sfx.Play("deny"); Debug.LogWarning("[ML] plan not saved: " + e.Message); Refresh(); return "not saved: " + e.Message;
            }
        }

        // The plan saved last comes back when the app starts. Its images are read from beside it; one that is missing
        // is cut again from the same panorama by the saved direction and field of view, and is marked as such.
        void LoadSavedPlan()
        {
            var r = MLPlanStore.LoadLatest(); if (r == null || r.captures == null) return;
            try
            {
                planSession = r.sessionId; planCreatedUtc = r.createdUtc; planSavedUtc = r.savedUtc ?? ""; planSavedPath = Path.Combine(MLPlanStore.Dir(r.sessionId), MLPlanStore.FileName);
                foreach (var s in r.stationRoles ?? new StationRole[0]) if (M.stations.Any(x => x.id == s.stationId)) planRole[s.stationId] = s.roleId ?? "";
                if (r.viewedEvidenceIds != null) viewedEvidence.AddRange(r.viewedEvidenceIds); if (r.visitedStationIds != null) visitedStations.AddRange(r.visitedStationIds);
                foreach (var c in r.captures)
                {
                    if (M.stations.All(s => s.id != c.stationId)) continue;
                    var cap = new Cap { n = ++capSeq, id = c.id, station = c.stationId, purpose = c.purposeId ?? "", purposeSource = c.purposeSource ?? "", sourceTask = c.sourceTaskId ?? "", createdUtc = c.createdUtc, file = c.imageRelativePath ?? "",
                        az = c.azDeg, el = c.elDeg, hfov = c.horizontalFovDeg, vfov = c.verticalFovDeg, needsReview = c.needsReview };
                    foreach (var i in r.planItems ?? new PlanItem[0]) if (i.originCaptureId == c.id && NextTypes.Contains(i.observationType)) cap.next.Add(new Next { type = i.observationType, createdUtc = i.createdUtc });
                    string path = cap.file.Length > 0 ? Path.Combine(MLPlanStore.Dir(planSession), cap.file) : null;
                    if (path != null && File.Exists(path)) { cap.tex = new Texture2D(2, 2, TextureFormat.RGB24, false); if (!cap.tex.LoadImage(File.ReadAllBytes(path))) { Destroy(cap.tex); cap.tex = null; } }
                    if (cap.tex == null) { cap.missing = true; cap.tex = RenderCapture(cap.station, cap.az, cap.el, cap.hfov); }
                    MakeCapVisuals(cap); caps.Add(cap);
                }
                relOn = r.relationshipPlan != null && r.relationshipPlan.stationIds != null && r.relationshipPlan.stationIds.Length > 0 && PlanStations.All(s => InPlan(s.id));
                planLoaded = true; planDirty = false;
                Log($"plan read back: {r.sessionId}, {PlannedCount} planned observations, {caps.Count} captures ({caps.Count(c => c.missing)} cut again from the panorama)");
            }
            catch (Exception e) { Debug.LogWarning("[ML] saved observation plan not restored: " + e.Message); }
        }

        // ---------- the camera ----------
        // The task whose purpose a photo takes: the one being worked on in FIELD, or, looking ahead from the briefing,
        // the one selected on the timeline if it is at this station.
        MTask FieldTask => stage != Stage.Field ? null : preview != null ? (M.tasks[selTask].station == At && M.tasks[selTask].leg < 0 ? M.tasks[selTask] : null) : (!Finished && Task.station == here && Task.leg < 0 ? Task : null);
        string CamContext => At + "/" + (FieldTask != null ? FieldTask.index : -1);
        // What the next photo is for: the selected task's purpose, unless another was chosen on the viewfinder for
        // this station and task. "" when neither says: then it has to be chosen before the shutter works.
        string CamPurpose(out MTask from)
        {
            from = null; if (camPick.Length > 0 && camPickFor == CamContext) return camPick;
            string p = PurposeOfTask(FieldTask); if (p.Length > 0) from = FieldTask; return p;
        }
        public void CamPurposePick(string id) { camPick = id; camPickFor = CamContext; finderFor = ""; sfx.Play("toggle", .6f); Log("camera purpose chosen: " + id); Refresh(); }

        // Which direction of the panorama a ray looks at: the ray is carried to where the image is drawn (the floor
        // below the horizon, the far sphere above it, as in MLPano.shader) and the direction is taken from the
        // place the image was taken. The floor is only how the image is shown; no distance is read from it.
        bool PanoAim(Ray ray, out float az, out float el)
        {
            az = el = 0; if (shownPano == null) return false;
            Vector3 c = panoT.position, hit; float floorY = c.y - MastHeight, t = ray.direction.y < -1e-4f ? (floorY - ray.origin.y) / ray.direction.y : -1;
            if (t > 0 && Vector3.Distance(ray.GetPoint(t), c) < PanoFar) hit = ray.GetPoint(t);
            else
            {
                Vector3 o = ray.origin - c; float b = Vector3.Dot(o, ray.direction), q = b * b - (o.sqrMagnitude - PanoFar * PanoFar);
                if (q < 0) return false; hit = ray.GetPoint(-b + Mathf.Sqrt(q));
            }
            var d = panoT.InverseTransformDirection((hit - c).normalized);
            az = (Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg + 360) % 360; el = Mathf.Asin(Mathf.Clamp(d.y, -1, 1)) * Mathf.Rad2Deg; return true;
        }
        // Where a direction of the panorama is drawn in the room.
        Vector3 PanoPoint(float az, float el)
        {
            var d = panoT.TransformDirection(Quaternion.Euler(-el, az, 0) * Vector3.forward);
            return panoT.position + d * (d.y < -1e-4f ? Mathf.Min(MastHeight / -d.y, PanoFar) : PanoFar);
        }

        // The view itself: cut from the station's panorama by direction and field of view (MLCapture.shader).
        Texture2D RenderCapture(string station, float az, float el, float hfov)
        {
            var st = St(station); var src = panoTex.TryGetValue(station, out var t) && t != null ? t : Resources.Load<Texture2D>("ML/pano_" + station);
            if (capMat == null) capMat = new Material(Sh("MLCapture"));
            float th = Mathf.Tan(hfov * Mathf.Deg2Rad / 2);
            capMat.SetTexture("_Mask", Resources.Load<Texture2D>("ML/panomask_" + station));
            capMat.SetFloat("_Az", az); capMat.SetFloat("_El", el); capMat.SetFloat("_TanH", th); capMat.SetFloat("_TanV", th * CapH / CapW); capMat.SetFloat("_ElMin", st.panoElMin); capMat.SetFloat("_ElMax", st.panoElMax);
            var rt = RenderTexture.GetTemporary(CapW, CapH, 0, RenderTextureFormat.ARGB32); var was = RenderTexture.active;
            Graphics.Blit(src, rt, capMat); RenderTexture.active = rt;
            var tex = new Texture2D(CapW, CapH, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, CapW, CapH), 0, 0); tex.Apply();
            RenderTexture.active = was; RenderTexture.ReleaseTemporary(rt); return tex;
        }

        public void ToggleCamera()
        {
            if (!CanCamera || busy) return;
            camOn = !camOn; reviewCap = null; lastShotAt = Time.time; finderFor = ""; sfx.Play("toggle", .7f); Log("camera " + (camOn ? "up" : "down")); Refresh();
            if (camOn && !bannerHasButton) HideBanner();   // the arrival message would stand in the frame
        }
        /// <summary>The shutter, for checks from the Editor (in the app it is a pinch, a trigger or a click while the camera is up).</summary>
        public string ShootNow() { lastShotAt = -10; return Shoot(null); }
        string Shoot(MLRig.Pointer p)
        {
            if (!camOn || !CanCamera || busy || shotCap != null || panoOpacity < .9f) return "camera not ready";
            if (Time.time - lastShotAt < .5f) return "too soon"; lastShotAt = Time.time;
            string purpose = CamPurpose(out var from);
            if (purpose.Length == 0) { sfx.Play("deny"); Flash(L("Choose what this view is for first: the purposes are under the frame", "先に、この構図の目的を選んでください(枠の下)")); return "no purpose chosen"; }
            if (!PanoAim(new Ray(rig.Head.position, rig.Head.forward), out float az, out float el)) { sfx.Play("deny"); return "no image that way"; }
            string station = At; var c = new Cap { n = ++capSeq, station = station, az = az, el = el, hfov = CapFov, vfov = CapVFov, createdUtc = UtcNow(), purpose = purpose,
                purposeSource = from != null ? "inherited_from_selected_task" : "chosen_by_user", sourceTask = FieldTask != null ? TaskId(FieldTask) : "" };
            c.id = "capture_" + c.n.ToString("00");
            c.tex = RenderCapture(station, az, el, CapFov);
            try { string dir = MLPlanStore.Dir(planSession); Directory.CreateDirectory(dir); File.WriteAllBytes(Path.Combine(dir, c.id + ".jpg"), c.tex.EncodeToJPG(90)); c.file = c.id + ".jpg"; }
            catch (Exception e) { c.file = ""; Debug.LogWarning("[ML] capture image not stored (its direction and field of view are kept): " + e.Message); }
            MakeCapVisuals(c); caps.Add(c); planShown.Remove(station); shotCap = c; shotPlaced = false; flashAt = Time.time; planDirty = true;
            if (p != null) rig.Haptic(p, .8f, .08f); sfx.Play("shutter");
            Log($"{c.id} at {station}: az {az:0.0} el {el:0.0} fov {CapFov:0}, purpose {purpose} ({c.purposeSource}), image {(c.file.Length > 0 ? "stored" : "not stored")}");
            DrawShot(); Refresh(); return $"captured {c.id} az {az:0.0} el {el:0.0} purpose {purpose}";
        }
        /// <summary>Closes the photo card. The photo is kept as it is: with or without a next observation.</summary>
        public void ShotDone() { if (shotCap == null) return; Log($"{shotCap.id} kept: {shotCap.next.Count} next observation(s), review later {shotCap.needsReview}"); shotCap = null; sfx.Play("toggle", .6f); Refresh(); }
        public void CapDiscard(int n)
        {
            var c = caps.Find(x => x.n == n); if (c == null) return; if (shotCap == c) shotCap = null; caps.Remove(c); planShown.Remove(c.station);
            try { if (c.file.Length > 0) File.Delete(Path.Combine(MLPlanStore.Dir(planSession), c.file)); } catch (Exception) { }
            DestroyCap(c); if (PlanStations.Any(s => !InPlan(s.id))) relOn = false;
            planDirty = true; sfx.Play("undo"); Log(c.id + " discarded, with what was planned from it"); Refresh();
        }
        void DestroyCap(Cap c)
        {
            if (c.tex != null) Destroy(c.tex); if (c.fanT != null) Destroy(c.fanT.gameObject); if (c.fanMat != null) Destroy(c.fanMat);
            if (c.tag != null) { panels.Remove(c.tag); Destroy(c.tag.go); }
            if (selCap == c.n) selCap = -1; if (reviewCap == c) reviewCap = null;
        }

        /// <summary>A capture picked on the plan or on the map: its picture and its numbers come up on the evidence panel.</summary>
        public void SelectCapture(int n)
        {
            var c = caps.Find(x => x.n == n); if (stage != Stage.Table) return;
            pairView = false; selCap = c == null || selCap == n ? -1 : n; if (selCap >= 0) { sel = c.station; selMark = -1; planShown[c.station] = caps.Where(x => x.station == c.station).ToList().IndexOf(c); }
            sfx.Play("toggle", .6f); foreach (var x in caps) DrawCapTag(x); Refresh();
            if (selCap >= 0 && introDone && !busy && !OnPatch(St(c.station).x, St(c.station).z, .05f)) TableTo(Framing(new[] { new Vector2(St(c.station).x, St(c.station).z) }, 40));
        }
        Cap SelectedCapture(string station) { var c = caps.Find(x => x.n == selCap); return c != null && c.station == station ? c : null; }
        /// <summary>Back into the panorama at a capture's station, facing the way the capture looked, with its frame shown.</summary>
        public void ReviewInField(int n)
        {
            var c = caps.Find(x => x.n == n); if (c == null || busy) return;
            reviewCap = c; camOn = false; faceAz = c.az; faceEl = c.el; ToField(c.station);
        }

        // ---------- drawing: FIELD ----------
        // The frame, with a strip above it (the head-up strip is below): where the frame looks, and what the view is for.
        void DrawFinder(bool review)
        {
            var s = finder; s.Clear(); const float W = CapW, H = CapH, T = FinderStrip; var c = new Color(1, 1, 1, .95f);
            s.Box(1, T + 1, W - 2, H - 2, 6, Color.clear, new Color(1, 1, 1, .32f));
            foreach (var (cx, cy, sx, sy) in new[] { (5f, T + 5f, 1f, 1f), (W - 5f, T + 5f, -1f, 1f), (5f, T + H - 5f, 1f, -1f), (W - 5f, T + H - 5f, -1f, -1f) })
            { s.Stroke(cx, cy, cx + sx * 76, cy, 7, c); s.Stroke(cx, cy, cx, cy + sy * 76, 7, c); }
            s.Stroke(W / 2 - 20, T + H / 2, W / 2 + 20, T + H / 2, 3, c); s.Stroke(W / 2, T + H / 2 - 20, W / 2, T + H / 2 + 20, 3, c);
            finderFlash = s.Picture(Texture2D.whiteTexture, 0, T, W, H, new Rect(0, 0, 1, 1)); finderFlash.color = new Color(1, 1, 1, 0);
            s.Tag(review ? L($"CAPTURE {reviewCap.n:00} · THE SAME FRAMING, FROM ITS SAVED DIRECTION", $"撮影 {reviewCap.n:00} · 保存した方向からの同じ構図") : L("REHEARSAL CAPTURE · HISTORICAL MASTCAM-Z PANORAMA", "模擬の撮影 · 過去の Mastcam-Z パノラマ"), 18, T + H - 50, Amber);
            float top = review ? T - 64 : 0; s.Box(0, top, W, T - 10 - top, 14, new Color(8 / 255f, 10 / 255f, 14 / 255f, .86f));
            finderInfo = s.Text("", 22, T - 28, Size.Label, Ink, true); finderShown = "";
            if (review) { s.Text(L("Purpose: ", "目的: ") + Word(reviewCap.purpose), W - 22, T - 28, Size.Label, Amber, true, 1); return; }
            // the purpose of the view: taken from the selected task, or chosen here; shown before the shutter is pressed
            string purpose = CamPurpose(out var from);
            s.Text(purpose.Length > 0 ? (rig.xr ? L("pinch to capture", "ピンチで撮影") : L("click to capture", "クリックで撮影")) : L("choose a purpose to capture", "目的を選ぶと撮影できます"), W - 22, T - 28, Size.Label, Amber, true, 1);
            s.Text(L("PURPOSE", "目的"), 22, 36, Size.Note, purpose.Length > 0 ? Gray : Amber, true);
            s.Text(from != null ? L("from the task", "タスクから") : purpose.Length > 0 ? L("chosen here", "ここで選択") : L("choose one", "選んでください"), 22, 58, Size.Note * .8f, from != null ? Soft : Amber);
            float x = 150, bw = (W - 150 - 22 - 16) / 3;
            foreach (var id in Purposes) { string pid = id; s.Button(Word(id), x, 10, bw, 54, () => CamPurposePick(pid), purpose == id, px: Size.Note * .8f, key: "CamPurpose:" + id); x += bw + 8; }
        }

        // The photo just taken: what it is, what it is for (taken from the task; it can be changed), and what to
        // observe next. Each next observation is one that is still to be made; none is needed to keep the photo.
        void DrawShot()
        {
            var s = shot; s.Clear(); var c = shotCap; if (c == null) return; var st = St(c.station); int n = c.n;
            s.Frame(new Color(10 / 255f, 12 / 255f, 17 / 255f, .96f), Amber);
            const float tx = 30, ty = 34, tw = 420, th = 280, X = 480, W = 530;
            s.Picture(c.tex, tx, ty, tw, th, new Rect(0, 0, 1, 1)); s.Box(tx, ty, tw, th, 6, Color.clear, new Color(1, 1, 1, .3f));
            s.Text(L($"REHEARSAL CAPTURE {c.n:00}", $"模擬の撮影 {c.n:00}"), tx, ty + th + 34, Size.Note, Amber, true);
            s.Text(L($"Historical Mastcam-Z panorama · {st.panoSols}", $"過去の Mastcam-Z パノラマ · {st.panoSols}"), tx, ty + th + 62, Size.Note, Gray);
            s.Text(L($"az {MLData.Deg3(c.az)}° · el {c.el:+0;-0;0}° · {c.hfov:0}° wide, from its centre", $"方位 {MLData.Deg3(c.az)}° · 仰角 {c.el:+0;-0;0}° · 画角 {c.hfov:0}°(撮影位置から)"), tx, ty + th + 88, Size.Note, Gray);
            s.Text($"{st.id} {st.name}", X, 66, Size.Heading, Ink, true);
            string pl = L("Purpose", "目的"); s.Text(pl, X, 112, Size.Label, Gray, true); s.Text(Word(c.purpose), X + s.Measure(pl, Size.Label, true) + 14, 112, Size.Label, Ink, true);
            s.Button(L("Change", "変更"), X + W - 116, 122, 116, 38, () => CapPurposeCycle(n), px: Size.Note, key: "Change purpose");
            s.Text(c.purposeSource == "inherited_from_selected_task" ? L("taken from the selected task", "選択中のタスクから引き継ぎ") : L("chosen by you", "あなたが選択"), X, 138, Size.Note, c.purposeSource == "inherited_from_selected_task" ? Soft : Gray);
            string nl = L("NEXT OBSERVATION", "次の観測"); s.Text(nl, X, 186, Size.Label, Ink, true); s.Text(L("· adds work not yet done", "· 未実施の作業を追加"), X + s.Measure(nl, Size.Label, true) + 12, 186, Size.Note, Gray);
            float y = 200;
            foreach (var type in NextTypes)
            {
                string t = type; bool added = c.next.Any(x => x.type == type);
                s.Button(added ? L("Added to plan · ", "計画に追加済み · ") + Word(type) : PlanButton(type), X, y, W, 50, () => CapNext(n, t), added, primary: !added, px: Size.Note, key: "Next:" + type); y += 58;
            }
            float by = 400; string sid = c.station;
            s.Button(c.needsReview ? L("Review later · on hold", "あとで確認 · 保留中") : L("Review later", "あとで確認"), tx, by + 34, 250, 52, () => CapReview(n), c.needsReview, px: Size.Note, key: "Review later");
            if (WatsonTex(sid) != null) s.Button(L("Rover close-up", "ローバーの接写"), tx + 262, by + 34, 158, 52, () => ShowWatson(sid), px: Size.Note, key: "Rover close-up");
            s.Button(L("Done ›", "完了 ›"), X, by - 18, W - 130, 60, ShotDone, primary: true, px: Size.Label, key: "Done");
            s.Button(L("Discard", "破棄"), X + W - 118, by - 18, 118, 60, () => CapDiscard(n), px: Size.Note, key: "Discard");
            s.Wrap(L("A view you framed in a past panorama: not a new observation, and nothing is measured from it.", "過去のパノラマの中で決めた構図です。新しい観測ではなく、ここから何かを測るものでもありません。"), X, by + 70, W, 24, Size.Note * .9f, Dim);
        }

        // An existing rover close-up (WATSON, on the rover's arm): another instrument on another sol, shown as the
        // separate record it is. It is never made from the panorama, and opening it collects nothing.
        static Texture2D WatsonTex(string station) => Resources.Load<Texture2D>("ML/watson_" + station);
        public void ShowWatson(string station)
        {
            var tex = WatsonTex(station); if (tex == null) return; var s = refCard; s.Clear(); Viewed("pia24926_watson_" + station);
            s.Frame(new Color(10 / 255f, 12 / 255f, 17 / 255f, .97f), Ink);
            s.Text(L("EXISTING ROVER CLOSE-UP · NASA OBSERVATION", "既存のローバー接写 · NASA の観測"), 40, 66, Size.Label, Ink, true);
            float ih = 470, iw = Mathf.Min(920, ih * tex.width / tex.height); s.Picture(tex, 40 + (920 - iw) / 2, 90, iw, ih, new Rect(0, 0, 1, 1));
            s.Wrap(station == "B" ? L("WATSON, sol 482: the abrasion patch Thornton Gap at Skinner Ridge (PIA24926, left).", "WATSON、sol 482: Skinner Ridge の研磨面 Thornton Gap(PIA24926 の左)。")
                                  : L("WATSON, sol 504: the abrasion patch Berry Hollow at Wildcat Ridge (PIA24926, right).", "WATSON、sol 504: Wildcat Ridge の研磨面 Berry Hollow(PIA24926 の右)。"), 40, 604, 920, 34, Size.Body, Ink);
            s.Wrap(L("Credit: NASA/JPL-Caltech/ASU/MSSS. Another instrument, on another sol. It is not a zoom into this panorama, and a close-up planned here stays not yet collected.",
                     "クレジット: NASA/JPL-Caltech/ASU/MSSS。別の機器が別の sol に撮った画像です。このパノラマの拡大ではなく、ここで予定した接写は未取得のままです。"), 40, 644, 760, 27, Size.Note, Gray);
            s.Button(L("Close", "閉じる"), 820, 680, 140, 52, () => { refCard.Show = false; sfx.Play("toggle", .6f); }, key: "Close close-up");
            if (rig.xr) PlaceInFront(s, .95f, 0); s.Show = true; sfx.Play("toggle", .7f); if (stage == Stage.Table) Refresh();
        }
        void PlaceInFront(MLPanel p, float dist, float drop)
        {
            var yaw = Quaternion.Euler(0, rig.HeadYaw, 0); var at = rig.Head.position + yaw * new Vector3(0, drop, dist);
            p.go.transform.SetPositionAndRotation(at, Quaternion.LookRotation(at - rig.Head.position, Vector3.up));
        }

        // ---------- drawing: PLAN, the board's observation plan ----------
        void DrawPlanTabs(MLPanel b, float x)
        {
            b.Button(L("Tasks", "作業"), x, 30, 150, 46, () => PlanTab(false), !planTab, px: Size.Note, key: "Tab:tasks");
            b.Button(L($"Observation plan · {PlannedCount}", $"観測計画 · {PlannedCount}"), x + 162, 30, 300, 46, () => PlanTab(true), planTab, px: Size.Note, key: "Tab:plan");
        }

        void DrawPlanBoard(MLPanel b)
        {
            const float X = 48, W = 990; string goal = L("GOAL", "目的");
            b.Text(goal, X, 120, Size.Label, Gray, true); b.Text(GoalName, X + b.Measure(goal, Size.Label, true) + 16, 122, Size.Heading, Ink, true);
            b.Text(L("SIMULATED PLAN", "模擬の計画"), X + W, 120, Size.Note, Amber, true, 1);
            float y = b.Wrap(GoalQuestion, X, 156, W, 27, Size.Note, Soft);
            // what to do, in one line: the plan is made of views, each kept with what it is for and what it leaves to be checked
            b.Text(L("Frame what you need to document. Keep its purpose and what remains to be checked.", "記録が必要なものを構図に収める。目的と、まだ確かめていないことを一緒に残す。"), X, y + 2, Size.Note, Soft);
            y = 232;
            foreach (var s in PlanStations) { DrawPlanStation(b, s, X, y, W, 190); y += 198; }
            DrawPlanPair(b, X, y, W, 92); y += 102;
            // saving: the board says "saved" only when the plan has been written and read back
            bool empty = !PlanStations.Any(s => InPlan(s.id));
            b.Button(L("Save observation plan", "観測計画を保存"), X, y, 330, 62, () => SavePlan(), dim: empty, primary: !empty, px: Size.Label, key: "Save plan");
            string line1, line2 = ""; var col = Gray;
            if (planSaveError.Length > 0) { line1 = L("NOT SAVED: ", "保存できません: ") + planSaveError; line2 = L("Nothing was written. The plan is kept here.", "何も書き込まれていません。計画は残っています。"); col = Slope20; }
            else if (empty) line1 = L("Nothing in the plan yet.", "計画はまだ空です。");
            else if (planDirty || planSavedUtc.Length == 0) { line1 = L($"Not saved yet · {PlannedCount} planned", $"未保存 · 予定の観測 {PlannedCount} 件"); col = Amber; if (planSavedUtc.Length > 0) line2 = L("changed since ", "変更あり · 前回の保存 ") + UtcShort(planSavedUtc); }
            else if (planLoaded) { line1 = L("Read back from the saved file", "保存ファイルから読み戻し"); line2 = L("saved ", "保存 ") + UtcShort(planSavedUtc); col = Soft; }
            else { line1 = L("Saved · ", "保存済み · ") + UtcShort(planSavedUtc); line2 = planSession; col = Soft; }
            if (line1.Length > 36) line1 = line1.Substring(0, 35) + "…";
            b.Text(line1, X + 348, y + 26, Size.Note, col, true); if (line2.Length > 0) b.Text(line2, X + 348, y + 52, Size.Note * .9f, Gray);
            if (planSavedUtc.Length > 0 && !planDirty) b.Button(L("Plan card", "計画カード"), X + W - 272, y + 8, 130, 46, ShowPlanCard, px: Size.Note, key: "Plan card");
            b.Button(L("New plan", "新しい計画"), X + W - 130, y + 8, 130, 46, NewPlan, dim: empty, px: Size.Note, key: "New plan");
            b.Wrap(L("Existing evidence: past NASA data you opened. Planned: not yet collected. Rehearsal capture: a view you framed in a past panorama. Ages need laboratory analysis and a justified geological relationship.",
                     "既存の根拠: 開いた過去の NASA データ。予定: 未取得。模擬の撮影: 過去のパノラマの中で決めた構図。年代の決定には、室内分析と根拠のある地質関係が必要です。"), X, y + 88, W, 23, Size.Note * .85f, Dim);
        }

        // One outcrop of the plan, to be read at a glance: the role it was given, how much has been recorded there, and
        // for the photo shown, what it is for, what was planned from it and what that leaves to verify. Nothing is
        // changed here. Picking the photo opens it on the right-hand panel, where its purpose and next observations
        // are changed; the outcrop's role is given on its science card. However many photos there are, the row stays one row.
        void DrawPlanStation(MLPanel b, Station s, float x, float y0, float w, float h)
        {
            bool has = planRole.TryGetValue(s.id, out string role), inPlan = InPlan(s.id); var mine = caps.Where(c => c.station == s.id).ToList(); var cap = ShownCap(s.id); string sid = s.id; float rr = x + w - 18;
            b.Box(x, y0, w, h, 16, new Color(1, 1, 1, inPlan ? .055f : .02f), new Color(1, 1, 1, sid == sel && stage == Stage.Table ? .36f : inPlan ? .17f : .09f));
            string name = $"{s.id} {s.name}"; b.Text(name, x + 18, y0 + 44, Size.Heading, inPlan ? Ink : Gray, true);
            if (inPlan) b.Text(has ? " — " + Word(role) : L(" — role not chosen", " — 役割は未選択"), x + 18 + b.Measure(name, Size.Heading, true), y0 + 44, has ? Size.Heading : Size.Label, has ? Soft : Amber, true);
            b.Hit("PlanRow:" + sid, x, y0, w, 56, () => { sfx.Play("toggle", .6f); SelectStation(sid); }, 16, true);   // the row's head picks the outcrop: its science card comes up on the right
            if (!inPlan)
            {
                b.Wrap(L($"Not in the plan. Pick {s.id} on the map, or a task there: its science card on the right gives it the role it serves. Then frame a view of it in FIELD.", $"計画に入っていません。地図で {s.id}(またはそこでの作業)を選ぶと、右の科学カードで役割を選べます。そのあと FIELD で構図を決めて撮影します。"), x + 18, y0 + 100, w - 36, 31, Size.Label, Gray);
                return;
            }
            int planned = mine.Sum(c => c.next.Count), hold = mine.Count(c => c.needsReview);
            b.Text(L($"{mine.Count} photo{(mine.Count == 1 ? "" : "s")} · {planned} planned{(hold > 0 ? $" · {hold} on hold" : "")}", $"写真 {mine.Count} · 予定 {planned}{(hold > 0 ? $" · 保留 {hold}" : "")}"), rr, y0 + 42, Size.Note, hold > 0 ? Amber : Gray, true, 1);
            const float tw = 162, th = 108; float tx = x + 18, ty = y0 + 66, lx = tx + tw + 22, vx = lx + 110;
            b.Box(tx, ty, tw, th, 8, new Color(.1f, .12f, .14f, 1), new Color(1, 1, 1, .14f));
            if (cap == null)
            {
                b.Text(L("no view framed", "構図なし"), tx + tw / 2, ty + th / 2 + 7, Size.Note, Dim, false, 0);
                b.Text(L("No photo yet.", "写真はまだありません。"), lx, y0 + 92, Size.Label, Soft);
                b.Text(L("Select a task at this station for the purpose, then frame a view in FIELD.", "この地点の作業を選ぶと目的が決まります。FIELD で構図を決めて撮影します。"), lx, y0 + 122, Size.Note, Gray);
                b.Button(L($"Frame a view at {sid} ›", $"{sid} で構図を決める ›"), lx, y0 + 136, 300, 42, () => ToField(sid), primary: true, px: Size.Note, key: "Frame at " + sid);
                return;
            }
            int n = cap.n; b.Picture(cap.tex, tx, ty, tw, th, new Rect(0, 0, 1, 1)); b.Hit("cap:" + n, tx, ty, tw, th, () => SelectCapture(n), 8);
            if (selCap == n) b.Box(tx - 3, ty - 3, tw + 6, th + 6, 10, Color.clear, Amber);
            if (mine.Count > 1) b.Button(L($"photo {mine.IndexOf(cap) + 1} of {mine.Count} ›", $"写真 {mine.IndexOf(cap) + 1} / {mine.Count} ›"), rr - 190, y0 + 62, 190, 34, () => PlanShowNext(sid), px: Size.Note * .9f, key: "NextPhoto:" + sid);
            b.Text(L("Purpose", "目的"), lx, y0 + 90, Size.Note, Gray, true); b.Text(Word(cap.purpose), vx, y0 + 90, Size.Label, Ink, true);
            b.Text(L("Next", "次の観測"), lx, y0 + 122, Size.Note, Gray, true);
            b.Text(cap.next.Count > 0 ? string.Join(" + ", cap.next.Select(nx => Word(nx.type))) + L(" — planned, not yet collected", " — 予定、未取得") : L("none chosen — photo only", "未選択 — 写真のみ"), vx, y0 + 122, Size.Note, cap.next.Count > 0 ? Ink : Soft, cap.next.Count > 0);
            b.Text(L("To verify", "未確認"), lx, y0 + 150, Size.Note, Gray, true);
            b.Text(cap.next.Count > 0 ? string.Join(" · ", cap.next.Select(nx => Word(Unresolved(nx.type)))) : "—", vx, y0 + 150, Size.Note, Soft);
            if (cap.needsReview) b.Text(L("REVIEW LATER · ON HOLD", "あとで確認 · 保留中"), rr, y0 + 150, Size.Note, Amber, true, 1);
            else if (cap.missing) b.Text(L("image cut again from the panorama", "画像はパノラマから再作成"), rr, y0 + 150, Size.Note * .9f, Amber, false, 1);
            // what was opened of the existing evidence: never more than that
            var ev = EvidenceFor(sid).Select(EvidenceName).Distinct().ToArray(); string evl = L("EXISTING EVIDENCE OPENED", "開いた既存の根拠");
            b.Text(evl, lx, y0 + 177, Size.Note * .8f, Gray, true); b.Text(ev.Length > 0 ? string.Join(" · ", ev) : L("none yet", "まだありません"), lx + b.Measure(evl, Size.Note * .8f, true) + 12, y0 + 177, Size.Note * .8f, Gray);
        }

        void DrawPlanPair(MLPanel b, float x, float y0, float w, float h)
        {
            bool both = PlanStations.All(s => InPlan(s.id)); string ids = string.Join(" + ", PlanStations.Select(s => s.id)), head = L($"{ids} TOGETHER", $"{ids} を合わせて");
            b.Box(x, y0, w, h, 16, new Color(1, 1, 1, relOn ? .055f : .02f), new Color(1, 1, 1, pairView ? .36f : relOn ? .17f : .09f));
            b.Text(head, x + 18, y0 + 34, Size.Label, relOn || pairView ? Ink : Gray, true);
            if (both) b.Button(pairView ? L("Comparing", "比較中") : L("Compare photos ›", "写真を比べる ›"), x + w - 18 - 190, y0 + 8, 190, 34, PlanPairView, pairView, px: Size.Note * .9f, key: "PlanRow:pair");   // puts the two outcrops' photos side by side on the right
            b.Text(L("Observation pair · not a mapped contact, and not the same layer", "観測の組 · 地質境界でも、同じ層を示す線でもありません"), x + 18 + b.Measure(head, Size.Label, true) + 16, y0 + 34, Size.Note, Gray);
            float rr = x + w - 18;
            if (!both) { b.Text(L("With both outcrops in the plan, documenting their relationship can be added.", "両方の露頭が計画に入ると、関係の記録を追加できます。"), x + 18, y0 + 68, Size.Note, Gray); return; }
            if (!relOn) { b.Button(L("Add: document the geological relationship", "追加: 地質学的な関係を記録する"), x + 18, y0 + 44, 540, 42, PlanRelationship, primary: true, px: Size.Note, key: "Relationship"); return; }
            string rl = L("Document the geological relationship", "地質学的な関係を記録する"); b.Text(rl, x + 18, y0 + 62, Size.Note, Ink, true);
            b.Text(L(" · planned — not yet collected", " · 予定 — 未取得"), x + 18 + b.Measure(rl, Size.Note, true), y0 + 62, Size.Note, Ink, true);
            b.Text(L("To verify: ", "未確認: ") + string.Join(" · ", PairUnresolved.Select(Word)) + L(" · not from height alone", " · 標高だけでは決めない"), x + 18, y0 + 84, Size.Note * .9f, Soft);
            b.Button(L("Remove", "外す"), rr - 110, y0 + 44, 110, 38, PlanRelationship, px: Size.Note, key: "Relationship");
        }

        // The right-hand panel for an outcrop of the plan: what the published study says of it, and the way to add
        // it with the role it serves. Returns the baseline for whatever comes next.
        float DrawScience(MLPanel b, Station st, float x, float y, float w)
        {
            if (introDone && stage == Stage.Table) { Viewed("bosak2024_" + st.id); Viewed("m20_sample_record_" + st.id); }   // it is on the panel in front of the person now
            b.Text(L("PUBLISHED ROVER STUDY · BOSAK ET AL. 2024", "ローバー観測の論文 · BOSAK ほか 2024"), x, y, Size.Label, Ink, true);
            // where NASA took a close-up of this outcrop, it can be opened: as the separate, existing record it is
            string wid = st.id; if (WatsonTex(wid) != null) b.Button(L("Rover close-up ›", "ローバーの接写 ›"), x + w - 200, y - 28, 200, 38, () => ShowWatson(wid), px: Size.Note, key: "Rover close-up");
            else b.Text(L("EXISTING EVIDENCE", "既存の根拠"), x + w, y, Size.Note, Gray, true, 1);
            y = b.Wrap(GeoCard(st.id), x, y + 38, w, 35, Size.Body, Ink);
            y = b.Wrap(L("Published rover interpretation; not identified from this panorama.", "公表されたローバー観測の解釈。このパノラマから同定したものではない。"), x, y - 4, w, 27, Size.Note, Gray);
            y = b.Sourced(L("REGIONAL SPECTRAL CONTEXT", "周辺の分光データ"), Ink, L($"MRO CRISM · {M.crismMPerPx:0} m/pixel. Not a ranking of individual outcrops.", $"MRO CRISM · 1 画素 {M.crismMPerPx:0} m。個々の露頭の順位ではありません。"), x, y + 4, w);
            // adding it to the plan: the role is chosen with it
            bool has = planRole.TryGetValue(st.id, out string role); string sid = st.id, al = has ? L("IN THE PLAN AS", "計画での役割") : L("ADD TO THE PLAN AS", "計画に追加:"); float ay = y - 6;
            b.Text(al, x, ay + 33, Size.Note, has ? Gray : Ink, true); float ax = x + Mathf.Max(250, b.Measure(al, Size.Note, true) + 16);
            foreach (var r in Roles) { string rid = r; b.Button(Word(r), ax, ay, 262, 50, () => PlanAdd(sid, rid), has && role == r, primary: !has, px: Size.Note, key: "Add:" + r); ax += 272; }
            if (has) b.Button(L("Remove", "外す"), x + w - 110, ay + 6, 110, 38, () => PlanRemove(sid), px: Size.Note, key: "Remove from plan");
            return ay + 50;
        }

        // A photo picked on the plan or on the map, in the place of the station's picture: the view itself, what it is
        // for and what is planned from it. This is where a photo's purpose and its next observations are changed, where
        // it is set aside or discarded. The outcrop's evidence stays under it; "Evidence picture" gives the picture back.
        void DrawCaptureReview(MLPanel b, Cap c, float x, float y, float w, float h)
        {
            var st = St(c.station); float iw = h * CapW / CapH, tx = x + iw + 24, tw = w - iw - 40; int n = c.n;
            b.Picture(c.tex, x, y, iw, h, new Rect(0, 0, 1, 1));
            b.Text(L($"REHEARSAL CAPTURE {c.n:00}", $"模擬の撮影 {c.n:00}"), tx, y + 32, Size.Note, Amber, true);
            b.Text(L($"az {MLData.Deg3(c.az)}° · el {c.el:+0;-0;0}° · {c.hfov:0}° × {c.vfov:0}°", $"方位 {MLData.Deg3(c.az)}° · 仰角 {c.el:+0;-0;0}° · {c.hfov:0}° × {c.vfov:0}°"), x + w - 14, y + 32, Size.Note * .9f, Gray, false, 1);
            b.Text(c.missing ? L("image file missing: cut again from the same panorama by the saved direction", "画像ファイルが無いため、保存した方向から同じパノラマを切り出し直しました")
                             : L($"Mastcam-Z panorama · {st.panoSols} · past NASA data, not a new observation", $"Mastcam-Z パノラマ · {st.panoSols} · 過去の NASA データ。新しい観測ではありません"), tx, y + 56, Size.Note * .85f, c.missing ? Amber : Gray);
            b.Text(L("Purpose", "目的"), tx, y + 94, Size.Note, Gray, true);
            b.Button(Word(c.purpose) + " ›", tx + 100, y + 68, 340, 38, () => CapPurposeCycle(n), px: Size.Note, key: "PurposeCycle");
            b.Text(c.purposeSource == "inherited_from_selected_task" ? L("from the task", "タスクから") : L("chosen", "選択"), tx + 452, y + 94, Size.Note * .85f, c.purposeSource == "inherited_from_selected_task" ? Soft : Gray);
            b.Text(L("Next", "次の観測"), tx, y + 136, Size.Note, Gray, true); float bx = tx + 100, bw = (tw - 100 - 16) / 3;
            foreach (var type in NextTypes) { string t = type; b.Button(Word(type), bx, y + 110, bw, 38, () => CapNextToggle(n, t), c.next.Any(v => v.type == type), px: Size.Note * .75f, key: "Detail:Next:" + type); bx += bw + 8; }
            b.Text(c.next.Count > 0 ? L("Planned — not yet collected", "予定 — 未取得") : L("Photo only — no next observation chosen", "写真のみ — 次の観測は未選択"), tx, y + 172, Size.Note, c.next.Count > 0 ? Ink : Soft, true);
            if (c.next.Count > 0) b.Text(L("To verify: ", "未確認: ") + string.Join(" · ", c.next.Select(v => Word(Unresolved(v.type)))), tx, y + 194, Size.Note * .85f, Soft);
            float by = y + h - 42;
            b.Button(c.needsReview ? L("On hold · review later", "保留中 · あとで確認") : L("Review later", "あとで確認"), tx, by, 230, 36, () => CapReview(n), c.needsReview, px: Size.Note * .9f, key: "Detail:Review");
            b.Button(L("Discard photo", "写真を破棄"), tx + 242, by, 170, 36, () => CapDiscard(n), px: Size.Note * .9f, key: "Discard capture");
            b.Button(L("Evidence picture", "根拠の画像"), x + w - 14 - 180, by, 180, 36, () => SelectCapture(n), px: Size.Note * .9f, key: "Close capture");
        }

        // The two outcrops' photos side by side, when the row that pairs them is picked: what there is to compare, as
        // photographs. Picking either photo opens it. Nothing here says how the two rocks relate.
        void DrawPairView(MLPanel b, float x, float y, float w, float h)
        {
            var two = PlanStations.Take(2).ToArray(); float iw = h * CapW / CapH, mx = x + iw + 18, mw = w - 2 * iw - 36, ty = y + 34;
            for (int i = 0; i < two.Length; i++)
            {
                var s = two[i]; var c = ShownCap(s.id); float ix = i == 0 ? x : x + w - iw;
                if (c != null) { int n = c.n; b.Picture(c.tex, ix, y, iw, h, new Rect(0, 0, 1, 1)); b.Hit("cap:" + n, ix, y, iw, h, () => SelectCapture(n), 8); }
                else { b.Box(ix, y, iw, h, 8, new Color(.07f, .08f, .1f, 1), new Color(1, 1, 1, .14f)); b.Text(L("no view framed", "構図なし"), ix + iw / 2, y + h / 2 + 7, Size.Note, Dim, false, 0); }
                b.Tag($"{s.id} {s.name}", ix + 12, y + 12, Ink, false);
            }
            b.Text(L("OBSERVATION PAIR", "観測の組"), mx + mw / 2, ty, Size.Note, Amber, true, 0); ty += 34;
            foreach (var s in two)
            {
                var c = ShownCap(s.id); bool has = planRole.TryGetValue(s.id, out string role);
                b.Text(s.id + " · " + (has ? Word(role) : L("role not chosen", "役割は未選択")), mx, ty, Size.Note * .85f, has ? Ink : Amber, true); ty += 24;
                ty = b.Wrap(c != null ? Word(c.purpose) : L("no photo yet", "写真なし"), mx, ty, mw, 22, Size.Note * .8f, Soft) + 12;
            }
            b.Wrap(relOn ? L("Relationship: planned — not yet collected", "関係の記録: 予定 — 未取得") : L("Relationship: not in the plan", "関係の記録: 計画に未追加"), mx, ty + 4, mw, 22, Size.Note * .8f, relOn ? Ink : Gray, true);
        }
        // Under them: what the published study says of taking the two together, and what the plan holds for it.
        void DrawPairNotes(MLPanel b, float x, float y, float w)
        {
            b.Text(L("PUBLISHED ROVER STUDY · BOSAK ET AL. 2024", "ローバー観測の論文 · BOSAK ほか 2024"), x, y, Size.Label, Ink, true); b.Text(L("EXISTING EVIDENCE", "既存の根拠"), x + w, y, Size.Note, Gray, true, 1);
            y = b.Wrap(L("Plan observations at both sites and document their geological context. Dating requires laboratory analysis and a justified geological relationship.",
                         "両地点の観測を計画し、地質学的な文脈を記録する。年代の決定には、室内分析と、根拠のある地質関係が必要。"), x, y + 38, w, 35, Size.Body, Ink);
            y = b.Wrap(L("Published rover interpretation; not identified from these panoramas.", "公表されたローバー観測の解釈。これらのパノラマから同定したものではない。"), x, y - 4, w, 27, Size.Note, Gray);
            y = b.Sourced(L("IN THE PLAN", "計画"), relOn ? Cyan : Amber, relOn
                ? L("Document the geological relationship · planned — not yet collected · to verify: ", "地質学的な関係を記録する · 予定 — 未取得 · 未確認: ") + string.Join(" · ", PairUnresolved.Select(Word))
                : L("the relationship of the two is not in the plan yet: it is added on the left", "両地点の関係は、まだ計画に入っていません。左で追加します"), x, y + 6, w);
            b.Sourced(L("OBSERVATION PAIR", "観測の組"), Amber, L("two views to be compared on site. Not a mapped contact, not the same layer, and no age from height.", "現地で見比べる 2 つの構図。地質境界でも同じ層でもなく、標高から年代は決めません。"), x, y + 2, w);
        }

        // ---------- drawing: the saved plan ----------
        public void ShowPlanCard()
        {
            var s = card; s.Clear(); const float X = 80; float W = s.w - 2 * X, y = 190;
            s.Frame(new Color(10 / 255f, 12 / 255f, 17 / 255f, .97f), Cyan);
            bool saved = planSavedUtc.Length > 0 && !planDirty;
            s.Text(saved ? L("OBSERVATION PLAN · SAVED", "観測計画 · 保存済み") : L("OBSERVATION PLAN · NOT SAVED", "観測計画 · 未保存"), X, 74, Size.Label, saved ? Ink : Amber, true);
            s.Text(L("SIMULATED PLAN · HISTORICAL NASA DATA", "模擬の計画 · 過去の NASA データ"), X + W, 74, Size.Label, Amber, true, 1);
            s.Text(L("Goal: ", "目的: ") + GoalName, X, 142, Size.Title, Ink, true);
            s.Text(L("Where will you look, why, and what remains unresolved?", "どこを、何のために見て、何がまだ分かっていないか。"), X + W, 140, Size.Label, Soft, false, 1);
            foreach (var st in PlanStations.OrderBy(p => p.id))
            {
                bool has = planRole.TryGetValue(st.id, out string role); var mine = caps.Where(c => c.station == st.id).ToList(); var cap = mine.LastOrDefault(c => c.next.Count > 0) ?? mine.LastOrDefault(); const float tw = 195, th = 130;
                s.Box(X, y, tw, th, 8, new Color(.1f, .12f, .14f, 1), new Color(1, 1, 1, .16f));
                if (cap != null && cap.tex != null) s.Picture(cap.tex, X, y, tw, th, new Rect(0, 0, 1, 1)); else s.Text(L("no view framed", "構図なし"), X + tw / 2, y + th / 2 + 7, Size.Note, Dim, false, 0);
                float lx = X + tw + 28; string name = $"{st.id} {st.name}";
                if (!InPlan(st.id)) { s.Text(name, lx, y + 44, Size.Heading, Gray, true); s.Text(L("Not in the plan", "計画に入っていません"), lx, y + 88, Size.Body, Gray); y += 152; continue; }
                s.Text(name, lx, y + 38, Size.Heading, Ink, true);
                s.Text(" — " + (has ? Word(role) : L("role not chosen", "役割は未選択")), lx + s.Measure(name, Size.Heading, true), y + 38, Size.Heading, has ? Soft : Amber, true);
                if (cap == null) s.Text(L("No photo framed yet", "写真はまだありません"), lx, y + 80, Size.Body, Soft);
                else
                {
                    var all = mine.SelectMany(c => c.next.Select(nx => nx.type)).Distinct().ToArray(); int hold = mine.Count(c => c.needsReview);
                    s.Text(L("Purpose: ", "目的: ") + Word(cap.purpose) + (mine.Count > 1 ? L($"  ·  {mine.Count} photos", $"  ·  写真 {mine.Count} 枚") : ""), lx, y + 80, Size.Body, Ink);
                    s.Text(all.Length > 0 ? L("Next: ", "次の観測: ") + string.Join(" + ", all.Select(Word)) + L(" — planned, not yet collected", " — 予定、未取得") : L("Photo only — no next observation chosen", "写真のみ — 次の観測は未選択"), lx, y + 118, Size.Body, all.Length > 0 ? Ink : Soft, all.Length > 0);
                    s.Text(L($"looking {MLData.Deg3(cap.az)}°", $"方位 {MLData.Deg3(cap.az)}°"), X + W, y + 80, Size.Label, Gray, false, 1);
                    if (hold > 0) s.Text(L("review later", "あとで確認"), X + W, y + 38, Size.Label, Amber, true, 1);
                }
                y += 152;
            }
            string together = L("Together: ", "合わせて: ");
            s.Text(together, X, y + 36, Size.Heading, Ink, true);
            s.Text(relOn ? L("Document the geological relationship", "地質学的な関係を記録する") : L("not planned", "予定なし"), X + s.Measure(together, Size.Heading, true) + 12, y + 36, Size.Heading, relOn ? Ink : Gray, true);
            if (relOn) s.Text(L("Observation pair · planned, not yet collected", "観測の組 · 予定、未取得"), X, y + 72, Size.Label, Soft);
            y += 124;
            var ev = viewedEvidence.Select(EvidenceName).Distinct().ToArray();
            var open = caps.SelectMany(c => c.next.Select(nx => Unresolved(nx.type))).Concat(relOn ? PairUnresolved.Take(1) : new string[0]).Distinct().Select(Word).ToArray();
            foreach (var (label, text, col) in new[] {
                (L("Existing evidence", "既存の根拠"), ev.Length > 0 ? string.Join(" · ", ev) : L("none opened", "未確認"), Ink),
                (L("Planned observations", "予定の観測"), L($"{PlannedCount} · not yet collected", $"{PlannedCount} 件 · 未取得"), Ink),
                (L("Open questions", "未確認の事項"), (open.Length > 0 ? string.Join(" · ", open) + " · " : "") + L("field context and laboratory analysis still required", "現地での確認と室内分析が必要"), Amber) })
            { s.Text(label, X, y, Size.Label, Gray, true); y = s.Wrap(text, X + 310, y, W - 310, 34, Size.Label, col, true) + 12; }
            s.Text(saved ? L($"Saved {UtcShort(planSavedUtc)} · {planSession}/{MLPlanStore.FileName}", $"保存 {UtcShort(planSavedUtc)} · {planSession}/{MLPlanStore.FileName}") : L("Not saved", "未保存"), X, s.h - 44, Size.Note, Gray);
            s.Button(L("Close", "閉じる"), X + W - 180, s.h - 96, 180, 60, () => { card.Show = false; sfx.Play("toggle", .6f); }, px: Size.Label, key: "Close card");
            if (rig.xr) PlaceInFront(s, 1.25f, -.04f);
            s.Show = true;
        }

        // ---------- on the map ----------
        void MakeCapVisuals(Cap c)
        {
            c.fanMat = ColorMat(new Color(Amber.r, Amber.g, Amber.b, .3f), 3099, true);
            c.fanT = Add("Capture fan " + c.n, fanMesh, c.fanMat, world).transform; c.fanT.localRotation = Quaternion.Euler(0, c.az, 0); c.fanT.gameObject.SetActive(false);
            if (fanRimMat == null) fanRimMat = ColorMat(new Color(1, .93f, .78f, .95f), 3100, true);
            Add("Rim", fanRim, fanRimMat, c.fanT);
            c.tag = new MLPanel("Capture " + c.n, 96, 160, .05f, null, true); c.tag.Order(5); c.tag.Snap(false); panels.Add(c.tag); DrawCapTag(c);
        }
        // A camera tag on a short stem, at the far end of the capture's sector. Picking it brings the capture up.
        // A filled dot: something was planned from it. A ring: set aside for later.
        void DrawCapTag(Cap c)
        {
            var p = c.tag; p.Clear(); bool on = c.n == selCap; int n = c.n; var ink = on ? new Color(.1f, .06f, 0, 1) : Amber;
            p.Stroke(48, 84, 48, 146, 5, Amber); p.Box(39, 138, 18, 18, 9, Amber);
            p.Box(10, 14, 76, 68, 12, on ? Amber : new Color(8 / 255f, 10 / 255f, 14 / 255f, .88f), Amber);
            p.Box(22, 30, 30, 22, 5, Color.clear, ink); p.Box(30, 35, 14, 12, 6, ink); p.Box(27, 25, 12, 7, 2, ink);   // a small camera
            p.Text(n.ToString(), 67, 53, Size.Label, ink, true, 0);
            if (c.next.Count > 0) p.Box(72, 6, 20, 20, 10, Ink); if (c.needsReview) p.Box(4, 6, 20, 20, 10, Color.clear, Color.white);
            p.Hit("cap:" + n, 0, 0, 96, 104, () => SelectCapture(n), 12);
        }
        void DrawPairTag()
        {
            var s = pairTag; s.Clear(); s.Box(0, 4, s.w, s.h - 8, 14, new Color(8 / 255f, 10 / 255f, 14 / 255f, .88f), new Color(Amber.r, Amber.g, Amber.b, .6f));
            s.Text(L("Observation pair", "観測の組"), s.w / 2, 46, Size.Body, Amber, true, 0);
        }
        void RedrawPlan()
        {
            DrawPairTag(); finderFor = ""; foreach (var c in caps) DrawCapTag(c);
            if (shotCap != null) DrawShot(); if (card.Show) ShowPlanCard(); refCard.Show = false;
        }

        // ---------- per frame ----------
        void UpdatePlan(float dt)
        {
            var head = rig.Head; bool field = stage == Stage.Field && !busy && panoOpacity > .9f;
            if (field)
            {   // standing in a station's panorama: that image has been opened
                string at = At; if (!visitedStations.Contains(at)) visitedStations.Add(at);
                if (St(at).hasSample) Viewed("mcz_panorama_" + at);
            }
            if (stage != Stage.Field) { camOn = false; reviewCap = null; shotCap = null; fieldAt = null; }
            else { if (fieldAt != At) { fieldAt = At; fieldCapSeq = capSeq; } if (!CanCamera) camOn = false; }
            if (stage != Stage.Table) pairView = false;
            if (stage != Stage.Table && card.Show) card.Show = false;

            // the viewfinder: held in front of the eyes while the camera is up; or standing where a saved capture looked
            bool live = camOn && field && shotCap == null, review = !camOn && field && reviewCap != null && reviewCap.station == At;
            finder.Show = live || review;
            if ((live || shotCap != null) && banner.Show && !bannerHasButton) HideBanner();   // arriving with the camera up: the frame is not covered
            if (finder.Show)
            {
                string want = (review ? "review" + reviewCap.n : "live" + CamPurpose(out var from) + (from != null)) + MLText.lang + At; if (want != finderFor) { finderFor = want; DrawFinder(review); }
                float az, el, k = finder.go.transform.lossyScale.x; var st = St(At);
                if (review)
                {
                    az = reviewCap.az; el = reviewCap.el; var dir = (PanoPoint(az, el) - head.position).normalized; var rot = Quaternion.LookRotation(dir, Vector3.up);
                    finder.go.transform.SetPositionAndRotation(head.position + dir * FinderDist + rot * Vector3.up * (FinderStrip / 2 * k), rot);
                    if (!rig.xr) finder.go.transform.SetParent(null, true);
                }
                else
                {
                    PanoAim(new Ray(head.position, head.forward), out az, out el);
                    if (rig.xr) { var rot = Quaternion.LookRotation(head.forward, Vector3.up); finder.go.transform.SetPositionAndRotation(head.position + head.forward * FinderDist + rot * Vector3.up * (FinderStrip / 2 * k), rot); }
                    else if (finder.go.transform.parent != head) { finder.go.transform.SetParent(head, false); finder.go.transform.localPosition = new Vector3(0, FinderStrip / 2 * k, FinderDist); finder.go.transform.localRotation = Quaternion.identity; }
                }
                string info = $"{st.id} {st.name} · az {MLData.Deg3(az)}° · el {el:+0;-0;0}° · {CapFov:0}° × {CapVFov:0}°";
                if (info != finderShown && finderInfo != null) { finderShown = info; finderInfo.text = info; }
            }
            if (finderFlash != null) { float k = Mathf.Clamp01(1 - (Time.time - flashAt) / .28f); finderFlash.color = new Color(1, 1, 1, k * k * .85f); }
            // the photo card comes up where the person is looking, and stays there while it is answered
            shot.Show = shotCap != null && stage == Stage.Field;
            if (shot.Show && rig.xr && !shotPlaced) { shotPlaced = true; PlaceInFront(shot, shotDist, -.1f); }
            if (refCard.Show && stage != Stage.Field && stage != Stage.Table) refCard.Show = false;

            // on the table: each capture's sector and tag, the line that pairs the two outcrops, and for each region
            // pointed at in a panorama the direction it was pointed in (the mark itself is only an approximate map point)
            bool table = stage == Stage.Table && introDone; float S = world.localScale.x, r = Mathf.Clamp(.095f / S, 8, 140);
            foreach (var c in caps)
            {
                // however many photos there are, the map shows the one that is picked (or, while the two outcrops are compared, the one of each)
                var st = St(c.station); bool on = table && (c.n == selCap || (pairView && ShownCap(c.station) == c)) && OnPatch(st.x, st.z, .004f);
                if (c.fanT.gameObject.activeSelf != on) c.fanT.gameObject.SetActive(on);
                c.tag.Show = on; if (!on && c.tag.alpha <= 0) continue;
                c.fanT.localPosition = D.Local(st.x, st.z, .35f); c.fanT.localScale = Vector3.one * r;
                c.fanMat.SetColor("_Color", new Color(Amber.r, Amber.g, Amber.b, c.n == selCap ? .66f : .4f));
                float a = c.az * Mathf.Deg2Rad; var at = world.TransformPoint(D.Local(st.x + Mathf.Sin(a) * r * .84f, st.z - Mathf.Cos(a) * r * .84f, .2f));
                float d = Mathf.Max(.05f, Vector3.Distance(at, head.position)), mw = .043f * d * (c.n == selCap ? 1.25f : 1); var rot = Quaternion.LookRotation(at - head.position, Vector3.up);
                c.tag.SetWorldWidth(mw); c.tag.go.transform.SetPositionAndRotation(at + rot * Vector3.up * (mw * 67f / 96f), rot);
            }
            var pair = PlanStations.Take(2).ToArray(); bool pairOn = table && relOn && pair.Length == 2 && OnPatch(pair[0].x, pair[0].z, .004f) && OnPatch(pair[1].x, pair[1].z, .004f);
            pairLine.enabled = pairOn; pairTag.Show = pairOn;
            if (pairOn)
            {
                Vector3 a = world.TransformPoint(D.Local(pair[0].x, pair[0].z, .5f)), b = world.TransformPoint(D.Local(pair[1].x, pair[1].z, .5f)), mid = (a + b) / 2 + Vector3.up * .012f;
                pairLine.SetPosition(0, a); pairLine.SetPosition(1, b); pairLine.startWidth = pairLine.endWidth = .0035f;
                float d = Mathf.Max(.05f, Vector3.Distance(mid, head.position)); var rot = Quaternion.LookRotation(mid - head.position, Vector3.up);
                pairTag.SetWorldWidth(.13f * d); pairTag.go.transform.SetPositionAndRotation(mid + rot * new Vector3(.1f * d, -.035f * d, 0), rot);
            }
            foreach (var m in marks)
            {
                if (!markLines.TryGetValue(m.n, out var lr)) markLines[m.n] = lr = NewLine("Region direction " + m.n, markLineMat);
                var st = St(m.station); lr.enabled = table && OnPatch(st.x, st.z, .004f) && OnPatch(m.x, m.z, .004f);
                if (lr.enabled)
                {   // the region that is picked has its line drawn firmly; the others are there, faintly
                    bool picked = m.n == selMark; lr.sharedMaterial = picked ? markLineMat : markLineDim;
                    lr.SetPosition(0, world.TransformPoint(D.Local(st.x, st.z, .3f))); lr.SetPosition(1, world.TransformPoint(m.local)); lr.startWidth = lr.endWidth = picked ? .003f : .0014f;
                }
            }
            if (markLines.Count > marks.Count)
                foreach (int n in markLines.Keys.Where(k => marks.All(m => m.n != k)).ToList()) { Destroy(markLines[n].gameObject); markLines.Remove(n); }
        }

        /// <summary>For checks from the Editor: the plan as it stands, and where it is kept.</summary>
        public string PlanState() => $"tab={planTab} selcap={selCap} pair={pairView} roles=[{string.Join(",", planRole.OrderBy(p => p.Key).Select(p => p.Key + ":" + p.Value))}] rel={relOn} planned={PlannedCount} "
            + $"caps=[{string.Join(",", caps.Select(c => $"{c.id}@{c.station}:{c.az:0.0}/{c.el:0.0}:{c.purpose}({c.purposeSource};{c.sourceTask}):next={string.Join("+", c.next.Select(x => x.type))}:{(c.needsReview ? "review" : "-")}:{(c.missing ? "recut" : c.file)}"))}] "
            + $"cam={camOn} camPurpose={(stage == Stage.Field ? CamPurpose(out _) : "")} shot={(shotCap != null ? shotCap.id : "-")} "
            + $"viewed=[{string.Join(",", viewedEvidence.OrderBy(e => e))}] visited=[{string.Join(",", visitedStations.OrderBy(e => e))}] dirty={planDirty} saved={planSavedUtc} loaded={planLoaded} error={planSaveError} session={planSession} root={MLPlanStore.Root}";
    }
}
