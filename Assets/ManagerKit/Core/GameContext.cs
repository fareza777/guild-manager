using System;
using System.Collections.Generic;
using UnityEngine;

namespace ManagerKit
{
    public interface ISystem { void Init(GameContext c); }
    /// <summary>Anything that persists. Each system serializes itself into its own blob.</summary>
    public interface ISaveable { string SaveKey { get; } string Save(); void Load(string json); void Reset(); }
    public interface ITickable { void Tick(double now); }
    public interface IModSource { void CollectMods(List<Mod> into); }

    public enum NoteKind { Info, Good, Warn, Reward, Level, Quest, Rank, Build }
    public struct Note { public NoteKind kind; public string text; public string icon; }

    /// <summary>
    /// Root container of the manager-sim template. Owns content + every system. Games add their own systems with Add().
    /// </summary>
    public class GameContext
    {
        public const int SaveVersion = 1;
        public ContentDb Content;
        public Rng Rng = new Rng();
        public readonly ModifierBus Mods;
        public readonly Counters Counters;
        public readonly StatResolver Stats;
        public readonly Wallet Wallet;
        public readonly Inventory Inventory;
        public readonly Roster Roster;
        public readonly TimerSystem Timers;
        public readonly UpgradeSystem Upgrades;
        public readonly StationSystem Stations;
        public readonly RankSystem Ranks;
        public readonly MissionSystem Missions;
        public readonly QuestSystem Quests;
        public readonly RandomEvents Events;
        public readonly IdleVault Vault;
        public readonly DailySystem Daily;
        public readonly Recruiter Recruiter;
        public readonly Market Market;
        public readonly Crafting Crafting;
        public readonly GuildMeta Meta = new GuildMeta();

        public readonly List<object> Systems = new List<object>();
        public event Action<Note> OnNote;
        public event Action OnChanged;
        public double LastTick;
        public double SessionStart;
        public OfflineReport LastOffline;

        public GameContext(ContentDb content)
        {
            Content = content;
            Mods = Add(new ModifierBus());
            Counters = Add(new Counters());
            Stats = Add(new StatResolver());
            Wallet = Add(new Wallet());
            Inventory = Add(new Inventory());
            Roster = Add(new Roster());
            Timers = Add(new TimerSystem());
            Upgrades = Add(new UpgradeSystem());
            Stations = Add(new StationSystem());
            Ranks = Add(new RankSystem());
            Missions = Add(new MissionSystem());
            Quests = Add(new QuestSystem());
            Events = Add(new RandomEvents());
            Vault = Add(new IdleVault());
            Daily = Add(new DailySystem());
            Recruiter = Add(new Recruiter());
            Market = Add(new Market());
            Crafting = Add(new Crafting());
            Add(Meta);
        }

        public T Add<T>(T sys) where T : class
        {
            Systems.Add(sys);
            if (sys is ISystem s) s.Init(this);
            return sys;
        }
        public T Get<T>() where T : class { foreach (var s in Systems) if (s is T t) return t; return null; }

        /// <summary>Wires every system after all are registered (so Init order does not matter).</summary>
        public void Start()
        {
            foreach (var s in Systems) if (s is IPostInit p) p.PostInit();
            Mods.Dirty();
        }

        // ---------- notifications ----------
        public void Note(NoteKind k, string text, string icon = null) => OnNote?.Invoke(new Note { kind = k, text = text, icon = icon });
        public void Changed() => OnChanged?.Invoke();

