using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ManagerKit
{
    [Serializable]
    public class UnitInstance
    {
        public string uid, defId, classId, name, sprite, rarity = "common";
        public int level = 1, promo;
        public double xp;
        public List<string> traits = new List<string>();
        public List<string> equip = new List<string> { "", "", "" };   // item uids, index = slot
        public double hp = -1;                                          // -1 = full
        public float scale = 1f;
        public bool monster, fav;
        public double mHp = 1, mAtk = 1, mDef = 1, mSpd = 1;
        public double hired;
        public double price;                                            // only meaningful for recruit candidates
    }

    public class Roster : ISystem, ISaveable, IPostInit, ITickable
    {
        public static readonly string[] Slots = { "weapon", "armor", "accessory" };
        GameContext c;
        readonly List<UnitInstance> units = new List<UnitInstance>();
        readonly Dictionary<string, UnitInstance> byUid = new Dictionary<string, UnitInstance>();
        readonly Dictionary<string, Dictionary<string, double>> cache = new Dictionary<string, Dictionary<string, double>>();
        int modifierRevision = -1;
        double lastRegen;
        public int Version { get; private set; }
        [Serializable] class Data { public List<UnitInstance> u = new List<UnitInstance>(); public double regen; }

        public IReadOnlyList<UnitInstance> All => units;
        public int Count => units.Count;
        public string SaveKey => "roster";
        public void Init(GameContext ctx) { c = ctx; }
        public void PostInit() { c.Stats.Register("hero", k => k == "count" ? units.Count : k == "maxlevel" ? (units.Count == 0 ? 0 : units.Max(u => u.level)) : k == "maxpromo" ? (units.Count == 0 ? 0 : units.Max(u => u.promo)) : k == "monsters" ? units.Count(u => u.monster) : k == "rare" ? units.Count(u => c.Content.Rarities.FindIndex(r => r.id == u.rarity) >= 2) : 0); }

        public string Save() => JsonUtility.ToJson(new Data { u = units, regen = lastRegen });
        public void Load(string json)
        {
            units.Clear(); byUid.Clear(); cache.Clear();
            var d = JsonUtility.FromJson<Data>(json);
            if (d != null) { foreach (var u in d.u) { while (u.equip.Count < Slots.Length) u.equip.Add(""); units.Add(u); byUid[u.uid] = u; } lastRegen = d.regen; }
            Dirty();
        }
        public void Reset() { units.Clear(); byUid.Clear(); cache.Clear(); lastRegen = Util.Now; Dirty(); }
        public void Dirty() { Version++; cache.Clear(); c.Mods.Dirty(); }

        public UnitInstance Get(string uid) => uid != null && byUid.TryGetValue(uid, out var u) ? u : null;
        public int Capacity => (int)(c.Content.Bal("roster.cap", 6) + c.Mods.Add("roster.cap"));
        public bool Full => units.Count >= Capacity;

        public void Add(UnitInstance u)
        {
            if (string.IsNullOrEmpty(u.uid)) u.uid = Util.Uid();
            if (u.hired <= 0) u.hired = Util.Now;
            units.Add(u); byUid[u.uid] = u; Dirty();
            c.Counters.Add("hero.hired");
        }
        public void Remove(UnitInstance u)
        {
            if (u == null || Get(u.uid) != u) return;
            for (int i = 0; i < u.equip.Count; i++) if (!string.IsNullOrEmpty(u.equip[i])) c.Inventory.Unequip(u, i);
            units.Remove(u); byUid.Remove(u.uid); Dirty();
        }

        public string DismissBlocker(UnitInstance u)
        {
            if (u == null || Get(u.uid) != u) return "This hero is no longer in your guild. Choose another hero.";
            if (IsBusy(u)) return "Finish or claim this hero's task before dismissing them.";
            if (Count <= 1) return "Keep at least one hero in your guild.";
            return null;
        }
        public bool CanDismiss(UnitInstance u) => DismissBlocker(u) == null;
        public bool Dismiss(UnitInstance u)
        {
            if (!CanDismiss(u)) return false;
            Remove(u); c.Changed(); return true;
        }

        // ------------- progression -------------
        public double XpToNext(int level) => Math.Round(c.Content.Bal("xp.base", 30) * Math.Pow(level, c.Content.Bal("xp.exp", 1.55)));
        public int LevelCap(UnitInstance u) => (int)(c.Mods.Apply("hero.levelcap", c.Content.Bal("level.cap", 8)) + u.promo * c.Content.Bal("promo.levels", 10));
        public int MaxPromo => (int)c.Content.Bal("promo.max", 3);
        public bool CanPromote(UnitInstance u) => u != null && Get(u.uid) == u && !IsBusy(u) && u.level >= LevelCap(u) && u.promo < MaxPromo;
        public List<Res> PromoCost(UnitInstance u) => new List<Res> { new Res("gold", Math.Round(c.Content.Bal("promo.gold", 400) * Math.Pow(u.promo + 1, 2.2))), new Res("essence", (u.promo + 1) * c.Content.Bal("promo.essence", 3)) };
        public bool Promote(UnitInstance u)
        {
            if (!CanPromote(u) || !c.Pay(PromoCost(u))) return false;
            double health = HpFrac(u);
            u.promo++; Dirty(); c.Counters.Add("hero.promoted");
            SetHp(u, MaxHp(u) * health);
            c.Note(NoteKind.Level, $"{u.name} promoted to {new string('*', u.promo)}!");
            return true;
        }

        /// <summary>Adds xp, handles level ups. Returns levels gained.</summary>
        public int AddXp(UnitInstance u, double xp)
        {
            if (xp <= 0) return 0;
            int gained = 0, cap = LevelCap(u);
            double health = HpFrac(u);
            u.xp += xp;
            while (u.level < cap && u.xp >= XpToNext(u.level)) { u.xp -= XpToNext(u.level); u.level++; gained++; }
            if (u.level >= cap) u.xp = 0;
            if (gained > 0)
            {
                Dirty(); c.Counters.Add("hero.levelups", gained); c.Counters.Max("hero.best_level", u.level);
                SetHp(u, MaxHp(u) * Math.Min(1, health + gained * c.Content.Bal("level.recovery_hp", 0.08)));
                c.Note(NoteKind.Level, $"{u.name} reached Lv {u.level}!");
            }
            return gained;
        }

        // ------------- stats -------------
        double TraitSum(UnitInstance u, string key, string op)
        {
            double s = 0;
            foreach (var t in u.traits) if (c.Content.TraitMap.TryGetValue(t, out var td)) foreach (var m in td.mods) if (m.key == key && m.op == op) s += m.v;
            return s;
        }
        public double TraitPct(UnitInstance u, string key) => TraitSum(u, key, "pct");
        public double PartyPct(IEnumerable<UnitInstance> party, string key) { double s = 0; foreach (var u in party) s += TraitSum(u, key, "pct"); return s; }

        public Dictionary<string, double> Stats(UnitInstance u)
        {
            if (modifierRevision != c.Mods.Revision) { cache.Clear(); modifierRevision = c.Mods.Revision; }
            if (cache.TryGetValue(u.uid, out var d)) return d;
            d = new Dictionary<string, double>();
            var cls = c.Content.Class(u.classId); var rar = c.Content.Rarity(u.rarity);
            var bal = c.Content;
            double promoMul = 1 + u.promo * bal.Bal("promo.mult", 0.25);
            foreach (var sd in bal.Stats)
            {
                double b = 0, g = 0;
                if (cls != null) { foreach (var s in cls.baseStats) if (s.id == sd.id) b = s.v; foreach (var s in cls.growth) if (s.id == sd.id) g = s.v; }
                double v = b + g * (u.level - 1);
                double gear = c.Inventory.GearStat(u, sd.id);
                if (!sd.pct)
                {
                    double um = sd.id == "hp" ? u.mHp : sd.id == "atk" ? u.mAtk : sd.id == "def" ? u.mDef : sd.id == "spd" ? u.mSpd : 1;
                    v = v * (rar?.mult ?? 1) * promoMul * um + gear;
                    v = v * (1 + TraitPct(u, "stat." + sd.id) + c.Mods.Pct("hero." + sd.id)) + c.Mods.Add("hero." + sd.id);
                }
                else v += gear + TraitSum(u, "stat." + sd.id, "add") + c.Mods.Add("hero." + sd.id);
                d[sd.id] = Math.Round(v, 1);
            }
            cache[u.uid] = d; return d;
        }
        public double Stat(UnitInstance u, string id) => Stats(u).TryGetValue(id, out var v) ? v : 0;
        public double MaxHp(UnitInstance u) => Math.Max(1, Stat(u, "hp"));
        public double Hp(UnitInstance u) => u.hp < 0 ? MaxHp(u) : Math.Min(u.hp, MaxHp(u));
        public double HpFrac(UnitInstance u) => Hp(u) / MaxHp(u);
        public void SetHp(UnitInstance u, double v) { v = Math.Max(0, v); u.hp = v >= MaxHp(u) - 0.01 ? -1 : v; }
        public double Power(UnitInstance u) => Math.Round(Stat(u, "hp") / 8 + Stat(u, "atk") * 1.6 + Stat(u, "def") * 1.4 + Stat(u, "spd") * 1.0);
        public double PartyPower(IEnumerable<UnitInstance> p) { double s = 0; foreach (var u in p) s += Power(u); return s; }

        public bool IsBusy(UnitInstance u) => c.Timers.UnitBusy(u.uid);
        public string BusyWhat(UnitInstance u) => c.Timers.UnitTask(u.uid)?.kind;
        public bool Ready(UnitInstance u) => u != null && !IsBusy(u) && HpFrac(u) >= c.Content.Bal("ready.hp", 0.25);

        public string ReadinessBlocker(UnitInstance u)
        {
            if (u == null || Get(u.uid) != u) return "This hero is no longer in your guild. Choose another hero.";
            if (IsBusy(u))
            {
                if (BusyWhat(u) == "expedition") return "This hero is on an expedition. Claim the return in Board > Contracts, or choose another hero.";
                return "This hero is training or healing. Wait for the task to finish, or choose another hero.";
            }
            if (HpFrac(u) < c.Content.Bal("ready.hp", 0.25)) return "This hero needs recovery. Use a potion in Heroes, heal at the Infirmary, or choose another hero.";
            return null;
        }

        // passive regeneration, resolved with absolute time so it also covers offline
        public void Tick(double now)
        {
            double dt = now - lastRegen; if (dt < 20) return;
            lastRegen = now;
            double pctPerMin = c.Content.Bal("regen.pct_min", 0.6) / 100.0;
            foreach (var u in units)
            {
                if (u.hp < 0 || c.Timers.UnitBusy(u.uid)) continue;
                var max = MaxHp(u);
                SetHp(u, u.hp + max * pctPerMin * (dt / 60.0));
            }
        }

        // ------------- synergies -------------
        public List<SynergyDef> ActiveSynergies(IEnumerable<UnitInstance> party)
        {
            var res = new List<SynergyDef>(); var list = party.ToList();
            foreach (var s in c.Content.Synergies)
            {
                bool ok;
                if (!string.IsNullOrEmpty(s.classA))
                {
                    string second = string.IsNullOrEmpty(s.classB) ? s.classA : s.classB;
                    ok = list.Any(u => u.classId == s.classA) && list.Any(u => u.classId == second) && (s.classA != second || list.Count(u => u.classId == s.classA) >= 2);
                }
                else if (s.tag == "monster") ok = list.Count(u => u.monster) >= s.count;
                else ok = list.Count(u => c.Content.Class(u.classId)?.tags.Contains(s.tag) == true) >= s.count;
                if (ok) res.Add(s);
            }
            return res;
        }
        public string ClassName(UnitInstance u) => c.Content.Class(u.classId)?.name ?? u.classId;
    }

    // =====================================================================================
    public class Recruiter : ISystem, ISaveable, ITickable, INewGame
    {
        GameContext c;
        [Serializable] class Data { public List<UnitInstance> pool = new List<UnitInstance>(); public double refreshAt; public double lastFree; public int rolls; }
        Data d = new Data();
        public IReadOnlyList<UnitInstance> Pool => d.pool;
        public double RefreshAt => d.refreshAt;
        public string SaveKey => "recruiter";
        public void Init(GameContext ctx) { c = ctx; }
        public string Save() => JsonUtility.ToJson(d);
        public void Load(string json) { d = JsonUtility.FromJson<Data>(json) ?? new Data(); }
        public void Reset() { d = new Data(); }
        public void OnNewGame() { Refresh(true); }
        public int Slots => (int)(c.Content.Bal("tavern.slots", 3) + c.Mods.Add("tavern.slots"));
        public double Interval => c.Content.Bal("tavern.refresh", 1200);

        public void Tick(double now)
        {
            if (c.Upgrades.Level("tavern") <= 0) return;
            if (now >= d.refreshAt) { Refresh(true); c.Changed(); }
            else if (d.pool.Count < Slots) { Refresh(false); c.Changed(); }
        }

        public bool FreeRefreshReady => Util.Now - d.lastFree >= c.Content.Bal("tavern.free_refresh", 3600);
        public int RefreshGemCost => (int)c.Content.Bal("tavern.refresh_gems", 5);
        public bool ManualRefresh(bool useGems)
        {
            if (FreeRefreshReady && !useGems) { d.lastFree = Util.Now; Refresh(true); c.Changed(); return true; }
            if (useGems && c.Pay(new[] { new Res("gems", RefreshGemCost) })) { Refresh(true); return true; }
            return false;
        }

        public void Refresh(bool force)
        {
            int slots = Slots;
            if (force) d.pool.Clear();
            while (d.pool.Count > slots) d.pool.RemoveAt(d.pool.Count - 1);
            while (d.pool.Count < slots)
            {
                int roll = d.rolls++;
                string starterRole = roll < 3 ? new[] { "knight", "cleric", "warrior" }[roll] : null;
                int minimum = roll > 0 && roll % 12 == 11 ? Math.Min(2, c.Content.Rarities.Count - 1) : 0;
                d.pool.Add(Generate(c.Rng, roll == 0 && c.Counters.Get("hero.hired") <= 1, minimum, starterRole));
            }
            EnsureAffordableRecruit();
            if (force || Util.Now >= d.refreshAt) d.refreshAt = Util.Now + Interval;
        }

        /// <summary>The guided hire quest always has someone the guild can actually afford.</summary>
        public void EnsureAffordableRecruit()
        {
            var q = c.Quests.CurrentMain;
            if (q == null || q.goals.Count == 0) return;
            var stat = q.goals[0].stat;
            if (stat != "hero.count" && stat != "hero.hired") return;
            if (c.Roster.Count + 0.01 >= q.goals[0].target) return;
            UnitInstance cheap = null;
            foreach (var u in d.pool) if (cheap == null || u.price < cheap.price) cheap = u;
            if (cheap == null) return;
            double gold = c.Wallet.Get("gold");
            if (cheap.price > gold) cheap.price = gold;
        }

        public UnitInstance Generate(Rng rng, bool friendly = false, int minRarity = 0, string preferredClass = null)
        {
            var db = c.Content;
            var cls = db.Class(preferredClass) ?? rng.Pick(db.Classes.Where(k => !k.tags.Contains("monster")).ToList());
            var rw = new List<double>();
            for (int i = 0; i < db.Rarities.Count; i++)
            {
                double w = i < minRarity ? 0 : db.Rarities[i].weight;
                if (i >= 2) w *= 1 + Math.Max(-0.9, c.Mods.Pct("tavern.rare"));
                rw.Add(w);
            }
            int ri = Math.Max(0, rng.Weighted(rw));
            var rar = db.Rarities[ri];
            int rank = c.Ranks.Index;
            var u = new UnitInstance
            {
                uid = Util.Uid(), classId = cls.id, rarity = rar.id,
                sprite = rng.Pick(db.Skins),
                name = rng.Pick(db.Names.first) + (rng.Chance(0.55) && db.Names.last.Count > 0 ? " " + rng.Pick(db.Names.last) : ""),
                level = Mathf.Clamp(1 + rank + rng.Range(0, 1 + ri), 1, 40),
                mHp = Math.Round(rng.Range(0.9f, 1.1f), 2), mAtk = Math.Round(rng.Range(0.9f, 1.1f), 2),
                mDef = Math.Round(rng.Range(0.9f, 1.1f), 2), mSpd = Math.Round(rng.Range(0.92f, 1.08f), 2),
            };
            int tc = friendly ? 0 : ri == 0 ? rng.Range(0, 1) : ri == 1 ? 1 : ri == 2 ? rng.Range(1, 2) : 2;
            var tw = new List<double>(); foreach (var t in db.Traits) tw.Add(t.weight);
            for (int k = 0; k < tc && db.Traits.Count > 0; k++)
            {
                int ti = rng.Weighted(tw); if (ti < 0) break;
                var t = db.Traits[ti]; if (u.traits.Contains(t.id)) continue;
                u.traits.Add(t.id); tw[ti] = 0;
            }
            double price = db.Bal("recruit.base", 60) * Math.Pow(rar.mult, 2.2) * (1 + 0.12 * u.level) * (1 - Math.Min(0.5, c.Mods.Pct("tavern.discount")));
            u.price = friendly ? Math.Round(price * 0.5) : Math.Round(price);
            return u;
        }

        public UnitInstance FromDef(UnitDef def)
        {
            var db = c.Content; var cls = db.Class(def.classId);
            var u = new UnitInstance { uid = Util.Uid(), defId = def.id, classId = def.classId, name = def.name, sprite = def.sprite, rarity = def.rarity, monster = true, scale = def.scale, level = Mathf.Clamp(2 + c.Ranks.Index * 2, 1, 40), traits = new List<string>(def.traits) };
            foreach (var m in def.mult) { if (m.id == "hp") u.mHp = m.v; else if (m.id == "atk") u.mAtk = m.v; else if (m.id == "def") u.mDef = m.v; else if (m.id == "spd") u.mSpd = m.v; }
            return u;
        }

        public bool CanHire(UnitInstance u) => u != null && d.pool.Contains(u) && !c.Roster.Full && c.Wallet.Has("gold", u.price);
        public bool Hire(UnitInstance u)
        {
            if (u == null || !d.pool.Contains(u)) return false;
            if (c.Roster.Full) { c.Note(NoteKind.Warn, "Roster is full! Upgrade the Barracks."); return false; }
            if (!c.Wallet.Spend("gold", u.price)) { c.Note(NoteKind.Warn, "Not enough gold"); return false; }
            d.pool.Remove(u); u.price = 0; c.Roster.Add(u);
            c.Note(NoteKind.Good, $"{u.name} joined the guild!");
            Refresh(false); c.Changed(); return true;
        }
        public bool HireDef(UnitDef def)
        {
            if (def == null || IsHiredDef(def.id)) return false;
            if (c.Roster.Full) { c.Note(NoteKind.Warn, "Roster is full! Upgrade the Barracks."); return false; }
            if (!c.MeetsAll(def.req)) return false;
            if (!c.Pay(def.cost)) { c.Note(NoteKind.Warn, "Not enough resources"); return false; }
            var u = FromDef(def); c.Roster.Add(u); c.Counters.Add("monster.tamed");
            c.Note(NoteKind.Good, $"{u.name} has joined your guild!"); c.Changed(); return true;
        }
        public bool IsHiredDef(string defId) => c.Roster.All.Any(u => u.defId == defId);
    }
}
