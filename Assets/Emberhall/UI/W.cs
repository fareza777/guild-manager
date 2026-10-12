using System;
using System.Collections.Generic;
using System.Linq;
using ManagerKit;
using ManagerKit.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Emberhall
{
    /// <summary>Game-specific reusable widgets (cards, chips, rows).</summary>
    public static class W
    {
        public static GameContext C => App.Ctx;

        // ---------- text helpers ----------
        public static string Cap(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0]) + s.Substring(1);
        public static string Col(Color c, string s) => $"<color=#{ColorUtility.ToHtmlStringRGB(c)}>{s}</color>";
        public static Color Ink(Color color) => Color.Lerp(color, Theme.TextDark, 0.38f);
        public static string MissionName(string name)
        {
            foreach (string prefix in new[] { "Safe Route: ", "Dangerous: " })
                if (name != null && name.StartsWith(prefix, StringComparison.Ordinal)) return L.T(prefix) + L.T(name.Substring(prefix.Length));
            return L.T(name);
        }

        public static void PageBackdrop(Transform parent)
        {
            U.Img(parent, Boxes.Gradient(Theme.Hex("#d8edf0"), Theme.Hex("#f3e6c6")), Color.white, "page_backdrop").Fill();
            var glow = U.Img(parent, Boxes.Glow, new Color(1f, 0.9f, 0.63f, 0.3f), "warmth");
            glow.At(0.5f, 1, 0, -120, 1400, 900);
            U.Img(parent, Boxes.Vignette(), new Color(0.48f, 0.39f, 0.19f, 0.08f), "vignette").Fill();
        }

        // ---------- resources ----------
        public static RectTransform ResChip(Transform parent, string id, double n, int fs = 34, bool red = false, float icon = 44)
        {
            fs = Mathf.Max(28, fs); icon = Mathf.Max(40f, icon);
            var row = U.Rt(parent, "res"); U.HBox(row, 6, null, TextAnchor.MiddleCenter, true, true, false, false);
            U.Icon(row, Art.ResIcon(C, id), icon).Le(icon, icon);
            var t = U.Txt(row, Util.Num(n), fs, red ? Theme.Bad : Theme.Text, TextAnchor.MiddleCenter, true); t.Le(t.preferredWidth + 8, fs + 10);
            row.Le(icon + 6 + t.preferredWidth + 6, icon);
            return row;
        }

        /// <summary>Row of cost chips; chips are red when unaffordable. Returns the row.</summary>
        public static RectTransform CostRow(Transform parent, IEnumerable<Res> cost, int fs = 32, float icon = 40, TextAnchor align = TextAnchor.MiddleCenter)
        {
            var row = U.Rt(parent, "cost"); U.HBox(row, 18, null, align, true, true, false, false);
            float w = 0; int n = 0;
            foreach (var r in cost) { var chip = ResChip(row, r.id, r.n, fs, !C.Has(r), icon); w += chip.GetComponent<LayoutElement>().preferredWidth + 18; n++; }
            row.Le(w, icon + 4); return row;
        }

        public static RectTransform RewardRow(Transform parent, IEnumerable<Res> rewards, float size = 78, int fs = 28)
        {
            fs = Mathf.Max(30, fs);
            var row = U.Rt(parent, "rewards"); U.HBox(row, 14, null, TextAnchor.MiddleCenter, true, true, false, false);
            float w = 0;
            foreach (var r in rewards)
            {
                var cell = RewardCell(row, r.id, r.n, size, fs); w += size + 14;
            }
            row.Le(Mathf.Max(0f, w - 14f), size + Mathf.Max(36f, fs + 10f) + 8f); return row;
        }
        public static RectTransform RewardCell(Transform parent, string id, double n, float size = 78, int fs = 28)
        {
            fs = Mathf.Max(28, fs); float labelH = Mathf.Max(36f, fs + 10f);
            var box = U.Rt(parent, "rw"); box.Le(size, size + labelH + 8f);
            var bg = U.Box(box, Theme.Inset, Theme.Black, "bg"); bg.At(0.5f, 1, 0, 0, size, size);
            var ic = U.Icon(bg.transform, Art.ResIcon(C, id), size - 16); ic.At(0.5f, 0.5f, 0, 0, size - 16, size - 16);
            var t = U.Txt(box, (id == "fame" ? "+" : "x") + Util.Num(n), fs, Theme.Text, TextAnchor.MiddleCenter, true); t.StretchX(0, 0, labelH, 0, 0);
            return box;
        }

        // ---------- stars ----------
        public static RectTransform Stars(Transform parent, int n, int max = 3, float size = 44)
        {
            var row = U.Rt(parent, "stars"); U.HBox(row, 2, null, TextAnchor.MiddleCenter, true, true, false, false);
            for (int i = 0; i < max; i++) U.Icon(row, Icons.Get(i < n ? "star" : "star_off"), size).Le(size, size);
            row.Le(max * (size + 2), size); return row;
        }

        // ---------- portraits ----------
        /// <summary>Animated unit portrait in a box of given height (feet anchored at bottom centre).</summary>
        public static Image Portrait(Transform parent, string spriteKey, float height, bool flip = false, string name = "portrait")
        {
            var holder = U.Rt(parent, name); holder.Sz(height * 0.9f, height); holder.Le(height * 0.9f, height);
            var img = U.Img(holder, null, Color.white, "unit"); img.At(0.5f, 0, 0, 0, 10, 10, 0.5f, 0);
            float scale;
            if (spriteKey != null && spriteKey.StartsWith("chara_")) scale = height / 20f;
            else if (spriteKey != null && spriteKey.StartsWith("mon:")) scale = height / 30f;
            else { var s = Art.Get(spriteKey); scale = s != null ? height / s.rect.height : 1; }
            var frames = Art.FrontFrames(spriteKey);
            SpriteAnim.On(img, frames, 5, true, scale);
            if (flip) img.rectTransform.localScale = new Vector3(-1, 1, 1);
            return img;
        }
        public static Image PortraitFor(Transform parent, UnitInstance u, float height) => Portrait(parent, u.sprite, height * (u.monster ? 1.0f : 1f));

        // ---------- hero & item cells ----------
        public static Image RarityBox(Transform parent, string rarity, bool dark = true)
        {
            var rc = Art.RarityColor(C, rarity);
            return U.Box(parent, Color.Lerp(Theme.Inset, rc, dark ? 0.18f : 0.4f), rc, "rarity", true, 0.15f);
        }

        public static UButton ItemCell(Transform parent, ItemInstance g, float size, Action onClick, bool showOwner = true)
        {
            var def = C.Content.Item(g.defId); var rc = Art.RarityColor(C, g.rarity);
            var box = U.Box(parent, Color.Lerp(Theme.Inset, rc, 0.22f), rc, "item", true, 0.15f); box.Sz(size, size); box.Le(size, size);
            box.gameObject.AddComponent<RectMask2D>();
            U.Img(box.transform, Boxes.Glow, new Color(rc.r, rc.g, rc.b, 0.18f), "relic_glow").Fill(12, 12, 12, 12);
            var b = box.gameObject.AddComponent<UButton>(); b.bg = box; b.noTint = true; b.onClick = onClick;
            var ic = U.Icon(box.transform, Art.ItemIcon(def), size * 0.68f); ic.At(0.5f, 0.5f, 0, 2, size * 0.68f, size * 0.68f);
            if (g.enh > 0) { var t = U.Txt(box.transform, "+" + g.enh, (int)(size * 0.26f), Theme.Gold, TextAnchor.UpperLeft, true); t.At(0, 1, 8, -4, size * 0.5f, size * 0.3f, 0, 1); }
            var tt = U.Txt(box.transform, "T" + def.tier, (int)(size * 0.22f), Theme.TextDim, TextAnchor.LowerRight, true); tt.At(1, 0, -8, 4, size * 0.5f, size * 0.3f, 1, 0);
            if (showOwner && !string.IsNullOrEmpty(g.owner)) { var e = U.Chip(box.transform, "E", Theme.Green, (int)(size * 0.22f), Color.white, 8, size * 0.28f); e.At(1, 1, -4, -4, size * 0.3f, size * 0.28f, 1, 1); }
            if (g.isNew) { var n = U.Dot(box.transform, Theme.Red, size * 0.22f); n.At(0, 0, 8, 8, size * 0.22f, size * 0.22f, 0, 0); }
            if (g.locked) { var l = U.Icon(box.transform, Icons.Get("lock"), size * 0.3f); l.At(0, 0, 4, 4, size * 0.3f, size * 0.3f, 0, 0); }
            return b;
        }

        public static UButton StackCell(Transform parent, string id, int n, float size, Action onClick)
        {
            var def = C.Content.Item(id);
            var box = U.Box(parent, Theme.Inset, Theme.Black, "stack", true, 0.15f); box.Sz(size, size); box.Le(size, size);
            box.gameObject.AddComponent<RectMask2D>();
            var b = box.gameObject.AddComponent<UButton>(); b.bg = box; b.noTint = true; b.onClick = onClick;
            var ic = U.Icon(box.transform, Art.ItemIcon(def), size * 0.68f); ic.At(0.5f, 0.5f, 0, 2, size * 0.68f, size * 0.68f);
            var t = U.Txt(box.transform, "x" + n, (int)(size * 0.27f), Theme.Text, TextAnchor.LowerRight, true); t.At(1, 0, -8, 4, size * 0.7f, size * 0.32f, 1, 0);
            return b;
        }

        public static Image ClassBadge(Transform parent, string classId, float size = 44)
        {
            var cls = C.Content.Class(classId); var col = Theme.Hex(cls?.color ?? "#ffffff");
            var bg = U.Box(parent, Color.Lerp(Theme.Inset, col, 0.35f), col, "cls"); bg.Sz(size, size);
            var ic = U.Icon(bg.transform, Icons.Get(cls?.icon ?? "icon:info"), size - 10); ic.At(0.5f, 0.5f, 0, 0, size - 10, size - 10);
            bg.Le(size, size); return bg;
        }

        /// <summary>Status text for a unit: "Ready", "Training", "Wounded"...</summary>
        public static (string text, Color color) UnitStatus(UnitInstance u)
        {
            var t = C.Timers.UnitTask(u.uid);
            if (t != null)
            {
                string s = t.kind == "train" ? "Training" : t.kind == "heal" ? "Healing" : t.kind == "expedition" ? "Expedition" : "Busy";
                return (L.T(s), Theme.Blue);
            }
            double f = C.Roster.HpFrac(u);
            if (f < C.Content.Bal("ready.hp", 0.25)) return (L.T(f <= 0.001 ? "Wounded" : "Needs rest"), Theme.Bad);
            if (f < 0.999) return (L.T("Hurt"), Theme.Warn);
            return (L.T("Ready"), Theme.Good);
        }

        public static UButton HeroCard(Transform parent, UnitInstance u, float w, float h, Action onClick, bool compact = false)
        {
            var rc = Art.RarityColor(C, u.rarity);
            Color body = Color.Lerp(Theme.PanelDark, rc, 0.4f);
            var plate = LuxArt.Button(body);
            var box = plate != null ? U.Img(parent, plate, Color.white, "hero", true) : U.Box(parent, Theme.PanelDark, Color.Lerp(Theme.Black, rc, 0.55f), "hero", true);
            box.Sz(w, h); box.Le(w, h);
            var b = box.gameObject.AddComponent<UButton>(); b.bg = box; b.noTint = plate == null; b.baseColor = body; b.ornate = plate != null; b.plateW = w; b.plateH = h; b.onClick = onClick;
            LuxArt.Raise(box, 8f, 0.4f);
            var back = U.Box(box.transform, Color.Lerp(Theme.Inset, rc, 0.2f), null, "pback"); back.At(0, 0.5f, 10, 0, h - 20, h - 20);
            var pic = Portrait(back.transform, u.sprite, (h - 20) * 0.7f); pic.transform.parent.GetComponent<RectTransform>().At(0.5f, 0, 0, 8, (h - 20) * 0.63f, (h - 20) * 0.7f, 0.5f, 0);
            float x0 = h - 6;
            var name = U.Txt(box.transform, u.name, compact ? 32 : 36, Theme.Text, TextAnchor.MiddleCenter, true); name.StretchX(1, -14, 48, x0 + 6, 18);
            var cls = ClassBadge(box.transform, u.classId, 44); cls.At(0, 1, x0 + 6, -58, 44, 44, 0, 1);
            var lv = U.Txt(box.transform, $"Lv {u.level}" + (u.promo > 0 ? " " + W.Col(Theme.Gold, new string('*', u.promo)) : ""), 30, Theme.Gold, TextAnchor.MiddleLeft, true); lv.StretchX(1, -66, 42, x0 + 58, 18);
            var bar = U.ProgressBar(box.transform, Theme.Green, w - x0 - 24, 26);
            bar.Value = (float)C.Roster.HpFrac(u); bar.fill.sprite = Boxes.Get(bar.Value < 0.3f ? Theme.Red : bar.Value < 0.65f ? Theme.Warn : Theme.Green, null, 0.35f);
            bar.back.R().At(0, 0, x0 + 6, 54, w - x0 - 24, 26, 0, 0);
            var (st, sc) = UnitStatus(u);
            var stt = U.Txt(box.transform, st, 28, sc, TextAnchor.MiddleCenter, true); stt.StretchX(0, 14, 38, x0 + 6, 18);
            return b;
        }

        public static RectTransform Header(Transform parent, string text, Sprite icon = null)
        {
            var row = U.Rt(parent, "header"); row.Le(-1, 64, 1);
            var t = U.Txt(row, text, 40, Theme.Gold, TextAnchor.MiddleCenter, true); t.StretchX(0.5f, 0, 54, 128, 128);
            if (icon != null) U.Icon(row, icon, 46).At(0, 0.5f, 64, 0, 46, 46, 0.5f, 0.5f);
            else if (LuxArt.Filigree != null) { var left = U.Img(row, LuxArt.Filigree, new Color(1, 1, 1, 0.5f), "left_ornament"); left.preserveAspect = true; left.At(0, 0.5f, 64, 0, 104, 32, 0.5f, 0.5f); }
            if (LuxArt.Filigree != null) { var line = U.Img(row, LuxArt.Filigree, new Color(1, 1, 1, 0.5f), "right_ornament"); line.preserveAspect = true; line.At(1, 0.5f, -64, 0, 104, 32, 0.5f, 0.5f); }
            return row;
        }

        public static Image Card(Transform parent, float h = -1, Color? fill = null, Color? border = null)
        {
            var c = U.Box(parent, fill ?? Theme.Panel, border ?? Theme.Black, "card", false);
            LuxArt.Raise(c, 6f, 0.32f);
            if (h > 0) c.Le(-1, h, 1); return c;
        }

        public static Text Info(Transform parent, string text, int fs = 32, Color? col = null, TextAnchor a = TextAnchor.MiddleCenter)
        {
            var t = U.Txt(parent, text, fs, col ?? Theme.TextDim, a, false, true); t.Le(-1, -1, 1); return t;
        }

        public static UButton TabBar(Transform parent, string[] labels, int sel, Action<int> onSel, float h = 90)
        {
            var row = U.Rt(parent, "tabs"); row.Le(-1, h, 1); U.HBox(row, 8, new RectOffset(12, 12, 0, 0), TextAnchor.MiddleCenter, true, true, true, true);
            for (int i = 0; i < labels.Length; i++)
            {
                int k = i; var b = U.Btn(row, L.T(labels[i]), i == sel ? Theme.Accent : Theme.PanelLight, () => onSel(k), 200, h, 38); b.Le(-1, h, 1);
            }
            return null;
        }
    }
}
