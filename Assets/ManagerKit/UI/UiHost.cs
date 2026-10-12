using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ManagerKit.UI
{
    /// <summary>Owns the canvas, camera, safe-area handling and the standard layer stack.</summary>
    public class UiHost : MonoBehaviour
    {
        public static UiHost I;
        public Canvas canvas; public Camera cam; public CanvasScaler scaler;
        public RectTransform root, back, safe, screens, hud, popups, toasts, top;
        public Action OnFrame;
        public Action OnBackKey;
        Rect lastSafe; int lastW, lastH;
        public static Vector2 CanvasSize => I == null ? new Vector2(1080, 1920) : I.root.rect.size;

        public static UiHost Create()
        {
            var go = new GameObject("UiHost"); DontDestroyOnLoad(go);
            var h = go.AddComponent<UiHost>(); I = h;
            var camGo = new GameObject("UiCamera"); camGo.transform.SetParent(go.transform, false);
            h.cam = camGo.AddComponent<Camera>(); h.cam.orthographic = true; h.cam.clearFlags = CameraClearFlags.SolidColor; h.cam.backgroundColor = Theme.Bg1; h.cam.cullingMask = 1 << 5; h.cam.depth = -10; h.cam.allowHDR = false; h.cam.allowMSAA = false;
            camGo.AddComponent<AudioListener>();

            var cgo = new GameObject("Canvas", typeof(RectTransform)); cgo.layer = 5; cgo.transform.SetParent(go.transform, false);
            h.canvas = cgo.AddComponent<Canvas>(); h.canvas.renderMode = RenderMode.ScreenSpaceCamera; h.canvas.worldCamera = h.cam; h.canvas.planeDistance = 10; h.canvas.pixelPerfect = false;
            h.scaler = cgo.AddComponent<CanvasScaler>(); h.scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; h.scaler.referenceResolution = new Vector2(1080, 1920); h.scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight; h.scaler.matchWidthOrHeight = 0f; h.scaler.referencePixelsPerUnit = 100;
            cgo.AddComponent<GraphicRaycaster>();
            h.root = (RectTransform)cgo.transform;

            h.back = U.Rt(h.root, "Back").Fill();
            h.safe = U.Rt(h.root, "Safe").Fill();
            h.screens = U.Rt(h.safe, "Screens").Fill();
            h.hud = U.Rt(h.safe, "Hud").Fill();
            h.popups = U.Rt(h.root, "Popups").Fill();
            h.toasts = U.Rt(h.safe, "Toasts").Fill();
            h.top = U.Rt(h.root, "Top").Fill();

            if (FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var es = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(StandaloneInputModule));
                es.transform.SetParent(go.transform, false);
            }
            h.ApplySafeArea(true);
            return h;
        }

        void ApplySafeArea(bool force)
        {
            var sa = Screen.safeArea; int w = Screen.width, hgt = Screen.height;
            if (!force && sa == lastSafe && w == lastW && hgt == lastH) return;
            lastSafe = sa; lastW = w; lastH = hgt;
            if (w <= 0 || hgt <= 0) return;
            safe.anchorMin = new Vector2(sa.xMin / w, sa.yMin / hgt); safe.anchorMax = new Vector2(sa.xMax / w, sa.yMax / hgt); safe.offsetMin = safe.offsetMax = Vector2.zero;
            float aspect = (float)w / hgt;
            scaler.matchWidthOrHeight = aspect > 0.62f ? Mathf.Clamp01((aspect - 0.5625f) * 6f) : 0f;
        }

        void Update()
        {
            ApplySafeArea(false);
            if (Input.GetKeyDown(KeyCode.Escape)) OnBackKey?.Invoke();
            OnFrame?.Invoke();
        }

        public Vector2 ScreenToCanvas(Vector2 screenPos, RectTransform space)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(space, screenPos, cam, out var p); return p;
        }
    }

    // =====================================================================================
    /// <summary>A full-screen page. Rebuilt wholesale when marked dirty (cheap: pages hold &lt; 300 objects).</summary>
    public abstract class UiScreen
    {
        public RectTransform Root;
        public bool Dirty = true;
        public virtual string Id => GetType().Name;
        protected readonly List<U.Scroll> scrolls = new List<U.Scroll>();
        readonly List<float> savedScroll = new List<float>();
        public abstract void Build(RectTransform root);
        public virtual void Tick() { }
        public virtual void OnEnter() { }
        public virtual void OnExit() { }
        public virtual bool OnBack() => false;
        public void Redraw() { Dirty = true; }

        public U.Scroll MakeScroll(Transform parent, float spacing = 14, RectOffset pad = null) { var s = U.ScrollV(parent, spacing, pad); scrolls.Add(s); return s; }

        public void Rebuild()
        {
            Dirty = false; savedScroll.Clear();
            foreach (var s in scrolls) if (s != null && s.content) savedScroll.Add(s.Pos);
            scrolls.Clear(); U.Clear(Root);
            Build(Root);
            if (savedScroll.Count > 0)
            {
                Canvas.ForceUpdateCanvases();
                for (int i = 0; i < scrolls.Count && i < savedScroll.Count; i++)
                {
                    U.Rebuild(scrolls[i].content);
                    scrolls[i].Pos = Mathf.Clamp(savedScroll[i], 0, Mathf.Max(0, scrolls[i].content.rect.height - scrolls[i].view.rect.height));
                }
            }
        }
    }

    public class Nav
    {
        readonly UiHost host; readonly List<UiScreen> stack = new List<UiScreen>();
        public UiScreen Current => stack.Count > 0 ? stack[stack.Count - 1] : null;
        public int Depth => stack.Count;
        public event Action<UiScreen> Changed;
        public Nav(UiHost h) { host = h; }

        UiScreen Mount(UiScreen s)
        {
            s.Root = U.Rt(host.screens, s.Id).Fill(); s.Dirty = true; return s;
        }
        /// <summary>Replace the whole stack with one root screen.</summary>
        public void Root(UiScreen s, bool animate = false)
        {
            while (stack.Count > 0) Pop(false);
            Push(s, animate);
        }
        public void Push(UiScreen s, bool animate = true)
        {
            if (Current != null) { Current.OnExit(); Current.Root.gameObject.SetActive(false); }
            Mount(s); stack.Add(s); Changed?.Invoke(s); s.Rebuild(); s.OnEnter();
            if (animate) { var cg = s.Root.gameObject.AddComponent<CanvasGroup>(); Tw.Fade(cg, 0, 1, 0.18f); Tw.Move(s.Root, new Vector2(36, 0), Vector2.zero, 0.22f, Ease.OutCubic); Tw.Scale(s.Root, 0.985f, 1f, 0.22f, Ease.OutCubic); }
        }
        public void Pop(bool animate = true)
        {
            if (stack.Count == 0) return;
            var s = stack[stack.Count - 1]; stack.RemoveAt(stack.Count - 1);
            s.OnExit(); UnityEngine.Object.Destroy(s.Root.gameObject);
            Changed?.Invoke(Current);
            if (Current != null) { Current.Root.gameObject.SetActive(true); Current.Rebuild(); Current.OnEnter(); }
        }
        public void Update()
        {
            var c = Current; if (c == null) return;
            if (c.Dirty) c.Rebuild();
            c.Tick();
        }
        public bool Back()
        {
            var c = Current; if (c == null) return false;
            if (c.OnBack()) return true;
            if (stack.Count > 1) { Pop(); return true; }
            return false;
        }
    }
}
