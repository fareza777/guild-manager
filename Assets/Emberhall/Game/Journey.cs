using System;
using System.Linq;
using ManagerKit;
using UnityEngine;

namespace Emberhall
{
    /// <summary>Campaign pacing: renewable stamina and regional boss mastery. Existing guilds receive a full reserve.</summary>
    public sealed class Journey : ISystem, ISaveable, ITickable, IBattleRules
    {
        public const int Capacity = 30, BossStars = 8;
        public const double RecoverySeconds = 300;
        [Serializable] sealed class State
        {
            public int energy = Capacity, paid;
            public double at;
            public string mission;
            public double rationDay = -1;
        }
        GameContext c;
        State state = new State();
        public string SaveKey => "journey";
        public void Init(GameContext context) { c = context; state.at = Util.Now; }
        public int Energy { get { Recover(Util.Now); return state.energy; } }
        public double NextRecovery => Energy >= Capacity ? 0 : Math.Max(0, RecoverySeconds - (Util.Now - state.at));
        public bool InBattle => !string.IsNullOrEmpty(state.mission);
        public bool CampUsed => state.rationDay == Util.Day(Util.Now);
        public bool CanRest => !InBattle && !CampUsed && Energy <= Capacity - 10;
        public bool RestAtCamp()
        {
            if (!CanRest) return false;
            state.rationDay = Util.Day(Util.Now); state.energy += 10; c.Changed(); return true;
        }
        public static bool Campaign(MissionDef m) => m != null && m.kind == "battle" && m.region != "tower" && m.region.StartsWith("r");
        public int Cost(MissionDef m)
        {
            if (m == null || m.kind != "battle") return 0; // Guild exams and contracts never consume stamina.
            if (!c.Missions.Cleared(m.id) && (m.id == "m_r1_1" || m.id == "m_r1_2" || m.id == "m_r1_3")) return 0;
            return m.boss || m.region == "tower" ? 3 : 2;
        }
        public int RegionStars(string region) => c.Content.Missions.Where(m => m.region == region && !m.boss).Sum(m => c.Missions.Stars(m.id));
        public string MasteryBlocker(MissionDef m)
        {
            if (!Campaign(m) || !m.boss || c.Missions.Cleared(m.id) || RegionStars(m.region) >= BossStars) return null;
            return $"Earn {BossStars} stars across this region's four trails before challenging its boss. ({RegionStars(m.region)}/{BossStars})";
        }
        public bool CanEnter(MissionDef m, out string why)
        {
            why = InBattle ? "A battle is already in progress." : MasteryBlocker(m);
            if (why != null) return false;
            if (Energy < Cost(m)) { why = "Not enough stamina. Train, forge gear or send a contract while your stamina recovers."; return false; }
            return true;
        }
        public bool Enter(MissionDef m, out string why)
        {
            if (!CanEnter(m, out why)) return false;
            int cost = Cost(m);
            state.energy -= cost; state.paid = cost; state.mission = m.id; c.Changed(); return true;
        }
        public void Result(MissionDef m, bool won)
        {
            if (state.mission != m.id) return;
            Recover(Util.Now);
            if (!won) state.energy = Math.Min(Capacity, state.energy + Math.Max(0, state.paid - 1));
            state.mission = null; state.paid = 0;
        }
        void Recover(double now)
        {
            if (state.at <= 0 || now < state.at || state.energy >= Capacity) { state.at = now; state.energy = Math.Min(state.energy, Capacity); return; }
            int recovered = (int)Math.Min(Capacity, Math.Floor((now - state.at) / RecoverySeconds));
            if (recovered <= 0) return;
            state.energy = Math.Min(Capacity, state.energy + recovered);
            state.at = state.energy == Capacity ? now : state.at + recovered * RecoverySeconds;
        }
        public void Tick(double now) { int before = state.energy; Recover(now); if (before != state.energy) c.Changed(); }
        public string Save() { Recover(Util.Now); return JsonUtility.ToJson(state); }
        public void Load(string json)
        {
            state = JsonUtility.FromJson<State>(json) ?? new State();
            state.energy = Math.Clamp(state.energy, 0, Capacity); Recover(Util.Now);
            // A cold restart returns to Hall, never to an unresolved animation. An interrupted run costs at most one.
            if (InBattle) { state.energy = Math.Min(Capacity, state.energy + Math.Max(0, state.paid - 1)); state.mission = null; state.paid = 0; }
        }
        public void Reset() { state = new State { at = Util.Now }; }
    }
}
