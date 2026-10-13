using System;
using System.Collections.Generic;
using ManagerKit;
using UnityEngine;

namespace Emberhall
{
    /// <summary>Only an SDK earned-reward callback may redeem a ticket. Tickets belong to one loaded guild.</summary>
    public sealed class AdRewards : ISystem, ISaveable
    {
        public const int SupplyGold = 150, SupplyGems = 2, DailySupplyLimit = 3;
        public const double SupplyCooldown = 600;
        [Serializable] sealed class State
        {
            public List<string> claimed = new List<string>();
            public double supplyDay = -1;
            public int supplyCount;
            public double lastSupply;
        }
        public sealed class Ticket
        {
            internal string run, key;
            internal bool supply;
            internal double gold;
        }
        GameContext c;
        State state = new State();
        string run = Guid.NewGuid().ToString("N");
        readonly HashSet<Ticket> pending = new HashSet<Ticket>();
        public string SaveKey => "ad_rewards";
        public void Init(GameContext context) { c = context; }
        public int SuppliesToday => state.supplyDay == Util.Day(Util.Now) ? state.supplyCount : 0;
        public double SupplyWait => Math.Max(0, state.lastSupply + SupplyCooldown - Util.Now);
        public bool CanSupply => SuppliesToday < DailySupplyLimit && SupplyWait <= 0;
        public bool Claimed(string key) => state.claimed.Contains(key);
        public Ticket SupplyTicket() => CanSupply ? Issue("supply_" + Guid.NewGuid().ToString("N"), SupplyGold, true) : null;
        public Ticket VictoryTicket(string battleKey, double earnedGold)
            => string.IsNullOrEmpty(battleKey) || earnedGold < 2 || Claimed(battleKey) ? null : Issue(battleKey, Math.Floor(earnedGold * .5), false);
        Ticket Issue(string key, double gold, bool supply)
        {
            var ticket = new Ticket { run = run, key = key, gold = gold, supply = supply };
            pending.Add(ticket); return ticket;
        }
        public void Cancel(Ticket ticket) { if (ticket != null) pending.Remove(ticket); }
        public bool GrantEarned(Ticket ticket)
        {
            if (ticket == null || !pending.Remove(ticket) || ticket.run != run || Claimed(ticket.key)) return false;
            if (ticket.supply && !CanSupply) return false;
            state.claimed.Add(ticket.key);
            if (state.claimed.Count > 256) state.claimed.RemoveAt(0);
            if (ticket.supply)
            {
                if (state.supplyDay != Util.Day(Util.Now)) { state.supplyDay = Util.Day(Util.Now); state.supplyCount = 0; }
                state.supplyCount++; state.lastSupply = Util.Now;
            }
            c.Give(new Res("gold", ticket.gold));
            if (ticket.supply) c.Give(new Res("gems", SupplyGems));
            c.Changed(); return true;
        }
        public string Save() => JsonUtility.ToJson(state);
        public void Load(string json)
        {
            state = JsonUtility.FromJson<State>(json) ?? new State();
            if (state.claimed == null) state.claimed = new List<string>();
            InvalidateTickets();
        }
        public void Reset() { state = new State(); InvalidateTickets(); }
        void InvalidateTickets() { run = Guid.NewGuid().ToString("N"); pending.Clear(); }
    }

    /// <summary>Session-only frequency limits, using monotonic time rather than the device calendar.</summary>
    public sealed class AdBreakPolicy
    {
        readonly double started;
        readonly HashSet<string> battles = new HashSet<string>();
        double lastFullScreen = double.NegativeInfinity;
        int sinceBreak, shown;
        public AdBreakPolicy(double now) { started = now; }
        public void BattleFinished(string key, bool campaign)
        {
            if (campaign && battles.Add(key)) sinceBreak++;
        }
        public bool CanShow(double now, bool tutorialDone, bool ready, bool safe)
            => tutorialDone && ready && safe && sinceBreak >= 3 && shown < 5 && now - started >= 120 && now - lastFullScreen >= 180;
        public void InterstitialShown(double now) { shown++; sinceBreak = 0; lastFullScreen = now; }
        public void RewardedShown(double now) { lastFullScreen = now; sinceBreak = 0; }
    }
}
