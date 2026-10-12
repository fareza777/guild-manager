using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ManagerKit;
using NUnit.Framework;
using UnityEngine;

namespace Emberhall.Tests
{
    /// <summary>
    /// A greedy "average player" that plays the whole game in simulated time. Used to check pacing:
    /// how long rank-ups take, whether the economy stalls, whether any stage is unwinnable.
    /// </summary>
    public class BalanceBot
    {
        GameContext c; double clock; StringBuilder log = new StringBuilder();
        Dictionary<int, double> rankTime = new Dictionary<int, double>();
        int battles, losses, examLogs; string why = "";

        void Tick() { c.LastTick = 0; c.Tick(); }
        void Wait(double s) { clock += s; Tick(); }
        string T() => $"{(clock - start) / 3600.0:0.0}h";
        double start;

        public GameContext Play(double maxHours, bool verbose = true)
        {
            start = clock = 1_800_000_000; Util.ClockOverride = () => clock;
            c = GuildGame.Create(); c.Rng = new Rng(4242); GuildGame.StartNew(c, "Bot", 0, "warrior");
            double nextReport = 0;
            int idle = 0;
            while ((clock - start) / 3600.0 < maxHours && c.Ranks.Index < 6)
            {
                Tick();
                Housekeeping();
                bool acted = Fight();
                if (!acted) { idle++; Wait(idle > 6 ? 1800 : 300); } else idle = 0;
                if (c.Ranks.Index >= 0 && !rankTime.ContainsKey(c.Ranks.Index)) { rankTime[c.Ranks.Index] = clock - start; log.AppendLine($"[{T()}] reached rank {c.Ranks.Current.id}  gold={c.Wallet.Get("gold"):0} heroes={c.Roster.Count} maxLv={c.Roster.All.Max(u => u.level)} battles={battles} losses={losses}"); }
                if (verbose && (clock - start) >= nextReport) { nextReport += 3600; log.AppendLine($"  [{T()}] rank={c.Ranks.Current.id} fame={c.Ranks.Fame:0} gold={c.Wallet.Get("gold"):0} gems={c.Wallet.Get("gems"):0} heroes={c.Roster.Count} lv={string.Join(",", c.Roster.All.OrderByDescending(u => u.level).Take(4).Select(u => u.level))} quests={c.Counters.Get("quests.done")} why={why} fac={string.Join("/", c.Content.Upgrades.Where(u => u.kind == "facility").Select(u => c.Upgrades.Level(u.id)))}"); }
            }
            log.AppendLine($"END [{T()}] rank={c.Ranks.Current.id} fame={c.Ranks.Fame:0} battles={battles} losses={losses}");
            Debug.Log("[GM] BOT LOG\n" + log);
            Util.ClockOverride = null;
            return c;
        }

        void Housekeeping()
        {
            // quests
            foreach (var q in c.Content.Quests.ToList()) if (c.Quests.State(q.id) == QuestSystem.Ready) c.Quests.Claim(q);
            // vault
            if (c.Vault.Pending > c.Vault.Capacity * 0.9 || c.Vault.Pending > 300) c.Vault.Collect();
            // events
            foreach (var id in c.Events.Pending.ToList()) { var ev = c.Content.EventMap[id]; for (int i = 0; i < ev.choices.Count; i++) if (c.Events.Available(ev.choices[i])) { c.Events.Resolve(ev, i); break; } }
            // expeditions
            foreach (var t in c.Missions.Expeditions.Where(x => x.done).ToList()) c.Missions.ClaimExpedition(t);
            StartExpeditions();
            // daily
            if (!c.Daily.Claimed) c.Daily.Claim();
            // gear
            foreach (var g in c.Inventory.Gear.Where(x => c.Content.Rarities.FindIndex(r => r.id == x.rarity) <= 0 && string.IsNullOrEmpty(x.owner)).ToList()) { }
            c.Inventory.AutoEquipAll();
            if (c.Inventory.Gear.Count > c.Inventory.Capacity - 4) c.Inventory.SellJunk(1);
            // recruit first, then upgrades
            Recruit();
            if (c.Roster.Count >= Math.Min(4, c.Roster.Capacity) || c.Wallet.Get("gold") > 900) Upgrade();
            // train + heal
            Train(); Heal();
            Enhance();
        }

