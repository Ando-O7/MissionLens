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
    public class MLRig : MonoBehaviour
    {
        public class Pointer
        {
            public bool active, held, down;      // down: pressed this frame
            public Vector3 origin; public Quaternion rotation = Quaternion.identity;
            public Ray Ray => new Ray(origin, rotation * Vector3.forward);
            public float hitDistance = -1;       // set by the app each frame, for the ray visual
            public bool controller;              // a held controller rather than a bare hand
            internal LineRenderer line; internal Transform dot; internal InputAction haptic; internal float heldSince;
            // What the pointer has rested on for a moment. A pinch or a trigger pull jerks the ray; the press goes
            // to what was being pointed at just before it.
            public object restPanel, hoverPanel; public string restKey, hoverKey; public float hoverSince, restAt = -10;
            /// <summary>Seconds the press has been held (0 when not held).</summary>
            public float HeldFor => held ? Time.time - heldSince : 0;
        }

        public Camera cam;
        public bool xr;
        public readonly Pointer right = new Pointer(), left = new Pointer(), mouse = new Pointer();
        public Transform Head => cam.transform;
        public float HeadYaw => cam.transform.eulerAngles.y;

        // Desktop only: where the camera looks, and whether a press turned into a drag.
        public float lookYaw, lookPitch = -30;
        public float lastDragTime = -10;
        public bool wristToggle;                 // desktop: Space pressed this frame
        public bool anyKey;                      // desktop: Enter pressed this frame (stands in for the first press)
        /// <summary>For checks from the Editor: aim the desktop pointer through this viewport point instead of the mouse.</summary>
        public Vector2? debugAim; public bool debugClick;
        Vector2 pressAt; bool pressed, dragged; Vector2 pressLook;

        Transform space;                         // tracking space
        ARCameraManager passthrough;

        /// <summary>Draw the tracked hands as thin outlines. Wanted once the room is gone and the real hands with it.</summary>
        public bool showHands;
        XRHandSubsystem handSystem; float nextHandSearch;
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
            space = new GameObject("Camera Offset").transform; space.SetParent(transform, false);
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" }; camGo.transform.SetParent(space, false);
            cam = camGo.AddComponent<Camera>(); camGo.AddComponent<AudioListener>();
            cam.nearClipPlane = .03f; cam.farClipPlane = 8000; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.clear;
            if (xr)
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
                MakeVisual(right, "Right pointer"); MakeVisual(left, "Left pointer");
                var handMat = new Material(Resources.Load<Shader>("MLShaders/MLColor")); handMat.SetColor("_Color", new Color(.92f, .96f, 1f, .5f)); handMat.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Always); handMat.renderQueue = 3995;
                for (int i = 0; i < handLines.Length; i++)
                {
                    var hg = new GameObject("Hand line " + i); hg.transform.SetParent(transform, false);
                    var lr = hg.AddComponent<LineRenderer>(); lr.material = handMat; lr.useWorldSpace = true; lr.startWidth = lr.endWidth = .0045f; lr.numCapVertices = 2; lr.numCornerVertices = 2; lr.enabled = false; handLines[i] = lr;
                }
                right.haptic = new InputAction("haptic right", InputActionType.PassThrough, "<XRController>{RightHand}/{Haptic}"); right.haptic.Enable();
                left.haptic = new InputAction("haptic left", InputActionType.PassThrough, "<XRController>{LeftHand}/{Haptic}"); left.haptic.Enable();
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
            p.line = go.AddComponent<LineRenderer>(); p.line.material = mat; p.line.positionCount = 2; p.line.startWidth = .004f; p.line.endWidth = .002f; p.line.useWorldSpace = true; p.line.enabled = false;
            var dot = new GameObject(name + " dot"); dot.transform.SetParent(transform, false);
            dot.AddComponent<MeshFilter>().sharedMesh = MLMesh.Octa(); dot.AddComponent<MeshRenderer>().sharedMaterial = mat; p.dot = dot.transform; dot.SetActive(false);
        }

        /// <summary>
        /// Whether the room is (still partly) visible. On Quest that keeps passthrough on behind a transparent
        /// background; the app's sky sphere covers it by its own opacity. On a desktop the room is a flat colour.
        /// </summary>
        public void SetBackground(bool seeRoom, Color mockRoom)
        {
            if (passthrough != null && passthrough.enabled != seeRoom) passthrough.enabled = seeRoom;
            cam.backgroundColor = xr ? Color.clear : mockRoom;
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
            if (xr) { ReadHand(right, true); ReadHand(left, false); DrawHands(); }
            else ReadDesktop();
        }

        // Finger outlines from the hand-tracking joints, in place of the hands the person can no longer see.
        void DrawHands()
        {
            if (handSystem == null || !handSystem.running)
            {
                if (Time.time < nextHandSearch) { HideHands(0, 10); return; }
                nextHandSearch = Time.time + 1; SubsystemManager.GetSubsystems(handSystems);
                handSystem = handSystems.Find(h => h.running) ?? (handSystems.Count > 0 ? handSystems[0] : null);
                if (handSystem == null) { HideHands(0, 10); return; }
            }
            for (int side = 0; side < 2; side++)
            {
                var hand = side == 0 ? handSystem.rightHand : handSystem.leftHand;
                if (!showHands || !hand.isTracked) { HideHands(side * 5, 5); continue; }
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

        void ReadHand(Pointer p, bool isRight)
        {
            bool was = p.held; p.active = false; p.held = false; p.controller = false;
            var aim = isRight ? MetaAimHand.right : MetaAimHand.left;
            if (aim != null && aim.added && ((MetaAimFlags)aim.aimFlags.value & MetaAimFlags.Valid) != 0)
            {
                p.active = true; p.origin = space.TransformPoint(aim.devicePosition.value); p.rotation = space.rotation * aim.deviceRotation.value; p.held = aim.indexPressed.isPressed;
            }
            else
            {
                var c = isRight ? XRController.rightHand : XRController.leftHand;
                if (c != null && c.added && c.isTracked.isPressed)
                {
                    var pos = c.TryGetChildControl<Vector3Control>("pointerPosition") ?? c.devicePosition;
                    var rot = c.TryGetChildControl<QuaternionControl>("pointerRotation") ?? c.deviceRotation;
                    var trigger = c.TryGetChildControl<ButtonControl>("triggerPressed");
                    p.active = true; p.controller = true; p.origin = space.TransformPoint(pos.value); p.rotation = space.rotation * rot.value; p.held = trigger != null && trigger.isPressed;
                }
            }
            p.down = p.active && p.held && !was;
            if (p.down) p.heldSince = Time.time;
        }

        void ReadDesktop()
        {
            var m = Mouse.current; var k = Keyboard.current;
            wristToggle = k != null && k.spaceKey.wasPressedThisFrame;
            anyKey = k != null && (k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame);
            mouse.down = false;
            if (m == null) { mouse.active = false; return; }
            Vector2 at = m.position.ReadValue();
            if (debugAim.HasValue)
            {
                cam.transform.localRotation = Quaternion.Euler(-lookPitch, lookYaw, 0);
                var dr = cam.ViewportPointToRay(new Vector3(debugAim.Value.x, debugAim.Value.y, 0));
                mouse.active = true; mouse.origin = dr.origin; mouse.rotation = Quaternion.LookRotation(dr.direction); mouse.down = debugClick; debugClick = false;
                return;
            }
            if (m.leftButton.wasPressedThisFrame) { pressed = true; dragged = false; pressAt = at; pressLook = new Vector2(lookYaw, lookPitch); }
            if (pressed && m.leftButton.isPressed)
            {
                if ((at - pressAt).magnitude > 4) dragged = true;
                if (dragged)
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
                bool hit = p.active && p.hitDistance > 0;
                p.line.enabled = p.active; p.dot.gameObject.SetActive(hit);
                if (!p.active) continue;
                var end = p.origin + p.rotation * Vector3.forward * (hit ? p.hitDistance : .09f);
                p.line.SetPosition(0, p.origin + p.rotation * Vector3.forward * .02f); p.line.SetPosition(1, end);
                p.line.startWidth = hit ? .004f : .003f; p.line.endWidth = hit ? .002f : .0005f;
                if (hit) { p.dot.position = end; p.dot.localScale = Vector3.one * Mathf.Max(.006f, .012f * p.hitDistance) * (p.held ? .65f : 1f); }
            }
        }
    }
}
