using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static MissionLens.MLPanel;
using static MissionLens.MLText;

namespace MissionLens
{
    // Things that carry no data of their own and only help the eye: the opening in which the model spreads out,
    // the model's base, the sky behind it all, station markers, the comfort vignette, and the short messages
    // shown on arrival. None of it is an observation; the sky, haze and markers are drawn by us.
    public partial class MLApp
    {
        class RippleFx { public Transform t; public Material m; public float t0, dur, size; }

        float frontR, routeDraw, plateK, tableBaseO = 1, skirtO = 1, revealMax, vig;
        Transform plateT, skyT, vigT, studioT, floorT, plinthT;
        Material skirtMat, plateMat, frameMat, skyMat, vigMat, beaconMat, reticleMat, floorMat, plinthMat;
        static readonly Color StudioHorizon = new Color(.105f, .118f, .14f), StudioZenith = new Color(.03f, .035f, .045f), StudioBelow = new Color(.05f, .058f, .07f),
            MarsHorizon = new Color(.83f, .68f, .50f), MarsZenith = new Color(.60f, .48f, .36f), MarsBelow = new Color(.20f, .145f, .105f);
        MeshRenderer skirtR, skyR, vigR, beaconR, reticleR, gazeR; Material gazeMat;
        Shader ringShader;
        readonly List<RippleFx> ripples = new List<RippleFx>();
        float bannerUntil; bool bannerHasButton;

        static Shader Sh(string name) => Resources.Load<Shader>("MLShaders/" + name);

        void BuildFx()
        {
            // The tabletop model's base: walls down to a plate, and a light edge around the plate. The walls follow
            // whatever part of the terrain is on the table (refilled in MLAppTable.cs); the plate belongs to the table.
            skirtMat = new Material(Sh("MLSkirt")); skirtMesh = new Mesh { name = "Model walls" }; skirtMesh.MarkDynamic();
            skirtR = Add("Model walls", skirtMesh, skirtMat, world);
            plateT = new GameObject("Model base").transform; plateT.SetParent(tableFrame, false);
            plateMat = ColorMat(Color.black, 2450); frameMat = ColorMat(Color.white, 2451);
            Add("Plate", MLMesh.Box(new Vector3(FootHx * 2 + .03f, .004f, FootHz * 2 + .03f)), plateMat, plateT).transform.localPosition = new Vector3(0, -.002f, 0);
            Add("Plate edge", MLMesh.Frame(FootHx + .015f, FootHz + .015f, .0022f), frameMat, plateT).transform.localPosition = new Vector3(0, .0004f, 0);

            // The opening spreads from the starting station to the farthest corner of what is on the table.
            var b = St("BASE");
            foreach (var t in new[] { new Vector2(-FootHx, -FootHz), new Vector2(FootHx, -FootHz), new Vector2(FootHx, FootHz), new Vector2(-FootHx, FootHz) })
                revealMax = Mathf.Max(revealMax, Vector2.Distance(new Vector2(b.x, b.z), TableToMap(t, tvDefault)));

            // The studio room: a floor with a faint grid and a plinth under the model, for when the real room is
            // not a good backdrop (and on a desktop, where there is no real room).
            studioT = new GameObject("Studio room").transform; studioT.SetParent(tableFrame, false);
            floorMat = new Material(Sh("MLFloor")); floorMat.SetFloat("_Size", 16);
            floorMat.SetColor("_Color", new Color(.075f, .075f, .08f)); floorMat.SetColor("_Line", new Color(.5f, .5f, .52f));   // a neutral floor with a faint grid: no cyan, which is for what can be chosen
            floorT = Add("Floor", MLMesh.Quad(true), floorMat, studioT).transform; floorT.localScale = new Vector3(16, 1, 16);
            plinthMat = ColorMat(new Color(.055f, .062f, .074f, 1), 2400);   // after the room's walls (2399), which are after its floor (2398): none of them writes depth
            plinthT = Add("Plinth", MLMesh.Box(Vector3.one), plinthMat, studioT).transform;
            // For a recording in the Editor (and on a desktop) the studio room also has walls: a plain room about five
            // metres across, so that a film shows the model on its plinth in a room, at the size it has for the person,
            // and shows that room give way to Mars and come back. It is a room of ours, drawn. On the headset the studio
            // room stays a floor and a plinth, and the real room (passthrough) is untouched.
            if (!rig.xr || rig.sim) BuildRoomWalls();

            skyMat = new Material(Sh("MLSky")); skyR = Add("Sky", MLMesh.PanoSphere(90, -90, 5000, 90), skyMat, null); skyT = skyR.transform;
            vigMat = new Material(Sh("MLVignette")); vigR = Add("Vignette", MLMesh.PanoSphere(90, -90, .5f, 90), vigMat, null); vigT = vigR.transform;

            // Station markers on the ground, and how far along the route each station is (for the opening).
            ringShader = Sh("MLRing"); float along = 0;
            foreach (var l in M.legs)
            {
                for (int i = 2; i < l.route.Length; i += 2) along += Vector2.Distance(new Vector2(l.route[i], l.route[i + 1]), new Vector2(l.route[i - 2], l.route[i - 1]));
                var pin = pins.First(p => p.st.id == l.to); if (pin.st.id != "BASE") pin.routeM = along;
            }
            foreach (var p in pins)
            {
                p.ring = new Material(ringShader); p.ring.SetFloat("_Fill", .28f);
                p.ringT = Add("Marker " + p.st.id, MLMesh.Quad(true), p.ring, world).transform;
                p.leader = ColorMat(Color.white, 3990, true); p.leaderT = Add("Leader " + p.st.id, MLMesh.Quad(false), p.leader, null).transform;
            }
            beaconMat = new Material(Sh("MLBeam")); beaconR = Add("Beacon", MLMesh.Cross(), beaconMat, world); beaconR.enabled = false;
            reticleMat = new Material(ringShader); reticleMat.SetColor("_Color", Ink); reticleMat.SetFloat("_Radius", .62f); reticleMat.SetFloat("_Width", .1f); reticleMat.SetFloat("_Fill", .35f);
            reticleR = Add("Reticle", MLMesh.Quad(false), reticleMat, null); reticleR.enabled = false; reticleR.sortingOrder = MLRig.OverPanels;
            gazeMat = new Material(ringShader); gazeMat.SetFloat("_Radius", .6f); gazeMat.SetFloat("_Width", .16f); gazeMat.renderQueue = 4005;
            gazeR = Add("Gaze dot", MLMesh.Quad(false), gazeMat, null); gazeR.enabled = false; gazeR.sortingOrder = MLRig.OverPanels;
        }

