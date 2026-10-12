using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ManagerKit.UI
{
    /// <summary>
    /// Painted UI kit (buttons, popup frame, filigree, embers) loaded from Resources/Art/ui.
    /// Button faces are recolored at runtime so one gold frame serves every enamel colour.
    /// </summary>
    public static class LuxArt
    {
        const int ButtonBorder = 24;
        const int PanelBorder = 204;
        const float PanelPpu = 210f;
        public static float PanelInset => HasFrame ? PanelBorder * (100f / PanelPpu) : 16f;

        static Texture2D buttonTex, panelTex, filigreeTex, emberTex;
        static bool tried;
        static Sprite panel, filigree, ember;
        static readonly Dictionary<string, Sprite> buttons = new Dictionary<string, Sprite>();

        public static bool HasButton { get { Ensure(); return buttonTex != null; } }
        public static bool HasFrame { get { Ensure(); return panel != null; } }
        public static Sprite PanelFrame { get { Ensure(); return panel; } }
        public static Sprite Filigree { get { Ensure(); return filigree; } }
        public static Sprite Ember { get { Ensure(); return ember; } }

        static void Ensure()
        {
            if (tried) return;
            tried = true;
            buttonTex = Load("button_base");
            panelTex = Load("panel_frame");
            filigreeTex = Load("filigree");
            emberTex = Load("ember");
            if (buttonTex != null && !buttonTex.isReadable) buttonTex = null;
            if (panelTex != null) panel = Sprite.Create(panelTex, new Rect(0, 0, panelTex.width, panelTex.height), new Vector2(0.5f, 0.5f), PanelPpu, 0, SpriteMeshType.FullRect, new Vector4(PanelBorder, PanelBorder, PanelBorder, PanelBorder));
            if (filigreeTex != null) filigree = Sprite.Create(filigreeTex, new Rect(0, 0, filigreeTex.width, filigreeTex.height), new Vector2(0.5f, 0.5f), 100f);
            if (emberTex != null) ember = Sprite.Create(emberTex, new Rect(0, 0, emberTex.width, emberTex.height), new Vector2(0.5f, 0.5f), 100f);
        }

        static Texture2D Load(string name)
        {
            var t = Resources.Load<Texture2D>("Art/ui/" + name);
            if (t == null) return null;
            t.filterMode = FilterMode.Bilinear;
            t.wrapMode = TextureWrapMode.Clamp;
            return t;
        }

        /// <summary>Wide ornate button when it fits; otherwise the procedural enamel frame.</summary>
        public static Sprite Plate(Color fill, float w, float h, float bevel = 0.22f)
        {
            if (fill.a > 0.45f && w >= 200f && h >= 86f && h <= 280f && w >= h * 1.65f)
            {
                var s = Button(fill);
                if (s != null) return s;
            }
            return Boxes.Get(fill, null, bevel);
        }

        public static Sprite Button(Color fill)
        {
            Ensure();
            if (buttonTex == null) return null;
            string key = ColorUtility.ToHtmlStringRGBA(fill);
            if (buttons.TryGetValue(key, out var s)) return s;
            try
            {
                var src = buttonTex.GetPixels32();
                int w = buttonTex.width, h = buttonTex.height;
                var dst = new Color32[src.Length];
                float tr = fill.r, tg = fill.g, tb = fill.b;
                for (int i = 0; i < src.Length; i++)
                {
                    var p = src[i];
                    int x = i % w, y = i / w;
                    if (p.a < 8 || IsFrame(p.r, p.g, p.b, x, y, w, h)) { dst[i] = p; continue; }
                    float lum = (p.r * 0.3f + p.g * 0.59f + p.b * 0.11f) / 255f;
                    float n = Mathf.Clamp01((lum - 0.04f) / 0.62f);
                    float rr, gg, bb;
                    if (n < 0.62f)
                    {
                        float k = n / 0.62f;
                        rr = tr * (0.28f + 0.72f * k); gg = tg * (0.28f + 0.72f * k); bb = tb * (0.28f + 0.72f * k);
                    }
                    else
                    {
                        float k = (n - 0.62f) / 0.38f;
                        rr = tr + (1f - tr) * k * 0.85f; gg = tg + (1f - tg) * k * 0.85f; bb = tb + (1f - tb) * k * 0.85f;
                    }
                    dst[i] = new Color32((byte)(Mathf.Clamp01(rr) * 255f), (byte)(Mathf.Clamp01(gg) * 255f), (byte)(Mathf.Clamp01(bb) * 255f), p.a);
                }
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, name = "btn_" + key };
                tex.SetPixels32(dst); tex.Apply(false, true);
                s = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(ButtonBorder, ButtonBorder, ButtonBorder, ButtonBorder));
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("Lux button recolor failed: " + e.Message);
                buttonTex = null; return null;
            }
            buttons[key] = s; return s;
        }

        static bool Goldish(byte r, byte g, byte b) => r > 110 && g > 70 && r + 15 > g && r > b + 35 && g > b + 10;

        static bool IsFrame(byte r, byte g, byte b, int x, int y, int w, int h)
        {
            int edge = Mathf.Min(Mathf.Min(x, w - 1 - x), Mathf.Min(y, h - 1 - y));
            int corner = Mathf.Min(w, h) / 5;
            int cdx = Mathf.Min(x, w - 1 - x), cdy = Mathf.Min(y, h - 1 - y);
            bool inCorner = cdx < corner && cdy < corner;
            if (Goldish(r, g, b) && (edge < corner || inCorner)) return true;
            if (inCorner && edge < corner && b > 70 && b > g) return true;
            if (edge < Mathf.Min(w, h) / 14 && (r > 80 || g > 80) && b < 190) return true;
            return false;
        }

        public static void Raise(Graphic g, float dy = 8f, float a = 0.5f)
        {
            if (g == null || g.GetComponent<Shadow>() != null) return;
            var sh = g.gameObject.AddComponent<Shadow>();
            sh.effectColor = new Color(0.04f, 0.015f, 0.02f, a);
            sh.effectDistance = new Vector2(0f, -dy);
        }
    }

    /// <summary>A slow diagonal highlight that sweeps across a button.</summary>
    public class Gleam : MonoBehaviour
    {
        RectTransform band;
        Image img;
        float wait, t;
        void Start()
        {
            img = U.Img(transform, Boxes.Sheen(), new Color(1f, 0.97f, 0.88f, 0f), "gleam");
            band = img.rectTransform;
            band.anchorMin = Vector2.zero;
            band.anchorMax = new Vector2(0f, 1f);
            band.pivot = new Vector2(0.5f, 0.5f);
            band.sizeDelta = new Vector2(140f, 0f);
            band.localRotation = Quaternion.Euler(0f, 0f, 18f);
            wait = Random.Range(0.2f, 3.4f);
        }
        void Update()
        {
            if (!band) return;
            if (wait > 0f) { wait -= Time.unscaledDeltaTime; return; }
            t += Time.unscaledDeltaTime;
            float w = ((RectTransform)transform).rect.width;
            float k = Mathf.Clamp01(t / 0.8f);
            band.anchoredPosition = new Vector2(Mathf.Lerp(-90f, w + 90f, k), 0f);
            var c = img.color; c.a = Mathf.Sin(k * Mathf.PI) * 0.38f; img.color = c;
            if (t >= 0.8f) { t = 0f; wait = Random.Range(3.4f, 6.5f); }
        }
    }
}
