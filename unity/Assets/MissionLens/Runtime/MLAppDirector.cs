#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using Unity.Collections;
using UnityEngine;
using UnityEngine.XR.Hands;
using static MissionLens.MLPanel;

namespace MissionLens
{
    // Records a demonstration in the Editor. A script plays the part of the person: it moves the simulated head and
    // hands (MLRigSim.cs) through a run of the app, and every frame is written to disk at a fixed rate together
    // with the sound. The app itself is not told what to do: the hands point, pinch and touch, and the app reacts
    // to them as it does to tracked hands on the headset. Editor only; nothing here is in the headset build.
    //
    //   PlayerPrefs "ml.sim" = 1, enter Play, then: MLApp.I.Direct("demo", "<folder>", "en")
    //   frames f_00000.jpg ... and audio.raw (32-bit float; rate and channels in audio.txt) appear in the folder.
    public partial class MLApp
    {
        MLRig.SimHand RH => rig.simHands[0];
        MLRig.SimHand LH => rig.simHands[1];
        /// <summary>What the director is doing, for whoever is waiting for it: a step name, then "done".</summary>
        public string directorStatus = "idle";
        bool recording; int recFrame;

        public void Direct(string scenario, string outDir, string language = "en") { StartCoroutine(CoDirect(scenario, outDir, language)); }

