using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ManagerKit
{
    // =====================================================================================
    public class RankSystem : ISystem, ISaveable, IModSource, IPostInit
    {
        GameContext c; int index;
        [Serializable] class Data { public int i; }
        public string SaveKey => "ranks";
        public int Index => index;
        public void Init(GameContext ctx) { c = ctx; }
        public string Save() => JsonUtility.ToJson(new Data { i = index });
        public void Load(string json) { index = JsonUtility.FromJson<Data>(json)?.i ?? 0; }
        public void Reset() { index = 0; }
        public void PostInit() { c.Stats.Register("rank", k => k == "index" ? index : k == "fame" ? Fame : 0); }

        public double Fame => c.Counters.Get("fame");
        public RankDef Current => c.Content.Rank(index);
        public RankDef Next => index + 1 < c.Content.Ranks.Count ? c.Content.Ranks[index + 1] : null;
        public bool IsMax => Next == null;
        public bool FameReached => Next != null && Fame >= Next.fame;
        public string ExamId => Next?.exam;
        public bool ExamDone => string.IsNullOrEmpty(ExamId) || c.Missions.Cleared(ExamId);
        public bool CanPromote => FameReached && ExamDone;
        public float Progress => Next == null ? 1f : Mathf.Clamp01((float)((Fame - Current.fame) / Math.Max(1, Next.fame - Current.fame)));

        public bool Promote()
        {
            if (!CanPromote) return false;
            index++; c.Counters.Add("rank.promotions"); c.Mods.Dirty();
            c.Note(NoteKind.Rank, $"Guild promoted to Rank {Current.id}: {Current.name}!");
            c.Changed(); return true;
        }
        public void CollectMods(List<Mod> into) { for (int i = 0; i <= index && i < c.Content.Ranks.Count; i++) into.AddRange(c.Content.Ranks[i].mods); }
    }

    // =====================================================================================
    [Serializable] public class QuestState { public string id; public int state; public List<KVD> b = new List<KVD>(); public double at; }

    /// <summary>Main / side / daily quests driven by counters. Goals are either deltas since activation or absolute stat values.</summary>
    public class QuestSystem : ISystem, ISaveable, ITickable, IPostInit
    {
        public const int Locked = 0, Active = 1, Ready = 2, Done = 3;
        GameContext c;
        readonly Dictionary<string, QuestState> st = new Dictionary<string, QuestState>();
        [Serializable] class Data { public List<QuestState> q = new List<QuestState>(); }
        int lastVersion = -1; double lastCheck;
        public event Action<QuestDef> OnReady;
        public string SaveKey => "quests";
        public void Init(GameContext ctx) { c = ctx; }
        public string Save() => JsonUtility.ToJson(new Data { q = st.Values.ToList() });
        public void Load(string json) { st.Clear(); var d = JsonUtility.FromJson<Data>(json); if (d != null) foreach (var q in d.q) st[q.id] = q; lastVersion = -1; }
        public void Reset() { st.Clear(); lastVersion = -1; }
        public void PostInit() { c.Stats.Register("quest", k => k.StartsWith("done.") ? (State(k.Substring(5)) == Done ? 1 : 0) : k == "doneCount" ? st.Values.Count(x => x.state == Done) : 0); }

        public int State(string id) => st.TryGetValue(id, out var s) ? s.state : Locked;
        public QuestState Get(string id) => st.TryGetValue(id, out var s) ? s : null;
        public IEnumerable<QuestDef> Of(string type) => c.Content.Quests.Where(q => q.type == type);
        public IEnumerable<QuestDef> Open(string type) => Of(type).Where(q => { int s = State(q.id); return s == Active || s == Ready; });
        public int ReadyCount => st.Values.Count(s => s.state == Ready);
        public QuestDef CurrentMain => c.Content.Quests.Where(q => q.type == "main" && (State(q.id) == Active || State(q.id) == Ready)).OrderBy(q => q.order).FirstOrDefault();

        public double Progress(QuestDef q, GoalDef g)
        {
            double cur = c.Stats.Get(g.stat);
            if (g.mode == "abs") return cur;
            var s = Get(q.id); double b = 0; if (s != null) foreach (var e in s.b) if (e.k == g.stat) b = e.v;
            return Math.Max(0, cur - b);
        }
        public bool GoalsMet(QuestDef q) { foreach (var g in q.goals) if (Progress(q, g) + 1e-6 < g.target) return false; return true; }
        public float Fraction(QuestDef q) { float f = 0; foreach (var g in q.goals) f += Mathf.Clamp01((float)(Progress(q, g) / g.target)); return q.goals.Count == 0 ? 1 : f / q.goals.Count; }

        QuestState Activate(QuestDef q)
        {
            var s = new QuestState { id = q.id, state = Active, at = Util.Now };
            foreach (var g in q.goals) s.b.Add(new KVD(g.stat, c.Stats.Get(g.stat)));
            st[q.id] = s; return s;
        }

        public void Tick(double now)
        {
            if (c.Counters.Version == lastVersion && now - lastCheck < 2) return;
            lastVersion = c.Counters.Version; lastCheck = now;
            foreach (var q in c.Content.Quests)
            {
                int s = State(q.id);
                if (s == Done) continue;
                if (s == Locked) { if (q.type != "daily" && c.MeetsAll(q.req)) { Activate(q); s = Active; } else continue; }
                if (s == Active && GoalsMet(q))
                {
                    st[q.id].state = Ready; lastVersion = -1;
                    c.Note(NoteKind.Quest, "Quest complete: " + q.title);
                    OnReady?.Invoke(q); c.Changed();
                }
            }
        }

        public bool Claim(QuestDef q)
        {
            if (State(q.id) != Ready) return false;
            st[q.id].state = Done;
            foreach (var r in q.rewards) c.Give(r);
            c.Counters.Add("quests.done"); c.Counters.Add("quests.done." + q.type);
            lastVersion = -1; Tick(Util.Now); c.Changed();
            return true;
        }

        /// <summary>Replaces today's daily quests with a fresh random selection.</summary>
        public void RollDailies(int count, int seed)
        {
            foreach (var q in Of("daily").ToList()) st.Remove(q.id);
            var pool = Of("daily").Where(q => c.MeetsAll(q.req)).ToList(); var rng = new Rng(seed);
            var w = pool.Select(p => (double)p.weight).ToList();
            for (int i = 0; i < count && pool.Count > 0; i++)
            {
                int k = rng.Weighted(w); if (k < 0) break;
                Activate(pool[k]); w[k] = 0;
            }
            lastVersion = -1; c.Changed();
        }
    }

    // =====================================================================================
    public class RandomEvents : ISystem, ISaveable, ITickable
    {
        GameContext c;
        [Serializable] class Data { public List<string> pending = new List<string>(); public double next; public string last; }
        Data d = new Data();
        public IReadOnlyList<string> Pending => d.pending;
        public string SaveKey => "events";
        public void Init(GameContext ctx) { c = ctx; }
        public string Save() => JsonUtility.ToJson(d);
        public void Load(string json) { d = JsonUtility.FromJson<Data>(json) ?? new Data(); }
        public void Reset() { d = new Data { next = Util.Now + 240 }; }
        public double Interval => c.Content.Bal("event.interval", 900);

        public void Tick(double now)
        {
            if (c.Counters.Get("missions.won") < c.Content.Bal("event.min_wins", 2)) return;
            int guard = 0;
            while (now >= d.next && guard++ < 3)
            {
                if (d.pending.Count < 3) Roll();
                d.next = Math.Max(d.next, now - Interval * 3) + Interval * c.Rng.Range(0.7f, 1.4f);
                if (d.next < now) d.next = now + Interval;
            }
        }
        public void Roll()
        {
            var pool = c.Content.Events.Where(e => c.MeetsAll(e.req) && e.id != d.last && !d.pending.Contains(e.id)).ToList();
            if (pool.Count == 0) return;
            int i = c.Rng.Weighted(pool.Select(e => e.w).ToList()); if (i < 0) return;
            d.pending.Add(pool[i].id); d.last = pool[i].id;
            c.Note(NoteKind.Info, "Something is happening at the guild hall!"); c.Changed();
        }
        public bool Available(EventChoice ch) => c.MeetsAll(ch.req) && c.CanPay(ch.cost);

        public EventOutcome Resolve(EventDef ev, int choice)
        {
            if (ev == null || !d.pending.Contains(ev.id) || choice < 0 || choice >= ev.choices.Count) return null;
            var ch = ev.choices[choice];
            if (!Available(ch)) return null;
            d.pending.Remove(ev.id);
            c.Pay(ch.cost);
            int oi = c.Rng.Weighted(ch.outcomes.Select(o => o.w).ToList());
            var o = ch.outcomes[Math.Max(0, oi)];
            foreach (var g in o.gain) c.Give(g);
            foreach (var l in o.loss) { if (c.Content.IsCurrency(l.id)) c.Wallet.Spend(l.id, Math.Min(l.n, c.Wallet.Get(l.id))); }
            if (!string.IsNullOrEmpty(o.item)) c.Inventory.AddItem(o.item, 1, c.Rng);
            if (o.hurt > 0 && c.Roster.Count > 0) { var u = c.Rng.Pick(c.Roster.All.ToList()); c.Roster.SetHp(u, c.Roster.Hp(u) - c.Roster.MaxHp(u) * o.hurt); }
            c.Counters.Add("events.done"); c.Changed();
            return o;
        }
        public void Dismiss(EventDef ev) { d.pending.Remove(ev.id); }
    }

    // =====================================================================================
    public class IdleVault : ISystem, ISaveable, INewGame
    {
        GameContext c; double last;
        [Serializable] class Data { public double last; }
        public string SaveKey => "vault";
        public void Init(GameContext ctx) { c = ctx; }
        public string Save() => JsonUtility.ToJson(new Data { last = last });
        public void Load(string json) { last = JsonUtility.FromJson<Data>(json)?.last ?? Util.Now; }
        public void Reset() { last = Util.Now; }
        public void OnNewGame() { last = Util.Now; }
        public double RatePerHour => c.Mods.Apply("income.gold_hr", 0);
        public double CapHours => c.Mods.Apply("vault.cap_hours", c.Content.Bal("vault.cap", 2));
        public double Capacity => RatePerHour * CapHours;
        public double Pending => Math.Min(Capacity, RatePerHour * Math.Max(0, Util.Now - last) / 3600.0);
        public bool Full => Pending >= Capacity - 0.01 && Capacity > 0;
        public double Collect()
        {
            double g = Math.Floor(Pending);
            if (g >= 1) { c.Wallet.Add("gold", g); c.Counters.Add("vault.collected"); c.Counters.Add("vault.gold", g); }
            last = Util.Now; c.Changed(); return g;
        }
    }

    // =====================================================================================
    [Serializable] public class CalendarDay { public List<Res> rewards = new List<Res>(); }

    public class DailySystem : ISystem, ISaveable, ITickable
    {
        GameContext c;
        [Serializable] class Data { public double day = -1; public int streak; public bool claimed; public int claimedTotal; }
        Data d = new Data();
        public int Streak => d.streak;
        public bool Claimed => d.claimed;
        public string SaveKey => "daily";
        public void Init(GameContext ctx) { c = ctx; }
        public string Save() => JsonUtility.ToJson(d);
        public void Load(string json) { d = JsonUtility.FromJson<Data>(json) ?? new Data(); }
        public void Reset() { d = new Data(); }
        public double ResetIn => (Util.Day(Util.Now) + 1) * 86400.0 - Util.Now;
        public int DailyCount => (int)c.Content.Bal("daily.count", 3);

        public void Tick(double now)
        {
            double day = Util.Day(now);
            if (day == d.day) return;
            d.streak = day == d.day + 1 ? d.streak + 1 : 1;
            d.day = day; d.claimed = false;
            c.Quests.RollDailies(DailyCount, (int)day);
            c.Counters.Add("days.played");
            c.Changed();
        }
        public List<Res> TodayReward => c.Content.CalendarRewards(d.streak);
        public bool Claim()
        {
            if (d.claimed) return false;
            foreach (var r in TodayReward) c.Give(r);
            d.claimed = true; d.claimedTotal++; c.Counters.Add("login.claims"); c.Changed(); return true;
        }
    }

    // =====================================================================================
    /// <summary>Guild-level meta: name, emblem, tutorial flags and play-time. Persisted with the save.</summary>
    public class GuildMeta : ISystem, ISaveable, ITickable
    {
        GameContext c;
        [Serializable] class Data { public string name = "Emberhall"; public int emblem; public double created; public double playSeconds; public int tutorial; public List<string> seen = new List<string>(); public double lastSeen; }
        Data d = new Data();
        public string GuildName { get => d.name; set => d.name = string.IsNullOrWhiteSpace(value) ? "Emberhall" : value.Trim(); }
        public int Emblem { get => d.emblem; set => d.emblem = value; }
        public double Created => d.created;
        public double PlaySeconds => d.playSeconds;
        public string SaveKey => "meta";
        public void Init(GameContext ctx) { c = ctx; }
        public string Save() { d.lastSeen = Util.Now; return JsonUtility.ToJson(d); }
        public void Load(string json) { d = JsonUtility.FromJson<Data>(json) ?? new Data(); }
        public void Reset() { d = new Data { created = Util.Now }; }
        double lastT;
        public void Tick(double now) { if (lastT > 0) d.playSeconds += Math.Min(5, now - lastT); lastT = now; }
        public bool Seen(string key) => d.seen.Contains(key);
        public void MarkSeen(string key) { if (!d.seen.Contains(key)) d.seen.Add(key); }
    }

    // =====================================================================================
    public class OfflineReport
    {
        public double awaySeconds, countedSeconds, vaultGold;
        public int finishedTasks, expeditionsReady, pendingEvents, questsReady;
        public bool Meaningful => awaySeconds >= 60 && (vaultGold >= 1 || finishedTasks > 0 || expeditionsReady > 0 || pendingEvents > 0);

        public static OfflineReport Build(GameContext c, double savedUtc)
        {
            var r = new OfflineReport();
            double now = Util.Now;
            r.awaySeconds = Math.Max(0, now - savedUtc);
            double cap = c.Mods.Apply("offline.cap_hours", c.Content.Bal("offline.cap", 8)) * 3600;
            r.countedSeconds = Math.Min(r.awaySeconds, cap);
            foreach (var t in c.Timers.Tasks) if (t.end <= now) { if (t.kind == "expedition") r.expeditionsReady++; else r.finishedTasks++; }
            r.vaultGold = Math.Floor(c.Vault.Pending);
            r.pendingEvents = c.Events.Pending.Count;
            return r;
        }
    }
}
