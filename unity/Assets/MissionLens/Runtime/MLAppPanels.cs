using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using static MissionLens.MLPanel;
using static MissionLens.MLText;

namespace MissionLens
{
    // The surfaces of the app: what is written on them and where they sit. Layout numbers are pixels on each
    // surface (x right, y down), carried over from the desktop mock in experiments/spatial/.
    public partial class MLApp
    {
        MLPanel board, boardR, strip, wrist, hud, title, banner, menu, neat, probe;
        readonly List<MLPanel> panels = new List<MLPanel>();   // every surface that can hold buttons
        TextMeshProUGUI hudHeading, hudClock, hudSub;
        readonly Dictionary<string, Texture2D> viewTex = new Dictionary<string, Texture2D>();
        bool wristPlaced; float restartArmedUntil;

        void BuildPanels()
        {
            // PLAN, in the room: two briefing panels (left: the objective and why this station; right: the NASA
            // evidence) and the task timeline. Where they sit is a layout that can be switched (ApplyLayout): spread
            // on the desk around the model, or as a wall behind it. What is done to the map and the room is on a
            // menu carried on the left hand (MLAppMenu.cs).
            board = new MLPanel("Board left", 1100, 900, .5f, tableFrame);
            boardR = new MLPanel("Board right", 1100, 900, .5f, tableFrame);
            strip = new MLPanel("Timeline", 1600, 270, .78f, tableFrame);
            // On the table itself, along the model's near edge: north, a scale bar, the scale and the source (a map's neatline).
            neat = new MLPanel("Neatline", 1720, 84, FootHx * 2, tableFrame); neat.go.transform.localPosition = new Vector3(0, .016f, -FootHz - .042f); neat.go.transform.localRotation = Quaternion.Euler(52, 0, 0);
            // What the terrain model says at the point being pointed at.
            probe = new MLPanel("Probe", 520, 112, .2f, null, true);
            menu = new MLPanel("Menu", 600, rig.xr ? 840 : 700, MenuW, rig.xr ? null : rig.Head); menuIntro = rig.xr; menu.Order(20);
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
                menu.go.transform.localPosition = new Vector3(-.4f, -.03f, .5f); menu.go.transform.localRotation = Quaternion.Euler(0, -24, 0);   // where a raised left hand would be
                rig.lookPitch = -3;
            }
            foreach (var s in M.stations)
            {
                pins.Add(new Pin { st = s, local = D.Local(s.x, s.z, .3f), label = new MLPanel("Pin " + s.id, 640, 128, .34f, null, true) });
                viewTex[s.id] = Resources.Load<Texture2D>("ML/view_" + s.id);
            }
            panels.AddRange(new[] { board, boardR, strip, menu, wrist, hud, title, banner, neat, probe }); panels.AddRange(pins.Select(p => p.label));
            foreach (var p in panels) p.Snap(false);
            DrawTitle();
        }

        /// <summary>"desk": the panels lie spread on the desk around the model. "wall": they stand behind it.</summary>
        public string layout = "desk";
        void ApplyLayout()
        {
            void Put(MLPanel p, float width, Vector3 at, Vector3 euler) { p.SetWorldWidth(width); p.go.transform.localPosition = at; p.go.transform.localRotation = Quaternion.Euler(euler); }
            if (layout == "wall")
            {   // about 1.3 m from the eyes and 50 degrees across, the timeline under the two panels
                Put(board, .625f, new Vector3(-.308f, .75f, .465f), new Vector3(2, -13, 0)); Put(boardR, .625f, new Vector3(.308f, .75f, .465f), new Vector3(2, 13, 0));
                Put(strip, 1.25f, new Vector3(0, .365f, .5f), new Vector3(11, 0, 0));
            }
            else
            {   // Like papers on a desk: a panel either side of the model, turned in and leaning toward the person, the
                // timeline along the near edge. About 0.9 m from the eyes: as near as they come without covering the
                // model's near corners. (They were 1.1 m away, which read as far.)
                Put(board, .46f, new Vector3(-.68f, .22f, -.4f), new Vector3(40, -48, 0)); Put(boardR, .46f, new Vector3(.68f, .22f, -.4f), new Vector3(40, 48, 0));
                Put(strip, .7f, new Vector3(-.1f, .035f, -.545f), new Vector3(60, 0, 0));
            }
        }
        public void ToggleLayout()
        {
            layout = layout == "desk" ? "wall" : "desk"; sfx.Play("toggle", .7f); Log("layout " + layout); ApplyLayout(); DrawMenu();
            if (!rig.xr) StartCoroutine(Look(.6f, rig.lookYaw, TableLookPitch));
        }

