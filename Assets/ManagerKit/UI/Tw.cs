using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ManagerKit.UI
{
    /// <summary>Allocation-light tween runner (unscaled time). All UI juice goes through here.</summary>
    public class Tw : MonoBehaviour
    {
        public class H { public bool dead; public void Kill() { dead = true; } }
        class Item { public Action<float> f; public float t, d, delay; public Func<float, float> ease; public Action done; public UnityEngine.Object owner; public bool loop, pingpong; public H h; }
        static Tw inst; static readonly List<Item> items = new List<Item>(); static readonly List<Item> add = new List<Item>();
        public static float TimeScale = 1f;

        static void Ensure() { if (inst == null) { var go = new GameObject("Tween"); DontDestroyOnLoad(go); inst = go.AddComponent<Tw>(); } }

        public static H To(float dur, Action<float> apply, Func<float, float> ease = null, float delay = 0, Action done = null, UnityEngine.Object owner = null, bool loop = false, bool pingpong = false)
        {
            Ensure();
            var h = new H();
            add.Add(new Item { f = apply, d = Mathf.Max(0.0001f, dur), delay = delay, ease = ease ?? Ease.OutQuad, done = done, owner = owner, loop = loop, pingpong = pingpong, h = h });
            return h;
        }
        public static H After(float sec, Action a, UnityEngine.Object owner = null) => To(0.0001f, _ => { }, null, sec, a, owner);

        void Update()
        {
            if (add.Count > 0) { items.AddRange(add); add.Clear(); }
            float dt = Time.unscaledDeltaTime * TimeScale;
            for (int i = items.Count - 1; i >= 0; i--)
            {
                var it = items[i];
                if (it.h.dead || IsDead(it.owner)) { items.RemoveAt(i); continue; }
                if (it.delay > 0) { it.delay -= dt; if (it.delay > 0) continue; dt = Mathf.Max(0, -it.delay); it.delay = 0; }
                it.t += dt;
                float k = Mathf.Clamp01(it.t / it.d);
                float v = it.pingpong ? (k < 0.5f ? k * 2 : (1 - k) * 2) : k;
                try { it.f(it.ease(v)); } catch (Exception e) { Debug.LogException(e); it.h.dead = true; }
                if (it.t >= it.d)
                {
                    if (it.loop && !it.h.dead) { it.t = 0; continue; }
                    items.RemoveAt(i);
                    try { it.done?.Invoke(); } catch (Exception e) { Debug.LogException(e); }
                }
            }
        }
        static bool IsDead(UnityEngine.Object o) => !ReferenceEquals(o, null) && o == null;

        // ------------ helpers ------------
        public static H Scale(Transform t, float from, float to, float dur, Func<float, float> ease = null, float delay = 0, Action done = null) => To(dur, k => { if (t) t.localScale = Vector3.one * Mathf.LerpUnclamped(from, to, k); }, ease ?? Ease.OutBack, delay, done, t);
        public static H Pop(Transform t, float dur = 0.35f, float delay = 0) { if (t) t.localScale = Vector3.zero; return Scale(t, 0f, 1f, dur, Ease.OutBack, delay); }
        public static H Punch(Transform t, float amount = 0.2f, float dur = 0.25f) => To(dur, k => { if (t) t.localScale = Vector3.one * (1 + amount * Mathf.Sin(k * Mathf.PI)); }, Ease.Linear, 0, () => { if (t) t.localScale = Vector3.one; }, t);
        public static H Move(RectTransform r, Vector2 from, Vector2 to, float dur, Func<float, float> ease = null, float delay = 0, Action done = null) => To(dur, k => { if (r) r.anchoredPosition = Vector2.LerpUnclamped(from, to, k); }, ease ?? Ease.OutCubic, delay, done, r);
        public static H Fade(CanvasGroup g, float from, float to, float dur, float delay = 0, Action done = null) => To(dur, k => { if (g) g.alpha = Mathf.Lerp(from, to, k); }, Ease.Linear, delay, done, g);
        public static H FadeG(Graphic g, float from, float to, float dur, float delay = 0, Action done = null) => To(dur, k => { if (g) { var c = g.color; c.a = Mathf.Lerp(from, to, k); g.color = c; } }, Ease.Linear, delay, done, g);
        public static H Pulse(Transform t, float amount = 0.06f, float period = 1.2f) => To(period, k => { if (t) t.localScale = Vector3.one * (1 + amount * k); }, Ease.InOutSine, 0, null, t, true, true);
        public static H Bob(RectTransform r, float amp = 8f, float period = 1.4f) { var p0 = r.anchoredPosition; return To(period, k => { if (r) r.anchoredPosition = p0 + new Vector2(0, amp * k); }, Ease.InOutSine, 0, null, r, true, true); }
        public static H Shake(RectTransform r, float amp = 12f, float dur = 0.3f) { var p0 = r.anchoredPosition; return To(dur, k => { if (r) r.anchoredPosition = p0 + new Vector2(UnityEngine.Random.Range(-1f, 1f), UnityEngine.Random.Range(-1f, 1f)) * amp * (1 - k); }, Ease.Linear, 0, () => { if (r) r.anchoredPosition = p0; }, r); }
        public static H Count(double from, double to, float dur, Action<double> apply, Action done = null, UnityEngine.Object owner = null) => To(dur, k => apply(from + (to - from) * k), Ease.OutCubic, 0, done, owner);
        public static H Spin(Transform t, float degPerSec) => To(1f, k => { if (t) t.Rotate(0, 0, -degPerSec * Time.unscaledDeltaTime); }, Ease.Linear, 0, null, t, true);
    }

    public static class Ease
    {
        public static float Linear(float k) => k;
        public static float OutQuad(float k) => 1 - (1 - k) * (1 - k);
        public static float OutCubic(float k) => 1 - Mathf.Pow(1 - k, 3);
        public static float InQuad(float k) => k * k;
        public static float InOutSine(float k) => -(Mathf.Cos(Mathf.PI * k) - 1) / 2;
        public static float OutBack(float k) { const float c1 = 1.70158f, c3 = c1 + 1; return 1 + c3 * Mathf.Pow(k - 1, 3) + c1 * Mathf.Pow(k - 1, 2); }
        public static float OutElastic(float k) { if (k <= 0) return 0; if (k >= 1) return 1; return Mathf.Pow(2, -10 * k) * Mathf.Sin((k * 10 - 0.75f) * (2 * Mathf.PI / 3)) + 1; }
    }
}
