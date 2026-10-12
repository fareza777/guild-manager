using System;
using System.Collections.Generic;
using System.Linq;

namespace ManagerKit
{
    public enum BattleDoctrine { Balanced, Assault, Guard }
    public class Status { public string type; public double v; public int turns; }

    public class Combatant
    {
        public int id, side;                       // side 0 = heroes, 1 = enemies
        public string name, sprite, skin, classId, anim, defId, unitUid;
        public bool boss, front, ranged, monster;
        public float scale = 1f;
        public double hp, maxHp, atk, def, spd, crit, shield, gauge;
        public List<SkillDef> skills = new List<SkillDef>();
        public int[] cd = new int[0];
        public List<Status> st = new List<Status>();
        public bool Alive => hp > 0;
        public double Mod(string t) { double s = 0; foreach (var x in st) if (x.type == t) s += x.v; return s; }
        public bool Has(string t) { foreach (var x in st) if (x.type == t) return true; return false; }
    }

    public class Hit { public int target, dmg, heal, shield; public bool crit, miss, kill; public string status; }
    public class BattleEvent
    {
        public string type;       // wave | act | tick | end
        public int actor = -1, wave;
        public string skill, vfx, text;
        public List<Hit> hits = new List<Hit>();
        public List<int> spawn = new List<int>();
    }
    public class BattleSetup
    {
        public List<Combatant> heroes = new List<Combatant>();
        public List<List<Combatant>> waves = new List<List<Combatant>>();
        public int seed = 1, maxTurns = 400;
        public double defK = 1.0;
        public BattleDoctrine doctrine;
    }
    public class BattleResult
    {
        public bool win; public int rounds, actions, heroActions;
        public List<BattleEvent> log = new List<BattleEvent>();
        public Dictionary<int, double> endHp = new Dictionary<int, double>();
        public List<int> deadHeroes = new List<int>();
        public List<Combatant> killed = new List<Combatant>();
        public int wavesCleared;
    }

    /// <summary>
    /// Deterministic auto-battle simulator (ATB gauge). Produces an event log the view replays,
    /// so the same code resolves battles instantly for offline / expedition / balance tests.
    /// </summary>
    public static class BattleSim
    {
        public static BattleResult Run(BattleSetup s)
        {
            var rng = new Rng(s.seed);
            var res = new BattleResult();
            var heroes = s.heroes;
            int nextId = 0;
            foreach (var h in heroes) { h.id = nextId++; h.gauge = rng.Range(0f, 35f); h.cd = new int[h.skills.Count]; for (int i = 0; i < h.cd.Length; i++) h.cd[i] = h.skills[i].cd > 0 ? Math.Min(1, h.skills[i].cd) : 0; }
            int turns = 0;
            for (int w = 0; w < s.waves.Count; w++)
            {
                var foes = s.waves[w];
                var ev = new BattleEvent { type = "wave", wave = w };
                foreach (var f in foes) { f.id = nextId++; f.gauge = rng.Range(0f, 40f); f.cd = new int[f.skills.Count]; for (int i = 0; i < f.cd.Length; i++) f.cd[i] = f.skills[i].cd > 0 ? 1 : 0; ev.spawn.Add(f.id); }
                res.log.Add(ev);
                var all = new List<Combatant>(); all.AddRange(heroes); all.AddRange(foes);
                while (foes.Any(f => f.Alive) && heroes.Any(h => h.Alive) && turns < s.maxTurns)
                {
                    // next actor = smallest time-to-full-gauge
                    Combatant actor = null; double best = double.MaxValue;
                    foreach (var c in all)
                    {
                        if (!c.Alive) continue;
                        double spd = Math.Max(1, c.spd * (1 + c.Mod("spd_up")));
                        double t = (100 - c.gauge) / spd;
                        if (t < best - 1e-9) { best = t; actor = c; }
                    }
                    if (actor == null) break;
                    foreach (var c in all) if (c.Alive) c.gauge += best * Math.Max(1, c.spd * (1 + c.Mod("spd_up")));
                    actor.gauge -= 100; turns++;
                    res.actions++; if (actor.side == 0) res.heroActions++;
                    Act(s, rng, actor, actor.side == 0 ? foes : heroes, actor.side == 0 ? heroes : foes, res);
                    foreach (var c in all) if (!c.Alive && c.side == 1 && !res.killed.Contains(c)) res.killed.Add(c);
                }
                if (!heroes.Any(h => h.Alive)) break;
                if (foes.All(f => !f.Alive)) res.wavesCleared++;
                else break;
            }
            res.win = res.wavesCleared == s.waves.Count && heroes.Any(h => h.Alive);
            res.rounds = heroes.Count == 0 ? 0 : (int)Math.Ceiling(res.heroActions / (double)heroes.Count);
            foreach (var h in heroes) { res.endHp[h.id] = Math.Max(0, h.hp); if (!h.Alive) res.deadHeroes.Add(h.id); }
            res.log.Add(new BattleEvent { type = "end", text = res.win ? "win" : "lose" });
            return res;
        }

