using System;
using System.Collections.Generic;
using UnityEngine;

namespace ManagerKit.UI
{
    /// <summary>Tiny software pixel canvas used to draw crisp procedural icons and UI frames. Origin is bottom-left.</summary>
    public class PixelCanvas
    {
        public readonly int W, H;
        public readonly Color32[] P;
        public PixelCanvas(int w, int h) { W = w; H = h; P = new Color32[w * h]; }

        public bool In(int x, int y) => x >= 0 && y >= 0 && x < W && y < H;
        public Color32 Get(int x, int y) => In(x, y) ? P[y * W + x] : default;
        public PixelCanvas Px(int x, int y, Color32 c) { if (In(x, y)) P[y * W + x] = c; return this; }
        public PixelCanvas Rect(int x, int y, int w, int h, Color32 c) { for (int j = 0; j < h; j++) for (int i = 0; i < w; i++) Px(x + i, y + j, c); return this; }
        public PixelCanvas Line(int x0, int y0, int x1, int y1, Color32 c, int thick = 1)
        {
            int dx = Math.Abs(x1 - x0), dy = -Math.Abs(y1 - y0), sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1, err = dx + dy;
            while (true)
            {
                if (thick <= 1) Px(x0, y0, c); else Rect(x0, y0, thick, thick, c);
                if (x0 == x1 && y0 == y1) break;
                int e2 = 2 * err;
                if (e2 >= dy) { err += dy; x0 += sx; }
                if (e2 <= dx) { err += dx; y0 += sy; }
            }
            return this;
        }
        public PixelCanvas Disc(float cx, float cy, float r, Color32 c)
        {
            for (int y = 0; y < H; y++) for (int x = 0; x < W; x++) { float dx = x + 0.5f - cx, dy = y + 0.5f - cy; if (dx * dx + dy * dy <= r * r) P[y * W + x] = c; }
            return this;
        }
        public PixelCanvas Ring(float cx, float cy, float r, float rin, Color32 c)
        {
            for (int y = 0; y < H; y++) for (int x = 0; x < W; x++) { float dx = x + 0.5f - cx, dy = y + 0.5f - cy; float d = dx * dx + dy * dy; if (d <= r * r && d >= rin * rin) P[y * W + x] = c; }
            return this;
        }
        /// <summary>Filled convex/concave polygon (even-odd).</summary>
        public PixelCanvas Poly(Color32 c, params float[] xy)
        {
            int n = xy.Length / 2;
            for (int y = 0; y < H; y++) for (int x = 0; x < W; x++)
                {
                    float px = x + 0.5f, py = y + 0.5f; bool inside = false;
                    for (int i = 0, j = n - 1; i < n; j = i++)
                    {
                        float xi = xy[i * 2], yi = xy[i * 2 + 1], xj = xy[j * 2], yj = xy[j * 2 + 1];
                        if ((yi > py) != (yj > py) && px < (xj - xi) * (py - yi) / (yj - yi) + xi) inside = !inside;
                    }
                    if (inside) P[y * W + x] = c;
                }
            return this;
        }
        /// <summary>Adds a 1px outline around every opaque shape (grows the drawing by 1px).</summary>
        public PixelCanvas Outline(Color32 c)
        {
            var src = (Color32[])P.Clone();
            for (int y = 0; y < H; y++) for (int x = 0; x < W; x++)
                {
                    if (src[y * W + x].a > 0) continue;
                    bool n = (x > 0 && src[y * W + x - 1].a > 0) || (x < W - 1 && src[y * W + x + 1].a > 0) || (y > 0 && src[(y - 1) * W + x].a > 0) || (y < H - 1 && src[(y + 1) * W + x].a > 0);
                    if (n) P[y * W + x] = c;
                }
            return this;
        }
        public PixelCanvas Mirror() { for (int y = 0; y < H; y++) for (int x = 0; x < W / 2; x++) { var t = P[y * W + x]; P[y * W + x] = P[y * W + W - 1 - x]; P[y * W + W - 1 - x] = t; } return this; }

        public Texture2D ToTexture()
        {
            var t = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "px" };
            t.SetPixels32(P); t.Apply(false, true); return t;
        }
        public Sprite ToSprite(float ppu = 100f, Vector4? border = null)
        {
            var t = ToTexture();
            return Sprite.Create(t, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), ppu, 0, SpriteMeshType.FullRect, border ?? Vector4.zero);
        }
    }

    public static class C32
    {
        public static Color32 Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }
        public static Color32 Lerp(Color32 a, Color32 b, float t) => Color32.Lerp(a, b, t);
        public static Color32 Lighten(Color32 c, float t) => Color32.Lerp(c, new Color32(255, 255, 255, c.a), t);
        public static Color32 Darken(Color32 c, float t) => Color32.Lerp(c, new Color32(0, 0, 0, c.a), t);
    }
}
