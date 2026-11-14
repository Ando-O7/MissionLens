using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR.Input;

namespace MissionLens
{
    /// <summary>
    /// The person: head camera and what they point with. On Quest this is the tracked head, passthrough and
    /// a pointer per hand (pinch) or controller (trigger). Without a headset (the Editor on a desktop) it is a
    /// camera you turn by dragging and a pointer under the mouse, so the same app can be checked on a screen.
    /// The rig itself never moves: the terrain is placed around the person.
    /// </summary>
    public partial class MLRig : MonoBehaviour
    {
        public class Pointer
        {
            public bool active, held, down;      // down: pressed this frame
            public Vector3 origin; public Quaternion rotation = Quaternion.identity;
            public Ray Ray => new Ray(origin, rotation * Vector3.forward);
            // How the hand itself is turned: the wrist joint from hand tracking, or the controller. For things that
            // are turned in the hand; the pointing ray is steadied and barely rolls. gripAxis is the hand's long
            // axis (wrist toward the middle finger's knuckle), the axis such a turn is measured about.
            public Quaternion grip = Quaternion.identity; public Vector3 gripAt, gripAxis = Vector3.forward;
            // Only the forefinger is held out, the other fingers curled: the hand is about to touch something near, not
            // to point at something far. No ray is drawn from it and it points at nothing.
            public bool fingerOnly;
            public float hitDistance = -1;       // set by the app each frame, for the ray visual
            public bool controller;              // a held controller rather than a bare hand
            public bool grabDown;                // began holding this frame (a pinch or trigger; on a desktop, the mouse button going down)
            public bool overSurface;             // set by the app: the pointer is on a panel or label
            public object tool;                  // set by the app: the thing on the desk this pointer has hold of, if any
            internal LineRenderer line; internal Transform dot; internal InputAction haptic; internal float heldSince;
            // What the pointer has rested on for a moment. A pinch or a trigger pull jerks the ray; the press goes
            // to what was being pointed at just before it.
            public object restPanel, hoverPanel; public string restKey, hoverKey; public float hoverSince, restAt = -10;
            /// <summary>Seconds the press has been held (0 when not held).</summary>
            public float HeldFor => held ? Time.time - heldSince : 0;
        }

        public Camera cam;
        public bool xr;
        /// <summary>Editor only (MLRigSim.cs): no headset, but the rig behaves as on one, with a head and hands placed by a script.</summary>
        public bool sim;
        public readonly Pointer right = new Pointer(), left = new Pointer(), mouse = new Pointer();
        /// <summary>
        /// In the headset, a fallback for when pointing does not work: the direction of the head, so the app can be
        /// worked by looking at a button and waiting. It is on while no hand or controller is tracked, and goes
        /// away for good once a pinch or a trigger press has got through, so that someone resting their hands
        /// does not press things by looking at them.
        /// </summary>
        public readonly Pointer gaze = new Pointer();
        float lastHandTime; bool everPressed;
        public Transform Head => cam.transform;
        public float HeadYaw => cam.transform.eulerAngles.y;

        // Desktop only: where the camera looks, and whether a press turned into a drag.
        public float lookYaw, lookPitch = -30;
        public float lastDragTime = -10;
        public bool wristToggle;                 // desktop: Space pressed this frame
        public bool anyKey;                      // desktop: Enter pressed this frame (stands in for the first press)
        public bool menuKey;                     // desktop: M pressed this frame (shows or hides the menu, which in the headset is on the left hand)
        /// <summary>For checks from the Editor: aim the desktop pointer through this viewport point instead of the mouse.</summary>
        public Vector2? debugAim; public bool debugClick, debugHold;
        /// <summary>For checks from the Editor: how far the desktop pointer's "hand" is rolled, in degrees.</summary>
        public float debugRoll;
        // Desktop only: wheel steps and Q / E this frame, and whether a drag is moving the table's map (not the view).
        public float wheel, turn; public bool lookLocked;
        /// <summary>0 while the person can see their real hands (passthrough), 1 once they cannot. Sets how solid the drawn hands are.</summary>
        public float handPresence;
        Vector2 pressAt; bool pressed, dragged; Vector2 pressLook;

        Transform space;                         // tracking space
        ARCameraManager passthrough;

