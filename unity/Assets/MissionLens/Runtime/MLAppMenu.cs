using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;
using static MissionLens.MLPanel;
using static MissionLens.MLText;

namespace MissionLens
{
    // The menu: what is done to the map (layers, height, what is on the table) and to the room (layout, timer,
    // studio room, where the table stands, language), with the map's north, scale and source at its head.
    //
    // In the headset it is carried on the left hand. Turn the back of the left hand toward you and open the thumb
    // wide: the menu appears in the corner the thumb and forefinger make, its lower edge resting on the thumb and
    // its left edge running up the forefinger, and stays with the hand. Close the thumb or drop the hand and it
    // goes. It is worked with the other hand, by pointing and pinching or by touching a button with a fingertip.
    // So the map stays in view while it is being changed, which a sign across the desk did not allow.
    //
    // Until the hand has done that once, the menu floats beside the model with the instruction written on it, so
    // that nothing depends on a pose nobody has been shown; "Menu" on the timeline puts it back there at any time.
    // With controllers it floats above the left controller while that is raised. On a desktop it sits at the left
    // of the view, where a raised left hand would be (M hides it).
    public partial class MLApp
    {
        const float MenuW = .17f;                                    // metres: about the span of an opened hand
        static readonly Vector3 MenuPin = new Vector3(-.273f, .346f, -.435f);   // where it floats when not on the hand: left of the model, between it and the left panel as seen from where the person stands
        bool menuIntro, menuPinned, menuHidden;                      // intro: not yet taken onto the hand. pinned: kept in the air by the timeline's button. hidden: desktop, M
        // A panel's place on the left hand: whether it is there, for how long the hand has asked for it or stopped
        // asking, where it is, and how far the other hand's fingertip was from its face a frame ago.
        class Dock { public bool on, touchArmed = true; public float poseFor, lostFor = 9, touchWas = -1000; public Vector3 at; public Quaternion rot = Quaternion.identity; }
        readonly Dock menuDock = new Dock(), wristDock = new Dock();
        float thumbMax, thumbLogAt;
        const float WristOnHand = .21f;                              // the FIELD panel's width when it is carried on the hand (metres)

