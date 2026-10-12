using System.Collections.Generic;
using UnityEngine;

namespace ManagerKit.UI
{
    public static class Theme
    {
        public const float PxScale = 4f;   // size of one "texel" of the pixel-art UI boxes, in canvas pixels
        static Font font;
        public static Font Font
        {
            get
            {
                if (font == null) font = Resources.Load<Font>("Fonts/Jersey10");
                if (font == null) font = Font.CreateDynamicFontFromOSFont("Arial", 24);
                return font;
            }
        }

        public static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }

        public static readonly Color Bg0 = Hex("#dceee3"), Bg1 = Hex("#f6ebce");
        public static readonly Color PanelDark = Hex("#eee1c2"), Panel = Hex("#fbf0d7"), PanelLight = Hex("#fff9e8"), Inset = Hex("#e2e7ce");
        public static readonly Color Text = Hex("#453327"), TextDim = Hex("#75614a"), TextDark = Hex("#38281c");
        public static readonly Color Gold = Hex("#95651f"), Good = Hex("#28683d"), Bad = Hex("#ad4036"), Warn = Hex("#9a5620");
        public static readonly Color Accent = Hex("#efad42"), AccentDark = Hex("#ae732e"), Blue = Hex("#65b4c7"), BlueDark = Hex("#36788d");
        public static readonly Color Green = Hex("#74ad62"), GreenDark = Hex("#497b47"), Red = Hex("#d66a52"), RedDark = Hex("#a84c3c");
        public static readonly Color Purple = Hex("#9589ba"), PurpleDark = Hex("#736994"), Grey = Hex("#a4ac9c"), GreyDark = Hex("#d7d8c5");
        public static readonly Color Parchment = Hex("#f1e6c8"), ParchmentDark = Hex("#d6c394");
        public static readonly Color Black = Hex("#8b7350");
    }

    /// <summary>Painted, 9-sliced frames. Gold enamel for panels, recessed tracks, and glossy bars.</summary>
    public static class Boxes
    {
        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        public static Sprite Get(Color fill, Color? border = null, float bevel = 0.22f, bool flat = false)
        {
            var bc = border ?? Color.Lerp(fill, Color.black, 0.55f);
            int kind = fill.a < 0.04f ? 4 : flat || bevel <= 0.06f ? 3 : bevel >= 0.32f ? 2 : bevel <= 0.14f ? 1 : 0;
            string key = kind + ColorUtility.ToHtmlStringRGBA(fill) + ColorUtility.ToHtmlStringRGBA(bc);
            if (cache.TryGetValue(key, out var sp)) return sp;
            sp = kind == 4 ? Clear() : Paint(fill, bc, kind);
            cache[key] = sp; return sp;
        }

        static Sprite Clear()
        {
            var t = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            t.SetPixels32(new Color32[16]); t.Apply(false, true);
            return Sprite.Create(t, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(1, 1, 1, 1));
        }

        /// <summary>kind: 0 plaque, 1 inset, 2 gloss bar, 3 soft fill.</summary>
        static Sprite Paint(Color fill, Color border, int kind)
        {
            int size = kind == 2 ? 72 : 128;
            int rad = kind == 2 ? 10 : kind == 3 ? 18 : 14;
            int slice = kind == 2 ? 10 : kind == 1 ? 11 : kind == 3 ? 14 : 13;
            // Cream, ivory and sage paper all share a light face. Saturated enamel
            // keeps the stronger bevel; a pale ivory must never inherit its shading.
            bool paper = fill.grayscale > 0.73f && Mathf.Min(fill.r, fill.g, fill.b) > 0.60f && Mathf.Max(fill.r, fill.g, fill.b) - Mathf.Min(fill.r, fill.g, fill.b) < 0.28f;
            Color trim = TrimColor(fill, border, paper);
            Color hi = Color.Lerp(trim, new Color(1f, 0.97f, 0.86f), 0.72f);
            Color mid = trim;
            Color lo = Color.Lerp(trim, new Color(0.28f, 0.13f, 0.04f), 0.62f);
            Color ink = new Color(0.09f, 0.04f, 0.05f, 1f);
            var px = new Color32[size * size];
            float hw = size * 0.5f - 2.2f, hh = hw;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float pxc = x + 0.5f - size * 0.5f, pyc = y + 0.5f - size * 0.5f;
                    float sd = SdRound(pxc, pyc, hw, hh, rad);
                    float cov = Mathf.Clamp01(0.9f - sd);
                    if (cov <= 0f) continue;
                    float inset = -sd;
                    Color c = fill;
                    if (kind == 3)
                    {
                        float t = Mathf.InverseLerp(-hh, hh, pyc);
                        c = Color.Lerp(Color.Lerp(fill, paper ? new Color(0.62f, 0.48f, 0.28f) : Color.black, paper ? 0.045f : 0.22f), Color.Lerp(fill, Color.white, paper ? 0.035f : 0.08f), t);
                    }
                    else if (kind == 2)
                    {
                        float t = Mathf.InverseLerp(-hh, hh, pyc);
                        c = Color.Lerp(Color.Lerp(fill, Color.black, 0.28f), Color.Lerp(fill, Color.white, 0.42f), t * t);
                        if (inset < 1.5f) c = Color.Lerp(ink, c, inset / 1.5f);
                    }
                    else
                    {
                        bool plaque = kind == 0;
                        float lip = plaque ? 10.5f : 8.2f;
                        if (inset < 1.5f) c = ink;
                        else if (inset < 3.1f) c = Color.Lerp(hi, mid, (inset - 1.5f) / 1.6f);
                        else if (inset < lip - 2.4f)
                        {
                            float m = Mathf.InverseLerp(3.1f, lip - 2.4f, inset);
                            float light = Mathf.Clamp01(0.35f + 0.65f * Mathf.InverseLerp(-hh, hh, pyc));
                            c = Color.Lerp(lo, hi, light * (1f - m * 0.35f));
                            c = Color.Lerp(c, mid, 0.25f);
                        }
                        else if (inset < lip - 0.8f) c = Color.Lerp(ink, lo, 0.55f);
                        else if (inset < lip) c = Color.Lerp(mid, hi, 0.35f);
                        else
                        {
                            float t = Mathf.InverseLerp(-hh, hh, pyc);
                            Color bot = Color.Lerp(fill, paper ? new Color(0.62f, 0.48f, 0.28f) : Color.black, paper ? 0.045f : 0.30f);
                            Color top = Color.Lerp(fill, Color.white, paper ? 0.06f : 0.16f);
                            c = Color.Lerp(bot, top, Mathf.SmoothStep(0f, 1f, t));
                            float vig = Mathf.Clamp01((inset - lip) / 18f);
                            c = Color.Lerp(Color.Lerp(c, paper ? new Color(0.62f, 0.48f, 0.28f) : Color.black, paper ? 0.065f : 0.38f), c, vig);
                            if (!paper && pyc > hh * 0.25f)
                            {
                                float g = Mathf.Clamp01((pyc - hh * 0.25f) / (hh * 0.7f));
                                c = Color.Lerp(c, Color.white, g * g * 0.16f);
                            }
                            if (paper && (y % 6 == 0)) c = Color.Lerp(c, new Color(0.62f, 0.48f, 0.28f), 0.06f);
                        }
                        if (plaque && inset > 2.2f && inset < 9f)
                        {
                            float cdx = Mathf.Abs(pxc) - (hw - 12f), cdy = Mathf.Abs(pyc) - (hh - 12f);
                            float dia = Mathf.Abs(cdx) + Mathf.Abs(cdy);
                            if (cdx > -2f && cdy > -2f && dia < 5.5f)
                                c = Color.Lerp(lo, hi, 1f - dia / 5.5f);
                        }
                    }
                    float n = Hash(x, y);
                    if (kind != 2) c = Color.Lerp(c, Color.white, (n - 0.5f) * (paper ? 0.05f : 0.035f));
                    c.a = fill.a * cov;
                    px[y * size + x] = c;
                }
            }
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, name = "frame" };
            tex.SetPixels32(px); tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(slice, slice, slice, slice));
        }

        static Color TrimColor(Color fill, Color border, bool paper)
        {
            float max = Mathf.Max(border.r, border.g, border.b), min = Mathf.Min(border.r, border.g, border.b);
            if (!paper && max - min > 0.2f && max > 0.25f && max < 0.97f)
                return Color.Lerp(border, new Color(1f, 0.88f, 0.55f), 0.42f);
            return paper ? new Color(0.62f, 0.42f, 0.16f) : new Color(0.92f, 0.74f, 0.36f);
        }

        static float SdRound(float x, float y, float hw, float hh, float r)
        {
            float dx = Mathf.Abs(x) - (hw - r), dy = Mathf.Abs(y) - (hh - r);
            float ax = Mathf.Max(dx, 0f), ay = Mathf.Max(dy, 0f);
            return Mathf.Sqrt(ax * ax + ay * ay) + Mathf.Min(Mathf.Max(dx, dy), 0f) - r;
        }

        static float Hash(int x, int y)
        {
            uint n = (uint)(x * 374761393 + y * 668265263);
            n = (n ^ (n >> 13)) * 1274126177u;
            return ((n ^ (n >> 16)) & 255) / 255f;
        }

        static readonly Dictionary<string, Sprite> grads = new Dictionary<string, Sprite>();
        public static Sprite Gradient(Color top, Color bottom)
        {
            string key = ColorUtility.ToHtmlStringRGBA(top) + ColorUtility.ToHtmlStringRGBA(bottom);
            if (grads.TryGetValue(key, out var sp)) return sp;
            const int N = 160;
            bool opaque = top.a > 0.9f && bottom.a > 0.9f;
            bool daylight = top.grayscale > 0.6f && bottom.grayscale > 0.6f;
            var t = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[N * N];
            for (int y = 0; y < N; y++) for (int x = 0; x < N; x++)
                {
                    float v = y / (N - 1f);
                    Color c = Color.Lerp(bottom, top, Mathf.SmoothStep(0f, 1f, v));
                    if (opaque)
                    {
                        float nx = (x + 0.5f) / N - 0.5f, ny = (y + 0.5f) / N - 0.5f;
                        float vig = Mathf.Clamp01(1.05f - (nx * nx * 1.15f + ny * ny * 1.45f) * 1.7f);
                        c = Color.Lerp(Color.Lerp(c, Color.black, daylight ? 0.06f : 0.55f), c, vig);
                        c = Color.Lerp(c, Color.white, (Hash(x, y) - 0.5f) * (daylight ? 0.018f : 0.045f));
                        float glow = Mathf.Exp(-(nx * nx * 7f + (ny + 0.32f) * (ny + 0.32f) * 9f));
                        c = Color.Lerp(c, daylight ? new Color(1f, 0.94f, 0.7f) : new Color(0.55f, 0.22f, 0.08f), glow * (daylight ? 0.1f : 0.22f));
                        c.a = 1f;
                    }
                    px[y * N + x] = c;
                }
            t.SetPixels32(px); t.Apply(false, true);
            sp = Sprite.Create(t, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f);
            grads[key] = sp; return sp;
        }

        static Sprite sheen, vignette;
        public static Sprite Sheen()
        {
            if (sheen != null) return sheen;
            const int W = 64, H = 16;
            var t = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[W * H];
            for (int y = 0; y < H; y++) for (int x = 0; x < W; x++)
                {
                    float u = Mathf.Abs((x + 0.5f) / W * 2f - 1f);
                    float a = Mathf.Clamp01(1f - u); a = a * a;
                    px[y * W + x] = new Color(1f, 1f, 1f, a);
                }
            t.SetPixels32(px); t.Apply(false, true);
            sheen = Sprite.Create(t, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 100f);
            return sheen;
        }

        public static Sprite Vignette()
        {
            if (vignette != null) return vignette;
            const int N = 256;
            var t = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[N * N];
            for (int y = 0; y < N; y++) for (int x = 0; x < N; x++)
                {
                    float nx = (x + 0.5f) / N * 2f - 1f, ny = (y + 0.5f) / N * 2f - 1f;
                    float d = Mathf.Sqrt(nx * nx * 0.72f + ny * ny * 0.95f);
                    float a = Mathf.SmoothStep(0.42f, 1.15f, d);
                    px[y * N + x] = new Color(1f, 1f, 1f, a);
                }
            t.SetPixels32(px); t.Apply(false, true);
            vignette = Sprite.Create(t, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f);
            return vignette;
        }

        static Sprite white, circle, glow;
        public static Sprite White { get { if (white == null) { var t = new Texture2D(2, 2); t.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white }); t.Apply(false, true); white = Sprite.Create(t, new Rect(0, 0, 2, 2), new Vector2(.5f, .5f), 100f); } return white; } }
        public static Sprite Circle { get { if (circle == null) { var pc = new PixelCanvas(32, 32); pc.Disc(16, 16, 15.5f, Color.white); circle = pc.ToSprite(100f); circle.texture.filterMode = FilterMode.Bilinear; } return circle; } }
        /// <summary>Soft radial glow (white, alpha falloff) for halos and vignettes.</summary>
        public static Sprite Glow
        {
            get
            {
                if (glow == null)
                {
                    var t = new Texture2D(64, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                    for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++) { float d = Vector2.Distance(new Vector2(x, y), new Vector2(31.5f, 31.5f)) / 32f; float a = Mathf.Clamp01(1 - d); t.SetPixel(x, y, new Color(1, 1, 1, a * a)); }
                    t.Apply(false, true); glow = Sprite.Create(t, new Rect(0, 0, 64, 64), new Vector2(.5f, .5f), 100f);
                }
                return glow;
            }
        }
    }
}
