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
        // What each colour is for (docs/UI_REDESIGN_PROPOSAL.md). Ink: what is read, figures, lines and marks on the
        // map. Soft: supporting text. Gray: labels, units, notes. Dim: credits, what is switched off. Cyan: what can
        // be chosen and what is chosen, and nothing else. Amber: the word for what is not real (simulated,
        // hypothetical), on a dark surface only; never on the map or the scenery, where warm colours mean slope.
        // Back: a surface's ground, a neutral black (it stays: text has to be readable over a real room).
        public static readonly Color Back = new Color(9 / 255f, 9 / 255f, 10 / 255f, .82f), Line = new Color(1, 1, 1, .24f), LineFaint = new Color(1, 1, 1, .12f);
        /// <summary>Distances, in surface pixels: within a group 8, label to content 16, round a rule 24, between groups 40, a surface's margin 64.</summary>
        public static class Gap { public const float S1 = 8, S2 = 16, S3 = 24, S4 = 40, S5 = 64; }
        /// <summary>Lines are 2 px (1 px shimmers in a headset), 3 px for what is chosen. Corners: 4 px on buttons and cells, none on surfaces.</summary>
        public const float Hair = 2, Strong = 3, Corner = 4;
        /// <summary>
        /// Where a thing on screen comes from, told by a mark's shape as well as by a word and a colour: a filled
        /// square is NASA's (an observation or a record), a hollow square is worked out by us from NASA's data, a
        /// hollow diamond is not real (simulated or hypothetical).
        /// </summary>
        public enum Origin { Nasa, Ours, NotReal }
        public static Origin OriginOf(Color c) => c == Cyan ? Origin.Ours : c == Amber ? Origin.NotReal : Origin.Nasa;
        public static Color ColorOf(Origin k) => k == Origin.Ours ? Soft : k == Origin.NotReal ? Amber : Ink;

        /// <summary>
        /// The type scale: every size on every surface is one of these (pixels on the surface), so that what leads
        /// and what follows can be told apart. Display: one figure or word on a surface. Title: what the surface is
        /// about. Heading: the same on a narrow surface, and figures to be read at a glance. Body: what is read to
        /// decide. Label: small headings, provenance tags, lesser buttons. Note: provenance, units, credits.
        /// English and Japanese use the same size; a text that does not fit is shortened, not shrunk.
        /// </summary>
        public static class Size { public const float Display = 64, Title = 42, Heading = 34, Body = 28, Label = 24, Note = 20; }

        public class Btn { public float x, y, w, h, r = 14; public Action fn; public string key; public bool quiet; }

        public readonly GameObject go;
        public readonly RectTransform root;
        public readonly int w, h;
        public readonly List<Btn> buttons = new List<Btn>();
        readonly bool overlay;
        readonly TextMeshProUGUI measurer;
        readonly CanvasGroup group;
        readonly Canvas canvas;
        readonly Image hoverFill, hoverRing;

        /// <summary>Whether the surface should be showing. It fades toward this in Tick; alpha is where it is now.</summary>
        public bool Show = true;
        public float alpha { get; private set; } = 1;
        public string hoverKey { get; private set; }
        float worldWidth, pressedAt = -10;

        static Sprite sRound, sRing, sEdge;
        static Material sOverlayImage;
        static readonly Dictionary<TMP_FontAsset, Material> sOverlayFonts = new Dictionary<TMP_FontAsset, Material>();

        /// <param name="overlay">Draw over everything (labels that must stay visible in front of terrain).</param>
        public MLPanel(string name, int w, int h, float worldWidth, Transform parent, bool overlay = false)
        {
            this.w = w; this.h = h; this.overlay = overlay;
            go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
            go.transform.SetParent(parent, false);
            canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
            group = go.GetComponent<CanvasGroup>(); group.interactable = false; group.blocksRaycasts = false;
            root = (RectTransform)go.transform;
            root.sizeDelta = new Vector2(w, h);
            SetWorldWidth(worldWidth);
            measurer = New<TextMeshProUGUI>("measure"); measurer.enabled = false; measurer.textWrappingMode = TextWrappingModes.NoWrap; measurer.font = MLText.Font;
            EnsureSprites();
            hoverFill = Sliced(sRound, 0, 0, 10, 10, Corner, Color.clear); hoverRing = Sliced(sEdge, 0, 0, 10, 10, Corner, Color.clear);
            hoverFill.name = hoverRing.name = "hover"; hoverFill.enabled = hoverRing.enabled = false;
        }

        public void SetWorldWidth(float width) { worldWidth = width; float k = width / w * (.94f + .06f * alpha * (2 - alpha)); root.localScale = new Vector3(k, k, k); }
        // Hidden surfaces keep their objects active and only stop drawing: text made on an inactive object would
        // lose the material it was given.
        public bool Visible => canvas.enabled;

        /// <summary>Drawn over panels of a lower order whatever their distance: for what is held in front of the others.</summary>
        public void Order(int order) { canvas.overrideSorting = true; canvas.sortingOrder = order; }

        /// <summary>Hidden (or shown) at once, without the fade.</summary>
        public void Snap(bool show) { Show = show; alpha = show ? 1 : 0; group.alpha = alpha; canvas.enabled = show; SetWorldWidth(worldWidth); }

        /// <summary>Fades toward Show, with a slight growth as it appears, and keeps the hover highlight alive.</summary>
        public void Tick(float dt, float perSecond = 5)
        {
            float to = Show ? 1 : 0;
            if (alpha != to)
            {
                alpha = Mathf.MoveTowards(alpha, to, dt * perSecond); group.alpha = alpha * (2 - alpha);
                if (canvas.enabled != alpha > 0) canvas.enabled = alpha > 0;
                SetWorldWidth(worldWidth);
            }
            if (hoverFill.enabled)
            {
                float flash = Mathf.Clamp01(1 - (Time.time - pressedAt) / .22f);
                hoverFill.color = new Color(1, 1, 1, .09f + .3f * flash); hoverRing.color = new Color(Cyan.r, Cyan.g, Cyan.b, .95f);
            }
        }

        public void Clear()
        {
            buttons.Clear();
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                var c = root.GetChild(i);
                if (c != measurer.transform && c != hoverFill.transform && c != hoverRing.transform) UnityEngine.Object.Destroy(c.gameObject);
            }
        }

        /// <summary>Highlights the button a pointer is on (null: none). Returns true when the pointer has just arrived on it.</summary>
        public bool Hover(Btn b)
        {
            string key = b?.key; bool arrived = key != null && key != hoverKey; hoverKey = key;
            hoverFill.enabled = hoverRing.enabled = b != null && !b.quiet;
            if (b == null) return false;
            foreach (var im in new[] { hoverFill, hoverRing })
            {
                Place(im.rectTransform, b.x - 3, b.y - 3, b.w + 6, b.h + 6, new Vector2(0, 1));
                im.pixelsPerUnitMultiplier = BaseRadius(im.sprite) / (Corner + 2); im.transform.SetAsLastSibling();
            }
            return arrived;
        }
        public void Pressed() => pressedAt = Time.time;

        // ---------- drawing ----------
        T New<T>(string name) where T : Component
        {
            var g = new GameObject(name, typeof(RectTransform)); g.transform.SetParent(root, false);
            var rt = (RectTransform)g.transform; rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            return g.AddComponent<T>();
        }
        static void Place(RectTransform rt, float x, float y, float w, float h, Vector2 pivot) { rt.pivot = pivot; rt.sizeDelta = new Vector2(w, h); rt.anchoredPosition = new Vector2(x, -y); }

        // "bold" now means "leads": the Medium face, never a synthesised bold. A leading text at label size or below
        // that is written in capitals is a label, and is set as one (narrower face, space between the letters).
        const float LabelTracking = 8;   // hundredths of an em
        static bool IsLabel(string s, float px, bool lead)
        {
            if (!lead || px > Size.Label || s == null) return false;
            int caps = 0; foreach (char c in s) { if (c >= 'a' && c <= 'z' || c >= 0x2E80) return false; if (c >= 'A' && c <= 'Z') caps++; }
            return caps >= 3;
        }
        static MLText.Face FaceFor(string s, float px, bool lead) => IsLabel(s, px, lead) ? MLText.Face.Label : lead ? MLText.Face.Medium : MLText.Face.Regular;

        /// <summary>One line of text with its baseline at y. align: -1 left, 0 centre, 1 right of x. bold: it leads (Medium).</summary>
        public TextMeshProUGUI Text(string s, float x, float y, float px, Color col, bool bold = false, int align = -1) => TextIn(FaceFor(s, px, bold), s, x, y, px, col, align);

        /// <summary>One line in a given face.</summary>
        public TextMeshProUGUI TextIn(MLText.Face face, string s, float x, float y, float px, Color col, int align = -1)
        {
            var t = New<TextMeshProUGUI>("text");
            Place(t.rectTransform, x, y, 0, 0, new Vector2(align < 0 ? 0 : align == 0 ? .5f : 1, .5f));
            t.font = MLText.Get(face); t.text = s; t.fontSize = px; t.color = col; t.fontStyle = FontStyles.Normal; t.characterSpacing = face == MLText.Face.Label ? LabelTracking : 0;
            t.textWrappingMode = TextWrappingModes.NoWrap; t.overflowMode = TextOverflowModes.Overflow; t.raycastTarget = false;
            t.alignment = align < 0 ? TextAlignmentOptions.BaselineLeft : align == 0 ? TextAlignmentOptions.Baseline : TextAlignmentOptions.BaselineRight;
            if (overlay) t.fontSharedMaterial = OverlayFont(t.font);
            return t;
        }

        public float Measure(string s, float px, bool bold = false) => MeasureIn(FaceFor(s, px, bold), s, px);
        public float MeasureIn(MLText.Face face, string s, float px)
        {
            measurer.font = MLText.Get(face); measurer.fontSize = px; measurer.fontStyle = FontStyles.Normal; measurer.characterSpacing = face == MLText.Face.Label ? LabelTracking : 0;
            return measurer.GetPreferredValues(s).x;
        }

        // ---------- the parts every surface is drawn from ----------
        /// <summary>A large figure or word: Display size, Light.</summary>
        public TextMeshProUGUI Display(string s, float x, float y, Color? col = null, int align = -1) => TextIn(MLText.Face.Light, s, x, y, Size.Display, col ?? Ink, align);
        /// <summary>What a surface is about: Title size, Regular.</summary>
        public TextMeshProUGUI Title(string s, float x, float y, Color? col = null, int align = -1) => TextIn(MLText.Face.Regular, s, x, y, Size.Title, col ?? Ink, align);
        /// <summary>A name or a short lead: Heading size, Medium.</summary>
        public TextMeshProUGUI Heading(string s, float x, float y, Color? col = null, int align = -1) => TextIn(MLText.Face.Medium, s, x, y, Size.Heading, col ?? Ink, align);
        /// <summary>What is read: Body size, Regular.</summary>
        public TextMeshProUGUI Body(string s, float x, float y, Color? col = null, int align = -1) => TextIn(MLText.Face.Regular, s, x, y, Size.Body, col ?? Ink, align);
        /// <summary>A small label: capitals with space between the letters (Japanese: Medium), gray unless told otherwise.</summary>
        public TextMeshProUGUI Label(string s, float x, float y, Color? col = null, int align = -1, float px = Size.Label)
        {
            bool cjk = MLText.HasCjk(s);
            return TextIn(cjk ? MLText.Face.Medium : MLText.Face.Label, cjk ? s : s.ToUpperInvariant(), x, y, px, col ?? Gray, align);
        }
        /// <summary>A note: Note size, Regular, gray.</summary>
        public TextMeshProUGUI Note(string s, float x, float y, Color? col = null, int align = -1) => TextIn(MLText.Face.Regular, s, x, y, Size.Note, col ?? Gray, align);
        /// <summary>A rule across: the 2 px line that separates groups (faint: within a group).</summary>
        public void Rule(float x, float y, float width, bool faint = false) => Fill(x, y - Hair / 2, width, Hair, faint ? LineFaint : Line);
        /// <summary>
        /// A figure with its unit and what it is: the figure large and light, the unit small beside it, the caption
        /// small beneath. Returns the width taken, so that figures can be set in a row.
        /// </summary>
        public float Number(string value, string unit, string caption, float x, float y, float px = Size.Display)
        {
            var face = px >= Size.Display ? MLText.Face.Light : MLText.Face.Regular;
            TextIn(face, value, x, y, px, Ink); float vw = MeasureIn(face, value, px), uw = 0;
            if (!string.IsNullOrEmpty(unit)) { Label(unit, x + vw + 6, y, Gray); uw = 6 + MeasureIn(MLText.HasCjk(unit) ? MLText.Face.Medium : MLText.Face.Label, MLText.HasCjk(unit) ? unit : unit.ToUpperInvariant(), Size.Label); }
            float cw = 0; if (!string.IsNullOrEmpty(caption)) { Note(caption, x, y + Size.Note + Gap.S1); cw = MeasureIn(MLText.Face.Regular, caption, Size.Note); }
            return Mathf.Max(vw + uw, cw);
        }
        /// <summary>The mark that says where a thing comes from, on the baseline y. Returns how far the text after it begins.</summary>
        public float SourceMark(Origin k, float x, float y, float px = Size.Note, Color? col = null)
        {
            float a = Mathf.Round(px * .6f), top = y - a - 1; var c = col ?? ColorOf(k);
            if (k == Origin.Nasa) Fill(x, top, a, a, c);
            else if (k == Origin.Ours) Outline(x, top, a, a, Hair, c);
            else { float cx = x + a / 2, cy = top + a / 2, d = a * .62f; Stroke(cx, cy - d, cx + d, cy, Hair, c); Stroke(cx + d, cy, cx, cy + d, Hair, c); Stroke(cx, cy + d, cx - d, cy, Hair, c); Stroke(cx - d, cy, cx, cy - d, Hair, c); }
            return a + Mathf.Round(px * .45f);
        }
        /// <summary>The mark and the word for a source ("NASA OBSERVATION"). Returns the x after it.</summary>
        public float SourceLabel(Origin k, string word, float x, float y, float px = Size.Note, Color? col = null)
        {
            float adv = SourceMark(k, x, y, px, col); Text(word, x + adv, y, px, col ?? ColorOf(k), true);
            return x + adv + Measure(word, px, true);
        }

        /// <summary>Word-wrapped text; returns the baseline for whatever comes next. indent: how far in the first line begins.</summary>
        public float Wrap(string s, float x, float y, float width, float lineHeight, float px, Color col, bool bold = false, int align = -1, float indent = 0)
        {
            if (MLText.HasCjk(s)) return WrapCjk(s, x, y, width, lineHeight, px, col, bold, align, indent);
            var line = "";
            foreach (var word in s.Split(' '))
            {
                if (line.Length > 0 && Measure(line + word, px, bold) > width - indent) { Text(line.TrimEnd(), x + indent, y, px, col, bold, align); y += lineHeight; line = ""; indent = 0; }
                line += word + " ";
            }
            Text(line.TrimEnd(), x + indent, y, px, col, bold, align);
            return y + lineHeight;
        }

        /// <summary>
        /// A note that begins with where it comes from (the mark and the word: see Origin) and runs on from it.
        /// The colour passed says which origin it is, as the callers have always said it: ink NASA's, cyan ours,
        /// amber not real. Returns the baseline for whatever comes next.
        /// </summary>
        public float Sourced(string source, Color col, string s, float x, float y, float width)
        {
            float end = SourceLabel(OriginOf(col), source, x, y);
            return Wrap(s, x, y, width, 27, Size.Note, Gray, indent: end - x + 12);
        }

        // Japanese has no spaces to break at: a line may break after any Japanese character, runs of Latin letters and
        // digits stay whole, and a line does not begin with closing punctuation. Japanese glyphs are one em wide,
        // so only the Latin runs need measuring.
        float WrapCjk(string s, float x, float y, float width, float lineHeight, float px, Color col, bool bold, int align, float indent)
        {
            var line = new System.Text.StringBuilder(); float lw = 0; int i = 0;
            while (i < s.Length)
            {
                int j = i; string tok;
                if (s[i] >= 0x2E80 || s[i] == ' ') { tok = s[i].ToString(); j = i + 1; }
                else { while (j < s.Length && s[j] < 0x2E80 && s[j] != ' ') j++; tok = s.Substring(i, j - i); }
                float tw = tok == " " ? px * .28f : tok[0] >= 0x2E80 ? px * (bold ? 1.03f : 1f) : Measure(tok, px, bold);
                bool hang = tok.Length == 1 && MLText.NoLineStart(tok[0]);
                if (lw + tw > width - indent && line.Length > 0 && !hang)
                {
                    Text(line.ToString().TrimEnd(), x + indent, y, px, col, bold, align); y += lineHeight; line.Clear(); lw = 0; indent = 0;
                    if (tok == " ") { i = j; continue; }
                }
                line.Append(tok); lw += tw; i = j;
            }
            Text(line.ToString().TrimEnd(), x + indent, y, px, col, bold, align);
            return y + lineHeight;
        }

        /// <summary>
        /// A box, filled and / or outlined. Whatever radius is asked for, a box has the one small corner (Corner);
        /// only a square as round as it is wide (a dot, a ring) stays round. Outlines are 2 px.
        /// </summary>
        public void Box(float x, float y, float w, float h, float r, Color fill, Color? stroke = null)
        {
            EnsureSprites();
            bool round = Mathf.Abs(w - h) < 1 && r >= Mathf.Min(w, h) / 2 - .5f;
            float rr = round ? r : Mathf.Min(r, Corner);
            if (fill.a > 0) Sliced(sRound, x, y, w, h, rr, fill);
            if (stroke.HasValue) Sliced(round ? sRing : sEdge, x, y, w, h, rr, stroke.Value);
        }
        static float BaseRadius(Sprite sp) => sp == sEdge ? 8 : 24;
        Image Sliced(Sprite sp, float x, float y, float w, float h, float r, Color c)
        {
            var im = New<Image>("box"); Place(im.rectTransform, x, y, w, h, new Vector2(0, 1));
            im.sprite = sp; im.type = Image.Type.Sliced; im.pixelsPerUnitMultiplier = BaseRadius(sp) / Mathf.Max(1f, Mathf.Min(r, Mathf.Min(w, h) / 2)); im.color = c; im.raycastTarget = false;
            if (overlay) im.material = OverlayImage();
            return im;
        }
        /// <summary>A plain filled rectangle (rules, grounds, the square marks).</summary>
        public Image Fill(float x, float y, float w, float h, Color c)
        {
            var im = New<Image>("fill"); Place(im.rectTransform, x, y, w, h, new Vector2(0, 1)); im.color = c; im.raycastTarget = false;
            if (overlay) im.material = OverlayImage();
            return im;
        }
        /// <summary>A square-cornered outline of a given thickness.</summary>
        public void Outline(float x, float y, float w, float h, float t, Color c)
        {
            Fill(x, y, w, t, c); Fill(x, y + h - t, w, t, c); Fill(x, y + t, t, h - 2 * t, c); Fill(x + w - t, y + t, t, h - 2 * t, c);
        }

        /// <summary>A straight stroke from one point to another.</summary>
        public Image Stroke(float x0, float y0, float x1, float y1, float thickness, Color c)
        {
            EnsureSprites();
            var im = New<Image>("line"); float len = Vector2.Distance(new Vector2(x0, y0), new Vector2(x1, y1));
            Place(im.rectTransform, x0, y0, len + thickness, thickness, new Vector2(thickness / 2 / (len + thickness), .5f));
            im.rectTransform.localRotation = Quaternion.Euler(0, 0, -Mathf.Atan2(y1 - y0, x1 - x0) * Mathf.Rad2Deg);
            im.sprite = sRound; im.type = Image.Type.Sliced; im.pixelsPerUnitMultiplier = 24f / Mathf.Max(1f, thickness / 2); im.color = c; im.raycastTarget = false;
            if (overlay) im.material = OverlayImage();
            return im;
        }

        /// <summary>
        /// What every surface shares: a neutral dark ground with square corners and one rule along its top. No
        /// outline and no corner brackets: what is on a surface is separated by rules and by space. (The accent
        /// colour is no longer drawn; cyan belongs to what can be chosen.)
        /// </summary>
        public void Frame(Color ground, Color accent, float x = 0, float y = 0, float fw = -1, float fh = -1)
        {
            if (fw < 0) fw = w; if (fh < 0) fh = h;
            Fill(x, y, fw, fh, ground); Fill(x, y, fw, Hair, new Color(1, 1, 1, .5f));
        }

        /// <summary>A thin circle. (A box's outline is a small sprite stretched: at a large radius it would come out thick and soft.)</summary>
        public void Circle(float cx, float cy, float radius, float thickness, Color c, int segments = 96)
        {
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2 / segments, b = (i + 1) * Mathf.PI * 2 / segments;
                Stroke(cx + Mathf.Sin(a) * radius, cy - Mathf.Cos(a) * radius, cx + Mathf.Sin(b) * radius, cy - Mathf.Cos(b) * radius, thickness, c);
            }
        }

        /// <summary>A tick mark inside the box (x, y, size).</summary>
        public void Check(float x, float y, float size, Color c)
        {
            float t = size * .13f; Stroke(x + size * .22f, y + size * .52f, x + size * .43f, y + size * .72f, t, c); Stroke(x + size * .43f, y + size * .72f, x + size * .8f, y + size * .3f, t, c);
        }

        public RawImage Picture(Texture tex, float x, float y, float w, float h, Rect uv)
        {
            var im = New<RawImage>("picture"); Place(im.rectTransform, x, y, w, h, new Vector2(0, 1));
            im.texture = tex; im.uvRect = uv; im.raycastTarget = false;
            if (overlay) im.material = OverlayImage();   // on a surface that is drawn over everything, a picture has to be too, or the surface's own ground covers it
            return im;
        }

        public void Button(string label, float x, float y, float w, float h, Action fn, bool on = false, bool dim = false, bool primary = false, float px = Size.Label, string key = null)
        {
            // The one main action on a surface is a cyan plate. A choice that is on carries a cyan line under it.
            // Every other button is a thin outline; one that cannot be pressed has none.
            if (dim) Box(x, y, w, h, Corner, new Color(1, 1, 1, .04f));
            else if (primary) Box(x, y, w, h, Corner, Cyan);
            else if (on) { Box(x, y, w, h, Corner, new Color(1, 1, 1, .08f)); Fill(x, y + h - Strong, w, Strong, Cyan); }
            else Box(x, y, w, h, Corner, new Color(0, 0, 0, .25f), Line);
            Text(label, x + w / 2, y + h / 2 + px * .36f, px, dim ? Dim : primary ? OnCyan : Ink, true, 0);
            if (!dim) Hit(key ?? label, x, y, w, h, fn);
        }
        /// <summary>An area that can be pointed at and pressed. quiet: no highlight (large areas such as a whole strip).</summary>
        public void Hit(string key, float x, float y, float w, float h, Action fn, float r = 14, bool quiet = false) => buttons.Add(new Btn { x = x, y = y, w = w, h = h, r = r, fn = fn, key = key, quiet = quiet });

        /// <summary>
        /// A provenance word on a dark strip (for use over a picture), e.g. "NASA OBSERVATION", with the mark of
        /// its origin before it. The colour passed says which origin, as for Sourced.
        /// </summary>
        public void Tag(string label, float x, float y, Color col, bool mark = true)
        {
            if (!mark) { Fill(x, y, Measure(label, Size.Note, true) + 24, 32, new Color(0, 0, 0, .66f)); Text(label, x + 12, y + 23, Size.Note, col, true); return; }   // a state or a name: no origin to mark
            var k = OriginOf(col); float adv = Mathf.Round(Size.Note * .6f) + Mathf.Round(Size.Note * .45f);
            Fill(x, y, Measure(label, Size.Note, true) + 24 + adv, 32, new Color(0, 0, 0, .66f));
            SourceLabel(k, label, x + 12, y + 23);
        }

        // ---------- pointing ----------
        /// <summary>Where a ray meets this surface, in surface pixels. False when it misses or the surface is hidden.</summary>
        public bool Raycast(Ray ray, out float distance, out Vector2 px, float margin = 0)
        {
            distance = 0; px = default;
            if (!canvas.enabled || alpha < .6f) return false;
            var plane = new Plane(root.forward, root.position);
            if (!plane.Raycast(ray, out distance) || distance <= 0) return false;
            var l = root.InverseTransformPoint(ray.GetPoint(distance));
            px = new Vector2(l.x + w / 2f, h / 2f - l.y);
            return px.x >= -margin && px.x <= w + margin && px.y >= -margin && px.y <= h + margin;
        }
        /// <summary>
        /// The button under a point; failing that, the nearest one within `reach` pixels, so a hand that wavers
        /// just off a small button still has it.
        /// </summary>
        public Btn ButtonAt(Vector2 px, float reach = 0)
        {
            for (int i = buttons.Count - 1; i >= 0; i--) { var b = buttons[i]; if (px.x >= b.x && px.x <= b.x + b.w && px.y >= b.y && px.y <= b.y + b.h) return b; }
            Btn best = null; float bd = reach;
            foreach (var b in buttons)
            {
                if (b.quiet) continue;
                float dx = Mathf.Max(0, Mathf.Max(b.x - px.x, px.x - b.x - b.w)), dy = Mathf.Max(0, Mathf.Max(b.y - px.y, px.y - b.y - b.h)), d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d < bd) { bd = d; best = b; }
            }
            return best;
        }
        public Btn Find(string key) => buttons.Find(b => b.key == key) ?? buttons.Find(b => b.key != null && b.key.StartsWith(key));
        public Vector3 WorldPoint(Btn b) => root.TransformPoint(new Vector3(b.x + b.w / 2 - w / 2f, h / 2f - (b.y + b.h / 2), 0));

        // ---------- shared resources ----------
        public static Color Hex(string hex) { ColorUtility.TryParseHtmlString("#" + hex, out var c); return c; }

        static void EnsureSprites()
        {
            if (sRound != null) return;
            sRound = MakeSprite(false, 64, 24, 3); sRing = MakeSprite(true, 64, 24, 3); sEdge = MakeSprite(true, 32, 8, 4);
        }
        // A rounded square, filled or as an outline, sliced so any size keeps its corners. Round things use the
        // 64 px one (radius 24, 3 px line). Boxes use the 32 px outline (radius 8, 4 px line): drawn at the 4 px
        // corner it is scaled by a half, which leaves a 2 px line.
        static Sprite MakeSprite(bool ring, int N, float R, float T)
        {
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
        // Each face has its own atlas, so each needs its own copy of the material that draws over everything. The
        // shader comes from the material asset made by MLBuild (which is what makes the shader ship in builds).
        static Material OverlayFont(TMP_FontAsset font)
        {
            if (sOverlayFonts.TryGetValue(font, out var m) && m != null) return m;
            var asset = Resources.Load<Material>("MLMat/FontOverlay");
            m = asset != null ? new Material(font.material) { shader = asset.shader, name = font.name + " overlay", hideFlags = HideFlags.HideAndDontSave } : font.material;
            return sOverlayFonts[font] = m;
        }
    }
}