        static void Act(BattleSetup s, Rng rng, Combatant a, List<Combatant> foes, List<Combatant> allies, BattleResult res)
        {
            // --- start of turn: cooldowns, statuses ---
            for (int i = 0; i < a.cd.Length; i++) if (a.cd[i] > 0) a.cd[i]--;
            var activeStatuses = a.st.ToList();
            var tick = new BattleEvent { type = "tick", actor = a.id };
            for (int i = a.st.Count - 1; i >= 0; i--)
            {
                var x = a.st[i];
                if (x.type == "poison") { int d = (int)Math.Max(1, Math.Round(x.v)); a.hp -= d; tick.hits.Add(new Hit { target = a.id, dmg = d, status = "poison", kill = a.hp <= 0 }); }
            }
            if (tick.hits.Count > 0) res.log.Add(tick);
            if (!a.Alive) { EndTurn(a, activeStatuses); return; }
            if (a.Has("stun")) { res.log.Add(new BattleEvent { type = "act", actor = a.id, skill = "stunned", text = "stunned" }); EndTurn(a, activeStatuses); return; }

            // --- choose skill ---
            int si = 0;
            // Preserve an endangered ally before spending a turn on offensive blessings.
            bool emergency = allies.Any(x => x.Alive && x.hp / x.maxHp < (s.doctrine == BattleDoctrine.Guard && a.side == 0 ? 0.8 : 0.6));
            for (int i = a.skills.Count - 1; i >= 1; i--)
            {
                if (a.cd[i] > 0) continue;
                var sk = a.skills[i];
                if (emergency && sk.type != "heal" && a.skills.Where((x, k) => a.cd[k] <= 0).Any(x => x.type == "heal")) continue;
                if (sk.type == "heal" && !allies.Any(x => x.Alive && x.hp / x.maxHp < (s.doctrine == BattleDoctrine.Guard && a.side == 0 ? 0.8 : 0.7))) continue;
                if (sk.status == "taunt" && allies.Count(x => x.Alive) <= 1) continue;
                if (sk.type == "buff" && !string.IsNullOrEmpty(sk.status) && allies.Where(x => x.Alive).All(x => x.Has(sk.status)) && sk.target != "self") continue;
                if (sk.type == "buff" && sk.target == "self" && a.Has(sk.status)) continue;
                si = i; break;
            }
            var skill = a.skills[si];
            if (skill.cd > 0) a.cd[si] = skill.cd;

            // --- targets ---
            var targets = PickTargets(s, rng, a, skill, foes, allies);
            if (targets.Count == 0) { EndTurn(a, activeStatuses); return; }
            var ev = new BattleEvent { type = "act", actor = a.id, skill = skill.id, vfx = skill.vfx };
            foreach (var t in targets)
            {
                var h = new Hit { target = t.id };
                if (skill.type == "heal")
                {
                    int amt = (int)Math.Round(a.atk * (1 + a.Mod("atk_up")) * skill.power * rng.Range(0.95f, 1.05f));
                    double before = t.hp; t.hp = Math.Min(t.maxHp, t.hp + amt); h.heal = (int)(t.hp - before);
                }
                else if (skill.type == "buff") { }
                else
                {
                    double evade = t.Has("evade") ? 0.5 : 0;
                    if (rng.Chance(0.04 + evade)) h.miss = true;
                    else
                    {
                        double atk = a.atk * Math.Max(0.1, 1 + a.Mod("atk_up") - a.Mod("weaken"));
                        double def = t.def * (1 + t.Mod("def_up")) * (skill.type == "magic" ? 0.5 : 1);
                        double dmg = atk * skill.power * rng.Range(0.92f, 1.08f) * 100.0 / (100.0 + def * s.defK);
                        h.crit = rng.Chance(a.crit / 100.0 + skill.crit);
                        if (h.crit) dmg *= 1.6;
                        int d = (int)Math.Max(1, Math.Round(dmg));
                        if (t.shield > 0) { double ab = Math.Min(t.shield, d); t.shield -= ab; d -= (int)ab; h.shield = (int)ab; if (t.shield <= 0) t.st.RemoveAll(x => x.type == "shield"); }
                        t.hp -= d; h.dmg = d; h.kill = t.hp <= 0;
                    }
                }
                if (!h.miss && !string.IsNullOrEmpty(skill.status) && t.Alive && skill.type != "heal")
                {
                    ApplyStatus(a, t, skill); h.status = skill.status;
                }
                ev.hits.Add(h);
            }
            res.log.Add(ev);
            EndTurn(a, activeStatuses, targets.Contains(a) ? skill.status : null);
        }