        // ---------- the opening ----------
        public void Begin() { if (stage == Stage.Title && !busy) StartCoroutine(CoIntro()); }
        IEnumerator CoIntro()
        {
            busy = true; sfx.Play("intro");
            if (rig.xr || !placed) PlaceTable();
            stage = Stage.Table; view = TableView(); ApplyView(); Refresh();
            if (!rig.xr) StartCoroutine(Look(3.2f, tableFrame.eulerAngles.y, TableLookPitch));
            yield return Tween(.6f, k => plateK = EaseOut(k));
            yield return Tween(3.3f, k =>
            {
                frontR = Mathf.Max(.01f, Ease(Mathf.Clamp01(k / .82f)) * (revealMax + 140));
                float d = Mathf.Clamp01((k - .34f) / .62f); routeDraw = Ease(d) * routeTotal;
                foreach (var p in pins) if (!p.shown && k > .34f && p.routeM <= routeDraw) { p.shown = true; sfx.Play("toggle", .45f); }
            });
            frontR = 1e6f; routeDraw = 1e9f; introDone = true; foreach (var p in pins) p.shown = true;
            sfx.Play("ready"); busy = false; TaskLayer(true); Refresh(); DrawMenu();
        }

        // ---------- per frame ----------
        void UpdateFx(float dt)
        {
            var head = rig.Head.position; float S = world.localScale.x; var b = St("BASE");
            Shader.SetGlobalVector("_MLReveal", new Vector4(frontR, baseY + BaseMargin(tv), b.x, -b.z));
            Shader.SetGlobalFloat("_MLRouteDraw", routeDraw); Shader.SetGlobalFloat("_MLInvS", 1 / S);
            terrainR.enabled = frontR > 0;

            tableBaseO = Mathf.MoveTowards(tableBaseO, OnTable && clipG < .3f ? 1 : 0, dt * 3);   // the table's plate waits until the model is nearly down: before that it would hide it
            bool baseOn = tableBaseO > .01f && plateK > 0;
            plateT.gameObject.SetActive(baseOn); plateT.localScale = new Vector3(plateK, 1, plateK);
            plateMat.SetColor("_Color", new Color(.035f, .04f, .048f, tableBaseO)); frameMat.SetColor("_Color", new Color(1, 1, 1, .32f * tableBaseO));
            skirtO = Mathf.MoveTowards(skirtO, OnTable && clipG < .05f ? 1 : 0, dt * 4);   // the side walls come once the model has landed
            skirtR.enabled = skirtO > .01f && frontR > 0; skirtMat.SetFloat("_A", skirtO * tableBaseO);

            // The sphere behind everything is the dissolve between rooms: clear for the real room, dark for the studio
            // room, and the Mars sky once the table has been left.
            float realRoom = roomO * (1 - studioK), skyO = 1 - realRoom, mars = Ease(1 - roomO);
            skyR.enabled = skyO > .003f; skyMat.SetFloat("_Opacity", Ease(skyO)); skyT.position = head;
            skyMat.SetColor("_Horizon", Color.Lerp(StudioHorizon, MarsHorizon, mars)); skyMat.SetColor("_Zenith", Color.Lerp(StudioZenith, MarsZenith, mars)); skyMat.SetColor("_Below", Color.Lerp(StudioBelow, MarsBelow, mars));
            float studioA = Ease(roomO) * studioK; bool studioOn = studioA > .01f && placed;
            if (studioT.gameObject.activeSelf != studioOn) studioT.gameObject.SetActive(studioOn);
            if (studioOn) foreach (var (wm, wc) in roomMats) wm.SetColor("_Color", new Color(wc.r, wc.g, wc.b, studioA));   // the walls go with the room, as the floor does
            if (studioOn) { floorMat.SetFloat("_A", studioA); var pc = plinthMat.GetColor("_Color"); pc.a = studioA * Mathf.Clamp01(1 - clipG * 3); plinthMat.SetColor("_Color", pc); }   // the plinth too waits for the model to come down
            vig = Mathf.MoveTowards(vig, (rig.xr && !rig.sim && moving) || debugVignette ? 1 : 0, dt * 3.5f);   // a comfort aid for the headset; not drawn in an Editor recording
            vigR.enabled = vig > .01f; vigMat.SetFloat("_Amount", vig * .94f); vigT.position = head;

            // Route ribbons: half a metre wide on the ground at true scale; on the table about 5.6 mm wide and lifted
            // 4 mm at any stretch of the map, with dashes that keep their size and pace on the table.
            foreach (var o in legs)
            {
                o.mat.SetFloat("_HalfWidth", Mathf.Max(.25f, .0028f / S)); o.mat.SetFloat("_Lift", Mathf.Max(.3f, .0039f / S));
                o.mat.SetFloat("_Dash", Mathf.Max(3.2f, .046f / S)); o.mat.SetFloat("_Speed", Mathf.Max(1.6f, .025f / S) * (o.back ? -1 : 1));
            }

            // Ground rings: big enough to find on the table, about a pace wide at true scale.
            float rr = Mathf.Clamp(.014f / S, 1.3f, 8f); string target = Target;
            foreach (var p in pins)
            {
                bool atIt = !OnTable && Vector2.Distance(new Vector2(view.ax, view.az), new Vector2(p.st.x, p.st.z)) < 6;
                bool show = p.shown && !atIt; if (p.ringT.gameObject.activeSelf != show) p.ringT.gameObject.SetActive(show);
                if (!show) continue;
                p.ringT.localPosition = D.Local(p.st.x, p.st.z, .3f + rr * .1f); p.ringT.localScale = Vector3.one * rr * 4.76f;   // the ring sits at 0.42 of the quad's half width
                bool key = OnTable ? p.st.id == sel : p.st.id == target;
                var c = key && OnTable ? Cyan : Color.white; c.a = key ? 1 : OnTable ? .8f : .5f;
                p.ring.SetColor("_Color", c); p.ring.SetFloat("_Pulse", key ? 1 : 0); p.ring.SetFloat("_OnTable", OnTable ? 1 : 0);
            }

            // An upright marker over the station to go to next, visible through terrain.
            bool beacon = !OnTable && target != null;
            beaconR.enabled = beacon;
            if (beacon)
            {
                var s = St(target); float w = Mathf.Clamp(.02f / S, .8f, 3f), h = Mathf.Clamp(1.2f / S, 40f, 70f);
                beaconR.transform.localPosition = D.Local(s.x, s.z); beaconR.transform.localScale = new Vector3(w, h, w);
                // It is there to be found from a distance. Coming up to it, it thins away, and is gone by the time you
                // arrive, instead of standing in front of your face.
                float away = Vector2.Distance(new Vector2(view.ax, view.az), new Vector2(s.x, s.z));
                var bc = Ink; bc.a = Ease(Mathf.InverseLerp(12, 55, away)); beaconMat.SetColor("_Color", bc);
            }

            for (int i = ripples.Count - 1; i >= 0; i--)
            {
                var r = ripples[i]; float k = (Time.time - r.t0) / r.dur;
                if (k >= 1) { Destroy(r.t.gameObject); Destroy(r.m); ripples.RemoveAt(i); continue; }
                r.m.SetFloat("_T", Mathf.Max(.002f, EaseOut(k)));
            }
            if (restartArmedUntil > 0 && Time.time > restartArmedUntil) { restartArmedUntil = 0; Refresh(); }
        }

