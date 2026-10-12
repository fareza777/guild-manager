using System;
using System.Collections.Generic;
using System.Linq;
using ManagerKit;
using ManagerKit.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Emberhall
{
    public class QuestsScreen : UiScreen
    {
        static int tab;
        GameContext C => App.Ctx;
        public override string Id => "Quests";
        readonly List<(Text t, Func<string> f)> live = new List<(Text, Func<string>)>();
        double nextTick;
        public override void OnEnter() { Audio.PlayMusic("town"); }

        public override void Build(RectTransform root)
        {
            live.Clear();
            W.PageBackdrop(root);
            var col = U.Rt(root, "col"); col.Fill(); U.VBox(col, 0, null, TextAnchor.UpperCenter, true, true, true, false);
            var tabs = U.Rt(col, "tabs"); tabs.Le(-1, 110); U.HBox(tabs, 8, new RectOffset(14, 14, 12, 4), TextAnchor.MiddleCenter, true, true, true, true);
            string[] names = { "Story", "Side", "Daily", "Journal" }; string[] types = { "main", "side", "daily", "journal" };
            for (int i = 0; i < names.Length; i++)
            {
                int k = i; int rdy = C.Content.Quests.Count(q => q.type == types[i] && C.Quests.State(q.id) == QuestSystem.Ready);
                var b = U.Btn(tabs, L.T(names[i]), tab == i ? Theme.Accent : Theme.PanelLight, () => { tab = k; Redraw(); }, 300, 94, 34); b.Le(-1, 94, 1);
                if (rdy > 0) { var bd = U.Badge(b.transform, rdy.ToString(), 44); bd.At(1, 1, -6, -6, 44, 44, 1, 1); }
            }
            var content = U.Rt(col, "content"); content.Le(-1, -1, 1, 1);
            var sc = MakeScroll(content, 14, new RectOffset(18, 18, 14, 40));
            if (tab == 0) BuildMain(sc.content); else if (tab == 1) BuildSide(sc.content); else if (tab == 2) BuildDaily(sc.content); else BuildJournal(sc.content);
        }

        void BuildJournal(RectTransform p)
        {
            var art = U.Box(p, Theme.PanelLight, Theme.Gold, "journal_art"); art.Le(-1, 370, 1);
            art.gameObject.AddComponent<RectMask2D>(); GeneratedArt.Cover(art.R(), "story_frontier");
            W.Header(p, StoryScript.Pick("The Lantern Road", "Jalan Lentera"), Icons.Get("quest"));
            W.Info(p, StoryScript.Pick("A storm scattered five beacon lenses. Every road you reopen brings their light closer to home.",
                "Badai memencarkan lima lensa suar. Setiap jalan yang dibuka membawa cahayanya lebih dekat ke rumah."), 36);
            foreach (var q in C.Content.Quests.Where(q => q.type == "main").OrderBy(q => q.order))
            {
                int state = C.Quests.State(q.id);
                if (state == QuestSystem.Locked) continue;
                var card = U.Box(p, Theme.Parchment, Theme.ParchmentDark, "journal_entry");
                U.VBox(card.R(), 18, new RectOffset(28, 28, 24, 28), TextAnchor.UpperCenter, true, true, true, false);
                card.Le(-1, -1, 1);
                var chapter = U.Txt(card.transform, L.T(q.title), 42, Theme.TextDark, TextAnchor.MiddleCenter, true, true); chapter.Le(-1, -1, 1);
                chapter.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                var status = U.Txt(card.transform, L.T(state == QuestSystem.Done ? "Completed" : "Current chapter"), 28, Theme.BlueDark, TextAnchor.MiddleCenter); status.Le(-1, 52);
                var story = U.Txt(card.transform, L.T(q.story), 36, Theme.TextDark, TextAnchor.MiddleCenter, false, true); story.Le(-1, -1, 1);
                story.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            }
        }

        void BuildMain(RectTransform p)
        {
            var all = C.Content.Quests.Where(q => q.type == "main").OrderBy(q => q.order).ToList();
            int done = all.Count(q => C.Quests.State(q.id) == QuestSystem.Done);
            var head = U.Box(p, Theme.PanelDark, Theme.Gold, "head"); head.Le(-1, 150, 1);
            var pic = W.Portrait(head.transform, "chara_3", 120); pic.transform.parent.GetComponent<RectTransform>().At(0, 0.5f, 14, -4, 108, 120, 0, 0.5f);
            var t = U.Txt(head.transform, L.T("The Road to Rank S"), 44, Theme.Gold, TextAnchor.MiddleLeft, true); t.At(0, 1, 140, -14, 800, 54, 0, 1);
            var bar = U.ProgressBar(head.transform, Theme.Accent, 760, 36, $"{done}/{all.Count}", 26); bar.Value = done / (float)all.Count; bar.back.R().At(0, 0, 140, 22, 760, 36, 0, 0);
            int shown = 0;
            foreach (var q in all)
            {
                int st = C.Quests.State(q.id);
                if (st == QuestSystem.Done) continue;
                if (shown >= 4) { break; }
                shown++;
                if (st == QuestSystem.Locked) { var lk = U.Box(p, Theme.GreyDark, Theme.Black, "lock"); lk.Le(-1, 100, 1); var lt = U.Txt(lk.transform, "???  -  " + L.T(q.title), 36, Theme.TextDim, TextAnchor.MiddleCenter); lt.Fill(92, 92, 8, 8); U.Icon(lk.transform, Icons.Get("lock"), 56).At(1, 0.5f, -24, 0, 56, 56, 1, 0.5f); }
                else QuestRow(p, q, true);
            }
            if (done > 0) { var dl = U.Txt(p, L.T("{0} chapters completed", done), 32, Theme.TextDim); dl.Le(-1, 50); }
            if (done == all.Count) W.Info(p, L.T("You completed the main story! Legend of Emberhall!"), 40, Theme.Gold);
        }

        void BuildSide(RectTransform p)
        {
            var list = C.Content.Quests.Where(q => q.type == "side" && (C.Quests.State(q.id) == QuestSystem.Active || C.Quests.State(q.id) == QuestSystem.Ready)).OrderByDescending(q => C.Quests.State(q.id) == QuestSystem.Ready).ThenBy(q => q.order).ToList();
            int done = C.Content.Quests.Count(q => q.type == "side" && C.Quests.State(q.id) == QuestSystem.Done), total = C.Content.Quests.Count(q => q.type == "side");
            W.Info(p, L.T("Optional goals with big rewards. Completed: {0}/{1}", done, total), 34, Theme.TextDim);
            if (list.Count == 0) W.Info(p, L.T("No side quests available right now. Keep playing to unlock more!"), 36);
            foreach (var q in list) QuestRow(p, q, false);
        }

        void BuildDaily(RectTransform p)
        {
            var cal = U.Box(p, Theme.PanelDark, Theme.Gold, "cal", true); cal.Le(-1, 150, 1);
            var cb = cal.gameObject.AddComponent<UButton>(); cb.bg = cal; cb.noTint = true; cb.onClick = () => Popups.Show(new DailyPopup());
            U.Icon(cal.transform, Icons.Get("star"), 90).At(0, 0.5f, 24, 0, 90, 90, 0, 0.5f);
            var t = U.Txt(cal.transform, L.T("Daily Login Reward"), 42, Theme.Gold, TextAnchor.MiddleLeft, true); t.At(0, 1, 140, -22, 700, 52, 0, 1);
            var s = U.Txt(cal.transform, C.Daily.Claimed ? L.T("Claimed! Streak: {0} days", C.Daily.Streak) : L.T("Tap to claim today's reward!"), 32, C.Daily.Claimed ? Theme.TextDim : Theme.Good, TextAnchor.MiddleLeft, true); s.At(0, 0, 140, 24, 700, 44, 0, 0);
            if (!C.Daily.Claimed) { var d = U.Dot(cal.transform, Theme.Red, 40); d.At(1, 1, -20, -20, 40, 40, 1, 1); Tw.Pulse(d.transform, 0.2f, 0.5f); }
            var tm = U.Txt(p, "", 32, Theme.TextDim, TextAnchor.MiddleCenter); tm.Le(-1, 44); live.Add((tm, () => L.T("Daily quests reset in {0}", Util.Dur(C.Daily.ResetIn))));
            var list = C.Content.Quests.Where(q => q.type == "daily" && C.Quests.State(q.id) != QuestSystem.Locked).OrderBy(q => C.Quests.State(q.id) == QuestSystem.Done).ToList();
            if (list.Count == 0) W.Info(p, L.T("Daily quests will appear after your first day."), 34);
            foreach (var q in list) QuestRow(p, q, false);
        }

        void QuestRow(RectTransform p, QuestDef q, bool big)
        {
            int st = C.Quests.State(q.id); bool ready = st == QuestSystem.Ready, done = st == QuestSystem.Done;
            var card = U.Box(p, done ? Theme.GreyDark : ready ? Color.Lerp(Theme.Panel, Theme.Green, 0.25f) : Theme.Panel, ready ? Theme.Gold : Theme.Black, "q"); card.Le(-1, big ? 390 : 370, 1);
            var tt = U.Txt(card.transform, L.T(q.title), 40, done ? Theme.TextDim : Theme.Gold, TextAnchor.MiddleCenter, true); tt.StretchX(1, -16, 72, 24, 24);
            var rw = U.Rt(card.transform, "rw"); rw.StretchX(1, -92, 56, 24, 24); U.HBox(rw, 18, null, TextAnchor.MiddleCenter, false, false);
            foreach (var r in q.rewards.Take(3)) W.ResChip(rw, r.id, r.n, 26, false, 34);
            string blurb = q.desc;
            var ds = U.Txt(card.transform, L.T(blurb), 32, Theme.Text, TextAnchor.MiddleCenter, false, true); ds.StretchX(1, -166, big ? 136 : 116, 28, 28);
            ds.verticalOverflow = VerticalWrapMode.Truncate;
            var g = q.goals[0]; double cur = Math.Min(g.target, C.Quests.Progress(q, g));
            var bar = U.ProgressBar(card.transform, ready ? Theme.Green : Theme.Blue, 520, 30, $"{Util.Num(cur)}/{Util.Num(g.target)}", 22); bar.Value = (float)(cur / g.target); bar.back.R().At(0, 0, 20, 16, 520, 30, 0, 0);
            if (ready)
            {
                var b = U.Btn(card.transform, L.T("CLAIM"), Theme.Green, () => Claim(q), 210, 84, 36); b.At(1, 0, -14, 12, 210, 84, 1, 0); Tw.Pulse(b.transform, 0.04f, 0.8f);
            }
            else if (done) U.Icon(card.transform, Icons.Get("check"), 64).At(1, 0, -24, 16, 64, 64, 1, 0);
            else if (big && !string.IsNullOrEmpty(q.hint))
            {
                var b = U.Btn(card.transform, L.T("GO"), Theme.Accent, () => Go(q), 180, 84, 34); b.At(1, 0, -14, 12, 180, 84, 1, 0);
            }
        }

        void Go(QuestDef q)
        {
            var h = q.hint ?? "";
            if (h.StartsWith("tab_")) App.I.hud.GoTab((Hud.Tab)Enum.Parse(typeof(Hud.Tab), W.Cap(h.Substring(4))));
            else if (h.StartsWith("bld_")) App.I.nav.Push(new FacilityScreen(h.Substring(4)));
        }

        void Claim(QuestDef q)
        {
            if (q.type == "main" || !string.IsNullOrEmpty(q.story)) { Popups.Show(new QuestRewardPopup(q)); return; }
            if (C.Quests.Claim(q)) { Audio.Play("gem"); Toasts.Show(string.Join("  ", q.rewards.Select(r => $"+{Util.Num(r.n)} {L.T(C.ResName(r.id))}")), Icons.Get("star"), Theme.GreenDark); Redraw(); }
        }

        public override void Tick()
        {
            if (Time.realtimeSinceStartupAsDouble < nextTick) return; nextTick = Time.realtimeSinceStartupAsDouble + 0.5;
            foreach (var (t, f) in live) if (t) t.text = f();
            if (C.Quests.ReadyCount != lastReady) { lastReady = C.Quests.ReadyCount; Redraw(); }
        }
        int lastReady = -1;
    }
}
