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
    // wide: the menu unfolds from the corner the thumb and forefinger make, its lower edge resting on the thumb and
    // its left edge by the forefinger's knuckle, lying in the plane of the back of the hand. Close the thumb and it
    // folds away. It is worked with the other hand: touch a button with a fingertip, or point and pinch.
    // So the map stays in view while it is being changed, which a sign across the desk did not allow.
    //
    // Until the hand has done that once, the menu floats beside the model with the instruction written on it, so
    // that nothing depends on a pose nobody has been shown; "Menu" on the timeline puts it back there at any time.
    // With controllers it floats above the left controller while that is raised. On a desktop it sits at the left
    // of the view, where a raised left hand would be (M hides it).
    public partial class MLApp
    {
        const float MenuW = .24f;                                    // metres: wider than tall, like a card held between thumb and forefinger
        static readonly Vector3 MenuPin = new Vector3(-.3f, .3f, -.44f);   // where its centre floats when not on the hand: left of the model's near corner
        bool menuIntro, menuPinned, menuHidden;                      // intro: not yet taken onto the hand. pinned: kept in the air by the timeline's button. hidden: desktop, M
        // A panel's place on the left hand: whether it is there, how far it has unfolded, for how long the hand has
        // asked for it or stopped asking, where its lower left corner is, and how far the other hand's fingertip was
        // from its face a frame ago.
        class Dock { public bool on, touchArmed = true; public float grow, askFor, refuseFor, lostFor = 9, touchWas = -1000; public Vector3 at; public Quaternion rot = Quaternion.identity; }
        readonly Dock menuDock = new Dock(), wristDock = new Dock();
        float thumbMax, thumbLogAt;
        const float WristOnHand = .2f;                               // the FIELD panel's width when it is carried on the hand (metres)

        void DrawMenu()
        {
            var s = menu; s.Clear(); const float X = 28; float W = s.w - 2 * X, col = (W - 28) / 2, bw = (col - 12) / 2, bh = 80; int px = Ja ? 23 : 25;
            s.Frame(new Color(10 / 255f, 12 / 255f, 17 / 255f, .95f), Cyan);
            // the map as it lies on the table now: north (the arrow turns as the map is turned), scale, height
            float cx = X + 40, cy = 62, r = 34, th = tv.yawRel * Mathf.Deg2Rad; Vector2 up = new Vector2(Mathf.Sin(th), -Mathf.Cos(th)), side = new Vector2(-up.y, up.x);
            s.Box(cx - r, cy - r, 2 * r, 2 * r, r, new Color(0, 0, 0, 0), new Color(1, 1, 1, .28f));
            Vector2 c = new Vector2(cx, cy), tip = c + up * (r - 8), tail = c - up * (r - 11), barb = tip - up * 16;
            s.Stroke(tail.x, tail.y, tip.x, tip.y, 5, Ink); s.Stroke(tip.x, tip.y, barb.x + side.x * 10, barb.y + side.y * 10, 5, Ink); s.Stroke(tip.x, tip.y, barb.x - side.x * 10, barb.y - side.y * 10, 5, Ink);
            s.Text("N", X + 92, 54, 22, Soft, true); s.Text(ScaleText, X + 118, 58, 36, Ink, true);
            s.Text(tableEx > 1 ? L("height ×2", "高さ ×2") : L("height as measured", "高さ 実寸"), X + 92, 90, 20, Soft);
            s.Wrap(L("MRO HiRISE terrain model, 2 m grid · walls and base drawn by us", "MRO HiRISE の地形モデル(2 m 格子)· 側壁と台はこちらで描画"), X + 330, 54, 400, 24, 17, Gray);
            s.Button(Ja ? "English" : "日本語", s.w - X - 124, 30, 124, 56, ToggleLanguage, px: 22, key: "Language");

            // left: what is done to the map. right: what is done to the room.
            float y0 = 142, x1 = X + col + 28;
            s.Text(L("THE MAP", "地図"), X, y0, 18, Dim, true); s.Text(L("THE ROOM", "部屋と配置"), x1, y0, 18, Dim, true);
            float y = y0 + 14;
            s.Button(L("Slope", "傾斜"), X, y, bw, bh, () => SetLayer("slope"), layer == "slope", px: px, key: "Slope");
            s.Button(L("Spectral", "分光"), X + bw + 12, y, bw, bh, () => SetLayer("crism"), layer == "crism", px: px, key: "Spectral");
            s.Button(layout == "desk" ? L("Layout: desk", "配置: 机") : L("Layout: wall", "配置: 壁"), x1, y, bw, bh, ToggleLayout, px: px, key: "Layout");
            s.Button(tools ? L("Timer: on", "タイマー: 入") : L("Timer: off", "タイマー: 切"), x1 + bw + 12, y, bw, bh, ToggleTools, tools, px: px, key: "Timer"); y += bh + 12;
            s.Button(L("Height ×2", "高さ ×2"), X, y, bw, bh, ToggleHeight, tableEx > 1, px: px, key: "Height ×2");
            s.Button(L("Whole route", "ルート全体"), X + bw + 12, y, bw, bh, WholeRoute, px: px, key: "Whole route");
            if (rig.xr)
            {
                s.Button(studio ? L("Room: studio", "部屋: スタジオ") : L("Room: yours", "部屋: 実際"), x1, y, bw, bh, ToggleStudio, studio, px: px, key: "Room");
                s.Button(L("Bring table here", "机をここへ"), x1 + bw + 12, y, bw, bh, PlaceTable, px: Ja ? 23 : 22, key: "Bring table here");
            }
            y += bh + 12;
            s.Button(followTasks ? L("Follow tasks: on", "作業に追従: 入") : L("Follow tasks: off", "作業に追従: 切"), X, y, col, bh, ToggleFollow, followTasks, px: px, key: "Follow tasks");
            // beside it, how the map itself is handled (and, the first time, how the menu is taken onto the hand)
            if (rig.xr && menuIntro)
                s.Wrap(L("Turn the back of your left hand toward you and open your thumb wide: this menu comes to your hand. Close the thumb and it folds away.", "左手の甲を自分に向けて親指を大きく開くと、このメニューが手に付きます。親指を閉じると消えます。"), x1, y + 26, col, 26, 20, Amber);
            else
                s.Wrap(rig.xr ? L("Pinch at the model and move to slide the map. Both hands: pull apart to stretch, turn to turn.", "模型のところでピンチして動かすと地図が滑ります。両手で広げると拡大、回すと回転。")
                              : L("Drag the model to slide it, wheel to zoom, Q / E to turn. M hides this menu.", "模型をドラッグで移動、ホイールで拡大縮小、Q / E で回転。M でこのメニューを隠します。"), x1, y + 26, col, 25, 19, Gray);
            DrawNeatline();
        }

        /// <summary>Keeps the menu floating beside the model, or lets it go back to the hand.</summary>
        public void ToggleMenuPin()
        {
            menuPinned = !(menuPinned || menuIntro); menuIntro = false; sfx.Play("toggle", .7f); Log("menu " + (menuPinned ? "pinned" : "on the hand")); DrawMenu(); DrawStrip();
        }

        // The left hand as a place to put a panel. The frame is that of the back of the hand: "up" runs from the wrist
        // to the middle knuckle, "right" across the knuckles toward the thumb, and the panel lies in that plane (the
        // thumb itself swings out of the plane as it opens, so it is not used for the angle; it only sets how low the
        // panel sits). The corner is beside the forefinger's knuckle, just above the opened thumb.
        //   thumbDeg  how far the thumb is opened from the line of the hand
        //   toEyes    whether the back of the hand is toward the eyes
        bool HandCorner(out Vector3 corner, out Quaternion rot, out float thumbDeg, out bool toEyes)
        {
            corner = default; rot = Quaternion.identity; thumbDeg = 0; toEyes = false;
            if (!rig.Joint(1, XRHandJointID.Wrist, out var wr) || !rig.Joint(1, XRHandJointID.MiddleProximal, out var mp)
                || !rig.Joint(1, XRHandJointID.IndexProximal, out var ip) || !rig.Joint(1, XRHandJointID.LittleProximal, out var lp)
                || !rig.Joint(1, XRHandJointID.ThumbProximal, out var tp) || !rig.Joint(1, XRHandJointID.ThumbTip, out var tt)) return false;
            Vector3 along = mp - wr, across = Vector3.ProjectOnPlane(ip - lp, along), thumb = tt - tp;
            if (along.sqrMagnitude < 1e-4f || across.sqrMagnitude < 1e-5f || thumb.sqrMagnitude < 4e-4f) return false;
            Vector3 up = along.normalized, right = across.normalized, palmward = Vector3.Cross(right, up);   // palmward: away from the eyes when the back of the hand is toward them
            thumbDeg = Vector3.Angle(thumb, up);
            float thumbTop = Mathf.Max(Vector3.Dot(tt - ip, up), Vector3.Dot(tp - ip, up));   // how far up the hand the thumb reaches, from the forefinger's knuckle (negative: toward the wrist)
            corner = ip + right * .028f + up * (Mathf.Clamp(thumbTop, -.06f, .02f) + .024f);
            toEyes = Vector3.Dot(palmward, (corner - rig.Head.position).normalized) > .3f;
            rot = Quaternion.LookRotation(palmward, up);
            return true;
        }

        // What the left hand says about a panel now: +1 it asks for it (back of the hand toward the eyes, thumb
        // opened wide, in view), -1 it plainly does not (thumb closed, hand turned or lowered), 0 it cannot be seen.
        // Opened wide to call it; it stays until the thumb has come clearly back. (Measured on the headset against
        // the line of the hand: an opened thumb stands at 60 to 75 degrees, a closed or resting one at 25 to 40.
        // Calling it at 66 degrees was too strict.)
        // With a controller in the left hand, which has no fingers to read: raised and looked at.
        int HandAsks(Dock d, out Vector3 corner, out Quaternion rot)
        {
            var l = rig.left; var head = rig.Head.position; corner = d.at; rot = d.rot;
            if (HandCorner(out var hc, out var hr, out float thumbDeg, out bool toEyes))
            {
                bool inView = Vector3.Angle(rig.Head.forward, hc - head) < 55 && Vector3.Distance(hc, head) < .8f;
                if (toEyes && inView)
                {   // for the device log: how far thumbs really open
                    thumbMax = Mathf.Max(thumbMax, thumbDeg);
                    if (Time.time > thumbLogAt) { thumbLogAt = Time.time + 6; Log($"back of the left hand shown: thumb opened up to {thumbMax:0} deg (a panel comes at 50, goes at 40)"); thumbMax = 0; }
                }
                if (!(toEyes && inView && !l.held && thumbDeg > (d.on ? 40 : 50))) return -1;
                corner = hc; rot = hr; return 1;
            }
            if (!l.active || !l.controller) return 0;
            if (l.held || Vector3.Distance(l.origin, head) > .75f || Vector3.Angle(rig.Head.forward, l.origin - head) > 34 || Vector3.Dot(l.rotation * Vector3.forward, rig.Head.forward) > .5f) return -1;
            rot = Quaternion.LookRotation(l.origin + Vector3.up * .15f - head, Vector3.up); corner = l.origin + Vector3.up * .06f + rot * Vector3.left * .1f; return 1;
        }
        // The panel unfolds once the hand has asked for a moment. It folds away as soon as the hand plainly stops
        // asking; if the hand merely cannot be seen (the other hand reaching across hides the fingers) it waits half a
        // second first. Returns +1 when it has just come, -1 when it has just gone.
        static int DockStep(Dock d, int asks, bool allowed, float dt)
        {
            d.askFor = asks > 0 ? d.askFor + dt : 0; d.refuseFor = asks < 0 ? d.refuseFor + dt : 0; d.lostFor = asks == 0 ? d.lostFor + dt : 0; bool was = d.on;
            if (!d.on && allowed && d.askFor > .1f) d.on = true;
            if (d.on && (!allowed || d.refuseFor > .14f || d.lostFor > .5f)) d.on = false;
            d.grow = Mathf.MoveTowards(d.grow, d.on ? 1 : 0, dt / (d.on ? .22f : .14f));
            return d.on == was ? 0 : d.on ? 1 : -1;
        }
        // Where a carried panel is drawn. It grows out of its lower left corner (the corner of the hand) with a small
        // overshoot, and shrinks back into it. It follows the hand smoothly; while the hand has stopped asking or
        // cannot be seen it stays where it was.
        static void DockPlace(Dock d, MLPanel p, float width, bool follow, Vector3 corner, Quaternion rot, float dt)
        {
            if (p.alpha < .05f && d.grow < .05f) { d.at = corner; d.rot = rot; }
            else if (follow) { d.at = Vector3.Lerp(d.at, corner, 1 - Mathf.Exp(-24 * dt)); d.rot = Quaternion.Slerp(d.rot, rot, 1 - Mathf.Exp(-14 * dt)); }
            float g = d.grow, k = Mathf.Max(.03f, d.on ? EaseOut(g) * (1 + .07f * Mathf.Sin(g * Mathf.PI)) : g * g);
            p.SetWorldWidth(width * k);
            p.go.transform.SetPositionAndRotation(d.at + d.rot * new Vector3(width * k / 2, width * k * p.h / p.w / 2, 0), d.rot);
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
            int asks = HandAsks(menuDock, out var corner, out var rot); bool allowed = live && !menuPinned;
            int change = DockStep(menuDock, asks, allowed, dt);
            if (change > 0)
            {
                sfx.Play("toggle", .6f); Log("menu on the hand" + (menuIntro ? " (first time)" : ""));
                if (menuIntro) { menuIntro = false; DrawMenu(); DrawStrip(); }
            }
            else if (change < 0 && live) sfx.Play("undo", .35f);
            bool floating = (menuIntro || menuPinned) && !menuDock.on;
            if (floating)
            {   // beside the model, turned toward where the person stands, at full size
                var centre = tableFrame.TransformPoint(MenuPin); rot = Quaternion.LookRotation(centre - tableFrame.TransformPoint(0, EyeAboveTable, -TableAhead), Vector3.up);
                corner = centre - rot * new Vector3(MenuW / 2, MenuW * menu.h / menu.w / 2, 0);
                menuDock.at = menu.alpha < .05f ? corner : Vector3.Lerp(menuDock.at, corner, 1 - Mathf.Exp(-10 * dt)); menuDock.rot = menu.alpha < .05f ? rot : Quaternion.Slerp(menuDock.rot, rot, 1 - Mathf.Exp(-10 * dt));
                menu.Show = live; menu.SetWorldWidth(MenuW);
                menu.go.transform.SetPositionAndRotation(menuDock.at + menuDock.rot * new Vector3(MenuW / 2, MenuW * menu.h / menu.w / 2, 0), menuDock.rot);
                return;
            }
            menu.Show = live && menuDock.on;
            DockPlace(menuDock, menu, MenuW, asks > 0 && menuDock.on, corner, rot, dt);
        }

        // The FIELD panel is carried the same way (or, called up from the head-up strip, stays where it was called up).
        void UpdateWristOnHand(float dt)
        {
            bool allowed = !OnTable && !wristUp;
            int asks = HandAsks(wristDock, out var corner, out var rot), change = DockStep(wristDock, asks, allowed, dt);
            if (change > 0) { sfx.Play("toggle", .6f); Log("panel on the hand"); } else if (change < 0 && allowed) sfx.Play("undo", .35f);
            if (!wristUp && (wristDock.on || wristDock.grow > 0)) DockPlace(wristDock, wrist, WristOnHand, asks > 0 && wristDock.on, corner, rot, dt);
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
            return $"menu show={menu.Show} alpha={menu.alpha:F2} onHand={menuDock.on} grow={menuDock.grow:F2} intro={menuIntro} pinned={menuPinned} vp=({v.x:F2},{v.y:F2}) at={menu.go.transform.position.ToString("F2")} | panel show={wrist.Show} onHand={wristDock.on}";
        }
        /// <summary>
        /// For checks from the Editor: a left hand held up in front of the camera, its back toward it, with the thumb
        /// opened by `thumbDeg` from the line of the hand (0 or less: take the hand away). Optionally a right forefinger
        /// tip at a point of the menu given in its pixels, `mm` in front of its face (negative) or through it.
        /// </summary>
        public void DebugHand(float thumbDeg, float tipX = -1, float tipY = -1, float mm = -100)
        {
            if (thumbDeg <= 0) { rig.debugJoints = null; if (!rig.xr) { menu.go.transform.SetParent(rig.Head, false); PlaceDesktopMenu(); } return; }
            var h = rig.Head; var j = rig.debugJoints ?? new Dictionary<(int, XRHandJointID), Vector3>();
            Vector3 wr = h.TransformPoint(-.2f, -.2f, .42f), up = h.up, right = h.right;
            Vector3 thumb = Quaternion.AngleAxis(-thumbDeg, h.forward) * up;   // opened toward the person's right
            j[(1, XRHandJointID.Wrist)] = wr; j[(1, XRHandJointID.MiddleProximal)] = wr + up * .09f;
            j[(1, XRHandJointID.IndexProximal)] = wr + up * .09f + right * .022f; j[(1, XRHandJointID.LittleProximal)] = wr + up * .08f - right * .04f;
            j[(1, XRHandJointID.ThumbProximal)] = wr + up * .045f + right * .045f; j[(1, XRHandJointID.ThumbTip)] = wr + up * .045f + right * .045f + thumb * .055f;
            if (tipX >= 0) { var t = menu.go.transform; j[(0, XRHandJointID.IndexTip)] = t.TransformPoint(tipX - menu.w / 2f, menu.h / 2f - tipY, mm / 1000 / t.lossyScale.z); }
            else j.Remove((0, XRHandJointID.IndexTip));
            rig.debugJoints = j;
        }
        // Desktop: at the left of the view, where a raised left hand would be.
        void PlaceDesktopMenu()
        {
            menu.SetWorldWidth(MenuW); menu.go.transform.localPosition = new Vector3(-.43f, -.2f, .5f); menu.go.transform.localRotation = Quaternion.Euler(0, -24, 0);
        }
    }
}