        Transform wallsT; readonly List<(Material m, Color c)> roomMats = new List<(Material, Color)>();
        void BuildRoomWalls()
        {
            wallsT = new GameObject("Walls").transform; wallsT.SetParent(studioT, false);
            const float hx = 2.7f, zBack = 2.4f, zFront = -3.4f, hgt = 2.8f; float zc = (zBack + zFront) / 2, len = zBack - zFront;
            // drawn before everything else in the room (they are the farthest things in it) and after the floor, which runs on under them
            void Wall(string name, Vector3 size, Vector3 at, Color c) { var m = ColorMat(c, 2399); roomMats.Add((m, c)); Add(name, MLMesh.Box(size), m, wallsT).transform.localPosition = at; }
            // each wall a little different, as plain walls are under one soft light: a room in a single flat colour would have no corners
            Wall("Back wall", new Vector3(hx * 2, hgt, .06f), new Vector3(0, hgt / 2, zBack), new Color(.118f, .124f, .136f));
            Wall("Left wall", new Vector3(.06f, hgt, len), new Vector3(-hx, hgt / 2, zc), new Color(.086f, .091f, .102f));
            Wall("Right wall", new Vector3(.06f, hgt, len), new Vector3(hx, hgt / 2, zc), new Color(.098f, .103f, .114f));
            Wall("Front wall", new Vector3(hx * 2, hgt, .06f), new Vector3(0, hgt / 2, zFront), new Color(.074f, .079f, .089f));
            var skirting = new Color(.2f, .205f, .215f);   // where the walls meet the floor
            Wall("Skirting back", new Vector3(hx * 2, .09f, .02f), new Vector3(0, .045f, zBack - .045f), skirting);
            Wall("Skirting left", new Vector3(.02f, .09f, len), new Vector3(-hx + .045f, .045f, zc), skirting);
            Wall("Skirting right", new Vector3(.02f, .09f, len), new Vector3(hx - .045f, .045f, zc), skirting);
        }

