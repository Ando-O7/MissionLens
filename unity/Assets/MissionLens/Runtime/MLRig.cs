using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Management;

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
            internal LineRenderer line; internal Transform dot;
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
        Vector2 pressAt; bool pressed, dragged; Vector2 pressLook;

        Transform space;                         // tracking space
        ARCameraManager passthrough;

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

        /// <summary>MR (see the room) or VR (a flat sky colour behind everything).</summary>
        public void SetBackground(bool seeRoom, Color sky, Color mockRoom)
        {
            if (passthrough != null) passthrough.enabled = seeRoom;
            cam.backgroundColor = xr ? (seeRoom ? Color.clear : sky) : (seeRoom ? mockRoom : sky);
        }

        void Update()
        {
            if (xr) { ReadHand(right, true); ReadHand(left, false); }
            else ReadDesktop();
        }

        void ReadHand(Pointer p, bool isRight)
        {
            bool was = p.held; p.active = false; p.held = false;
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
                    p.active = true; p.origin = space.TransformPoint(pos.value); p.rotation = space.rotation * rot.value; p.held = trigger != null && trigger.isPressed;
                }
            }
            p.down = p.active && p.held && !was;
        }

        void ReadDesktop()
        {
            var m = Mouse.current; var k = Keyboard.current;
            wristToggle = k != null && k.spaceKey.wasPressedThisFrame;
            mouse.down = false;
            if (m == null) { mouse.active = false; return; }
            Vector2 at = m.position.ReadValue();
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
                bool show = p.active && p.hitDistance > 0;
                p.line.enabled = show; p.dot.gameObject.SetActive(show);
                if (!show) continue;
                var end = p.origin + p.rotation * Vector3.forward * p.hitDistance;
                p.line.SetPosition(0, p.origin); p.line.SetPosition(1, end);
                p.dot.position = end; p.dot.localScale = Vector3.one * Mathf.Max(.006f, .012f * p.hitDistance) * (p.held ? .7f : 1f);
            }
        }
    }
}
