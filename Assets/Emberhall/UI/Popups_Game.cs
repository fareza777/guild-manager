using System;
using System.Collections.Generic;
using System.Linq;
using ManagerKit;
using ManagerKit.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Emberhall
{
    // =====================================================================================
    public class SettingsPopup : Popup
    {
        public override string Title => L.T("Settings");
        public override float Height => 1380;
        public override void Build(RectTransform body)
        {
            Row(body, "Music", Icons.Get("note"), r => { U.Slide(r, Prefs.Music, v => { Prefs.Music = v; Audio.SetVolumes(Prefs.Sfx, v); }, 440, 56).track.Le(440, 56); });
            Row(body, "Sound effects", Icons.Get("speaker"), r => { U.Slide(r, Prefs.Sfx, v => { Prefs.Sfx = v; Audio.SetVolumes(v, Prefs.Music); }, 440, 56).track.Le(440, 56); });
            Row(body, "Vibration", Icons.Get("clock"), r => U.Switch(r, Prefs.Vibrate, v => { Prefs.Vibrate = v; Native.Vibration = v; if (v) Native.Haptic(); }));
            Row(body, "Tutorial hints", Icons.Get("info"), r => U.Switch(r, Prefs.Hints, v => Prefs.Hints = v));
            Row(body, "Battle effects", Icons.Get("star"), r => U.Switch(r, Prefs.BattleFx, v => Prefs.BattleFx = v));
            Row(body, "Frame rate", Icons.Get("spd"), r =>
            {
                foreach (var f in new[] { 30, 60 }) { int ff = f; var b = U.Btn(r, ff + " FPS", Prefs.Fps == ff ? Theme.Accent : Theme.PanelLight, () => { Prefs.Fps = ff; Prefs.Apply(); Redraw(); }, 190, 80, 34); b.Le(190, 80); }
            });
            Row(body, "Language", Icons.Get("globe"), r =>
            {
                foreach (var code in L.Languages) { var cc = code; var b = U.Btn(r, L.DisplayName(cc), L.Lang == cc ? Theme.Accent : Theme.PanelLight, () =>
                {
                    Prefs.Lang = cc; L.Load(cc);
                    if (!App.I.InGame) { App.I.GoTitle(); return; }
                    Popups.RedrawAll();
                    if (App.I.nav.Current is BattleScreen battle) battle.RefreshLanguage();
                    else App.I.nav.Current?.Redraw();
                }, 250, 132, 30); b.Le(250, 132); }
            });
            U.Spacer(body, 6);
            if (App.I.HasSave || App.I.InGame)
            {
                var b = U.Btn(body, L.T("Reset all progress"), Theme.RedDark, () => Popups.Confirm(L.T("Reset everything?"), L.T("This deletes your guild, heroes and items. This cannot be undone."), L.T("Delete"), () => { Close(); App.I.ResetProgress(); }, L.T("Cancel"), true), 600, 100, 40, Icons.Get("trash"));
                b.Le(-1, 100);
            }
            var v = U.Txt(body, $"Emberhall v{App.Version}  -  {L.T("Made with Unity")}", 28, Theme.TextDim); v.Le(-1, 40);
        }
        void Row(RectTransform body, string label, Sprite icon, Action<RectTransform> content)
        {
            var card = W.Card(body, 118, Theme.PanelDark); var r = card.R();
            U.HBox(r, 16, new RectOffset(20, 20, 8, 8), TextAnchor.MiddleLeft, true, true, false, false);
            U.Icon(r, icon, 56).Le(56, 56);
            var t = U.Txt(r, L.T(label), 40, Theme.Text, TextAnchor.MiddleCenter, true); t.Le(-1, 60, 1);
            var holder = U.Rt(r, "ctl"); U.HBox(holder, 14, null, TextAnchor.MiddleRight, true, true, false, false);
            content(holder);
            float controlHeight = Mathf.Max(90, holder.GetComponentsInChildren<LayoutElement>().Select(e => e.preferredHeight).DefaultIfEmpty(90).Max());
            holder.Le(-1, controlHeight); card.Le(-1, controlHeight + 24);
            holder.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        }
    }

    // =====================================================================================
    public class AboutPopup : Popup
    {
        public override string Title => L.T("About");
        public override float Height => 1360;
        public override void Build(RectTransform body)
        {
            var logoBox = U.Rt(body, "logo"); logoBox.Le(-1, 330);
            var logo = Brand.Logo(logoBox, 0.62f); logo.At(0.5f, 0.5f, 0, 0, 620, 320);
            var v = U.Txt(body, $"{L.T("Version")} {App.Version}", 36, Theme.Gold, TextAnchor.MiddleCenter, true); v.Le(-1, 50);
            W.Info(body, L.T("A pixel-art guild manager. Recruit heroes, send them on quests, forge legendary gear and rebuild the fallen Emberhall."), 34, Theme.Text);
            W.Header(body, L.T("Credits"));
            Line(body, "Game design & code", Brand.Studio);
            Line(body, "Pixel art", "Super Retro Collection + painted story and relic art");
            Line(body, "Font", "Lato by Lukasz Dziedzic (SIL OFL 1.1)");
            Line(body, "Music & SFX", "Synthesised in-game (chiptune)");
            Line(body, "Engine", "Unity");
            W.Header(body, L.T("Thank you for playing!"));
            var row = U.Rt(body, "btns"); row.Le(-1, 110); U.HBox(row, 18, null, TextAnchor.MiddleCenter, true, true, true, true);
            U.Btn(row, L.T("Share"), Theme.BlueDark, () => Native.Share(Brand.Title, Brand.ShareText), 300, 100, 38, Icons.Get("share")).Le(-1, 100, 1);
            U.Btn(row, L.T("Rate us"), Theme.PurpleDark, Native.Rate, 300, 100, 38, Icons.Get("star")).Le(-1, 100, 1);
        }
        void Line(RectTransform body, string k, string v)
        {
            var r = U.Rt(body, "line"); r.Le(-1, 180); U.VBox(r, 8, null, TextAnchor.MiddleCenter, true, true, true, false);
            var a = U.Txt(r, L.T(k), 30, Theme.TextDim); a.Le(-1, 56);
            var b = U.Txt(r, L.T(v), 34, Theme.Text, TextAnchor.MiddleCenter, false, true); b.Le(-1, 112);
        }
    }

    // =====================================================================================
    public class MenuPopup : Popup
    {
        public override string Title => L.T("Menu");
        public override float Height => 1040;
        public override void Build(RectTransform body)
        {
            var C = App.Ctx;
            Item(body, "Guild Rank", "flame", Theme.AccentDark, () => { Close(); Popups.Show(new RankPopup()); });
            Item(body, "Daily Reward", "star", Theme.PurpleDark, () => { Close(); Popups.Show(new DailyPopup()); });
            Item(body, "Settings", "gear", Theme.PanelLight, () => { Close(); Popups.Show(new SettingsPopup()); });
            Item(body, "About", "info", Theme.PanelLight, () => { Close(); Popups.Show(new AboutPopup()); });
            Item(body, "Share with friends", "share", Theme.BlueDark, () => Native.Share(Brand.Title, Brand.ShareText));
            Item(body, "Rate the game", "star", Theme.PurpleDark, Native.Rate);
            Item(body, "Save & exit to title", "arrow_l", Theme.RedDark, () => { Close(); App.I.SaveNow(); App.I.GoTitle(); });
        }
        void Item(RectTransform body, string text, string icon, Color col, Action act)
        { var b = U.Btn(body, L.T(text), col, act, 800, 112, 44, Icons.Get(icon)); b.Le(-1, 112); }
    }

    // =====================================================================================
    public class DailyPopup : Popup
    {
        public override string Title => L.T("Daily Reward");
        public override float Height => 1250;
        public override void Build(RectTransform body)
        {
            var C = App.Ctx;
            W.Info(body, L.T("Log in every day for bigger rewards! Streak: {0}", C.Daily.Streak), 38, Theme.Gold);
            int cycle = Math.Max(1, C.Content.Calendar.Count);
            int today = (Math.Max(1, C.Daily.Streak) - 1) % cycle;
            var grid = U.Rt(body, "grid"); U.Grid(grid, new Vector2(280, 250), new Vector2(18, 18), 3, null, TextAnchor.UpperCenter); grid.Fit(); grid.Le(-1, ((cycle + 2) / 3) * 268);
            for (int i = 0; i < cycle; i++)
            {
                bool isToday = i == today, past = i < today || (isToday && C.Daily.Claimed);
                var cell = U.Box(grid, isToday ? Theme.Accent : Theme.PanelDark, isToday ? Theme.Gold : Theme.Black, "day", false);
                var t = U.Txt(cell.transform, L.T("Day {0}", i + 1), 32, isToday ? Color.white : Theme.TextDim, TextAnchor.MiddleCenter, true); t.At(0.5f, 1, 0, -8, 260, 40, 0.5f, 1);
                var rw = C.Content.Calendar[i].rewards;
                var row = U.Rt(cell.transform, "rw"); row.At(0.5f, 0.43f, 0, 0, 260, 144); U.HBox(row, 6, null, TextAnchor.MiddleCenter, true, true, false, false);
                foreach (var r in rw.Take(2)) { var c = W.RewardCell(row, r.id, r.n, 76, 26); }
                if (past) { var ov = U.Solid(cell.transform, new Color(0, 0, 0, 0.5f), "done"); ov.Fill(); U.Icon(cell.transform, Icons.Get("check"), 90).At(0.5f, 0.5f, 0, 0, 90, 90); }
                if (isToday && !C.Daily.Claimed) Tw.Pulse(cell.transform, 0.04f, 0.6f);
            }
            if (!C.Daily.Claimed)
            {
                var b = U.Btn(body, L.T("CLAIM TODAY'S REWARD"), Theme.Green, () =>
                {
                    var rw = C.Daily.TodayReward.ToList(); C.Daily.Claim(); Audio.Play("gem");
                    Fx.Burst(Panel, new Vector2(0, -300), Icons.Get("star"), 20, 700, null, 40);
                    Redraw();
                }, 800, 120, 48);
                b.Le(-1, 120); Tw.Pulse(b.transform, 0.03f, 0.7f);
            }
            else W.Info(body, L.T("Come back in {0} for the next reward.", Util.Dur(C.Daily.ResetIn)), 34, Theme.TextDim);
        }
    }

    // =====================================================================================
    public class EventPopup : Popup
    {
        readonly EventDef ev; EventOutcome outcome; string picked;
        public EventPopup(EventDef e) { ev = e; }
        public override string Title => L.T(ev.title);
        public override float Height => 1250;
        public override bool CloseOnDim => false;
        public override bool ShowClose => false;
        public override void Build(RectTransform body)
        {
            var C = App.Ctx;
            var stage = U.Box(body, Theme.Inset, Theme.Black, "stage"); stage.Le(-1, 300, 1);
            var back = U.Img(stage.transform, Art.Bg("PlainB"), new Color(1, 1, 1, 0.9f), "bg"); back.Fill(8, 8, 8, 8); back.preserveAspect = false;
            var pic = W.Portrait(stage.transform, ev.sprite ?? "chara_9", 250); pic.transform.parent.GetComponent<RectTransform>().At(0.5f, 0, 0, 20, 220, 250, 0.5f, 0);
            Tw.Bob(pic.transform.parent.GetComponent<RectTransform>(), 6, 1.2f);
            var txt = U.Txt(body, L.T(ev.text), 40, Theme.Text, TextAnchor.MiddleCenter, false, true); txt.Le(-1, -1, 1);
            if (outcome == null)
            {
                for (int i = 0; i < ev.choices.Count; i++)
                {
                    int k = i; var ch = ev.choices[i];
                    bool ok = C.Events.Available(ch);
                    var b = U.Btn(body, L.T(ch.text), ok ? Theme.PanelLight : Theme.GreyDark, () => Choose(k), 800, 110, 38);
                    b.Le(-1, 110); b.Interactable = ok;
                    if (ch.cost.Count > 0 && !C.CanPay(ch.cost)) { var warn = U.Txt(body, L.T("Not enough resources"), 28, Theme.Bad); warn.Le(-1, 30); }
                }
            }
            else
            {
                var res = U.Box(body, Theme.PanelDark, Theme.Black, "result"); res.Le(-1, -1, 1);
                U.VBox(res.R(), 14, new RectOffset(24, 24, 24, 24), TextAnchor.UpperCenter, true, true, true, false);
                var t = U.Txt(res.transform, L.T(outcome.text), 40, Theme.Gold, TextAnchor.MiddleCenter, false, true); t.Le(-1, -1, 1);
                var gains = outcome.gain.ToList(); if (!string.IsNullOrEmpty(outcome.item)) gains.Add(new Res(outcome.item, 1));
                if (gains.Count > 0) { var row = W.RewardRow(res.transform, gains); row.GetComponent<LayoutElement>().flexibleWidth = 0; }
                if (outcome.hurt > 0) { var h = U.Txt(res.transform, L.T("A hero was hurt!"), 34, Theme.Bad); h.Le(-1, 44); }
                var ok = U.Btn(body, L.T("Continue"), Theme.Green, Close, 600, 110, 46); ok.Le(-1, 110);
            }
        }
        void Choose(int k)
        {
            var C = App.Ctx; var o = C.Events.Resolve(ev, k); if (o == null) return;
            outcome = o; Audio.Play(o.gain.Count > 0 ? "coin" : "pop"); Redraw();
        }
        public override bool OnBack() { if (outcome != null) Close(); return true; }
    }

    // =====================================================================================
    public class OfflinePopup : Popup
    {
        readonly OfflineReport r;
        public OfflinePopup(OfflineReport rep) { r = rep; }
        public override string Title => L.T("Welcome Back!");
        public override float Height => 1200;
        public override bool CloseOnDim => false;
        public override void Build(RectTransform body)
        {
            var C = App.Ctx;
            W.Info(body, L.T("You were away for {0}.", Util.Dur(r.awaySeconds)), 40, Theme.Text);
            var card = W.Card(body, -1, Theme.PanelDark); U.VBox(card.R(), 12, new RectOffset(24, 24, 20, 20), TextAnchor.UpperCenter, true, true, true, false); card.Le(-1, -1, 1);
            void Line(Sprite ic, string text) { var row = U.Rt(card.transform, "ln"); row.Le(-1, 70); U.HBox(row, 14, null, TextAnchor.MiddleLeft, true, true, false, false); U.Icon(row, ic, 56).Le(56, 56); var t = U.Txt(row, text, 38, Theme.Text, TextAnchor.MiddleLeft, true); t.Le(-1, 60, 1); }
            if (r.vaultGold >= 1) Line(Art.ResIcon(C, "gold"), L.T("Treasury collected {0} gold", Util.Num(r.vaultGold)));
            if (r.finishedTasks > 0) Line(Icons.Get("hammer"), L.T("{0} tasks finished", r.finishedTasks));
            if (r.expeditionsReady > 0) Line(Icons.Get("board"), L.T("{0} expeditions returned", r.expeditionsReady));
            if (r.pendingEvents > 0) Line(Icons.Get("scroll"), L.T("{0} events await you", r.pendingEvents));
            var b = U.Btn(body, r.vaultGold >= 1 ? L.T("COLLECT") : L.T("Continue"), Theme.Green, () =>
            {
                if (r.vaultGold >= 1 && C.Upgrades.Level("treasury") > 0) { C.Vault.Collect(); Audio.Play("coin"); }
                Close();
            }, 700, 120, 50);
            b.Le(-1, 120); Tw.Pulse(b.transform, 0.03f, 0.7f);
        }
    }

    // =====================================================================================
    public class RankPopup : Popup
    {
        public override string Title => L.T("Guild Rank");
        public override float Height => 1560;
        public override void Build(RectTransform body)
        {
            var C = App.Ctx; var cur = C.Ranks.Current; var nx = C.Ranks.Next;
            var head = U.Box(body, Theme.PanelDark, Theme.Gold, "head"); head.Le(-1, 300, 1);
            var sh = Brand.ShieldImg(head.transform, Theme.Hex(GuildGame.Emblems[Mathf.Clamp(C.Meta.Emblem, 0, 5)]), 180); sh.At(0, 0.5f, 30, 0, 156, 180, 0, 0.5f);
            var big = U.Txt(head.transform, cur.id, 120, Theme.Hex(cur.color), TextAnchor.MiddleCenter, true); big.At(0, 0.5f, 210, 20, 150, 150, 0, 0.5f);
            var nm = U.Txt(head.transform, L.T(cur.name), 44, Theme.Gold, TextAnchor.MiddleCenter, true); nm.StretchX(1, -26, 56, 370, 24);
            var pk = U.Txt(head.transform, L.T(cur.perk), 32, Theme.Text, TextAnchor.MiddleCenter, false, true); pk.StretchX(1, -108, 176, 370, 24);
            if (nx != null)
            {
                var bar = U.ProgressBar(body, Theme.Accent, 900, 54, $"{Util.Num(C.Ranks.Fame)} / {Util.Num(nx.fame)} Fame", 32); bar.Value = C.Ranks.Progress; bar.back.Le(-1, 54, 1);
                if (C.Ranks.CanPromote)
                {
                    var pb = U.Btn(body, L.T("PROMOTE TO RANK {0}!", nx.id), Theme.Accent, () => { var rid = nx; if (C.Ranks.Promote()) { Close(); Popups.Show(new RankUpPopup(rid)); } }, 800, 130, 50, Icons.Get("star")); pb.Le(-1, 130); Tw.Pulse(pb.transform, 0.04f, 0.6f);
                }
                else if (C.Ranks.FameReached && !C.Ranks.ExamDone)
                {
                    var ex = C.Content.Mission(C.Ranks.ExamId);
                    var eb = U.Btn(body, L.T("TAKE THE RANK {0} EXAM", nx.id), Theme.Red, () => { Close(); Popups.Show(new PartyPopup(ex)); }, 800, 130, 46, Icons.Get("sword")); eb.Le(-1, 130); Tw.Pulse(eb.transform, 0.03f, 0.7f);
                    W.Info(body, L.T("Recommended hero level: {0}+", ex.level), 30);
                }
                else W.Info(body, L.T("Earn {0} more Fame to unlock the Rank {1} exam.", Util.Num(Math.Max(0, nx.fame - C.Ranks.Fame)), nx.id), 32);
            }
            else W.Info(body, L.T("Maximum rank reached. You are a legend!"), 38, Theme.Gold);
            W.Header(body, L.T("Rank ladder"));
            for (int i = 0; i < C.Content.Ranks.Count; i++)
            {
                var r = C.Content.Ranks[i]; bool done = i <= C.Ranks.Index, now = i == C.Ranks.Index;
                var row = U.Box(body, now ? Theme.PanelLight : Theme.PanelDark, now ? Theme.Gold : Theme.Black, "r"); row.Le(-1, 200, 1);
                var badge = U.Box(row.transform, done ? Theme.Hex(r.color) * 0.55f : Theme.GreyDark, Theme.Black, "b"); badge.At(0, 0.5f, 16, 0, 96, 96, 0, 0.5f);
                var l = U.Txt(badge.transform, r.id, 64, done ? Theme.Hex(r.color) : Theme.TextDim, TextAnchor.MiddleCenter, true); l.Fill();
                var n = U.Txt(row.transform, $"{L.T(r.name)}  <size=36><color=#75614a>({Util.Num(r.fame)} Fame)</color></size>", 36, done ? Theme.Text : Theme.TextDim, TextAnchor.MiddleLeft, true); n.StretchX(1, -10, 46, 130, 94);
                var d = U.Txt(row.transform, L.T(r.perk), 28, Theme.TextDim, TextAnchor.UpperLeft, false, true); d.StretchX(1, -78, 108, 130, 94);
                if (done) U.Icon(row.transform, Icons.Get("check"), 56).At(1, 0.5f, -16, 0, 56, 56, 1, 0.5f);
            }
        }
    }

    public class RankUpPopup : Popup
    {
        readonly RankDef rank;
        public RankUpPopup(RankDef r) { rank = r; }
        public override string Title => L.T("Rank Up!");
        public override float Height => 1400;
        public override bool CloseOnDim => false;
        public override bool UseScroll => false;
        public override bool ShowClose => false;
        public override bool HasFooter => true;
        public override void OnShown() { Audio.Play("rank"); }
        public override void Build(RectTransform body)
        {
            var big = U.Txt(body, rank.id, 340, Theme.Hex(rank.color), TextAnchor.MiddleCenter, true); big.At(0.5f, 0.62f, 0, 0, 500, 400);
            Tw.Pop(big.transform, 0.8f);
            var glow = U.Img(body, Boxes.Glow, new Color(1, 0.75f, 0.2f, 0.6f), "glow"); glow.At(0.5f, 0.62f, 0, 0, 900, 900); glow.transform.SetAsFirstSibling(); Tw.Spin(glow.transform, 12);
            var nm = U.Txt(body, L.T(rank.name), 62, Theme.Gold, TextAnchor.MiddleCenter, true); nm.At(0.5f, 0.28f, 0, 20, 900, 80);
            var pk = U.Txt(body, L.T(rank.perk), 38, Theme.Text, TextAnchor.UpperCenter, false, true); pk.At(0.5f, 0.28f, 0, -72, 800, 196, 0.5f, 1);
            Tw.After(0.3f, () => { if (Panel) { Fx.Burst(Panel, new Vector2(0, 100), Icons.Get("star"), 30, 900, null, 44); } });
        }
        public override void BuildFooter(RectTransform f) { U.Btn(f, L.T("AWESOME!"), Theme.Green, Close, 600, 100, 48).Le(-1, 100, 1); }
    }

    // =====================================================================================
    public class QuestRewardPopup : Popup
    {
        readonly QuestDef q; bool claimed;
        public QuestRewardPopup(QuestDef quest) { q = quest; }
        public override string Title => L.T("Quest Complete!");
        public override float Height => 1380;
        public override bool CloseOnDim => false;
        public override void Build(RectTransform body)
        {
            var C = App.Ctx;
            var t = U.Txt(body, L.T(q.title), 56, Theme.Gold, TextAnchor.MiddleCenter, true); t.Le(-1, 80);
            if (!string.IsNullOrEmpty(q.story))
            {
                var scene = U.Box(body, Theme.PanelLight, Theme.Gold, "chapter_scene"); scene.Le(-1, 300, 1);
                scene.gameObject.AddComponent<RectMask2D>(); GeneratedArt.Cover(scene.R(), "story_frontier");
                var card = U.Box(body, Theme.Parchment, Theme.ParchmentDark, "story"); card.Le(-1, 300, 1);
                var tx = U.Txt(card.transform, L.T(q.story), 36, Theme.TextDark, TextAnchor.MiddleCenter, false, true); tx.Fill(28, 28, 24, 24);
            }
            W.Header(body, L.T("Rewards"));
            var rr = W.RewardRow(body, q.rewards, 100, 32);
            var holder = U.Rt(body, "rwh"); holder.Le(-1, 150); rr.SetParent(holder, false); rr.anchorMin = rr.anchorMax = new Vector2(0.5f, 0.5f);
            var b = U.Btn(body, claimed ? L.T("Done") : L.T("CLAIM"), Theme.Green, () =>
            {
                if (claimed) { Close(); return; }
                if (C.Quests.Claim(q)) { claimed = true; Audio.Play("gem"); Fx.Burst(Panel, new Vector2(0, -100), Icons.Get("star"), 26, 800, null, 40); Close(); }
            }, 700, 120, 52);
            b.Le(-1, 120); Tw.Pulse(b.transform, 0.03f, 0.7f);
        }
    }
}