        void DrawMenu()
        {
            var s = menu; s.Clear(); const float X = 26; float W = s.w - 2 * X, bw = (W - 12) / 2, bh = 82; int px = Ja ? 24 : 26;
            s.Frame(new Color(10 / 255f, 12 / 255f, 17 / 255f, .95f), Cyan);
            // the map as it lies on the table now: north (the arrow turns as the map is turned), scale, height
            float cx = X + 42, cy = 70, r = 36, th = tv.yawRel * Mathf.Deg2Rad; Vector2 up = new Vector2(Mathf.Sin(th), -Mathf.Cos(th)), side = new Vector2(-up.y, up.x);
            s.Box(cx - r, cy - r, 2 * r, 2 * r, r, new Color(0, 0, 0, 0), new Color(1, 1, 1, .28f));
            Vector2 c = new Vector2(cx, cy), tip = c + up * (r - 9), tail = c - up * (r - 12), barb = tip - up * 17;
            s.Stroke(tail.x, tail.y, tip.x, tip.y, 5, Ink); s.Stroke(tip.x, tip.y, barb.x + side.x * 11, barb.y + side.y * 11, 5, Ink); s.Stroke(tip.x, tip.y, barb.x - side.x * 11, barb.y - side.y * 11, 5, Ink);
            s.Text("N", X + 98, 62, 24, Soft, true); s.Text(ScaleText, X + 126, 66, 38, Ink, true);
            s.Text(tableEx > 1 ? L("height ×2", "高さ ×2") : L("height as measured", "高さ 実寸"), X + 98, 98, 21, Soft);
            s.Button(Ja ? "English" : "日本語", s.w - X - 124, 36, 124, 58, ToggleLanguage, px: 22, key: "Language");
            s.Text(L("MRO HiRISE terrain model, 2 m grid · walls and base drawn by us", "MRO HiRISE の地形モデル(2 m 格子)· 側壁と台はこちらで描画"), X, 142, 17, Gray);

            float y = 180; s.Text(L("THE MAP", "地図"), X, y, 18, Dim, true); y += 14;
            s.Button(L("Slope", "傾斜"), X, y, bw, bh, () => SetLayer("slope"), layer == "slope", px: px, key: "Slope");
            s.Button(L("Spectral", "分光"), X + bw + 12, y, bw, bh, () => SetLayer("crism"), layer == "crism", px: px, key: "Spectral"); y += bh + 12;
            s.Button(L("Height ×2", "高さ ×2"), X, y, bw, bh, ToggleHeight, tableEx > 1, px: px, key: "Height ×2");
            s.Button(L("Whole route", "ルート全体"), X + bw + 12, y, bw, bh, WholeRoute, px: px, key: "Whole route"); y += bh + 12;
            s.Button(followTasks ? L("Follow tasks: on", "作業に追従: 入") : L("Follow tasks: off", "作業に追従: 切"), X, y, W, bh, ToggleFollow, followTasks, px: px, key: "Follow tasks"); y += bh + 36;

            s.Text(L("THE ROOM", "部屋と配置"), X, y, 18, Dim, true); y += 14;
            s.Button(layout == "desk" ? L("Layout: desk", "配置: 机") : L("Layout: wall", "配置: 壁"), X, y, bw, bh, ToggleLayout, px: px, key: "Layout");
            s.Button(tools ? L("Timer: on", "タイマー: 入") : L("Timer: off", "タイマー: 切"), X + bw + 12, y, bw, bh, ToggleTools, tools, px: px, key: "Timer"); y += bh + 12;
            if (rig.xr)
            {
                s.Button(studio ? L("Room: studio", "部屋: スタジオ") : L("Room: yours", "部屋: 実際"), X, y, bw, bh, ToggleStudio, studio, px: px, key: "Room");
                s.Button(L("Bring table here", "机をここへ"), X + bw + 12, y, bw, bh, PlaceTable, px: px, key: "Bring table here"); y += bh + 12;
            }
            y += 24;
            if (rig.xr && menuIntro)
                y = s.Wrap(L("Turn the back of your left hand toward you and open your thumb wide: this menu comes to your hand.", "左手の甲を自分に向けて親指を大きく開くと、このメニューが手に付きます。"), X, y, W, 27, 21, Amber) + 2;
            DrawNeatline();
            s.Wrap(rig.xr ? L("Pinch at the model and move to slide the map. Pinch with both hands and pull apart to stretch it, or turn them to turn it.", "模型のところでピンチして動かすと地図が滑ります。両手でピンチして広げると拡大、回すと回転します。")
                          : L("Drag the model to slide it, wheel to zoom, Q / E to turn. M hides this menu.", "模型をドラッグで移動、ホイールで拡大縮小、Q / E で回転。M でこのメニューを隠します。"), X, y, W, 25, 19, Gray);
        }

        /// <summary>Keeps the menu floating beside the model, or lets it go back to the hand.</summary>
        public void ToggleMenuPin()
        {
            menuPinned = !(menuPinned || menuIntro); menuIntro = false; sfx.Play("toggle", .7f); Log("menu " + (menuPinned ? "pinned" : "on the hand")); DrawMenu(); DrawStrip();
        }

        // The corner between the left hand's forefinger and its opened thumb, as seen on the back of the hand, and a
        // panel's place in it: left edge up the forefinger, lower edge on the thumb.
        //   thumbDeg  how far the thumb is opened from the forefinger
        //   toEyes    whether the back of the hand is toward the eyes
        bool HandCorner(float w, float h, out Vector3 centre, out Quaternion rot, out float thumbDeg, out bool toEyes)
        {
            centre = default; rot = Quaternion.identity; thumbDeg = 0; toEyes = false;
            if (!rig.Joint(1, XRHandJointID.IndexProximal, out var ip) || !rig.Joint(1, XRHandJointID.IndexTip, out var it)
                || !rig.Joint(1, XRHandJointID.ThumbProximal, out var tp) || !rig.Joint(1, XRHandJointID.ThumbTip, out var tt)) return false;
            Vector3 finger = it - ip, thumb = tt - tp;
            if (finger.magnitude < .05f || thumb.magnitude < .025f) return false;   // forefinger curled in: there is no corner to sit in
            Vector3 up = finger.normalized, right = Vector3.ProjectOnPlane(thumb, up); thumbDeg = Vector3.Angle(thumb, up);
            if (right.sqrMagnitude < 1e-5f) return false; right.Normalize();
            Vector3 palmward = Vector3.Cross(right, up);   // out of the palm: away from the eyes when the back of the hand is toward them
            float thumbTop = Mathf.Max(Vector3.Dot(tt - ip, up), Vector3.Dot(tp - ip, up));   // how far up the forefinger the thumb reaches (negative: toward the wrist)
            Vector3 corner = ip + right * .026f + up * (thumbTop + .022f);
            Vector3 away = (corner - rig.Head.position).normalized; toEyes = Vector3.Dot(palmward, away) > .3f;
            // Turned half way from the plane of the hand toward the eyes, so it stays readable as the hand tilts.
            rot = Quaternion.LookRotation(Vector3.Slerp(palmward, away, .5f), up);
            centre = corner + rot * new Vector3(w / 2, h / 2, 0);
            return true;
        }

