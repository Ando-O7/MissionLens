#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;

namespace MissionLens
{
    // For recording a demonstration in the Editor, where there is no headset. With PlayerPrefs "ml.sim" = 1 the rig
    // behaves as it does on the headset (the app takes all its headset paths), but the head and the two hands are
    // placed by a script (MLAppDirector.cs) instead of by tracking. The hands are the same models as on the
    // headset, posed here; what the app reads (pointing ray, pinch, joint positions) comes from those models, so
    // what is seen and what the app reacts to are the same thing. None of this is compiled into the headset build.
    public partial class MLRig
    {
        public class SimHand
        {
            public bool on;
            // Where the hand is asked to be; it eases there. rot: forward runs along the hand to the fingers, up comes out of the back of the hand.
            public Vector3 wristTo, aimTo; public Quaternion rotTo = Quaternion.identity;
            public float curlTo = .1f, indexTo = .1f, thumbTo = .25f;   // the three outer fingers and the forefinger: -0.3 straight .. 1 closed. thumb: 0 by the forefinger .. 1 opened wide
            public bool pinch, point;                                   // pinch: thumb and forefinger together. point: only the forefinger out
            public float speed = 8;                                     // how briskly it eases (per second)
            internal Vector3 wrist, aim; internal Quaternion rot = Quaternion.identity; internal float curl = .1f, index = .1f, thumb = .25f, pinchK; internal bool placed, wasPinch;
            internal GameObject go; internal Renderer r; internal Material mat; internal Vector3 wristLocal; internal Transform root;
            internal readonly Dictionary<XRHandJointID, Transform> joints = new Dictionary<XRHandJointID, Transform>();
            internal readonly Dictionary<Transform, Quaternion> bind = new Dictionary<Transform, Quaternion>();
            public Vector3 Wrist => wrist; public Quaternion Rot => rot;
            public Vector3 At(XRHandJointID id) => joints.TryGetValue(id, out var t) ? t.position : wrist;
            /// <summary>Put the hand there at once, without easing.</summary>
            public void Snap() { wrist = wristTo; rot = rotTo; aim = aimTo; curl = curlTo; index = indexTo; thumb = thumbTo; placed = true; }
        }
        public readonly SimHand[] simHands = { new SimHand(), new SimHand() };   // right, left
        /// <summary>Where the simulated head is and looks (world); it eases there.</summary>
        public Vector3 simHeadTo = new Vector3(0, 1.6f, 0); public float simYawTo, simPitchTo, simHeadSpeed = 3.2f; float simYaw, simPitch;

        void BuildSim()
        {
            cam.fieldOfView = 70; cam.transform.localPosition = simHeadTo;
            var shader = Resources.Load<Shader>("MLShaders/MLHand");
            for (int side = 0; side < 2; side++)
            {
                var h = simHands[side]; var prefab = Resources.Load<GameObject>("MLHands/" + (side == 0 ? "RightHand" : "LeftHand"));
                h.go = Instantiate(prefab, space); h.go.name = side == 0 ? "Right hand (sim)" : "Left hand (sim)"; h.root = h.go.transform;
                string pre = side == 0 ? "R_" : "L_";
                foreach (var t in h.go.GetComponentsInChildren<Transform>(true))
                {
                    h.bind[t] = t.localRotation;
                    if (t.name.StartsWith(pre) && System.Enum.TryParse(t.name.Substring(2), out XRHandJointID id)) h.joints[id] = t;
                }
                h.wristLocal = h.joints[XRHandJointID.Wrist].localPosition;
                var skin = h.go.GetComponentInChildren<SkinnedMeshRenderer>(true); h.mat = new Material(shader);
                var mats = new Material[skin.sharedMaterials.Length]; for (int i = 0; i < mats.Length; i++) mats[i] = h.mat;
                skin.sharedMaterials = mats; skin.updateWhenOffscreen = true; skin.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; skin.receiveShadows = false; skin.sortingOrder = OverPanels;
                h.r = skin; h.r.enabled = false;
            }
        }

        static void Bend(SimHand h, XRHandJointID id, float deg) { if (h.joints.TryGetValue(id, out var t)) t.localRotation = h.bind[t] * Quaternion.Euler(deg, 0, 0); }
        static void Finger(SimHand h, XRHandJointID knuckle, float c)
        {   // the three joints of a finger close together; the ids of a finger follow one another
            Bend(h, knuckle, c * 68); Bend(h, knuckle + 1, c * 92); Bend(h, knuckle + 2, c * 55);
        }

