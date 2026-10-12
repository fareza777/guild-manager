using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ManagerKit
{
    [Serializable]
    public class TimedTask
    {
        public string uid, kind, refId, data;
        public List<string> units = new List<string>();
        public double start, end;
        public int level, seed;
        public bool done;
        public double successChance; // departure forecast; zero falls back for older saves
    }

    /// <summary>
    /// All timers are absolute UTC timestamps, so offline progress is automatic: on the next Tick
    /// everything that finished while the app was closed completes.
    /// </summary>
    public class TimerSystem : ISystem, ISaveable, ITickable
    {
        GameContext c;
        readonly List<TimedTask> tasks = new List<TimedTask>();
        readonly Dictionary<string, Action<TimedTask>> handlers = new Dictionary<string, Action<TimedTask>>();
        readonly HashSet<string> keep = new HashSet<string>();
        [Serializable] class Data { public List<TimedTask> t = new List<TimedTask>(); }
        public IReadOnlyList<TimedTask> Tasks => tasks;
        public string SaveKey => "timers";
        public void Init(GameContext ctx) { c = ctx; }
        public string Save() => JsonUtility.ToJson(new Data { t = tasks });
        public void Load(string json) { tasks.Clear(); var d = JsonUtility.FromJson<Data>(json); if (d != null) tasks.AddRange(d.t); }
        public void Reset() { tasks.Clear(); }

        /// <param name="keepUntilClaimed">task stays (done=true) after finishing until Claim() is called.</param>
        public void OnComplete(string kind, Action<TimedTask> h, bool keepUntilClaimed = false) { handlers[kind] = h; if (keepUntilClaimed) keep.Add(kind); }

        public TimedTask Start(string kind, string refId, double seconds, IEnumerable<string> units = null, int level = 0, string data = null)
        {
            var t = new TimedTask { uid = Util.Uid(), kind = kind, refId = refId, start = Util.Now, end = Util.Now + Math.Max(0, seconds), level = level, data = data, seed = c.Rng.Int(1, int.MaxValue) };
            if (units != null) t.units.AddRange(units);
            tasks.Add(t); c.Changed(); return t;
        }
        public double Remaining(TimedTask t) => Math.Max(0, t.end - Util.Now);
        public double Progress(TimedTask t) => t.end <= t.start ? 1 : Mathf.Clamp01((float)((Util.Now - t.start) / (t.end - t.start)));
        public void Claim(TimedTask t) { tasks.Remove(t); c.Changed(); }
        public bool UnitBusy(string uid) { foreach (var t in tasks) if (t.units.Contains(uid)) return true; return false; }
        public TimedTask UnitTask(string uid) { foreach (var t in tasks) if (t.units.Contains(uid)) return t; return null; }
        public IEnumerable<TimedTask> OfKind(string kind) => tasks.Where(t => t.kind == kind);

        public int SpeedCost(TimedTask t) => Remaining(t) <= 0 ? 0 : Math.Max(1, (int)Math.Ceiling(Remaining(t) / 60.0 * c.Content.Bal("gem.per_min", 0.4)));
        public bool Speedup(TimedTask t)
        {
            int cost = SpeedCost(t);
            if (t.done || !c.Wallet.Spend("gems", cost)) return false;
            t.end = Util.Now; c.Counters.Add("speedups"); Tick(Util.Now); c.Changed(); return true;
        }

        public void Tick(double now)
        {
            for (int i = tasks.Count - 1; i >= 0; i--)
            {
                var t = tasks[i];
                if (t.done || now < t.end) continue;
                t.done = true;
                handlers.TryGetValue(t.kind, out var h);
                if (!keep.Contains(t.kind)) tasks.RemoveAt(i);
                h?.Invoke(t);
                c.Changed();
            }
        }
    }

    // =====================================================================================
    public class UpgradeSystem : ISystem, ISaveable, IModSource, IPostInit, INewGame
    {
        GameContext c;
        readonly Dictionary<string, int> lv = new Dictionary<string, int>();
        [Serializable] class Data { public List<KVD> l = new List<KVD>(); }
        public string SaveKey => "upgrades";
        public void Init(GameContext ctx) { c = ctx; }
        public string Save() { var d = new Data(); foreach (var p in lv) d.l.Add(new KVD(p.Key, p.Value)); return JsonUtility.ToJson(d); }
        public void Load(string json) { lv.Clear(); var d = JsonUtility.FromJson<Data>(json); if (d != null) foreach (var e in d.l) lv[e.k] = (int)e.v; }
        public void Reset() { lv.Clear(); }
        public void OnNewGame() { foreach (var u in c.Content.Upgrades) if (u.start > 0) lv[u.id] = u.start; c.Mods.Dirty(); }

        public void PostInit()
        {
            c.Stats.Register("facility", k => Level(k));
            c.Stats.Register("research", k => Level(k));
            Action<TimedTask> done = t =>
            {
                var d = c.Content.Upgrade(t.refId); if (d == null) return;
                lv[d.id] = Math.Max(Level(d.id), t.level);
                c.Mods.Dirty(); c.Counters.Add("upgrade.done"); c.Counters.Add(d.kind + ".done");
                c.Counters.Set("lvl." + d.id, lv[d.id]);
                c.Note(NoteKind.Build, $"{d.name} upgraded to Lv {lv[d.id]}!", d.icon);
            };
            c.Timers.OnComplete("build", done);
            c.Timers.OnComplete("research", done);
        }

        public int Level(string id) => lv.TryGetValue(id, out var l) ? l : 0;
        public int MaxLevel(UpgradeDef d) => d.kind == "facility" ? Math.Min(d.max, (int)(c.Content.Bal("facility.base_max", 2) + c.Content.Bal("facility.per_rank", 2) * Math.Max(0, c.Ranks.Index))) : d.max;
        public bool Unlocked(UpgradeDef d) => c.MeetsAll(d.req);
        public TimedTask Pending(string id) => c.Timers.Tasks.FirstOrDefault(t => (t.kind == "build" || t.kind == "research") && t.refId == id);
        public int NextLevel(UpgradeDef d) => Level(d.id) + 1;

        public List<Res> Cost(UpgradeDef d, int level)
        {
            var l = new List<Res>(); double m = Math.Pow(d.costGrowth, Math.Max(0, level - 1));
            foreach (var b in d.baseCost) l.Add(new Res(b.id, Math.Round(b.n * m * (b.id == "gold" ? (1 - Math.Min(0.5, c.Mods.Pct("build.discount"))) : 1))));
            return l;
        }
        public double Time(UpgradeDef d, int level) => Math.Max(d.baseTime <= 0 ? 0 : 3, Math.Round(d.baseTime * Math.Pow(d.timeGrowth, Math.Max(0, level - 1)) / (1 + c.Mods.Pct(d.kind == "research" ? "research.speed" : "build.speed"))));
        public int Slots(string kind) => (int)(kind == "research" ? c.Content.Bal("research.slots", 1) + c.Mods.Add("research.slots") : c.Content.Bal("builders", 1) + c.Mods.Add("builders"));
        public int Busy(string kind) => c.Timers.Tasks.Count(t => t.kind == (kind == "research" ? "research" : "build"));

        public string Blocker(UpgradeDef d)
        {
            if (!Unlocked(d)) return "Locked";
            if (Pending(d.id) != null) return "In progress";
            int next = NextLevel(d);
            if (next > d.max) return "Max level";
            if (next > MaxLevel(d)) return "Needs higher Guild Rank";
            if (Busy(d.kind) >= Slots(d.kind)) return d.kind == "research" ? "Scholar busy" : "Builders busy";
            return null;
        }
        public bool CanStart(UpgradeDef d) => Blocker(d) == null && c.CanPay(Cost(d, NextLevel(d)));
        public bool Start(UpgradeDef d)
        {
            if (!CanStart(d)) return false;
            int next = NextLevel(d);
            c.Pay(Cost(d, next));
            double t = Time(d, next);
            c.Timers.Start(d.kind == "research" ? "research" : "build", d.id, t, null, next);
            c.Counters.Add("upgrade.started");
            if (t <= 0) c.Timers.Tick(Util.Now);
            c.Changed(); return true;
        }

        public void CollectMods(List<Mod> into)
        {
            foreach (var d in c.Content.Upgrades)
            {
                int l = Level(d.id); if (l <= 0) continue;
                foreach (var e in d.effects) into.Add(new Mod(e.key, e.@base + e.per * (l - 1), e.op));
            }
        }
        /// <summary>Human-readable per-level effect text, e.g. "+2 Roster capacity".</summary>
        public double EffectValue(EffectDef e, int level) => level <= 0 ? 0 : e.@base + e.per * (level - 1);
    }

    // =====================================================================================
    public class StationSystem : ISystem, IPostInit
    {
        GameContext c;
        public void Init(GameContext ctx) { c = ctx; }
        public void PostInit()
        {
            c.Timers.OnComplete("train", t =>
            {
                var sd = c.Content.StationMap.TryGetValue(t.refId, out var s) ? s : null;
                foreach (var uid in t.units)
                {
                    var u = c.Roster.Get(uid); if (u == null) continue;
                    double xp = c.Roster.XpToNext(u.level) * (sd?.value ?? 1) * (1 + c.Mods.Pct("train.xp")) * (1 + c.Roster.TraitPct(u, "xp.gain"));
                    c.Roster.AddXp(u, xp); c.Counters.Add("train.done");
                    c.Note(NoteKind.Good, $"{u.name} finished training");
                }
            });
            c.Timers.OnComplete("heal", t =>
            {
                foreach (var uid in t.units) { var u = c.Roster.Get(uid); if (u == null) continue; u.hp = -1; c.Counters.Add("heal.done"); c.Note(NoteKind.Good, $"{u.name} is fully healed"); }
                c.Roster.Dirty();
            });
        }

        public StationDef ByKind(string kind) => c.Content.Stations.FirstOrDefault(s => s.kind == kind);
        public int Slots(StationDef s) => c.Upgrades.Level(s.upgradeId) <= 0 ? 0 : (int)(s.slotsBase + c.Mods.Add(s.slotsKey));
        public IEnumerable<TimedTask> Active(StationDef s) => c.Timers.Tasks.Where(t => t.kind == s.kind && t.refId == s.id);
        public int Free(StationDef s) => Slots(s) - Active(s).Count();

        public double Duration(StationDef s, UnitInstance u)
        {
            double speed = 1 + c.Mods.Pct(s.speedKey);
            if (s.kind == "heal") return Math.Max(5, (1 - c.Roster.HpFrac(u)) * s.baseTime * (1 + u.level * 0.04) / speed);
            return Math.Max(5, (s.baseTime + s.perLevelTime * u.level) / speed);
        }
        public List<Res> Cost(StationDef s, UnitInstance u)
        {
            if (s.kind == "heal") return new List<Res>();
            return new List<Res> { new Res("gold", Math.Round(s.goldBase + s.goldPerLevel * u.level)) };
        }
        public string Blocker(StationDef s, UnitInstance u)
        {
            if (c.Upgrades.Level(s.upgradeId) <= 0) return "Not built";
            if (c.Roster.IsBusy(u)) return "Busy";
            if (Free(s) <= 0) return "No free slot";
            if (s.kind == "train" && u.level >= c.Roster.LevelCap(u)) return "At level cap";
            if (s.kind == "heal" && c.Roster.HpFrac(u) >= 0.999) return "Already healthy";
            if (!c.CanPay(Cost(s, u))) return "Not enough gold";
            return null;
        }
        public bool Start(StationDef s, UnitInstance u)
        {
            if (Blocker(s, u) != null) return false;
            c.Pay(Cost(s, u));
            c.Timers.Start(s.kind, s.id, Duration(s, u), new[] { u.uid });
            c.Counters.Add(s.kind + ".started"); return true;
        }
    }
}
