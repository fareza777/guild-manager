using System;
using System.Collections.Generic;
using System.Linq;
using ManagerKit;
using ManagerKit.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Emberhall
{
    public class HeroesScreen : UiScreen
    {
        static int sort;   // 0 level, 1 power, 2 rarity, 3 class
        GameContext C => App.Ctx;
        public override string Id => "Heroes";
        public override void OnEnter() { Audio.PlayMusic("town"); }

        public override void Build(RectTransform root)
        {
            W.PageBackdrop(root);
            var sc = MakeScroll(root, 16, new RectOffset(18, 18, 18, 40));
            var top = U.Rt(sc.content, "top"); top.Le(-1, 100, 1); U.HBox(top, 12, null, TextAnchor.MiddleCenter, true, true, false, true);
            var cap = U.Box(top, Theme.Inset, Theme.Black, "cap"); cap.Le(300, 100);
            U.Icon(cap.transform, Icons.Get("heroes"), 62).At(0, 0.5f, 14, 0, 62, 62, 0, 0.5f);
            var ct = U.Txt(cap.transform, $"{C.Roster.Count}/{C.Roster.Capacity}", 56, C.Roster.Full ? Theme.Warn : Theme.Text, TextAnchor.MiddleCenter, true); ct.Fill(76, 10, 0, 4);
            string[] sn = { "Level", "Power", "Rarity", "Class" };
            var sb = U.Btn(top, L.T("Sort: {0}", L.T(sn[sort])), Theme.PanelLight, () => { sort = (sort + 1) % 4; Redraw(); }, 330, 100, 34); sb.Le(330, 100);
            var ae = U.Btn(top, L.T("Auto-Equip"), Theme.GreenDark, () => { C.Inventory.AutoEquipAll(); Audio.Play("equip"); Toasts.Show(L.T("Best gear equipped")); Redraw(); }, 330, 100, 34, Icons.Get("sword")); ae.Le(-1, 100, 1);

            var list = C.Roster.All.ToList();
            switch (sort)
            {
                case 0: list = list.OrderByDescending(u => u.level).ThenByDescending(u => C.Roster.Power(u)).ToList(); break;
                case 1: list = list.OrderByDescending(u => C.Roster.Power(u)).ToList(); break;
                case 2: list = list.OrderByDescending(u => C.Content.Rarities.FindIndex(r => r.id == u.rarity)).ThenByDescending(u => u.level).ToList(); break;
                default: list = list.OrderBy(u => u.classId).ThenByDescending(u => u.level).ToList(); break;
            }
            var grid = U.Rt(sc.content, "grid"); U.Grid(grid, new Vector2(500, 230), new Vector2(14, 14), 2, null, TextAnchor.UpperCenter); grid.Fit();
            foreach (var u in list) { var uu = u; var card = W.HeroCard(grid, uu, 500, 230, () => Popups.Show(new HeroPopup(uu.uid))); }
            if (!C.Roster.Full)
            {
                var add = U.Box(grid, Theme.PanelDark, Theme.Grey, "add", true); var ab = add.gameObject.AddComponent<UButton>(); ab.bg = add; ab.noTint = true; ab.onClick = () => App.I.nav.Push(new FacilityScreen("tavern"));
                U.Icon(add.transform, Icons.Get("plus"), 90).At(0.5f, 0.55f, 0, 0, 90, 90);
                var t = U.Txt(add.transform, L.T("Recruit at Tavern"), 34, Theme.TextDim); t.At(0.5f, 0.2f, 0, 0, 440, 44);
            }
            if (list.Count > 0)
            {
                var syn = U.Box(sc.content, Theme.PanelDark, Theme.Black, "syn"); syn.Le(-1, -1, 1); U.VBox(syn.R(), 8, new RectOffset(20, 20, 16, 16), TextAnchor.UpperCenter, true, true, true, false);
                var th = U.Txt(syn.transform, L.T("Party Synergies"), 40, Theme.Gold, TextAnchor.MiddleCenter, true); th.Le(-1, 50);
                foreach (var s in C.Content.Synergies) { var st = U.Txt(syn.transform, $"<color=#{ColorUtility.ToHtmlStringRGB(Theme.Good)}>{L.T(s.name)}</color>: {L.T(s.desc)}", 30, Theme.Text, TextAnchor.MiddleLeft, false, true); st.Le(-1, -1, 1); }
            }
        }
    }

    // =====================================================================================
    public class HeroPopup : Popup
    {
        readonly string uid;
        public HeroPopup(string id) { uid = id; }
        GameContext C => App.Ctx;
        UnitInstance U_ => C.Roster.Get(uid);
        double nextStateCheck;
        string shownHeroState;
        public override string Title => U_ != null ? U_.name : "";
        public override float Height => 0;

        public override void Build(RectTransform body)
        {
            var u = U_; if (u == null) { Close(); return; }
            shownHeroState = HeroState(u);
            var cls = C.Content.Class(u.classId); var rar = C.Content.Rarity(u.rarity); var rc = Theme.Hex(rar.color);
            var st = C.Roster.Stats(u);
            // --- banner ---
            var banner = U.Box(body, Color.Lerp(Theme.Inset, rc, 0.25f), rc, "banner"); banner.Le(-1, 360, 1);
            var sky = U.Img(banner.transform, Art.Bg(u.monster ? "DungeonA" : "PlainA"), new Color(1, 1, 1, 0.5f), "bgart"); sky.Fill(6, 6, 6, 6);
            var pic = W.Portrait(banner.transform, u.sprite, u.monster ? 300 : 280); pic.transform.parent.GetComponent<RectTransform>().At(0.28f, 0, 0, 24, 300, 300, 0.5f, 0); Tw.Bob(pic.transform.parent.GetComponent<RectTransform>(), 5, 1.3f);
            var info = U.Rt(banner.transform, "info"); info.At(1, 0.5f, -20, 0, 470, 330, 1, 0.5f); U.VBox(info, 8, null, TextAnchor.MiddleLeft, true, true, true, false);
            var nm = U.Txt(info, u.name, 48, Theme.Text, TextAnchor.MiddleCenter, true); nm.Le(-1, 56);
            var row1 = U.Rt(info, "r1"); row1.Le(-1, 52); U.HBox(row1, 10, null, TextAnchor.MiddleCenter, false, false); W.ClassBadge(row1, u.classId, 48); var cl = U.Txt(row1, L.T(cls.name), 38, Theme.Text, TextAnchor.MiddleCenter, true); cl.Le(cl.preferredWidth + 6, 48);
            var row2 = U.Rt(info, "r2"); row2.Le(-1, 48); U.HBox(row2, 10, null, TextAnchor.MiddleCenter, false, false);
            var rch = U.Chip(row2, L.T(rar.name), Color.Lerp(Theme.Inset, rc, 0.55f), 28, Color.white, 14, 42);
            if (u.monster) U.Chip(row2, L.T("Monster"), Theme.PurpleDark, 28, Color.white, 14, 42);
            var lv = U.Txt(info, $"Lv {u.level}/{C.Roster.LevelCap(u)}  {(u.promo > 0 ? W.Col(Theme.Gold, new string('*', u.promo)) : "")}", 40, Theme.Gold, TextAnchor.MiddleCenter, true); lv.Le(-1, 48);
            var xpbar = U.ProgressBar(info, Theme.Blue, 460, 30, u.level >= C.Roster.LevelCap(u) ? "MAX" : $"XP {Util.Num(u.xp)}/{Util.Num(C.Roster.XpToNext(u.level))}", 22);
            xpbar.Value = u.level >= C.Roster.LevelCap(u) ? 1 : (float)(u.xp / C.Roster.XpToNext(u.level));
            // --- hp & status ---
            var hprow = U.Rt(body, "hp"); hprow.Le(-1, 70); U.HBox(hprow, 14, null, TextAnchor.MiddleCenter, true, true, false, false);
            U.Icon(hprow, Icons.Get("hp"), 52).Le(52, 52);
            var hb = U.ProgressBar(hprow, C.Roster.HpFrac(u) < 0.3 ? Theme.Red : Theme.Green, 600, 40, $"{Util.Num(C.Roster.Hp(u))} / {Util.Num(C.Roster.MaxHp(u))}", 28); hb.Value = (float)C.Roster.HpFrac(u); hb.back.Le(600, 40, 1);
            var (stx, scol) = W.UnitStatus(u); var stt = U.Txt(hprow, stx, 34, scol, TextAnchor.MiddleRight, true); stt.Le(190, 50);

            // --- stats ---
            W.Header(body, L.T("Stats"), Icons.Get("atk"));
            var sg = U.Rt(body, "stats"); U.Grid(sg, new Vector2(286, 120), new Vector2(12, 12), 3, null, TextAnchor.UpperCenter); sg.Fit();
            foreach (var sd in C.Content.Stats)
            {
                var cell = U.Box(sg, Theme.PanelDark, Theme.Black, "s");
                U.Icon(cell.transform, Art.Get(sd.icon), 54).At(0, 0.5f, 10, 0, 54, 54, 0, 0.5f);
                var tn = U.Txt(cell.transform, sd.name, 30, Theme.TextDim, TextAnchor.MiddleCenter); tn.StretchX(1, -10, 38, 72, 12);
                double gear = C.Inventory.GearStat(u, sd.id);
                var tv = U.Txt(cell.transform, Util.Num(st[sd.id]) + (sd.pct ? "%" : ""), 42, Theme.Text, TextAnchor.MiddleCenter, true); tv.StretchX(0, 18, 50, 72, gear > 0.05 ? 96 : 12);
                if (gear > 0.05) { var tg = U.Txt(cell.transform, "+" + Util.Num(gear), 28, Theme.Good, TextAnchor.MiddleCenter, true); tg.At(1, 0, -8, 20, 86, 40, 1, 0); }
            }
            var pw = U.Box(sg, Theme.Inset, Theme.Gold, "pw"); var pwt = U.Txt(pw.transform, $"{L.T("Power")}\n<size=44><color=#95651f>{Util.Num(C.Roster.Power(u))}</color></size>", 30, Theme.TextDim, TextAnchor.MiddleCenter, false, true); pwt.Fill(8, 8, 8, 8);

            // --- equipment ---
            W.Header(body, L.T("Equipment"), Icons.Get("sword"));
            var eq = U.Rt(body, "eq"); eq.Le(-1, 220); U.HBox(eq, 14, null, TextAnchor.MiddleCenter, true, true, true, true);
            for (int si = 0; si < Roster.Slots.Length; si++)
            {
                int k = si; var g = C.Inventory.Get(u.equip[si]);
                var cell = U.Box(eq, Theme.PanelDark, Theme.Black, "slot", true); cell.Le(-1, 220, 1);
                var cb = cell.gameObject.AddComponent<UButton>(); cb.bg = cell; cb.noTint = true; cb.onClick = () => Popups.Show(new ItemPickerPopup(uid, k));
                var sl = U.Txt(cell.transform, L.T(W.Cap(Roster.Slots[si])), 28, Theme.TextDim); sl.StretchX(1, -10, 40, 10, 10);
                if (g != null)
                {
                    var def = C.Content.Item(g.defId); var rcol = Art.RarityColor(C, g.rarity);
                    var ic = W.ItemCell(cell.transform, g, 90, () => Popups.Show(new ItemPickerPopup(uid, k)), false); ic.At(0.5f, 0.5f, 0, 14, 90, 90);
                    var n = U.Txt(cell.transform, L.T(def.name) + (g.enh > 0 ? " +" + g.enh : ""), 28, W.Ink(rcol), TextAnchor.MiddleCenter, true); n.StretchX(0, 10, 64, 10, 10);
                }
                else
                {
                    U.Icon(cell.transform, Icons.Get("plus"), 70, new Color(1, 1, 1, 0.55f)).At(0.5f, 0.5f, 0, 6, 70, 70);
                    var n = U.Txt(cell.transform, L.T("Empty"), 28, Theme.TextDim); n.StretchX(0, 10, 40, 10, 10);
                }
            }
            var autoEq = U.Btn(body, L.T("Auto-equip best gear"), Theme.GreenDark, () => { C.Inventory.AutoEquip(U_); Audio.Play("equip"); Redraw(); }, 800, 90, 36, Icons.Get("sword")); autoEq.Le(-1, 90);

            // --- traits ---
            if (u.traits.Count > 0)
            {
                W.Header(body, L.T("Traits"), Icons.Get("star"));
                foreach (var tid in u.traits)
                {
                    var t = C.Content.TraitMap[tid]; var tr = U.Box(body, Theme.PanelDark, t.good ? Theme.GreenDark : Theme.RedDark, "trait"); tr.Le(-1, 84, 1);
                    var tn = U.Txt(tr.transform, L.T(t.name), 36, t.good ? Theme.Good : Theme.Warn, TextAnchor.MiddleLeft, true); tn.At(0, 0.5f, 22, 0, 300, 60, 0, 0.5f);
                    var td = U.Txt(tr.transform, L.T(t.desc), 32, Theme.Text, TextAnchor.MiddleRight, false, true); td.At(1, 0.5f, -22, 0, 560, 70, 1, 0.5f);
                }
            }
            // --- skills ---
            W.Header(body, L.T("Skills"), Icons.Get("crit"));
            foreach (var sid in cls.skills)
            {
                if (!C.Content.SkillMap.TryGetValue(sid, out var sk)) continue;
                bool open = sk.unlockLevel <= u.level + u.promo * 5;
                var row = U.Box(body, open ? Theme.PanelDark : Theme.GreyDark, Theme.Black, "skill"); row.Le(-1, 166, 1);
                var nm2 = U.Txt(row.transform, L.T(sk.name) + (sk.cd > 0 ? $"  <size=28><color=#75614a>CD {sk.cd}</color></size>" : ""), 36, open ? Theme.Gold : Theme.TextDim, TextAnchor.MiddleCenter, true); nm2.StretchX(1, -12, 46, 22, 22);
                var ds = U.Txt(row.transform, open ? L.T(sk.desc ?? "") : L.T("Unlocks at level {0}", sk.unlockLevel), 32, Theme.TextDim, TextAnchor.MiddleCenter, false, true); ds.StretchX(1, -66, 80, 22, 22);
            }
            // --- consumables ---
            var cons = C.Content.Items.Where(i => i.type == "consumable" && (i.use == "heal_pct" || i.use == "xp") && C.Inventory.Count(i.id) > 0).ToList();
            if (cons.Count > 0)
            {
                W.Header(body, L.T("Use items"), Icons.Get("potion"));
                var cr = U.Rt(body, "cons"); U.Grid(cr, new Vector2(150, 150), new Vector2(12, 12), 5, null, TextAnchor.UpperLeft); cr.Fit();
                foreach (var it in cons) { var id = it.id; W.StackCell(cr, id, C.Inventory.Count(id), 150, () => { if (C.Inventory.Use(id, U_)) { Audio.Play(C.Content.Item(id).use == "xp" ? "levelup" : "heal"); Redraw(); } }); }
            }
            // --- actions ---
            W.Header(body, L.T("Actions"));
            var ar = U.Rt(body, "act"); ar.Le(-1, 110); U.HBox(ar, 12, null, TextAnchor.MiddleCenter, true, true, true, true);
            var tr2 = U.Btn(ar, L.T("Train"), Theme.BlueDark, () => { Close(); App.I.nav.Push(new FacilityScreen("training")); }, 260, 100, 34); tr2.Le(-1, 100, 1);
            var hl = U.Btn(ar, L.T("Heal"), Theme.GreenDark, () => { Close(); App.I.nav.Push(new FacilityScreen("infirmary")); }, 260, 100, 34); hl.Le(-1, 100, 1);
            if (C.Roster.MaxPromo > 0)
            {
                bool can = C.Roster.CanPromote(u);
                var pb = U.Btn(ar, L.T("Promote"), can ? Theme.Accent : Theme.GreyDark, () => Popups.Show(new PromotePopup(uid)), 260, 100, 34, Icons.Get("star")); pb.Le(-1, 100, 1);
            }
            var disb = U.Btn(body, L.T("Dismiss hero"), Theme.RedDark, ConfirmDismiss, 800, 80, 32); disb.Le(-1, 80);
            var dismissBlocker = C.Roster.DismissBlocker(u);
            disb.Interactable = dismissBlocker == null;
            if (dismissBlocker != null) W.Info(body, L.T(dismissBlocker), 32, Theme.Warn);
        }

        string HeroState(UnitInstance u) => u == null ? "missing" : C.Roster.Version + ":" + C.Mods.Revision + ":" + C.Roster.IsBusy(u) + ":" + Math.Round(C.Roster.HpFrac(u) * 1000) + ":" + C.Roster.Count;

        public override void Tick()
        {
            if (Time.realtimeSinceStartupAsDouble < nextStateCheck) return;
            nextStateCheck = Time.realtimeSinceStartupAsDouble + 0.5;
            if (HeroState(U_) != shownHeroState) Redraw();
        }

        void ConfirmDismiss()
        {
            var hero = U_; var why = C.Roster.DismissBlocker(hero);
            if (why != null) { Toasts.Show(L.T(why)); Redraw(); return; }
            Popups.Confirm(L.T("Dismiss {0}?", hero.name), L.T("They will leave the guild forever. Equipped gear returns to your bag."), L.T("Dismiss"), () =>
            {
                var current = U_; var blocker = C.Roster.DismissBlocker(current);
                if (blocker != null) { Toasts.Show(L.T(blocker)); Redraw(); return; }
                if (C.Roster.Dismiss(current)) Close();
            }, L.T("Cancel"), true);
        }
    }

    public class PromotePopup : Popup
    {
        readonly string uid; public PromotePopup(string id) { uid = id; }
        GameContext C => App.Ctx; UnitInstance U_ => C.Roster.Get(uid);
        double nextStateCheck;
        int shownRosterVersion;
        bool shownBusy;
        public override string Title => L.T("Promotion");
        public override float Height => 900;
        public override bool UseScroll => false;
        public override bool HasFooter => true;
        public override void Build(RectTransform body)
        {
            var u = U_; if (u == null) { Close(); return; }
            shownRosterVersion = C.Roster.Version; shownBusy = C.Roster.IsBusy(u);
            var col = U.Rt(body, "c"); col.Fill(20, 20, 20, 20); U.VBox(col, 16, null, TextAnchor.UpperCenter, true, true, true, false);
            var pic = W.Portrait(col, u.sprite, 230); pic.transform.parent.GetComponent<LayoutElement>().preferredHeight = 240;
            W.Info(col, L.T("Promoting {0} raises all stats by {1}% and the level cap by {2}.", u.name, (int)(C.Content.Bal("promo.mult", 0.25) * 100), (int)C.Content.Bal("promo.levels", 10)), 36, Theme.Text);
            if (C.Roster.IsBusy(u)) W.Info(col, L.T("Finish or claim this hero's task before promotion."), 36, Theme.Warn);
            else if (!C.Roster.CanPromote(u)) { W.Info(col, u.promo >= C.Roster.MaxPromo ? L.T("Already at the highest promotion.") : L.T("Reach level {0} first.", C.Roster.LevelCap(u)), 36, Theme.Warn); }
            else { var cost = C.Roster.PromoCost(u); var cr = W.CostRow(col, cost, 40, 52); }
        }
        public override void BuildFooter(RectTransform f)
        {
            var u = U_; if (u == null) { Close(); return; }
            bool can = C.Roster.CanPromote(u) && C.CanPay(C.Roster.PromoCost(u));
            var b = U.Btn(f, L.T("PROMOTE"), Theme.Accent, () => { if (C.Roster.Promote(U_)) { Audio.Play("rank"); Close(); } }, 500, 100, 46, Icons.Get("star")); b.Le(-1, 100, 1); b.Interactable = can;
        }
        public override void Tick()
        {
            if (Time.realtimeSinceStartupAsDouble < nextStateCheck) return;
            nextStateCheck = Time.realtimeSinceStartupAsDouble + 0.5;
            var hero = U_;
            if (hero == null) { Close(); return; }
            if (C.Roster.Version != shownRosterVersion || C.Roster.IsBusy(hero) != shownBusy) Redraw();
        }
    }

    // =====================================================================================
    public class ItemPickerPopup : Popup
    {
        readonly string uid; readonly int slot;
        public ItemPickerPopup(string unit, int s) { uid = unit; slot = s; }
        GameContext C => App.Ctx;
        public override string Title => L.T(W.Cap(Roster.Slots[slot]));
        public override float Height => 1400;
        public override void Build(RectTransform body)
        {
            var u = C.Roster.Get(uid); if (u == null) { Close(); return; }
            var cur = C.Inventory.Get(u.equip[slot]);
            if (cur != null)
            {
                var b = U.Btn(body, L.T("Unequip {0}", C.Content.Item(cur.defId).name), Theme.RedDark, () => { var hero = C.Roster.Get(uid); if (hero == null) { Toasts.Show(L.T("Hero unavailable")); Close(); return; } C.Inventory.Unequip(hero, slot); Audio.Play("equip"); Close(); App.Ctx.Changed(); }, 800, 90, 34); b.Le(-1, 90);
            }
            var items = C.Inventory.Gear.Where(g => { var d = C.Content.Item(g.defId); return Inventory.SlotIndex(d) == slot && C.Inventory.CanEquip(u, g); }).OrderByDescending(g => C.Inventory.Score(g)).ToList();
            if (items.Count == 0) { W.Info(body, L.T("No compatible items in your bag. Defeat monsters or visit the Blacksmith!"), 36); return; }
            double curScore = cur != null ? C.Inventory.Score(cur) : 0;
            foreach (var g in items)
            {
                var gg = g; var def = C.Content.Item(g.defId); var rc = Art.RarityColor(C, g.rarity);
                var row = U.Box(body, Theme.PanelDark, gg == cur ? Theme.Gold : Theme.Black, "row", true); row.Le(-1, 164, 1);
                var rb = row.gameObject.AddComponent<UButton>(); rb.bg = row; rb.noTint = true; rb.sfx = "equip";
                rb.onClick = () => { C.Inventory.Equip(C.Roster.Get(uid), gg); Close(); App.Ctx.Changed(); };
                var cell = W.ItemCell(row.transform, gg, 118, null); cell.At(0, 0.5f, 16, 0, 118, 118, 0, 0.5f); cell.GetComponent<Image>().raycastTarget = false;
                var nm = U.Txt(row.transform, L.T(def.name) + (gg.enh > 0 ? " +" + gg.enh : ""), 36, W.Ink(rc), TextAnchor.MiddleLeft, true); nm.StretchX(1, -14, 46, 150, 260);
                var stx = string.Join("  ", C.Content.Stats.Where(s => C.Inventory.ItemStat(gg, s.id) > 0.01).Select(s => $"{s.name} +{Util.Num(C.Inventory.ItemStat(gg, s.id))}"));
                var sd = U.Txt(row.transform, stx, 28, Theme.TextDim, TextAnchor.UpperLeft, false, true); sd.StretchX(0, 14, 62, 150, 220);
                double diff = C.Inventory.Score(gg) - curScore;
                if (gg != cur && !string.IsNullOrEmpty(gg.owner)) { var o = U.Txt(row.transform, L.T("On {0}", C.Roster.Get(gg.owner)?.name), 24, Theme.Warn, TextAnchor.MiddleRight); o.At(1, 1, -16, -10, 240, 30, 1, 1); }
                if (gg == cur) U.Chip(row.transform, L.T("Equipped"), Theme.GreenDark, 26, Color.white, 12, 40).At(1, 0.5f, -16, 0, 160, 40, 1, 0.5f);
                else { var dt = U.Txt(row.transform, (diff >= 0 ? "+" : "") + Util.Num(diff), 40, diff >= 0 ? Theme.Good : Theme.Bad, TextAnchor.MiddleRight, true); dt.At(1, 0.5f, -20, -10, 140, 50, 1, 0.5f); var up = U.Icon(row.transform, Icons.Get(diff >= 0 ? "arrow_u" : "arrow_d"), 40, diff >= 0 ? Theme.Good : Theme.Bad); up.At(1, 0.5f, -170, -10, 40, 40, 1, 0.5f); }
            }
        }
    }

    /// <summary>Generic "choose one hero" list (training, healing...).</summary>
    public class HeroPickPopup : Popup
    {
        readonly string title; readonly Func<UnitInstance, string> blocker; readonly Action<UnitInstance> pick; readonly Func<UnitInstance, string> extra;
        public HeroPickPopup(string t, Func<UnitInstance, string> block, Action<UnitInstance> onPick, Func<UnitInstance, string> info = null) { title = t; blocker = block; pick = onPick; extra = info; }
        public override string Title => title;
        public override float Height => 1300;
        public override void Build(RectTransform body)
        {
            var C = App.Ctx; var list = C.Roster.All.OrderByDescending(u => blocker(u) == null).ThenByDescending(u => u.level).ToList();
            if (list.Count == 0) W.Info(body, L.T("No heroes"));
            foreach (var u in list)
            {
                var uu = u; var why = blocker(u);
                var row = U.Box(body, why == null ? Theme.PanelDark : Theme.GreyDark, Theme.Black, "row", true); row.Le(-1, 150, 1);
                var rb = row.gameObject.AddComponent<UButton>(); rb.bg = row; rb.noTint = true;
                rb.onClick = () => { if (blocker(uu) != null) { Toasts.Show(L.T(blocker(uu))); return; } pick(uu); Close(); };
                var pic = W.Portrait(row.transform, u.sprite, 120); pic.transform.parent.GetComponent<RectTransform>().At(0, 0.5f, 20, -4, 108, 120, 0, 0.5f);
                var nm = U.Txt(row.transform, u.name, 38, Theme.Text, TextAnchor.MiddleLeft, true); nm.At(0, 1, 150, -10, 520, 46, 0, 1);
                var lv = U.Txt(row.transform, $"{L.T(C.Roster.ClassName(u))}  Lv {u.level}", 30, Theme.Gold, TextAnchor.MiddleLeft); lv.At(0, 1, 150, -58, 520, 38, 0, 1);
                var hp = U.ProgressBar(row.transform, C.Roster.HpFrac(u) < 0.3 ? Theme.Red : Theme.Green, 360, 22); hp.Value = (float)C.Roster.HpFrac(u); hp.back.R().At(0, 0, 150, 14, 360, 22, 0, 0);
                var inf = extra?.Invoke(u);
                var r = U.Txt(row.transform, why != null ? L.T(why) : (inf ?? ""), 30, why != null ? Theme.Bad : Theme.Good, TextAnchor.MiddleRight, false, true); r.At(1, 0.5f, -20, 0, 330, 120, 1, 0.5f);
            }
        }
    }
}
