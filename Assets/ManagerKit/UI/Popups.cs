using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ManagerKit.UI
{
    /// <summary>Modal dialog base. Subclass, override Build; call Popups.Show(p).</summary>
    public abstract class Popup
    {
        public RectTransform Root, Panel, Body, Footer; public Text TitleText; public CanvasGroup Group; public Image Dim;
        U.Scroll scroll;
        RectTransform header, bodyArea;
        float heightLimit, bodyChrome, headerTop, headerHeight = 108;
        public virtual string Title => "";
        public virtual float Width => 1024;
        /// <summary>Maximum panel height in canvas px; &lt;=0 uses the safe screen height. Short scrolling bodies fit their content.</summary>
        public virtual float Height => 0;
        public virtual bool CloseOnDim => true;
        public virtual bool ShowClose => true;
        public virtual bool UseScroll => true;
        public virtual bool HasFooter => false;
        public virtual Color Tint => Theme.Panel;
        public abstract void Build(RectTransform body);
        public virtual void BuildFooter(RectTransform footer) { }
        public virtual void OnShown() { }
        public virtual void OnClosed() { }
        public virtual void Tick() { }
        public virtual bool OnBack() { Close(); return true; }
        public bool Dirty;
        public bool IsOpen { get; internal set; }
        public void Close() { Popups.Close(this); }
        public void Redraw() { Dirty = true; }

        internal void Mount(UiHost h)
        {
            Root = U.Rt(h.popups, GetType().Name).Fill();
            Dim = U.Solid(Root, new Color(0.015f, 0.006f, 0.03f, 0.8f), "dim", true);
            Dim.Fill();
            var db = Dim.gameObject.AddComponent<UButton>(); db.bg = Dim; db.noTint = true; db.sfx = null; db.onClick = () => { if (CloseOnDim) OnBack(); };
            Group = Root.gameObject.AddComponent<CanvasGroup>();

            bool framed = LuxArt.HasFrame;
            float edge = framed ? Mathf.Min(64f, LuxArt.PanelInset) : 0f;
            float side = framed ? edge * 0.72f : 0f;
            float headTop = framed ? edge * (ShowClose ? 0.8f : 0.55f) : 0f;
            headerTop = headTop;
            Canvas.ForceUpdateCanvases();
            var available = h.safe.rect.size;
            if (available.x <= 0 || available.y <= 0) available = UiHost.CanvasSize;
            float hh = Mathf.Min(Height > 0 ? Height : available.y * 0.88f, available.y - 64f);
            heightLimit = hh;
            Panel = U.Box(Root, Tint, Theme.Black, "panel", true, framed ? 0.05f : 0.22f).R();
            Panel.At(0.5f, 0.5f, 0, 0, Mathf.Min(Width, available.x - 48f), hh);
            LuxArt.Raise(Panel.GetComponent<Graphic>(), 16f, 0.55f);
            var head = U.Box(Panel, Color.Lerp(Tint, Theme.Inset, 0.4f), Theme.Gold, "head", false, framed ? 0.05f : 0.18f).R();
            header = head;
            head.anchorMin = new Vector2(0, 1); head.anchorMax = new Vector2(1, 1); head.pivot = new Vector2(0.5f, 1);
            head.offsetMin = new Vector2(side, -(headTop + 108)); head.offsetMax = new Vector2(-side, -headTop);
            float titleSide = ShowClose ? 120f : 28f;
            TitleText = U.Txt(head, L.T(Title), 48, Theme.Gold, TextAnchor.MiddleCenter, true); TitleText.Fill(titleSide, titleSide, 8, 8);
            if (ShowClose)
            {
                var x = U.IconBtn(head, Icons.Get("cross"), Theme.RedDark, () => OnBack(), 84, "close"); x.At(1, 0.5f, -22, 0, 84, 84, 1, 0.5f);
            }
            float footH = HasFooter ? 124 : 0;
            float footBot = framed ? edge * 0.75f : 0f;
            if (HasFooter)
            {
                Footer = U.Box(Panel, Color.Lerp(Tint, Theme.Inset, 0.4f), Theme.Black, "foot", false, framed ? 0.05f : 0.18f).R();
                Footer.anchorMin = new Vector2(0, 0); Footer.anchorMax = new Vector2(1, 0); Footer.pivot = new Vector2(0.5f, 0);
                Footer.offsetMin = new Vector2(side, footBot); Footer.offsetMax = new Vector2(-side, footBot + footH);
                U.HBox(Footer, 20, new RectOffset(24, 24, 12, 12), TextAnchor.MiddleCenter, true, true, true, true);
            }
            float topPad = headTop + 118;
            float botPad = footBot + footH + (framed ? edge * 0.28f : 14f);
            bodyChrome = topPad + botPad;
            var area = U.Rt(Panel, "area"); area.Fill(Mathf.Max(24f, side * 0.65f), Mathf.Max(24f, side * 0.65f), topPad, botPad);
            bodyArea = area;
            if (UseScroll) { scroll = U.ScrollV(area, 16, new RectOffset(20, 20, 16, 30)); Body = scroll.content; }
            else Body = U.Rt(area, "body").Fill();
            FitHeader();
            BuildAll();
            FitBodyHeight();
            if (framed)
            {
                var fr = U.Img(Panel, LuxArt.PanelFrame, Color.white, "ornate");
                fr.raycastTarget = false; fr.Fill(10, 10, 10, 10); fr.transform.SetAsLastSibling();
            }
            Tw.Fade(Group, 0, 1, 0.16f); Tw.Scale(Panel, 0.9f, 1f, 0.32f, Ease.OutBack);
            IsOpen = true;
        }

        void BuildAll()
        {
            U.Clear(Body); if (Footer != null) U.Clear(Footer);
            Build(Body);
            if (HasFooter) BuildFooter(Footer);
        }
        void FitBodyHeight()
        {
            if (scroll == null || !Panel || !Body) return;
            Canvas.ForceUpdateCanvases();
            U.Rebuild(Body);
            float contentHeight = LayoutUtility.GetPreferredHeight(Body);
            if (contentHeight <= 0) return;
            float fitted = Mathf.Min(heightLimit, Mathf.Max(360f, bodyChrome + contentHeight + 8f));
            Panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, fitted);
            Canvas.ForceUpdateCanvases();
            U.Rebuild(Body);
        }
        internal void Rebuild()
        {
            Dirty = false; float pos = scroll != null ? scroll.Pos : 0;
            if (TitleText) TitleText.text = L.T(Title);
            FitHeader();
            BuildAll();
            FitBodyHeight();
            if (scroll != null) { Canvas.ForceUpdateCanvases(); U.Rebuild(scroll.content); scroll.Pos = Mathf.Clamp(pos, 0, Mathf.Max(0, scroll.content.rect.height - scroll.view.rect.height)); }
        }

        void FitHeader()
        {
            if (!TitleText || !header || !bodyArea) return;
            Canvas.ForceUpdateCanvases();
            float needed = Mathf.Max(108, TitleText.preferredHeight + 16);
            float change = needed - headerHeight;
            if (Mathf.Abs(change) < .1f) return;
            header.offsetMin = new Vector2(header.offsetMin.x, -(headerTop + needed));
            bodyArea.offsetMax += new Vector2(0, -change);
            bodyChrome += change; headerHeight = needed;
        }
    }

    public static class Popups
    {
        static readonly List<Popup> stack = new List<Popup>();
        public static Popup Top => stack.Count > 0 ? stack[stack.Count - 1] : null;
        public static int Count => stack.Count;
        public static event Action<Popup> Opened;
        public static event Action<Popup> Closed;

        public static T Show<T>(T p) where T : Popup
        {
            var h = UiHost.I; stack.Add(p); p.Mount(h); p.OnShown(); Opened?.Invoke(p); return p;
        }
        public static void Close(Popup p)
        {
            if (!stack.Remove(p)) return;
            p.IsOpen = false; p.OnClosed();
            if (p.Root) UnityEngine.Object.Destroy(p.Root.gameObject);
            Closed?.Invoke(p);
        }
        public static void CloseAll() { while (stack.Count > 0) Close(stack[stack.Count - 1]); }
        public static bool Back() { var t = Top; if (t == null) return false; t.OnBack(); return true; }
        public static void Update()
        {
            for (int i = 0; i < stack.Count; i++) { var p = stack[i]; if (p.Dirty) p.Rebuild(); p.Tick(); }
        }
        public static void RedrawAll() { foreach (var p in stack) p.Dirty = true; }

        // ------- common dialogs -------
        public static MsgPopup Message(string title, string text, string ok = null, Action onOk = null)
            => Show(new MsgPopup { title = title, text = text, buttons = new List<(string, Color, Action)> { (ok ?? L.T("OK"), Theme.Green, onOk) } });
        public static MsgPopup Confirm(string title, string text, string yes, Action onYes, string no = null, bool danger = false)
            => Show(new MsgPopup { title = title, text = text, buttons = new List<(string, Color, Action)> { (no ?? L.T("Cancel"), Theme.Grey, null), (yes, danger ? Theme.Red : Theme.Green, onYes) } });
    }

    public class MsgPopup : Popup
    {
        public string title, text; public List<(string label, Color color, Action act)> buttons = new List<(string, Color, Action)>();
        public override string Title => title;
        public override float Height => 520;
        public override bool UseScroll => false;
        public override bool HasFooter => true;
        public override bool ShowClose => false;
        public override void Build(RectTransform body)
        {
            var t = U.Txt(body, text, 40, Theme.Text, TextAnchor.MiddleCenter, false, true); t.Fill(30, 30, 20, 20);
        }
        public override void BuildFooter(RectTransform f)
        {
            foreach (var b in buttons) { var bb = b; U.Btn(f, bb.label, bb.color, () => { Close(); bb.act?.Invoke(); }, 300, 100, 42).Le(-1, 100, 1); }
        }
    }

    // =====================================================================================
    public static class Toasts
    {
        static readonly Queue<(string, Sprite, Color)> q = new Queue<(string, Sprite, Color)>();
        static bool busy;
        public static float TopOffset = 220;
        static int generation;
        public static void Clear()
        {
            generation++; q.Clear(); busy = false;
            if (UiHost.I != null) U.Clear(UiHost.I.toasts);
        }
        public static void Show(string text, Sprite icon = null, Color? c = null)
        {
            q.Enqueue((text, icon, c ?? Theme.Panel)); if (!busy) Next();
        }
        static void Next()
        {
            if (q.Count == 0 || UiHost.I == null) { busy = false; return; }
            busy = true; var (text, icon, col) = q.Dequeue();
            int token = generation;
            var box = U.Box(UiHost.I.toasts, col, Theme.Black, "toast"); var r = box.R();
            var t = U.Txt(box.transform, text, 38, col.grayscale > 0.64f ? Theme.TextDark : new Color(1f, 0.98f, 0.9f), TextAnchor.MiddleCenter, true);
            float w = Mathf.Min(UiHost.I.safe.rect.width - 48f, t.preferredWidth + 90 + (icon ? 70 : 0));
            float textW = Mathf.Max(1f, w - (icon ? 100f : 40f));
            var settings = t.GetGenerationSettings(new Vector2(textW, 0)); settings.resizeTextForBestFit = false;
            float th = t.cachedTextGeneratorForLayout.GetPreferredHeight(text, settings) / t.pixelsPerUnit;
            float toastH = Mathf.Clamp(th + 32f, 96f, 180f);
            r.At(0.5f, 1, 0, -TopOffset, w, toastH, 0.5f, 1);
            t.Fill(icon ? 80 : 20, 20, 12, 14);
            if (icon) { var ic = U.Icon(box.transform, icon, 60); ic.At(0, 0.5f, 22, 0, 60, 60); }
            var cg = box.gameObject.AddComponent<CanvasGroup>();
            float dur = q.Count > 2 ? 1.0f : 1.9f;
            Tw.To(0.25f, k => { if (r) { cg.alpha = k; r.anchoredPosition = new Vector2(0, -TopOffset + 40 * (1 - k)); } }, Ease.OutCubic, 0, null, r);
            Tw.To(0.3f, k => { if (cg) cg.alpha = 1 - k; }, Ease.Linear, dur, () => { if (box) UnityEngine.Object.Destroy(box.gameObject); if (token == generation) Next(); }, r);
        }
    }

    // =====================================================================================
    public static class Fx
    {
        public static void FloatText(Transform parent, Vector2 pos, string text, Color c, int size = 48, float rise = 130, float dur = 0.9f, bool pop = false, bool bottomOrigin = false)
        {
            var t = U.Txt(parent, text, size, c, TextAnchor.MiddleCenter, true, false, "float"); var r = t.R(); r.anchoredPosition = pos;
            // Floating combat feedback is the only text without a card. It still needs
            // a real rect now that all normal UI text obeys its bounds.
            r.sizeDelta = new Vector2(Mathf.Clamp(t.preferredWidth + 36f, 160f, 700f), size * 1.65f);
            if (bottomOrigin) r.anchorMin = r.anchorMax = Vector2.zero;
            Tw.To(dur, k => { if (!r) return; r.anchoredPosition = pos + new Vector2(0, rise * Ease.OutCubic(k)); var cc = t.color; cc.a = k < 0.6f ? 1 : 1 - (k - 0.6f) / 0.4f; t.color = cc; if (pop) r.localScale = Vector3.one * (1 + 0.5f * Mathf.Sin(Mathf.Min(1, k * 4) * Mathf.PI * 0.5f) * (1 - k)); }, Ease.Linear, 0, () => { if (r) UnityEngine.Object.Destroy(r.gameObject); }, r);
        }

        public static void Burst(Transform parent, Vector2 pos, Sprite spr, int n, float speed = 400, Color? tint = null, float size = 26, float life = 0.8f, float gravity = 900, bool bottomOrigin = false)
        {
            for (int i = 0; i < n; i++)
            {
                var img = U.Img(parent, spr, tint ?? Color.white, "p"); var r = img.R(); r.sizeDelta = Vector2.one * size * UnityEngine.Random.Range(0.6f, 1.2f); r.anchoredPosition = pos;
                if (bottomOrigin) r.anchorMin = r.anchorMax = Vector2.zero;
                float a = UnityEngine.Random.Range(0f, Mathf.PI * 2), s = speed * UnityEngine.Random.Range(0.4f, 1f);
                var v = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * s + Vector2.up * speed * 0.3f; float rot = UnityEngine.Random.Range(-360f, 360f);
                float lf = life * UnityEngine.Random.Range(0.7f, 1.2f);
                Tw.To(lf, k => { if (!r) return; float tt = k * lf; r.anchoredPosition = pos + v * tt + Vector2.down * 0.5f * gravity * tt * tt; r.localRotation = Quaternion.Euler(0, 0, rot * tt); var c = img.color; c.a = 1 - Mathf.Pow(k, 3); img.color = c; }, Ease.Linear, 0, () => { if (r) UnityEngine.Object.Destroy(r.gameObject); }, r);
            }
        }

        public static void Rays(Transform parent, Vector2 pos, float size, Color c, bool bottomOrigin = false)
        {
            var g = U.Img(parent, Boxes.Glow, new Color(c.r, c.g, c.b, 0.8f), "glow"); g.R().anchoredPosition = pos; g.R().sizeDelta = Vector2.one * size * 0.3f;
            if (bottomOrigin) g.R().anchorMin = g.R().anchorMax = Vector2.zero;
            Tw.To(0.6f, k => { if (g) { g.R().sizeDelta = Vector2.one * size * (0.3f + 0.7f * k); var cc = g.color; cc.a = 0.8f * (1 - k); g.color = cc; } }, Ease.OutCubic, 0, () => { if (g) UnityEngine.Object.Destroy(g.gameObject); }, g);
        }
    }
}
