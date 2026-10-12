using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ManagerKit.UI
{
    /// <summary>Code-first UGUI builder. Everything in the game UI is made with these helpers (no prefabs/scenes to maintain).</summary>
    public static class U
    {
        // ---------------- rects ----------------
        public static RectTransform Rt(Transform parent, string name = "rt")
        {
            var go = new GameObject(name, typeof(RectTransform)); go.layer = 5;
            var r = go.GetComponent<RectTransform>(); r.SetParent(parent, false);
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f); r.pivot = new Vector2(0.5f, 0.5f); r.sizeDelta = Vector2.zero; r.anchoredPosition = Vector2.zero;
            return r;
        }
        /// <summary>Stretch to parent with inward offsets.</summary>
        public static T Fill<T>(this T c, float l = 0, float r = 0, float t = 0, float b = 0) where T : Component
        {
            var rt = (RectTransform)c.transform; rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(l, b); rt.offsetMax = new Vector2(-r, -t); return c;
        }
        /// <summary>Anchor at (ax,ay) in the parent, pivot = anchor, offset (x,y), fixed size.</summary>
        public static T At<T>(this T c, float ax, float ay, float x, float y, float w, float h, float px = -1, float py = -1) where T : Component
        {
            h = ReadingHeight(c, h);
            var rt = (RectTransform)c.transform; rt.anchorMin = rt.anchorMax = new Vector2(ax, ay); rt.pivot = new Vector2(px < 0 ? ax : px, py < 0 ? ay : py);
            rt.anchoredPosition = new Vector2(x, y); rt.sizeDelta = new Vector2(w, h); return c;
        }
        /// <summary>Stretch horizontally at vertical anchor ay with fixed height.</summary>
        public static T StretchX<T>(this T c, float ay, float y, float h, float padL = 0, float padR = 0) where T : Component
        {
            h = ReadingHeight(c, h);
            var rt = (RectTransform)c.transform; rt.anchorMin = new Vector2(0, ay); rt.anchorMax = new Vector2(1, ay); rt.pivot = new Vector2(0.5f, ay);
            rt.offsetMin = new Vector2(padL, y - h * ay); rt.offsetMax = new Vector2(-padR, y + h * (1 - ay)); return c;
        }
        public static T StretchY<T>(this T c, float ax, float x, float w, float padB = 0, float padT = 0) where T : Component
        {
            var rt = (RectTransform)c.transform; rt.anchorMin = new Vector2(ax, 0); rt.anchorMax = new Vector2(ax, 1); rt.pivot = new Vector2(ax, 0.5f);
            rt.offsetMin = new Vector2(x - w * ax, padB); rt.offsetMax = new Vector2(x + w * (1 - ax), -padT); return c;
        }
        public static T Sz<T>(this T c, float w, float h) where T : Component { ((RectTransform)c.transform).sizeDelta = new Vector2(w, h); return c; }
        public static RectTransform R(this Component c) => (RectTransform)c.transform;
        static float ReadingHeight(Component c, float height)
        {
            var text = c.GetComponent<Text>();
            if (text != null) return Mathf.Max(height, text.fontSize * 1.24f);
            if (c.name == "bar" || c.name == "chip" || c.name == "badge")
            {
                text = c.GetComponentInChildren<Text>();
                if (text != null) return Mathf.Max(height, text.fontSize * 1.24f + 8);
            }
            return height;
        }

        // ---------------- images ----------------
        public static Image Img(Transform parent, Sprite s, Color? c = null, string name = "img", bool raycast = false)
        {
            var r = Rt(parent, name); var i = r.gameObject.AddComponent<Image>();
            i.sprite = s; i.color = c ?? Color.white; i.raycastTarget = raycast;
            if (s != null && s.border != Vector4.zero) { i.type = Image.Type.Sliced; i.pixelsPerUnitMultiplier = 1f; i.fillCenter = true; }
            return i;
        }
        public static Image Box(Transform parent, Color fill, Color? border = null, string name = "box", bool raycast = false, float bevel = 0.22f)
            => Img(parent, Boxes.Get(fill, border, bevel), Color.white, name, raycast);
        public static Image Solid(Transform parent, Color c, string name = "solid", bool raycast = false) => Img(parent, Boxes.White, c, name, raycast);
        public static Image Icon(Transform parent, Sprite s, float size, Color? tint = null, string name = "icon")
        { var i = Img(parent, s, tint, name); i.preserveAspect = true; i.Sz(size, size); return i; }

        // ---------------- text ----------------
        public static Text Txt(Transform parent, string s, int size = 36, Color? c = null, TextAnchor a = TextAnchor.MiddleCenter, bool outline = false, bool wrap = false, string name = "txt")
        {
            var r = Rt(parent, name); var t = r.gameObject.AddComponent<Text>();
            t.font = Theme.Font; t.text = s ?? ""; t.fontSize = Theme.ReadingSize(size); t.fontStyle = outline ? FontStyle.Bold : FontStyle.Normal; t.color = c ?? Theme.Text; t.alignment = a; t.raycastTarget = false; t.supportRichText = true;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Truncate;
            t.lineSpacing = 1.04f;
            if (!wrap) t.FitText();
            return t;
        }
        /// <summary>Gentle fitting for short labels in fixed boxes; paragraphs keep their reading size.</summary>
        public static Text FitText(this Text t, int minimum = 0)
        {
            t.resizeTextForBestFit = true;
            t.resizeTextMaxSize = t.fontSize;
            t.resizeTextMinSize = Mathf.Min(t.fontSize, Mathf.Max(36, minimum > 0 ? minimum : Mathf.RoundToInt(t.fontSize * 0.82f)));
            return t;
        }
        /// <summary>Text that stretches to its parent, wraps and clips to the box.</summary>
        public static Text Para(Transform parent, string s, int size = 32, Color? c = null, TextAnchor a = TextAnchor.UpperLeft)
        { var t = Txt(parent, s, size, c ?? Theme.TextDim, a, false, true, "para"); t.Fill(); return t; }

        // ---------------- layout ----------------
        public static VerticalLayoutGroup VBox(RectTransform r, float spacing = 10, RectOffset pad = null, TextAnchor align = TextAnchor.UpperCenter, bool ctrlW = true, bool ctrlH = true, bool expandW = true, bool expandH = false)
        {
            var v = r.gameObject.AddComponent<VerticalLayoutGroup>(); v.spacing = spacing; v.padding = pad ?? new RectOffset(); v.childAlignment = align;
            v.childControlWidth = ctrlW; v.childControlHeight = ctrlH; v.childForceExpandWidth = expandW; v.childForceExpandHeight = expandH;
            if (expandW) v.Le(-1, -1, 0, -1);
            return v;
        }
        public static HorizontalLayoutGroup HBox(RectTransform r, float spacing = 10, RectOffset pad = null, TextAnchor align = TextAnchor.MiddleCenter, bool ctrlW = true, bool ctrlH = true, bool expandW = false, bool expandH = true)
        {
            var h = r.gameObject.AddComponent<HorizontalLayoutGroup>(); h.spacing = spacing; h.padding = pad ?? new RectOffset(); h.childAlignment = align;
            h.childControlWidth = ctrlW; h.childControlHeight = ctrlH; h.childForceExpandWidth = expandW; h.childForceExpandHeight = expandH;
            if (expandH) h.Le(-1, -1, -1, 0);
            return h;
        }
        public static GridLayoutGroup Grid(RectTransform r, Vector2 cell, Vector2 spacing, int cols = 0, RectOffset pad = null, TextAnchor align = TextAnchor.UpperCenter)
        {
            var g = r.gameObject.AddComponent<GridLayoutGroup>(); g.cellSize = cell; g.spacing = spacing; g.padding = pad ?? new RectOffset(); g.childAlignment = align;
            if (cols > 0) { g.constraint = GridLayoutGroup.Constraint.FixedColumnCount; g.constraintCount = cols; var fit = r.gameObject.AddComponent<AdaptiveGrid>(); fit.Init(g, cell, cols); }
            return g;
        }
        public static LayoutElement Le(this Component c, float w = -1, float h = -1, float flexW = -1, float flexH = -1, float minW = -1, float minH = -1)
        {
            if (h > 0) h = ReadingHeight(c, h);
            var l = c.GetComponent<LayoutElement>() ?? c.gameObject.AddComponent<LayoutElement>();
            if ((w > 0 || h > 0) && c.transform is RectTransform rr) rr.sizeDelta = new Vector2(w > 0 ? w : rr.sizeDelta.x, h > 0 ? h : rr.sizeDelta.y);
            if (w >= 0) l.preferredWidth = w; if (h >= 0) l.preferredHeight = h; if (flexW >= 0) l.flexibleWidth = flexW; if (flexH >= 0) l.flexibleHeight = flexH;
            if (minW >= 0) l.minWidth = minW; if (minH >= 0) l.minHeight = minH; return l;
        }
        public static ContentSizeFitter Fit(this RectTransform r, bool vertical = true, bool horizontal = false)
        {
            var f = r.GetComponent<ContentSizeFitter>() ?? r.gameObject.AddComponent<ContentSizeFitter>();
            f.verticalFit = vertical ? ContentSizeFitter.FitMode.PreferredSize : ContentSizeFitter.FitMode.Unconstrained;
            f.horizontalFit = horizontal ? ContentSizeFitter.FitMode.PreferredSize : ContentSizeFitter.FitMode.Unconstrained; return f;
        }
        public static RectTransform Spacer(Transform parent, float h = 10, float w = 0, float flex = 0)
        { var r = Rt(parent, "spacer"); r.Le(w > 0 ? w : -1, h, flex > 0 ? flex : -1, -1, 0, 0); return r; }
        public static void Clear(Transform t) { for (int i = t.childCount - 1; i >= 0; i--) { var c = t.GetChild(i); c.SetParent(null); UnityEngine.Object.Destroy(c.gameObject); } }
        public static void Rebuild(RectTransform r) { LayoutRebuilder.ForceRebuildLayoutImmediate(r); }

        // ---------------- scroll ----------------
        public class Scroll { public ScrollRect sr; public RectTransform view, content; public float Pos { get => content.anchoredPosition.y; set { var p = content.anchoredPosition; p.y = value; content.anchoredPosition = p; } } }

        public static Scroll ScrollV(Transform parent, float spacing = 14, RectOffset pad = null, string name = "scroll")
        {
            var root = Rt(parent, name); var bg = root.gameObject.AddComponent<Image>(); bg.color = new Color(0, 0, 0, 0); bg.raycastTarget = true; root.Fill();
            var sr = root.gameObject.AddComponent<ScrollRect>(); sr.horizontal = false; sr.vertical = true; sr.movementType = ScrollRect.MovementType.Elastic; sr.elasticity = 0.08f; sr.decelerationRate = 0.135f; sr.scrollSensitivity = 45;
            var view = Rt(root, "view"); view.Fill(); view.gameObject.AddComponent<RectMask2D>();
            var content = Rt(view, "content"); content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1); content.pivot = new Vector2(0.5f, 1); content.sizeDelta = Vector2.zero;
            VBox(content, spacing, pad ?? new RectOffset(24, 24, 20, 40)); content.Fit();
            sr.viewport = view; sr.content = content;
            return new Scroll { sr = sr, view = view, content = content };
        }
        public static Scroll ScrollH(Transform parent, float spacing = 12, RectOffset pad = null, string name = "hscroll")
        {
            var root = Rt(parent, name); var bg = root.gameObject.AddComponent<Image>(); bg.color = new Color(0, 0, 0, 0); bg.raycastTarget = true;
            var sr = root.gameObject.AddComponent<ScrollRect>(); sr.horizontal = true; sr.vertical = false; sr.movementType = ScrollRect.MovementType.Elastic; sr.elasticity = 0.08f; sr.scrollSensitivity = 45;
            var view = Rt(root, "view"); view.Fill(); view.gameObject.AddComponent<RectMask2D>();
            var content = Rt(view, "content"); content.anchorMin = new Vector2(0, 0); content.anchorMax = new Vector2(0, 1); content.pivot = new Vector2(0, 0.5f); content.sizeDelta = Vector2.zero;
            HBox(content, spacing, pad ?? new RectOffset(16, 16, 6, 6), TextAnchor.MiddleLeft, true, true, false, true); content.Fit(false, true);
            sr.viewport = view; sr.content = content;
            return new Scroll { sr = sr, view = view, content = content };
        }

        // ---------------- button ----------------
        public static UButton Btn(Transform parent, string text, Color fill, Action click, float w = 300, float h = 100, int fs = 40, Sprite icon = null, Color? textColor = null, string name = "btn", bool ornate = false)
        {
            if (w < 360 && !string.IsNullOrEmpty(text) && !char.IsDigit(text[0])) icon = null;
            ornate = ornate && fill.a > 0.5f;
            var img = ornate ? Img(parent, LuxArt.Plate(fill, w, h), Color.white, name, true) : Box(parent, fill, null, name, true, .06f);
            var b = img.gameObject.AddComponent<UButton>(); b.bg = img; b.soft = !ornate; b.baseColor = fill; b.onClick = click; b.ornate = ornate; b.plateW = w; b.plateH = h; img.Sz(w, h);
            b.Le(w, h);
            if (ornate) LuxArt.Raise(img, 6f, .24f);
            if (ornate) img.gameObject.AddComponent<Gleam>();
            var content = Rt(img.transform, "button_content"); content.Fill(ornate ? 24 : 14, ornate ? 24 : 14, 8, 8);
            HBox(content, 12, null, TextAnchor.MiddleCenter, true, true, false, false);
            if (icon != null)
            {
                float isz = Mathf.Min(h - 30, 64);
                b.icon = Icon(content, icon, isz); b.icon.Le(isz, isz, 0, 0, isz, isz);
            }
            if (text != null)
            {
                b.labelTint = textColor ?? (fill.grayscale > 0.64f ? Theme.TextDark : new Color(1f, 0.98f, 0.9f));
                b.label = Txt(content, text, fs, b.labelTint, TextAnchor.MiddleCenter, true);
                b.label.Le(-1, -1, 0, 1, 0, 0);
            }
            img.gameObject.AddComponent<RectMask2D>();
            return b;
        }
        public static UButton IconBtn(Transform parent, Sprite icon, Color fill, Action click, float size = 90, string name = "iconbtn")
        {
            var img = Box(parent, fill, null, name, true, .06f); var b = img.gameObject.AddComponent<UButton>(); b.bg = img; b.soft = true; b.baseColor = fill; b.onClick = click; img.Sz(size, size); b.Le(size, size);
            b.icon = Icon(img.transform, icon, size - 28); b.icon.At(0.5f, 0.5f, 0, 0, size - 28, size - 28); return b;
        }

        // ---------------- misc widgets ----------------
        public class Bar { public Image back, fill; public Text label; float v; public float Value { get => v; set { v = Mathf.Clamp01(value); var f = (RectTransform)fill.transform; f.anchorMax = new Vector2(v, 1); fill.enabled = v > 0.001f; } } }
        public static Bar ProgressBar(Transform parent, Color fillColor, float w, float h, string label = null, int fs = 26)
        {
            if (label != null) h = Mathf.Max(h, Theme.ReadingSize(fs) * 1.24f + 8);
            var back = Box(parent, Theme.Inset, Theme.Black, "bar", false, 0.1f); back.Sz(w, h); back.Le(w, h);
            float inset = Mathf.Min(5f, h * 0.18f);
            var inner = Rt(back.transform, "inner"); inner.Fill(inset, inset, inset, inset);
            var fill = Box(inner, fillColor, null, "fill", false, 0.35f); var fr = fill.R();
            fr.anchorMin = Vector2.zero; fr.anchorMax = new Vector2(0, 1); fr.pivot = new Vector2(0, 0.5f); fr.offsetMin = fr.offsetMax = Vector2.zero;
            var bar = new Bar { back = back, fill = fill };
            if (label != null) { bar.label = Txt(back.transform, label, fs, Theme.TextDark, TextAnchor.MiddleCenter, false); bar.label.Fill(8, 8, 0, 1); }
            return bar;
        }
        public static Image Dot(Transform parent, Color c, float size) { var i = Img(parent, Boxes.Circle, c, "dot"); i.Sz(size, size); return i; }

        public static Image Badge(Transform parent, string text, float size = 44)
        {
            size = Mathf.Max(size, 64);
            var b = Box(parent, Theme.Red, Theme.Black, "badge"); b.Sz(Mathf.Max(size, 20 + text.Length * 14), size);
            var t = Txt(b.transform, text, 28, Color.white, TextAnchor.MiddleCenter, false); t.Fill(4, 4, 4, 4); return b;
        }
        public static Image Chip(Transform parent, string text, Color fill, int fs = 26, Color? tc = null, float padX = 18, float h = 40)
        {
            h = Mathf.Max(h, Theme.ReadingSize(fs) * 1.24f + 8);
            var c = Box(parent, fill, null, "chip");
            Color ink = tc ?? Color.white;
            if (ink == Color.white && fill.grayscale > 0.56f) ink = Theme.TextDark;
            var t = Txt(c.transform, text, fs, ink, TextAnchor.MiddleCenter, false);
            float w = Mathf.Max(h, t.preferredWidth + padX * 2); c.Sz(w, h); c.Le(w, h); t.Fill(8, 8, 2, 2); return c;
        }
        public static Image Divider(Transform parent, float h = 4, Color? c = null) { var d = Solid(parent, c ?? new Color(1, 1, 1, 0.08f), "div"); d.Le(-1, h); d.Sz(0, h); return d; }

        public class Toggle { public Image track, knob; public bool on; public Action<bool> changed; public void Set(bool v, bool notify = false) { on = v; Apply(); if (notify) changed?.Invoke(on); } public void Apply() { track.color = on ? Theme.Green : Theme.Grey; knob.rectTransform.anchorMin = knob.rectTransform.anchorMax = new Vector2(on ? 1 : 0, 0.5f); knob.rectTransform.pivot = new Vector2(on ? 1 : 0, 0.5f); knob.rectTransform.anchoredPosition = new Vector2(on ? -6 : 6, 0); } }
        public static Toggle Switch(Transform parent, bool on, Action<bool> changed, float w = 150, float h = 70)
        {
            var track = Box(parent, Theme.Grey, Theme.Black, "switch", true); track.Sz(w, h); track.Le(w, h);
            var knob = Box(track.transform, Color.white, Theme.Black, "knob", false); knob.Sz(h - 14, h - 14);
            var t = new Toggle { track = track, knob = knob, on = on, changed = changed }; t.Apply();
            var b = track.gameObject.AddComponent<UButton>(); b.bg = track; b.noTint = true; b.onClick = () => { t.on = !t.on; t.Apply(); changed?.Invoke(t.on); };
            return t;
        }

        public class Sld { public Slider s; public Image track, fill, knob; }
        public static Sld Slide(Transform parent, float value, Action<float> changed, float w = 520, float h = 56)
        {
            var track = Box(parent, Theme.Inset, Theme.Black, "slider", true, 0.1f); track.Sz(w, h); track.Le(w, h);
            var s = track.gameObject.AddComponent<Slider>(); s.minValue = 0; s.maxValue = 1; s.wholeNumbers = false;
            var area = Rt(track.transform, "area"); area.Fill(14, 14, 8, 8);
            var fill = Box(area, Theme.Accent, null, "fill", false, 0.35f); var fr = fill.R(); fr.anchorMin = new Vector2(0, 0); fr.anchorMax = new Vector2(1, 1); fr.offsetMin = fr.offsetMax = Vector2.zero;
            var knobArea = Rt(track.transform, "knobArea"); knobArea.Fill(14, 14, 0, 0);
            var knob = Box(knobArea, Color.white, Theme.Black, "knob", false); knob.Sz(h * 0.6f, h + 8);
            s.fillRect = fr; s.handleRect = knob.R(); s.targetGraphic = knob; s.direction = Slider.Direction.LeftToRight; s.transition = Selectable.Transition.None;
            s.SetValueWithoutNotify(value); s.onValueChanged.AddListener(v => changed?.Invoke(v));
            return new Sld { s = s, track = track, fill = fill, knob = knob };
        }
    }

    /// <summary>Keeps grids inside their available width while preserving useful cell sizes.</summary>
    public class AdaptiveGrid : UIBehaviour, ILayoutSelfController
    {
        GridLayoutGroup grid; Vector2 designCell; int designColumns;
        public void Init(GridLayoutGroup g, Vector2 cell, int columns) { grid = g; designCell = cell; designColumns = columns; }
        public void SetLayoutHorizontal()
        {
            if (grid == null) return;
            float width = ((RectTransform)transform).rect.width - grid.padding.horizontal;
            if (width <= 1f) return;
            int columns = Mathf.Clamp(Mathf.FloorToInt((width + grid.spacing.x) / (designCell.x * 0.78f + grid.spacing.x)), 1, designColumns);
            float cellW = Mathf.Min(designCell.x, (width - (columns - 1) * grid.spacing.x) / columns);
            grid.constraintCount = columns;
            grid.cellSize = new Vector2(cellW, designCell.y * cellW / designCell.x);
        }
        public void SetLayoutVertical() { }
        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();
            if (grid != null && isActiveAndEnabled) LayoutRebuilder.MarkLayoutForRebuild((RectTransform)transform);
        }
    }

    /// <summary>Juicy button: press-scale, tint, click sound, disabled state.</summary>
    public class UButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler, IPointerExitHandler
    {
        public Image bg, icon; public Text label; public Action onClick; public Color baseColor = Color.white, labelTint = Color.white;
        public string sfx = "click"; public bool noTint; public bool interactable = true; public bool ornate, soft; public float plateW, plateH; public Action<UButton> onSound;
        public static Action<string> PlaySound;
        Tw.H press; bool down; Vector3 baseScale = Vector3.one;
        public bool Interactable { get => interactable; set { interactable = value; Apply(); } }
        public void SetColor(Color c) { baseColor = c; Apply(); }
        public void Apply()
        {
            if (bg == null || noTint) return;
            var c = interactable ? baseColor : Theme.GreyDark;
            bg.sprite = ornate ? LuxArt.Plate(c, plateW, plateH) : Boxes.Get(c, bevel: soft ? .06f : .22f);
            if (bg.sprite != null && bg.sprite.border != Vector4.zero) { bg.type = Image.Type.Sliced; bg.pixelsPerUnitMultiplier = 1f; }
            if (label) label.color = interactable ? labelTint : Theme.TextDim;
            if (icon) icon.color = interactable ? Color.white : new Color(1, 1, 1, 0.5f);
        }
        void Start() { baseScale = transform.localScale; Apply(); }
        public void OnPointerDown(PointerEventData e)
        {
            down = true; press?.Kill(); var t = transform;
            press = Tw.To(0.08f, k => { if (t) t.localScale = baseScale * Mathf.Lerp(1f, 0.94f, k); if (!noTint && bg) bg.color = Color.Lerp(Color.white, new Color(0.78f, 0.72f, 0.62f), k); }, Ease.Linear, 0, null, this);
        }
        public void OnPointerUp(PointerEventData e) { Release(); }
        public void OnPointerExit(PointerEventData e) { Release(); }
        void Release()
        {
            if (!down) return; down = false; press?.Kill(); var t = transform;
            if (!noTint && bg) bg.color = Color.white;
            float s0 = t.localScale.x / (baseScale.x == 0 ? 1 : baseScale.x);
            press = Tw.To(0.2f, k => { if (t) t.localScale = baseScale * Mathf.LerpUnclamped(s0, 1f, k); }, Ease.OutBack, 0, null, this);
        }
        public void OnPointerClick(PointerEventData e)
        {
            if (!interactable) { PlaySound?.Invoke("deny"); return; }
            PlaySound?.Invoke(sfx);
            onClick?.Invoke();
        }
        void OnDisable() { down = false; if (transform) transform.localScale = baseScale; }
    }
}