        // ---------- recording ----------
        IEnumerator CoRecord(string dir, int w, int h, int fps)
        {
            Directory.CreateDirectory(dir); foreach (var f in Directory.GetFiles(dir, "f_*.jpg")) File.Delete(f);
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 }; var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            int channels = AudioSettings.speakerMode == AudioSpeakerMode.Mono ? 1 : 2;
            File.WriteAllText(Path.Combine(dir, "audio.txt"), $"{AudioSettings.outputSampleRate} {channels} {fps}");
            var wav = new FileStream(Path.Combine(dir, "audio.raw"), FileMode.Create); var bytes = new byte[0];
            Time.captureFramerate = fps; AudioRenderer.Start(); recording = true; recFrame = 0;
            var endOfFrame = new WaitForEndOfFrame();
            while (recording)
            {
                yield return endOfFrame;
                var cam = rig.cam; cam.targetTexture = rt; cam.Render(); cam.targetTexture = null;
                var active = RenderTexture.active; RenderTexture.active = rt; tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); RenderTexture.active = active;
                File.WriteAllBytes(Path.Combine(dir, $"f_{recFrame:00000}.jpg"), tex.EncodeToJPG(94)); recFrame++;
                int n = AudioRenderer.GetSampleCountForCaptureFrame() * channels;
                using (var buf = new NativeArray<float>(n, Allocator.Temp))
                {
                    AudioRenderer.Render(buf);
                    if (bytes.Length != n * 4) bytes = new byte[n * 4];
                    Buffer.BlockCopy(buf.ToArray(), 0, bytes, 0, n * 4); wav.Write(bytes, 0, n * 4);
                }
            }
            AudioRenderer.Stop(); wav.Close(); Time.captureFramerate = 0; Destroy(rt); Destroy(tex);
        }

        // ---------- the person ----------
        static IEnumerator Pause(float s) { for (float t = 0; t < s; t += Time.deltaTime) yield return null; }
        static IEnumerator WaitFor(Func<bool> ok, float atMost = 15) { for (float t = 0; t < atMost && !ok(); t += Time.deltaTime) yield return null; }
        Quaternion BodyYaw => Quaternion.Euler(0, rig.simYawTo, 0);
        void Head(float yaw, float pitch, float speed = 3.2f) { rig.simYawTo = yaw; rig.simPitchTo = pitch; rig.simHeadSpeed = speed; }
        void HeadTo(Vector3 at, float speed = 3.2f, float lift = 0)
        {
            var d = at - rig.simHeadTo; Head(Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, Mathf.Asin(Mathf.Clamp(d.normalized.y, -1, 1)) * Mathf.Rad2Deg + lift, speed);
        }
        // A hand at rest in front of the body: low, fingers forward, the back of the hand up and a little outward.
        void Rest(int side, float speed = 6)
        {
            var h = rig.simHands[side]; float s = side == 0 ? 1 : -1;
            h.on = true; h.pinch = false; h.point = false; h.curlTo = .34f; h.indexTo = .24f; h.thumbTo = .3f; h.speed = speed;
            h.wristTo = rig.simHeadTo + BodyYaw * new Vector3(.21f * s, -.62f, .2f);
            h.rotTo = BodyYaw * Quaternion.LookRotation(new Vector3(-.25f * s, -.05f, 1), Vector3.up) * Quaternion.Euler(0, 0, -28 * s);
            h.aimTo = h.wristTo + BodyYaw * new Vector3(-.1f * s, -1, .12f);   // down at the floor by the feet: a hand at rest does not point at the map
        }
        // Raised to point at something far: the hand comes up in front of the shoulder and its ray goes to the target.
        float pointRaise;   // metres: how much higher the pointing hand is held (near a panel, so that it stays in clear view)
        void PointAt(int side, Vector3 target, float speed = 7)
        {
            var h = rig.simHands[side]; float s = side == 0 ? 1 : -1;
            h.on = true; h.point = false; h.curlTo = .36f; h.indexTo = .16f; h.thumbTo = .35f; h.speed = speed;
            h.wristTo = rig.simHeadTo + BodyYaw * new Vector3(.19f * s, -.33f + pointRaise, .34f);
            var to = (target - h.wristTo).normalized;
            h.rotTo = Quaternion.LookRotation(to + Vector3.up * .22f, Vector3.up) * Quaternion.Euler(0, 0, -34 * s); h.aimTo = target;
        }
        Btn FindButton(MLPanel p, string key) => p.buttons.Find(x => x.key == key) ?? p.buttons.Find(x => x.key != null && x.key.StartsWith(key));
        static Vector3 ButtonWorld(MLPanel p, Btn b) => p.go.transform.TransformPoint(b.x + b.w / 2 - p.w / 2f, p.h / 2f - (b.y + b.h / 2), 0);

        // Point at a button, wait until the app shows the ray resting on it, pinch. If `worked` is given and the press
        // has not had its effect a moment later (the panel was still moving, say), point and pinch again.
        IEnumerator PressRay(int side, MLPanel p, string key, float settle = .55f, Func<bool> worked = null)
        {
            var h = rig.simHands[side]; var ptr = side == 0 ? rig.right : rig.left; var b = FindButton(p, key);
            if (b == null) { Debug.LogWarning("[director] no button " + key); yield break; }
            for (int attempt = 0; attempt < 3; attempt++)
            {
                float on = 0;
                for (float t = 0; t < settle + 1.5f && (t < settle || on < .18f); t += Time.deltaTime)
                {
                    b = FindButton(p, key) ?? b; PointAt(side, ButtonWorld(p, b)); on = ptr.hoverKey == b.key ? on + Time.deltaTime : 0; yield return null;
                }
                h.pinch = true; for (float t = 0; t < .17f; t += Time.deltaTime) { PointAt(side, ButtonWorld(p, FindButton(p, key) ?? b)); yield return null; }
                h.pinch = false; for (float t = 0; t < .22f; t += Time.deltaTime) yield return null;
                if (worked == null) break;
                for (float t = 0; t < .5f && !worked(); t += Time.deltaTime) yield return null;
                if (worked()) break;
                Debug.LogWarning("[director] pressing " + key + " again");
            }
        }
        // Touch a button on a near panel with the right forefinger: only that finger out, in through the panel's face and back.
        IEnumerator TouchButton(MLPanel p, string key)
        {
            var h = RH; if (FindButton(p, key) == null) { Debug.LogWarning("[director] no button " + key); yield break; }
            h.on = true; h.pinch = false; h.point = true; h.speed = 9;
            IEnumerator Reach(float off, float seconds)
            {   // the wrist goes where it must for the fingertip to be `off` metres in front of the button
                for (float t = 0; t < seconds; t += Time.deltaTime)
                {
                    var tr = p.go.transform; var b = FindButton(p, key); if (b == null) yield break;
                    h.rotTo = Quaternion.LookRotation((tr.forward * 1f + tr.up * .45f - tr.right * .3f).normalized, (tr.up - tr.forward * .5f + tr.right * .5f).normalized);
                    h.wristTo = ButtonWorld(p, b) - tr.forward * off - (h.At(XRHandJointID.IndexTip) - h.Wrist); h.aimTo = ButtonWorld(p, b);
                    yield return null;
                }
            }
            yield return Reach(.075f, .5f); yield return Reach(-.012f, .22f); yield return Reach(.08f, .3f);
            h.point = false;
        }

        // Raised higher than for a button, to point out over the head-up strip at the ground ahead.
        void PointOut(int side, Vector3 target)
        {
            var h = rig.simHands[side]; float s = side == 0 ? 1 : -1;
            h.on = true; h.point = false; h.curlTo = .36f; h.indexTo = .16f; h.thumbTo = .35f; h.speed = 6;
            h.wristTo = rig.simHeadTo + BodyYaw * new Vector3(.23f * s, -.16f, .4f);
            h.rotTo = Quaternion.LookRotation((target - h.wristTo).normalized + Vector3.up * .15f, Vector3.up) * Quaternion.Euler(0, 0, -34 * s); h.aimTo = target;
        }
        // Point at the ground in a direction given from where the head looks, and pinch: a candidate is recorded there.
        // aim: how long the hand points before it pinches.
        IEnumerator RecordAt(float yawOff, float pitch, float aim = 1.1f)
        {
            var dir = Quaternion.Euler(-pitch, rig.simYawTo + yawOff, 0) * Vector3.forward;
            if (!PointOnTerrain(new Ray(rig.simHeadTo, dir), out var local, out _)) { Debug.LogWarning("[director] no terrain that way"); yield break; }
            var at = world.TransformPoint(local); int before = marks.Count;
            for (int attempt = 0; attempt < 3 && marks.Count == before; attempt++)
            {
                for (float t = 0; t < aim; t += Time.deltaTime) { PointOut(0, at); yield return null; }
                RH.pinch = true; yield return Pause(.18f); RH.pinch = false; yield return Pause(.7f);
            }
        }

        // Where the scenes of a film begin and which frames are kept as stills: "<frame> <name>" per line, written
        // beside the frames as cues.txt. The subtitles are laid by it afterwards (tools/tokyo_v2_edit.py).
        readonly System.Collections.Generic.List<string> cues = new System.Collections.Generic.List<string>();
        void Cue(string name) { cues.Add($"{recFrame} {name}"); directorStatus = name; }

        // The film for the Tokyo venue, second cut: one job from start to finish. On the table, check the walk to B
        // and the work there; go to B; record a candidate in the real view; come back and find the same candidate on
        // the map. The title and the opening are gone through before the recording starts: that is the state the
        // film begins from, and nothing else is prepared. The candidate is recorded by pointing and pinching.
        IEnumerator CoTokyo2(string outDir)
        {
            Vector3 T(float x, float y, float z) => tableFrame.TransformPoint(x, y, z);
            HeadTo(title.go.transform.position, 6); rig.simHeadTo += new Vector3(0, 0, .3f); yield return Pause(.6f);
            yield return PressRay(0, title, "Begin briefing", .5f, () => stage != Stage.Title || busy); Rest(0);
            HeadTo(T(0, .1f, -.05f), 4, 11); Time.timeScale = 3; yield return WaitFor(() => introDone && !busy); Time.timeScale = 1;
            var stand = rig.simHeadTo; yield return Pause(1.2f);
            // a step toward something to read it, without turning the film into a close-up
            Vector3 Toward(Vector3 at, float step) { var v = at - stand; v.y = 0; return stand + v.normalized * step; }
            cues.Clear(); if (outDir != null) { StartCoroutine(CoRecord(outDir, 1920, 1080, 30)); yield return null; }

            // 1. the table as the briefing opens: the whole route. Station B is picked on it.
            Cue("goal"); yield return Pause(1.0f);
            var pb = pins.Find(p => p.st.id == "B"); if (pb != null) yield return PressRay(0, pb.label, "pin:B", .8f, () => sel == "B");
            Rest(0); yield return Pause(1.7f);
            // 2. the walk to B: its part of the map comes onto the table with the slope classes; a point on the climb; its numbers
            Cue("walk"); var tb = FindButton(strip, "task:3"); if (tb != null) HeadTo(ButtonWorld(strip, tb), 2.2f, -13);
            float s0 = tv.S; yield return PressRay(0, strip, "task:3", .6f, () => framing != null || tv.S != s0); Rest(0);
            HeadTo(T(0, .1f, -.05f), 1.8f, 11); yield return Pause(2.3f); Cue("still:1_plan_route_and_slope");
            PointAt(0, T(.02f, .06f, .12f)); yield return Pause(1.5f); Rest(0);
            Cue("read");   // no subtitle over the panel while it is read
            // (a panel is read from a step nearer, looking a little under its middle, so that the timeline below it stays clear of the subtitle)
            rig.simHeadTo = Toward(board.go.transform.position, .42f); HeadTo(board.go.transform.position, 1.6f, -9); yield return Pause(2.3f);
            // 3. the work at B: NASA's record of the place, and why it is on the route
            Cue("survey"); rig.simHeadTo = stand; tb = FindButton(strip, "task:5"); if (tb != null) HeadTo(ButtonWorld(strip, tb), 2.2f, -6);
            yield return PressRay(0, strip, "task:5", .6f, () => selTask == 5); Rest(0);
            rig.simHeadTo = Toward(boardR.go.transform.position, .42f); HeadTo(boardR.go.transform.position, 1.6f, -9); yield return Pause(2.5f);
            rig.simHeadTo = Toward(board.go.transform.position, .42f); HeadTo(board.go.transform.position, 1.6f, -9); yield return Pause(2.4f);
            // 4. the simulated Marswalk is set to begin at this task ...
            pointRaise = .13f; Cue("start"); var sb = FindButton(board, "Start here"); if (sb != null) HeadTo(ButtonWorld(board, sb), 1.8f, 4);   // looking at the button: the hand comes up clear of the subtitle
            yield return PressRay(0, board, "Start here", .7f, () => taskIndex == 5); Rest(0); yield return Pause(1.2f);
            // ... and goes out to B: the same station, in the panorama taken there
            Cue("field"); yield return PressRay(0, board, "Marswalk", .6f, () => busy || stage != Stage.Table);
            pointRaise = 0; rig.simHeadTo = stand; Rest(0); Rest(1); Head(rig.simYawTo * .3f, -6, 1.3f);
            yield return WaitFor(() => stage == Stage.Field && !busy && panoOpacity > .98f); yield return Pause(1.0f);
            yield return WaitFor(() => !banner.Show, 6);
            // 5. one candidate in the real view: point, pinch, and it stays
            Cue("record"); Head(rig.simYawTo - 8, -7, 1.2f); yield return Pause(.7f);
            yield return RecordAt(-16, -3.5f, 1.9f); yield return Pause(.8f); Rest(0); yield return Pause(1.4f);
            Cue("still:2_field_candidate_01"); yield return Pause(1.4f);
            // 6. back at the table: the same candidate is on the map
            Cue("back"); yield return PressRay(0, hud, "HudTable", .7f, () => stage == Stage.Table); Rest(0);
            yield return Pause(.6f); HeadTo(T(0, .1f, -.05f), 1.4f, 10);
            yield return WaitFor(() => stage == Stage.Table && !busy, 12); yield return Pause(1.0f);
            yield return WaitFor(() => !banner.Show, 6); yield return Pause(.9f);
            // 7. the candidate is picked: the task it was recorded in comes up on the panel. The last view is held, with nothing over it.
            if (marks.Count > 0)
            {
                var m = marks[0]; Cue("pick"); yield return WaitFor(() => framing == null, 3);
                yield return PressRay(0, m.label, "mark:" + m.n, .7f, () => selMark == m.n); Rest(0); yield return Pause(1.5f);
                Cue("still:3a_plan_candidate_01_on_map"); yield return Pause(.5f);
                Cue("hold"); rig.simHeadTo = Toward(board.go.transform.position, .5f); HeadTo(board.go.transform.position, 1.4f, -5); yield return Pause(3.0f);
                Cue("still:3b_plan_candidate_01_task"); yield return Pause(1.6f);
            }
            recording = false; yield return null; yield return null;
            if (outDir != null) File.WriteAllLines(Path.Combine(outDir, "cues.txt"), cues);
            directorStatus = "done " + recFrame + " frames";
        }

        // The film for the NASA form: the briefing's observation plan from the goal to the saved plan (MLAppPlan.cs).
        // On the table, the plan's goal; each outcrop's science card, and the role it is given. At B, the camera: the
        // view takes its purpose from the task that is selected, and a wider context image is planned from the photo.
        // At C the purpose is chosen on the viewfinder, and a close-up with scale is planned. Back at the table the
        // relationship of the two is added and the plan is saved; the saved plan is held at the end.
        // The title and the opening are gone through before the recording starts, and the plan is emptied: that is
        // the state the film begins from. Everything after it is done by the hands, through the app's own buttons,
        // shutter and save. The moves between the table and the stations run faster than in the app (timeScale),
        // which the film's notes say; tools/briefing_edit.py lays the subtitles and fits the film to thirty seconds.
        IEnumerator CoBriefing(string outDir)
        {
            Vector3 T(float x, float y, float z) => tableFrame.TransformPoint(x, y, z);
            NewPlan();
            HeadTo(title.go.transform.position, 6); rig.simHeadTo += new Vector3(0, 0, .3f); yield return Pause(.6f);
            yield return PressRay(0, title, "Begin briefing", .5f, () => stage != Stage.Title || busy); Rest(0);
            HeadTo(T(0, .1f, -.05f), 4, 11); Time.timeScale = 3; yield return WaitFor(() => introDone && !busy); Time.timeScale = 1;
            var stand = rig.simHeadTo; yield return Pause(1.0f);
            Vector3 Toward(Vector3 at, float step) { var v = at - stand; v.y = 0; return stand + v.normalized * step; }
            void LookAz(float az, float el, float speed = 2.4f) => Head(rig.simYawTo + Mathf.DeltaAngle(rig.simYawTo, view.yaw + az), el, speed);
            IEnumerator Arrive(Func<bool> there) { Time.timeScale = 2.6f; yield return WaitFor(there, 14); Time.timeScale = 1; }
            // the shutter: the hand comes up, points into the frame and pinches
            IEnumerator Shutter()
            {
                int before = caps.Count;
                for (int attempt = 0; attempt < 3 && caps.Count == before; attempt++)
                {
                    for (float t = 0; t < .55f; t += Time.deltaTime) { PointOut(0, rig.Head.position + rig.Head.forward * 40 + rig.Head.right * 6); yield return null; }
                    RH.pinch = true; yield return Pause(.18f); RH.pinch = false; yield return WaitFor(() => caps.Count > before, .8f);
                }
                if (caps.Count == before) Debug.LogWarning("[director] the shutter did not take a photo");
            }
            // what was framed: plan the next observation from the photo's card, and close it
            IEnumerator FromPhoto(string type)
            {
                yield return WaitFor(() => shot.Show && shot.alpha > .9f, 2);
                // a lean toward the card, as one does to read something held up: the card is then large enough in the film to be followed
                var was = rig.simHeadTo; var to = shot.go.transform.position - was; to.y = 0; rig.simHeadTo = was + to.normalized * .36f; HeadTo(shot.go.transform.position, 2.8f, 1);
                yield return PressRay(0, shot, "Next:" + type, .8f, () => shotCap != null && shotCap.next.Count > 0); yield return Pause(.5f);
                yield return PressRay(0, shot, "Done", .45f, () => shotCap == null); Rest(0); rig.simHeadTo = was;
            }
            pointRaise = .08f;
            // the film opens a step from the left-hand panel, where its two faces are in reach
            rig.simHeadTo = Toward(board.go.transform.position, .5f); HeadTo(board.go.transform.position, 3, -3); yield return Pause(1.1f);
            cues.Clear(); if (outDir != null) { StartCoroutine(CoRecord(outDir, 1920, 1080, 30)); yield return null; }

            // 1. the goal: the briefing opens on the observation plan, which is empty
            Cue("goal"); yield return Pause(3.5f);
            // 2. the two outcrops: the science card of each, and the role it is given
            Cue("roles"); rig.simHeadTo = stand; var tb = FindButton(strip, "task:8"); if (tb != null) HeadTo(ButtonWorld(strip, tb), 2.6f, 4);
            yield return PressRay(0, strip, "task:8", .5f, () => selTask == 8);
            rig.simHeadTo = Toward(boardR.go.transform.position, .5f); HeadTo(boardR.go.transform.position, 2.4f, -8); yield return Pause(.6f);
            yield return PressRay(0, boardR, "Add:environmental_record", .6f, () => planRole.ContainsKey("C")); yield return Pause(.3f);
            yield return PressRay(0, strip, "task:4", .5f, () => selTask == 4); yield return Pause(.6f);
            yield return PressRay(0, boardR, "Add:timing_context", .6f, () => planRole.ContainsKey("B")); Rest(0); yield return Pause(.5f);
            // 3. B, in the panorama taken there: the camera; the view takes the selected task's purpose; a wider context image is planned
            Cue("frame"); yield return PressRay(0, boardR, "Stand at B", .5f, () => busy || stage != Stage.Table);
            rig.simHeadTo = stand; Rest(0); Rest(1); Head(rig.simYawTo * .3f, -4, 1.6f);
            yield return Arrive(() => stage == Stage.Field && !busy && panoOpacity > .98f); yield return Pause(.3f);
            yield return PressRay(0, hud, "Camera", .6f, () => camOn); Rest(0);
            LookAz(356, 5); yield return Pause(1.1f);
            yield return Shutter(); yield return FromPhoto("wider_context_image");
            // 4. C: the purpose is chosen on the viewfinder; a close-up with scale is planned
            Cue("closeup"); var pc = pins.Find(p => p.st.id == "C");
            if (pc != null) { HeadTo(pc.label.go.transform.position, 2.6f); yield return Pause(.5f); yield return PressRay(0, pc.label, "pin:C", .6f, () => busy || preview == "C"); }
            Rest(0); yield return Arrive(() => stage == Stage.Field && preview == "C" && !busy && panoOpacity > .98f); yield return Pause(.2f);
            LookAz(338, 1); yield return Pause(.7f);
            yield return PressRay(0, finder, "CamPurpose:inspect_grain_texture", .6f, () => CamPurpose(out _) == "inspect_grain_texture"); Rest(0); yield return Pause(.4f);
            yield return Shutter(); yield return FromPhoto("close_up_with_scale");
            // 5. back at the table: both photos and what was planned from them; their relationship; saved
            Cue("connect"); yield return PressRay(0, hud, "HudTable", .6f, () => stage == Stage.Table); Rest(0);
            HeadTo(T(0, .1f, -.05f), 1.8f, 12); yield return Arrive(() => stage == Stage.Table && !busy); yield return Pause(.7f);
            rig.simHeadTo = Toward(board.go.transform.position, .5f); HeadTo(board.go.transform.position, 2.2f, -4); yield return Pause(1.2f);
            yield return PressRay(0, board, "Relationship", .6f, () => relOn); yield return Pause(.6f);
            yield return PressRay(0, board, "Save plan", .6f, () => card.Show); Rest(0);
            // 6. the saved plan, held
            // (it came up in front of where the person stood; they turn to face it squarely, a little nearer, and read it)
            Cue("card"); yield return WaitFor(() => card.alpha > .9f, 2); yield return Pause(.3f); var ct = card.go.transform;
            rig.simHeadTo = ct.position - ct.forward * .7f + Vector3.up * .01f; HeadTo(ct.position, 3.2f); yield return Pause(.5f); HeadTo(ct.position, 3.2f); yield return Pause(5.6f);
            pointRaise = 0; recording = false; yield return null; yield return null;
            if (outDir != null) File.WriteAllLines(Path.Combine(outDir, "cues.txt"), cues);
            if (outDir != null) File.WriteAllText(Path.Combine(outDir, "plan.txt"), planSavedPath + "\n" + PlanState());
            directorStatus = "done " + recFrame + " frames";
        }

        // The run shown in Tokyo: check a walk and a station's work on the table, go to that station, record two
        // candidates in the real view, come back and find them on the map.
        IEnumerator CoTokyo(bool brisk)
        {
            float beat = brisk ? .7f : 1; Vector3 T(float x, float y, float z) => tableFrame.TransformPoint(x, y, z);
            directorStatus = "title"; HeadTo(title.go.transform.position, 6);
            rig.simHeadTo += new Vector3(0, 0, .3f); rig.simHeadSpeed = 1.6f; yield return Pause(1.0f * beat);
            yield return PressRay(0, title, "Begin briefing", .7f, () => stage != Stage.Title || busy); Rest(0);
            directorStatus = "opening"; yield return Pause(.5f);
            HeadTo(T(0, .1f, -.05f), 1.6f, 11); if (brisk) Time.timeScale = 1.5f;
            yield return WaitFor(() => introDone && !busy); Time.timeScale = 1; yield return Pause(.5f * beat);

            // 1. a walk: the climb to B comes onto the table with its slope classes
            directorStatus = "walk task"; var tb = FindButton(strip, "task:3"); if (tb != null) HeadTo(ButtonWorld(strip, tb), 2.2f, -13);
            yield return PressRay(0, strip, "task:3", .6f, () => selTask == 3); Rest(0);
            HeadTo(T(0, .1f, -.05f), 1.8f, 11); yield return Pause(1.5f * beat);
            PointAt(0, T(.02f, .06f, .12f)); yield return Pause(1.5f * beat); Rest(0);   // the slope and elevation of a point on the climb
            HeadTo(board.go.transform.position, 1.6f, -4); yield return Pause(1.8f * beat);
            // 2. the work at B: why there, and NASA's record of it
            directorStatus = "station task"; tb = FindButton(strip, "task:5"); if (tb != null) HeadTo(ButtonWorld(strip, tb), 2.2f, -6);
            yield return PressRay(0, strip, "task:5", .6f, () => selTask == 5); Rest(0);
            HeadTo(boardR.go.transform.position, 1.5f, -6); yield return Pause(2.0f * beat);
            HeadTo(board.go.transform.position, 1.5f, -8); yield return Pause(1.6f * beat);
            // 3. the simulated Marswalk is set to begin there, and goes out
            directorStatus = "start"; yield return PressRay(0, board, "Start here", .7f, () => taskIndex == 5); yield return Pause(.7f * beat);
            yield return PressRay(0, board, "Marswalk", .6f, () => busy || stage != Stage.Table); Rest(0); Rest(1);
            Head(rig.simYawTo * .3f, -6, 1.3f);
            yield return WaitFor(() => stage == Stage.Field && !busy && panoOpacity > .98f); yield return Pause(1.6f * beat);
            // 4. two candidates in the real view
            directorStatus = "record"; Head(rig.simYawTo - 8, -7, 1.2f); yield return Pause(.9f * beat);
            yield return RecordAt(-16, -3.5f); yield return Pause(.6f * beat);
            Head(rig.simYawTo + 22, -8, 1.2f); yield return Pause(.6f * beat);
            yield return RecordAt(13, -6); Rest(0); yield return Pause(1.2f * beat);
            // 5. back at the table they are on the map; one is picked
            directorStatus = "back"; yield return PressRay(0, hud, "HudTable", .7f, () => stage == Stage.Table); Rest(0);
            yield return Pause(.6f); HeadTo(T(0, .1f, -.05f), 1.4f, 10);
            yield return WaitFor(() => stage == Stage.Table && !busy, 12); yield return Pause(3.0f * beat);
            if (marks.Count > 0)
            {
                var m = marks[0]; directorStatus = "pick"; yield return WaitFor(() => framing == null, 3);
                yield return PressRay(0, m.label, "mark:" + m.n, .7f, () => selMark == m.n); Rest(0); yield return Pause(1.4f * beat);
                HeadTo(board.go.transform.position, 1.5f, -12); yield return Pause(3.0f * beat);
            }
        }

        // ---------- the run ----------
        IEnumerator CoDirect(string scenario, string outDir, string language)
        {
            // "demo": the short run at its own pace. "demo30": the same, with the two stretches the app paces itself (the
            // opening and the walk) run faster and the pauses shorter, to fit thirty seconds. "full": with the timer, the
            // probe and the way back to the table as well.
            bool full = scenario == "full", brisk = scenario == "demo30"; float beat = brisk ? .65f : 1; Vector3 T(float x, float y, float z) => tableFrame.TransformPoint(x, y, z);
            directorStatus = "setting up";
            if (MLText.lang != language) SetLanguage(language);
            studio = true; studioK = 1;   // the studio room: there is no real room to see through to here
            menuIntro = false;            // the menu is shown on the hand only (its first-time place beside the model would be in the way)
            planTab = scenario == "briefing";   // the earlier films work the Marswalk's tasks, which are the board's other face
            rig.simHeadTo = new Vector3(0, 1.6f, 0); Head(0, -3, 20); Rest(0); Rest(1); RH.Snap(); LH.Snap();
            yield return Pause(.6f);
            if (scenario == "tokyo2") { yield return CoTokyo2(outDir); yield break; }   // it starts its own recording, after the opening
            if (scenario == "briefing") { yield return CoBriefing(outDir); yield break; }   // likewise
            if (outDir != null) { StartCoroutine(CoRecord(outDir, 1920, 1080, 30)); yield return null; }

            if (scenario.StartsWith("tokyo")) { yield return CoTokyo(scenario == "tokyo30"); recording = false; yield return null; yield return null; directorStatus = "done " + recFrame + " frames"; yield break; }

            // 1. the title; begin
            directorStatus = "title"; HeadTo(title.go.transform.position, 6);
            rig.simHeadTo += new Vector3(0, 0, .3f); rig.simHeadSpeed = 1.6f; yield return Pause(1.0f * beat);   // a step toward the title as it is read
            yield return PressRay(0, title, "Begin briefing", .7f, () => stage != Stage.Title || busy); Rest(0);
            // 2. the opening: the model spreads over the table and the route is drawn
            directorStatus = "opening"; yield return Pause(.5f);
            HeadTo(T(0, .1f, -.05f), 1.6f, 11); if (brisk) Time.timeScale = 1.5f;
            yield return WaitFor(() => introDone && !busy); Time.timeScale = 1; yield return Pause(.6f * beat);

            // 3. both hands take the map: pull apart to stretch it, turning it a little (leaning in over the table to reach)
            directorStatus = "stretch"; var stand = rig.simHeadTo; rig.simHeadTo = stand + BodyYaw * new Vector3(0, -.03f, .1f);
            foreach (int side in new[] { 0, 1 })
            {
                var h = rig.simHands[side]; float s = side == 0 ? 1 : -1; h.pinch = false; h.point = false; h.curlTo = .3f; h.indexTo = .2f; h.thumbTo = .4f; h.speed = 6;
                h.wristTo = T(.1f * s, .21f, -.3f); h.rotTo = tableFrame.rotation * Quaternion.LookRotation(new Vector3(-.2f * s, -.45f, 1), Vector3.up) * Quaternion.Euler(0, 0, -20 * s); h.aimTo = T(.12f * s, 0, .05f);
            }
            yield return Pause(.75f); RH.pinch = true; yield return Pause(.14f); LH.pinch = true; yield return Pause(.25f);
            RH.speed = LH.speed = 2.6f; RH.wristTo = T(.2f, .2f, -.22f); LH.wristTo = T(-.19f, .2f, -.36f);
            yield return Pause(1.3f); RH.pinch = LH.pinch = false; yield return Pause(.3f); Rest(0); Rest(1); rig.simHeadTo = stand;
            yield return Pause(.5f * beat);

            // 4. a task on the timeline: the table brings its part of the route (here the climb to B) under the eyes
            directorStatus = "timeline"; var tb = FindButton(strip, "task:3"); if (tb != null) HeadTo(ButtonWorld(strip, tb), 2.2f, -14);
            yield return PressRay(0, strip, "task:3", .6f, () => selTask == 3); Rest(0);
            HeadTo(T(0, .1f, -.05f), 1.8f, 11); yield return Pause(1.3f * beat);

            // 5. the menu on the left hand: back of the hand toward the eyes, thumb wide; a fingertip presses Slope
            directorStatus = "menu";
            LH.curlTo = -.25f; LH.indexTo = -.25f; LH.thumbTo = 1; LH.speed = 6;
            LH.wristTo = rig.simHeadTo + BodyYaw * new Vector3(-.25f, -.35f, .37f);
            LH.rotTo = Quaternion.LookRotation(Vector3.up + BodyYaw * new Vector3(.1f, 0, .12f), rig.simHeadTo + Vector3.down * .1f - LH.wristTo);
            yield return WaitFor(() => menuDock.on && menuDock.grow > .95f, 3); yield return Pause(.4f);
            yield return TouchButton(menu, "Slope"); Rest(0);
            if (layer != "slope") Debug.LogWarning("[director] the touch did not switch the slope layer on");
            yield return Pause(1.6f * beat);
            LH.thumbTo = 0; yield return Pause(.5f); Rest(1); yield return Pause(.3f * beat);

            if (full)
            {
                // 6. the timer: pinch it, turn the hand, and the planned timeline runs on (the table follows)
                directorStatus = "timer";
                var dialAt = dial.go.transform.TransformPoint(0, DialUp - 190, 0);
                RH.curlTo = .3f; RH.indexTo = .2f; RH.speed = 6; RH.rotTo = tableFrame.rotation * Quaternion.LookRotation(new Vector3(-.1f, -.3f, 1), Vector3.up) * Quaternion.Euler(0, 0, 10);
                for (float t = 0; t < .7f; t += Time.deltaTime) { RH.wristTo = dialAt - (rig.right.origin - RH.Wrist); RH.aimTo = dialAt + tableFrame.forward; yield return null; }
                RH.pinch = true; yield return Pause(.3f);
                RH.speed = 4; RH.wristTo = T(.2f, .3f, -.38f); yield return Pause(.8f);
                var hold = RH.rotTo; RH.speed = 3;
                RH.rotTo = hold * Quaternion.Euler(0, 0, -52); yield return Pause(1.3f);
                RH.rotTo = hold * Quaternion.Euler(0, 0, -20); yield return Pause(1.0f);
                RH.pinch = false; yield return Pause(.3f); Rest(0); yield return Pause(.6f);
                // 7. point at the model: the elevation and slope of that point
                directorStatus = "probe"; PointAt(0, T(.1f, .03f, .05f)); yield return Pause(1.6f); PointAt(0, T(-.12f, .03f, .2f), 3); yield return Pause(1.6f); Rest(0);
            }

            // 8. out onto the surface
            directorStatus = "marswalk";
            var mb = FindButton(board, "Marswalk"); if (mb != null) HeadTo(ButtonWorld(board, mb), 2.4f, -12);
            yield return Pause(.5f); yield return PressRay(0, board, "Marswalk", .7f, () => busy || stage != Stage.Table); Rest(0); Rest(1);
            Head(rig.simYawTo * .3f, -7, 1.3f);
            yield return WaitFor(() => stage == Stage.Field && !busy && panoOpacity > .98f); yield return Pause(1.2f);
            // look round the real panorama a little, then toward where the walk goes
            directorStatus = "field"; Head(24, -5, .9f); yield return Pause(full ? 2.0f : brisk ? .5f : 1.0f);
            string to = Task.station; var ts = St(to); HeadTo(world.TransformPoint(D.Local(ts.x, ts.z, 1.5f)), 1.4f, -6); yield return Pause(1.0f);
            yield return PressRay(0, hud, "HudAction", .7f, () => travelling != null); Rest(0); if (brisk) Time.timeScale = 1.6f;
            // 9. the walk stops where the ground is steepest; look, confirm, go on
            directorStatus = "walk"; yield return WaitFor(() => checkpoint != null, 20); Time.timeScale = 1; yield return Pause(.9f * beat);
            if (checkpoint != null) { HeadTo(world.TransformPoint(checkpoint.local), 1.6f, -4); yield return Pause(.9f * beat); yield return PressRay(0, hud, "HudAction", .7f, () => checkpoint == null); Rest(0); }
            if (brisk) Time.timeScale = 1.6f;
            yield return WaitFor(() => stage == Stage.Field && here == to && !busy, 20); Time.timeScale = 1;
            directorStatus = "arrived"; Head(rig.simYawTo + 14, -4, .7f); yield return Pause(full ? 3.4f : brisk ? 2.0f : 2.6f);

            if (full)
            {   // 10. back to the table
                directorStatus = "back"; yield return PressRay(0, hud, "HudTable", .7f); Rest(0);
                yield return Pause(.6f); HeadTo(T(0, .1f, -.05f), 1.4f, 9);
                yield return WaitFor(() => stage == Stage.Table && !busy, 12); yield return Pause(1.8f);
            }
            recording = false; yield return null; yield return null;
            directorStatus = "done " + recFrame + " frames";
        }
    }
}
#endif