        void ReadSim()
        {
            float dt = Time.deltaTime;
            // the head
            float kh = 1 - Mathf.Exp(-simHeadSpeed * dt);
            simYaw = Mathf.LerpAngle(simYaw, simYawTo, kh); simPitch = Mathf.Lerp(simPitch, simPitchTo, kh);
            float sway = Time.time * .6f;   // nobody holds their head quite still
            cam.transform.position = Vector3.Lerp(cam.transform.position, simHeadTo, kh) + new Vector3(Mathf.PerlinNoise(sway, 0) - .5f, Mathf.PerlinNoise(0, sway) - .5f, 0) * .0012f;
            cam.transform.rotation = Quaternion.Euler(-simPitch + (Mathf.PerlinNoise(sway, 7) - .5f) * .5f, simYaw + (Mathf.PerlinNoise(9, sway) - .5f) * .6f, 0);

            for (int side = 0; side < 2; side++)
            {
                var h = simHands[side]; var p = side == 0 ? right : left; bool was = p.held; float sgn = side == 1 ? 1 : -1;
                h.r.enabled = h.on;
                p.active = h.on; p.controller = false;
                if (!h.on) { p.held = p.down = p.grabDown = false; p.fingerOnly = false; h.placed = false; continue; }
                if (!h.placed) h.Snap();
                float k = 1 - Mathf.Exp(-h.speed * dt), t = Time.time * .9f + side * 31;
                h.wrist = Vector3.Lerp(h.wrist, h.wristTo + new Vector3(Mathf.PerlinNoise(t, 1) - .5f, Mathf.PerlinNoise(2, t) - .5f, Mathf.PerlinNoise(t, 3) - .5f) * .006f, k);
                h.rot = Quaternion.Slerp(h.rot, h.rotTo, k); h.aim = Vector3.Lerp(h.aim, h.aimTo, k);
                float kf = 1 - Mathf.Exp(-16 * dt);
                h.pinchK = Mathf.Lerp(h.pinchK, h.pinch ? 1 : 0, 1 - Mathf.Exp(-28 * dt));
                h.curl = Mathf.Lerp(h.curl, h.point ? .95f : h.curlTo, kf); h.index = Mathf.Lerp(h.index, h.point ? -.3f : h.indexTo, kf); h.thumb = Mathf.Lerp(h.thumb, h.point ? 0 : h.thumbTo, kf);
                // pose the model
                h.root.SetPositionAndRotation(h.wrist - h.rot * h.wristLocal, h.rot);
                float idx = Mathf.Lerp(h.index, .5f, h.pinchK);
                Finger(h, XRHandJointID.IndexProximal, idx); Finger(h, XRHandJointID.MiddleProximal, h.curl); Finger(h, XRHandJointID.RingProximal, h.curl * 1.05f); Finger(h, XRHandJointID.LittleProximal, h.curl * 1.1f);
                if (h.joints.TryGetValue(XRHandJointID.ThumbMetacarpal, out var tm))
                {   // the thumb swings out in the plane of the palm as it opens, and comes across to meet the forefinger in a pinch
                    float open = Mathf.Lerp(h.thumb, .12f, h.pinchK);
                    tm.localRotation = Quaternion.AngleAxis(Mathf.Lerp(-14, 52, open) * sgn, Vector3.up) * Quaternion.AngleAxis(h.pinchK * -16, Vector3.right) * h.bind[tm];
                    Bend(h, XRHandJointID.ThumbProximal, Mathf.Lerp(8, -18, open) + h.pinchK * 14); Bend(h, XRHandJointID.ThumbDistal, Mathf.Lerp(6, -8, open) + h.pinchK * 22);
                }
                h.mat.SetColor("_Color", h.pinchK > .5f ? new Color(.37f, .83f, 1f, 1) : new Color(.88f, .94f, 1f, 1));
                h.mat.SetFloat("_Body", .2f + h.pinchK * .12f); h.mat.SetFloat("_Rim", .8f);
                // what the app reads from it
                p.origin = (h.At(XRHandJointID.IndexProximal) + h.At(XRHandJointID.ThumbProximal)) / 2;
                var dir = h.aim - p.origin; p.rotation = dir.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(dir) : h.rot;
                p.grip = h.rot; p.gripAt = h.wrist; p.gripAxis = h.rot * Vector3.forward; p.fingerOnly = h.point;
                p.held = h.pinchK > .6f; p.down = p.grabDown = p.held && !was; if (p.down) p.heldSince = Time.time;
            }
            gaze.active = false; if (right.down || left.down) everPressed = true;
        }
        bool SimJoint(int side, XRHandJointID id, out Vector3 at)
        {
            var h = simHands[side]; at = default;
            if (!h.on || !h.joints.TryGetValue(id, out var t)) return false;
            at = t.position; return true;
        }
    }
}
#endif