        // The studio room's floor lies on the real floor (height 0) and its plinth reaches from there to the model.
        void PlaceStudio()
        {
            float h = Mathf.Max(.05f, tableFrame.position.y - .004f);
            floorT.localPosition = new Vector3(0, -tableFrame.position.y, 0); if (wallsT != null) wallsT.localPosition = floorT.localPosition;
            plinthT.localScale = new Vector3(FootHx * 2 + .03f, h, FootHz * 2 + .03f); plinthT.localPosition = new Vector3(0, -.004f - h / 2, 0);
        }
        public void ToggleStudio() { studio = !studio; sfx.Play("toggle", .7f); Log("room " + (studio ? "studio" : "passthrough")); DrawMenu(); }

        /// <summary>A ring that spreads once from a point on the ground (where a mark was placed).</summary>
        void Ripple(Vector3 at, Color col, float size)
        {
            var m = new Material(ringShader); m.SetColor("_Color", col); m.SetFloat("_Radius", 0); m.SetFloat("_Width", 0); m.SetFloat("_T", .002f);
            var t = Add("Ripple", MLMesh.Quad(false), m, null).transform; t.position = at; t.localScale = Vector3.one * size;
            t.rotation = Quaternion.LookRotation(at - rig.Head.position, Vector3.up);   // facing the person, so it reads at any distance
            ripples.Add(new RippleFx { t = t, m = m, t0 = Time.time, dur = .9f, size = size });
        }

