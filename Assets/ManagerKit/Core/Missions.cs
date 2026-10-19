using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ManagerKit
{
    public interface IBattleRules
    {
        bool CanEnter(MissionDef mission, out string why);
        bool Enter(MissionDef mission, out string why);
        void Result(MissionDef mission, bool won);
    }
    [Serializable] public class MissionProgress { public string id; public int clears, stars, bestRounds; }

    public class MissionOutcome
    {
        public bool win, first; public int stars, rounds;
        public List<Res> res = new List<Res>();
        public List<Drop> drops = new List<Drop>();
        public double xp;
        public Dictionary<string, int> levelUps = new Dictionary<string, int>();
        public List<string> fallen = new List<string>();
        public List<UnitInstance> units = new List<UnitInstance>();
        public bool rankReady; public string mission;
    }

    public class ExpeditionResult
    {
        public int outcome;   // 0 fail, 1 success, 2 great
        public List<Res> res = new List<Res>(); public List<Drop> drops = new List<Drop>(); public double xp; public MissionDef mission;
        public List<UnitInstance> units = new List<UnitInstance>(); public Dictionary<string, int> levelUps = new Dictionary<string, int>();
    }

    public class MissionSystem : ISystem, ISaveable, ITickable, IPostInit, INewGame
    {
        GameContext c;
        readonly Dictionary<string, MissionProgress> prog = new Dictionary<string, MissionProgress>();
        [Serializable] class Data { public List<MissionProgress> p = new List<MissionProgress>(); public List<MissionDef> board = new List<MissionDef>(); public double boardAt; public int seq; public int tower; public List<string> activeExp = new List<string>(); }
        Data d = new Data();
        string recommendedKey;
        readonly List<string> recommendedIds = new List<string>();
        public string SaveKey => "missions";
        public IReadOnlyList<MissionDef> Board => d.board;
        public double BoardRefreshAt => d.boardAt;
        public int TowerBest => d.tower;
        public void Init(GameContext ctx) { c = ctx; }

        public string Save() { d.p = prog.Values.ToList(); return JsonUtility.ToJson(d); }
        public void Load(string json) { recommendedKey = null; prog.Clear(); d = JsonUtility.FromJson<Data>(json) ?? new Data(); foreach (var p in d.p) prog[p.id] = p; }
        public void Reset() { recommendedKey = null; prog.Clear(); d = new Data(); }
        public void OnNewGame() { RefreshBoard(); }

        public void PostInit()
        {
            c.Stats.Register("mission", k =>
            {
                if (k.StartsWith("clear.")) return Clears(k.Substring(6));
                if (k.StartsWith("stars.")) return Stars(k.Substring(6));
                if (k == "totalstars") return prog.Values.Sum(p => p.stars);
                if (k == "tower") return d.tower;
                if (k.StartsWith("region.")) { var rid = k.Substring(7); return c.Content.Missions.Count(m => m.region == rid && Clears(m.id) > 0); }
                return 0;
            });
            c.Timers.OnComplete("expedition", t => c.Note(NoteKind.Good, "An expedition has returned!"), true);
        }

        public int Clears(string id) => prog.TryGetValue(id, out var p) ? p.clears : 0;
        public int Stars(string id) => prog.TryGetValue(id, out var p) ? p.stars : 0;
        public bool Cleared(string id) => Clears(id) > 0;
        public bool Unlocked(MissionDef m) => c.MeetsAll(m.req) && (!c.Content.RegionMap.TryGetValue(m.region, out var r) || c.MeetsAll(r.req));
        public IEnumerable<MissionDef> InRegion(string region) => c.Content.Missions.Where(m => m.region == region && m.kind != "contract" && m.waves.Count > 0);
        public IEnumerable<RegionDef> CampaignRegions => c.Content.Regions.Where(r => r.order < 90);

        // ===================== contract board =====================
        public void Tick(double now)
        {
            if (c.Ranks.Index >= 0 && (now >= d.boardAt || d.board.Count == 0) && c.Upgrades.Level("tavern") > 0) RefreshBoard();
        }
        public int BoardSize => (int)(c.Content.Bal("board.size", 4) + c.Mods.Add("board.size"));
        public void RefreshBoard()
        {
            d.board.Clear(); d.seq++;
            var templates = c.Content.Missions.Where(m => m.kind == "contract" && c.MeetsAll(m.req)).ToList();
            if (templates.Count == 0) return;
            var rng = new Rng((int)(Util.Now / 60) + d.seq * 977);
            int rank = Math.Max(0, c.Ranks.Index);
            for (int i = 0; i < BoardSize; i++)
            {
                var t = templates[(i + rng.Int(0, templates.Count)) % templates.Count];
                var m = JsonUtility.FromJson<MissionDef>(JsonUtility.ToJson(t));
                // Every board offers a safe route, a standard route and a demanding route.
                double diff = i % 3 == 0 ? rng.Range(0.65f, 0.8f) : i % 3 == 1 ? rng.Range(0.9f, 1.08f) : rng.Range(1.15f, 1.35f);
                double scale = 1 + 0.55 * rank;
                m.id = $"ct{d.seq}_{i}"; m.kind = "expedition"; m.level = 1 + rank * 3;
                m.power = Math.Round(t.power * scale * diff);
                m.duration = Math.Round(t.duration * rng.Range(0.8f, 1.4f) * (1 + 0.1 * rank));
                foreach (var r in m.rewards) r.n = Math.Round(r.n * scale * (0.8 + 0.4 * diff));
                m.xp = Math.Round(t.xp * scale * (0.8 + 0.4 * diff));
                m.tier = 1 + rank / 2; m.boss = diff > 1.25;
                if (m.boss) m.name = "Dangerous: " + m.name;
                else if (i % 3 == 0) m.name = "Safe Route: " + m.name;
                m.desc = t.desc;
                d.board.Add(m);
            }
            d.boardAt = Util.Now + c.Content.Bal("board.refresh", 1800);
            c.Changed();
        }
        public bool ManualBoardRefresh()
        {
            if (!c.Pay(new[] { new Res("gems", c.Content.Bal("board.refresh_gems", 5)) })) return false;
            RefreshBoard(); return true;
        }

        // ===================== tower =====================
        public MissionDef TowerMission(int floor)
        {
            var db = c.Content; var rng = new Rng(floor * 7919 + 13);
            int tier = Mathf.Clamp(1 + (floor - 1) / 8, 1, 5);
            bool boss = floor % 5 == 0;
            var m = new MissionDef { id = "tower_" + floor, kind = "battle", region = "tower", name = $"Tower Floor {floor}", level = 4 + (int)(floor * 2.2), tier = tier, boss = boss, xp = 18 + floor * 6, loot = "loot_tower", maxTurns = 120, par3 = 14 + floor / 4 };
            var pool = db.EnemyMap.Values.Where(e => e.tags.Contains("t" + tier) && !e.boss).ToList();
            var bosses = db.EnemyMap.Values.Where(e => e.tags.Contains("boss_t" + tier)).ToList();
            if (pool.Count == 0) pool = db.EnemyMap.Values.Where(e => !e.boss).ToList();
            int waves = boss ? 2 : 1 + Math.Min(2, floor / 12);
            for (int w = 0; w < waves; w++)
            {
                var wd = new WaveDef(); bool last = w == waves - 1;
                if (boss && last && bosses.Count > 0) { wd.enemies.Add(new Res(rng.Pick(bosses).id, 1)); wd.enemies.Add(new Res(rng.Pick(pool).id, 1)); }
                else { int cnt = Mathf.Clamp(2 + floor / 10 + rng.Range(0, 1), 2, 4); for (int k = 0; k < cnt; k++) wd.enemies.Add(new Res(rng.Pick(pool).id, 1)); }
                m.waves.Add(wd);
            }
            m.rewards.Add(new Res("gold", Math.Round(90 * Math.Pow(floor, 1.18) * (boss ? 2 : 1))));
            m.rewards.Add(new Res("fame", Math.Round(2 + floor * 0.9 * (boss ? 2 : 1))));
            if (floor % 10 == 0) m.rewards.Add(new Res("gems", 5 + floor / 10 * 2));
            return m;
        }

        // ===================== battle =====================
        public Combatant MakeHero(UnitInstance u, int idx, Dictionary<string, double> synMul)
        {
            var db = c.Content; var cls = db.Class(u.classId); var st = c.Roster.Stats(u);
            double hpFrac = c.Roster.HpFrac(u);
            double M(string s) => synMul != null && synMul.TryGetValue(s, out var v) ? 1 + v : 1;
            var h = new Combatant
            {
                side = 0, name = u.name, sprite = u.sprite, classId = u.classId, anim = cls?.anim ?? "sword01", unitUid = u.uid, monster = u.monster, scale = u.scale,
                maxHp = Math.Round(st["hp"] * M("hp")), atk = st["atk"] * M("atk"), def = st["def"] * M("def"), spd = st["spd"] * M("spd"), crit = st["crit"],
                front = cls != null && !cls.ranged, ranged = cls?.ranged ?? false,
            };
            h.hp = Math.Max(1, Math.Round(h.maxHp * hpFrac));
            if (cls != null) foreach (var sid in cls.skills) if (db.SkillMap.TryGetValue(sid, out var sk) && sk.unlockLevel <= u.level + u.promo * 5) h.skills.Add(sk);
            if (h.skills.Count == 0) h.skills.Add(new SkillDef { id = "basic", name = "Attack" });
            return h;
        }

        public Combatant MakeEnemy(EnemyDef e, int level)
        {
            var db = c.Content; double g = db.Bal("enemy.growth", 0.16);
            double S(string id) { foreach (var s in e.stats) if (s.id == id) return s.v; return 0; }
            double lv = Math.Max(0, level - 1);
            var en = new Combatant
            {
                side = 1, name = e.name, sprite = e.sprite, skin = e.skin, defId = e.id, boss = e.boss, scale = e.scale, front = true,
                maxHp = Math.Round(S("hp") * (1 + lv * g)), atk = S("atk") * (1 + lv * g), def = S("def") * (1 + lv * g * 0.7), spd = S("spd") * (1 + lv * 0.012), crit = S("crit"),
            };
            en.hp = en.maxHp;
            foreach (var sid in e.skills) if (db.SkillMap.TryGetValue(sid, out var sk)) en.skills.Add(sk);
            if (en.skills.Count == 0) en.skills.Add(db.SkillMap.TryGetValue("attack", out var a) ? a : new SkillDef { id = "attack", name = "Attack" });
            return en;
        }

        public Dictionary<string, double> SynergyMultipliers(IEnumerable<UnitInstance> party)
        {
            var m = new Dictionary<string, double>();
            foreach (var s in c.Roster.ActiveSynergies(party)) foreach (var mod in s.mods) if (mod.key.StartsWith("stat.") && mod.op == "pct") { var k = mod.key.Substring(5); m[k] = (m.TryGetValue(k, out var x) ? x : 0) + mod.v; }
            return m;
        }

        public BattleSetup BuildSetup(MissionDef m, IList<UnitInstance> party, int seed = 0, BattleDoctrine doctrine = BattleDoctrine.Balanced)
        {
            var s = new BattleSetup { seed = seed != 0 ? seed : c.Rng.Int(1, int.MaxValue), maxTurns = m.maxTurns > 0 ? Math.Max(m.maxTurns * 4, 120) : 400, defK = c.Content.Bal("def.k", 1), doctrine = doctrine };
            var syn = SynergyMultipliers(party);
            for (int i = 0; i < party.Count; i++)
            {
                var hero = MakeHero(party[i], i, syn);
                if (doctrine == BattleDoctrine.Assault) { hero.atk *= 1.18; hero.spd *= 1.08; hero.def *= 0.8; }
                else if (doctrine == BattleDoctrine.Guard) { hero.atk *= 0.92; hero.def *= 1.35; hero.shield = Math.Round(hero.maxHp * 0.08); }
                s.heroes.Add(hero);
            }
            foreach (var w in m.waves)
            {
                var list = new List<Combatant>();
                foreach (var r in w.enemies) if (c.Content.EnemyMap.TryGetValue(r.id, out var e)) for (int k = 0; k < (int)r.n; k++) list.Add(MakeEnemy(e, m.level));
                s.waves.Add(list);
            }
            return s;
        }

        public bool CanStartBattle(MissionDef m, IList<UnitInstance> party, out string why)
        {
            if (!ValidParty(party, out why)) return false;
            if (m == null || m.waves.Count == 0) { why = "This battle is unavailable. Return to Board > Campaign and choose an unlocked mission."; return false; }
            if (!Unlocked(m)) { why = "This mission is locked. Clear the previous stage or raise your Guild Rank on the Board."; return false; }
            if (c.Get<IBattleRules>() is IBattleRules rules && !rules.CanEnter(m, out why)) return false;
            return true;
        }

        public bool TryStartBattle(MissionDef m, IList<UnitInstance> party, out string why)
        {
            if (!CanStartBattle(m, party, out why)) return false;
            return c.Get<IBattleRules>() is IBattleRules rules ? rules.Enter(m, out why) : true;
        }

        public int PartySize => Math.Max(1, (int)(c.Content.Bal("party.size", 3) + c.Mods.Add("party.size")));

        /// <summary>Compare ready heroes against this encounter, without consuming the campaign RNG.</summary>
        public List<UnitInstance> RecommendedParty(MissionDef mission, BattleDoctrine doctrine = BattleDoctrine.Balanced)
        {
            var ready = c.Roster.All.Where(c.Roster.Ready).OrderByDescending(c.Roster.Power).ThenBy(u => u.uid).ToList();
            var party = ready.Take(PartySize).ToList();
            if (mission == null || party.Count == 0) return party;
            string key = mission.id + ":" + mission.level + ":" + mission.power + ":" + doctrine + ":" + PartySize + ":" + c.Mods.Revision + ":" + c.Roster.Version + ":"
                + string.Join(",", ready.Select(u => u.uid + ":" + u.level + ":" + Math.Round(c.Roster.HpFrac(u) * 1000)));
            if (key == recommendedKey) return recommendedIds.Select(c.Roster.Get).Where(u => u != null && c.Roster.Ready(u)).ToList();
            // Keep the search bounded while retaining the strongest candidate of every class.
            var candidates = ready.Take(12).Concat(ready.GroupBy(u => u.classId).Select(g => g.First())).Distinct().ToList();
            bool expedition = mission.kind == "expedition" || mission.kind == "contract";
            double Score(List<UnitInstance> team)
            {
                if (expedition) return SuccessChance(mission, team) * 10000 + Math.Log(1 + c.Roster.PartyPower(team));
                double score = 0;
                for (int sample = 0; sample < 3; sample++)
                {
                    var setup = BuildSetup(mission, team, 700 + sample * 31, doctrine);
                    var result = BattleSim.Run(setup);
                    double health = setup.heroes.Sum(h => Math.Max(0, h.hp) / Math.Max(1, h.maxHp));
                    double enemyDamage = setup.waves.SelectMany(w => w).Sum(e => 1 - Math.Max(0, e.hp) / Math.Max(1, e.maxHp));
                    score += (result.win ? 100000 : 0) + result.wavesCleared * 10000 + enemyDamage * 1000 + health * 100 - result.heroActions * .1;
                }
                return score + Math.Log(1 + c.Roster.PartyPower(team)) * .01;
            }
            double best = Score(party);
            for (int pass = 0; pass < 2; pass++)
            {
                bool improved = false;
                for (int slot = 0; slot < party.Count; slot++)
                    foreach (var candidate in candidates)
                    {
                        if (party.Contains(candidate)) continue;
                        var trial = new List<UnitInstance>(party); trial[slot] = candidate;
                        double score = Score(trial);
                        if (score <= best + .001) continue;
                        party = trial; best = score; improved = true;
                    }
                if (!improved) break;
            }
            party = party.OrderBy(u => c.Content.Class(u.classId)?.ranged ?? false).ThenByDescending(c.Roster.Power).ToList();
            recommendedKey = key; recommendedIds.Clear(); recommendedIds.AddRange(party.Select(u => u.uid));
            return party;
        }

        bool ValidParty(IList<UnitInstance> party, out string why)
        {
            why = null;
            if (party == null || party.Count == 0) { why = "Choose at least one healthy hero from the list below."; return false; }
            if (party.Count > PartySize) { why = "Your party is full. Remove a hero before adding another."; return false; }
            var seen = new HashSet<string>();
            foreach (var u in party)
            {
                if (u == null || c.Roster.Get(u.uid) != u) { why = "This hero is no longer in your guild. Choose another hero."; return false; }
                if (!seen.Add(u.uid)) { why = "Each hero can occupy only one party slot. Remove the duplicate."; return false; }
                var blocker = c.Roster.ReadinessBlocker(u);
                if (blocker != null) { why = blocker; return false; }
            }
            return true;
        }

        public static string DoctrineDescription(BattleDoctrine doctrine)
        {
            if (doctrine == BattleDoctrine.Assault) return "+18% ATK, +8% SPD, -20% DEF. Finish the weakest foe.";
            if (doctrine == BattleDoctrine.Guard) return "+35% DEF, 8% starting shield, -8% ATK. Stop the strongest threat.";
            return "Focus wounded enemies. Keep full attack and defense.";
        }

        public MissionOutcome Complete(MissionDef m, IList<UnitInstance> party, BattleSetup setup, BattleResult r)
        {
            var o = new MissionOutcome { win = r.win, rounds = r.rounds, mission = m.id, units = party.ToList() };
            c.Counters.Add("battles.total");
            for (int i = 0; i < party.Count; i++)
            {
                var u = party[i]; var h = setup.heroes[i];
                bool dead = r.deadHeroes.Contains(h.id);
                c.Roster.SetHp(u, r.endHp.TryGetValue(h.id, out var hp) ? hp : 0);
                if (dead) { o.fallen.Add(u.name); c.Counters.Add("hero.fallen"); }
            }
            foreach (var k in r.killed) { c.Counters.Add("enemies.killed"); c.Counters.Add("kills." + k.defId); var ed = c.Content.EnemyMap[k.defId]; foreach (var tg in ed.tags) c.Counters.Add("kills.tag." + tg); if (k.boss) c.Counters.Add("boss.killed"); }
            double xpMul = (1 + c.Mods.Pct("xp.gain") + c.Roster.PartyPct(party, "xp.gain"));
            double luck = c.Mods.Add("loot.luck") + c.Roster.PartyPct(party, "loot.luck");
            double goldMul = 1 + c.Mods.Pct("gold.gain") + c.Roster.PartyPct(party, "gold.gain");
            if (r.win)
            {
                int stars = 1 + (o.fallen.Count == 0 ? 1 : 0) + (r.rounds <= m.par3 ? 1 : 0);
                o.stars = stars; o.first = Clears(m.id) == 0;
                if (!prog.TryGetValue(m.id, out var p)) prog[m.id] = p = new MissionProgress { id = m.id };
                int gainedStars = Math.Max(0, stars - p.stars);
                p.clears++; p.stars = Math.Max(p.stars, stars); p.bestRounds = p.bestRounds == 0 ? r.rounds : Math.Min(p.bestRounds, r.rounds);
                foreach (var rw in m.rewards) o.res.Add(new Res(rw.id, Math.Round(rw.n * (rw.id == "gold" ? goldMul : rw.id == "fame" ? 1 + c.Mods.Pct("fame.gain") : 1))));
                if (o.first) foreach (var rw in m.firstClear) o.res.Add(new Res(rw.id, rw.n));
                if (gainedStars > 0 && stars == 3 && m.kind == "battle" && m.region != "tower") o.res.Add(new Res("gems", 2));
                o.drops = Loot.Merge(Loot.Roll(c, m.loot, luck, c.Rng));
                foreach (var rw in o.res) c.Give(rw);
                Loot.Apply(c, o.drops);
                c.Counters.Add("missions.won"); c.Counters.Add("missions.stars", gainedStars);
                if (o.fallen.Count == 0) c.Counters.Add("perfect.wins");
                if (m.region == "tower") { if (m.id.StartsWith("tower_") && int.TryParse(m.id.Substring(6), out var fl)) { d.tower = Math.Max(d.tower, fl); c.Counters.Max("tower.best", fl); } }
                if (r.rounds <= 6) c.Counters.Add("fast.wins");
                o.xp = Math.Round(m.xp * xpMul);
            }
            else { c.Counters.Add("missions.lost"); o.xp = Math.Round(m.xp * 0.25 * xpMul); }
            foreach (var u in party)
            {
                bool dead = r.deadHeroes.Contains(setup.heroes[party.IndexOf(u)].id);
                int lv = c.Roster.AddXp(u, dead ? o.xp * 0.5 : o.xp);
                if (lv > 0) o.levelUps[u.uid] = lv;
                // Camp supplies soften attrition while infirmary, potions and healers still matter.
                double maxHp = c.Roster.MaxHp(u);
                if (dead) c.Roster.SetHp(u, maxHp * c.Content.Bal("battle.revive_hp", 0.2));
                else if (r.win) c.Roster.SetHp(u, c.Roster.Hp(u) + maxHp * c.Content.Bal("battle.recovery_hp", 0.1));
            }
            o.rankReady = c.Ranks.FameReached;
            c.Get<IBattleRules>()?.Result(m, r.win);
            c.Roster.Dirty(); c.Changed();
            return o;
        }

        // ===================== expeditions =====================
        public double PartyPowerFor(MissionDef m, IEnumerable<UnitInstance> party)
        {
            var list = party.Where(u => u != null).GroupBy(u => u.uid).Select(g => g.First()).ToList();
            double p = list.Sum(u => c.Roster.Power(u) * (0.5 + 0.5 * c.Roster.HpFrac(u)));
            int match = list.Where(u => m.recommend.Contains(u.classId)).Select(u => u.classId).Distinct().Count();
            int synergies = c.Roster.ActiveSynergies(list).Count;
            return p * (1 + Math.Min(3, match) * 0.08 + Math.Min(3, synergies) * 0.04) * (1 + c.Mods.Pct("expedition.power"));
        }
        public double SuccessChance(MissionDef m, IEnumerable<UnitInstance> party)
        {
            double ratio = PartyPowerFor(m, party) / Math.Max(1, m.power);
            double p = 1.0 / (1.0 + Math.Exp(-(ratio - 1.0) * 4.5)) + c.Mods.Add("expedition.success");
            return Math.Max(0.05, Math.Min(0.97, p));
        }
        public int ExpeditionSlots => (int)(c.Content.Bal("expedition.slots", 1) + c.Mods.Add("expedition.slots"));
        public IEnumerable<TimedTask> Expeditions => c.Timers.Tasks.Where(t => t.kind == "expedition");

        public bool StartExpedition(MissionDef m, IList<UnitInstance> party, out string why)
        {
            if (!ValidParty(party, out why)) return false;
            if (m == null || !d.board.Any(x => x.id == m.id)) { why = "This contract has expired. Return to Board > Contracts and choose a current offer."; return false; }
            if (!Unlocked(m)) { why = "Mission locked"; return false; }
            if (Expeditions.Count() >= ExpeditionSlots) { why = "No free expedition party"; return false; }
            double dur = Math.Max(10, m.duration / (1 + c.Mods.Pct("expedition.speed")));
            var task = c.Timers.Start("expedition", m.id, dur, party.Select(u => u.uid), 0, JsonUtility.ToJson(m));
            task.successChance = SuccessChance(m, party);
            d.board.RemoveAll(x => x.id == m.id);
            c.Counters.Add("expeditions.started"); c.Changed(); return true;
        }

        public ExpeditionResult ClaimExpedition(TimedTask t)
        {
            if (t == null || t.kind != "expedition" || !t.done || !c.Timers.Tasks.Contains(t)) return null;
            var m = JsonUtility.FromJson<MissionDef>(t.data);
            if (m == null) return null;
            var party = t.units.Select(u => c.Roster.Get(u)).Where(u => u != null).ToList();
            var rng = new Rng(t.seed);
            double p = t.successChance > 0 ? t.successChance : SuccessChance(m, party);
            double roll = rng.D();
            var r = new ExpeditionResult { mission = m, units = party, outcome = roll < p * 0.28 ? 2 : roll < p ? 1 : 0 };
            double mul = r.outcome == 2 ? 1.6 : r.outcome == 1 ? 1.0 : 0.25;
            double goldMul = 1 + c.Mods.Pct("gold.gain") + c.Roster.PartyPct(party, "gold.gain");
            double luck = c.Mods.Add("loot.luck") + c.Roster.PartyPct(party, "loot.luck");
            foreach (var rw in m.rewards) r.res.Add(new Res(rw.id, Math.Max(1, Math.Round(rw.n * mul * (rw.id == "gold" ? goldMul : 1)))));
            if (r.outcome > 0)
            {
                r.drops = Loot.Merge(Loot.Roll(c, m.loot, luck + (r.outcome == 2 ? 0.5 : 0), rng));
                if (r.outcome == 2) r.drops.AddRange(Loot.Roll(c, m.loot, luck + 0.5, rng));
            }
            foreach (var rw in r.res) c.Give(rw);
            Loot.Apply(c, r.drops);
            r.xp = Math.Round(m.xp * mul * (1 + c.Mods.Pct("xp.gain") + c.Roster.PartyPct(party, "xp.gain")));
            foreach (var u in party)
            {
                int lv = c.Roster.AddXp(u, r.xp); if (lv > 0) r.levelUps[u.uid] = lv;
                if (r.outcome == 0) c.Roster.SetHp(u, c.Roster.Hp(u) - c.Roster.MaxHp(u) * 0.45);
                else if (r.outcome == 1) c.Roster.SetHp(u, c.Roster.Hp(u) - c.Roster.MaxHp(u) * 0.12);
            }
            c.Counters.Add(r.outcome > 0 ? "expeditions.won" : "expeditions.failed");
            if (r.outcome > 0) c.Counters.Add("fame", m.boss ? 2 : 1);
            c.Timers.Claim(t); c.Roster.Dirty(); c.Changed();
            return r;
        }
    }
}
