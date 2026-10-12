using System;
using System.Collections.Generic;
using UnityEngine;

namespace ManagerKit.UI
{
    /// <summary>
    /// Procedural 16x16 pixel icons. Names are used as "icon:name" keys in content packs.
    /// Games can add their own with Icons.Register.
    /// </summary>
    public static class Icons
    {
        public delegate void Painter(PixelCanvas c, Color32 tint);
        static readonly Dictionary<string, Painter> painters = new Dictionary<string, Painter>();
        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        public static readonly Color32 OUT = new Color32(28, 18, 32, 255), WH = new Color32(255, 255, 255, 255);
        public static readonly Color32 GOLD = new Color32(255, 211, 78, 255), GOLD2 = new Color32(217, 154, 30, 255), GOLD3 = new Color32(255, 243, 168, 255);
        public static readonly Color32 RED = new Color32(232, 80, 58, 255), RED2 = new Color32(168, 42, 36, 255);
        public static readonly Color32 BLUE = new Color32(74, 168, 255, 255), CYAN = new Color32(111, 227, 255, 255), CYAN2 = new Color32(52, 160, 214, 255);
        public static readonly Color32 GREEN = new Color32(88, 210, 106, 255), GREEN2 = new Color32(38, 140, 70, 255);
        public static readonly Color32 PURPLE = new Color32(176, 112, 255, 255), PURPLE2 = new Color32(110, 64, 190, 255);
        public static readonly Color32 BROWN = new Color32(154, 106, 60, 255), BROWN2 = new Color32(106, 68, 38, 255);
        public static readonly Color32 STEEL = new Color32(199, 208, 220, 255), STEEL2 = new Color32(133, 145, 163, 255), STEEL3 = new Color32(86, 96, 114, 255);
        public static readonly Color32 PARCH = new Color32(241, 230, 200, 255), TAN = new Color32(224, 176, 112, 255), ORANGE = new Color32(255, 154, 60, 255);

        public static void Register(string name, Painter p) { painters[name] = p; }
        public static bool Has(string name) => painters.ContainsKey(name);

        public static Sprite Get(string name, Color32? tint = null)
        {
            if (name != null && name.StartsWith("icon:")) name = name.Substring(5);
            var t = tint ?? new Color32(255, 255, 255, 255);
            string key = name + "#" + t.r + "," + t.g + "," + t.b;
            if (cache.TryGetValue(key, out var s)) return s;
            var c = new PixelCanvas(16, 16);
            if (painters.TryGetValue(name ?? "", out var p)) p(c, t); else Unknown(c);
            c.Outline(OUT);
            s = c.ToSprite(16f);
            s.name = key; cache[key] = s; return s;
        }
        static void Unknown(PixelCanvas c) { c.Rect(3, 3, 10, 10, Color.magenta).Rect(6, 6, 4, 4, Color.black); }

        static Color32 Tint(Color32 t, Color32 fallback) => (t.r == 255 && t.g == 255 && t.b == 255) ? fallback : t;
        static Color32 Lt(Color32 c, float a = 0.45f) => C32.Lighten(c, a);
        static Color32 Dk(Color32 c, float a = 0.35f) => C32.Darken(c, a);

