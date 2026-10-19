using System;
using System.Collections.Generic;
using System.Linq;
using ManagerKit;
using ManagerKit.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Emberhall
{
    public static class BoardState { public static int Tab; public static string Region = "r1"; public static List<string> LastParty = new List<string>(); }

    public class BoardScreen : UiScreen
    {
        GameContext C => App.Ctx;
        public override string Id => "Board";
        double nextTick;
        double shownBoardRefresh;
        int shownReturns;
        readonly List<(Text t, Func<string> f)> live = new List<(Text, Func<string>)>();
        readonly List<(U.Bar b, Func<float> f)> liveBars = new List<(U.Bar, Func<float>)>();
        public override void OnEnter() { Audio.PlayMusic("town"); }

        public override void Build(RectTransform root)
        {
            live.Clear(); liveBars.Clear();
            shownBoardRefresh = C.Missions.BoardRefreshAt;
            shownReturns = C.Missions.Expeditions.Count(x => x.done);
            W.PageBackdrop(root);
            var col = U.Rt(root, "col"); col.Fill(); U.VBox(col, 0, null, TextAnchor.UpperCenter, true, true, true, false);
            // tabs
            var tabs = U.Rt(col, "tabs"); tabs.Le(-1, 148); U.HBox(tabs, 8, new RectOffset(14, 14, 12, 4), TextAnchor.MiddleCenter, true, true, true, true);
            string[] names = { "Campaign", "Contracts", "Endless Spire" }; bool[] lockd = { false, false, C.Ranks.Index < 2 };
            for (int i = 0; i < 3; i++) { int k = i; var b = U.Btn(tabs, L.T(names[i]) , BoardState.Tab == i ? Theme.Accent : Theme.PanelLight, () => { if (lockd[k]) { Toasts.Show(L.T("Reach Rank D to unlock")); return; } BoardState.Tab = k; Redraw(); }, 300, 132, 34, lockd[i] ? Icons.Get("lock") : null); b.Le(-1, 132, 1); }
            var content = U.Rt(col, "content"); content.Le(-1, -1, 1, 1);
            var sc = MakeScroll(content, 14, new RectOffset(18, 18, 14, 40));
            var journey = C.Get<Journey>();
            var stamina = W.TextButton(sc.content, "", () => Popups.Show(new JourneyPopup()), 36, Theme.GreenDark); stamina.Le(-1, 86);
            Func<string> energyLine = () => StoryScript.Pick($"Stamina  {journey.Energy}/{Journey.Capacity}  ·  Journey guide", $"Stamina  {journey.Energy}/{Journey.Capacity}  ·  Panduan");
            stamina.label.text = energyLine(); live.Add((stamina.label, energyLine)); Hud.Reg("btn_journey", stamina);
            BuildRankCard(sc.content);
            if (BoardState.Tab == 0) BuildCampaign(sc.content); else if (BoardState.Tab == 1) BuildContracts(sc.content); else BuildTower(sc.content);
        }

        // ------------------------------------------------------------------------------
        void BuildRankCard(RectTransform parent)
        {
            var nx = C.Ranks.Next; var cur = C.Ranks.Current;
            var card = U.Box(parent, Theme.PanelDark, Theme.Gold, "rank", true); card.Le(-1, 142, 1);
            var cb = card.gameObject.AddComponent<UButton>(); cb.bg = card; cb.noTint = true; cb.onClick = () => Popups.Show(new RankPopup());
            var big = U.Txt(card.transform, cur.id, 96, Theme.Hex(cur.color), TextAnchor.MiddleCenter, true); big.At(0, 0.5f, 20, 0, 110, 110, 0, 0.5f);
            if (nx == null) { var t = U.Txt(card.transform, L.T("Maximum rank - a living legend!"), 38, Theme.Gold, TextAnchor.MiddleLeft, true); t.At(0, 0.5f, 150, 0, 800, 60, 0, 0.5f); return; }
            var tt = U.Txt(card.transform, C.Ranks.CanPromote ? L.T("Ready to promote to Rank {0}!", nx.id) : C.Ranks.FameReached ? L.T("Rank {0} exam available!", nx.id) : L.T("Next: Rank {0} - {1}", nx.id, L.T(nx.name)), 36, Theme.Text, TextAnchor.MiddleLeft, true); tt.StretchX(1, -16, 48, 150, C.Ranks.FameReached ? 284 : 24);
            var bar = U.ProgressBar(card.transform, Theme.Accent, 600, 40, $"{Util.Num(C.Ranks.Fame)} / {Util.Num(nx.fame)}", 28); bar.Value = C.Ranks.Progress; bar.back.R().StretchX(0, 22, 40, 150, C.Ranks.FameReached ? 284 : 24);
            if (C.Ranks.FameReached)
            {
                var b = U.Btn(card.transform, C.Ranks.CanPromote ? L.T("PROMOTE") : L.T("EXAM"), C.Ranks.CanPromote ? Theme.Accent : Theme.Red, () =>
                {
                    if (C.Ranks.CanPromote) { var nr = nx; if (C.Ranks.Promote()) Popups.Show(new RankUpPopup(nr)); }
                    else Popups.Show(new PartyPopup(C.Content.Mission(C.Ranks.ExamId)));
                }, 240, 100, 38); b.At(1, 0.5f, -16, 0, 240, 100, 1, 0.5f); Tw.Pulse(b.transform, 0.05f, 0.6f);
            }
        }

        // ------------------------------------------------------------------------------
        void BuildCampaign(RectTransform parent)
        {
            var regions = C.Missions.CampaignRegions.ToList();
            if (!regions.Any(r => r.id == BoardState.Region)) BoardState.Region = regions[0].id;
            // region chips
            var hs = U.ScrollH(parent, 10, new RectOffset(4, 4, 4, 4)); hs.view.parent.GetComponent<RectTransform>().Le(-1, 148, 1);
            foreach (var r in regions)
            {
                var rr = r; bool open = C.MeetsAll(r.req); bool sel = BoardState.Region == r.id;
                var b = U.Btn(hs.content, L.T(r.name), sel ? Theme.Hex(r.color) * 0.8f : Theme.PanelLight, () => { if (!open) { Toasts.Show(StoryScript.Pick("Defeat the previous region's boss and pass your guild exam to open this road.", "Kalahkan boss wilayah sebelumnya dan lulus ujian guild untuk membuka jalan ini.")); return; } BoardState.Region = rr.id; Redraw(); }, 320, 92, 32, open ? null : Icons.Get("lock"));
                b.Le(420, 132);
            }
            var reg = C.Content.RegionMap[BoardState.Region];
            // banner
            var banner = U.Box(parent, Theme.Inset, Theme.Hex(reg.color), "banner"); banner.Le(-1, 400, 1);
            var mask = banner.gameObject.AddComponent<RectMask2D>();
            GeneratedArt.Cover(banner.R(), "story_frontier");
            var caption = U.Solid(banner.transform, Theme.PanelLight, "caption"); caption.StretchX(0, 0, 166);
            var nm = U.Txt(caption.transform, L.T(reg.name), 44, Theme.TextDark, TextAnchor.MiddleCenter, true); nm.StretchX(1, -12, 66, 24, 220);
            var ds = U.Txt(caption.transform, StoryScript.Pick($"Boss mastery: {C.Get<Journey>().RegionStars(reg.id)}/{Journey.BossStars} trail stars", $"Penguasaan boss: {C.Get<Journey>().RegionStars(reg.id)}/{Journey.BossStars} bintang jalur"), 30, Theme.TextDim, TextAnchor.MiddleCenter); ds.StretchX(0, 12, 64, 24, 24);
            var stars = C.Missions.InRegion(reg.id).Sum(m => C.Missions.Stars(m.id)); int maxStars = C.Missions.InRegion(reg.id).Count() * 3;
            var sc = U.Txt(caption.transform, $"{stars}/{maxStars}", 36, Theme.BlueDark, TextAnchor.MiddleRight, true); sc.At(1, 1, -70, -20, 140, 54, 1, 1);
            U.Icon(caption.transform, Icons.Get("star"), 46).At(1, 1, -18, -24, 46, 46, 1, 1);

            bool pointerSet = false;
            foreach (var m in C.Missions.InRegion(reg.id).OrderBy(x => x.order))
            {
                var mm = m; bool unlocked = C.Missions.Unlocked(m); int st = C.Missions.Stars(m.id);
                var card = U.Box(parent, unlocked ? Theme.Panel : Theme.GreyDark, m.boss ? Theme.Red : Theme.Black, "stage"); card.Le(-1, 252, 1);
                var num = U.Box(card.transform, m.boss ? Theme.RedDark : Theme.PanelDark, Theme.Black, "num"); num.At(0, 1, 14, -14, 80, 80, 0, 1);
                var nt = U.Txt(num.transform, m.boss ? "" : (m.order % 10).ToString(), 52, Theme.Gold, TextAnchor.MiddleCenter, true); nt.Fill();
                if (m.boss) U.Icon(num.transform, Icons.Get("skull"), 58).At(0.5f, 0.5f, 0, 0, 58, 58);
                var title = U.Txt(card.transform, W.MissionName(m.name), 40, unlocked ? Theme.Text : Theme.TextDim, TextAnchor.MiddleLeft, true); title.At(0, 1, 110, -12, 640, 50, 0, 1);
                var lvl = U.Txt(card.transform, L.T("Lv {0}", m.level) + (m.boss ? "  " + W.Col(Theme.Red, L.T("BOSS")) : ""), 30, Theme.Gold, TextAnchor.MiddleLeft, true); lvl.At(0, 1, 110, -60, 400, 36, 0, 1);
                var stRow = W.Stars(card.transform, st, 3, 40); stRow.At(0, 1, 430, -62, 130, 40, 0, 1);
                // enemies
                var er = U.Rt(card.transform, "enemies"); er.At(0, 0, 20, 14, 520, 110, 0, 0); U.HBox(er, 6, null, TextAnchor.LowerLeft, false, false);
                foreach (var eid in m.waves.SelectMany(w => w.enemies).Select(e => e.id).Distinct().Take(5))
                {
                    var ed = C.Content.EnemyMap[eid]; var holder = U.Rt(er, "e"); holder.Le(ed.boss ? 100 : 74, 100);
                    var img = U.Img(holder, null, Color.white, "u"); img.At(0.5f, 0, 0, 0, 10, 10, 0.5f, 0);
                    var fr = Art.FrontFrames(ed.sprite); float sc2 = ed.sprite.StartsWith("bat:") ? 100f / Mathf.Max(1, Art.Get(ed.sprite).rect.height) * 0.85f : 2.8f;
                    SpriteAnim.On(img, fr, 4, true, sc2 * (ed.boss ? 1.25f : 1f));
                }
                er.gameObject.SetActive(unlocked);
                // rewards
                var rw = U.Rt(card.transform, "rw"); rw.At(1, 0, -20, 118, 330, 56, 1, 0); U.HBox(rw, 12, null, TextAnchor.MiddleRight, false, false);
                foreach (var r in m.rewards) W.ResChip(rw, r.id, r.n, 30, false, 38);
                if (!unlocked)
                {
                    var lk = U.Chip(card.transform, L.T("Locked"), Theme.GreyDark, 28, Theme.TextDim, 16, 62); lk.At(1, 0, -20, 20, 200, 80, 1, 0);
                    var why = U.Txt(card.transform, ReqText(m), 26, Theme.Bad, TextAnchor.UpperLeft, false, true); why.StretchX(0, 20, 84, 24, 280);
                }
                else
                {
                    bool boss = m.boss; var fb = U.Btn(card.transform, L.T(C.Missions.Cleared(m.id) ? "FIGHT" : "START"), boss ? Theme.Red : Theme.Accent, () => Popups.Show(new PartyPopup(mm)), 250, 92, 40, Icons.Get("sword")); fb.At(1, 0, -20, 20, 250, 92, 1, 0);
                    if (!pointerSet && !C.Missions.Cleared(m.id)) { Hud.Reg("btn_main", fb); pointerSet = true; }
                }
            }
        }
        string ReqText(MissionDef m)
        {
            var requirements = m.req.AsEnumerable();
            if (C.Content.RegionMap.TryGetValue(m.region, out var region)) requirements = requirements.Concat(region.req);
            return string.Join("\n", requirements.Where(r => C.Stats.Get(r.stat) < r.min).Take(2).Select(FacilityScreen.ReqText));
        }

        // ------------------------------------------------------------------------------
        void BuildContracts(RectTransform parent)
        {
            var hdr = W.Info(parent, L.T("Contracts are timed expeditions: send a party, close the app, and collect rewards later - even while offline."), 30, Theme.TextDim);
            var act = C.Missions.Expeditions.ToList();
            W.Header(parent, $"{L.T("Active expeditions")} ({act.Count}/{C.Missions.ExpeditionSlots})", Icons.Get("clock"));
            if (act.Count == 0) W.Info(parent, L.T("No active expeditions. Pick a contract below!"), 30);
            foreach (var t in act)
            {
                var tt = t; var m = JsonUtility.FromJson<MissionDef>(t.data);
                var card = U.Box(parent, Theme.Panel, tt.done ? Theme.Gold : Theme.Black, "exp"); card.Le(-1, 218, 1);
                var nm = U.Txt(card.transform, W.MissionName(m.name), 40, Theme.Text, TextAnchor.MiddleLeft, true); nm.At(0, 1, 24, -12, 640, 50, 0, 1);
                var party = U.Rt(card.transform, "party"); party.At(0, 0, 24, 14, 360, 90, 0, 0); U.HBox(party, 4, null, TextAnchor.LowerLeft, false, false);
                foreach (var uid in t.units) { var u = C.Roster.Get(uid); if (u != null) W.Portrait(party, u.sprite, 84); }
                if (tt.done)
                {
                    var cb = U.Btn(card.transform, L.T("CLAIM"), Theme.Green, () => ClaimExpedition(tt), 280, 100, 44); cb.At(1, 0.5f, -20, 0, 280, 100, 1, 0.5f); Tw.Pulse(cb.transform, 0.05f, 0.5f);
                    var rd = U.Txt(card.transform, L.T("Returned!"), 34, Theme.Good, TextAnchor.MiddleLeft, true); rd.At(0, 1, 24, -62, 400, 40, 0, 1);
                }
                else
                {
                    var bar = U.ProgressBar(card.transform, Theme.Blue, 520, 36, "", 24); bar.back.R().At(0, 1, 24, -66, 520, 36, 0, 1);
                    liveBars.Add((bar, () => (float)C.Timers.Progress(tt)));
                    var lt = U.Txt(card.transform, "", 34, Theme.Text, TextAnchor.MiddleLeft, true); lt.At(0, 1, 560, -62, 200, 44, 0, 1); live.Add((lt, () => Util.Dur(C.Timers.Remaining(tt))));
                    var sb = U.Btn(card.transform, "", Theme.PurpleDark, () => Popups.Show(new SpeedupPopup(tt)), 220, 100, 36, Icons.Get("gem")); sb.At(1, 0.5f, -20, 0, 220, 100, 1, 0.5f);
                    live.Add((sb.label, () => C.Timers.SpeedCost(tt).ToString()));
                }
            }
            W.Header(parent, L.T("Contract board"), Icons.Get("board"));
            var rf = U.Rt(parent, "refresh"); rf.Le(-1, 90); U.HBox(rf, 14, null, TextAnchor.MiddleCenter, true, true, false, true);
            var rt = U.Txt(rf, "", 32, Theme.TextDim, TextAnchor.MiddleLeft); rt.Le(-1, 60, 1); live.Add((rt, () => L.T("New contracts in {0}", Util.Dur(C.Missions.BoardRefreshAt - Util.Now))));
            var rb = U.Btn(rf, L.T("Refresh"), Theme.PurpleDark, () => { if (!C.Missions.ManualBoardRefresh()) Toasts.Show(L.T("Not enough gems")); else Redraw(); }, 280, 84, 32, Icons.Get("gem")); rb.Le(280, 84);
            rb.label.text = $"{L.T("Refresh")} {(int)C.Content.Bal("board.refresh_gems", 5)}";
            var top = C.Roster.All.Where(C.Roster.Ready).OrderByDescending(C.Roster.Power).Take(3).ToList();
            foreach (var m in C.Missions.Board)
            {
                var mm = m; double ch = top.Count > 0 ? C.Missions.SuccessChance(m, top) : 0;
                var card = U.Box(parent, Theme.Panel, m.boss ? Theme.Red : Theme.Black, "ct"); card.Le(-1, 400, 1);
                var nm = U.Txt(card.transform, W.MissionName(m.name), 40, Theme.Text, TextAnchor.MiddleLeft, true); nm.At(0, 1, 24, -12, 700, 124, 0, 1);
                var ds = U.Txt(card.transform, L.T(m.desc), 28, Theme.TextDim, TextAnchor.UpperLeft); ds.At(0, 1, 24, -146, 640, 96, 0, 1);
                var chips = U.Rt(card.transform, "chips"); chips.At(0, 1, 24, -252, 650, 56, 0, 1); U.HBox(chips, 10, null, TextAnchor.MiddleLeft, false, false);
                U.Chip(chips, Util.Dur(m.duration), Theme.BlueDark, 26, Color.white, 14, 42);
                U.Chip(chips, L.T("Power {0}", Util.Num(m.power)), Theme.GreyDark, 26, Color.white, 14, 42);
                var cc = ch >= 0.75 ? Theme.GreenDark : ch >= 0.4 ? Theme.AccentDark : Theme.RedDark;
                U.Chip(chips, $"{(int)(ch * 100)}%", cc, 26, Color.white, 14, 42);
                var rr = U.Rt(card.transform, "rw"); rr.At(0, 0, 24, 14, 600, 56, 0, 0); U.HBox(rr, 14, null, TextAnchor.MiddleLeft, false, false);
                foreach (var r in m.rewards) W.ResChip(rr, r.id, r.n, 32, false, 40);
                var rec = U.Rt(card.transform, "rec"); rec.At(1, 1, -20, -14, 230, 50, 1, 1); U.HBox(rec, 6, null, TextAnchor.MiddleRight, false, false);
                foreach (var cid in m.recommend.Take(4)) W.ClassBadge(rec, cid, 46);
                bool free = C.Missions.Expeditions.Count() < C.Missions.ExpeditionSlots;
                var sb = U.Btn(card.transform, L.T("SEND"), free ? Theme.Green : Theme.GreyDark, () => { if (!free) { Toasts.Show(L.T("All expedition parties are away")); return; } Popups.Show(new PartyPopup(mm)); }, 220, 92, 40, Icons.Get("arrow_r")); sb.At(1, 0, -20, 20, 220, 92, 1, 0);
                if (free && m == C.Missions.Board[0]) Hud.Reg("btn_main", sb);
            }
        }

        void ClaimExpedition(TimedTask t)
        {
            var r = C.Missions.ClaimExpedition(t); Popups.Show(new ExpeditionResultPopup(r)); Redraw();
        }

        // ------------------------------------------------------------------------------
        void BuildTower(RectTransform parent)
        {
            int best = C.Missions.TowerBest; int next = best + 1; var m = C.Missions.TowerMission(next);
            var head = U.Box(parent, Theme.PanelLight, Theme.Gold, "tower"); head.Le(-1, 460, 1);
            head.gameObject.AddComponent<RectMask2D>(); GeneratedArt.Cover(head.R(), "story_frontier");
            var badge = U.Box(head.transform, Theme.PanelLight, Theme.Gold, "floor_badge"); badge.At(.5f, 1, 0, -24, 300, 292, .5f, 1);
            var fl = U.Txt(badge.transform, L.T("FLOOR"), 38, Theme.BlueDark, TextAnchor.MiddleCenter, true); fl.StretchX(1, -16, 60, 12, 12);
            var big = U.Txt(badge.transform, next.ToString(), 126, Theme.Gold, TextAnchor.MiddleCenter, true); big.StretchX(0, 24, 188, 12, 12);
            var caption = U.Solid(head.transform, Theme.PanelLight, "caption"); caption.StretchX(0, 0, 112);
            var rec = U.Txt(caption.transform, L.T("Best: floor {0}  -  Enemy level {1}", best, m.level) + (m.boss ? "  -  " + W.Col(Theme.Red, L.T("BOSS")) : ""), 32, Theme.Text, TextAnchor.MiddleCenter, true); rec.Fill(24, 24, 12, 12);
            W.Info(parent, L.T("Each floor is harder than the last. Rewards include rare gear, Arcane Essence and lots of Fame."), 30, Theme.TextDim);
            var er = U.Rt(parent, "enemies"); er.Le(-1, 130); U.HBox(er, 18, null, TextAnchor.LowerCenter, false, false);
            foreach (var eid in m.waves.SelectMany(w => w.enemies).Select(e => e.id).Distinct().Take(6))
            {
                var ed = C.Content.EnemyMap[eid]; var holder = U.Rt(er, "e"); holder.Le(100, 120); var img = U.Img(holder, null, Color.white, "u"); img.At(0.5f, 0, 0, 0, 10, 10, 0.5f, 0);
                float sc2 = ed.sprite.StartsWith("bat:") ? 110f / Mathf.Max(1, Art.Get(ed.sprite).rect.height) : 3.2f; SpriteAnim.On(img, Art.FrontFrames(ed.sprite), 4, true, sc2);
            }
            var rw = U.Rt(parent, "rw"); rw.Le(-1, 64); U.HBox(rw, 18, null, TextAnchor.MiddleCenter, false, false);
            foreach (var r in m.rewards) W.ResChip(rw, r.id, r.n, 36, false, 46);
            var b = U.Btn(parent, L.T("CLIMB!"), Theme.Accent, () => Popups.Show(new PartyPopup(m)), 700, 130, 54, Icons.Get("sword")); b.Le(-1, 130); Hud.Reg("btn_main", b); Tw.Pulse(b.transform, 0.03f, 0.8f);
        }

        public override void Tick()
        {
            if (Time.realtimeSinceStartupAsDouble < nextTick) return;
            nextTick = Time.realtimeSinceStartupAsDouble + 0.25;
            foreach (var (t, f) in live) if (t) t.text = f();
            foreach (var (b, f) in liveBars) if (b != null && b.fill) b.Value = f();
            if (BoardState.Tab == 1 && (C.Missions.Expeditions.Count(x => x.done) != shownReturns || C.Missions.BoardRefreshAt != shownBoardRefresh)) Redraw();
        }
    }

    // =====================================================================================
    public class SpeedupPopup : Popup
    {
        readonly TimedTask t; public SpeedupPopup(TimedTask task) { t = task; }
        public override string Title => L.T("Speed Up");
        public override float Height => 620;
        public override bool UseScroll => false;
        public override bool HasFooter => true;
        Text rem; Text cost;
        public override void Build(RectTransform body)
        {
            var C = App.Ctx;
            var col = U.Rt(body, "c"); col.Fill(20, 20, 20, 20); U.VBox(col, 20, null, TextAnchor.MiddleCenter, true, true, true, false);
            rem = U.Txt(col, "", 60, Theme.Text, TextAnchor.MiddleCenter, true); rem.Le(-1, 90);
            W.Info(col, L.T("Spend gems to finish this right away."), 36);
        }
        public override void Tick() { if (rem) rem.text = Util.Dur(App.Ctx.Timers.Remaining(t)); if (cost) cost.text = App.Ctx.Timers.SpeedCost(t).ToString(); if (App.Ctx.Timers.Remaining(t) <= 0) Close(); }
        public override void BuildFooter(RectTransform f)
        {
            var C = App.Ctx;
            var b = U.Btn(f, "", Theme.PurpleDark, () => { if (C.Timers.Speedup(t)) { Audio.Play("gem"); Close(); } else Toasts.Show(L.T("Not enough gems")); }, 500, 100, 44, Icons.Get("gem")); b.Le(-1, 100, 1); cost = b.label;
        }
    }

    public class ExpeditionResultPopup : Popup
    {
        readonly ExpeditionResult r; public ExpeditionResultPopup(ExpeditionResult res) { r = res; }
        public override string Title => r.outcome == 2 ? L.T("Great Success!") : r.outcome == 1 ? L.T("Success!") : L.T("Expedition Failed");
        public override float Height => 1100;
        public override bool CloseOnDim => false;
        public override void OnShown() { Audio.Play(r.outcome > 0 ? "win" : "lose"); }
        public override void Build(RectTransform body)
        {
            var C = App.Ctx;
            var t = U.Txt(body, W.MissionName(r.mission.name), 46, r.outcome > 0 ? Theme.Gold : Theme.Bad, TextAnchor.MiddleCenter, true); t.Le(-1, 70);
            var pr = U.Rt(body, "party"); pr.Le(-1, 110); U.HBox(pr, 8, null, TextAnchor.LowerCenter, false, false); foreach (var u in r.units) W.Portrait(pr, u.sprite, 100);
            W.Header(body, L.T("Rewards"));
            var rows = r.res.Select(x => new Res(x.id, x.n)).ToList();
            var rr = W.RewardRow(body, rows, 92, 30); var holder = U.Rt(body, "rwh"); holder.Le(-1, 130); rr.SetParent(holder, false); rr.anchorMin = rr.anchorMax = new Vector2(0.5f, 0.5f);
            if (r.drops.Count > 0)
            {
                W.Header(body, L.T("Loot"));
                var grid = U.Rt(body, "loot"); U.Grid(grid, new Vector2(120, 120), new Vector2(12, 12), 6, null, TextAnchor.UpperCenter); grid.Fit();
                foreach (var d in r.drops) { if (d.gear != null) W.ItemCell(grid, d.gear, 120, null); else W.StackCell(grid, d.res.id, (int)d.res.n, 120, null); }
            }
            W.Info(body, L.T("Each hero gained {0} XP.", Util.Num(r.xp)), 32);
            if (r.outcome == 0) W.Info(body, L.T("Your heroes were hurt. Visit the Infirmary!"), 32, Theme.Bad);
            var ok = U.Btn(body, L.T("Continue"), Theme.Green, Close, 600, 110, 46); ok.Le(-1, 110);
        }
    }
}
