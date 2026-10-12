using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ManagerKit
{
    [Serializable]
    public class ItemInstance
    {
        public string uid, defId, rarity = "common", owner = "";
        public int enh;
        public List<StatVal> affix = new List<StatVal>();
        public bool isNew, locked;
    }

    public struct Drop { public Res res; public ItemInstance gear; }

    public class Inventory : ISystem, ISaveable
    {
        GameContext c;
        readonly List<ItemInstance> gear = new List<ItemInstance>();
        readonly Dictionary<string, ItemInstance> byUid = new Dictionary<string, ItemInstance>();
        readonly Dictionary<string, double> stacks = new Dictionary<string, double>();
        [Serializable] class Data { public List<ItemInstance> g = new List<ItemInstance>(); public List<KVD> s = new List<KVD>(); }
        public IReadOnlyList<ItemInstance> Gear => gear;
        public IReadOnlyDictionary<string, double> Stacks => stacks;
        public int Version { get; private set; }
        public string SaveKey => "inventory";
        public void Init(GameContext ctx) { c = ctx; }

        public string Save() => JsonUtility.ToJson(new Data { g = gear, s = stacks.ToList() });
        public void Load(string json)
        {
            gear.Clear(); byUid.Clear(); stacks.Clear();
            var d = JsonUtility.FromJson<Data>(json);
            if (d != null) { foreach (var g in d.g) { gear.Add(g); byUid[g.uid] = g; } foreach (var s in d.s) stacks[s.k] = s.v; }
            Version++;
        }
        public void Reset() { gear.Clear(); byUid.Clear(); stacks.Clear(); Version++; }

        public int Capacity => (int)(c.Content.Bal("inv.cap", 40) + c.Mods.Add("inv.cap"));
        public int Used => gear.Count;
        public bool Full => gear.Count >= Capacity;
        public ItemInstance Get(string uid) => !string.IsNullOrEmpty(uid) && byUid.TryGetValue(uid, out var g) ? g : null;
        public int Count(string defId)
        {
            if (stacks.TryGetValue(defId, out var v)) return (int)v;
            int n = 0; foreach (var g in gear) if (g.defId == defId) n++; return n;
        }

        // ---------- add / remove ----------
        public ItemInstance AddItem(string defId, int n, Rng rng, string rarity = null)
        {
            if (n <= 0) return null;
            var def = c.Content.Item(defId); if (def == null) return null;
            c.Counters.Add("item.gained." + defId, n);
            if (!def.IsGear) { stacks[defId] = (stacks.TryGetValue(defId, out var v) ? v : 0) + n; Version++; return null; }
            ItemInstance last = null;
            for (int i = 0; i < n; i++) { last = Generate(def, rarity, 0, rng ?? c.Rng); last.isNew = true; AddGear(last); }
            return last;
        }
        public void AddGear(ItemInstance g)
        {
            if (string.IsNullOrEmpty(g.uid)) g.uid = Util.Uid();
            gear.Add(g); byUid[g.uid] = g; Version++;
            c.Counters.Add("gear.gained");
            int ri = c.Content.Rarities.FindIndex(r => r.id == g.rarity);
            if (ri >= 0) c.Counters.Max("gear.best_rarity", ri);
        }
        public bool Remove(string defId, int n)
        {
            if (n <= 0) return false;
            if (Count(defId) < n) return false;
            if (stacks.ContainsKey(defId)) { stacks[defId] -= n; if (stacks[defId] <= 0.0001) stacks.Remove(defId); Version++; return true; }
            var removable = gear.Where(x => x.defId == defId && !x.locked && string.IsNullOrEmpty(x.owner)).Take(n).ToList();
            if (removable.Count < n) return false;
            foreach (var g in removable) RemoveGear(g);
            return true;
        }
        public void RemoveGear(ItemInstance g)
        {
            if (!string.IsNullOrEmpty(g.owner)) { var u = c.Roster.Get(g.owner); if (u != null) Unequip(u, SlotIndex(c.Content.Item(g.defId))); }
            gear.Remove(g); byUid.Remove(g.uid); Version++;
        }

        // ---------- generation ----------
        public ItemInstance Generate(ItemDef def, string rarityId, double luck, Rng rng)
        {
            var db = c.Content;
            string rid = rarityId;
            if (rid == null)
            {
                var w = new List<double>();
                for (int i = 0; i < db.Rarities.Count; i++) w.Add(db.Rarities[i].weight * (i == 0 ? 1 : 1 + luck * i));
                rid = db.Rarities[Math.Max(0, rng.Weighted(w))].id;
            }
            var rar = db.Rarity(rid);
            var g = new ItemInstance { uid = Util.Uid(), defId = def.id, rarity = rar.id };
            for (int i = 0; i < rar.affixes && db.Stats.Count > 0; i++)
            {
                var st = rng.Pick(db.Stats);
                double v = db.Bal("affix." + st.id, 2) * def.tier * rng.Range(0.7f, 1.3f);
                var ex = g.affix.Find(a => a.id == st.id);
                if (ex != null) ex.v += Math.Round(v, 1); else g.affix.Add(new StatVal(st.id, Math.Round(v, 1)));
            }
            return g;
        }

        // ---------- stats ----------
        public double ItemStat(ItemInstance g, string stat)
        {
            var def = c.Content.Item(g.defId); if (def == null) return 0;
            double v = 0; foreach (var s in def.stats) if (s.id == stat) v += s.v;
            var rar = c.Content.Rarity(g.rarity);
            bool pct = c.Content.StatMap.TryGetValue(stat, out var sd) && sd.pct;
            if (!pct) v = v * (rar?.mult ?? 1) * (1 + g.enh * c.Content.Bal("enh.mult", 0.12));
            foreach (var a in g.affix) if (a.id == stat) v += a.v;
            return v;
        }
        public double GearStat(UnitInstance u, string stat)
        {
            double s = 0;
            for (int i = 0; i < u.equip.Count; i++) { var g = Get(u.equip[i]); if (g != null) s += ItemStat(g, stat); }
            return s;
        }
        public double Score(ItemInstance g)
        {
            double s = 0;
            foreach (var st in c.Content.Stats)
            {
                double w = st.id == "hp" ? 0.125 : st.id == "atk" ? 1.6 : st.id == "def" ? 1.4 : st.id == "spd" ? 1 : 3;
                s += ItemStat(g, st.id) * w;
            }
            return s;
        }

        public double ScoreFor(ItemInstance g, UnitInstance unit)
        {
            var cls = c.Content.Class(unit.classId);
            bool tank = cls?.tags.Contains("tank") == true;
            bool healer = cls?.tags.Contains("holy") == true;
            double hp = ItemStat(g, "hp"), atk = ItemStat(g, "atk"), def = ItemStat(g, "def");
            double spd = ItemStat(g, "spd"), crit = ItemStat(g, "crit");
            return hp * (tank ? 0.2 : 0.125) + atk * (healer ? 2.1 : tank ? 1.2 : 1.8)
                + def * (tank ? 2.1 : 1.25) + spd * (healer ? 1.6 : 1.1) + crit * (tank || healer ? 1.2 : 3);
        }
        public static int SlotIndex(ItemDef d) => d == null ? -1 : Array.IndexOf(Roster.Slots, d.slot);

        // ---------- equip ----------
        public bool CanEquip(UnitInstance u, ItemInstance g)
        {
            if (u == null || g == null) return false;
            var def = c.Content.Item(g.defId); if (def == null || !def.IsGear) return false;
            if (def.classes.Count > 0 && !def.classes.Contains(u.classId)) return false;
            if (def.type == "weapon") { var cls = c.Content.Class(u.classId); if (cls != null && !string.IsNullOrEmpty(def.family) && def.family != cls.weapon) return false; }
            return true;
        }
        public bool Equip(UnitInstance u, ItemInstance g)
        {
            if (!CanEquip(u, g)) return false;
            int si = SlotIndex(c.Content.Item(g.defId)); if (si < 0) return false;
            if (!string.IsNullOrEmpty(g.owner)) { var o = c.Roster.Get(g.owner); if (o != null) { o.equip[SlotIndex(c.Content.Item(g.defId))] = ""; } }
            Unequip(u, si);
            u.equip[si] = g.uid; g.owner = u.uid; g.isNew = false;
            c.Roster.Dirty(); Version++; c.Counters.Add("gear.equipped"); return true;
        }
        public void Unequip(UnitInstance u, int slot)
        {
            if (slot < 0 || slot >= u.equip.Count) return;
            var g = Get(u.equip[slot]); if (g != null) g.owner = "";
            u.equip[slot] = ""; c.Roster.Dirty(); Version++;
        }
        public void AutoEquip(UnitInstance u)
        {
            for (int si = 0; si < Roster.Slots.Length; si++)
            {
                ItemInstance best = Get(u.equip[si]); double bs = best != null ? ScoreFor(best, u) : -1;
                foreach (var g in gear)
                {
                    var def = c.Content.Item(g.defId); if (def == null || SlotIndex(def) != si || !CanEquip(u, g)) continue;
                    if (!string.IsNullOrEmpty(g.owner) && g.owner != u.uid) continue;
                    double s = ScoreFor(g, u); if (s > bs + 0.01) { best = g; bs = s; }
                }
                if (best != null && best.uid != u.equip[si]) Equip(u, best);
            }
        }
        public void AutoEquipAll() { foreach (var u in c.Roster.All) AutoEquip(u); }

        // ---------- selling ----------
        public double SellValue(ItemInstance g)
        {
            var def = c.Content.Item(g.defId); var rar = c.Content.Rarity(g.rarity);
            return Math.Max(1, Math.Round(def.price * (rar?.sell ?? 1) * (1 + g.enh * 0.35 + g.affix.Count * 0.15)));
        }
        public double SellValueStack(string id) => Math.Max(1, Math.Round(c.Content.Item(id).price * 0.5));
        public bool Sell(ItemInstance g)
        {
            if (g == null || Get(g.uid) != g || g.locked || !string.IsNullOrEmpty(g.owner)) return false;
            double v = SellValue(g); RemoveGear(g); c.Wallet.Add("gold", v); c.Counters.Add("items.sold"); c.Changed(); return true;
        }
        public bool SellStack(string id, int n)
        {
            var def = c.Content.Item(id);
            if (n <= 0 || def == null || def.IsGear) return false;
            if (Count(id) < n) return false;
            double v = SellValueStack(id) * n; Remove(id, n); c.Wallet.Add("gold", v); c.Counters.Add("items.sold", n); c.Changed(); return true;
        }
        /// <summary>Sells all unequipped, unlocked gear of rarity index &lt;= maxRarity. Returns gold earned.</summary>
        public double SellJunk(int maxRarityIdx)
        {
            double total = 0; int n = 0;
            foreach (var g in gear.Where(x => x.owner == "" && !x.locked && c.Content.Rarities.FindIndex(r => r.id == x.rarity) <= maxRarityIdx).ToList())
            { total += SellValue(g); RemoveGear(g); n++; }
            if (total > 0) { c.Wallet.Add("gold", total); c.Counters.Add("items.sold", n); c.Changed(); }
            return total;
        }

        // ---------- consumables ----------
        public bool Use(string defId, UnitInstance target)
        {
            var def = c.Content.Item(defId); if (def == null || def.type != "consumable" || Count(defId) < 1) return false;
            switch (def.use)
            {
                case "heal_pct": if (target == null || c.Roster.IsBusy(target) || c.Roster.HpFrac(target) >= 0.999) return false; c.Roster.SetHp(target, c.Roster.Hp(target) + c.Roster.MaxHp(target) * def.useV); break;
                case "xp": if (target == null || c.Roster.IsBusy(target) || target.level >= c.Roster.LevelCap(target)) return false; c.Roster.AddXp(target, def.useV); break;
                case "gold": c.Wallet.Add("gold", def.useV); break;
                case "fame": c.Counters.Add("fame", def.useV); break;
                default: return false;
            }
            Remove(defId, 1); c.Counters.Add("items.used"); c.Roster.Dirty(); c.Changed(); return true;
        }
    }

    // =====================================================================================
    public static class Loot
    {
        public static List<Drop> Roll(GameContext c, string tableId, double luck, Rng rng)
        {
            var res = new List<Drop>();
            if (string.IsNullOrEmpty(tableId) || !c.Content.LootMap.TryGetValue(tableId, out var t)) return res;
            var w = new List<double>(); foreach (var e in t.entries) w.Add(e.w);
            for (int r = 0; r < t.rolls; r++)
            {
                int i = rng.Weighted(w); if (i < 0) break;
                var e = t.entries[i];
                if (e.chance < 1 && !rng.Chance(Math.Min(1, e.chance * (1 + luck)))) continue;
                int n = (int)Math.Round(e.min + (e.max - e.min) * rng.D());
                if (e.id == "@gear")
                {
                    var pool = c.Content.Items.Where(x => x.IsGear && x.tier == e.tier && (string.IsNullOrEmpty(e.slot) || x.slot == e.slot)).ToList();
                    if (pool.Count == 0) continue;
                    for (int k = 0; k < Math.Max(1, n); k++) res.Add(new Drop { gear = c.Inventory.Generate(rng.Pick(pool), null, luck, rng) });
                }
                else res.Add(new Drop { res = new Res(e.id, Math.Max(1, n)) });
            }
            return res;
        }
        public static void Apply(GameContext c, List<Drop> drops)
        {
            foreach (var d in drops)
            {
                if (d.gear != null) { d.gear.isNew = true; c.Inventory.AddGear(d.gear); }
                else if (d.res != null) c.Give(d.res);
            }
            c.Changed();
        }
        /// <summary>Merge currency/material duplicates for display.</summary>
        public static List<Drop> Merge(List<Drop> l)
        {
            var o = new List<Drop>(); var m = new Dictionary<string, int>();
            foreach (var d in l)
            {
                if (d.gear != null) { o.Add(d); continue; }
                if (m.TryGetValue(d.res.id, out var i)) o[i].res.n += d.res.n; else { m[d.res.id] = o.Count; o.Add(new Drop { res = new Res(d.res.id, d.res.n) }); }
            }
            return o;
        }
    }

    // =====================================================================================
    public class Crafting : ISystem, IPostInit
    {
        GameContext c;
        public void Init(GameContext ctx) { c = ctx; }
        public void PostInit()
        {
            c.Timers.OnComplete("craft", t =>
            {
                var r = c.Content.RecipeMap.TryGetValue(t.refId, out var rr) ? rr : null; if (r == null) return;
                var def = c.Content.Item(r.result);
                int bonus = c.Rng.Chance(c.Mods.Add("forge.double")) ? 1 : 0;
                string rar = null;
                if (def.IsGear) { double luck = c.Mods.Add("forge.luck"); var tmp = c.Inventory.Generate(def, null, luck, c.Rng); rar = tmp.rarity; }
                c.Inventory.AddItem(r.result, r.n + bonus, c.Rng, rar);
                c.Counters.Add("crafted", r.n + bonus); c.Counters.Add("crafted." + def.type);
                c.Note(NoteKind.Build, $"Forged {def.name}!", def.icon);
            });
        }
        public int Slots => (int)(c.Content.Bal("forge.slots", 1) + c.Mods.Add("forge.slots"));
        public IEnumerable<TimedTask> Active => c.Timers.Tasks.Where(t => t.kind == "craft");
        public double CraftTime(RecipeDef r) => Math.Max(5, r.time / (1 + c.Mods.Pct("forge.speed")));
        public bool Unlocked(RecipeDef r) => c.MeetsAll(r.req);
        public bool CanCraft(RecipeDef r) => Unlocked(r) && Active.Count() < Slots && c.CanPay(r.cost);
        public bool Craft(RecipeDef r)
        {
            if (!CanCraft(r)) return false;
            c.Pay(r.cost);
            c.Timers.Start("craft", r.id, CraftTime(r));
            c.Changed(); return true;
        }

        // ----- enhancement -----
        public int MaxEnh => (int)c.Mods.Apply("forge.max_enh", c.Content.Bal("enh.max", 2));
        public List<Res> EnhCost(ItemInstance g)
        {
            var def = c.Content.Item(g.defId); double lv = g.enh + 1;
            return new List<Res> {
                new Res("gold", Math.Round(c.Content.Bal("enh.gold", 30) * Math.Pow(lv, 1.6) * def.tier * (1 - Math.Min(0.5, c.Mods.Pct("forge.discount"))))),
                new Res("iron_ore", Math.Ceiling(lv * c.Content.Bal("enh.ore", 1) + def.tier - 1)) };
        }
        public double EnhChance(ItemInstance g) => g.enh < 4 ? 1.0 : Math.Min(1.0, Math.Max(0.3, 1.0 - (g.enh - 3) * 0.14) + c.Mods.Add("forge.luck_enh"));
        public bool CanEnhance(ItemInstance g) => g != null && c.Inventory.Get(g.uid) == g && g.enh < MaxEnh && c.CanPay(EnhCost(g));
        /// <returns>0 fail, 1 success, -1 invalid</returns>
        public int Enhance(ItemInstance g)
        {
            if (!CanEnhance(g)) return -1;
            double chance = EnhChance(g);
            c.Pay(EnhCost(g));
            c.Counters.Add("enhance.tries");
            bool ok = c.Rng.Chance(chance);
            if (ok) { g.enh++; c.Counters.Add("enhance.success"); c.Counters.Max("enhance.best", g.enh); c.Roster.Dirty(); }
            c.Changed(); return ok ? 1 : 0;
        }

        // ----- salvage -----
        public List<Res> SalvageYield(ItemInstance g)
        {
            var def = c.Content.Item(g.defId); int ri = Math.Max(0, c.Content.Rarities.FindIndex(r => r.id == g.rarity));
            var l = new List<Res> { new Res("iron_ore", def.tier * (1 + ri) + g.enh) };
            if (ri >= 2) l.Add(new Res("essence", ri - 1));
            return l;
        }
        public bool Salvage(ItemInstance g)
        {
            if (g == null || c.Inventory.Get(g.uid) != g || g.locked || !string.IsNullOrEmpty(g.owner)) return false;
            foreach (var r in SalvageYield(g)) c.Give(r);
            c.Inventory.RemoveGear(g); c.Counters.Add("salvaged"); c.Changed(); return true;
        }
    }

    // =====================================================================================
    public class Market : ISystem, ISaveable, ITickable, INewGame
    {
        GameContext c;
        [Serializable] public class Offer { public string itemId, rarity; public double price; public string cur = "gold"; public bool sold; public int qty = 1; }
        [Serializable] class Data { public List<Offer> offers = new List<Offer>(); public double day = -1; }
        Data d = new Data();
        public IReadOnlyList<Offer> Offers => d.offers;
        public string SaveKey => "market";
        public void Init(GameContext ctx) { c = ctx; }
        public string Save() => JsonUtility.ToJson(d);
        public void Load(string json) { d = JsonUtility.FromJson<Data>(json) ?? new Data(); }
        public void Reset() { d = new Data(); }
        public void OnNewGame() { }
        public double NextRefresh => (d.day + 1) * 86400.0;

        public void Tick(double now)
        {
            if (c.Ranks.Index < 0) return;
            double day = Util.Day(now);
            if (d.offers.Count == 0 || day != d.day) Generate(day);
        }
        void Generate(double day)
        {
            d.day = day; d.offers.Clear();
            var rng = new Rng((int)(day * 31 + 7));
            int tier = Math.Max(1, c.Ranks.Index / 2 + 1);
            int slots = (int)(c.Content.Bal("market.slots", 6) + c.Mods.Add("market.slots"));
            var gearPool = c.Content.Items.Where(i => i.IsGear && i.tier <= tier).ToList();
            var cons = c.Content.Items.Where(i => i.type == "consumable").ToList();
            var mats = c.Content.Items.Where(i => i.type == "material" && i.price > 0).ToList();
            for (int i = 0; i < slots; i++)
            {
                var roll = rng.F();
                if (roll < 0.5 && gearPool.Count > 0)
                {
                    var def = rng.Pick(gearPool); var g = c.Inventory.Generate(def, null, 0.3, rng);
                    d.offers.Add(new Offer { itemId = def.id, rarity = g.rarity, price = Math.Round(def.price * c.Content.Rarity(g.rarity).sell * 3.2) });
                }
                else if (roll < 0.8 && cons.Count > 0) { var def = rng.Pick(cons); int q = rng.Range(1, 3); d.offers.Add(new Offer { itemId = def.id, price = Math.Round(def.price * 2.5 * q), qty = q }); }
                else if (mats.Count > 0) { var def = rng.Pick(mats); int q = rng.Range(2, 6); d.offers.Add(new Offer { itemId = def.id, price = Math.Round(def.price * 2.2 * q), qty = q }); }
            }
            d.offers.Add(new Offer { itemId = "essence", price = 6, cur = "gems", qty = 3 });
        }
        public bool Buy(Offer o)
        {
            if (o == null || !d.offers.Contains(o) || o.sold) return false;
            var def = c.Content.Item(o.itemId);
            if (def == null) return false;
            if (def.IsGear && c.Inventory.Full) { c.Note(NoteKind.Warn, "Bag is full"); return false; }
            if (!c.Wallet.Spend(o.cur, o.price)) { c.Note(NoteKind.Warn, "Not enough " + c.ResName(o.cur)); return false; }
            c.Inventory.AddItem(o.itemId, o.qty, c.Rng, o.rarity);
            o.sold = true; c.Counters.Add("market.bought"); c.Changed(); return true;
        }
    }
}