        // The tracked hands are drawn as hand models (from Unity's XR Hands sample, driven by the tracked joints).
        // If the models cannot be set up, thin finger outlines are drawn instead once the real hands are out of sight.
        class HandModel { public GameObject go; public Renderer r; public Material mat; public XRHandTrackingEvents events; public int joints; }
        readonly HandModel[] handModels = new HandModel[2];   // right, left
        XRHandSubsystem handSystem; float nextHandSearch; bool handsFailed;
        readonly LineRenderer[] handLines = new LineRenderer[10];   // five fingers per hand, right hand first
        static readonly List<XRHandSubsystem> handSystems = new List<XRHandSubsystem>();
        static readonly XRHandJointID[][] Fingers =
        {
            new[] { XRHandJointID.Wrist, XRHandJointID.ThumbMetacarpal, XRHandJointID.ThumbProximal, XRHandJointID.ThumbDistal, XRHandJointID.ThumbTip },
            new[] { XRHandJointID.Wrist, XRHandJointID.IndexMetacarpal, XRHandJointID.IndexProximal, XRHandJointID.IndexIntermediate, XRHandJointID.IndexDistal, XRHandJointID.IndexTip },
            new[] { XRHandJointID.Wrist, XRHandJointID.MiddleMetacarpal, XRHandJointID.MiddleProximal, XRHandJointID.MiddleIntermediate, XRHandJointID.MiddleDistal, XRHandJointID.MiddleTip },
            new[] { XRHandJointID.Wrist, XRHandJointID.RingMetacarpal, XRHandJointID.RingProximal, XRHandJointID.RingIntermediate, XRHandJointID.RingDistal, XRHandJointID.RingTip },
            new[] { XRHandJointID.Wrist, XRHandJointID.LittleMetacarpal, XRHandJointID.LittleProximal, XRHandJointID.LittleIntermediate, XRHandJointID.LittleDistal, XRHandJointID.LittleTip },
        };

        /// <summary>
        /// Draw order for the hands and the pointers. Panels that are held in front of others are given an order of
        /// their own (MLPanel.Order); the hand that reaches for them must still be drawn over them.
        /// </summary>
        public const int OverPanels = 100;

        public static MLRig Create()
        {
            var go = new GameObject("Rig");
            var rig = go.AddComponent<MLRig>();
            rig.Build();
            return rig;
        }

        void Build()
        {
            xr = XRGeneralSettings.Instance != null && XRGeneralSettings.Instance.Manager != null && XRGeneralSettings.Instance.Manager.activeLoader != null;
#if UNITY_EDITOR
            if (!xr && PlayerPrefs.GetInt("ml.sim", 0) == 1) { sim = true; xr = true; }
#endif
            space = new GameObject("Camera Offset").transform; space.SetParent(transform, false);
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" }; camGo.transform.SetParent(space, false);
            cam = camGo.AddComponent<Camera>(); camGo.AddComponent<AudioListener>();
            cam.nearClipPlane = .03f; cam.farClipPlane = 8000; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.clear;
            if (xr && !sim)
            {
                var origin = gameObject.AddComponent<XROrigin>();
                origin.Camera = cam; origin.CameraFloorOffsetObject = space.gameObject; origin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Floor;
                var pose = camGo.AddComponent<TrackedPoseDriver>();
                var p = new InputAction("head position", binding: "<XRHMD>/centerEyePosition", expectedControlType: "Vector3");
                var r = new InputAction("head rotation", binding: "<XRHMD>/centerEyeRotation", expectedControlType: "Quaternion");
                pose.positionInput = new InputActionProperty(p); pose.rotationInput = new InputActionProperty(r); p.Enable(); r.Enable();
                var session = new GameObject("AR Session"); session.transform.SetParent(transform, false);
                session.AddComponent<ARSession>(); session.AddComponent<ARInputManager>();
                passthrough = camGo.AddComponent<ARCameraManager>();   // on Quest, enabling this component turns passthrough on
                // Render the eyes at a higher resolution than the default: thin text and distant edges crawl less.
                // The first run on a Quest 3 used about a third of the GPU at the default, so there is room.
                UnityEngine.XR.XRSettings.eyeTextureResolutionScale = 1.3f;
                MakeHandModel(0); MakeHandModel(1);
            }
            if (xr)
            {
                MakeVisual(right, "Right pointer"); MakeVisual(left, "Left pointer");
                var handMat = new Material(Resources.Load<Shader>("MLShaders/MLColor")); handMat.SetColor("_Color", new Color(.92f, .96f, 1f, .5f)); handMat.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Always); handMat.renderQueue = 3995;
                for (int i = 0; i < handLines.Length; i++)
                {
                    var hg = new GameObject("Hand line " + i); hg.transform.SetParent(transform, false);
                    var lr = hg.AddComponent<LineRenderer>(); lr.material = handMat; lr.useWorldSpace = true; lr.startWidth = lr.endWidth = .0045f; lr.numCapVertices = 2; lr.numCornerVertices = 2; lr.enabled = false; lr.sortingOrder = OverPanels; handLines[i] = lr;
                }
                right.haptic = new InputAction("haptic right", InputActionType.PassThrough, "<XRController>{RightHand}/{Haptic}"); right.haptic.Enable();
                left.haptic = new InputAction("haptic left", InputActionType.PassThrough, "<XRController>{LeftHand}/{Haptic}"); left.haptic.Enable();
#if UNITY_EDITOR
                if (sim) BuildSim();
#endif
            }
            else
            {
                cam.fieldOfView = 70; cam.transform.localPosition = new Vector3(0, 1.6f, 0);
            }
        }

