using System;
using System.Collections.Generic;
using ManagerKit;
using ManagerKit.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Emberhall
{
    /// <summary>
    /// Sprite catalogue. All art comes from the shared pixel asset library (copied to Resources/Art by Tools/import_assets.ps1);
    /// sheets are sliced at runtime so no .meta slicing has to be maintained.
    /// Keys used in content: chara_N | bat:Name | mon:01_0 | bg:Name | prop:name | icon:name | anim:name
    /// </summary>
    public static class Art
    {
        static readonly Dictionary<string, Texture2D> tex = new Dictionary<string, Texture2D>();
        static readonly Dictionary<string, Sprite> spr = new Dictionary<string, Sprite>();
        static readonly Dictionary<string, float> footCache = new Dictionary<string, float>();

        public static Texture2D Tex(string path)
        {
            if (tex.TryGetValue(path, out var t)) return t;
            t = Resources.Load<Texture2D>("Art/" + path);
            if (t != null) { t.filterMode = FilterMode.Point; t.wrapMode = TextureWrapMode.Clamp; }
            tex[path] = t; return t;
        }

        static Sprite Slice(string cacheKey, string path, int x, int yTop, int w, int h, Vector2 pivot)
        {
            if (spr.TryGetValue(cacheKey, out var s)) return s;
            var t = Tex(path); if (t == null) { spr[cacheKey] = null; return null; }
            int y = t.height - yTop - h;                      // sheets are described top-down
            s = Sprite.Create(t, new Rect(x, y, w, h), pivot, 100f, 0, SpriteMeshType.FullRect);
            s.name = cacheKey; spr[cacheKey] = s; return s;
        }

        // ---- characters (16x20 walk sheets, rows: down, left, right, up) ----
        public static int SkinIndex(string key) { if (key != null && key.StartsWith("chara_") && int.TryParse(key.Substring(6), out var n)) return Mathf.Clamp(n, 0, 31); return 0; }
        public static Sprite Chara(int skin, int dir, int frame) => Slice($"ch{skin}_{dir}_{frame}", $"chara/chara_{skin}", frame * 16, dir * 20, 16, 20, new Vector2(0.5f, 0f));
        public static readonly int[] WalkSeq = { 0, 1, 2, 1 };

        // ---- attack animation frames (32x32, rows: down, left, right, up; 3 frames) ----
        public static Sprite Arpg(string anim, int skin, int row, int col)
            => Slice($"ar_{anim}{skin}_{row}_{col}", $"arpg/{anim}_c{skin}", col * 32, row * 32, 32, 32, new Vector2(14f / 32f, 11f / 32f));

        // ---- monster sheets (48x48 frames, 3 cols x 4 rows) ----
        public static Sprite Monster(string sheet, int row, int col)
        {
            string path = sheet.StartsWith("ADD") ? $"monster/Additional_bonus_Monsters_front_anim_only_{sheet.Substring(4)}" : $"monster/Monsters_{sheet}";
            string fk = path + "#" + row;
            if (!footCache.TryGetValue(fk, out var foot))
            {
                foot = 0; var t = Tex(path);
                if (t != null)
                {
                    var px = t.GetPixels32(); int minY = 47;
                    for (int c = 0; c < 3; c++) for (int y = 0; y < 48; y++) for (int x = 0; x < 48; x++)
                            {
                                int py = t.height - (row * 48 + 48) + y;           // bottom-up
                                if (px[py * t.width + c * 48 + x].a > 20 && y < minY) minY = y;
                            }
                    foot = minY / 48f;
                }
                footCache[fk] = foot;
            }
            return Slice($"mo_{sheet}_{row}_{col}", path, col * 48, row * 48, 48, 48, new Vector2(0.5f, foot));
        }

        public static Sprite Battler(string name)
        {
            if (spr.TryGetValue("bt_" + name, out var s)) return s;
            var t = Tex("battler/" + name); if (t == null) { spr["bt_" + name] = null; return null; }
            s = Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0f), 100f, 0, SpriteMeshType.FullRect); spr["bt_" + name] = s; return s;
        }
        public static Sprite Whole(string path, Vector2? pivot = null)
        {
            if (spr.TryGetValue("w_" + path, out var s)) return s;
            var t = Tex(path); if (t == null) { spr["w_" + path] = null; return null; }
            s = Sprite.Create(t, new Rect(0, 0, t.width, t.height), pivot ?? new Vector2(0.5f, 0f), 100f, 0, SpriteMeshType.FullRect); spr["w_" + path] = s; return s;
        }
        public static Sprite Bg(string name) => Whole("bg/" + name, new Vector2(0.5f, 0.5f));
        public static Sprite Prop(string name) => Whole("props/" + name);
        /// <summary>Frame of a horizontal strip (fireplace / torch): w x h per frame.</summary>
        public static Sprite Strip(string path, int frame, int w, int h, Vector2? pivot = null) => Slice($"st_{path}_{frame}", path, frame * w, 0, w, h, pivot ?? new Vector2(0.5f, 0f));
        public static Sprite ChestFrame(int n, int frame) => Slice($"chest{n}_{frame}", $"anim/chest_0{n}_16x32", frame * 18, 0, 18, 32, new Vector2(0.5f, 0f));
        public static Sprite FarmIcon(int sheet, int col, int row) => Slice($"farm{sheet}_{col}_{row}", $"anim/farm_icons_{sheet}_16x16", col * 16, row * 16, 16, 16, new Vector2(0.5f, 0.5f));

        /// <summary>Resolve any content sprite key.</summary>
        public static Sprite Get(string key, Color32? tint = null)
        {
            if (string.IsNullOrEmpty(key)) return Icons.Get("info");
            if (key.StartsWith("icon:")) return Icons.Get(key.Substring(5), tint);
            if (key.StartsWith("prop:")) return Prop(key.Substring(5)) ?? Icons.Get("info");
            if (key.StartsWith("bg:")) return Bg(key.Substring(3));
            if (key.StartsWith("bat:")) return Battler(key.Substring(4));
            if (key.StartsWith("mon:")) { var sh = key.Substring(4); return Monster(sh, sh.StartsWith("ADD") ? 0 : 1, 1); }
            if (key.StartsWith("chara_")) return Chara(SkinIndex(key), 0, 1);
            return Icons.Get(key, tint);
        }

        public static Sprite ItemIcon(ItemDef d)
        {
            if (d == null) return Icons.Get("info");
            int art = d.family == "sword" ? 0 : d.family == "bow" ? 6 : d.family == "spear" ? 7 : d.family == "staff" ? 8 : d.family == "dagger" ? 9 :
                d.type == "armor" ? d.icon == "icon:robe" ? 13 : d.family == "light" ? 12 : 1 : d.family == "ring" ? 2 : d.id.StartsWith("potion_") ? 3 :
                d.id == "iron_ore" ? 4 : d.id.StartsWith("tome_") || d.id == "fame_scroll" || d.icon == "icon:book" ? 5 : d.icon == "icon:shield" ? 10 : d.family == "boots" ? 11 :
                d.family == "amulet" ? 14 : d.id == "essence" ? 15 : d.id == "soul_seal" ? 16 : d.id == "coin_bag" ? 17 : -1;
            if (art >= 0) { var painted = GeneratedArt.Item(art); if (painted) return painted; }
            Color32? tint = null; if (!string.IsNullOrEmpty(d.tint)) { ColorUtility.TryParseHtmlString(d.tint, out var c); tint = c; }
            return Get(d.icon, tint);
        }
        public static Sprite ResIcon(GameContext c, string id)
        {
            if (id == "fame") return Icons.Get("flame");
            if (c.Content.CurrencyMap.TryGetValue(id, out var cur)) return Get(cur.icon);
            return ItemIcon(c.Content.Item(id));
        }
        public static Color RarityColor(GameContext c, string id) => Theme.Hex(c.Content.Rarity(id)?.color ?? "#C7CBD1");

        // ---------------------------------------------------------------------------------
        /// <summary>Idle animation frames + facing for a unit (hero or enemy) as shown in lists / hall.</summary>
        public static Sprite[] FrontFrames(string spriteKey)
        {
            if (spriteKey == null) return new Sprite[0];
            if (spriteKey.StartsWith("chara_")) { int s = SkinIndex(spriteKey); return new[] { Chara(s, 0, 0), Chara(s, 0, 1), Chara(s, 0, 2), Chara(s, 0, 1) }; }
            if (spriteKey.StartsWith("mon:")) { var sh = spriteKey.Substring(4); int row = sh.StartsWith("ADD") ? 0 : 0; return new[] { Monster(sh, row, 0), Monster(sh, row, 1), Monster(sh, row, 2), Monster(sh, row, 1) }; }
            return new[] { Get(spriteKey) };
        }
        /// <summary>Native pixel scale that makes this sprite type look roughly the same on screen.</summary>
        public static float NativeScale(string key, float baseScale) => key != null && key.StartsWith("bat:") ? baseScale * 0.55f : baseScale;
    }

    /// <summary>Plays a Sprite[] on an Image. Used for every animated pixel sprite in the UI. With scale>0 it also sizes/pivots the Image per frame (feet-anchored).</summary>
    public class SpriteAnim : MonoBehaviour
    {
        public Image img; public Sprite[] frames; public float fps = 6; public bool loop = true, playing = true; public Action onEnd; public float scale;
        float t; int idx;
        public static SpriteAnim On(Image i, Sprite[] f, float fps = 6, bool loop = true, float scale = 0) { var a = i.GetComponent<SpriteAnim>() ?? i.gameObject.AddComponent<SpriteAnim>(); a.img = i; a.scale = scale; a.Play(f, fps, loop); return a; }
        public void Play(Sprite[] f, float rate = 6, bool lp = true, Action end = null)
        {
            frames = f; fps = rate; loop = lp; onEnd = end; t = 0; idx = 0; playing = true;
            if (f != null && f.Length > 0 && img) Show(f[0]);
        }
        public void Show(Sprite s)
        {
            if (!img || s == null) return;
            img.sprite = s;
            if (scale > 0) { var r = img.rectTransform; r.pivot = new Vector2(s.pivot.x / s.rect.width, s.pivot.y / s.rect.height); r.sizeDelta = s.rect.size * scale; }
        }
        void Update()
        {
            if (!playing || frames == null || frames.Length <= 1 || img == null) return;
            t += Time.unscaledDeltaTime * fps;
            if (float.IsNaN(t) || float.IsInfinity(t)) t = 0f;
            int len = frames.Length;
            if (loop) t %= len;
            int n = (int)t;
            if (n < 0 || n >= len) { if (!loop) { playing = false; onEnd?.Invoke(); } return; }
            if (n != idx && frames[n] != null) { idx = n; Show(frames[n]); }
        }
    }
}