        // Whether the left hand is asking for a panel now (back of the hand toward the eyes, thumb opened wide, in
        // view), and where the panel would sit. Opened wide to call it; it then stays until the thumb is clearly
        // closed again. With a controller in the left hand, which has no fingers to read: raised and looked at.
        bool HandAsks(Dock d, float w, float h, out Vector3 at, out Quaternion rot)
        {
            var l = rig.left; var head = rig.Head.position; at = d.at; rot = d.rot;
            if (HandCorner(w, h, out var hc, out var hr, out float thumbDeg, out bool toEyes))
            {
                bool inView = Vector3.Angle(rig.Head.forward, hc - head) < 55 && Vector3.Distance(hc, head) < .8f;
                if (toEyes && inView)
                {   // for the device log: how far thumbs really open, to set the angle the menu comes at
                    thumbMax = Mathf.Max(thumbMax, thumbDeg);
                    if (Time.time > thumbLogAt) { thumbLogAt = Time.time + 4; Log($"back of the left hand shown: thumb opened up to {thumbMax:0} deg (a panel comes at 50)"); thumbMax = 0; }
                }
                if (!(toEyes && inView && !l.held && thumbDeg > (d.on ? 34 : 50))) return false;
                at = hc; rot = hr; return true;
            }
            if (!l.active || !l.controller) return false;
            if (l.held || Vector3.Distance(l.origin, head) > .75f || Vector3.Angle(rig.Head.forward, l.origin - head) > 34 || Vector3.Dot(l.rotation * Vector3.forward, rig.Head.forward) > .5f) return false;
            at = l.origin + Vector3.up * (h / 2 + .06f); rot = Quaternion.LookRotation(at - head, Vector3.up); return true;
        }
        // The panel comes onto the hand once the hand has asked for a moment, and leaves half a second after it
        // stops asking: the other hand reaching across can hide the fingers for a moment. True when it has just come.
        static bool DockStep(Dock d, bool asks, bool allowed, float dt)
        {
            d.poseFor = asks ? d.poseFor + dt : 0; d.lostFor = asks ? 0 : d.lostFor + dt; bool was = d.on;
            if (!d.on && d.poseFor > .12f) d.on = true;
            if (d.on && (!allowed || d.lostFor > .5f)) d.on = false;
            return d.on && !was;
        }
        // Where a carried panel is drawn: it appears in place, then follows the hand smoothly; while the hand has
        // stopped asking (or cannot be seen) it waits where it was.
        static void DockPlace(Dock d, MLPanel p, bool follow, Vector3 at, Quaternion rot, float dt)
        {
            if (p.alpha < .05f) { d.at = at; d.rot = rot; }
            else if (follow) { d.at = Vector3.Lerp(d.at, at, 1 - Mathf.Exp(-22 * dt)); d.rot = Quaternion.Slerp(d.rot, rot, 1 - Mathf.Exp(-16 * dt)); }
            p.go.transform.SetPositionAndRotation(d.at, d.rot);
        }

        void UpdateMenu(float dt)
        {
            bool live = stage == Stage.Table && introDone && roomO > .6f;
            if (!rig.xr)
            {
                if (rig.menuKey && live) { menuHidden = !menuHidden; sfx.Play("toggle", .6f); }
                if (rig.debugJoints == null) { menu.Show = live && !menuHidden; return; }
                menu.go.transform.SetParent(null, true);   // a check from the Editor with joints given: behave as on the headset
            }
            bool asks = HandAsks(menuDock, MenuW, MenuW * menu.h / menu.w, out var at, out var rot) && live && !menuPinned;
            if (DockStep(menuDock, asks, live && !menuPinned, dt))
            {
                sfx.Play("toggle", .6f); Log("menu on the hand" + (menuIntro ? " (first time)" : ""));
                if (menuIntro) { menuIntro = false; DrawMenu(); DrawStrip(); }
            }
            bool floating = (menuIntro || menuPinned) && !menuDock.on;
            if (floating) { at = tableFrame.TransformPoint(MenuPin); rot = Quaternion.LookRotation(at - tableFrame.TransformPoint(0, EyeAboveTable, -TableAhead), Vector3.up); }
            menu.Show = live && (menuDock.on || floating);
            DockPlace(menuDock, menu, asks || floating, at, rot, dt);
        }

