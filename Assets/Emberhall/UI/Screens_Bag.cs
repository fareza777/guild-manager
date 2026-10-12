using System;
using System.Collections.Generic;
using System.Linq;
using ManagerKit;
using ManagerKit.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Emberhall
{
    public static class Bag { public static int OpenTab; public static int Filter; public static int Sort; }

    public class BagScreen : UiScreen
    {
        public static void EquipBest()
        {
            App.Ctx.Inventory.AutoEquipAll(); App.Ctx.Quests.Tick(Util.Now); App.Ctx.Changed();
            Audio.Play("equip"); Toasts.Show(L.T("Best gear equipped"));
            App.I.nav.Current?.Redraw();
        }
        GameContext C => App.Ctx;
        public override string Id => "Bag";
        readonly List<(Text t, Func<string> f)> live = new List<(Text, Func<string>)>();
        double nextTick;
        double shownMarketRefresh;
        public override void OnEnter() { Audio.PlayMusic("town"); }

        public override void Build(RectTransform root)
        {
            live.Clear();
            shownMarketRefresh = C.Market.NextRefresh;
            W.PageBackdrop(root);
            var col = U.Rt(root, "col"); col.Fill(); U.VBox(col, 0, null, TextAnchor.UpperCenter, true, true, true, false);
            var tabs = U.Rt(col, "tabs"); tabs.Le(-1, 110); U.HBox(tabs, 8, new RectOffset(14, 14, 12, 4), TextAnchor.MiddleCenter, true, true, true, true);
            string[] names = { "Gear", "Items", "Market" };
            for (int i = 0; i < 3; i++) { int k = i; var b = U.Btn(tabs, L.T(names[i]), Bag.OpenTab == i ? Theme.Accent : Theme.PanelLight, () => { Bag.OpenTab = k; Redraw(); }, 300, 94, 38); b.Le(-1, 94, 1); }
            var content = U.Rt(col, "content"); content.Le(-1, -1, 1, 1);
            var sc = MakeScroll(content, 14, new RectOffset(18, 18, 14, 40));
            foreach (var g in C.Inventory.Gear) if (g.isNew) { }
            if (Bag.OpenTab == 0) BuildGear(sc.content); else if (Bag.OpenTab == 1) BuildItems(sc.content); else BuildMarket(sc.content);
        }

        void BuildGear(RectTransform p)
        {
            var inv = C.Inventory;
            var equip = U.Btn(p, L.T("Auto-equip best gear"), Theme.Green, EquipBest, 800, 94, 36, Icons.Get("sword")); equip.Le(-1, 94, 1);
            Hud.Reg("btn_main", equip);
            var top = U.Rt(p, "top"); top.Le(-1, 132); U.HBox(top, 10, null, TextAnchor.MiddleCenter, true, true, false, true);
            var cap = U.Box(top, Theme.Inset, inv.Full ? Theme.Red : Theme.Black, "cap"); cap.Le(260, 92);
            U.Icon(cap.transform, Icons.Get("bag"), 56).At(0, 0.5f, 12, 0, 56, 56, 0, 0.5f);
            U.Txt(cap.transform, $"{inv.Used}/{inv.Capacity}", 48, inv.Full ? Theme.Bad : Theme.Text, TextAnchor.MiddleCenter, true).Fill(70, 8, 0, 4);
            string[] fn = { "All", "Weapon", "Armor", "Accessory" };
            var fb = U.Btn(top, L.T(fn[Bag.Filter]), Theme.PanelLight, () => { Bag.Filter = (Bag.Filter + 1) % 4; Redraw(); }, 240, 92, 34, Icons.Get("menu")); fb.Le(240, 92);
            string[] sn = { "Rarity", "Tier", "Newest" };
            var sb = U.Btn(top, L.T(sn[Bag.Sort]), Theme.PanelLight, () => { Bag.Sort = (Bag.Sort + 1) % 3; Redraw(); }, 300, 92, 34, Icons.Get("arrow_d")); sb.Le(300, 92);
            var junk = U.Btn(top, L.T("Sell junk"), Theme.GreenDark, SellJunk, 240, 92, 32, Icons.Get("coin")); junk.Le(-1, 92, 1);

            var list = inv.Gear.Where(g => Bag.Filter == 0 || C.Content.Item(g.defId).type == new[] { "", "weapon", "armor", "accessory" }[Bag.Filter]).ToList();
            int RI(ItemInstance g) => C.Content.Rarities.FindIndex(r => r.id == g.rarity);
            switch (Bag.Sort)
            {
                case 0: list = list.OrderByDescending(RI).ThenByDescending(g => C.Content.Item(g.defId).tier).ThenByDescending(g => g.enh).ToList(); break;
                case 1: list = list.OrderByDescending(g => C.Content.Item(g.defId).tier).ThenByDescending(RI).ToList(); break;
                default: list = Enumerable.Reverse(list).ToList(); break;
            }
            if (list.Count == 0) { W.Info(p, L.T("Your bag is empty. Win battles to find gear!"), 38); return; }
            var grid = U.Rt(p, "grid"); U.Grid(grid, new Vector2(230, 230), new Vector2(18, 18), 4, null, TextAnchor.UpperCenter); grid.Fit();
            foreach (var g in list) { var gg = g; W.ItemCell(grid, gg, 230, () => { gg.isNew = false; Popups.Show(new ItemPopup(gg.uid)); }); }
            foreach (var g in inv.Gear) if (g.isNew) { }
        }

        void SellJunk()
        {
            var inv = C.Inventory; int maxR = 1;
            var junk = inv.Gear.Where(g => string.IsNullOrEmpty(g.owner) && !g.locked && C.Content.Rarities.FindIndex(r => r.id == g.rarity) <= maxR).ToList();
            if (junk.Count == 0) { Toasts.Show(L.T("Nothing to sell")); return; }
            double total = junk.Sum(g => inv.SellValue(g));
            Popups.Confirm(L.T("Sell junk?"), L.T("Sell {0} Common & Uncommon items that nobody is wearing for {1} gold?", junk.Count, Util.Num(total)), L.T("Sell"), () => { double g = inv.SellJunk(maxR); Audio.Play("coin"); Toasts.Show("+" + Util.Num(g) + " " + L.T("gold"), Art.ResIcon(C, "gold")); Redraw(); });
        }

        void BuildItems(RectTransform p)
        {
            var inv = C.Inventory;
            var items = inv.Stacks.Where(s => s.Value > 0).Select(s => (def: C.Content.Item(s.Key), n: (int)s.Value)).Where(x => x.def != null).OrderBy(x => x.def.type).ThenBy(x => x.def.name).ToList();
            if (items.Count == 0) { W.Info(p, L.T("No materials or consumables yet."), 38); return; }
            W.Header(p, L.T("Materials"), Icons.Get("ore"));
            var g1 = U.Rt(p, "mats"); U.Grid(g1, new Vector2(182, 182), new Vector2(12, 12), 5, null, TextAnchor.UpperLeft); g1.Fit();
            foreach (var x in items.Where(x => x.def.type == "material")) { var id = x.def.id; W.StackCell(g1, id, x.n, 182, () => Popups.Show(new StackPopup(id))); }
            W.Header(p, L.T("Consumables"), Icons.Get("potion"));
            var g2 = U.Rt(p, "cons"); U.Grid(g2, new Vector2(182, 182), new Vector2(12, 12), 5, null, TextAnchor.UpperLeft); g2.Fit();
            foreach (var x in items.Where(x => x.def.type == "consumable")) { var id = x.def.id; W.StackCell(g2, id, x.n, 182, () => Popups.Show(new StackPopup(id))); }
        }

        void BuildMarket(RectTransform p)
        {
            W.Info(p, L.T("The merchant restocks every day. Gear quality is random!"), 32, Theme.TextDim);
            var t = U.Txt(p, "", 34, Theme.Gold, TextAnchor.MiddleCenter, true); t.Le(-1, 50); live.Add((t, () => L.T("New stock in {0}", Util.Dur(C.Market.NextRefresh - Util.Now))));
            foreach (var o in C.Market.Offers)
            {
                var oo = o; var def = C.Content.Item(o.itemId); var rc = o.rarity != null ? Art.RarityColor(C, o.rarity) : Theme.Black;
                var card = U.Box(p, o.sold ? Theme.GreyDark : Theme.Panel, o.rarity != null ? rc : Theme.Black, "offer"); card.Le(-1, 196, 1);
                var ib = U.Box(card.transform, Theme.Inset, rc, "ib"); ib.At(0, 0.5f, 16, 0, 118, 118, 0, 0.5f);
                U.Icon(ib.transform, Art.ItemIcon(def), 84).At(0.5f, 0.5f, 0, 0, 84, 84);
                var nm = U.Txt(card.transform, L.T(def.name) + (o.qty > 1 ? " x" + o.qty : ""), 38, o.rarity != null ? rc : Theme.Text, TextAnchor.MiddleLeft, true); nm.StretchX(1, -14, 48, 150, 260);
                string sub = def.IsGear ? $"{C.Content.Rarity(o.rarity).name}  T{def.tier}  " + string.Join(" ", def.stats.Select(s => $"{s.id.ToUpper()}+{Util.Num(s.v)}")) : L.T(def.desc);
                var sd = U.Txt(card.transform, sub, 27, Theme.TextDim, TextAnchor.UpperLeft, false, true); sd.StretchX(1, -84, 100, 150, 260);
                if (o.sold) { var sold = U.Chip(card.transform, L.T("SOLD"), Theme.GreyDark, 34, Theme.TextDim, 20, 70); sold.At(1, 0.5f, -20, 0, 180, 70, 1, 0.5f); }
                else
                {
                    var b = U.Btn(card.transform, Util.Num(o.price), o.cur == "gems" ? Theme.PurpleDark : Theme.GreenDark, () => { if (C.Market.Buy(oo)) { Audio.Play(oo.cur == "gems" ? "gem" : "coin"); Redraw(); } }, 220, 100, 38, Art.ResIcon(C, o.cur)); b.At(1, 0.5f, -16, 0, 220, 100, 1, 0.5f);
                    b.Interactable = C.Wallet.Has(o.cur, o.price);
                }
            }
        }

        public override void Tick()
        {
            if (Time.realtimeSinceStartupAsDouble < nextTick) return; nextTick = Time.realtimeSinceStartupAsDouble + 0.5;
            foreach (var (t, f) in live) if (t) t.text = f();
            if (Bag.OpenTab == 2 && C.Market.NextRefresh != shownMarketRefresh) Redraw();
        }
    }

    // =====================================================================================
    public class ItemPopup : Popup
    {
        readonly string uid; public ItemPopup(string id) { uid = id; }
        GameContext C => App.Ctx; ItemInstance G => C.Inventory.Get(uid);
        public override string Title => G != null ? C.Content.Item(G.defId).name : "";
        public override float Height => 0;
        Image iconImg;

        public override void Build(RectTransform body)
        {
            var g = G; if (g == null) { Close(); return; }
            var def = C.Content.Item(g.defId); var rar = C.Content.Rarity(g.rarity); var rc = Theme.Hex(rar.color);
            var top = U.Box(body, Color.Lerp(Theme.Inset, rc, 0.3f), rc, "top"); top.Le(-1, 230, 1);
            var ib = U.Box(top.transform, Theme.Inset, rc, "ib"); ib.At(0, 0.5f, 20, 0, 180, 180, 0, 0.5f);
            iconImg = U.Icon(ib.transform, Art.ItemIcon(def), 140); iconImg.At(0.5f, 0.5f, 0, 0, 140, 140);
            var nm = U.Txt(top.transform, L.T(def.name) + (g.enh > 0 ? " <color=#95651f>+" + g.enh + "</color>" : ""), 46, W.Ink(rc), TextAnchor.MiddleCenter, true); nm.StretchX(1, -18, 60, 230, 22);
            var meta = U.Rt(top.transform, "meta"); meta.StretchX(1, -90, 46, 230, 22); U.HBox(meta, 8, null, TextAnchor.MiddleCenter, true, true, false, false);
            U.Chip(meta, L.T(rar.name), Color.Lerp(Theme.Inset, rc, 0.6f), 26, Color.white, 14, 42); U.Chip(meta, "T" + def.tier, Theme.GreyDark, 26, Color.white, 14, 42); U.Chip(meta, L.T(W.Cap(def.slot ?? def.type)), Theme.GreyDark, 26, Color.white, 14, 42);
            if (!string.IsNullOrEmpty(def.family)) U.Chip(meta, W.Cap(def.family), Theme.GreyDark, 26, Color.white, 14, 42);
            if (!string.IsNullOrEmpty(g.owner)) { var o = U.Txt(top.transform, L.T("Equipped by {0}", C.Roster.Get(g.owner)?.name), 32, Theme.Good, TextAnchor.MiddleLeft, true); o.At(0, 0, 230, 20, 640, 40, 0, 0); }

            // stats
            W.Header(body, L.T("Stats"));
            foreach (var sd in C.Content.Stats)
            {
                double v = C.Inventory.ItemStat(g, sd.id); if (v < 0.01) continue;
                var row = U.Rt(body, "st"); row.Le(-1, 70); U.HBox(row, 14, new RectOffset(16, 16, 0, 0), TextAnchor.MiddleCenter, true, true, false, false);
                U.Icon(row, Art.Get(sd.icon), 46).Le(46, 46);
                var n = U.Txt(row, sd.name, 38, Theme.TextDim, TextAnchor.MiddleCenter); n.Le(-1, 50, 1);
                var value = U.Box(row, Theme.Inset, Theme.Black, "stat_value"); value.Le(168, 76);
                var t = U.Txt(value.transform, "+" + Util.Num(v) + (sd.pct ? "%" : ""), 42, Theme.Good, TextAnchor.MiddleCenter, true); t.Fill(8, 8, 4, 4);
            }
            if (g.affix.Count > 0) { var af = U.Txt(body, L.T("Includes {0} bonus affix(es)", g.affix.Count), 28, Theme.Purple, TextAnchor.MiddleCenter); af.Le(-1, 40); }

            // enhance
            if (def.IsGear)
            {
                W.Header(body, L.T("Enhance"), Icons.Get("hammer"));
                bool built = C.Upgrades.Level("blacksmith") > 0;
                if (!built) W.Info(body, L.T("Build the Blacksmith to enhance gear."), 32, Theme.Warn);
                else if (g.enh >= C.Crafting.MaxEnh) W.Info(body, L.T("Max enhancement for your Blacksmith level ({0}). Upgrade the Blacksmith!", C.Crafting.MaxEnh), 32, Theme.Warn);
                else
                {
                    var cost = C.Crafting.EnhCost(g);
                    W.CostRow(body, cost, 40, 50);
                    W.Info(body, L.T("Success chance: {0}", Util.Pct(C.Crafting.EnhChance(g))), 34, C.Crafting.EnhChance(g) >= 0.7 ? Theme.Good : Theme.Warn);
                    var b = U.Btn(body, L.T("ENHANCE"), Theme.Accent, DoEnhance, 700, 110, 46, Icons.Get("hammer")); b.Le(-1, 110); b.Interactable = C.Crafting.CanEnhance(g);
                }
            }
            // actions
            W.Header(body, L.T("Actions"));
            var row2 = U.Rt(body, "act"); row2.Le(-1, 110); U.HBox(row2, 12, null, TextAnchor.MiddleCenter, true, true, true, true);
            if (def.IsGear) { var eb = U.Btn(row2, L.T("Equip"), Theme.GreenDark, () => Popups.Show(new HeroPickPopup(L.T("Equip on..."), u => C.Inventory.CanEquip(u, G) ? null : "Cannot equip", u => { C.Inventory.Equip(u, G); Audio.Play("equip"); Redraw(); App.Ctx.Changed(); }, u => { var cur = C.Inventory.Get(u.equip[Inventory.SlotIndex(def)]); double d = C.Inventory.Score(G) - (cur != null ? C.Inventory.Score(cur) : 0); return (d >= 0 ? "+" : "") + Util.Num(d); })), 260, 100, 38); eb.Le(-1, 100, 1); }
            var lk = U.Btn(row2, g.locked ? L.T("Unlock") : L.T("Lock"), Theme.PanelLight, () => { G.locked = !G.locked; Redraw(); App.Ctx.Changed(); }, 260, 100, 38, Icons.Get("lock")); lk.Le(-1, 100, 1);
            bool free = string.IsNullOrEmpty(g.owner) && !g.locked;
            var sv = C.Inventory.SellValue(g);
            var sell = U.Btn(body, L.T("Sell for {0}", Util.Num(sv)), Theme.GreenDark, () => { if (C.Inventory.Sell(G)) { Audio.Play("coin"); Close(); } }, 700, 100, 40, Art.ResIcon(C, "gold")); sell.Le(-1, 100); sell.Interactable = free;
            if (C.Upgrades.Level("blacksmith") > 0 && def.IsGear)
            {
                var y = C.Crafting.SalvageYield(g); var sal = U.Btn(body, L.T("Salvage") + "  " + string.Join(" ", y.Select(r => "+" + r.n + " " + C.ResName(r.id))), Theme.PurpleDark, () => Popups.Confirm(L.T("Salvage item?"), L.T("The item will be destroyed for materials."), L.T("Salvage"), () => { if (C.Crafting.Salvage(G)) { Audio.Play("forge"); Close(); } }), 700, 100, 32, Icons.Get("ore")); sal.Le(-1, 100); sal.Interactable = free;
            }
        }

        void DoEnhance()
        {
            var g = G; int r = C.Crafting.Enhance(g);
            if (r == 1) { Audio.Play("forge"); Audio.Play("levelup", 0.6f); Fx.Burst(Panel, new Vector2(0, Panel.rect.height / 2 - 260), Icons.Get("star"), 18, 600, null, 36); Toasts.Show(L.T("Enhancement succeeded! +{0}", g.enh), Icons.Get("hammer"), Theme.GreenDark); }
            else if (r == 0) { Audio.Play("deny"); Tw.Shake(Panel, 16, 0.3f); Toasts.Show(L.T("The enhancement failed..."), Icons.Get("cross"), Theme.RedDark); }
            Redraw();
        }
    }

    public class StackPopup : Popup
    {
        readonly string id; public StackPopup(string i) { id = i; }
        GameContext C => App.Ctx;
        public override string Title => C.Content.Item(id).name;
        public override float Height => 900;
        public override bool UseScroll => false;
        public override bool HasFooter => true;
        public override void Build(RectTransform body)
        {
            var def = C.Content.Item(id); var col = U.Rt(body, "c"); col.Fill(20, 20, 20, 20); U.VBox(col, 18, null, TextAnchor.UpperCenter, true, true, true, false);
            var ib = U.Box(col, Theme.Inset, Theme.Black, "ib"); ib.Le(190, 190); ib.R().Sz(190, 190); U.Icon(ib.transform, Art.ItemIcon(def), 150).At(0.5f, 0.5f, 0, 0, 150, 150);
            var n = U.Txt(col, $"x{C.Inventory.Count(id)}", 54, Theme.Gold, TextAnchor.MiddleCenter, true); n.Le(-1, 70);
            W.Info(col, L.T(def.desc), 36, Theme.Text);
            W.Info(col, L.T("Sell value: {0} gold each", Util.Num(C.Inventory.SellValueStack(id))), 30, Theme.TextDim);
        }
        public override void BuildFooter(RectTransform f)
        {
            var def = C.Content.Item(id);
            if (def.type == "consumable") U.Btn(f, L.T("Use"), Theme.Green, () => { Close(); Use(def); }, 300, 100, 42).Le(-1, 100, 1);
            U.Btn(f, L.T("Sell 1"), Theme.GreenDark, () => { if (C.Inventory.SellStack(id, 1)) { Audio.Play("coin"); if (C.Inventory.Count(id) <= 0) Close(); else Redraw(); } }, 260, 100, 38).Le(-1, 100, 1);
            if (C.Inventory.Count(id) > 1) U.Btn(f, L.T("Sell all"), Theme.RedDark, () => { C.Inventory.SellStack(id, C.Inventory.Count(id)); Audio.Play("coin"); Close(); }, 260, 100, 38).Le(-1, 100, 1);
        }
        void Use(ItemDef def)
        {
            if (def.use == "heal_pct" || def.use == "xp") Popups.Show(new HeroPickPopup(L.T("Use {0} on...", def.name), u => def.use == "heal_pct" && C.Roster.HpFrac(u) > 0.999 ? "Already healthy" : (def.use == "xp" && u.level >= C.Roster.LevelCap(u) ? "At level cap" : null), u => { if (C.Inventory.Use(def.id, u)) Audio.Play(def.use == "xp" ? "levelup" : "heal"); }, u => def.use == "heal_pct" ? Util.Pct(C.Roster.HpFrac(u)) + " HP" : "Lv " + u.level));
            else if (C.Inventory.Use(def.id, null)) { Audio.Play("coin"); Toasts.Show(L.T(def.desc)); }
        }
    }
}