        static Icons()
        {
            // ---------- currencies ----------
            Register("coin", (c, t) => { c.Disc(8, 8, 6.2f, GOLD2).Disc(8, 8, 5.2f, GOLD).Ring(8, 8, 3.7f, 3f, GOLD2).Rect(7, 6, 2, 4, GOLD2).Rect(4, 10, 2, 2, GOLD3).Px(5, 9, GOLD3); });
            Register("gem", (c, t) => { c.Poly(CYAN2, 8, 14.5f, 13.5f, 10, 8, 1.5f, 2.5f, 10).Poly(CYAN, 8, 14, 12, 10, 8, 10, 4, 10).Poly(WH, 6, 12, 8, 13.5f, 7.5f, 11, 5.5f, 11).Line(3, 10, 13, 10, CYAN2).Poly(Lt(CYAN2, 0.2f), 8, 10, 13, 10, 8, 2.5f); });
            Register("flame", (c, t) => { c.Poly(RED, 8, 15, 11, 10.5f, 13, 6, 10.5f, 2, 5.5f, 2, 3, 6, 5, 10.5f).Poly(ORANGE, 8, 12, 10, 8.5f, 11, 6, 9.5f, 3, 6.5f, 3, 5, 6, 6.5f, 9).Poly(GOLD, 8, 8.5f, 9.5f, 6, 9, 4, 7, 3, 6.5f, 4.5f, 7, 7); });
            Register("star", (c, t) => { var col = Tint(t, GOLD); Star(c, col, 8, 8, 6.8f, 3f); Px(c, 6, 10, Lt(col, 0.7f)); });
            Register("star_off", (c, t) => { Star(c, STEEL3, 8, 8, 6.8f, 3f); });

            // ---------- stats ----------
            Register("hp", (c, t) => { c.Disc(5.2f, 10.5f, 3.4f, RED).Disc(10.8f, 10.5f, 3.4f, RED).Poly(RED, 1.8f, 9.3f, 14.2f, 9.3f, 8, 1.8f).Rect(4, 11, 2, 2, Lt(RED, 0.6f)); });
            Register("atk", (c, t) => Sword(c, ORANGE, true));
            Register("def", (c, t) => { c.Poly(BLUE, 2.5f, 13.5f, 13.5f, 13.5f, 13.5f, 8, 8, 1.5f, 2.5f, 8).Poly(Lt(BLUE, 0.4f), 4, 12, 8, 12, 8, 4, 4, 8).Rect(7, 4, 2, 8, WH).Rect(5, 8, 6, 2, WH); });
            Register("spd", (c, t) => { c.Poly(GREEN, 9.5f, 14.5f, 3.5f, 7, 7.5f, 7, 5.5f, 1.5f, 12.5f, 9, 8.5f, 9); });
            Register("crit", (c, t) => { c.Poly(GOLD, 8, 14.5f, 9.4f, 9.4f, 14.5f, 8, 9.4f, 6.6f, 8, 1.5f, 6.6f, 6.6f, 1.5f, 8, 6.6f, 9.4f).Rect(7, 7, 2, 2, WH); });

            // ---------- weapons / gear ----------
            Register("sword", (c, t) => Sword(c, Tint(t, STEEL), false));
            Register("spear", (c, t) => { var col = Tint(t, STEEL); c.Line(2, 2, 11, 11, BROWN, 2).Line(2, 3, 10, 11, Lt(BROWN, 0.3f)).Poly(col, 10, 8, 8, 10, 15, 15).Poly(Lt(col), 10.4f, 9.2f, 9.2f, 10.4f, 13, 13); });
            Register("bow", (c, t) => { var col = Tint(t, BROWN); for (int a = -68; a < 68; a += 6) { float r1 = a * Mathf.Deg2Rad, r2 = (a + 6) * Mathf.Deg2Rad; c.Line(Mathf.RoundToInt(4 + 8 * Mathf.Cos(r1)), Mathf.RoundToInt(8 + 7.2f * Mathf.Sin(r1)), Mathf.RoundToInt(4 + 8 * Mathf.Cos(r2)), Mathf.RoundToInt(8 + 7.2f * Mathf.Sin(r2)), col, 2); } c.Line(6, 14, 6, 2, STEEL).Line(4, 8, 13, 8, BROWN2).Poly(STEEL, 13, 8, 11, 10, 11, 6); });
            Register("staff", (c, t) => { var col = Tint(t, PURPLE); c.Line(2, 1, 10, 9, BROWN, 2).Line(2, 2, 9, 9, Lt(BROWN, 0.25f)).Rect(8, 8, 3, 3, GOLD2).Disc(11.5f, 11.5f, 3.4f, col).Disc(11, 12, 1.6f, Lt(col, 0.7f)); });
            Register("dagger", (c, t) => { var col = Tint(t, STEEL); c.Line(6, 6, 13, 13, col, 2).Line(7, 6, 13, 12, Lt(col)).Line(4, 8, 8, 4, GOLD2, 2).Line(2, 2, 5, 5, BROWN, 2).Px(1, 1, GOLD); });
            Register("plate", (c, t) => { var col = Tint(t, STEEL); c.Poly(col, 2, 13, 6, 14.5f, 8, 12.5f, 10, 14.5f, 14, 13, 14.5f, 8, 11, 7.5f, 11, 1.5f, 5, 1.5f, 5, 7.5f, 1.5f, 8).Poly(Dk(col), 6.5f, 14.5f, 8, 12.5f, 9.5f, 14.5f).Rect(5, 6, 6, 1, GOLD2).Rect(6, 8, 1, 4, Lt(col)).Rect(9, 3, 1, 3, Dk(col)); });
            Register("robe", (c, t) => { var col = Tint(t, BLUE); c.Poly(col, 5.5f, 14.5f, 10.5f, 14.5f, 13.5f, 12, 14.5f, 4.5f, 11.5f, 6, 11.5f, 1.5f, 4.5f, 1.5f, 4.5f, 6, 1.5f, 4.5f, 2.5f, 12).Poly(Dk(col, 0.5f), 5.5f, 14.5f, 10.5f, 14.5f, 8, 10).Rect(4, 5, 8, 1, GOLD2).Rect(7, 2, 1, 3, Lt(col)); });
            Register("ring", (c, t) => { var col = Tint(t, CYAN); c.Ring(8, 6.5f, 5.4f, 3.4f, GOLD).Rect(5, 9, 2, 2, GOLD3).Disc(8, 12, 2.7f, col).Rect(7, 12, 1, 1, WH); });
            Register("amulet", (c, t) => { var col = Tint(t, RED); c.Line(2, 14, 7, 8, STEEL2).Line(14, 14, 9, 8, STEEL2).Disc(8, 5.5f, 4.3f, GOLD2).Disc(8, 5.5f, 3f, col).Rect(7, 6, 1, 2, WH); });

            // ---------- materials ----------
            Register("ore", (c, t) => { c.Poly(STEEL2, 2, 4, 4, 10, 9, 12.5f, 13, 9, 14, 4, 8, 1.5f).Poly(STEEL, 4, 10, 9, 12.5f, 8, 6).Poly(STEEL3, 8, 6, 14, 4, 8, 1.5f, 2, 4).Rect(10, 8, 2, 2, ORANGE).Rect(5, 4, 2, 1, ORANGE); });
            Register("essence", (c, t) => { c.Poly(PURPLE, 8, 13.5f, 11.5f, 8, 8, 2.5f, 4.5f, 8).Poly(Lt(PURPLE, 0.5f), 8, 13.5f, 8, 8, 4.5f, 8).Px(13, 13, WH).Px(13, 12, WH).Px(13, 14, WH).Px(12, 13, WH).Px(14, 13, WH).Px(3, 12, WH).Px(3, 11, WH).Px(3, 13, WH).Px(2, 12, WH).Px(4, 12, WH); });
            Register("seal", (c, t) => { c.Disc(8, 8, 6.4f, PURPLE2).Disc(8, 8, 5.2f, PURPLE).Poly(GOLD, 8, 13, 12, 5.5f, 4, 5.5f).Poly(GOLD, 8, 3, 12, 10.5f, 4, 10.5f).Disc(8, 8, 1.8f, PURPLE2); });

            // ---------- nav / ui ----------
            Register("hall", (c, t) => { c.Rect(3, 2, 10, 7, TAN).Poly(RED, 1, 8, 8, 14.5f, 15, 8).Rect(7, 2, 3, 5, BROWN2).Rect(4, 5, 2, 2, CYAN).Rect(11, 5, 1, 2, CYAN).Rect(10, 11, 2, 3, STEEL2); });
            Register("heroes", (c, t) => { c.Poly(STEEL, 2, 2, 2, 9, 5, 13.5f, 11, 13.5f, 14, 9, 14, 2).Rect(4, 6, 8, 2, STEEL3).Rect(7, 2, 2, 5, STEEL2).Poly(RED, 8, 13, 11, 15, 14, 13).Rect(4, 9, 2, 3, Lt(STEEL, 0.5f)); });
            Register("board", (c, t) => { c.Rect(3, 1, 2, 6, BROWN2).Rect(11, 1, 2, 6, BROWN2).Rect(1, 6, 14, 8, BROWN).Rect(2, 7, 12, 6, Lt(BROWN, 0.2f)).Rect(3, 8, 4, 4, PARCH).Rect(8, 9, 4, 3, PARCH).Px(4, 11, RED).Px(10, 11, RED); });
            Register("bag", (c, t) => { c.Poly(BROWN, 3, 1.5f, 1.5f, 8, 3.5f, 12.5f, 12.5f, 12.5f, 14.5f, 8, 13, 1.5f).Rect(3, 8, 10, 3, BROWN2).Rect(7, 6, 2, 4, GOLD).Line(6, 13, 6, 15, BROWN2).Line(10, 13, 10, 15, BROWN2).Line(6, 15, 10, 15, BROWN2).Rect(4, 3, 2, 3, Lt(BROWN, 0.25f)); });
            Register("quest", (c, t) => { c.Rect(3, 2, 10, 12, PARCH).Rect(2, 12, 12, 2, TAN).Rect(2, 2, 12, 2, TAN).Rect(5, 9, 6, 1, BROWN).Rect(5, 7, 4, 1, BROWN).Rect(11, 5, 2, 3, RED).Rect(11, 4, 2, 1, RED); });
            Register("menu", (c, t) => { c.Rect(2, 3, 12, 2, WH).Rect(2, 7, 12, 2, WH).Rect(2, 11, 12, 2, WH); });
            Register("gear", (c, t) => { c.Disc(8, 8, 5.3f, STEEL); for (int i = 0; i < 8; i++) { float a = i * Mathf.PI / 4; c.Rect(Mathf.RoundToInt(8 + 6 * Mathf.Cos(a)) - 1, Mathf.RoundToInt(8 + 6 * Mathf.Sin(a)) - 1, 2, 2, STEEL); } c.Disc(8, 8, 2.3f, STEEL3).Disc(8, 8, 1.2f, new Color32(0, 0, 0, 0)); });
            Register("share", (c, t) => { c.Line(4, 8, 12, 4, WH).Line(4, 8, 12, 12, WH).Disc(4, 8, 2.6f, CYAN).Disc(12, 4, 2.6f, CYAN).Disc(12, 12, 2.6f, CYAN); });
            Register("info", (c, t) => { c.Disc(8, 8, 6.6f, BLUE).Rect(7, 4, 2, 5, WH).Rect(7, 10, 2, 2, WH); });
            Register("lock", (c, t) => { c.Ring(8, 9, 4, 2.4f, STEEL).Rect(3, 2, 10, 8, GOLD).Rect(7, 4, 2, 3, BROWN2).Rect(4, 8, 8, 1, GOLD3); });
            Register("check", (c, t) => { c.Line(3, 8, 6, 4, GREEN, 2).Line(6, 4, 13, 12, GREEN, 2); });
            Register("cross", (c, t) => { c.Line(3, 3, 12, 12, RED, 2).Line(3, 12, 12, 3, RED, 2); });
            Register("plus", (c, t) => { c.Rect(7, 3, 2, 10, GREEN).Rect(3, 7, 10, 2, GREEN); });
            Register("minus", (c, t) => { c.Rect(3, 7, 10, 2, RED); });
            Register("arrow_r", (c, t) => { c.Poly(WH, 2, 6, 8, 6, 8, 3, 14, 8, 8, 13, 8, 10, 2, 10); });
            Register("arrow_l", (c, t) => { c.Poly(WH, 14, 6, 8, 6, 8, 3, 2, 8, 8, 13, 8, 10, 14, 10); });
            Register("arrow_u", (c, t) => { c.Poly(WH, 6, 2, 6, 8, 3, 8, 8, 14, 13, 8, 10, 8, 10, 2); });
            Register("arrow_d", (c, t) => { c.Poly(WH, 6, 14, 6, 8, 3, 8, 8, 2, 13, 8, 10, 8, 10, 14); });
            Register("clock", (c, t) => { c.Disc(8, 8, 6.4f, WH).Disc(8, 8, 5.2f, PARCH).Line(8, 8, 8, 12, OUT).Line(8, 8, 11, 6, OUT); });
            Register("speaker", (c, t) => { c.Poly(WH, 2, 6, 5, 6, 9, 3, 9, 13, 5, 10, 2, 10).Line(11, 6, 11, 10, WH).Line(13, 4, 13, 12, WH); });
            Register("note", (c, t) => { c.Disc(5, 4, 2.6f, WH).Disc(11, 3, 2.6f, WH).Rect(6, 4, 1, 9, WH).Rect(12, 3, 1, 9, WH).Rect(6, 12, 7, 2, WH); });
            Register("globe", (c, t) => { c.Disc(8, 8, 6.4f, BLUE).Poly(GREEN, 4, 12, 8, 13, 9, 9, 6, 8, 3, 9).Poly(GREEN, 9, 6, 13, 7, 12, 3, 9, 3).Ring(8, 8, 6.4f, 5.4f, CYAN); });
            Register("trash", (c, t) => { c.Rect(4, 2, 8, 9, STEEL).Rect(3, 11, 10, 2, STEEL2).Rect(6, 13, 4, 1, STEEL2).Rect(6, 4, 1, 6, STEEL3).Rect(9, 4, 1, 6, STEEL3); });
            Register("pin", (c, t) => { c.Disc(8, 10, 4.5f, RED).Poly(RED, 4.5f, 8, 11.5f, 8, 8, 1).Disc(8, 10, 1.8f, WH); });
            Register("heart_off", (c, t) => { c.Disc(5.2f, 10.5f, 3.4f, STEEL3).Disc(10.8f, 10.5f, 3.4f, STEEL3).Poly(STEEL3, 1.8f, 9.3f, 14.2f, 9.3f, 8, 1.8f); });
            Register("skull", (c, t) => { c.Disc(8, 9, 5.5f, WH).Rect(5, 2, 6, 4, WH).Rect(4, 8, 3, 3, OUT).Rect(9, 8, 3, 3, OUT).Rect(7, 5, 2, 1, OUT).Rect(6, 2, 1, 2, OUT).Rect(9, 2, 1, 2, OUT); });
            Register("scroll", (c, t) => { c.Rect(3, 2, 10, 12, PARCH).Rect(2, 12, 12, 2, TAN).Rect(2, 2, 12, 2, TAN).Rect(5, 9, 6, 1, BROWN).Rect(5, 7, 6, 1, BROWN).Rect(5, 5, 4, 1, BROWN); });
            Register("hammer", (c, t) => { c.Line(3, 2, 11, 10, BROWN, 2).Rect(8, 10, 6, 4, STEEL).Rect(8, 13, 6, 1, Lt(STEEL, 0.5f)).Rect(8, 10, 6, 1, STEEL3); });
            Register("potion", (c, t) => { var col = Tint(t, RED); c.Rect(6, 11, 4, 3, BROWN).Rect(6, 9, 4, 2, STEEL).Poly(STEEL, 6, 9, 10, 9, 14, 3, 14, 2, 2, 2, 2, 3).Poly(col, 3, 7, 13, 7, 13.5f, 3, 12, 2.2f, 4, 2.2f, 2.5f, 3).Rect(4, 8, 1, 4, WH); });
        }

        static void Px(PixelCanvas c, int x, int y, Color32 col) { c.Px(x, y, col); }
        static void Star(PixelCanvas c, Color32 col, float cx, float cy, float ro, float ri)
        {
            var pts = new float[20];
            for (int i = 0; i < 10; i++) { float a = Mathf.PI / 2 + i * Mathf.PI / 5; float r = i % 2 == 0 ? ro : ri; pts[i * 2] = cx + r * Mathf.Cos(a); pts[i * 2 + 1] = cy + r * Mathf.Sin(a) - 0.4f; }
            c.Poly(col, pts);
        }
        static void Sword(PixelCanvas c, Color32 col, bool small)
        {
            c.Line(6, 6, 13, 13, col, 2).Line(7, 6, 13, 12, Lt(col, 0.6f)).Px(14, 14, col).Px(14, 13, col).Px(13, 14, col)
             .Line(3, 7, 7, 3, GOLD2, 2).Line(1, 1, 4, 4, BROWN, 2).Px(1, 1, GOLD);
        }
    }
}