        void StartExpeditions()
        {
            while (c.Missions.Expeditions.Count() < c.Missions.ExpeditionSlots)
            {
                var ready = c.Roster.All.Where(c.Roster.Ready).OrderBy(u => c.Roster.Power(u)).ToList();
                if (c.Roster.Count < 6 || ready.Count < 5) return;
                ready = ready.Take(ready.Count - 3).ToList();
                MissionDef best = null; List<UnitInstance> bp = null; double bs = 0;
                foreach (var m in c.Missions.Board)
                {
                    var party = ready.Take(Math.Min(3, ready.Count)).ToList(); if (m.duration > 3600) continue;
                    double p = c.Missions.SuccessChance(m, party);
                    double v = p * m.rewards[0].n / Math.Max(1, m.duration);
                    if (p > 0.6 && (best == null || m.rewards[0].n > best.rewards[0].n)) { best = m; bp = party; }
                }
                if (best == null) return;
                if (!c.Missions.StartExpedition(best, bp, out _)) return;
            }
        }

        void Upgrade()
        {
            string[] order = { "tavern", "blacksmith", "training", "infirmary", "treasury", "barracks", "library" };
            foreach (var id in order)
            {
                var d = c.Content.Upgrade(id);
                if (c.Upgrades.Blocker(d) != null) continue;
                var cost = c.Upgrades.Cost(d, c.Upgrades.NextLevel(d));
                var gold = cost.First(x => x.id == "gold").n;
                // keep a reserve for recruiting early on
                if (c.Wallet.Get("gold") < gold * 1.15) continue;
                // buy missing ore if short (the bot simply 'grinds' it)
                foreach (var r in cost.Where(x => x.id != "gold")) if (c.Inventory.Count(r.id) < r.n) { c.Inventory.AddItem(r.id, (int)(r.n - c.Inventory.Count(r.id)), c.Rng); }
                c.Upgrades.Start(d);
            }
            foreach (var d in c.Content.Upgrades.Where(u => u.kind == "research"))
            {
                if (c.Upgrades.Blocker(d) != null) continue;
                var cost = c.Upgrades.Cost(d, c.Upgrades.NextLevel(d));
                if (!c.CanPay(cost) || c.Wallet.Get("gold") < cost[0].n * 1.5) continue;
                c.Upgrades.Start(d);
            }
        }

        void Recruit()
        {
            // tame monsters when affordable
            foreach (var u in c.Content.Units) if (!c.Recruiter.IsHiredDef(u.id) && c.MeetsAll(u.req) && c.CanPay(u.cost) && !c.Roster.Full && c.Wallet.Get("gold") > 1.5 * u.cost.First(x => x.id == "gold").n) c.Recruiter.HireDef(u);
            if (c.Roster.Full) return;
            var pool = c.Recruiter.Pool.OrderByDescending(p => Score(p)).ToList();
            foreach (var cand in pool)
            {
                if (c.Roster.Count >= 4 && c.Wallet.Get("gold") < cand.price * 1.6) continue;
                if (c.Roster.All.Count(u => u.classId == cand.classId) >= 2 && c.Roster.Count < 6 && c.Roster.Count >= 3) continue;
                if (c.Wallet.Get("gold") >= cand.price && (c.Roster.Count < 4 || Score(cand) > c.Roster.All.Min(u => Score(u)) * 0.9)) { c.Recruiter.Hire(cand); return; }
            }
        }
        double Score(UnitInstance u) { var d = c.Content.Rarities.FindIndex(r => r.id == u.rarity); return c.Roster.Power(u) * (1 + 0.15 * d); }

        void Train()
        {
            var st = c.Stations.ByKind("train"); if (st == null) return;
            foreach (var u in c.Roster.All.OrderBy(x => x.level).ToList())
            {
                if (c.Stations.Free(st) <= 0) return;
                if (c.Stations.Blocker(st, u) == null && c.Wallet.Get("gold") > c.Stations.Cost(st, u)[0].n * 3) c.Stations.Start(st, u);
            }
        }
        void Heal()
        {
            var st = c.Stations.ByKind("heal"); if (st == null) return;
            foreach (var u in c.Roster.All.Where(x => c.Roster.HpFrac(x) < 0.6).OrderBy(x => c.Roster.HpFrac(x)).ToList())
            { if (c.Stations.Free(st) <= 0) return; c.Stations.Start(st, u); }
        }
        void Enhance()
        {
            foreach (var u in c.Roster.All)
                for (int s = 0; s < 3; s++)
                {
                    var g = c.Inventory.Get(u.equip[s]); if (g == null) continue;
                    if (c.Crafting.CanEnhance(g) && c.Wallet.Get("gold") > c.Crafting.EnhCost(g)[0].n * 4) c.Crafting.Enhance(g);
                }
            // promote
            foreach (var u in c.Roster.All) if (c.Roster.CanPromote(u) && c.CanPay(c.Roster.PromoCost(u))) c.Roster.Promote(u);
        }