        // ---------- unified resource handling (currency or item) ----------
        public bool Has(Res r) => Content.IsCurrency(r.id) ? Wallet.Has(r.id, r.n) : Inventory.Count(r.id) >= r.n;
        public bool CanPay(IEnumerable<Res> cost) { if (cost == null) return true; foreach (var r in cost) if (!Has(r)) return false; return true; }
        public bool Pay(IEnumerable<Res> cost)
        {
            if (!CanPay(cost)) return false;
            if (cost == null) return true;
            foreach (var r in cost) { if (Content.IsCurrency(r.id)) Wallet.Spend(r.id, r.n); else Inventory.Remove(r.id, (int)r.n); }
            Changed(); return true;
        }
        public void Give(Res r, string reason = null)
        {
            if (r == null || r.n <= 0) return;
            if (Content.IsCurrency(r.id)) Wallet.Add(r.id, r.n);
            else if (Content.ItemMap.ContainsKey(r.id)) Inventory.AddItem(r.id, (int)Math.Max(1, Math.Round(r.n)), Rng);
            else if (r.id == "fame") Counters.Add("fame", r.n);
        }
        public void Give(IEnumerable<Res> l) { if (l != null) foreach (var r in l) Give(r); Changed(); }
        public string ResName(string id) => Content.CurrencyMap.TryGetValue(id, out var c) ? c.name : (Content.ItemMap.TryGetValue(id, out var i) ? i.name : id);
        public bool MeetsAll(IEnumerable<Req> reqs) { if (reqs == null) return true; foreach (var r in reqs) if (Stats.Get(r.stat) < r.min) return false; return true; }

        // ---------- frame update ----------
        public void Tick()
        {
            double now = Util.Now;
            if (now - LastTick < 0.25) return;
            LastTick = now;
            foreach (var s in Systems) if (s is ITickable t) t.Tick(now);
        }

        // ---------- lifecycle ----------
        public void NewGame()
        {
            foreach (var s in Systems) if (s is ISaveable sv) sv.Reset();
            SessionStart = Util.Now;
            foreach (var s in Systems) if (s is INewGame n) n.OnNewGame();
            Mods.Dirty(); Changed();
        }
        public string SaveBlobs(out int version)
        {
            version = SaveVersion;
            var env = new SaveEnvelope { version = SaveVersion, savedUtc = Util.Now, gameId = Content.GameId };
            foreach (var s in Systems) if (s is ISaveable sv) env.blobs.Add(new KVS(sv.SaveKey, sv.Save()));
            return JsonUtility.ToJson(env);
        }
        public bool LoadBlobs(string json)
        {
            var env = JsonUtility.FromJson<SaveEnvelope>(json);
            if (env == null || env.blobs == null || env.blobs.Count == 0) return false;
            var map = new Dictionary<string, string>(); foreach (var b in env.blobs) map[b.k] = b.v;
            foreach (var s in Systems) if (s is ISaveable sv) { if (map.TryGetValue(sv.SaveKey, out var blob)) sv.Load(blob); else sv.Reset(); }
            SessionStart = Util.Now;
            Mods.Dirty();
            LastOffline = OfflineReport.Build(this, env.savedUtc);
            return true;
        }
    }

    public interface IPostInit { void PostInit(); }
    public interface INewGame { void OnNewGame(); }

    [Serializable] public class SaveEnvelope { public int version; public double savedUtc; public string gameId; public List<KVS> blobs = new List<KVS>(); }

    // =====================================================================================
    public class Wallet : ISystem, ISaveable
    {
        GameContext c; readonly Dictionary<string, double> amt = new Dictionary<string, double>();
        [Serializable] class Data { public List<KVD> a = new List<KVD>(); }
        public event Action<string, double> OnChange;
        public void Init(GameContext ctx) { c = ctx; }
        public string SaveKey => "wallet";
        public double Get(string id) => amt.TryGetValue(id, out var v) ? v : 0;
        public bool Has(string id, double n) => Get(id) + 1e-6 >= n;
        public void Add(string id, double n)
        {
            if (n == 0) return;
            amt[id] = Get(id) + n;
            if (n > 0) c.Counters.Add("earn." + id, n);
            OnChange?.Invoke(id, n);
        }
        public bool Spend(string id, double n)
        {
            if (!Has(id, n)) return false;
            amt[id] = Get(id) - n; c.Counters.Add("spend." + id, n);
            OnChange?.Invoke(id, -n); return true;
        }
        public string Save() => JsonUtility.ToJson(new Data { a = amt.ToList() });
        public void Load(string json) { amt.Clear(); var d = JsonUtility.FromJson<Data>(json); if (d != null) foreach (var e in d.a) amt[e.k] = e.v; }
        public void Reset() { amt.Clear(); foreach (var cur in c.Content.Currencies) amt[cur.id] = cur.start; }
    }

