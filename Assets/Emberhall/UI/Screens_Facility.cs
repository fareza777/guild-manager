using System;
using System.Collections.Generic;
using System.Linq;
using ManagerKit;
using ManagerKit.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Emberhall
{
    /// <summary>Generic facility page: banner, upgrade card, then facility-specific tools (recruit, forge, train, heal, vault, research).</summary>
    public class FacilityScreen : UiScreen
    {
        public readonly string FacilityId;
        int sub; static int forgeFilter;
        GameContext C => App.Ctx;
        UpgradeDef Def => C.Content.Upgrade(FacilityId);
        public override string Id => "Facility_" + FacilityId;
        readonly List<(Text t, Func<string> f)> live = new List<(Text, Func<string>)>();
        readonly List<(U.Bar b, Func<float> f)> bars = new List<(U.Bar, Func<float>)>();
        double nextTick; int lastLevel = -1; int lastBusy = -1;
        double shownRecruitRefresh;
        bool shownFreeRefresh;

        public FacilityScreen(string id) { FacilityId = id; }
        public void ShowRecruitment() { sub = 0; Redraw(); }
        public override void OnEnter() { Audio.PlayMusic("town"); }
        public override bool OnBack() => false;

        public static string ReqText(Req r)
        {
            var C = App.Ctx; var s = r.stat; int n = (int)r.min;
            if (s == "missions.won") return L.T("Win {0} battles", n);
            if (s == "rank.index") return L.T("Reach Guild Rank {0}", C.Content.Rank(n).id);
            if (s.StartsWith("facility.")) return L.T("{0} Lv {1}", L.T(C.Content.Upgrade(s.Substring(9))?.name ?? s), n);
            if (s.StartsWith("mission.clear.")) return L.T("Clear {0}", L.T(C.Content.Mission(s.Substring(14))?.name ?? s));
            if (s.StartsWith("quest.done.")) return L.T("Complete quest: {0}", L.T(C.Content.QuestMap.TryGetValue(s.Substring(11), out var q) ? q.title : s));
            return $"{s} >= {n}";
        }

        public override void Build(RectTransform root)
        {
            live.Clear(); bars.Clear();
            var def = Def; W.PageBackdrop(root);
            var sc = MakeScroll(root, 14, new RectOffset(18, 18, 14, 40)); var p = sc.content;
            int lv = C.Upgrades.Level(def.id);
            lastLevel = lv;

            // header
            var head = U.Rt(p, "head"); head.Le(-1, 100); U.HBox(head, 12, null, TextAnchor.MiddleLeft, true, true, false, false);
            var back = U.IconBtn(head, Icons.Get("arrow_l"), Theme.PanelLight, () => App.I.nav.Pop(), 96); back.Le(96, 96);
            var tt = U.Txt(head, L.T(def.name), 56, Theme.Gold, TextAnchor.MiddleLeft, true); tt.Le(-1, 80, 1);
            if (lv > 0) { var lc = U.Chip(head, $"Lv {lv}", Theme.AccentDark, 40, Color.white, 24, 66); }

            // banner
            var ban = U.Box(p, Theme.Inset, Theme.Black, "banner"); ban.Le(-1, 330, 1);
            var sky = U.Img(ban.transform, Art.Bg("PlainA"), Color.white, "sky"); sky.Fill(6, 6, 6, 6); sky.preserveAspect = false;
            var sp = Art.Prop(def.sprite != null ? def.sprite : "house_20"); if (sp != null)
            { var h = U.Img(ban.transform, sp, lv > 0 ? Color.white : new Color(0.4f, 0.38f, 0.5f), "house"); float s = 300f / sp.rect.height; h.At(0.5f, 0, 0, 20, sp.rect.width * s, sp.rect.height * s, 0.5f, 0); }
            if (lv == 0) U.Icon(ban.transform, Icons.Get("hammer"), 100).At(0.5f, 0.5f, 0, 30, 100, 100);
            W.Info(p, L.T(def.desc), 34, Theme.Text);

            BuildUpgrade(p, def, lv);
            if (lv > 0)
            {
                switch (FacilityId)
                {
                    case "tavern": BuildTavern(p); break;
                    case "blacksmith": BuildBlacksmith(p); break;
                    case "training": BuildStation(p, C.Stations.ByKind("train"), "Train a hero"); break;
                    case "infirmary": BuildStation(p, C.Stations.ByKind("heal"), "Heal a hero"); break;
                    case "barracks": BuildBarracks(p); break;
                    case "treasury": BuildTreasury(p); break;
                    case "library": BuildLibrary(p); break;
                }
            }
            shownRecruitRefresh = C.Recruiter.RefreshAt;
            shownFreeRefresh = C.Recruiter.FreeRefreshReady;
        }

        // ------------------------------------------------------------------------------------
        void BuildUpgrade(RectTransform p, UpgradeDef def, int lv)
        {
            var card = U.Box(p, Theme.Panel, Theme.Gold, "up"); card.Le(-1, -1, 1); U.VBox(card.R(), 12, new RectOffset(24, 24, 20, 22), TextAnchor.UpperCenter, true, true, true, false);
            var task = C.Upgrades.Pending(def.id); int next = lv + 1; int max = C.Upgrades.MaxLevel(def);
            if (task != null)
            {
                var t = U.Txt(card.transform, L.T("Upgrading to Lv {0}...", task.level), 42, Theme.Gold, TextAnchor.MiddleCenter, true); t.Le(-1, 54);
                var bar = U.ProgressBar(card.transform, Theme.Accent, 800, 46, "", 28); bar.back.Le(-1, 46, 1); bars.Add((bar, () => (float)C.Timers.Progress(task)));
                var tm = U.Txt(card.transform, "", 44, Theme.Text, TextAnchor.MiddleCenter, true); tm.Le(-1, 56); live.Add((tm, () => Util.Dur(C.Timers.Remaining(task))));
                var sb = U.Btn(card.transform, "", Theme.PurpleDark, () => { if (!C.Timers.Speedup(task)) Toasts.Show(L.T("Not enough gems")); else { Audio.Play("gem"); Redraw(); } }, 600, 100, 42, Icons.Get("gem")); sb.Le(-1, 100); live.Add((sb.label, () => L.T("Finish now") + "  " + C.Timers.SpeedCost(task)));
                return;
            }
            if (!C.Upgrades.Unlocked(def))
            {
                var t = U.Txt(card.transform, L.T("Locked"), 44, Theme.Bad, TextAnchor.MiddleCenter, true); t.Le(-1, 56);
                foreach (var r in def.req) { bool ok = C.Stats.Get(r.stat) >= r.min; var rt = U.Txt(card.transform, (ok ? "<color=#6fe08a>" : "<color=#ff6b6b>") + ReqText(r) + "</color>  (" + Util.Num(Math.Min(r.min, C.Stats.Get(r.stat))) + "/" + Util.Num(r.min) + ")", 36, Theme.Text, TextAnchor.MiddleCenter); rt.Le(-1, 50); }
                return;
            }
            if (lv >= def.max) { var t = U.Txt(card.transform, L.T("Maximum level reached"), 44, Theme.Gold, TextAnchor.MiddleCenter, true); t.Le(-1, 60); }
            else if (next > max)
            {
                var t = U.Txt(card.transform, L.T("Level {0} reached - needs a higher Guild Rank to go further", lv), 38, Theme.Warn, TextAnchor.MiddleCenter, false, true); t.Le(-1, 110);
                int needRank = (int)Math.Ceiling((next - C.Content.Bal("facility.base_max", 2)) / C.Content.Bal("facility.per_rank", 2)); needRank = Mathf.Clamp(needRank, 1, C.Content.Ranks.Count - 1);
                var rt = U.Txt(card.transform, L.T("Required: Rank {0}", C.Content.Rank(needRank).id), 36, Theme.Gold, TextAnchor.MiddleCenter, true); rt.Le(-1, 48);
            }
            else
            {
                var t = U.Txt(card.transform, lv == 0 ? L.T("Build") : L.T("Upgrade to Lv {0}", next), 46, Theme.Gold, TextAnchor.MiddleCenter, true); t.Le(-1, 60);
                for (int i = 0; i < def.effects.Count; i++)
                {
                    var e = def.effects[i]; string note = i < def.levelNotes.Count ? L.T(def.levelNotes[i]) : e.key;
                    var nt = U.Txt(card.transform, "<color=#6fe08a>+</color> " + note, 32, Theme.Text, TextAnchor.MiddleCenter); nt.Le(-1, 40);
                }
                var cost = C.Upgrades.Cost(def, next);
                var cr = W.CostRow(card.transform, cost, 40, 50); var tm = U.Txt(card.transform, "<color=#bba9d4>" + L.T("Time") + "</color> " + Util.Dur(C.Upgrades.Time(def, next)), 34, Theme.Text, TextAnchor.MiddleCenter); tm.Le(-1, 44);
                var blocker = C.Upgrades.Blocker(def);
                var b = U.Btn(card.transform, lv == 0 ? L.T("BUILD") : L.T("UPGRADE"), Theme.Green, () => DoUpgrade(def), 700, 120, 54, Icons.Get("hammer")); b.Le(-1, 120);
                b.Interactable = C.CanPay(cost) && blocker == null;
                var quest = C.Quests.CurrentMain;
                bool questBuild = quest != null && quest.hint == "bld_" + def.id && quest.goals.Count > 0 && quest.goals[0].stat == "facility." + def.id;
                if (questBuild) Hud.Reg("btn_main", b);
                if (blocker != null) { var wt = U.Txt(card.transform, L.T(blocker), 32, Theme.Warn, TextAnchor.MiddleCenter); wt.Le(-1, 40); }
                else if (!C.CanPay(cost)) { var wt = U.Txt(card.transform, L.T("Not enough resources"), 32, Theme.Bad, TextAnchor.MiddleCenter); wt.Le(-1, 40); }
            }
        }

        void DoUpgrade(UpgradeDef def)
        {
            if (C.Upgrades.Start(def)) { Audio.Play("build"); Tw.Punch(Root, 0.01f, 0.2f); if (App.I) App.I.SaveNow(); Redraw(); }
        }

        // ------------------------------------------------------------------------------------
        void BuildTavern(RectTransform p)
        {
            var tabs = U.Rt(p, "tabs"); tabs.Le(-1, 92); U.HBox(tabs, 8, null, TextAnchor.MiddleCenter, true, true, true, true);
            bool den = C.Ranks.Index >= 1 || C.Content.Units.Any(u => C.MeetsAll(u.req));
            U.Btn(tabs, L.T("Recruit"), sub == 0 ? Theme.Accent : Theme.PanelLight, () => { sub = 0; Redraw(); }, 300, 88, 38).Le(-1, 88, 1);
            U.Btn(tabs, L.T("Beast Den"), sub == 1 ? Theme.Accent : Theme.PanelLight, () => { if (!den) { Toasts.Show(L.T("Reach Rank E to tame monsters")); return; } sub = 1; Redraw(); }, 300, 88, 38, den ? null : Icons.Get("lock")).Le(-1, 88, 1);
            if (sub == 0) TavernRecruit(p); else TavernDen(p);
        }

        void TavernRecruit(RectTransform p)
        {
            C.Recruiter.EnsureAffordableRecruit();
            Hud.Targets.Remove("btn_hire");
            var hdr = U.Rt(p, "hdr"); hdr.Le(-1, 92); U.HBox(hdr, 10, null, TextAnchor.MiddleCenter, true, true, false, true);
            var cap = U.Box(hdr, Theme.Inset, C.Roster.Full ? Theme.Red : Theme.Black, "cap"); cap.Le(250, 92);
            U.Icon(cap.transform, Icons.Get("heroes"), 56).At(0, 0.5f, 12, 0, 56, 56, 0, 0.5f); U.Txt(cap.transform, $"{C.Roster.Count}/{C.Roster.Capacity}", 48, C.Roster.Full ? Theme.Bad : Theme.Text, TextAnchor.MiddleCenter, true).Fill(70, 8, 0, 4);
            var rt = U.Txt(hdr, "", 30, Theme.TextDim, TextAnchor.MiddleLeft); rt.Le(-1, 80, 1); live.Add((rt, () => L.T("New faces in {0}", Util.Dur(C.Recruiter.RefreshAt - Util.Now))));
            bool free = C.Recruiter.FreeRefreshReady;
            var rb = U.Btn(hdr, free ? L.T("Free refresh") : L.T("Refresh"), free ? Theme.Green : Theme.PurpleDark, () => { if (C.Recruiter.ManualRefresh(!free)) { Audio.Play("swoosh"); Redraw(); } else Toasts.Show(L.T("Not enough gems")); }, 270, 92, 30, free ? null : Icons.Get("gem")); rb.Le(270, 92);
            if (!free) rb.label.text = C.Recruiter.RefreshGemCost.ToString();
            foreach (var cand in C.Recruiter.Pool.ToList())
            {
                var u = cand; var rc = Art.RarityColor(C, u.rarity); var st = C.Roster.Stats(u);
                var card = U.Box(p, Theme.Panel, Color.Lerp(Theme.Black, rc, 0.7f), "cand"); card.Le(-1, 330, 1);
                var pb = U.Box(card.transform, Color.Lerp(Theme.Inset, rc, 0.2f), rc, "pb"); pb.At(0, 0.5f, 16, 0, 200, 290, 0, 0.5f);
                var pic = W.Portrait(pb.transform, u.sprite, 190); pic.transform.parent.GetComponent<RectTransform>().At(0.5f, 0, 0, 24, 170, 190, 0.5f, 0);
                W.ClassBadge(pb.transform, u.classId, 52).At(0, 1, 6, -6, 52, 52, 0, 1);
                var nm = U.Txt(card.transform, u.name, 44, Theme.Text, TextAnchor.MiddleLeft, true); nm.At(0, 1, 236, -14, 520, 54, 0, 1);
                var meta = U.Rt(card.transform, "meta"); meta.At(0, 1, 236, -72, 600, 44, 0, 1); U.HBox(meta, 8, null, TextAnchor.MiddleLeft, false, false);
                U.Chip(meta, L.T(C.Content.Rarity(u.rarity).name), Color.Lerp(Theme.Inset, rc, 0.6f), 26, Color.white, 14, 42); U.Chip(meta, L.T(C.Roster.ClassName(u)), Theme.GreyDark, 26, Color.white, 14, 42); U.Chip(meta, "Lv " + u.level, Theme.AccentDark, 26, Color.white, 14, 42);
                var stats = U.Rt(card.transform, "stats"); stats.At(0, 1, 236, -124, 600, 40, 0, 1); U.HBox(stats, 16, null, TextAnchor.MiddleLeft, false, false);
                foreach (var sd in C.Content.Stats.Take(4)) { var cc = U.Rt(stats, "s"); U.HBox(cc, 4, null, TextAnchor.MiddleLeft, false, false); U.Icon(cc, Art.Get(sd.icon), 34).Le(34, 34); var tx = U.Txt(cc, Util.Num(st[sd.id]), 30, Theme.Text, TextAnchor.MiddleLeft, true); tx.Le(tx.preferredWidth + 4, 36); cc.Le(34 + 4 + tx.preferredWidth + 8, 38); }
                var tr = U.Rt(card.transform, "traits"); tr.StretchX(0, 20, 104, 236, 288); U.VBox(tr, 4, null, TextAnchor.LowerLeft, true, true, true, false);
                if (u.traits.Count == 0) { var nt = U.Txt(tr, L.T("No special traits"), 26, Theme.TextDim, TextAnchor.MiddleLeft); nt.Le(-1, 32); }
                foreach (var tid in u.traits.Take(2)) { var t = C.Content.TraitMap[tid]; var tl = U.Txt(tr, $"<color=#{ColorUtility.ToHtmlStringRGB(t.good ? Theme.Good : Theme.Warn)}>{L.T(t.name)}</color>  <size=24>{L.T(t.desc)}</size>", 28, Theme.Text, TextAnchor.MiddleLeft); tl.Le(-1, 48); }
                var hb = U.Btn(card.transform, L.T("Hire") + " " + Util.Num(u.price), Theme.Green, () => { if (C.Recruiter.Hire(u)) { Audio.Play("hire"); Fx.Burst(Root, Vector2.zero, Icons.Get("star"), 14, 600, null, 34); Redraw(); } }, 250, 100, 34, Art.ResIcon(C, "gold")); hb.At(1, 0, -16, 16, 250, 100, 1, 0);
                hb.Interactable = C.Wallet.Has("gold", u.price) && !C.Roster.Full;
                if (C.Roster.Full) { hb.label.text = L.T("FULL"); }
                var quest = C.Quests.CurrentMain;
                if (quest != null && quest.hint == "bld_tavern" && quest.goals.Count > 0 && (quest.goals[0].stat == "hero.count" || quest.goals[0].stat == "hero.hired") && hb.Interactable && !Hud.Targets.ContainsKey("btn_hire"))
                    Hud.Reg("btn_hire", hb);
            }
        }

        void TavernDen(RectTransform p)
        {
            W.Info(p, L.T("Tame monsters with Soul Seals (dropped by bosses and contracts). Monsters are powerful and form a Beast Pack together!"), 30, Theme.TextDim);
            foreach (var def in C.Content.Units)
            {
                var d = def; bool hired = C.Recruiter.IsHiredDef(d.id); bool open = C.MeetsAll(d.req); var rc = Art.RarityColor(C, d.rarity);
                var card = U.Box(p, open ? Theme.Panel : Theme.GreyDark, Color.Lerp(Theme.Black, rc, 0.7f), "den"); card.Le(-1, 300, 1);
                var pb = U.Box(card.transform, Color.Lerp(Theme.Inset, rc, 0.2f), rc, "pb"); pb.At(0, 0.5f, 14, 0, 200, 270, 0, 0.5f);
                var pic = W.Portrait(pb.transform, d.sprite, 230); pic.transform.parent.GetComponent<RectTransform>().At(0.5f, 0, 0, 14, 180, 230, 0.5f, 0); if (!open) pic.color = new Color(0.2f, 0.2f, 0.3f);
                var nm = U.Txt(card.transform, d.name, 44, Theme.Text, TextAnchor.MiddleLeft, true); nm.At(0, 1, 236, -12, 400, 54, 0, 1);
                var cls = C.Content.Class(d.classId); var meta = U.Rt(card.transform, "m"); meta.At(0, 1, 236, -68, 620, 42, 0, 1); U.HBox(meta, 8, null, TextAnchor.MiddleLeft, false, false);
                U.Chip(meta, L.T(C.Content.Rarity(d.rarity).name), Color.Lerp(Theme.Inset, rc, 0.6f), 26, Color.white, 14, 42); U.Chip(meta, L.T(cls.role), Theme.GreyDark, 26, Color.white, 14, 42);
                var ds = U.Txt(card.transform, L.T(d.desc), 28, Theme.TextDim, TextAnchor.UpperLeft, false, true); ds.StretchX(1, -118, 68, 236, 24);
                if (hired) { var tm = U.Chip(card.transform, L.T("Tamed"), Theme.GreenDark, 34, Color.white, 24, 70); tm.At(1, 0, -20, 20, 200, 70, 1, 0); }
                else if (!open) { var rq = U.Txt(card.transform, string.Join("\n", d.req.Select(r => ReqText(r))), 28, Theme.Bad, TextAnchor.MiddleLeft); rq.At(0, 0, 236, 20, 400, 80, 0, 0); U.Icon(card.transform, Icons.Get("lock"), 60).At(1, 0, -30, 30, 60, 60, 1, 0); }
                else
                {
                    var cr = W.CostRow(card.transform, d.cost, 32, 40, TextAnchor.MiddleLeft); cr.R().At(0, 0, 236, 20, 420, 50, 0, 0);
                    var tb = U.Btn(card.transform, L.T("TAME"), Theme.Purple, () => { if (C.Recruiter.HireDef(d)) { Audio.Play("hire"); Fx.Burst(Root, Vector2.zero, Icons.Get("star"), 20, 700, null, 34); Redraw(); } }, 200, 90, 40); tb.At(1, 0, -16, 16, 200, 90, 1, 0);
                    tb.Interactable = C.CanPay(d.cost) && !C.Roster.Full;
                }
            }
        }

        // ------------------------------------------------------------------------------------
        void BuildBlacksmith(RectTransform p)
        {
            var tabs = U.Rt(p, "tabs"); tabs.Le(-1, 92); U.HBox(tabs, 8, null, TextAnchor.MiddleCenter, true, true, true, true);
            U.Btn(tabs, L.T("Forge"), sub == 0 ? Theme.Accent : Theme.PanelLight, () => { sub = 0; Redraw(); }, 300, 88, 38).Le(-1, 88, 1);
            U.Btn(tabs, L.T("Enhance"), sub == 1 ? Theme.Accent : Theme.PanelLight, () => { sub = 1; Redraw(); }, 300, 88, 38).Le(-1, 88, 1);
            if (sub == 1)
            {
                W.Info(p, L.T("Choose a piece of gear to enhance. Higher enhancement raises its stats by {0}% per level.", (int)(C.Content.Bal("enh.mult", 0.12) * 100)), 30, Theme.TextDim);
                var gear = C.Inventory.Gear.OrderByDescending(g => !string.IsNullOrEmpty(g.owner)).ThenByDescending(g => C.Content.Rarities.FindIndex(r => r.id == g.rarity)).ToList();
                if (gear.Count == 0) { W.Info(p, L.T("No gear to enhance yet."), 34); return; }
                var grid = U.Rt(p, "grid"); U.Grid(grid, new Vector2(182, 182), new Vector2(12, 12), 5, null, TextAnchor.UpperCenter); grid.Fit();
                foreach (var g in gear) { var gg = g; W.ItemCell(grid, gg, 182, () => Popups.Show(new ItemPopup(gg.uid))); }
                return;
            }
            // active crafts
            var active = C.Crafting.Active.ToList(); int slots = C.Crafting.Slots;
            W.Header(p, L.T("Forge slots") + $" ({active.Count}/{slots})", Icons.Get("hammer"));
            foreach (var t in active)
            {
                var tt = t; var rc = C.Content.RecipeMap[t.refId]; var item = C.Content.Item(rc.result);
                var row = U.Box(p, Theme.Panel, Theme.Black, "craft"); row.Le(-1, 140, 1);
                var ib = U.Box(row.transform, Theme.Inset, Theme.Black, "ib"); ib.At(0, 0.5f, 14, 0, 104, 104, 0, 0.5f); U.Icon(ib.transform, Art.ItemIcon(item), 76).At(0.5f, 0.5f, 0, 0, 76, 76);
                var nm = U.Txt(row.transform, L.T(item.name), 38, Theme.Text, TextAnchor.MiddleLeft, true); nm.StretchX(1, -14, 46, 140, 230);
                var bar = U.ProgressBar(row.transform, Theme.Accent, 500, 32, "", 22); bar.back.R().StretchX(0, 22, 32, 140, 430); bars.Add((bar, () => (float)C.Timers.Progress(tt)));
                var tm = U.Txt(row.transform, "", 32, Theme.Text, TextAnchor.MiddleRight, true); tm.At(1, 0, -230, 20, 160, 40, 1, 0); live.Add((tm, () => Util.Dur(C.Timers.Remaining(tt))));
                var sb = U.Btn(row.transform, "", Theme.PurpleDark, () => { if (C.Timers.Speedup(tt)) { Audio.Play("gem"); Redraw(); } else Toasts.Show(L.T("Not enough gems")); }, 190, 96, 34, Icons.Get("gem")); sb.At(1, 0.5f, -14, 0, 190, 96, 1, 0.5f); live.Add((sb.label, () => C.Timers.SpeedCost(tt).ToString()));
            }
            if (active.Count < slots) W.Info(p, L.T("{0} free forge slot(s)", slots - active.Count), 30, Theme.Good);
            // recipe filter
            string[] ft = { "Weapons", "Armor", "Accessories", "Potions" };
            var fr = U.Rt(p, "filter"); fr.Le(-1, 84); U.HBox(fr, 8, null, TextAnchor.MiddleCenter, true, true, true, true);
            for (int i = 0; i < 4; i++) { int k = i; U.Btn(fr, L.T(ft[i]), forgeFilter == i ? Theme.Accent : Theme.PanelLight, () => { forgeFilter = k; Redraw(); }, 200, 78, 30).Le(-1, 78, 1); }
            string[] types = { "weapon", "armor", "accessory", "consumable" };
            var recs = C.Content.Recipes.Where(r => C.Content.Item(r.result).type == types[forgeFilter]).OrderBy(r => C.Content.Item(r.result).tier).ThenBy(r => C.Content.Item(r.result).family).ToList();
            foreach (var r in recs)
            {
                var rr = r; var item = C.Content.Item(r.result); bool unlocked = C.Crafting.Unlocked(r);
                var row = U.Box(p, unlocked ? Theme.Panel : Theme.GreyDark, Theme.Black, "rec"); row.Le(-1, 190, 1);
                var ib = U.Box(row.transform, Theme.Inset, Theme.Black, "ib"); ib.At(0, 0.5f, 14, 0, 120, 120, 0, 0.5f); U.Icon(ib.transform, Art.ItemIcon(item), 90).At(0.5f, 0.5f, 0, 0, 90, 90);
                var nm = U.Txt(row.transform, item.name + (r.n > 1 ? " x" + r.n : ""), 38, unlocked ? Theme.Text : Theme.TextDim, TextAnchor.MiddleLeft, true); nm.At(0, 1, 150, -12, 520, 46, 0, 1);
                string info = item.IsGear ? $"T{item.tier}  " + string.Join(" ", item.stats.Select(s => $"{s.id.ToUpper()}+{Util.Num(s.v)}")) : L.T(item.desc);
                var inf = U.Txt(row.transform, info, 26, Theme.TextDim, TextAnchor.MiddleLeft); inf.At(0, 1, 150, -58, 560, 34, 0, 1);
                if (unlocked) { var cr = W.CostRow(row.transform, r.cost, 28, 36, TextAnchor.MiddleLeft); cr.R().At(0, 0, 150, 14, 560, 44, 0, 0); }
                else { var lt = U.Txt(row.transform, string.Join(", ", r.req.Select(q => ReqText(q))), 28, Theme.Bad, TextAnchor.MiddleLeft); lt.At(0, 0, 150, 14, 560, 44, 0, 0); }
                var cb = U.Btn(row.transform, unlocked ? L.T("CRAFT") : "", unlocked ? Theme.Green : Theme.GreyDark, () => { if (C.Crafting.Craft(rr)) { Audio.Play("forge"); Redraw(); } else Toasts.Show(L.T(C.Crafting.Active.Count() >= C.Crafting.Slots ? "All forge slots are busy" : "Not enough resources")); }, 210, 96, 36, unlocked ? null : Icons.Get("lock")); cb.At(1, 0.5f, -14, 10, 210, 96, 1, 0.5f);
                cb.Interactable = unlocked && C.Crafting.CanCraft(r);
                var tm = U.Txt(row.transform, Util.Dur(C.Crafting.CraftTime(r)), 26, Theme.TextDim, TextAnchor.MiddleCenter); tm.At(1, 0, -14, 6, 210, 30, 1, 0);
            }
        }

        // ------------------------------------------------------------------------------------
        void BuildStation(RectTransform p, StationDef st, string cta)
        {
            if (st == null) return;
            int slots = C.Stations.Slots(st); var active = C.Stations.Active(st).ToList();
            W.Header(p, L.T(st.kind == "train" ? "Training slots" : "Healing beds") + $" ({active.Count}/{slots})", Icons.Get(st.kind == "train" ? "star" : "hp"));
            W.Info(p, st.kind == "train" ? L.T("Training grants a full level of XP and works while you are away.") : L.T("Wounded heroes heal here much faster than resting. Potions work instantly."), 30, Theme.TextDim);
            for (int i = 0; i < slots; i++)
            {
                var t = i < active.Count ? active[i] : null;
                var row = U.Box(p, Theme.Panel, t != null ? Theme.Accent : Theme.Black, "slot", t == null); row.Le(-1, 170, 1);
                if (t != null)
                {
                    var u = t.units.Count > 0 ? C.Roster.Get(t.units[0]) : null; var tt = t;
                    if (u != null) { var pic = W.Portrait(row.transform, u.sprite, 130); pic.transform.parent.GetComponent<RectTransform>().At(0, 0.5f, 16, -4, 116, 130, 0, 0.5f); }
                    var nm = U.Txt(row.transform, u != null ? u.name : L.T("Hero unavailable"), 40, Theme.Text, TextAnchor.MiddleLeft, true); nm.At(0, 1, 150, -12, 500, 50, 0, 1);
                    var bar = U.ProgressBar(row.transform, st.kind == "train" ? Theme.Blue : Theme.Green, 480, 34, "", 22); bar.back.R().At(0, 0, 150, 56, 480, 34, 0, 0); bars.Add((bar, () => (float)C.Timers.Progress(tt)));
                    var tm = U.Txt(row.transform, "", 34, Theme.Text, TextAnchor.MiddleLeft, true); tm.At(0, 0, 150, 14, 300, 40, 0, 0); live.Add((tm, () => Util.Dur(C.Timers.Remaining(tt))));
                    var sb = U.Btn(row.transform, "", Theme.PurpleDark, () => { if (C.Timers.Speedup(tt)) { Audio.Play("gem"); Redraw(); } else Toasts.Show(L.T("Not enough gems")); }, 200, 96, 34, Icons.Get("gem")); sb.At(1, 0.5f, -14, 0, 200, 96, 1, 0.5f); live.Add((sb.label, () => C.Timers.SpeedCost(tt).ToString()));
                }
                else
                {
                    var rb = row.gameObject.AddComponent<UButton>(); rb.bg = row; rb.noTint = true; rb.onClick = () => Popups.Show(new HeroPickPopup(L.T(cta), u => C.Stations.Blocker(st, u), u => { if (C.Stations.Start(st, u)) { Audio.Play(st.kind == "train" ? "equip" : "heal"); Redraw(); } },
                        u => Util.Dur(C.Stations.Duration(st, u)) + (st.kind == "train" ? "  " + Util.Num(C.Stations.Cost(st, u)[0].n) + "g" : "")));
                    U.Icon(row.transform, Icons.Get("plus"), 76, new Color(1, 1, 1, 0.6f)).At(0.5f, 0.6f, 0, 0, 76, 76);
                    var tx = U.Txt(row.transform, L.T(cta), 36, Theme.TextDim); tx.At(0.5f, 0.2f, 0, 0, 700, 44);
                    if (i == 0 && FacilityId == "training" && active.Count == 0) Hud.Reg("slot_free", row);
                }
            }
            if (st.kind == "heal")
            {
                var hurt = C.Roster.All.Where(u => C.Roster.HpFrac(u) < 0.99 && !C.Roster.IsBusy(u)).OrderBy(u => C.Roster.HpFrac(u)).ToList();
                W.Header(p, L.T("Injured heroes"), Icons.Get("hp"));
                if (hurt.Count == 0) W.Info(p, L.T("Everyone is in great shape!"), 34, Theme.Good);
                foreach (var u in hurt.Take(8)) { var uu = u; var c = W.HeroCard(p, uu, 1000, 200, () => { if (C.Stations.Free(st) > 0 && C.Stations.Start(st, uu)) { Audio.Play("heal"); Redraw(); } else Toasts.Show(L.T("No free bed")); }); }
            }
        }

        void BuildBarracks(RectTransform p)
        {
            W.Header(p, L.T("Guild capacity"), Icons.Get("heroes"));
            Stat(p, "heroes", L.T("Roster"), $"{C.Roster.Count}/{C.Roster.Capacity}");
            Stat(p, "bag", L.T("Bag space"), $"{C.Inventory.Used}/{C.Inventory.Capacity}");
            Stat(p, "sword", L.T("Party size"), C.Missions.PartySize.ToString());
            Stat(p, "board", L.T("Expedition parties"), C.Missions.ExpeditionSlots.ToString());
            W.Header(p, L.T("Promotion"), Icons.Get("star"));
            var eligible = C.Roster.All.Where(u => C.Roster.CanPromote(u)).ToList();
            if (eligible.Count == 0) W.Info(p, L.T("Heroes at their level cap can be promoted for more power."), 30, Theme.TextDim);
            foreach (var u in eligible) { var uu = u; W.HeroCard(p, uu, 1000, 200, () => Popups.Show(new PromotePopup(uu.uid))); }
        }

        void Stat(RectTransform p, string icon, string label, string value)
        {
            var row = U.Box(p, Theme.PanelDark, Theme.Black, "stat"); row.Le(-1, 92, 1);
            U.Icon(row.transform, Icons.Get(icon), 56).At(0, 0.5f, 20, 0, 56, 56, 0, 0.5f);
            var l = U.Txt(row.transform, label, 38, Theme.Text, TextAnchor.MiddleLeft, true); l.At(0, 0.5f, 94, 0, 560, 60, 0, 0.5f);
            var v = U.Txt(row.transform, value, 44, Theme.Gold, TextAnchor.MiddleRight, true); v.At(1, 0.5f, -24, 0, 300, 60, 1, 0.5f);
        }

        void BuildTreasury(RectTransform p)
        {
            W.Header(p, L.T("Vault"), Icons.Get("coin"));
            var card = U.Box(p, Theme.PanelDark, Theme.Gold, "vault"); card.Le(-1, 330, 1);
            U.Icon(card.transform, Icons.Get("coin"), 120).At(0.5f, 1, 0, -18, 120, 120, 0.5f, 1);
            var amt = U.Txt(card.transform, "", 72, Theme.Gold, TextAnchor.MiddleCenter, true); amt.At(0.5f, 1, 0, -146, 900, 80, 0.5f, 1); live.Add((amt, () => Util.Num(C.Vault.Pending)));
            var cap = U.ProgressBar(card.transform, Theme.Accent, 780, 36, "", 24); cap.back.R().At(0.5f, 0, 0, 100, 780, 36, 0.5f, 0); bars.Add((cap, () => C.Vault.Capacity > 0 ? (float)(C.Vault.Pending / C.Vault.Capacity) : 0));
            var ct = U.Txt(card.transform, "", 30, Theme.TextDim, TextAnchor.MiddleCenter); ct.At(0.5f, 0, 0, 56, 900, 36, 0.5f, 0); live.Add((ct, () => L.T("Capacity: {0} gold ({1}h)", Util.Num(C.Vault.Capacity), C.Vault.CapHours.ToString("0.#"))));
            var cb = U.Btn(card.transform, L.T("COLLECT"), Theme.Green, () => { double g = C.Vault.Collect(); if (g >= 1) { Audio.Play("coin"); Fx.Burst(Root, new Vector2(0, 100), Icons.Get("coin"), 16, 600, null, 36); Redraw(); } }, 360, 76, 40); cb.At(0.5f, 0, 0, 8, 360, 76, 0.5f, 0);
            cb.gameObject.SetActive(false); cb.Interactable = true; vaultBtn = cb; Hud.Reg("btn_collect", cb);
            Stat(p, "coin", L.T("Income"), L.T("{0} gold / hour", Util.Num(C.Vault.RatePerHour)));
            Stat(p, "clock", L.T("Offline limit"), (C.Mods.Apply("offline.cap_hours", C.Content.Bal("offline.cap", 8))).ToString("0.#") + "h");
            W.Info(p, L.T("Gold accumulates even while the game is closed, up to the vault's capacity. Visit often!"), 30, Theme.TextDim);
        }
        UButton vaultBtn;

        void BuildLibrary(RectTransform p)
        {
            int slots = C.Upgrades.Slots("research"), busy = C.Upgrades.Busy("research");
            W.Header(p, L.T("Research") + $" ({busy}/{slots})", Icons.Get("scroll"));
            foreach (var def in C.Content.Upgrades.Where(u => u.kind == "research"))
            {
                var d = def; int lv = C.Upgrades.Level(d.id); bool unlocked = C.Upgrades.Unlocked(d); var task = C.Upgrades.Pending(d.id);
                var row = U.Box(p, unlocked ? Theme.Panel : Theme.GreyDark, task != null ? Theme.Accent : Theme.Black, "res"); row.Le(-1, 250, 1);
                var ib = U.Box(row.transform, Theme.Inset, Theme.Black, "ib"); ib.At(0, 1, 16, -16, 100, 100, 0, 1); U.Icon(ib.transform, Art.Get(d.icon), 70).At(0.5f, 0.5f, 0, 0, 70, 70);
                var nm = U.Txt(row.transform, L.T(d.name) + $"  <size=30><color=#ffd34e>{lv}/{d.max}</color></size>", 40, unlocked ? Theme.Text : Theme.TextDim, TextAnchor.MiddleLeft, true); nm.At(0, 1, 132, -12, 640, 50, 0, 1);
                var ds = U.Txt(row.transform, L.T(d.desc), 28, Theme.TextDim, TextAnchor.UpperLeft, false, true); ds.At(0, 1, 132, -62, 640, 70, 0, 1);
                if (task != null)
                {
                    var tt = task; var bar = U.ProgressBar(row.transform, Theme.Accent, 560, 30, "", 20); bar.back.R().At(0, 0, 24, 54, 560, 30, 0, 0); bars.Add((bar, () => (float)C.Timers.Progress(tt)));
                    var tm = U.Txt(row.transform, "", 32, Theme.Text, TextAnchor.MiddleLeft, true); tm.At(0, 0, 24, 12, 300, 38, 0, 0); live.Add((tm, () => Util.Dur(C.Timers.Remaining(tt))));
                    var sb = U.Btn(row.transform, "", Theme.PurpleDark, () => { if (C.Timers.Speedup(tt)) { Audio.Play("gem"); Redraw(); } else Toasts.Show(L.T("Not enough gems")); }, 220, 96, 34, Icons.Get("gem")); sb.At(1, 0, -14, 14, 220, 96, 1, 0); live.Add((sb.label, () => C.Timers.SpeedCost(tt).ToString()));
                }
                else if (!unlocked) { var rq = U.Txt(row.transform, string.Join(", ", d.req.Select(r => ReqText(r))), 28, Theme.Bad, TextAnchor.MiddleLeft); rq.At(0, 0, 24, 20, 600, 40, 0, 0); U.Icon(row.transform, Icons.Get("lock"), 60).At(1, 0, -30, 24, 60, 60, 1, 0); }
                else if (lv >= d.max) { var mx = U.Chip(row.transform, "MAX", Theme.GreenDark, 34, Color.white, 24, 66); mx.At(1, 0, -20, 24, 140, 66, 1, 0); }
                else
                {
                    var cost = C.Upgrades.Cost(d, lv + 1); var cr = W.CostRow(row.transform, cost, 28, 36, TextAnchor.MiddleLeft); cr.R().At(0, 0, 24, 54, 480, 40, 0, 0);
                    var tm = U.Txt(row.transform, Util.Dur(C.Upgrades.Time(d, lv + 1)), 28, Theme.TextDim, TextAnchor.MiddleLeft); tm.At(0, 0, 24, 14, 300, 36, 0, 0);
                    var b = U.Btn(row.transform, L.T("RESEARCH"), Theme.Green, () => { if (C.Upgrades.Start(d)) { Audio.Play("magic"); Redraw(); } }, 250, 96, 34); b.At(1, 0, -14, 14, 250, 96, 1, 0);
                    b.Interactable = C.Upgrades.CanStart(d);
                }
            }
        }

        // ------------------------------------------------------------------------------------
        public override void Tick()
        {
            if (Time.realtimeSinceStartupAsDouble < nextTick) return; nextTick = Time.realtimeSinceStartupAsDouble + 0.25;
            foreach (var (t, f) in live) if (t) t.text = f();
            foreach (var (b, f) in bars) if (b != null && b.fill) b.Value = f();
            int lv = C.Upgrades.Level(FacilityId); int busy = C.Timers.Tasks.Count;
            if (lv != lastLevel || busy != lastBusy) { if (lastBusy != -1) Redraw(); lastBusy = busy; }
            if (FacilityId == "tavern" && (C.Recruiter.RefreshAt != shownRecruitRefresh || C.Recruiter.FreeRefreshReady != shownFreeRefresh)) Redraw();
            if (vaultBtn) vaultBtn.gameObject.SetActive(C.Vault.Pending >= 1);
        }
    }
}