        // The FIELD panel is carried the same way (or, called up from the head-up strip, stays where it was called up).
        void UpdateWristOnHand(float dt)
        {
            bool allowed = !OnTable && !wristUp;
            bool asks = HandAsks(wristDock, WristOnHand, WristOnHand * wrist.h / wrist.w, out var at, out var rot) && allowed;
            if (DockStep(wristDock, asks, allowed, dt)) { sfx.Play("toggle", .6f); Log("panel on the hand"); }
            if (wristDock.on) { wrist.SetWorldWidth(WristOnHand); DockPlace(wristDock, wrist, asks, at, rot, dt); }
        }

        // A fingertip of the right hand pressed onto a carried panel presses the button there. The button lights up
        // as the finger comes near, and is pressed as the fingertip passes through the panel's face from the front.
        void Touch(MLPanel p, Dock d)
        {
            if ((rig.xr || rig.debugJoints != null) && p.Visible && p.alpha >= .6f && !busy && rig.Joint(0, XRHandJointID.IndexTip, out var tip))
            {
                var t = p.go.transform; var l = t.InverseTransformPoint(tip); float mm = l.z * t.lossyScale.z * 1000;   // in front of the face (negative) or through it
                var b = Mathf.Abs(mm) < 60 ? p.ButtonAt(new Vector2(l.x + p.w / 2f, p.h / 2f - l.y)) : null;
                if (b != null && !b.quiet && mm > -50 && mm < 25) hovered[p] = b;
                if (mm < -14) d.touchArmed = true;   // drawn back: ready for the next press
                else if (d.touchArmed && b != null && d.touchWas < -3 && mm >= -3 && mm < 30)
                {
                    d.touchArmed = false; if (!b.quiet) { p.Pressed(); sfx.Play("press"); Log("press " + b.key + " (touch)"); }
                    b.fn();
                }
                d.touchWas = mm;
            }
            else d.touchWas = -1000;
        }
        void TouchPanels() { Touch(menu, menuDock); Touch(wrist, wristDock); }

        /// <summary>For checks from the Editor: where the menu is and why.</summary>
        public string MenuState()
        {
            var v = rig.cam.WorldToViewportPoint(menu.go.transform.position);
            return $"menu show={menu.Show} alpha={menu.alpha:F2} onHand={menuDock.on} intro={menuIntro} pinned={menuPinned} vp=({v.x:F2},{v.y:F2}) at={menu.go.transform.position.ToString("F2")} | panel show={wrist.Show} onHand={wristDock.on}";
        }
        /// <summary>
        /// For checks from the Editor: a left hand held up in front of the camera, its back toward it, with the thumb
        /// opened by `thumbDeg` from the forefinger (0 or less: take the hand away). Optionally a right forefinger tip
        /// at a point of the menu given in its pixels, `mm` in front of its face (negative) or through it.
        /// </summary>
        public void DebugHand(float thumbDeg, float tipX = -1, float tipY = -1, float mm = -100, bool onPanel = false)
        {
            if (thumbDeg <= 0) { rig.debugJoints = null; if (!rig.xr) { menu.go.transform.SetParent(rig.Head, false); menu.go.transform.localPosition = new Vector3(-.4f, -.03f, .5f); menu.go.transform.localRotation = Quaternion.Euler(0, -24, 0); } return; }
            var h = rig.Head; var j = rig.debugJoints ?? new Dictionary<(int, XRHandJointID), Vector3>();
            Vector3 ip = h.TransformPoint(-.16f, -.12f, .42f), up = h.up, right = h.right;
            Vector3 thumb = Quaternion.AngleAxis(-thumbDeg, h.forward) * up;   // opened toward the person's right
            j[(1, XRHandJointID.IndexProximal)] = ip; j[(1, XRHandJointID.IndexTip)] = ip + up * .08f;
            j[(1, XRHandJointID.ThumbProximal)] = ip - up * .045f + right * .02f; j[(1, XRHandJointID.ThumbTip)] = ip - up * .045f + right * .02f + thumb * .055f;
            if (tipX >= 0) { var q = onPanel ? wrist : menu; var t = q.go.transform; j[(0, XRHandJointID.IndexTip)] = t.TransformPoint(tipX - q.w / 2f, q.h / 2f - tipY, mm / 1000 / t.lossyScale.z); }
            else j.Remove((0, XRHandJointID.IndexTip));
            rig.debugJoints = j;
        }
    }
}
