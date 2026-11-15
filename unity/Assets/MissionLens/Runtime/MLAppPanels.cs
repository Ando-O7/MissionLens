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
            probe = new MLPanel("Probe", 520, 146, .2f, null, true);
            menu = new MLPanel("Menu", 960, 520, MenuW, rig.xr ? null : rig.Head); menuIntro = rig.xr; menu.Order(20);
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
                PlaceDesktopMenu();
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
        [System.NonSerialized] public string layout = "wall";   // not serialised: the scene must not carry an old choice over the default
        void ApplyLayout()
        {
            void Put(MLPanel p, float width, Vector3 at, Vector3 euler) { p.SetWorldWidth(width); p.go.transform.localPosition = at; p.go.transform.localRotation = Quaternion.Euler(euler); }
            if (layout == "wall")
            {   // Like a pair of screens across a desk: about 0.95 m from the eyes, standing over the far half of the
                // model, the timeline under them. Their lower edges are high enough that the model's far edge still
                // shows beneath. (They stood behind the model, 1.3 m away, which read as far.)
                Put(board, .5f, new Vector3(-.262f, .665f, .12f), new Vector3(3, -12, 0)); Put(boardR, .5f, new Vector3(.262f, .665f, .12f), new Vector3(3, 12, 0));
                Put(strip, 1.0f, new Vector3(0, .345f, .13f), new Vector3(12, 0, 0));
            }
            else
            {   // Like papers on a desk: a panel either side of the model, turned in and leaning toward the person, the
                // timeline along the near edge. About 0.9 m from the eyes: as near as they come without covering the
                // model's near corners. (They were 1.1 m away, which read as far.)
                Put(board, .46f, new Vector3(-.68f, .22f, -.4f), new Vector3(40, -48, 0)); Put(boardR, .46f, new Vector3(.68f, .22f, -.4f), new Vector3(40, 48, 0));
                Put(strip, .7f, new Vector3(-.1f, .035f, -.545f), new Vector3(60, 0, 0));
            }
            PlaceGlobe();
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
            // the observation plan's surfaces (MLAppPlan.cs): the photo card and the viewfinder's purposes in FIELD, the saved plan at the table
            if (refCard.Show) yield return refCard;
            if (shot.Show) yield return shot;
            if (card.Show) yield return card;
            if (finder.Show && camOn) yield return finder;
            if (banner.Show && bannerHasButton) yield return banner;
            foreach (var p in pins) yield return p.label;
            if (stage == Stage.Table) { foreach (var m in marks) yield return m.label; foreach (var c in caps) yield return c.tag; }
        }
        bool IsLabel(MLPanel s) => s == banner || s == finder || pins.Any(p => p.label == s) || marks.Any(m => m.label == s) || caps.Any(c => c.tag == s);

        MLPanel MakeMarkLabel(int n) { var p = new MLPanel("Mark " + n, 96, 160, .05f, null, true); p.Order(5); return p; }   // over the station labels, which they often stand close to
        // A numbered tag on a short stem; the foot of the stem (bottom centre) is the marked point. The same number is
        // shown in FIELD and on the table. On the table it can be picked; the picked one is drawn filled.
        void DrawMarkLabel(Mark m)
        {
            var p = m.label; p.Clear(); bool on = m.n == selMark; int n = m.n;
            var c = on ? Cyan : Ink;   // white: a mark on the map is not warm, where warm colours mean slope. Cyan when it is the one chosen.
            for (float sy = 92; sy < 140; sy += 14) p.Stroke(48, sy, 48, sy + 6, 4, c);   // a broken stem: not real
            p.Box(42, 140, 12, 12, 6, c);
            p.Box(12, 12, 72, 72, 36, new Color(0, 0, 0, .62f));
            foreach (var (ax, ay, bx, by) in new[] { (48f, 8f, 88f, 48f), (88f, 48f, 48f, 88f), (48f, 88f, 8f, 48f), (8f, 48f, 48f, 8f) }) p.Stroke(ax, ay, bx, by, 4, c);   // a hollow diamond: not real
            p.Text(n.ToString("00"), 48, 60, Size.Heading, c, true, 0);
            p.Hit("mark:" + n, 0, 0, 96, 104, () => SelectMark(n), 12);
        }

        // Positions that depend on the head or on the moving terrain, and which surfaces show.
        void UpdateSurfaces(float dt)
        {
            var head = rig.Head.position; bool table = stage == Stage.Table; var yaw = Quaternion.Euler(0, rig.HeadYaw, 0);
            title.Show = stage == Stage.Title && !busy;
            board.Show = boardR.Show = strip.Show = neat.Show = table && introDone && roomO > .6f;
            if (card.Show) board.Show = boardR.Show = strip.Show = false;   // the saved plan comes up in front of the person and is read by itself; closing it brings the panels back
            globeTag.Show = board.Show; globeR.enabled = globeTag.alpha > .01f; globeMat.SetFloat("_Alpha", globeTag.alpha);   // the globe comes and goes with the briefing panels
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
                float mw = (OnTable ? .043f : .05f) * d * pop * (m.n == selMark ? 1.25f : 1); var mrot = Quaternion.LookRotation(at - head, Vector3.up);
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
                var s = p.st; bool isNext = s.id == target; int mk = marks.Count(m => m.station == s.id), ph = caps.Count(c => c.station == s.id);
                string sub = "";
                if (table) sub = string.Join(" · ", new[] { s.id == here ? (taskIndex == 0 ? L("start", "出発点") : Finished ? L("finish", "帰着点") : L("the Marswalk is here", "Marswalk はここ")) : "", ph > 0 ? L(ph + (ph == 1 ? " photo" : " photos"), "写真 " + ph) : "", mk > 0 ? L(mk + " marked", "印 " + mk) : "" }.Where(x => x.Length > 0));
                else if (travelling != null) sub = "";   // on the way: the distance left is on the head-up strip
                else if (s.id != At) sub = $"{BearingFromHere(s.id).d:0} m" + (stage == Stage.Field && St(At).hidden.Contains(s.id) ? L(" · target behind terrain (model)", " · 目標は地形の陰(モデル)") : "");
                bool full = table ? s.id == sel : isNext || preview != null || (Finished && s.id != here);
                var col = table && s.id == sel ? Cyan : Color.white;   // cyan: the station that is chosen
                p.k = table && !full ? .72f : 1; p.text = full ? s.id + " " + s.name : s.id; p.sub = full ? sub : sub.Split(new[] { " · " }, System.StringSplitOptions.None)[0]; p.col = col;
                DrawPin(p);
            }
            foreach (var o in legs)
            {
                bool fwd = o.leg.from == here && o.leg.to == target, back = o.leg.to == here && o.leg.from == target;
                bool next = table ? o.index == selLeg : fwd || back;
                // Over the real image only the leg about to be walked is drawn, and not at the person's feet.
                o.go.SetActive(table || next || stage != Stage.Field); o.back = back;
                var c = Color.white; c.a = table ? (next ? .98f : .42f) : next ? (stage == Stage.Field ? .5f : .7f) : .2f;
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
            HideBanner(); DrawTitle(); Refresh(); DrawMenu(); DrawGlobeTag(); RedrawPlan(); dialBuiltFor = -1; dialTask = -1; lensShown = -999;
            if (OnTable && stage != Stage.Title) { DrawBoard(); DrawStrip(); }
        }
        public void ToggleLanguage() { sfx.Play("toggle", .7f); SetLanguage(Ja ? "en" : "ja"); }
        string MedaLine => Ja ? M.medaLine.Replace("Measured here, sol", "この地点での実測 · sol").Replace(" local:", "(現地時刻):").Replace("air", "気温") : M.medaLine;

        void DrawPin(Pin p)
        {
            var l = p.label; l.Clear(); string text = p.text, sub = p.sub; var col = p.col; bool left = p.side < 0;
            // On the table the selected station's label also carries the way to stand there: what is done to a place is offered at the place.
            bool stand = stage == Stage.Table && introDone && p.st.id == sel; float extra = stand ? 152 : 0;
            float w = Mathf.Min(640, Mathf.Max(l.Measure(text, Size.Heading, true), sub.Length > 0 ? l.Measure(sub, Size.Label) : 0) + 104 + extra), x0 = left ? 640 - w : 0; p.boxW = w;
            l.Box(x0, 14, w, 100, 20, new Color(8 / 255f, 10 / 255f, 14 / 255f, .86f), new Color(col.r, col.g, col.b, .35f));
            float ix = left ? 594 : 46; l.Box(ix - 20, 44, 40, 40, 20, new Color(0, 0, 0, 0), col); l.Box(ix - 8, 56, 16, 16, 8, col);
            float tx = left ? 560 : 80; int al = left ? 1 : -1;
            l.Text(text, tx, sub.Length > 0 ? 58 : 76, Size.Heading, col, true, al);
            if (sub.Length > 0) l.Text(sub, tx, 96, Size.Label, Soft, false, al);
            string id = p.st.id;
            l.Hit("pin:" + id, x0, 14, w, 100, () => PressPin(id), 20);
            if (stand) l.Button(L("Stand ›", "立つ ›"), left ? x0 + 12 : x0 + w - 150, 31, 138, 66, () => ToField(id), primary: true, key: "Stand:" + id);
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
            s.Text("MISSION LENS", X, 156, 108, Ink, true);   // the name, once: the one size outside the scale
            s.Text(L("From science objectives to Marswalk execution", "科学目標から、火星を歩く 1 回の活動へ"), X, 222, Size.Title, Soft);
            float y = s.Wrap(L("One hypothetical crewed Marswalk at the front of Jezero Crater's fan.", "ジェゼロ・クレーターの扇状地前縁を歩く、仮想の有人 Marswalk 1 回分。"), X, 300, W, 36, Size.Body, Ink);
            y += 58;
            foreach (var (tag, col, text) in new[] {
                (L("NASA OBSERVATION", "NASA の観測"), Ink, L("HiRISE image and terrain model · Mastcam-Z · CRISM · MEDA · sampling record","HiRISE(画像・地形モデル)· Mastcam-Z · CRISM · MEDA · 試料採取記録")),
                (L("OUR CALCULATION", "こちらの計算"), Cyan, L("Route · slopes · predicted occlusion of station targets · walking times · dataset alignment", "ルート · 傾斜 · 地点の目標が地形に隠れるかの予測 · 歩行時間 · データの位置合わせ")),
                (L("SIMULATED", "模擬"), Amber, L("The Marswalk · its tasks and timeline · your photos, records and observation plan", "Marswalk · 作業と時間割 · あなたの撮影・記録・観測計画")) })
            {
                s.SourceLabel(OriginOf(col), tag, X, y, Size.Label);
                y = s.Wrap(text, X + 332, y, W - 332, 31, Size.Label, Soft) + 43;
            }
            s.Button(L("Begin briefing", "ブリーフィングを始める"), X, 676, 440, 92, Begin, primary: true, px: Size.Heading, key: "Begin briefing");   // the one thing to do here: a step larger than other main buttons
            s.Text(rig.xr ? L("Point with a hand and pinch · no hand in view? Look at a button and hold still", "手で指してピンチ · 手が映らないときはボタンを見つめる") : L("Click, or press Enter · drag to look around", "クリックまたは Enter · ドラッグで見回す"), X + 476, 729, Size.Note, Gray);
            s.Wrap("Data: NASA/JPL-Caltech/UArizona (HiRISE) · NASA/JPL-Caltech/ASU/MSSS (Mastcam-Z) · NASA/JPL-Caltech/JHUAPL (CRISM) · NASA/JPL-Caltech/CAB (MEDA) · NASA/JPL/USGS (Viking globe)", X, 830, W, 25, Size.Note, Dim);
            // the other language, and which build this is (so that it can be told apart from the one before it)
            s.Button(Ja ? "English" : "日本語", X + W - 170, 34, 170, 56, ToggleLanguage, key: "Language");
            var stamp = Resources.Load<TextAsset>("ML/build");
            if (stamp != null) s.Text("Build " + stamp.text.Trim(), X + W - 190, 72, Size.Note, Dim, false, 1);
        }

        // PLAN: left, what the Marswalk is for and why the selected station serves it; right, the NASA evidence.
        // After the last task the left side becomes the review of the run.
        void DrawBoard()
        {
            var b = board; b.Clear(); var st = St(sel); const float X1 = 48, W1 = 990, X2 = 30, W2 = 1040; float y;
            b.Frame(Back, Finished ? Amber : Cyan);
            string plan = Finished ? L("PLAN · REVIEW", "PLAN · 振り返り") : "PLAN";
            b.Text(plan, X1, 62, Size.Label, Gray, true);
            // the board has two faces: the task that is looked at, and the observation plan the briefing is for (MLAppPlan.cs)
            if (Finished) b.Text(M.title, X1 + b.Measure(plan, Size.Label, true) + 18, 62, Size.Label, Gray, true); else DrawPlanTabs(b, X1 + b.Measure(plan, Size.Label, true) + 18);
            b.Text(Finished ? L("SIMULATED RUN", "模擬の実行") : planTab ? L("EARTH-BASED BRIEFING", "地球での事前ブリーフィング") : L("HYPOTHETICAL MARSWALK", "仮想の Marswalk"), X1 + W1, 62, Size.Label, Amber, true, 1);
            int mk = marks.Count(m => m.station == st.id);
            if (!Finished && planTab) DrawPlanBoard(b);
            else if (!Finished)
            {
                var Ts = M.tasks[selTask]; var leg = Ts.leg >= 0 ? M.legs[Ts.leg] : null; bool current = selTask == taskIndex;
                // what the briefing is for, in one line: the one goal, and the question it asks of the two outcrops
                y = b.Wrap(GoalLine, X1, 100, W1, 27, Size.Note, Gray);
                // 1. the task that is looked at, and whether it is only looked at or is where the Marswalk is
                y += 22;
                b.Tag(current ? (taskIndex == 0 ? L("FIRST TASK OF THE MARSWALK", "Marswalk の最初の作業") : L("THE MARSWALK IS AT THIS TASK", "Marswalk はこの作業"))
                    : selTask < taskIndex ? L("LOOKING AT THE PLAN · A TASK ALREADY DONE", "計画を確認中 · 実施済みの作業") : L("LOOKING AT THE PLAN · THE MARSWALK IS NOT HERE YET", "計画を確認中 · Marswalk はまだここではありません"), X1, y - 23, current ? Ink : Soft, false);
                b.Text(L($"task {selTask + 1} of {M.tasks.Length}", $"作業 {selTask + 1} / {M.tasks.Length}"), X1 + W1, y, Size.Note, Gray, false, 1);
                y += 62; string subject = $"{Ts.station} {St(Ts.station).name} · {T(Ts.phase)}";
                b.Text(subject, X1, y, b.Measure(subject, Size.Title, true) > W1 ? Size.Heading : Size.Title, Ink, true);
                // 2. what to do
                y += 50; b.Text(L("WHAT TO DO", "すること"), X1, y, Size.Label, Gray, true); y += 40;
                foreach (var c in Ts.checks) y = b.Wrap("· " + T(c), X1, y, W1, 36, Size.Body, Ink);
                // 3. a leg: its numbers side by side, to be read at a glance. A station: why it is on the route.
                y += 14;
                if (leg != null)
                {
                    string head = L("WHAT TO CHECK ON THE WAY", "確かめること");
                    b.Text(head, X1, y, Size.Label, Gray, true); b.Text(L("· OUR CALCULATION", "· こちらの計算"), X1 + b.Measure(head, Size.Label, true) + 12, y, Size.Label, Soft, true);
                    float cx = X1;
                    foreach (var (value, what) in new[] {
                        ($"{leg.distM:0} m", L($"from {leg.from}", $"{leg.from} からの距離")), ($"{leg.ascentM:0} m", L("climb", "登り")), ($"{leg.slopeMaxDeg:0}°", L("steepest slope", "最大傾斜")),
                        (L($"{Mathf.RoundToInt(leg.estWalkMin)} min", $"約 {Mathf.RoundToInt(leg.estWalkMin)} 分"), L($"on foot at {M.walkSpeed} m/s", $"歩行({M.walkSpeed} m/s)")) })
                    { b.Text(value, cx, y + 52, Size.Heading, Ink, true); b.Text(what, cx, y + 80, Size.Note, Gray); cx += W1 / 4; }
                    y += 116; bool seen = St(leg.from).sees.Contains(leg.to);
                    y = b.Wrap(L($"From {leg.from}, the station target at {leg.to} is predicted {(seen ? "clear of terrain" : "behind terrain")} (model: eye 2 m, target 1 m above ground, about 2 m terrain grid) · {Mathf.RoundToInt(leg.pctOnTrack * 100)}% of the route follows Perseverance's track · {M.walkSpeed} m/s is our assumption",
                                 $"{leg.from} から、{leg.to} の目標点は{(seen ? "地形に隠れない" : "地形の陰になる")}と予測(モデル: 目の高さ 2 m、目標は地面から 1 m、地形は約 2 m 格子)· ルートの {Mathf.RoundToInt(leg.pctOnTrack * 100)}% は Perseverance の走行跡 · {M.walkSpeed} m/s は仮定"), X1, y, W1, 27, Size.Note, Gray);
                }
                else
                {
                    b.Text(L($"WHY STATION {st.id}", $"地点 {st.id} に行く理由"), X1, y, Size.Label, Gray, true); y += 40;
                    y = b.Wrap(T(st.why), X1, y, W1, 36, Size.Body, Ink);
                    // A prediction about one point per station, with its conditions; it says nothing of a whole outcrop.
                    string sees = st.sees.Length > 0 ? string.Join(", ", st.sees) : L("none", "なし"), hid = string.Join("/", st.hidden);
                    y = b.Sourced(L("OUR CALCULATION", "こちらの計算"), Cyan, L($"Predicted occlusion of station targets. Eye: 2 m; targets: 1 m above ground; approximately 2 m terrain grid (0.3 m tolerance). Clear from here: {sees}.{(st.hidden.Length > 0 ? $" {hid} station target{(st.hidden.Length > 1 ? "s" : "")}: predicted behind terrain. Inspect the outcrops locally." : "")}",
                        $"地点の目標点が地形に隠れるかの予測。目の高さ 2 m、目標は地面から 1 m、地形は約 2 m 格子(許容 0.3 m)。ここから隠れない目標: {sees}。{(st.hidden.Length > 0 ? $"{hid} の目標点は地形の陰と予測。露頭は現地で確かめる。" : "")}"), X1, y, W1);
                    if (st.id == M.medaStation) y = b.Sourced(L("NASA OBSERVATION", "NASA の観測"), Ink, L("MEDA, a past measurement · ", "MEDA(過去の実測)· ") + MedaLine, X1, y, W1);
                }
                // what the map shows for this task (or that it was changed by hand)
                bool own = layer == LayerFor(Ts);
                y = b.Sourced(own ? L("MAP", "地図") : L("MAP · CHANGED BY HAND", "地図 · 手動で変更"), own ? Cyan : Amber, LayerNote(layer), X1, y, W1);
                if (mk > 0)
                {   // what was recorded here in FIELD; the one picked on the map is spelt out (its task is the one shown above)
                    var picked = marks.Find(m => m.n == selMark && m.station == st.id);
                    string list = string.Join(", ", marks.Where(m => m.station == st.id).Select(m => "#" + m.n.ToString("00")));
                    b.Sourced(L("SIMULATED RECORD", "模擬の記録"), Amber, picked != null
                        ? L($"observation region {picked.n:00} — needs local verification · {picked.station} panorama, image direction {MLData.Deg3(picked.az)}° / {picked.el:+0;-0;0}° · map mark: approximate terrain intersection, not a surveyed sample site",
                            $"観測領域 {picked.n:00} — 現地での確認が必要 · {picked.station} のパノラマ、画像の方向 {MLData.Deg3(picked.az)}° / {picked.el:+0;-0;0}° · 地図の印は地形モデルとのおおよその交点で、測量した採取位置ではない")
                        : L($"observation regions {list} pointed at in FIELD — need local verification · pick a mark on the map", $"観測領域 {list} を FIELD で指定 — 現地での確認が必要 · 地図の印を選ぶと詳細"), X1, y, W1);
                }
                // 4. what can be done next. The way into FIELD is at the station the Marswalk has reached, with the task that is due there.
                const float ny = 726;
                b.Button(taskIndex == 0 ? L("Begin Marswalk", "Marswalk を始める") : L("Resume Marswalk", "Marswalk を再開"), X1, ny, 320, 66, () => ToField(here), primary: true, px: Size.Body, key: "Marswalk");
                string where = L($"The Marswalk is at {here} {St(here).name} (task {taskIndex + 1} of {M.tasks.Length})", $"Marswalk はいま {here} {St(here).name}(作業 {taskIndex + 1} / {M.tasks.Length})");
                if (current) b.Text(where + L(" · FIELD: the real surface image there", " · FIELD: その地点の実際の地表画像の中へ"), X1, ny + 98, Size.Note, Gray);
                else
                {
                    b.Button(L("Start from this task", "この作業から始める"), X1 + 336, ny, 300, 66, StartFromSelected, key: "Start here");
                    b.Text(where + L(" · starting from here keeps the records", " · ここから始めても記録は残る"), X1, ny + 98, Size.Note, Gray);
                }
                b.Text(L("Hypothetical plan. Stations, routes and timings: ours. Places, images, measurements: NASA data.", "仮想の計画: 地点・ルート・時間割はこちらで設定。場所・画像・計測値は NASA のデータ"), X1, 858, Size.Note, Dim);
            }
            else
            {
                b.Text(L("Marswalk complete", "Marswalk 完了"), X1, 126, Size.Title, Ink, true);
                b.Text(L($"{M.title} · {M.tasks.Length} tasks stepped through · {M.routeTotalM:0} m on foot · {MLData.Hm(M.plannedTotalMin)} planned", $"{M.title} · {M.tasks.Length} 作業を実施 · 徒歩 {M.routeTotalM:0} m · 計画 {MLData.Hm(M.plannedTotalMin)}"), X1, 166, Size.Label, Soft);
                b.Text(L("WHAT WAS RECORDED · SIMULATED", "記録したもの(模擬)"), X1, 222, Size.Label, Amber, true); y = 266;
                if (marks.Count == 0) y = b.Wrap(L("No observation regions were marked on this run.", "今回の実行では観測領域の印はありません。"), X1, y, W1, 36, Size.Body, Ink);
                foreach (var g in marks.GroupBy(m => m.station))
                {
                    b.Text(L($"{g.Key} {St(g.Key).name} · {g.Count()} observation region{(g.Count() > 1 ? "s" : "")} · need local verification", $"{g.Key} {St(g.Key).name} · 観測領域 {g.Count()} 件 · 現地での確認が必要"), X1, y, Size.Body, Ink, true); y += 36;
                    string line = string.Join("   ", g.Take(6).Select(m => L($"#{m.n}  image direction {MLData.Deg3(m.az)}°", $"#{m.n}  画像の方向 {MLData.Deg3(m.az)}°"))) + (g.Count() > 6 ? "   …" : "");
                    y = b.Wrap(line, X1, y, W1, 31, Size.Label, Soft) + 12;
                }
                y = b.Wrap(T(M.recordNote), X1, y + 4, W1, 27, Size.Note, Gray);
                b.Text(L("WHAT THIS RUN DID NOT MEASURE", "この実行で測っていないもの"), X1, y + 30, Size.Label, Gray, true);
                b.Wrap(L("No time was measured: T+ follows the planned timeline, which keeps the task structure and durations of the BASALT-2 planned EVA. Nothing here was checked against a real crew, suit or surface operation.",
                         "時間は計測していません。T+ は計画上の時間割に沿った値で、作業の構成と所要時間は BASALT-2 の計画 EVA に基づきます。実際の乗員・宇宙服・地表での運用と照らした確認は行っていません。"), X1, y + 72, W1, 36, Size.Body, Ink);
                b.Button(L("Start over", "最初からやり直す"), X1, 800, Ja ? 300 : 240, 64, Restart, key: "Start over");
                b.Text(L("Clears the run and returns to the title", "記録を消してタイトルに戻ります"), X1 + (Ja ? 322 : 262), 840, Size.Note, Gray);
            }

            // right panel: the NASA evidence for the selected station, and what can be done with it
            b = boardR; b.Clear(); b.Frame(Back, Cyan);
            // A rehearsal capture picked on the plan or on the map takes the picture's place (MLAppPlan.cs); the pair's row puts both outcrops' photos there.
            bool pairOn = pairView && stage == Stage.Table; var shown = pairOn ? null : SelectedCapture(sel); float ih = pairOn || st.hasSample ? 250 : 300; b.Box(X2, 48, W2, ih, 16, new Color(.1f, .12f, .14f, 1));
            if (pairOn)
            {
                DrawPairView(b, X2, 48, W2, ih);
                b.Text(L("Rehearsal captures of both outcrops, side by side · compared as photographs", "両方の露頭の模擬の撮影を並べたもの · 写真として見比べる"), X2, 48 + ih + 28, Size.Note, Amber);
                DrawPairNotes(b, X2, 48 + ih + 62, W2);
            }
            else
            {
                if (shown != null) DrawCaptureReview(b, shown, X2, 48, W2, ih);
                else
                {
                    if (viewTex.TryGetValue(sel, out var tex) && tex != null) { float vh = (tex.width * ih / W2) / tex.height; b.Picture(tex, X2, 48, W2, ih, new Rect(0, (1 - vh) / 2, 1, vh)); }
                    b.Tag(L("NASA OBSERVATION", "NASA の観測"), X2 + 14, 60, Ink);
                }
                b.Text(shown != null ? L("Rehearsal capture · a view framed in the Mastcam-Z panorama, by direction and field of view", "模擬の撮影 · Mastcam-Z パノラマの中で、方向と画角で決めた構図") : $"Mastcam-Z · {st.panoSols} · {st.panoCredit}", X2, 48 + ih + 28, Size.Note, shown != null ? Amber : Gray);
                y = 48 + ih + (st.hasSample ? 62 : 84); b.Text(st.hasSample ? L("NASA SAMPLING RECORD", "NASA の試料採取記録") : L("NASA ROVER RECORD", "NASA のローバー記録"), X2, y, Size.Label, Ink, true); y += st.hasSample ? 34 : 42;
                if (st.hasSample)
                {   // the cores first; what they are and when they were taken under them. Then the outcrop's science card and the way to add it to the plan.
                    b.Text(L($"Rock cores {st.sampleName}", $"岩石コア {st.sampleName}"), X2, y, Size.Body, Ink); y += 30;
                    y = b.Wrap(L($"{st.sampleRock}, {st.sampleLocation.ToLower()} · taken {st.sampleDate} (sols {st.sampleSol})", $"堆積岩、扇状地前縁 · 採取 {st.sampleDate}(sol {st.sampleSol})"), X2, y, W2, 27, Size.Note, Gray);
                    DrawScience(b, st, X2, y + 24, W2);
                }
                else
                {   // The spectral layer is regional context at 18 m per pixel. No reading is given for a single station: neighbouring stations share most of their pixels.
                    y = b.Wrap(T(st.note), X2, y, W2, 36, Size.Body, Ink);
                    y += 28; b.Text(L("REGIONAL SPECTRAL CONTEXT · MRO CRISM", "周辺の分光データ · MRO CRISM"), X2, y, Size.Label, Ink, true); y += 40;
                    b.Wrap(L($"{M.crismMPerPx:0} m/pixel. Not a ranking of individual outcrops. NASA label: Mg/Fe-OH bearing minerals may appear red; spectra are needed for positive identification.",
                             $"1 画素 {M.crismMPerPx:0} m。個々の露頭の順位ではありません。NASA の説明: Mg/Fe-OH を含む鉱物は赤く見えることがある。鉱物の特定にはスペクトルの確認が必要。"), X2, y, W2, 27, Size.Note, Gray);
                }
            }
            float x = X2; const float by = 762; string s = sel;
            if (shown != null) { int cn = shown.n; b.Button(L("Reframe at " + s, s + " で同じ構図へ"), x, by, 300, 68, () => ReviewInField(cn), primary: true, px: Size.Body, key: "Stand at " + s); }
            else b.Button(L("Stand at " + s, s + " に立つ"), x, by, 300, 68, () => ToField(s), primary: true, px: Size.Body, key: "Stand at " + s);
            x += 316;
            b.Button(L("Overlook", "地形を見渡す"), x, by, 300, 68, () => EnterTerrain(s, SGiant), key: "Preview 1 : 25"); x += 324;
            b.Text(L("terrain model · 1 : 25", "地形モデル · 1 : 25"), x, by + 42, Size.Note, Gray);
            const float ly = 866;
            if (layer == "slope")
            {
                float lx = X2;
                foreach (var (c, t) in new[] { (Slope10, "10–15°"), (Slope15, "15–20°"), (Slope20, L("over 20°", "20° 超")) })
                { b.Box(lx, ly - 17, 20, 20, 4, c); b.Text(t, lx + 28, ly, Size.Note, Gray); lx += 44 + b.Measure(t, Size.Note); }
                b.Text(L("· slope and contours: HiRISE terrain model; classes ours", "· 傾斜と等高線: HiRISE 地形モデルから。区分はこちらで設定"), lx, ly, Size.Note, Gray);
            }
            else if (layer == "crism") b.Text(L($"{M.crismObservation} PHY browse: R {M.crismBands[0]}, G {M.crismBands[1]}, B {M.crismBands[2]} · moved about {M.crismShiftM:0} m by us; residual not assessed · ×{M.crismGain}",
                                                $"{M.crismObservation} PHY ブラウズ: R {M.crismBands[0]}, G {M.crismBands[1]}, B {M.crismBands[2]} · こちらで約 {M.crismShiftM:0} m 移動(残差は未評価)· ×{M.crismGain}"), X2, ly, Size.Note * .9f, Gray);
            else b.Text(L("On the table: MRO HiRISE image draped over the HiRISE terrain model", "机上: MRO HiRISE の画像を HiRISE の地形モデルに重ねたもの"), X2, ly, Size.Note, Gray);
        }

        // PLAN: the task timeline. Each segment selects its task, station and leg.
        void DrawStrip()
        {
            var s = strip; s.Clear(); var Ts = M.tasks[selTask]; const float X0 = 30; float W = s.w - 60, k = W / M.plannedTotalMin; var leg = Ts.leg >= 0 ? M.legs[Ts.leg] : null;
            s.Frame(Back, Cyan);
            s.Text($"{Ts.station} {St(Ts.station).name} · {T(Ts.phase)}", X0, 58, Size.Heading, Ink, true);
            string state = Ts.index == taskIndex ? L(" · current task", " · 現在の作業") : Ts.index < taskIndex ? L(" · done", " · 完了") : "";
            s.Text(L($"{Ts.plannedMin} min", $"{Ts.plannedMin} 分") + $" · {MLData.Hm(Ts.startMin)}–{MLData.Hm(Ts.endMin)}{state}", X0 + W - 150, 56, Size.Label, Ts.index == taskIndex ? Ink : Gray, false, 1);
            // step through the tasks in order, for hands that find the short segments hard to hit
            int ti = selTask;
            s.Button("‹", X0 + W - 128, 20, 58, 50, () => SelectTask(ti - 1), dim: ti == 0, px: Size.Body, key: "PrevTask");
            s.Button("›", X0 + W - 58, 20, 58, 50, () => SelectTask(ti + 1), dim: ti == M.tasks.Length - 1, px: Size.Body, key: "NextTask");
            if (leg != null)
            {   // what the colours on the selected leg mean (the slope layer's classes). The leg's numbers are on the left panel.
                float lx = X0 + W;
                foreach (var (c, t) in new[] { (Slope20, L("over 20°", "20° 超")), (Slope15, "15–20°"), (Slope10, "10–15°") })
                { lx -= s.Measure(t, Size.Note); s.Text(t, lx, 96, Size.Note, Gray); lx -= 26; s.Box(lx, 80, 18, 18, 4, c); lx -= 16; }
                s.Text(L("slope on the route · classes ours", "ルート上の傾斜 · 区分はこちらで設定"), lx, 96, Size.Note, Gray, false, 1);
            }
            foreach (var t in M.tasks)
            {
                float x = X0 + t.startMin * k, w = t.plannedMin * k - 4; bool on = t.index == selTask; int i = t.index;
                s.Box(x, 110, w, 70, 8, on ? Cyan : new Color(1, 1, 1, t.station == sel ? .22f : t.index < taskIndex ? .27f : .12f), t.index == taskIndex && !Finished ? Ink : (Color?)null);
                // station, and under it what kind of task, where the segment is wide enough to hold the word
                // (its last word alone, "search" for "candidate search", where the whole would not fit)
                string kind = ShortPhase(t); if (s.Measure(kind, Size.Note) > w - 8 && kind.Contains(" ")) kind = kind.Substring(kind.LastIndexOf(' ') + 1);
                bool two = w >= 74 && s.Measure(kind, Size.Note) <= w - 8;
                if (w > 44) s.Text((t.leg >= 0 ? "→ " : "") + t.station, x + w / 2, two ? 141 : 154, Size.Label, on ? OnCyan : Ink, true, 0);
                if (two) s.Text(kind, x + w / 2, 168, Size.Note, on ? OnCyan : Soft, false, 0);
                s.Hit("task:" + i, x, 104, w + 4, 82, () => SelectTask(i), 10);
            }
            for (int m = 0; m < M.plannedTotalMin; m += 60) s.Text(MLData.Hm(m), X0 + m * k, 212, Size.Note, Gray);
            s.Text(MLData.Hm(M.plannedTotalMin), X0 + W, 212, Size.Note, Gray, false, 1);
            s.Text(L("Timeline: BASALT-2 planned EVA (Beaton et al. 2019, Table 2) · walking times are ours · outlined: where the Marswalk is",
                     "時間割: BASALT-2 の計画 EVA に基づく(Beaton ほか 2019、表 2)· 歩行時間はこちらの仮定 · 枠つき = Marswalk の現在位置"), X0, 250, Size.Note, Gray);
            bool armed = Time.time < restartArmedUntil; float rw = armed ? 290 : 130;
            if (rig.xr) s.Button(L("Menu", "メニュー"), s.w - 30 - rw - 12 - 130, 222, 130, 40, ToggleMenuPin, menuPinned || menuIntro, px: Size.Note, key: "Menu");
            s.Button(armed ? L("Press again to restart", "もう一度押すと最初から") : L("Restart", "やり直す"), s.w - 30 - rw, 222, rw, 40, () => { if (Time.time < restartArmedUntil) Restart(); else { restartArmedUntil = Time.time + 3; Refresh(); } }, armed, px: Size.Note, key: "Restart");
        }

        static string ShortPhase(MTask t) => t.leg >= 0 ? (t.phase == "Return" ? L("return", "帰還") : L("walk", "移動")) : t.phase.StartsWith("Contextual") ? L("survey", "状況調査") : t.phase.StartsWith("Candidate") ? L("candidate search", "候補探索")
            : t.phase.StartsWith("Presampling") ? L("presampling survey", "採取前調査") : t.phase.StartsWith("Sampling") ? L("sampling", "採取") : T(t.phase).ToLower();

        void DrawWrist()
        {
            var s = wrist; s.Clear(); const float PW = 600, PH = 860, IW = PW - 68; var Tk = Task; float y = 56, yb = PH - 196;
            s.Frame(new Color(9 / 255f, 9 / 255f, 10 / 255f, .95f), Cyan);
            if (stage == Stage.Giant || stage == Stage.Walk)
            {
                var n = Nearest(); string nid = n.id;
                s.Text(L("TERRAIN MODEL", "地形モデル"), 34, y, Size.Label, Ink, true); s.Text(stage == Stage.Giant ? L("PLAN · OVERLOOK", "PLAN · 見渡す") : L("TRUE SCALE", "実寸"), PW - 34, y, Size.Note, Gray, true, 1);
                y += 62; s.Text(stage == Stage.Giant ? L("Overlook · 1 : 25", "地形を見渡す · 1 : 25") : L("Terrain model · true scale", "地形モデル · 実寸"), 34, y, Size.Heading, Ink, true); y += 46;
                // orbital data only; what of the picture is ours
                y = s.Sourced(L("NASA OBSERVATION", "NASA の観測"), Ink, L("HiRISE terrain model and image, from orbit", "HiRISE の地形モデルと画像(軌道から)"), 34, y, IW);
                s.Sourced(L("DRAWN BY US", "こちらで描いたもの"), Cyan, L("sky and haze", "空と霞"), 34, y, IW);
                s.Button(L("Overlook 1 : 25", "見渡す 1 : 25"), 34, yb, 258, 62, () => Rescale(SGiant), stage == Stage.Giant, key: "1 : 25"); s.Button(L("True scale 1 : 1", "実寸 1 : 1"), 308, yb, 258, 62, () => Rescale(1), stage == Stage.Walk, key: "True scale");
                s.Button(L("Surface image at " + nid, nid + " の実画像へ"), 34, yb + 76, 340, 62, () => ToField(nid), key: "Surface image at " + nid); s.Button(L("Table", "机へ"), 390, yb + 76, 176, 62, ToTable, key: "Table");
            }
            else
            {
                var st = St(At); int mine = marks.Count(m => m.station == At); bool rec = CanMark;
                s.Text($"{st.id} {st.name}", 34, y, Size.Label, Ink, true);
                s.Text(preview != null ? L("PLAN · LOOKING AHEAD", "PLAN · 下見") : L($"FIELD · TASK {Mathf.Min(taskIndex + 1, M.tasks.Length)}/{M.tasks.Length}", $"FIELD · 作業 {Mathf.Min(taskIndex + 1, M.tasks.Length)}/{M.tasks.Length}"), PW - 34, y, Size.Note, Gray, true, 1);
                y += 58;
                if (preview != null)
                {   // looking ahead from the briefing: what is planned here, and nothing to do yet
                    s.Text(L("PLANNED HERE", "ここでの予定"), 34, y, Size.Label, Gray, true); y += 40;
                    foreach (var t in M.tasks.Where(t => t.station == st.id).Take(5))
                    {
                        s.Text(MLData.Hm(t.startMin), 34, y, Size.Note, t.index < taskIndex ? Dim : Gray); s.Text(T(t.phase), 100, y, Size.Label, t.index < taskIndex ? Dim : Ink, t.index == taskIndex);
                        s.Text(L(t.plannedMin + " min", t.plannedMin + " 分"), PW - 34, y, Size.Note, Gray, false, 1); y += 34;
                    }
                    y = s.Wrap(st.hasSample ? L($"Looking ahead from the briefing. The Marswalk is at {here} {St(here).name}. Views framed here with the camera go to the observation plan.", $"ブリーフィングからの下見です。Marswalk は {here} {St(here).name} にいます。ここでカメラで決めた構図は、観測計画に入ります。")
                                            : L($"Looking ahead from the briefing. The Marswalk is at {here} {St(here).name}; nothing is recorded here yet.", $"ブリーフィングからの下見です。Marswalk は {here} {St(here).name} にいます。ここではまだ何も記録されません。"), 34, y + 20, IW, 27, Size.Note, Soft);
                }
                else if (Finished)
                {
                    s.Text(L("All tasks complete", "全作業が完了"), 34, y, Size.Heading, Ink, true);
                    y = s.Wrap(L("Go back to the table to review what was recorded.", "机へ戻って、記録を振り返れます。"), 34, y + 42, IW, 31, Size.Label, Soft);
                }
                else if (Tk.station == here && Tk.leg < 0)
                {   // the task that is due here: its checks are what this panel is for
                    s.Text(L($"TASK · {Tk.plannedMin} MIN", $"作業 · {Tk.plannedMin} 分"), 34, y, Size.Label, Gray, true); y += 46;
                    y = s.Wrap(T(Tk.phase), 34, y, IW, 40, Size.Heading, Ink, true) - 14;
                    for (int i = 0; i < Tk.checks.Length; i++)
                    {
                        bool on = done.Contains(i); float y0 = y; int ci = i;
                        s.Box(36, y + 6, 34, 34, 8, on ? Cyan : new Color(0, 0, 0, 0), on ? Cyan : Hex("8b9097")); if (on) s.Check(36, y + 6, 34, OnCyan);
                        y = s.Wrap(T(Tk.checks[i]), 88, y + 33, PW - 126, 36, Size.Body, on ? Hex("8b9097") : Ink) - 2;
                        s.Hit("check:" + i, 28, y0 - 2, PW - 56, y - y0, () => ToggleCheck(ci), 10);
                    }
                    y += 26;
                    if (rec) y = s.Sourced(L("SIMULATED RECORD", "模擬の記録"), Amber, mine > 0 ? L($"{mine} observation region{(mine > 1 ? "s" : "")} · need local verification", $"観測領域 {mine} 件 · 現地での確認が必要")
                        : rig.xr ? L("point and pinch at a region to investigate · needs local verification", "調べたい領域を指してピンチ · 現地での確認が必要") : L("point and click at a region to investigate · needs local verification", "調べたい領域を指してクリック · 現地での確認が必要"), 34, y, IW);
                }
                else
                {
                    var b = BearingFromHere(Tk.station); bool seen = st.sees.Contains(Tk.station);
                    s.Text(L("NEXT", "次"), 34, y, Size.Label, Gray, true); y += 46;
                    y = s.Wrap($"{T(Tk.phase)} · {St(Tk.station).name}", 34, y, IW, 40, Size.Heading, Ink, true);
                    y = s.Wrap(L($"{b.d:0} m straight · bearing {MLData.Deg3(b.az)}° · station target predicted {(seen ? "clear of terrain" : "behind terrain")} (model)", $"直線 {b.d:0} m · 方位 {MLData.Deg3(b.az)}° · 目標点は{(seen ? "地形に隠れない" : "地形の陰")}と予測(モデル)"), 34, y, IW, 36, Size.Body, seen ? Ink : Soft);
                    var leg = M.legs.FirstOrDefault(l => (l.from == here && l.to == Tk.station) || (l.to == here && l.from == Tk.station));
                    if (leg != null) y = s.Sourced(L("OUR CALCULATION", "こちらの計算"), Cyan, L($"planned route {leg.distM:0} m, steepest {leg.slopeMaxDeg:0}°, about {Mathf.RoundToInt(leg.estWalkMin)} min at {M.walkSpeed} m/s (assumed)",
                                                                                                $"計画ルート {leg.distM:0} m、最大傾斜 {leg.slopeMaxDeg:0}°、約 {Mathf.RoundToInt(leg.estWalkMin)} 分({M.walkSpeed} m/s は仮定)"), 34, y + 2, IW);
                }
                // where the picture comes from and what of it is ours; what NASA recorded at this stop
                y += 10;
                y = s.Sourced(L("NASA OBSERVATION", "NASA の観測"), Ink, $"Mastcam-Z, {st.panoSols} · {st.panoCredit}", 34, y, IW);
                y = s.Sourced(L("ADDED BY US", "こちらで追加"), Cyan, L("its direction, the sky above it, the fill colour and metre grid where it has no pixels", "画像の向き、画像より上の空、画素の無い部分の色と 1 m 格子"), 34, y, IW);
                if (st.hasSample) y = s.Sourced(L("NASA RECORD", "NASA の記録"), Ink, L($"rock cores {st.sampleName} were taken within about {Mathf.CeilToInt(st.sampleDistM)} m of this stop", $"岩石コア {st.sampleName} を、この停止点から約 {Mathf.CeilToInt(st.sampleDistM)} m 以内で採取"), 34, y, IW);
                if (st.id == M.medaStation) s.Sourced(L("NASA OBSERVATION", "NASA の観測"), Ink, MedaLine, 34, y, IW);

                if (preview != null)
                {
                    s.Button(L("Back to the table", "机へ戻る"), 34, yb, IW, 62, ToTable, primary: true, px: Size.Body, key: "Back to the table");
                    string ph = st.id; s.Button(L("Step out to terrain", "地形モデルへ出る"), 34, yb + 76, IW, 62, () => EnterTerrain(ph, 1), key: "Step out to terrain");
                }
                else
                {
                    bool go = !Finished && Tk.station != here; float cw = rec && mine > 0 ? 350 : IW; string to = Tk.station;
                    if (Finished) s.Button(L("Review at the table", "机で振り返る"), 34, yb, IW, 62, ToTable, primary: true, px: Size.Body, key: "Review at the table");
                    else if (go) s.Button(L($"Walk to {to} {St(to).name}", $"{to} {St(to).name} へ歩く"), 34, yb, IW, 62, () => Travel(to), primary: true, px: Size.Body, key: "Walk to " + to);
                    else s.Button(L("Complete task", "作業を完了"), 34, yb, cw, 62, CompleteTask, done.Count == Tk.checks.Length, px: Size.Body, key: "Complete task");
                    if (!Finished && !go && rec && mine > 0) s.Button(L("Undo mark", "印を取り消す"), 34 + cw + 14, yb, IW - cw - 14, 62, UndoMark, key: "Undo mark");
                    string h = here; s.Button(L("Table", "机へ"), 34, yb + 76, 170, 62, ToTable, key: "Table"); s.Button(L("Step out to terrain", "地形モデルへ出る"), 220, yb + 76, 346, 62, () => EnterTerrain(h, 1), key: "Step out to terrain");
                }
            }
            s.Text(L("Hypothetical Marswalk · data: NASA", "仮想の Marswalk · データ: NASA"), 34, PH - 22, Size.Note, Dim);
        }

        // The clock on the head-up strip. It says "plan": it is where the timeline stands, not time that has passed.
        static string HudClock(float min) => L("PLAN T+ ", "計画 T+ ") + MLData.Hm(min);

        void DrawHud()
        {
            if (hud == null || OnTable) return;
            var s = hud; s.Clear(); var Tk = Task; float W = s.w; bool fin = Finished; string top, main, sub; var col = Ink; string target = Target;
            if (stage == Stage.Field && preview != null)
            {
                var st = St(preview); var here2 = M.tasks.Where(t => t.station == st.id && t.leg < 0).ToArray();
                top = L($"LOOKING AHEAD · {st.id} {st.name}", $"下見 · {st.id} {st.name}"); main = L("What you will see on arrival", "到着時に見える景色");
                sub = L($"{here2.Length} task{(here2.Length == 1 ? "" : "s")} planned here, {here2.Sum(t => t.plannedMin)} min · the Marswalk is at {here} {St(here).name}",
                        $"ここでの予定は {here2.Length} 作業、合計 {here2.Sum(t => t.plannedMin)} 分 · Marswalk は {here} {St(here).name}");
                if (CanCamera) sub = L("Open the camera to frame a view for the observation plan", "カメラを開いて、観測計画のための構図を決められます");
            }
            else if (stage == Stage.Field)
            {
                var st = St(here); top = $"{st.id} {st.name}";
                if (fin) { main = L("All tasks complete", "全作業が完了"); sub = L("Go back to the table to review the Marswalk", "机へ戻って Marswalk を振り返れます"); }
                else if (Tk.station == here && Tk.leg < 0)
                {   // what the hands do in this task is the headline; the task's name goes above it and the bookkeeping under it
                    top += " · " + ShortPhase(Tk); int got = marks.Count(m => m.station == here);
                    string progress = L($"{done.Count}/{Tk.checks.Length} checks", $"確認 {done.Count}/{Tk.checks.Length}");
                    if (Tk.recordCheck >= 0)
                    {
                        main = rig.xr ? L("Point at a region to investigate and pinch", "調べたい領域を指して、ピンチ") : L("Point at a region to investigate and click", "調べたい領域を指して、クリック");
                        sub = L($"{got} recorded · {progress} · simulated, needs local verification", $"記録 {got} 件 · {progress} · 模擬・現地での確認が必要");
                    }
                    else
                    {
                        main = L("Check the items, then complete the task", "確認項目を確かめて、作業を完了");
                        sub = (rig.xr ? L("open your left thumb for the panel · ", "左手の親指を開くとパネル · ") : L("the items are on the panel · ", "確認項目はパネルに · ")) + progress;
                    }
                }
                else
                {
                    var b = BearingFromHere(Tk.station); bool seen = st.sees.Contains(Tk.station); main = L("Next: ", "次: ") + $"{Tk.station} {St(Tk.station).name}"; col = Ink;
                    sub = L($"{Tk.phase} · {b.d:0} m · bearing {MLData.Deg3(b.az)}° · target {(seen ? "clear" : "behind terrain")} (model)", $"{T(Tk.phase)} · {b.d:0} m · 方位 {MLData.Deg3(b.az)}° · 目標は{(seen ? "隠れない" : "地形の陰")}(モデル)");
                }
            }
            else
            {
                var n = Nearest(); top = stage == Stage.Giant ? L("OVERLOOK · TERRAIN MODEL 1 : 25", "地形を見渡す · 地形モデル 1 : 25") : L("TERRAIN MODEL · TRUE SCALE", "地形モデル · 実寸");
                if (checkpoint != null) top = L("ROUTE CHECK · TERRAIN MODEL 1 : 1", "ルートの確認 · 地形モデル(実寸)");
                main = travelling != null ? L($"Walking to {travelling} {St(travelling).name}", $"{travelling} {St(travelling).name} へ移動中") : L("Orbital data only", "軌道からのデータのみ");
                sub = L($"Nearest station: {n.id} {St(n.id).name}, {n.d:0} m", $"最も近い地点: {n.id} {St(n.id).name}、{n.d:0} m");
            }
            if (checkpoint != null)
            {   // stopped on the way, at the steepest ground of this leg: to be looked at and confirmed before going on
                var cp = checkpoint; col = SlopeClass(cp.slope); if (col.a == 0) col = Ink;
                main = L($"Steepest ground on this leg: {cp.slope:0}°", $"この区間でいちばん急な所: {cp.slope:0}°");
                sub = (cp.ahead >= 2 ? L($"{cp.ahead:0} m ahead", $"{cp.ahead:0} m 先") : L("around here", "このあたり"))
                    + L($" · {cp.toGo:0} m to go · colours = slope classes (our calculation)", $" · 残り {cp.toGo:0} m · 色 = 傾斜の区分(こちらの計算)");
            }
            bool cam = CanCamera;
            if (cam && camOn)
            {   // the camera is up: what the frame is, and what the next photo will be for
                string purpose = CamPurpose(out var fromTask); col = Ink;
                main = purpose.Length == 0 ? L("Choose what this view is for", "この構図の目的を選ぶ") : L("Frame what you need to document", "記録が必要なものを構図に収める");
                sub = purpose.Length == 0 ? L("the purposes are under the frame · past panorama, not a new observation", "目的は枠の下 · 過去のパノラマで、新しい観測ではありません")
                    : L("Purpose: ", "目的: ") + Word(purpose) + (fromTask != null ? L(" · from the task", " · タスクから") : L(" · chosen here", " · ここで選択")) + (rig.xr ? L(" · pinch to capture", " · ピンチで撮影") : L(" · click to capture", " · クリックで撮影"));
            }
            if (flashMsg.Length > 0) sub = flashMsg;
            // Left: where you are, then what to do now, large. Right: the step that is due as one button, then the panel and the table;
            // at an outcrop of the observation plan, the camera beside them.
            float R = W - (cam ? 440 : 304), bx = W - (cam ? 416 : 280);
            s.Box(0, 0, W, s.h, 26, new Color(10 / 255f, 12 / 255f, 16 / 255f, .88f));
            if (!rig.xr) s.Hit("hud", 0, 0, W, s.h, ToggleWrist, 26, true);
            s.Text(top, 40, 48, Size.Label, Gray, true);
            hudClock = s.Text(HudClock(Pet), R - 124, 48, Size.Note, Gray, false, 1);
            hudHeading = s.Text($"HDG {MLData.Deg3(HeadingNow)}°", R, 48, Size.Note, Gray, false, 1);
            s.Text(main, 40, 104, Size.Heading, col, true); hudSub = s.Text(sub, 40, 146, Size.Label, flashMsg.Length > 0 ? Ink : Soft);
            hudSide = TurnSide();   // which way to turn when the next station is outside the view
            if (hudSide != 0) s.Text(hudSide < 0 ? $"« {target}" : $"{target} »", R, 104, Size.Heading, Ink, true, 1);
            s.Box(bx - 12, 18, 1.5f, s.h - 36, 1, new Color(1, 1, 1, .14f));
            if (cam) s.Button(camOn ? L("Camera on", "カメラ中") : L("Camera", "カメラ"), bx + 264, 20, 124, 128, ToggleCamera, camOn, px: Size.Note, key: "Camera");
            string act = null; System.Action fn = null; bool on = false;
            if (stage == Stage.Field && preview != null) { act = L("Back to the table", "机へ戻る"); fn = ToTable; }
            else if (stage == Stage.Field && fin) { act = L("Review at the table", "机で振り返る"); fn = ToTable; }
            else if (stage == Stage.Field && Tk.station != here) { string to = Tk.station; act = L("Walk to " + to, to + " へ歩く"); fn = () => Travel(to); }
            else if (stage == Stage.Field) { act = L("Complete task", "作業を完了"); fn = CompleteTask; on = done.Count == Tk.checks.Length; }
            else if (checkpoint != null) { act = L("Confirmed · go on", "確認して進む"); fn = ConfirmCheckpoint; on = true; }
            else if (travelling == null) { string nid = Nearest().id; act = L("Surface image at " + nid, nid + " の実画像へ"); fn = () => ToField(nid); }
            // a long label is written one step smaller rather than run out of its button
            if (act != null) s.Button(act, bx, 20, 252, 60, fn, on, primary: true, px: s.Measure(act, Size.Label, true) <= 232 ? Size.Label : Size.Note, key: "HudAction");
            else s.Button(L("Walking…", "移動中…"), bx, 20, 252, 60, () => { }, dim: true);
            s.Button(wristUp ? L("Close", "閉じる") : L("Panel", "パネル"), bx, 92, 120, 56, ToggleWrist, wristUp, key: "Panel");
            s.Button(L("Table", "机へ"), bx + 132, 92, 120, 56, ToTable, key: "HudTable");
        }
    }
}