        // Surfaces a pointer can act on right now.
        IEnumerable<MLPanel> Surfaces()
        {
            if (stage == Stage.Title) { yield return title; yield break; }
            if (stage == Stage.Table) { yield return board; yield return boardR; yield return strip; yield return menu; if (tools) { yield return dial; if (lensEnabled) yield return lensTag; } }
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
            board.Show = boardR.Show = strip.Show = neat.Show = table && introDone && roomO > .6f;
            UpdateMenu(dt);
            hud.Show = !OnTable;
            if (banner.Show && bannerUntil > 0 && Time.time > bannerUntil) banner.Show = false;
            if (rig.xr)
            {
                // Title: in front of the person, following their turn loosely until the briefing begins.
                if (stage == Stage.Title) Chase(title, ref titleChasing, head + yaw * new Vector3(0, -.06f, 1.25f), head, 30, 3, dt);
                // The FIELD panel: carried on the opened left hand like the menu at the table (MLAppMenu.cs); or, asked
                // for from the head-up strip, it stays where it was called up, low and to the left.
                UpdateWristOnHand(dt);
                wrist.Show = !OnTable && (wristUp || wristDock.on);
                if (wristUp) { if (!wristPlaced) { wristPlaced = true; wrist.SetWorldWidth(.26f); Follow(wrist, head + yaw * new Vector3(-.3f, -.2f, .55f), head, 1); } }
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
            foreach (var p in panels) p.Tick(dt, p == board || p == boardR || p == strip || p == title ? 2.6f : 5);
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

        float heavyDue;
        /// <param name="light">Redraw only what is cheap now; the briefing panels and the timeline follow a moment later.</param>
        void Refresh(bool light = false)
        {
            bool table = OnTable; int selLeg = M.tasks[selTask].leg; string target = Target;
            foreach (var p in pins)
            {
                var s = p.st; bool isNext = s.id == target; int mk = marks.Count(m => m.station == s.id);
                string sub = "";
                if (table) sub = string.Join(" · ", new[] { s.id == here ? (taskIndex == 0 ? L("start", "出発点") : Finished ? L("finish", "帰着点") : L("the Marswalk is here", "Marswalk はここ")) : "", mk > 0 ? L(mk + " marked", "印 " + mk) : "" }.Where(x => x.Length > 0));
                else if (travelling != null) sub = "";   // on the way: the distance left is on the head-up strip
                else if (s.id != At) sub = $"{BearingFromHere(s.id).d:0} m" + (stage == Stage.Field && St(At).hidden.Contains(s.id) ? L(" · behind terrain", " · 地形の陰") : "");
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
            if (!table) { DrawWrist(); DrawHud(); }
            else if (light) heavyDue = Time.time + .16f;
            else { heavyDue = 0; DrawBoard(); DrawStrip(); }
        }

        /// <summary>English or Japanese, for everything on screen.</summary>
        public void SetLanguage(string lang)
        {
            MLText.lang = lang == "ja" ? "ja" : "en"; PlayerPrefs.SetString("ml.lang", MLText.lang); Log("language " + MLText.lang);
            HideBanner(); DrawTitle(); Refresh(); DrawMenu(); dialBuiltFor = -1; dialTask = -1; lensShown = -999;
            if (OnTable && stage != Stage.Title) { DrawBoard(); DrawStrip(); }
        }
        public void ToggleLanguage() { sfx.Play("toggle", .7f); SetLanguage(Ja ? "en" : "ja"); }
        string MedaLine => Ja ? M.medaLine.Replace("Measured here, sol", "この地点での実測 · sol").Replace(" local:", "(現地時刻):").Replace("air", "気温") : M.medaLine;

        void DrawPin(Pin p)
        {
            var l = p.label; l.Clear(); string text = p.text, sub = p.sub; var col = p.col; bool left = p.side < 0;
            // On the table the selected station's label also carries the way to stand there: what is done to a place is offered at the place.
            bool stand = stage == Stage.Table && introDone && p.st.id == sel; float extra = stand ? 152 : 0;
            float w = Mathf.Min(640, Mathf.Max(l.Measure(text, 38, true), sub.Length > 0 ? l.Measure(sub, 27) : 0) + 104 + extra), x0 = left ? 640 - w : 0; p.boxW = w;
            l.Box(x0, 14, w, 100, 20, new Color(8 / 255f, 10 / 255f, 14 / 255f, .86f), new Color(col.r, col.g, col.b, .35f));
            float ix = left ? 594 : 46; l.Box(ix - 20, 44, 40, 40, 20, new Color(0, 0, 0, 0), col); l.Box(ix - 8, 56, 16, 16, 8, col);
            float tx = left ? 560 : 80; int al = left ? 1 : -1;
            l.Text(text, tx, sub.Length > 0 ? 58 : 77, 38, col, true, al);
            if (sub.Length > 0) l.Text(sub, tx, 97, 27, Soft, false, al);
            string id = p.st.id;
            l.Hit("pin:" + id, x0, 14, w, 100, () => PressPin(id), 20);
            if (stand) l.Button(L("Stand ›", "立つ ›"), left ? x0 + 12 : x0 + w - 150, 31, 138, 66, () => ToField(id), primary: true, px: 27, key: "Stand:" + id);
        }

        // On the table a label selects its station. Inside the terrain it takes you to the surface image there.
        // In FIELD it starts the walk if that station is the next stop; looking ahead, it hops to that station.
        void PressPin(string id)
        {
            if (stage == Stage.Table) SelectStation(id);
            else if (stage != Stage.Field || preview != null) ToField(id);
            else if (id == Target) Travel(id);
            else
            {
                sfx.Play("deny");
                Flash(Finished ? L("The Marswalk is complete. Review it at the table.", "Marswalk は完了しています。机で振り返れます。")
                    : Task.station == here ? L($"Finish the task here first: {Task.phase}", $"先にここでの作業を終えてください: {T(Task.phase)}")
                    : L($"Not the next stop. The current task is at {Task.station} {St(Task.station).name}.", $"次の地点ではありません。現在の作業は {Task.station} {St(Task.station).name} です。"));
            }
        }

        // Before the briefing. Says what the app is and, first of all, which of its contents are NASA's and which are ours.
        void DrawTitle()
        {
            var s = title; s.Clear(); const float X = 90; float W = s.w - 2 * X;
            s.Frame(Back, Cyan);
            s.Text("MISSION LENS", X, 156, 108, Ink, true);
            s.Text(L("From science objectives to Marswalk execution", "科学目標から、火星を歩く 1 回の活動へ"), X, 222, 42, Cyan, true);
            float y = s.Wrap(L("One hypothetical crewed Marswalk at the front of Jezero Crater's fan. Plan it on a tabletop terrain model, then stand at each station inside the panorama Perseverance took there.",
                "ジェゼロ・クレーターの扇状地前縁を歩く、仮想の有人 Marswalk を 1 回分。机上の地形模型で計画を確かめ、各地点では Perseverance がその場所で撮影したパノラマの中に立ちます。"), X, 306, W, 50, Ja ? 34 : 37, Ink);
            y += 30;
            foreach (var (tag, col, text) in new[] {
                (L("NASA OBSERVATION", "NASA の観測"), Ink, L("HiRISE image and terrain model · Mastcam-Z panoramas · CRISM spectral product · MEDA weather · Mars 2020 sampling record", "HiRISE の画像と地形モデル · Mastcam-Z のパノラマ · CRISM の分光データ · MEDA の気象 · Mars 2020 の試料採取記録")),
                (L("OUR CALCULATION", "こちらの計算"), Cyan, L("Route, slopes, lines of sight, walking times, dataset alignment", "ルート、傾斜、見通し、歩行時間、データ同士の位置合わせ")),
                (L("SIMULATED", "模擬"), Amber, L("The Marswalk itself, its tasks and timeline, and whatever you mark", "Marswalk そのもの、作業と時間割、あなたが付ける印")) })
            {
                s.Box(X, y - 30, 280, 44, 22, new Color(1, 1, 1, .07f), new Color(col.r, col.g, col.b, .7f)); s.Text(tag, X + 140, y, 22, col, true, 0);
                y = s.Wrap(text, X + 312, y, W - 312, 38, Ja ? 26 : 28, Soft) + 22;
            }
            s.Button(L("Begin briefing", "ブリーフィングを始める"), X, 724, 440, 92, Begin, primary: true, px: Ja ? 32 : 38, key: "Begin briefing");
            s.Text(rig.xr ? L("Point with a hand and pinch, or pull a controller trigger", "手で指してピンチ、またはコントローラーのトリガー") : L("Click, or press Enter · drag to look around", "クリックまたは Enter · ドラッグで見回す"), X + 476, rig.xr ? 764 : 782, 26, Gray);
            if (rig.xr) s.Text(L("No hand or controller in view? Look at a button and hold still.", "手もコントローラーも映らないときは、ボタンを見つめたまま待つ"), X + 476, 800, 22, Dim);
            s.Text("Data: NASA/JPL-Caltech/UArizona (HiRISE) · NASA/JPL-Caltech/ASU/MSSS (Mastcam-Z) · NASA/JPL-Caltech/JHUAPL (CRISM) · NASA/JPL-Caltech/CAB (MEDA)", X, 850, 19, Dim);
            // the other language, and which build this is (so that it can be told apart from the one before it)
            s.Button(Ja ? "English" : "日本語", X + W - 170, 34, 170, 56, ToggleLanguage, px: 26, key: "Language");
            var stamp = Resources.Load<TextAsset>("ML/build");
            if (stamp != null) s.Text("Build " + stamp.text.Trim(), X + W - 190, 72, 20, Dim, false, 1);
        }

        // PLAN: left, what the Marswalk is for and why the selected station serves it; right, the NASA evidence.
        // After the last task the left side becomes the review of the run.
        void DrawBoard()
        {
            var b = board; b.Clear(); var st = St(sel); const float X1 = 48, W1 = 990, X2 = 30, W2 = 1040; float y;
            b.Frame(Back, Finished ? Amber : Cyan);
            b.Text("MISSION LENS", X1, 62, 24, Gray, true); b.Text(Finished ? L("PLAN · REVIEW", "PLAN · 振り返り") : "PLAN", X1 + 196, 62, 24, Cyan, true);
            b.Text(Finished ? L("SIMULATED RUN", "模擬の実行") : L("HYPOTHETICAL MARSWALK", "仮想の Marswalk"), X1 + W1, 62, 24, Amber, true, 1);
            int mk = marks.Count(m => m.station == st.id); float big = Ja ? 30 : 32, small = Ja ? 23 : 25;
            if (!Finished)
            {
                b.Text(M.title, X1, 120, 46, Ink, true);
                b.Text(L($"{M.subtitle} · {M.routeTotalM:0} m on foot · {MLData.Hm(M.plannedTotalMin)} planned", $"{T(M.subtitle)} · 徒歩 {M.routeTotalM:0} m · 計画 {MLData.Hm(M.plannedTotalMin)}"), X1, 160, 27, Gray);
                b.Text(L("SCIENCE OBJECTIVE", "科学目標"), X1, 216, 24, Gray, true); y = b.Wrap(T(M.objective), X1, 258, W1, 41, big, Ink);
                y = b.Wrap(L("NASA record · ", "NASA の記録 · ") + T(M.objectiveBasis), X1, y - 2, W1, 32, small, Gray);
                b.Text(L($"WHY STATION {st.id} · {st.name.ToUpper()}", $"地点 {st.id} に行く理由 · {st.name.ToUpper()}"), X1, y + 32, 24, Cyan, true); y = b.Wrap(T(st.why), X1, y + 72, W1, 41, big, Ink);
                string sees = st.sees.Length > 0 ? string.Join(", ", st.sees) : L("none", "なし");
                y = b.Wrap(L($"Elevation {st.elev:0.0} m (MOLA datum) · ground slope {st.slopeDeg}° · in view from here: {sees}{(st.hidden.Length > 0 ? " · hidden: " + string.Join(", ", st.hidden) : "")} (our calculation from the terrain model)",
                             $"標高 {st.elev:0.0} m(MOLA 基準)· 地面の傾斜 {st.slopeDeg}° · ここから見える地点: {sees}{(st.hidden.Length > 0 ? " · 見えない地点: " + string.Join(", ", st.hidden) : "")}(地形モデルからこちらで計算)"), X1, y, W1, 32, small, Gray);
                if (st.id == M.medaStation) { b.Text(L("NASA observation · MEDA · ", "NASA の観測 · MEDA · ") + MedaLine, X1, y, small, Soft); y += 32; }
                if (mk > 0) b.Text(L($"Recorded in FIELD · simulated: {mk} candidate{(mk > 1 ? "s" : "")}, marked on the map", $"FIELD での記録(模擬): 候補 {mk} 件。地図に印があります"), X1, y + 2, small, Amber, true);
                // The way into FIELD proper: at the station the Marswalk has reached, with the task that is due.
                var Tk = Task;
                b.Button(taskIndex == 0 ? L("Begin Marswalk", "Marswalk を始める") : L("Resume Marswalk", "Marswalk を再開"), X1, 752, 330, 72, () => ToField(here), primary: true, px: Ja ? 26 : 30, key: "Marswalk");
                b.Text(L($"at {here} {St(here).name} · task {taskIndex + 1} of {M.tasks.Length}: {Tk.phase}{(Tk.station != here ? " " + Tk.station : "")}",
                         $"{here} {St(here).name} から · 作業 {taskIndex + 1} / {M.tasks.Length}: {T(Tk.phase)}{(Tk.station != here ? " " + Tk.station : "")}"), X1 + 356, 782, 25, Soft);
                b.Text(L("FIELD · real surface imagery at each station", "FIELD · 各地点で、実際の地表画像の中に立ちます"), X1 + 356, 814, 21, Dim);
                b.Wrap(T(M.scenarioNote), X1, 862, W1, 24, 20, Dim);
            }
            else
            {
                b.Text(L("Marswalk complete", "Marswalk 完了"), X1, 120, 46, Ink, true);
                b.Text(L($"{M.title} · {M.tasks.Length} tasks stepped through · {M.routeTotalM:0} m on foot · {MLData.Hm(M.plannedTotalMin)} planned", $"{M.title} · {M.tasks.Length} 作業を実施 · 徒歩 {M.routeTotalM:0} m · 計画 {MLData.Hm(M.plannedTotalMin)}"), X1, 160, 27, Gray);
                b.Text(L("WHAT WAS RECORDED · SIMULATED", "記録したもの(模擬)"), X1, 216, 24, Amber, true); y = 262;
                if (marks.Count == 0) y = b.Wrap(L("No candidates were marked on this run.", "今回の実行では候補の印はありません。"), X1, y, W1, 41, big, Ink);
                foreach (var g in marks.GroupBy(m => m.station))
                {
                    b.Text(L($"{g.Key} {St(g.Key).name} · {g.Count()} candidate{(g.Count() > 1 ? "s" : "")}", $"{g.Key} {St(g.Key).name} · 候補 {g.Count()} 件"), X1, y, big, Ink, true); y += 38;
                    string line = string.Join("   ", g.Take(6).Select(m => L($"#{m.n}  {m.d:0} m at {MLData.Deg3(m.az)}°", $"#{m.n}  {m.d:0} m · 方位 {MLData.Deg3(m.az)}°"))) + (g.Count() > 6 ? "   …" : "");
                    y = b.Wrap(line, X1, y, W1, 34, 26, Soft) + 10;
                }
                y = b.Wrap(T(M.recordNote), X1, y + 4, W1, 31, 23, Gray);
                b.Text(L("WHAT THIS RUN DID NOT MEASURE", "この実行で測っていないもの"), X1, y + 30, 24, Gray, true);
                b.Wrap(L("No time was measured: T+ follows the planned timeline, which keeps the task structure and durations of the BASALT-2 planned EVA. Nothing here was checked against a real crew, suit or surface operation.",
                         "時間は計測していません。T+ は計画上の時間割に沿った値で、作業の構成と所要時間は BASALT-2 の計画 EVA に基づきます。実際の乗員・宇宙服・地表での運用と照らした確認は行っていません。"), X1, y + 70, W1, 34, 26, Soft);
                b.Button(L("Start over", "最初からやり直す"), X1, 800, Ja ? 300 : 240, 64, Restart, px: 26, key: "Start over");
                b.Text(L("Clears the run and returns to the title", "記録を消してタイトルに戻ります"), X1 + (Ja ? 322 : 262), 841, 22, Dim);
            }

            // right panel: the NASA evidence for the selected station, and what can be done with it
            b = boardR; b.Clear(); b.Frame(Back, Cyan);
            const float ih = 300; b.Box(X2, 48, W2, ih, 16, new Color(.1f, .12f, .14f, 1));
            if (viewTex.TryGetValue(sel, out var tex) && tex != null) { float vh = (tex.width * ih / W2) / tex.height; b.Picture(tex, X2, 48, W2, ih, new Rect(0, (1 - vh) / 2, 1, vh)); }
            b.Tag(L("NASA OBSERVATION", "NASA の観測"), X2 + 14, 60, Ink);
            b.Text(L($"Mastcam-Z panorama, {st.panoSols}, looking {MLData.Deg3(st.viewAz)}° · {st.panoCredit}", $"Mastcam-Z パノラマ、{st.panoSols}、方位 {MLData.Deg3(st.viewAz)}° の向き · {st.panoCredit}"), X2, 48 + ih + 30, 23, Gray);
            y = 48 + ih + 78; b.Text(st.hasSample ? L("NASA SAMPLING RECORD", "NASA の試料採取記録") : L("NASA ROVER RECORD", "NASA のローバー記録"), X2, y, 24, Gray, true);
            y = b.Wrap(st.hasSample ? L($"Rock cores {st.sampleName}: {st.sampleRock.ToLower()}, {st.sampleLocation.ToLower()}. Taken {st.sampleDate} (sols {st.sampleSol}).", $"岩石コア {st.sampleName}: 堆積岩、扇状地前縁。採取 {st.sampleDate}(sol {st.sampleSol})。") : T(st.note), X2, y + 38, W2, 37, Ja ? 27 : 29, Ink);
            int p = st.crismRank; b.Text(L($"SPECTRAL CONTEXT · MRO CRISM · {M.crismMPerPx:0} M PER PIXEL", $"分光データ · MRO CRISM · 1 画素 {M.crismMPerPx:0} m"), X2, y + 24, 24, Gray, true);
            b.Wrap(L($"{M.crismBands[0]} band here: {(p >= 50 ? $"stronger than in {p}%" : $"weaker than in {100 - p}%")} of this area (our reading). NASA label: Mg/Fe-OH bearing minerals may appear red; spectra are needed for positive identification.",
                     $"この地点の {M.crismBands[0]} バンド: この範囲の {(p >= 50 ? $"{p}% より強い" : $"{100 - p}% より弱い")}(こちらの読み取り)。NASA の説明: Mg/Fe-OH を含む鉱物は赤く見えることがある。鉱物の特定にはスペクトルの確認が必要。"), X2, y + 60, W2, 35, Ja ? 25 : 27, Ink);
            float x = X2; const float by = 762; string s = sel; int bp = Ja ? 26 : 28;
            b.Text(L("LOOK AHEAD AT " + s, s + " を先に見る"), x, by - 14, 19, Dim, true);
            b.Button(L("Stand at " + s, s + " に立つ"), x, by, 300, 68, () => ToField(s), px: bp, key: "Stand at " + s); x += 316;
            b.Button(L("Preview 1 : 25", "下見 1 : 25"), x, by, 300, 68, () => EnterTerrain(s, SGiant), px: bp, key: "Preview 1 : 25"); x += 324;
            b.Wrap(rig.xr ? L("Slope, spectral and height are on the menu on your left hand.", "傾斜・分光・高さの切り替えは、左手のメニューにあります。")
                          : L("Slope, spectral and height are on the menu (M).", "傾斜・分光・高さの切り替えは、メニュー(M)にあります。"), x, by + 28, W2 - (x - X2), 26, 21, Dim);
            const float ly = 866;
            if (layer == "slope")
            {
                float lx = X2;
                foreach (var (c, t) in new[] { (Slope10, "10–15°"), (Slope15, "15–20°"), (Slope20, L("over 20°", "20° 超")) })
                { b.Box(lx, ly - 19, 22, 22, 4, c); b.Text(t, lx + 30, ly, 23, Ink); lx += 46 + b.Measure(t, 23); }
                b.Text(L("· slope and contours: HiRISE terrain model; classes ours", "· 傾斜と等高線: HiRISE 地形モデルから。区分はこちらで設定"), lx, ly, Ja ? 21 : 23, Gray);
            }
            else if (layer == "crism") b.Text(L($"{M.crismObservation} PHY: R {M.crismBands[0]}, G {M.crismBands[1]}, B {M.crismBands[2]} · moved {M.crismShiftM:0} m by us to fit HiRISE · brightness ×{M.crismGain}",
                                                $"{M.crismObservation} PHY: R {M.crismBands[0]}, G {M.crismBands[1]}, B {M.crismBands[2]} · HiRISE に合わせてこちらで {M.crismShiftM:0} m 移動 · 明るさ ×{M.crismGain}"), X2, ly, 21, Gray);
            else b.Text(L("On the table: MRO HiRISE image draped over the HiRISE terrain model", "机上: MRO HiRISE の画像を HiRISE の地形モデルに重ねたもの"), X2, ly, 23, Gray);
        }

        // PLAN: the task timeline. Each segment selects its task, station and leg.
        void DrawStrip()
        {
            var s = strip; s.Clear(); var Ts = M.tasks[selTask]; const float X0 = 30; float W = s.w - 60, k = W / M.plannedTotalMin; var leg = Ts.leg >= 0 ? M.legs[Ts.leg] : null;
            s.Frame(Back, Cyan);
            s.Text($"{Ts.station} {St(Ts.station).name} · {T(Ts.phase)}", X0, 52, 30, Ink, true);
            string state = Ts.index == taskIndex ? L(" · current task", " · 現在の作業") : Ts.index < taskIndex ? L(" · done", " · 完了") : "";
            s.Text(L($"{Ts.plannedMin} min", $"{Ts.plannedMin} 分") + $" · {MLData.Hm(Ts.startMin)}–{MLData.Hm(Ts.endMin)}{state}", X0 + W - 150, 52, 25, Ts.index == taskIndex ? Amber : Gray, false, 1);
            // step through the tasks in order, for hands that find the short segments hard to hit
            int ti = selTask;
            s.Button("‹", X0 + W - 128, 18, 58, 48, () => SelectTask(ti - 1), dim: ti == 0, px: 30, key: "PrevTask");
            s.Button("›", X0 + W - 58, 18, 58, 48, () => SelectTask(ti + 1), dim: ti == M.tasks.Length - 1, px: 30, key: "NextTask");
            string line = leg != null ? L($"Walk {leg.distM:0} m from {leg.from} · climb {leg.ascentM:0} m · steepest {leg.slopeMaxDeg:0}° · {Mathf.RoundToInt(leg.pctOnTrack * 100)}% on Perseverance's track · about {Mathf.RoundToInt(leg.estWalkMin)} min at {M.walkSpeed} m/s",
                                              $"{leg.from} から徒歩 {leg.distM:0} m · 登り {leg.ascentM:0} m · 最大傾斜 {leg.slopeMaxDeg:0}° · {Mathf.RoundToInt(leg.pctOnTrack * 100)}% は Perseverance の走行跡 · 約 {Mathf.RoundToInt(leg.estWalkMin)} 分({M.walkSpeed} m/s)")
                                     : string.Join("  ·  ", Ts.checks.Select(T));
            s.Text(line, X0, 90, Ja ? 21 : 23, Gray);
            if (leg != null)
            {   // what the colours on the selected leg mean (the slope layer's classes)
                float lx = X0 + W, textEnd = X0 + s.Measure(line, Ja ? 21 : 23); string cap = L("slope on the route", "ルート上の傾斜");
                foreach (var (c, t) in new[] { (Slope20, L("over 20°", "20° 超")), (Slope15, "15–20°"), (Slope10, "10–15°") })
                { lx -= s.Measure(t, 20); s.Text(t, lx, 90, 20, Soft); lx -= 26; s.Box(lx, 74, 18, 18, 4, c); lx -= 16; }
                if (lx - s.Measure(cap, 20) > textEnd + 24) s.Text(cap, lx, 90, 20, Dim, false, 1);
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
            s.Text(L("Timeline: BASALT-2 planned EVA (Beaton et al. 2019, Table 2) · walking times and slope classes are ours · amber: current task",
                     "時間割は BASALT-2 の計画 EVA(Beaton ほか 2019、表 2)に基づく · 歩行時間と傾斜の区分はこちらで設定 · 橙の枠: 現在の作業"), X0, 248, 19, Dim);
            bool armed = Time.time < restartArmedUntil; float rw = armed ? 270 : 130;
            if (rig.xr) s.Button(L("Menu", "メニュー"), s.w - 30 - rw - 12 - 130, 222, 130, 40, ToggleMenuPin, menuPinned || menuIntro, px: 19, key: "Menu");
            s.Button(armed ? L("Press again to restart", "もう一度押すと最初から") : L("Restart", "やり直す"), s.w - 30 - rw, 222, rw, 40, () => { if (Time.time < restartArmedUntil) Restart(); else { restartArmedUntil = Time.time + 3; Refresh(); } }, armed, px: 19, key: "Restart");
        }

        static string ShortPhase(MTask t) => t.leg >= 0 ? (t.phase == "Return" ? L("return", "帰還") : L("walk", "移動")) : t.phase.StartsWith("Contextual") ? L("survey", "状況調査") : t.phase.StartsWith("Candidate") ? L("candidate search", "候補探索")
            : t.phase.StartsWith("Presampling") ? L("presampling survey", "採取前調査") : t.phase.StartsWith("Sampling") ? L("sampling", "採取") : T(t.phase).ToLower();

        void DrawWrist()
        {
            var s = wrist; s.Clear(); const float PW = 600, PH = 860; var Tk = Task; float y = 54, yb = PH - 196;
            s.Frame(new Color(12 / 255f, 15 / 255f, 20 / 255f, .95f), preview != null || stage != Stage.Field ? Cyan : Amber);
            s.Text("MISSION LENS", 34, y, 20, Gray, true);
            s.Text(stage == Stage.Giant ? L("PLAN · ROUTE PREVIEW", "PLAN · ルートの下見") : stage == Stage.Walk ? L("TERRAIN MODEL", "地形モデル") : preview != null ? L("PLAN · LOOKING AHEAD", "PLAN · 下見") : "FIELD", PW - 34, y, 20, Cyan, true, 1); y += 44;
            s.Text(M.title, 34, y, 34, Ink, true); y += 36;
            s.Text(L($"T+ {MLData.Hm(Pet)} of {MLData.Hm(M.plannedTotalMin)} planned · task {Mathf.Min(taskIndex + 1, M.tasks.Length)}/{M.tasks.Length}", $"T+ {MLData.Hm(Pet)} / 計画 {MLData.Hm(M.plannedTotalMin)} · 作業 {Mathf.Min(taskIndex + 1, M.tasks.Length)}/{M.tasks.Length}"), 34, y, 22, Soft); y += 18;
            s.Box(34, y, PW - 68, 6, 3, new Color(1, 1, 1, .12f)); if (Pet > 0) s.Box(34, y, (PW - 68) * Pet / M.plannedTotalMin, 6, 3, Cyan); y += 44;
            if (stage == Stage.Giant || stage == Stage.Walk)
            {
                var n = Nearest(); string nid = n.id;
                s.Text(stage == Stage.Giant ? L("Inside the terrain model, 1 : 25", "地形モデルの中 · 1 : 25") : L("Inside the terrain model, true scale", "地形モデルの中 · 実寸"), 34, y, 26, Ink, true); y += 40;
                y = s.Wrap(L("Orbital data only: shape from the HiRISE terrain model, colour from the HiRISE image. No rover imagery here. Sky and haze are drawn by us.",
                             "軌道からのデータだけで作った表示です。形は HiRISE の地形モデル、色は HiRISE の画像。ローバーの画像はここにはありません。空と霞はこちらで描いたものです。"), 34, y, PW - 68, 28, 21, Soft) + 8;
                s.Wrap(L($"Planned route is the blue path. Nearest station: {n.id} {St(n.id).name}, {n.d:0} m.", $"青い線が計画したルート。最も近い地点: {n.id} {St(n.id).name}、{n.d:0} m。"), 34, y, PW - 68, 28, 21, Ink);
                s.Button("1 : 25", 34, yb, 258, 62, () => Rescale(SGiant), stage == Stage.Giant); s.Button(L("True scale", "実寸"), 308, yb, 258, 62, () => Rescale(1), stage == Stage.Walk, key: "True scale");
                s.Button(L("Surface image at " + nid, nid + " の実画像へ"), 34, yb + 76, 340, 62, () => ToField(nid), key: "Surface image at " + nid); s.Button(L("Table", "机へ"), 390, yb + 76, 176, 62, ToTable, key: "Table");
            }
            else
            {
                var st = St(At); int mine = marks.Count(m => m.station == At); bool rec = CanMark;
                s.Text($"{st.id}  {st.name}", 34, y, 30, Ink, true); y += 32;
                s.Text($"Mastcam-Z, {st.panoSols} · {st.panoCredit}", 34, y, 19, Soft); y += 25;
                y = s.Wrap(L("Ours: its direction, the sky above it, and the plain colour with a metre grid where it has no pixels.", "こちらで加えたもの: 画像の向き、画像より上の空、画素の無い部分の単色と 1 m 格子。"), 34, y, PW - 68, 24, 19, Gray) + 3;
                if (st.hasSample) y = s.Wrap(L($"NASA record: rock cores {st.sampleName} were taken within about {Mathf.CeilToInt(st.sampleDistM)} m of this stop.", $"NASA の記録: 岩石コア {st.sampleName} は、この停止点から約 {Mathf.CeilToInt(st.sampleDistM)} m 以内で採取された。"), 34, y, PW - 68, 26, 20, Soft);
                if (st.id == M.medaStation) { s.Text(MedaLine, 34, y, 20, Soft); y += 26; }
                y += 20;
                if (preview != null)
                {   // looking ahead from the briefing: what is planned here, and nothing to do yet
                    s.Text(L("PLANNED HERE", "ここでの予定"), 34, y, 21, Gray, true); y += 34;
                    foreach (var t in M.tasks.Where(t => t.station == st.id).Take(5))
                    {
                        s.Text(MLData.Hm(t.startMin), 34, y, 22, t.index < taskIndex ? Dim : Soft); s.Text(T(t.phase), 104, y, 22, t.index < taskIndex ? Dim : Ink, t.index == taskIndex);
                        s.Text(L(t.plannedMin + " min", t.plannedMin + " 分"), PW - 34, y, 22, Soft, false, 1); y += 30;
                    }
                    s.Wrap(L($"Looking ahead from the briefing. The Marswalk is at {here} {St(here).name}; nothing is recorded here yet.", $"ブリーフィングからの下見です。Marswalk は {here} {St(here).name} にいます。ここではまだ何も記録されません。"), 34, y + 24, PW - 68, 26, 20, Amber);
                    s.Button(L("Back to the table", "机へ戻る"), 34, yb, PW - 68, 62, ToTable, primary: true, key: "Back to the table");
                    string ph = st.id; s.Button(L("Step out to terrain", "地形モデルへ出る"), 34, yb + 76, PW - 68, 62, () => EnterTerrain(ph, 1), key: "Step out to terrain");
                }
                else
                {
                    if (Finished) { s.Text(L("All tasks complete", "全作業が完了"), 34, y, 26, Cyan, true); s.Wrap(L("Go back to the table to review what was recorded.", "机へ戻って、記録を振り返れます。"), 34, y + 36, PW - 68, 28, 21, Soft); }
                    else if (Tk.station == here && Tk.leg < 0)
                    {
                        s.Text(L($"TASK · {Tk.plannedMin} MIN", $"作業 · {Tk.plannedMin} 分"), 34, y, 21, Gray, true);
                        if (rec) s.Text(mine > 0 ? L($"{mine} marked · simulated record", $"印 {mine} 件 · 模擬の記録") : rig.xr ? L("point and pinch to mark", "指してピンチで印") : L("point and click to mark", "指してクリックで印"), PW - 34, y, 20, Amber, mine > 0, 1);
                        y += 36; s.Text(T(Tk.phase), 34, y, 28, Ink, true); y += 22;
                        for (int i = 0; i < Tk.checks.Length; i++)
                        {
                            bool on = done.Contains(i); float y0 = y; int ci = i;
                            s.Box(36, y + 6, 30, 30, 7, on ? Cyan : new Color(0, 0, 0, 0), on ? Cyan : Hex("8b9097")); if (on) s.Check(36, y + 6, 30, OnCyan);
                            y = s.Wrap(T(Tk.checks[i]), 84, y + 30, PW - 124, 28, 22, on ? Hex("8b9097") : Ink) + 6;
                            s.Hit("check:" + i, 28, y0 - 2, PW - 56, y - y0, () => ToggleCheck(ci), 10);
                        }
                        if (rec) s.Wrap(L("A mark's position comes from the terrain model and is approximate.", "印の位置は地形モデルから求めたおおよその位置です。"), 34, y + 22, PW - 68, 24, 19, Gray);
                    }
                    else
                    {
                        var b = BearingFromHere(Tk.station); bool seen = st.sees.Contains(Tk.station);
                        s.Text(L("NEXT", "次"), 34, y, 21, Gray, true); y += 36; s.Text($"{T(Tk.phase)} · {St(Tk.station).name}", 34, y, 28, Ink, true); y += 36;
                        s.Text(L($"{b.d:0} m straight · bearing {MLData.Deg3(b.az)}° · {(seen ? "in view" : "behind terrain")}", $"直線 {b.d:0} m · 方位 {MLData.Deg3(b.az)}° · {(seen ? "見える" : "地形の陰")}"), 34, y, 23, seen ? Ink : Amber); y += 40;
                        var leg = M.legs.FirstOrDefault(l => (l.from == here && l.to == Tk.station) || (l.to == here && l.from == Tk.station));
                        if (leg != null) s.Wrap(L($"Planned route {leg.distM:0} m, steepest {leg.slopeMaxDeg:0}°, about {Mathf.RoundToInt(leg.estWalkMin)} min at {M.walkSpeed} m/s (our assumption).",
                                                  $"計画ルート {leg.distM:0} m、最大傾斜 {leg.slopeMaxDeg:0}°、約 {Mathf.RoundToInt(leg.estWalkMin)} 分({M.walkSpeed} m/s、こちらの仮定)。"), 34, y, PW - 68, 26, 20, Soft);
                    }
                    bool go = !Finished && Tk.station != here; float cw = rec && mine > 0 ? 350 : PW - 68; string to = Tk.station;
                    if (Finished) s.Button(L("Review at the table", "机で振り返る"), 34, yb, PW - 68, 62, ToTable, primary: true, key: "Review at the table");
                    else if (go) s.Button(L($"Walk to {to} {St(to).name}", $"{to} {St(to).name} へ歩く"), 34, yb, PW - 68, 62, () => Travel(to), primary: true, key: "Walk to " + to);
                    else s.Button(L("Complete task", "作業を完了"), 34, yb, cw, 62, CompleteTask, done.Count == Tk.checks.Length, key: "Complete task");
                    if (!Finished && !go && rec && mine > 0) s.Button(L("Undo mark", "印を取り消す"), 34 + cw + 14, yb, PW - 68 - cw - 14, 62, UndoMark, key: "Undo mark");
                    string h = here; s.Button(L("Table", "机へ"), 34, yb + 76, 170, 62, ToTable, key: "Table"); s.Button(L("Step out to terrain", "地形モデルへ出る"), 220, yb + 76, 346, 62, () => EnterTerrain(h, 1), key: "Step out to terrain");
                }
            }
            s.Text(L("Hypothetical Marswalk · data: NASA", "仮想の Marswalk · データ: NASA"), 34, PH - 22, 18, Dim);
        }

        void DrawHud()
        {
            if (hud == null || OnTable) return;
            var s = hud; s.Clear(); var Tk = Task; float W = s.w; bool fin = Finished; string top, main, sub; var col = Ink; string target = Target;
            if (stage == Stage.Field && preview != null)
            {
                var st = St(preview); var here2 = M.tasks.Where(t => t.station == st.id && t.leg < 0).ToArray();
                top = L($"LOOKING AHEAD · {st.id} · {st.name.ToUpper()}", $"下見 · {st.id} · {st.name.ToUpper()}"); col = Ink; main = L("What you will see on arrival", "到着時に見える景色");
                sub = L($"{here2.Length} task{(here2.Length == 1 ? "" : "s")} planned here, {here2.Sum(t => t.plannedMin)} min in all · the Marswalk is at {here} {St(here).name}",
                        $"ここでの予定は {here2.Length} 作業、合計 {here2.Sum(t => t.plannedMin)} 分 · Marswalk は {here} {St(here).name}");
            }
            else if (stage == Stage.Field)
            {
                var st = St(here); top = $"{st.id} · {st.name.ToUpper()}";
                if (fin) { main = L("All tasks complete", "全作業が完了"); sub = L("Go back to the table to review the Marswalk", "机へ戻って Marswalk を振り返れます"); }
                else if (Tk.station == here && Tk.leg < 0)
                {
                    main = T(Tk.phase);
                    sub = L($"{done.Count}/{Tk.checks.Length} checks · {Tk.plannedMin} min planned", $"確認 {done.Count}/{Tk.checks.Length} · 計画 {Tk.plannedMin} 分")
                        + (Tk.recordCheck >= 0 ? (rig.xr ? L(" · point and pinch to mark a candidate", " · 指してピンチで候補に印") : L(" · point and click to mark a candidate", " · 指してクリックで候補に印")) : "");
                }
                else
                {
                    var b = BearingFromHere(Tk.station); bool seen = st.sees.Contains(Tk.station); main = L("Next: ", "次: ") + $"{Tk.station} {St(Tk.station).name}"; col = Amber;
                    sub = L($"{Tk.phase} · {b.d:0} m · bearing {MLData.Deg3(b.az)}° · {(seen ? "in view" : "behind terrain")}", $"{T(Tk.phase)} · {b.d:0} m · 方位 {MLData.Deg3(b.az)}° · {(seen ? "見える" : "地形の陰")}");
                }
            }
            else
            {
                var n = Nearest(); top = stage == Stage.Giant ? L("ROUTE PREVIEW · 1 : 25", "ルートの下見 · 1 : 25") : L("TERRAIN MODEL · TRUE SCALE", "地形モデル · 実寸");
                main = travelling != null ? L($"Walking to {travelling} {St(travelling).name}", $"{travelling} {St(travelling).name} へ移動中") : L("Orbital data only", "軌道からのデータのみ");
                sub = L($"Nearest station: {n.id} {St(n.id).name}, {n.d:0} m", $"最も近い地点: {n.id} {St(n.id).name}、{n.d:0} m");
            }
            if (flashMsg.Length > 0) sub = flashMsg;
            // Left: where you are and what is due. Right: the step that is due as one button, then the panel and the table.
            float R = W - 304, bx = W - 280;
            s.Box(0, 0, W, s.h, 26, new Color(10 / 255f, 12 / 255f, 16 / 255f, .88f));
            if (!rig.xr) s.Hit("hud", 0, 0, W, s.h, ToggleWrist, 26, true);
            s.Text(top, 40, 48, 23, stage == Stage.Field && preview != null ? Cyan : Gray, true);
            hudClock = s.Text($"T+ {MLData.Hm(Pet)} / {MLData.Hm(M.plannedTotalMin)}", R - 132, 48, 23, Gray, true, 1);
            hudHeading = s.Text($"HDG {MLData.Deg3(HeadingNow)}°", R, 48, 23, Gray, true, 1);
            s.Text(main, 40, 104, Ja ? 34 : 38, col, true); hudSub = s.Text(sub, 40, 146, Ja ? 22 : 24, flashMsg.Length > 0 ? Amber : Soft);
            hudSide = TurnSide();   // which way to turn when the next station is outside the view
            if (hudSide != 0) s.Text(hudSide < 0 ? $"« {target}" : $"{target} »", R, 104, 34, Amber, true, 1);
            s.Box(W - 292, 18, 1.5f, s.h - 36, 1, new Color(1, 1, 1, .14f));
            string act = null; System.Action fn = null; bool on = false;
            if (stage == Stage.Field && preview != null) { act = L("Back to the table", "机へ戻る"); fn = ToTable; }
            else if (stage == Stage.Field && fin) { act = L("Review at the table", "机で振り返る"); fn = ToTable; }
            else if (stage == Stage.Field && Tk.station != here) { string to = Tk.station; act = L("Walk to " + to, to + " へ歩く"); fn = () => Travel(to); }
            else if (stage == Stage.Field) { act = L("Complete task", "作業を完了"); fn = CompleteTask; on = done.Count == Tk.checks.Length; }
            else if (travelling == null) { string nid = Nearest().id; act = L("Surface image at " + nid, nid + " の実画像へ"); fn = () => ToField(nid); }
            if (act != null) s.Button(act, bx, 20, 252, 60, fn, on, primary: true, px: Ja ? 21 : 23, key: "HudAction");
            else s.Button(L("Walking…", "移動中…"), bx, 20, 252, 60, () => { }, dim: true, px: 23);
            s.Button(wristUp ? L("Close", "閉じる") : L("Panel", "パネル"), bx, 92, 120, 56, ToggleWrist, wristUp, px: Ja ? 20 : 22, key: "Panel");
            s.Button(L("Table", "机へ"), bx + 132, 92, 120, 56, ToTable, px: Ja ? 20 : 22, key: "HudTable");
        }
    }
}