        bool Fight()
        {
            // rank exam first
            int pSize = (int)(c.Content.Bal("party.size", 3) + c.Mods.Add("party.size"));
            var ready = c.Roster.All.Where(c.Roster.Ready).OrderByDescending(u => c.Roster.Power(u)).ToList();
            why = $"ready={ready.Count}/{c.Roster.Count} pSize={pSize} hp=[{string.Join(",", c.Roster.All.Select(u => (int)(c.Roster.HpFrac(u) * 100) + (c.Roster.IsBusy(u) ? "b" : "")))}]";
            if (ready.Count < Math.Min(pSize, c.Roster.Count)) return false;
            // balanced party: prefer variety of roles
            var party = PickParty(ready, pSize);
            if (c.Ranks.FameReached && !c.Ranks.ExamDone && c.Content.Mission(c.Ranks.ExamId) != null)
            {
                if (TryFight(c.Content.Mission(c.Ranks.ExamId), party, 0.55)) { if (c.Ranks.CanPromote) c.Ranks.Promote(); return true; }
            }
            else if (c.Ranks.CanPromote) c.Ranks.Promote();
            // pick best mission: highest level unlocked that we can win in trial sims
            var candidates = c.Content.Missions.Where(m => m.kind == "battle" && m.region != "tower" && c.Missions.Unlocked(m)).OrderByDescending(m => m.level).ToList();
            // prefer unfinished/ first clears, otherwise repeat the highest comfortable stage
            foreach (var m in candidates)
            {
                if (TryFight(m, party, 0.7)) return true;
            }
            // tower
            if (c.Ranks.Index >= 2)
            {
                var tm = c.Missions.TowerMission(c.Missions.TowerBest + 1);
                if (TryFight(tm, party, 0.7)) return true;
            }
            return false;
        }

        List<UnitInstance> PickParty(List<UnitInstance> ready, int size)
        {
            var party = new List<UnitInstance>();
            foreach (var role in new[] { "knight", "cleric", "warrior", "mage", "archer", "rogue" })
            {
                var u = ready.FirstOrDefault(x => x.classId == role && !party.Contains(x));
                if (u != null && party.Count < size) party.Add(u);
            }
            foreach (var u in ready) if (party.Count < size && !party.Contains(u)) party.Add(u);
            return party;
        }

        bool TryFight(MissionDef m, List<UnitInstance> party, double minWinRate)
        {
            int wins = 0, n = 5;
            for (int i = 0; i < n; i++) { var s = c.Missions.BuildSetup(m, party, 1000 + i * 17); var r = BattleSim.Run(s); if (r.win && r.deadHeroes.Count <= 1) wins++; }
            if (m.kind == "exam" && examLogs++ < 6) log.AppendLine($"    exam {m.id} wins {wins}/{n} party=[{string.Join(",", party.Select(u => u.classId + u.level))}] power={c.Roster.PartyPower(party)}");
            if (wins / (double)n < minWinRate) return false;
            var setup = c.Missions.BuildSetup(m, party);
            var res = BattleSim.Run(setup);
            c.Missions.Complete(m, party, setup, res);
            battles++; if (!res.win) losses++;
            Wait(50);   // time spent watching + navigating
            return true;
        }

        // ---------- tests ----------
        [Test, Explicit("Long-running pacing simulation")]
        public void FullPlaythroughPacing()
        {
            Play(24 * 14);
            Assert.GreaterOrEqual(c.Ranks.Index, 3, "A diligent player should be at least rank C within two weeks");
        }

        [Test]
        public void FirstSessionPacing()
        {
            Play(3);
            Assert.GreaterOrEqual(c.Ranks.Index, 1, "A new player should reach rank E within the first 3 hours");
        }
    }
}
