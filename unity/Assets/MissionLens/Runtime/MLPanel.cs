using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MissionLens
{
    /// <summary>
    /// A flat surface in the space that is drawn with absolute pixel coordinates (x right, y down, text placed
    /// by its baseline), like a 2D canvas. UGUI is used only to draw; pointing is resolved here, by intersecting
    /// a ray with the surface and looking up the button under it. The same path serves mouse, controller and hand.
    /// </summary>
    public class MLPanel
    {
        public static readonly Color Ink = Hex("e9e6df"), Gray = Hex("9aa0a8"), Dim = Hex("6b7178"), Cyan = Hex("5fd4ff"), Amber = Hex("ffb347"), Soft = Hex("b8bec6"), OnCyan = Hex("04121a");
        public static readonly Color Back = new Color(12 / 255f, 15 / 255f, 20 / 255f, .92f), Line = new Color(1, 1, 1, .2f);

        public class Btn { public float x, y, w, h; public Action fn; public string key; }

        public readonly GameObject go;
        public readonly RectTransform root;
        public readonly int w, h;
        public readonly List<Btn> buttons = new List<Btn>();
        readonly bool overlay;
        readonly TextMeshProUGUI measurer;

        static Sprite sRound, sRing;
        static Material sOverlayImage, sOverlayFont;

        /// <param name="overlay">Draw over everything (labels that must stay visible in front of terrain).</param>
        public MLPanel(string name, int w, int h, float worldWidth, Transform parent, bool overlay = false)
        {
            this.w = w; this.h = h; this.overlay = overlay;
            go = new GameObject(name, typeof(RectTransform), typeof(Canvas));
            go.transform.SetParent(parent, false);
            go.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            root = (RectTransform)go.transform;
            root.sizeDelta = new Vector2(w, h);
            SetWorldWidth(worldWidth);
            measurer = New<TextMeshProUGUI>("measure"); measurer.enabled = false; measurer.textWrappingMode = TextWrappingModes.NoWrap;
        }

        public void SetWorldWidth(float worldWidth) { float k = worldWidth / w; root.localScale = new Vector3(k, k, k); }
        public bool Visible { get => go.activeSelf; set { if (go.activeSelf != value) go.SetActive(value); } }

        public void Clear()
        {
            buttons.Clear();
            for (int i = root.childCount - 1; i >= 0; i--) { var c = root.GetChild(i); if (c != measurer.transform) UnityEngine.Object.Destroy(c.gameObject); }
        }

        // ---------- drawing ----------
        T New<T>(string name) where T : Component
        {
            var g = new GameObject(name, typeof(RectTransform)); g.transform.SetParent(root, false);
            var rt = (RectTransform)g.transform; rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            return g.AddComponent<T>();
        }
        static void Place(RectTransform rt, float x, float y, float w, float h, Vector2 pivot) { rt.pivot = pivot; rt.sizeDelta = new Vector2(w, h); rt.anchoredPosition = new Vector2(x, -y); }

        /// <summary>One line of text with its baseline at y. align: -1 left, 0 centre, 1 right of x.</summary>
        public TextMeshProUGUI Text(string s, float x, float y, float px, Color col, bool bold = false, int align = -1)
        {
            var t = New<TextMeshProUGUI>("text");
            Place(t.rectTransform, x, y, 0, 0, new Vector2(align < 0 ? 0 : align == 0 ? .5f : 1, .5f));
            t.text = s; t.fontSize = px; t.color = col; t.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            t.textWrappingMode = TextWrappingModes.NoWrap; t.overflowMode = TextOverflowModes.Overflow; t.raycastTarget = false;
            t.alignment = align < 0 ? TextAlignmentOptions.BaselineLeft : align == 0 ? TextAlignmentOptions.Baseline : TextAlignmentOptions.BaselineRight;
            if (overlay) t.fontSharedMaterial = OverlayFont(t.font);
            return t;
        }

        public float Measure(string s, float px, bool bold = false)
        {
            measurer.fontSize = px; measurer.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            return measurer.GetPreferredValues(s).x;
        }

        /// <summary>Word-wrapped text; returns the baseline for whatever comes next.</summary>
        public float Wrap(string s, float x, float y, float width, float lineHeight, float px, Color col, bool bold = false)
        {
            var line = "";
            foreach (var word in s.Split(' '))
            {
                if (line.Length > 0 && Measure(line + word, px, bold) > width) { Text(line.TrimEnd(), x, y, px, col, bold); y += lineHeight; line = ""; }
                line += word + " ";
            }
            Text(line.TrimEnd(), x, y, px, col, bold);
            return y + lineHeight;
        }

        public void Box(float x, float y, float w, float h, float r, Color fill, Color? stroke = null)
        {
            EnsureSprites();
            if (fill.a > 0) Sliced(sRound, x, y, w, h, r, fill);
            if (stroke.HasValue) Sliced(sRing, x, y, w, h, r, stroke.Value);
        }
        void Sliced(Sprite sp, float x, float y, float w, float h, float r, Color c)
        {
            var im = New<Image>("box"); Place(im.rectTransform, x, y, w, h, new Vector2(0, 1));
            im.sprite = sp; im.type = Image.Type.Sliced; im.pixelsPerUnitMultiplier = 24f / Mathf.Max(1f, Mathf.Min(r, Mathf.Min(w, h) / 2)); im.color = c; im.raycastTarget = false;
            if (overlay) im.material = OverlayImage();
        }

        public RawImage Picture(Texture tex, float x, float y, float w, float h, Rect uv)
        {
            var im = New<RawImage>("picture"); Place(im.rectTransform, x, y, w, h, new Vector2(0, 1));
            im.texture = tex; im.uvRect = uv; im.raycastTarget = false; return im;
        }

        public void Button(string label, float x, float y, float w, float h, Action fn, bool on = false, bool dim = false, bool primary = false, float px = 25, string key = null)
        {
            Box(x, y, w, h, 14, on ? Cyan : primary ? new Color(95 / 255f, 212 / 255f, 1, .2f) : new Color(1, 1, 1, dim ? .05f : .11f), primary && !on ? Cyan : (Color?)null);
            Text(label, x + w / 2, y + h / 2 + px * .36f, px, on ? OnCyan : dim ? Dim : Ink, true, 0);
            if (!dim) Hit(key ?? label, x, y, w, h, fn);
        }
        public void Hit(string key, float x, float y, float w, float h, Action fn) => buttons.Add(new Btn { x = x, y = y, w = w, h = h, fn = fn, key = key });

        /// <summary>Small dark pill with a provenance word, e.g. "NASA OBSERVATION".</summary>
        public void Tag(string label, float x, float y, Color col)
        {
            Box(x, y, Measure(label, 18, true) + 24, 30, 15, new Color(8 / 255f, 10 / 255f, 14 / 255f, .78f));
            Text(label, x + 12, y + 21, 18, col, true);
        }

        // ---------- pointing ----------
        /// <summary>Where a ray meets this surface, in surface pixels. False when it misses or the surface is hidden.</summary>
        public bool Raycast(Ray ray, out float distance, out Vector2 px)
        {
            distance = 0; px = default;
            if (!go.activeInHierarchy) return false;
            var plane = new Plane(root.forward, root.position);
            if (!plane.Raycast(ray, out distance) || distance <= 0) return false;
            var l = root.InverseTransformPoint(ray.GetPoint(distance));
            px = new Vector2(l.x + w / 2f, h / 2f - l.y);
            return px.x >= 0 && px.x <= w && px.y >= 0 && px.y <= h;
        }
        public Btn ButtonAt(Vector2 px)
        {
            for (int i = buttons.Count - 1; i >= 0; i--) { var b = buttons[i]; if (px.x >= b.x && px.x <= b.x + b.w && px.y >= b.y && px.y <= b.y + b.h) return b; }
            return null;
        }
        public Btn Find(string key) => buttons.Find(b => b.key == key) ?? buttons.Find(b => b.key != null && b.key.StartsWith(key));
        public Vector3 WorldPoint(Btn b) => root.TransformPoint(new Vector3(b.x + b.w / 2 - w / 2f, h / 2f - (b.y + b.h / 2), 0));

        // ---------- shared resources ----------
        public static Color Hex(string hex) { ColorUtility.TryParseHtmlString("#" + hex, out var c); return c; }

        static void EnsureSprites()
        {
            if (sRound != null) return;
            sRound = MakeSprite(false); sRing = MakeSprite(true);
        }
        // A 64 px rounded square (radius 24), filled or as a 3 px outline, sliced so any size keeps round corners.
        static Sprite MakeSprite(bool ring)
        {
            const int N = 64; const float R = 24, T = 3;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            var px = new Color32[N * N];
            for (int y = 0; y < N; y++) for (int x = 0; x < N; x++)
            {
                float dx = Mathf.Max(0, Mathf.Abs(x + .5f - N / 2f) - (N / 2f - R)), dy = Mathf.Max(0, Mathf.Abs(y + .5f - N / 2f) - (N / 2f - R));
                float d = Mathf.Sqrt(dx * dx + dy * dy) - R;   // signed distance to the rounded edge, negative inside
                float a = Mathf.Clamp01(.5f - d);
                if (ring) a *= Mathf.Clamp01(.5f + d + T);
                px[y * N + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            tex.SetPixels32(px); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect, new Vector4(R, R, R, R));
        }
        static Material OverlayImage()
        {
            if (sOverlayImage == null) sOverlayImage = new Material(Resources.Load<Shader>("MLShaders/MLUIOverlay")) { hideFlags = HideFlags.HideAndDontSave };
            return sOverlayImage;
        }
        static Material OverlayFont(TMP_FontAsset font)
        {
            if (sOverlayFont == null)
            {
                var asset = Resources.Load<Material>("MLMat/FontOverlay");   // made by MLBuild so the overlay shader ships in builds
                sOverlayFont = asset != null ? asset : font.material;
            }
            return sOverlayFont;
        }
    }
}