        void MakeVisual(Pointer p, string name)
        {
            var mat = new Material(Resources.Load<Shader>("MLShaders/MLColor")); mat.SetColor("_Color", new Color(.37f, .83f, 1f, .7f)); mat.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Always); mat.renderQueue = 4000;
            var go = new GameObject(name); go.transform.SetParent(transform, false);
            p.line = go.AddComponent<LineRenderer>(); p.line.material = mat; p.line.positionCount = 2; p.line.startWidth = .004f; p.line.endWidth = .002f; p.line.useWorldSpace = true; p.line.enabled = false; p.line.sortingOrder = OverPanels;
            var dot = new GameObject(name + " dot"); dot.transform.SetParent(transform, false);
            dot.AddComponent<MeshFilter>().sharedMesh = MLMesh.Octa(); var dr = dot.AddComponent<MeshRenderer>(); dr.sharedMaterial = mat; dr.sortingOrder = OverPanels; p.dot = dot.transform; dot.SetActive(false);
        }

        /// <summary>
        /// Whether the room is (still partly) visible. On Quest that keeps passthrough on behind a transparent
        /// background; the app's sky sphere covers it by its own opacity. On a desktop the room is a flat colour.
        /// </summary>
        public void SetBackground(bool seeRoom, Color mockRoom)
        {
            if (passthrough != null && passthrough.enabled != seeRoom) passthrough.enabled = seeRoom;
            cam.backgroundColor = xr && !sim ? Color.clear : mockRoom;
        }

        /// <summary>A short buzz on a held controller. Bare hands have nothing to buzz.</summary>
        public void Haptic(Pointer p, float amplitude, float seconds)
        {
            if (!xr || !p.controller || p.haptic == null) return;
            try { OpenXRInput.SendHapticImpulse(p.haptic, amplitude, seconds, p == right ? XRController.rightHand : XRController.leftHand); }
            catch (System.Exception) { p.haptic = null; }   // not supported by this runtime: stop trying
        }

        void Update()
        {
#if UNITY_EDITOR
            if (sim) { ReadSim(); return; }
#endif
            if (xr)
            {
                ReadHand(right, true); ReadHand(left, false);
                // The outlines are a nicety; if the hand data misbehaves on some runtime, drop them rather than the frame.
                if (!handsFailed) try { FindHands(); ReadGrip(right, 0); ReadGrip(left, 1); UpdateHandModels(); DrawHands(); } catch (System.Exception e) { handsFailed = true; HideHands(0, 10); Debug.LogWarning("Hand drawing switched off: " + e.Message); }
                if (right.active || left.active) lastHandTime = Time.time;
                if (right.down || left.down) everPressed = true;
                gaze.active = !everPressed && Time.time - lastHandTime > 2.5f; gaze.origin = Head.position; gaze.rotation = Head.rotation;
            }
            else { ReadDesktop(); mouse.grip = mouse.rotation * Quaternion.Euler(0, 0, debugRoll); mouse.gripAt = mouse.origin; mouse.gripAxis = mouse.rotation * Vector3.forward; }
        }

        /// <summary>Where a joint of a tracked hand is, in the world. side: 0 right, 1 left. False when that hand is not tracked.</summary>
        public bool Joint(int side, XRHandJointID id, out Vector3 at)
        {
            at = default;
#if UNITY_EDITOR
            if (sim) return SimJoint(side, id, out at);
#endif
            if (debugJoints != null) return debugJoints.TryGetValue((side, id), out at);
            if (!xr || handsFailed || handSystem == null || !handSystem.running) return false;
            var hand = side == 0 ? handSystem.rightHand : handSystem.leftHand;
            if (!hand.isTracked || !hand.GetJoint(id).TryGetPose(out UnityEngine.Pose pose)) return false;
            at = space.TransformPoint(pose.position); return true;
        }
        /// <summary>For checks from the Editor, where no hand is tracked: joint positions to report instead.</summary>
        public Dictionary<(int, XRHandJointID), Vector3> debugJoints;

        void FindHands()
        {
            if ((handSystem != null && handSystem.running) || Time.time < nextHandSearch) return;
            nextHandSearch = Time.time + 1; SubsystemManager.GetSubsystems(handSystems);
            handSystem = handSystems.Find(h => h.running) ?? (handSystems.Count > 0 ? handSystems[0] : null);
        }
        void ReadGrip(Pointer p, int side)
        {
            if (!p.active || p.controller || handSystem == null || !handSystem.running) { p.fingerOnly = false; return; }
            var hand = side == 0 ? handSystem.rightHand : handSystem.leftHand;
            if (!hand.isTracked || !hand.GetJoint(XRHandJointID.Wrist).TryGetPose(out UnityEngine.Pose pose)) { p.fingerOnly = false; return; }
            // how straight each finger is: the distance from knuckle to tip over the length of its bones (1 = straight, about 0.5 = curled into the palm)
            float fi = Straight(hand, XRHandJointID.IndexProximal, XRHandJointID.IndexIntermediate, XRHandJointID.IndexDistal, XRHandJointID.IndexTip),
                  fm = Straight(hand, XRHandJointID.MiddleProximal, XRHandJointID.MiddleIntermediate, XRHandJointID.MiddleDistal, XRHandJointID.MiddleTip),
                  fr = Straight(hand, XRHandJointID.RingProximal, XRHandJointID.RingIntermediate, XRHandJointID.RingDistal, XRHandJointID.RingTip);
            if (fi < 0 || fm < 0 || fr < 0) p.fingerOnly = false;
            else p.fingerOnly = p.fingerOnly ? fi > .86f && fm < .9f && fr < .9f : fi > .94f && fm < .8f && fr < .8f;   // firmly out to begin with; it then holds a little slack
            p.grip = space.rotation * pose.rotation; p.gripAt = space.TransformPoint(pose.position); p.gripAxis = p.grip * Vector3.forward;
            if (hand.GetJoint(XRHandJointID.MiddleProximal).TryGetPose(out UnityEngine.Pose knuckle))
            {
                var along = space.TransformPoint(knuckle.position) - p.gripAt; if (along.sqrMagnitude > 1e-4f) p.gripAxis = along.normalized;
            }
        }

        static float Straight(XRHand hand, XRHandJointID knuckle, XRHandJointID mid, XRHandJointID far, XRHandJointID tip)
        {
            if (!hand.GetJoint(knuckle).TryGetPose(out UnityEngine.Pose a) || !hand.GetJoint(mid).TryGetPose(out UnityEngine.Pose b)
                || !hand.GetJoint(far).TryGetPose(out UnityEngine.Pose c) || !hand.GetJoint(tip).TryGetPose(out UnityEngine.Pose d)) return -1;
            float bones = Vector3.Distance(a.position, b.position) + Vector3.Distance(b.position, c.position) + Vector3.Distance(c.position, d.position);
            return bones < 1e-4f ? -1 : Vector3.Distance(a.position, d.position) / bones;
        }

        // Finger outlines from the hand-tracking joints, in place of the hands the person can no longer see.
        void DrawHands()
        {
            if (handSystem == null || !handSystem.running) { HideHands(0, 10); return; }
            for (int side = 0; side < 2; side++)
            {
                var hand = side == 0 ? handSystem.rightHand : handSystem.leftHand;
                if (handModels[side] != null || handPresence < .5f || !hand.isTracked) { HideHands(side * 5, 5); continue; }
                for (int f = 0; f < 5; f++)
                {
                    var lr = handLines[side * 5 + f]; var ids = Fingers[f]; bool ok = true;
                    if (lr.positionCount != ids.Length) lr.positionCount = ids.Length;
                    for (int j = 0; j < ids.Length && ok; j++)
                    {
                        if (hand.GetJoint(ids[j]).TryGetPose(out UnityEngine.Pose pose)) lr.SetPosition(j, space.TransformPoint(pose.position)); else ok = false;
                    }
                    lr.enabled = ok;
                }
            }
        }
        void HideHands(int from, int count) { for (int i = from; i < from + count; i++) if (handLines[i] != null && handLines[i].enabled) handLines[i].enabled = false; }

        // A hand model whose joints follow the tracked hand. side: 0 right, 1 left.
        // The driver component is set up while the object is switched off: added to a live object it would run
        // before it has its list of joints, fail, and then throw on every hand update. (That happened on the
        // first try; on a Quest the exception also stopped the passthrough layer from being submitted.)
        void MakeHandModel(int side)
        {
            GameObject go = null;
            try
            {
                var prefab = Resources.Load<GameObject>("MLHands/" + (side == 0 ? "RightHand" : "LeftHand")); var shader = Resources.Load<Shader>("MLShaders/MLHand");
                if (prefab == null || shader == null) return;
                go = Instantiate(prefab, space); go.SetActive(false); go.name = side == 0 ? "Right hand" : "Left hand";
                go.transform.localPosition = Vector3.zero; go.transform.localRotation = Quaternion.identity;
                Transform wrist = null;
                foreach (var t in go.GetComponentsInChildren<Transform>(true)) if (t.name.EndsWith("Wrist")) { wrist = t; break; }
                var skin = go.GetComponentInChildren<SkinnedMeshRenderer>(true);
                if (wrist == null || skin == null) throw new System.InvalidOperationException("hand model has no wrist joint or no skinned mesh");
                var events = go.AddComponent<XRHandTrackingEvents>(); events.handedness = side == 0 ? Handedness.Right : Handedness.Left; events.updateType = XRHandTrackingEvents.UpdateTypes.BeforeRender;
                var driver = go.AddComponent<XRHandSkeletonDriver>();
                driver.jointTransformReferences = new List<JointToTransformReference>(); driver.rootTransform = wrist; driver.handTrackingEvents = events;
                var missing = new List<string>(); driver.FindJointsFromRoot(missing); driver.InitializeFromSerializedReferences();
                if (driver.jointTransformReferences.Count < 20) throw new System.InvalidOperationException("only " + driver.jointTransformReferences.Count + " hand joints found; missing " + string.Join(", ", missing));
                var mat = new Material(shader); var mats = new Material[skin.sharedMaterials.Length]; for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                skin.sharedMaterials = mats; skin.updateWhenOffscreen = true; skin.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; skin.receiveShadows = false; skin.enabled = false; skin.sortingOrder = OverPanels;
                go.SetActive(true);
                handModels[side] = new HandModel { go = go, r = skin, mat = mat, events = events, joints = driver.jointTransformReferences.Count };
            }
            catch (System.Exception e)
            {
                handModels[side] = null; if (go != null) Destroy(go);   // leave nothing behind that still listens to the hands
                Debug.LogWarning("Hand model not available, using outlines: " + e.Message);
            }
        }
        /// <summary>For checks from the Editor, where there is no hand tracking: build the two hand models and report what was wired up.</summary>
        public string TestHandModels()
        {
            if (space == null) return "no rig";
            MakeHandModel(0); MakeHandModel(1);
            string r = ""; for (int i = 0; i < 2; i++) r += (i == 0 ? "right: " : " | left: ") + (handModels[i] == null ? "not built" : handModels[i].joints + " joints, mesh " + handModels[i].r.name);
            for (int i = 0; i < 2; i++) if (handModels[i] != null) { Destroy(handModels[i].go); handModels[i] = null; }
            return r;
        }

        // Faint with a bright edge while the real hand is visible behind it; more solid once it is not. Turns
        // toward the accent colour while pinching, so a pinch is seen to have registered.
        void UpdateHandModels()
        {
            for (int side = 0; side < 2; side++)
            {
                var m = handModels[side]; if (m == null) continue;
                bool tracked = m.events != null && m.events.handIsTracked;
                if (m.r.enabled != tracked) m.r.enabled = tracked;
                if (!tracked) continue;
                var p = side == 0 ? right : left; float k = handPresence;
                m.mat.SetColor("_Color", p.held && !p.controller ? new Color(.37f, .83f, 1f, 1) : new Color(.88f, .94f, 1f, 1));
                m.mat.SetFloat("_Body", Mathf.Lerp(.03f, .2f, k) + (p.held ? .12f : 0)); m.mat.SetFloat("_Rim", Mathf.Lerp(.5f, .8f, k));
            }
        }

        void ReadHand(Pointer p, bool isRight)
        {
            bool was = p.held; p.active = false; p.held = false; p.controller = false;
            var aim = isRight ? MetaAimHand.right : MetaAimHand.left;
            if (aim != null && aim.added && ((MetaAimFlags)aim.aimFlags.value & MetaAimFlags.Valid) != 0)
            {
                p.active = true; p.origin = space.TransformPoint(aim.devicePosition.value); p.rotation = p.grip = space.rotation * aim.deviceRotation.value; p.gripAt = p.origin; p.gripAxis = p.rotation * Vector3.forward; p.held = aim.indexPressed.isPressed;
            }
            else
            {
                var c = isRight ? XRController.rightHand : XRController.leftHand;
                if (c != null && c.added && c.isTracked.isPressed)
                {
                    var pos = c.TryGetChildControl<Vector3Control>("pointerPosition") ?? c.devicePosition;
                    var rot = c.TryGetChildControl<QuaternionControl>("pointerRotation") ?? c.deviceRotation;
                    var trigger = c.TryGetChildControl<ButtonControl>("triggerPressed");
                    p.active = true; p.controller = true; p.origin = space.TransformPoint(pos.value); p.rotation = p.grip = space.rotation * rot.value; p.gripAt = p.origin; p.gripAxis = p.rotation * Vector3.forward; p.held = trigger != null && trigger.isPressed;
                }
            }
            p.down = p.grabDown = p.active && p.held && !was;
            if (p.down) p.heldSince = Time.time;
        }

        void ReadDesktop()
        {
            var m = Mouse.current; var k = Keyboard.current;
            wristToggle = k != null && k.spaceKey.wasPressedThisFrame;
            anyKey = k != null && (k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame);
            menuKey = k != null && k.mKey.wasPressedThisFrame;
            mouse.down = mouse.grabDown = false; wheel = 0; turn = 0;
            if (m == null) { mouse.active = false; return; }
            wheel = Mathf.Clamp(m.scroll.ReadValue().y, -1, 1) ; if (k != null) turn = (k.eKey.isPressed ? 1 : 0) - (k.qKey.isPressed ? 1 : 0);
            Vector2 at = m.position.ReadValue();
            if (debugAim.HasValue)
            {
                cam.transform.localRotation = Quaternion.Euler(-lookPitch, lookYaw, 0);
                var dr = cam.ViewportPointToRay(new Vector3(debugAim.Value.x, debugAim.Value.y, 0));
                mouse.active = true; mouse.origin = dr.origin; mouse.rotation = Quaternion.LookRotation(dr.direction); mouse.down = debugClick; debugClick = false;
                mouse.grabDown = debugHold && !mouse.held; mouse.held = debugHold;
                return;
            }
            if (m.leftButton.wasPressedThisFrame) { pressed = true; dragged = false; pressAt = at; pressLook = new Vector2(lookYaw, lookPitch); mouse.grabDown = true; }
            mouse.held = m.leftButton.isPressed;
            if (pressed && m.leftButton.isPressed)
            {
                if ((at - pressAt).magnitude > 4) dragged = true;
                if (dragged && !lookLocked)
                {
                    float kdeg = cam.fieldOfView / Screen.height;
                    lookYaw = pressLook.x - (at.x - pressAt.x) * kdeg; lookPitch = Mathf.Clamp(pressLook.y + (at.y - pressAt.y) * kdeg, -85, 60); lastDragTime = Time.time;
                }
            }
            if (pressed && m.leftButton.wasReleasedThisFrame) { pressed = false; mouse.down = !dragged; }
            cam.transform.localRotation = Quaternion.Euler(-lookPitch, lookYaw, 0);
            var ray = cam.ScreenPointToRay(at);
            mouse.active = true; mouse.origin = ray.origin; mouse.rotation = Quaternion.LookRotation(ray.direction);
        }

        void LateUpdate()
        {
            if (!xr) return;
            foreach (var p in new[] { right, left })
            {
                // Aimed at something: a line to it and a dot on it. Otherwise a short stub, so the hand is seen to point.
                bool on = p.active && !p.fingerOnly, hit = on && p.hitDistance > 0;
                p.line.enabled = on; p.dot.gameObject.SetActive(hit);
                if (!on) continue;
                var end = p.origin + p.rotation * Vector3.forward * (hit ? p.hitDistance : .09f);
                p.line.SetPosition(0, p.origin + p.rotation * Vector3.forward * .02f); p.line.SetPosition(1, end);
                p.line.startWidth = hit ? .004f : .003f; p.line.endWidth = hit ? .002f : .0005f;
                if (hit) { p.dot.position = end; p.dot.localScale = Vector3.one * Mathf.Max(.006f, .012f * p.hitDistance) * (p.held ? .65f : 1f); }
            }
        }
    }
}