        static void EndTurn(Combatant a, List<Status> active, string refreshed = null)
        {
            // A one-turn buff affects its owner's action. Newly applied self buffs keep their full duration.
            foreach (var x in active)
            {
                if (!a.st.Contains(x) || x.type == refreshed) continue;
                if (--x.turns > 0) continue;
                if (x.type == "shield") a.shield = 0;
                a.st.Remove(x);
            }
        }

        static void ApplyStatus(Combatant a, Combatant t, SkillDef sk)
        {
            double v = sk.statusV;
            if (sk.status == "poison") v = Math.Max(1, a.atk * sk.statusV);
            if (sk.status == "shield") { t.shield = Math.Max(t.shield, a.atk * sk.statusV); v = t.shield; }
            var ex = t.st.Find(x => x.type == sk.status);
            if (ex != null) { ex.turns = Math.Max(ex.turns, sk.statusT); ex.v = Math.Max(ex.v, v); }
            else t.st.Add(new Status { type = sk.status, v = v, turns = Math.Max(1, sk.statusT) });
        }

        static List<Combatant> PickTargets(BattleSetup setup, Rng rng, Combatant a, SkillDef sk, List<Combatant> foes, List<Combatant> allies)
        {
            var res = new List<Combatant>();
            var af = allies.Where(x => x.Alive).ToList(); var ef = foes.Where(x => x.Alive).ToList();
            switch (sk.target)
            {
                case "self": res.Add(a); break;
                case "ally_all": res.AddRange(af); break;
                case "ally_lowest": var low = af.OrderBy(x => x.hp / x.maxHp).FirstOrDefault(); if (low != null) res.Add(low); break;
                case "enemy_all": res.AddRange(ef); break;
                case "enemy_two":
                    var first = One(setup, rng, a, sk, ef); if (first != null) { res.Add(first); var rest = ef.Where(x => x != first).ToList(); var second = One(setup, rng, a, sk, rest); if (second != null) res.Add(second); }
                    break;
                default: var t = One(setup, rng, a, sk, ef); if (t != null) res.Add(t); break;
            }
            return res;
        }
        static Combatant One(BattleSetup setup, Rng rng, Combatant actor, SkillDef skill, List<Combatant> list)
        {
            if (list.Count == 0) return null;
            var taunt = list.Where(x => x.Has("taunt")).ToList();
            if (taunt.Count > 0) return rng.Pick(taunt);
            if (actor.side == 0)
            {
                var eligible = list;
                // Control skills spread their effect; focused attacks finish wounded threats.
                if (!string.IsNullOrEmpty(skill.status))
                {
                    var clean = list.Where(x => !x.Has(skill.status)).ToList();
                    if (clean.Count > 0) eligible = clean;
                }
                if (setup.doctrine == BattleDoctrine.Assault) return eligible.OrderBy(x => x.hp).ThenBy(x => x.id).First();
                if (setup.doctrine == BattleDoctrine.Guard) return eligible.OrderByDescending(x => x.atk * x.spd).ThenBy(x => x.hp).First();
                return eligible.OrderBy(x => x.hp / x.maxHp).ThenBy(x => x.hp).ThenBy(x => x.id).First();
            }
            var w = list.Select(x => x.front ? 3.0 : 1.0).ToList();
            return list[rng.Weighted(w)];
        }
    }
}
