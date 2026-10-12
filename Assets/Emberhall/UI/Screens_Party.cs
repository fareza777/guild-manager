using System;
using System.Collections.Generic;
using System.Linq;
using ManagerKit;
using ManagerKit.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Emberhall
{
    /// <summary>Choose heroes for a battle / exam / tower floor / expedition. Shows live win-chance from the real simulator.</summary>
    public class PartyPopup : Popup
    {
        readonly MissionDef m; readonly List<string> sel = new List<string>();
        static BattleDoctrine lastDoctrine;
        BattleDoctrine doctrine = lastDoctrine;
        GameContext C => App.Ctx;
        bool expedition => m.kind == "expedition" || m.kind == "contract";
        int MaxSize => C.Missions.PartySize;
        double chance; string chanceKey = "";
        double nextStateCheck;
        string shownRosterState;

        public PartyPopup(MissionDef mission)
        {
            m = mission;
            foreach (var id in BoardState.LastParty) { var u = C.Roster.Get(id); if (u != null && C.Roster.Ready(u) && sel.Count < MaxSize) sel.Add(id); }
            if (sel.Count == 0) Auto();
        }
        public override string Title => expedition ? L.T("Send Expedition") : L.T("Choose Your Party");
        public override float Height => 0;
        public override bool HasFooter => true;

        void Auto()
        {
            sel.Clear();
            var ready = C.Roster.All.Where(C.Roster.Ready).OrderByDescending(u => C.Roster.Power(u)).ToList();
            foreach (var role in new[] { "knight", "cleric", "warrior", "mage", "archer", "rogue" })
            {
                var u = ready.FirstOrDefault(x => x.classId == role && !sel.Contains(x.uid));
                if (u != null && sel.Count < MaxSize) sel.Add(u.uid);
            }
            foreach (var u in ready) if (sel.Count < MaxSize && !sel.Contains(u.uid)) sel.Add(u.uid);
        }
        List<UnitInstance> Party => sel.Select(C.Roster.Get).Where(u => u != null).ToList();

        double WinChance()
        {
            var p = Party; if (p.Count == 0) return 0;
            if (expedition) return C.Missions.SuccessChance(m, p);
            int wins = 0, n = 12;
            for (int i = 0; i < n; i++) { var s = C.Missions.BuildSetup(m, p, 700 + i * 31, doctrine); if (BattleSim.Run(s).win) wins++; }
            return wins / (double)n;
        }

        public override void Build(RectTransform body)
        {
            shownRosterState = RosterState();
            var party = Party;
            // --- mission summary ---
            var head = U.Box(body, Theme.PanelDark, m.boss ? Theme.Red : Theme.Black, "head"); head.Le(-1, expedition ? 234 : 330, 1);
            var nm = U.Txt(head.transform, W.MissionName(m.name), 44, Theme.Text, TextAnchor.MiddleCenter, true); nm.StretchX(1, -18, 56, 22, 22);
            var lv = U.Txt(head.transform, expedition ? $"{L.T("Duration")} {Util.Dur(m.duration)}   {L.T("Power")} {Util.Num(m.power)}" : L.T("Enemy level {0}", m.level) + (m.boss ? "  " + W.Col(Theme.Bad, L.T("BOSS")) : "") + (m.waves.Count > 1 ? $"   {m.waves.Count} {L.T("waves")}" : ""), 30, Theme.Gold, TextAnchor.MiddleLeft, true); lv.StretchX(1, -82, 42, 22, 22);
            if (!expedition)
            {
                var er = U.Rt(head.transform, "enemies"); er.StretchX(0, 16, 100, 22, 22); U.HBox(er, 12, null, TextAnchor.LowerLeft, false, false);
                foreach (var eid in m.waves.SelectMany(w => w.enemies).Select(e => e.id).Distinct().Take(6))
                {
                    var ed = C.Content.EnemyMap[eid]; var holder = U.Rt(er, "e"); holder.Le(ed.boss ? 100 : 72, 100); var img = U.Img(holder, null, Color.white, "u"); img.At(0.5f, 0, 0, 0, 10, 10, 0.5f, 0);
                    float s2 = ed.sprite.StartsWith("bat:") ? 100f / Mathf.Max(1, Art.Get(ed.sprite).rect.height) : 2.7f; SpriteAnim.On(img, Art.FrontFrames(ed.sprite), 4, true, s2 * (ed.boss ? 1.2f : 1f));
                }
            }
            var rr = U.Rt(head.transform, "rw"); rr.StretchX(1, -138, 50, 22, 280); U.HBox(rr, 18, null, TextAnchor.MiddleLeft, false, false);
            foreach (var r in m.rewards.Take(3)) W.ResChip(rr, r.id, r.n, 30, false, 38);
            var rec = U.Rt(head.transform, "rec"); rec.At(1, 1, -22, -132, 230, 50, 1, 1); U.HBox(rec, 6, null, TextAnchor.MiddleRight, false, false);
            foreach (var cid in m.recommend.Take(4)) W.ClassBadge(rec, cid, 46);
            if (m.recommend.Count > 0) { var rl = U.Txt(head.transform, L.T("Recommended"), 22, Theme.TextDim, TextAnchor.MiddleRight); rl.At(1, 1, -22, -186, 230, 28, 1, 1); }

            if (!expedition)
            {
                W.Header(body, L.T("Battle plan"), Icons.Get("sword"));
                var plans = U.Rt(body, "doctrines"); plans.Le(-1, 78); U.HBox(plans, 10, null, TextAnchor.MiddleCenter, true, true, true, true);
                foreach (BattleDoctrine plan in new[] { BattleDoctrine.Balanced, BattleDoctrine.Assault, BattleDoctrine.Guard })
                {
                    var chosen = plan;
                    var option = U.Btn(plans, L.T(plan.ToString()), doctrine == plan ? Theme.Accent : Theme.PanelLight,
                        () => { doctrine = chosen; lastDoctrine = chosen; Redraw(); }, 240, 78, 36);
                    option.Le(-1, 78, 1);
                }
                var description = U.Txt(body, L.T(MissionSystem.DoctrineDescription(doctrine)), 32, Theme.Text, TextAnchor.MiddleCenter, false, true);
                description.Le(-1, 92, 1);
            }

            // --- party slots ---
            int max = MaxSize;
            var slots = U.Rt(body, "slots"); slots.Le(-1, 280); U.HBox(slots, 10, null, TextAnchor.MiddleCenter, true, true, true, true);
            for (int i = 0; i < max; i++)
            {
                var u = i < party.Count ? party[i] : null;
                var cell = U.Box(slots, u != null ? Theme.Panel : Theme.Inset, u != null ? Art.RarityColor(C, u.rarity) : Theme.Grey, "slot", true); cell.Le(-1, 280, 1);
                var cb = cell.gameObject.AddComponent<UButton>(); cb.bg = cell; cb.noTint = true;
                if (u != null)
                {
                    var uid = u.uid; cb.onClick = () => { sel.Remove(uid); Redraw(); };
                    var pic = W.Portrait(cell.transform, u.sprite, 140); pic.transform.parent.GetComponent<RectTransform>().At(0.5f, 1, 0, -14, 126, 140, 0.5f, 1);
                    var n = U.Txt(cell.transform, u.name, 30, Theme.Text, TextAnchor.MiddleCenter, true); n.StretchX(0, 66, 58, 8, 8);
                    var l = U.Txt(cell.transform, $"Lv {u.level}", 28, Theme.Gold, TextAnchor.MiddleCenter, true); l.StretchX(0, 32, 32, 8, 8);
                    var hb = U.ProgressBar(cell.transform, Theme.Green, 140, 14); hb.Value = (float)C.Roster.HpFrac(u); hb.back.R().StretchX(0, 12, 14, 24, 24);
                    var cls = W.ClassBadge(cell.transform, u.classId, 40); cls.At(0, 1, 6, -6, 40, 40, 0, 1);
                }
                else { cb.onClick = null; U.Icon(cell.transform, Icons.Get("plus"), 64, new Color(1, 1, 1, 0.35f)).At(0.5f, 0.5f, 0, 0, 64, 64); }
            }
            // --- synergy + chance ---
            var syn = C.Roster.ActiveSynergies(party);
            var strip = U.ScrollH(body, 10, new RectOffset(0, 0, 2, 2), "synergies"); strip.view.parent.GetComponent<RectTransform>().Le(-1, 58, 1);
            var sr = strip.content;
            foreach (var s in syn) U.Chip(sr, L.T(s.name), Theme.GreenDark, 28, Color.white, 16, 48);
            if (syn.Count == 0) { var t = U.Txt(sr, L.T("No synergy - mix classes for bonuses!"), 30, Theme.TextDim, TextAnchor.MiddleCenter); t.Le(760, 48); }
            string key = string.Join(",", sel) + ":" + doctrine + ":" + shownRosterState; if (key != chanceKey) { chanceKey = key; chance = WinChance(); }
            var box = U.Box(body, Theme.Inset, chance >= 0.75 ? Theme.GreenDark : chance >= 0.4 ? Theme.AccentDark : Theme.RedDark, "chance"); box.Le(-1, 92, 1);
            var ct = U.Txt(box.transform, $"{L.T(expedition ? "Success chance" : "Estimated win chance")}: {W.Col(chance >= 0.75 ? Theme.Good : chance >= 0.4 ? Theme.Warn : Theme.Bad, (int)(chance * 100) + "%")}   <size=28>({L.T("Power")} {Util.Num(C.Roster.PartyPower(party))})</size>", 36, Theme.Text, TextAnchor.MiddleCenter, true); ct.Fill(20, 20, 10, 10);

            // --- hero list ---
            if (!C.Roster.All.Any(C.Roster.Ready)) W.Info(body, L.T("No heroes are ready. Open a hero and use a healing potion, or visit the Infirmary."), 30, Theme.Bad);
            else if (party.Count == 0) W.Info(body, L.T("Choose at least one healthy hero from the list below."), 30, Theme.Warn);
            W.Header(body, L.T("Heroes"), Icons.Get("heroes"));
            foreach (var u in C.Roster.All.OrderByDescending(x => C.Roster.Ready(x)).ThenByDescending(x => C.Roster.Power(x)))
            {
                var uu = u; bool ready = C.Roster.Ready(u); bool inP = sel.Contains(u.uid);
                var row = U.Box(body, !ready ? Theme.GreyDark : inP ? Color.Lerp(Theme.Panel, Theme.Green, 0.3f) : Theme.PanelDark, inP ? Theme.Green : Theme.Black, "h", true); row.Le(-1, 128, 1);
                var rb = row.gameObject.AddComponent<UButton>(); rb.bg = row; rb.noTint = true;
                rb.onClick = () =>
                {
                    if (!C.Roster.Ready(uu)) { Toasts.Show(L.T(C.Roster.ReadinessBlocker(uu))); return; }
                    if (sel.Contains(uu.uid)) sel.Remove(uu.uid); else if (sel.Count < MaxSize) sel.Add(uu.uid); else { Toasts.Show(L.T("Your party is full. Remove a hero before adding another.")); return; }
                    Redraw();
                };
                var pic = W.Portrait(row.transform, u.sprite, 100); pic.transform.parent.GetComponent<RectTransform>().At(0, 0.5f, 16, -2, 90, 100, 0, 0.5f);
                var n = U.Txt(row.transform, u.name, 36, Theme.Text, TextAnchor.MiddleLeft, true); n.At(0, 1, 120, -10, 420, 44, 0, 1);
                var cls = W.ClassBadge(row.transform, u.classId, 42); cls.At(0, 0, 120, 14, 42, 42, 0, 0);
                var l = U.Txt(row.transform, $"Lv {u.level}", 30, Theme.Gold, TextAnchor.MiddleLeft, true); l.At(0, 0, 172, 14, 150, 42, 0, 0);
                var hb = U.ProgressBar(row.transform, C.Roster.HpFrac(u) < 0.3 ? Theme.Red : Theme.Green, 220, 20); hb.Value = (float)C.Roster.HpFrac(u); hb.back.R().At(0, 0, 330, 22, 220, 20, 0, 0);
                var (st, sc) = W.UnitStatus(u);
                var stt = U.Txt(row.transform, inP ? L.T("In party") : st, 32, inP ? Theme.Good : sc, TextAnchor.MiddleRight, true); stt.At(1, 0.5f, -20, 0, 260, 50, 1, 0.5f);
                var pw = U.Txt(row.transform, Util.Num(C.Roster.Power(u)), 28, Theme.TextDim, TextAnchor.MiddleRight); pw.At(1, 1, -20, -10, 200, 32, 1, 1);
            }
        }

        string RosterState() => C.Roster.Version + ":" + C.Mods.Revision + ":" + string.Join(",", C.Roster.All.Select(u => u.uid + ":" + C.Roster.Ready(u) + ":" + Math.Round(C.Roster.HpFrac(u) * 1000)));

        public override void Tick()
        {
            if (Time.realtimeSinceStartupAsDouble < nextStateCheck) return;
            nextStateCheck = Time.realtimeSinceStartupAsDouble + 0.5;
            if (RosterState() == shownRosterState) return;
            // Healing and timed tasks can make a hero ready while this dialog is open.
            chanceKey = "";
            Redraw();
        }

        public override void BuildFooter(RectTransform f)
        {
            var auto = U.Btn(f, L.T("Auto"), Theme.BlueDark, () => { Auto(); Redraw(); }, 240, 100, 38, Icons.Get("star")); auto.Le(240, 100);
            if (!C.Roster.All.Any(C.Roster.Ready))
            {
                var heal = U.Btn(f, L.T("OPEN HEROES"), Theme.Green, () => { Close(); App.I.hud.GoTab(Hud.Tab.Heroes); }, 500, 100, 34);
                heal.Le(-1, 100, 1); return;
            }
            string label = expedition ? L.T("SEND") : L.T("FIGHT!");
            var go = U.Btn(f, label, expedition ? Theme.Green : (m.boss ? Theme.Red : Theme.Accent), Go, 500, 100, 52, Icons.Get(expedition ? "arrow_r" : "sword")); go.Le(-1, 100, 1);
            go.Interactable = Party.Count > 0; Hud.Reg("btn_party_go", go);
        }

        void Go()
        {
            var p = Party;
            BoardState.LastParty = sel.ToList();
            if (expedition)
            {
                if (C.Missions.StartExpedition(m, p, out var why)) { Audio.Play("swoosh"); Toasts.Show(L.T("Party sent!")); Close(); C.Changed(); }
                else Toasts.Show(L.T(why ?? "Cannot start"));
                return;
            }
            if (!C.Missions.CanStartBattle(m, p, out var w2)) { Toasts.Show(L.T(w2)); return; }
            Close(); App.I.nav.Push(new BattleScreen(m, p, doctrine));
        }
    }
}