    public class Counters : ISystem, ISaveable
    {
        GameContext c;
        readonly Dictionary<string, double> v = new Dictionary<string, double>();
        readonly HashSet<string> flags = new HashSet<string>();
        [Serializable] class Data { public List<KVD> v = new List<KVD>(); public List<string> f = new List<string>(); }
        public int Version { get; private set; }
        public void Init(GameContext ctx) { c = ctx; }
        public string SaveKey => "counters";
        public double Get(string k) => v.TryGetValue(k, out var x) ? x : 0;
        public void Add(string k, double n = 1) { v[k] = Get(k) + n; Version++; }
        public void Set(string k, double n) { v[k] = n; Version++; }
        public void Max(string k, double n) { if (n > Get(k)) { v[k] = n; Version++; } }
        public bool Flag(string k) => flags.Contains(k);
        public void SetFlag(string k, bool on = true) { if (on) flags.Add(k); else flags.Remove(k); Version++; }
        public string Save() => JsonUtility.ToJson(new Data { v = v.ToList(), f = new List<string>(flags) });
        public void Load(string json) { v.Clear(); flags.Clear(); var d = JsonUtility.FromJson<Data>(json); if (d != null) { foreach (var e in d.v) v[e.k] = e.v; foreach (var f in d.f) flags.Add(f); } Version++; }
        public void Reset() { v.Clear(); flags.Clear(); Version++; }
    }

    /// <summary>Aggregates modifiers from any number of sources (upgrades, ranks, traits...). Cached, invalidated with Dirty().</summary>
    public class ModifierBus : ISystem
    {
        GameContext c; bool dirty = true;
        public int Revision { get; private set; }
        readonly Dictionary<string, double> add = new Dictionary<string, double>();
        readonly Dictionary<string, double> pct = new Dictionary<string, double>();
        readonly List<Mod> tmp = new List<Mod>();
        public void Init(GameContext ctx) { c = ctx; }
        public void Dirty() { dirty = true; Revision++; }
        void Rebuild()
        {
            dirty = false; add.Clear(); pct.Clear(); tmp.Clear();
            foreach (var s in c.Systems) if (s is IModSource src) src.CollectMods(tmp);
            foreach (var m in tmp)
            {
                var d = m.op == "pct" ? pct : add;
                d[m.key] = (d.TryGetValue(m.key, out var x) ? x : 0) + m.v;
            }
        }
        public double Add(string key) { if (dirty) Rebuild(); return add.TryGetValue(key, out var x) ? x : 0; }
        public double Pct(string key) { if (dirty) Rebuild(); return pct.TryGetValue(key, out var x) ? x : 0; }
        /// <summary>(base + flat) * (1 + pct)</summary>
        public double Apply(string key, double baseValue) => (baseValue + Add(key)) * (1 + Pct(key));
        public double Mult(string key) => 1 + Pct(key);
    }

    /// <summary>Resolves "prefix.rest" stat names used by Req / GoalDef through registered providers.</summary>
    public class StatResolver : ISystem
    {
        GameContext c;
        readonly Dictionary<string, Func<string, double>> prov = new Dictionary<string, Func<string, double>>();
        public void Init(GameContext ctx) { c = ctx; }
        public void Register(string prefix, Func<string, double> f) { prov[prefix] = f; }
        public double Get(string key)
        {
            if (string.IsNullOrEmpty(key)) return 0;
            int i = key.IndexOf('.');
            if (i > 0 && prov.TryGetValue(key.Substring(0, i), out var f)) return f(key.Substring(i + 1));
            return c.Counters.Get(key);
        }
    }

    public interface IClockless { }
}