        // Where the pointer meets the terrain while a candidate can be marked.
        void PlaceReticle(Vector3 at, float distance)
        {
            reticleR.enabled = true; var t = reticleR.transform; t.position = at;
            t.rotation = Quaternion.LookRotation(at - rig.Head.position, Vector3.up); t.localScale = Vector3.one * Mathf.Clamp(distance * .035f, .03f, 4f);
        }
        void HideReticle() { if (reticleR.enabled) reticleR.enabled = false; }

        // Where the person is looking, when looking is the pointer. It fills as the wait to press runs out.
        void PlaceGazeDot(Vector3 at, float distance, float dwell)
        {
            gazeR.enabled = true; var t = gazeR.transform; t.position = at;
            t.rotation = Quaternion.LookRotation(at - rig.Head.position, Vector3.up); t.localScale = Vector3.one * distance * (.022f + .012f * dwell);
            gazeMat.SetColor("_Color", Color.Lerp(new Color(1, 1, 1, .75f), Cyan, dwell)); gazeMat.SetFloat("_Fill", dwell * 2);
        }
        void HideGazeDot() { if (gazeR.enabled) gazeR.enabled = false; }

        // ---------- short messages in front of the person ----------
        void ShowBanner(string top, string main, string sub, float seconds, string button = null, Action fn = null)
        {
            var s = banner; s.Clear(); float W = s.w, H = s.h; bannerHasButton = button != null;
            float bh = bannerHasButton ? H : 250, y0 = (H - bh) / 2;
            s.Box(0, y0, W, bh, 30, new Color(8 / 255f, 10 / 255f, 14 / 255f, .9f), new Color(1, 1, 1, .2f));
            s.Text(top, W / 2, y0 + 62, Size.Label, Soft, true, 0); s.Text(main, W / 2, y0 + 138, Size.Display, Ink, true, 0);
            s.Wrap(sub, W / 2, y0 + 192, W - 120, 32, Size.Label, Soft, false, 0);
            if (bannerHasButton) s.Button(button, W / 2 - 210, y0 + bh - 104, 420, 72, fn, primary: true, px: Size.Body, key: "Review at the table");
            if (rig.xr)
            {   // fixed in the room where the person is looking now
                var yaw = Quaternion.Euler(0, rig.HeadYaw, 0); var at = rig.Head.position + yaw * new Vector3(0, bannerHasButton ? -.05f : .1f, 1.5f);
                s.go.transform.SetPositionAndRotation(at, Quaternion.LookRotation(at - rig.Head.position, Vector3.up));
            }
            s.Show = true; bannerUntil = seconds > 0 ? Time.time + seconds : 0;
        }
        void HideBanner() { if (banner != null) { banner.Show = false; bannerHasButton = false; } }

        void Arrived(string id)
        {
            if (Finished) { MissionComplete(); return; }
            var st = St(id); sfx.Play("arrive");
            ShowBanner(preview != null ? L($"LOOKING AHEAD AT STATION {st.id}  ·  NASA OBSERVATION", $"地点 {st.id} の下見  ·  NASA の観測") : L($"STATION {st.id}  ·  NASA OBSERVATION", $"地点 {st.id}  ·  NASA の観測"), st.name,
                L($"Mastcam-Z panorama, {st.panoSols} · {st.panoCredit} · seen from the rover's mast, about 2 m above the ground", $"Mastcam-Z パノラマ、{st.panoSols} · {st.panoCredit} · ローバーのマスト(地面から約 2 m)から見た景色"), 3.6f);
        }
        void MissionComplete()
        {
            sfx.Play("complete"); int n = marks.Count;
            ShowBanner(L("HYPOTHETICAL MARSWALK  ·  SIMULATED RUN", "仮想の Marswalk  ·  模擬の実行"), L("Marswalk complete", "Marswalk 完了"),
                L($"{M.tasks.Length} tasks stepped through · {M.routeTotalM:0} m on foot · {(n == 0 ? "no candidates" : n == 1 ? "1 candidate" : n + " candidates")} recorded", $"{M.tasks.Length} 作業を実施 · 徒歩 {M.routeTotalM:0} m · 候補の記録 {n} 件"), 0, L("Review at the table", "机で振り返る"), ToTable);
        }
    }
}
