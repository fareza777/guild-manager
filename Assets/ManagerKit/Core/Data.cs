using System;
using System.Collections.Generic;
using UnityEngine;

namespace ManagerKit
{
    // =====================================================================================
    //  Data definitions. Everything the game *is* lives in JSON content packs that
    //  deserialize into these classes. Swap the pack to make a different manager game.
    // =====================================================================================

    /// <summary>A resource amount: id refers to a currency (see CurrencyDef) or an item (ItemDef).</summary>
    [Serializable] public class Res { public string id; public double n = 1; public Res() { } public Res(string id, double n) { this.id = id; this.n = n; } }
    /// <summary>Requirement: named stat (see StatResolver) must be >= min.</summary>
    [Serializable] public class Req { public string stat; public double min = 1; public Req() { } public Req(string s, double m) { stat = s; min = m; } }
    /// <summary>Modifier: op "add" (flat) or "pct" (fraction, 0.1 = +10%).</summary>
    [Serializable] public class Mod { public string key; public string op = "add"; public double v; public Mod() { } public Mod(string k, double v, string op = "add") { key = k; this.v = v; this.op = op; } }
    [Serializable] public class StatVal { public string id; public double v; public StatVal() { } public StatVal(string i, double v) { id = i; this.v = v; } }

    [Serializable] public class CurrencyDef { public string id; public string name; public string icon; public string color = "#FFFFFF"; public double start; public bool hud = true; public string desc; }
    [Serializable] public class StatDef { public string id; public string name; public string icon; public bool pct; public string color = "#FFFFFF"; }
    [Serializable] public class RarityDef { public string id; public string name; public string color = "#FFFFFF"; public double mult = 1; public int affixes; public double weight = 1; public double sell = 1; }
    [Serializable] public class TraitDef { public string id; public string name; public string desc; public bool good = true; public double weight = 1; public List<Mod> mods = new List<Mod>(); }

    [Serializable]
    public class SkillDef
    {
        public string id, name, desc;
        public string target = "enemy";   // enemy | enemy_all | enemy_two | ally_lowest | ally_all | self
        public string type = "phys";      // phys | magic | heal | buff
        public double power = 1;          // multiplier of ATK
        public int cd;                    // cooldown in own turns (0 = basic attack)
        public string status;             // stun | poison | shield | atk_up | def_up | spd_up | taunt | weaken
        public double statusV;            // magnitude (pct, or shield = x ATK)
        public int statusT = 2;           // duration in turns
        public string vfx = "slash";      // slash | arrow | fire | heal | bolt | dark | bite
        public int unlockLevel = 1;
        public double crit;               // extra crit chance
    }

    [Serializable]
    public class ClassDef
    {
        public string id, name, desc, role, icon, color = "#FFFFFF";
        public string anim = "sword01";   // attack animation set
        public string weapon = "sword";   // weapon family usable
        public bool ranged;
        public List<StatVal> baseStats = new List<StatVal>();
        public List<StatVal> growth = new List<StatVal>();
        public List<string> skills = new List<string>(); // [basic, special, ultimate]
        public List<string> tags = new List<string>();
    }

    /// <summary>Fixed recruitable unit (e.g. tameable monsters). Generic human recruits are generated from ClassDef.</summary>
    [Serializable]
    public class UnitDef
    {
        public string id, name, desc, classId, sprite, rarity = "rare";
        public float scale = 1f;
        public List<StatVal> mult = new List<StatVal>();   // multipliers over class base, id = stat
        public List<string> traits = new List<string>();
        public List<Res> cost = new List<Res>();
        public List<Req> req = new List<Req>();
    }

    [Serializable]
    public class ItemDef
    {
        public string id, name, desc, type = "material";   // weapon | armor | accessory | material | consumable
        public string slot, family, icon;                    // slot only for gear; family: sword/bow/staff/dagger/heavy/light
        public int tier = 1;
        public double price = 1;
        public List<StatVal> stats = new List<StatVal>();
        public List<string> classes = new List<string>();    // optional class restriction
        public string use; public double useV;               // consumables: heal_pct | xp | fame | gold
        public string tint;                                  // icon tint hex
        public bool IsGear => type == "weapon" || type == "armor" || type == "accessory";
    }

    [Serializable] public class RecipeDef { public string id, result, name; public int n = 1; public List<Res> cost = new List<Res>(); public double time = 0; public List<Req> req = new List<Req>(); }
    [Serializable]
    public class EnemyDef
    {
        public string id, name, sprite, skin = "sheet"; // skin: sheet (animated monster sheet) | battler (single big image)
        public float scale = 1f; public bool boss; public List<string> tags = new List<string>();
        public List<StatVal> stats = new List<StatVal>(); public List<string> skills = new List<string>();
        public double xp = 10, gold = 5;
    }
    [Serializable] public class LootEntry { public string id; public double w = 1; public double min = 1, max = 1; public string slot; public int tier = 1; public double chance = 1; }
    [Serializable] public class LootTableDef { public string id; public int rolls = 1; public List<LootEntry> entries = new List<LootEntry>(); }
    [Serializable] public class WaveDef { public List<Res> enemies = new List<Res>(); }
    [Serializable] public class RegionDef { public string id, name, desc, bg, color = "#FFFFFF"; public int order; public int tier = 1; public List<Req> req = new List<Req>(); }

    [Serializable]
    public class MissionDef
    {
        public string id, kind = "battle", region, name, desc, bg;   // kind: battle | exam | expedition
        public int order, level = 1, tier = 1;
        public List<WaveDef> waves = new List<WaveDef>();
        public List<Res> rewards = new List<Res>();       // always granted on win (gold, fame, ...)
        public List<Res> firstClear = new List<Res>();
        public double xp = 10;                             // XP per surviving hero
        public string loot;
        public List<Req> req = new List<Req>();
        public double duration;                            // expedition seconds
        public double power;                               // expedition difficulty (compared to party power)
        public List<string> recommend = new List<string>();
        public int maxTurns = 60; public int par3 = 12;    // stars: win / no deaths / won within par3 rounds
        public bool boss;
    }

    [Serializable] public class EffectDef { public string key; public string op = "add"; public double @base; public double per; }
    [Serializable]
    public class UpgradeDef
    {
        public string id, kind = "facility", name, desc, icon, sprite;   // kind: facility | research
        public int max = 10; public int slot;                         // slot: position on guild hall map
        public List<Res> baseCost = new List<Res>(); public double costGrowth = 1.5;
        public double baseTime = 30, timeGrowth = 1.6;                  // seconds
        public List<EffectDef> effects = new List<EffectDef>();
        public List<Req> req = new List<Req>();                          // to build (level 0 -> 1)
        public List<Req> reqLevel = new List<Req>();                      // stat requirements applied to level N+1 : use "lvl:N:stat"
        public List<string> levelNotes = new List<string>(); public int start;
    }

    [Serializable]
    public class StationDef
    {
        public string id, upgradeId, kind = "train"; // train | heal
        public string name, slotsKey, speedKey; public double slotsBase = 1;
        public double baseTime = 60, perLevelTime = 30; public double goldBase = 40, goldPerLevel = 25;
        public double value = 1; // train: xp multiplier on level-curve; heal: fraction of max hp per minute
    }

    [Serializable] public class RankDef { public string id, name, title, perk, exam; public double fame; public List<Mod> mods = new List<Mod>(); public string color = "#FFFFFF"; }
    [Serializable] public class GoalDef { public string stat; public double target = 1; public string mode = "delta"; public string text; }
    [Serializable]
    public class QuestDef
    {
        public string id, type = "side", title, desc, giver, story, hint, group;  // type: main | side | daily
        public int order;
        public List<GoalDef> goals = new List<GoalDef>(); public List<Res> rewards = new List<Res>();
        public List<Req> req = new List<Req>(); public List<string> next = new List<string>();
        public int weight = 1;  // daily pool weight
    }

    [Serializable] public class EventOutcome { public double w = 1; public string text; public List<Res> gain = new List<Res>(), loss = new List<Res>(); public double hurt; public string item; }
    [Serializable] public class EventChoice { public string text; public List<Req> req = new List<Req>(); public List<Res> cost = new List<Res>(); public List<EventOutcome> outcomes = new List<EventOutcome>(); }
    [Serializable] public class EventDef { public string id, title, text, sprite; public double w = 1; public List<Req> req = new List<Req>(); public List<EventChoice> choices = new List<EventChoice>(); }

    [Serializable] public class SynergyDef { public string id, name, desc, tag; public int count = 2; public List<Mod> mods = new List<Mod>(); public string classA, classB; }
    [Serializable] public class NamePool { public List<string> first = new List<string>(), last = new List<string>(); }

    /// <summary>One JSON file. Multiple packs in a folder merge into one ContentDb.</summary>
    [Serializable]
    public class ContentPack
    {
        public string gameId, title;
        public List<StatVal> balance = new List<StatVal>();
        public List<CurrencyDef> currencies = new List<CurrencyDef>();
        public List<StatDef> stats = new List<StatDef>();
        public List<RarityDef> rarities = new List<RarityDef>();
        public List<TraitDef> traits = new List<TraitDef>();
        public List<SkillDef> skills = new List<SkillDef>();
        public List<ClassDef> classes = new List<ClassDef>();
        public List<UnitDef> units = new List<UnitDef>();
        public List<ItemDef> items = new List<ItemDef>();
        public List<RecipeDef> recipes = new List<RecipeDef>();
        public List<EnemyDef> enemies = new List<EnemyDef>();
        public List<LootTableDef> loot = new List<LootTableDef>();
        public List<RegionDef> regions = new List<RegionDef>();
        public List<MissionDef> missions = new List<MissionDef>();
        public List<UpgradeDef> upgrades = new List<UpgradeDef>();
        public List<StationDef> stations = new List<StationDef>();
        public List<RankDef> ranks = new List<RankDef>();
        public List<QuestDef> quests = new List<QuestDef>();
        public List<EventDef> events = new List<EventDef>();
        public List<SynergyDef> synergies = new List<SynergyDef>();
        public NamePool names = new NamePool();
        public List<string> skins = new List<string>();
        public List<CalendarDay> calendar = new List<CalendarDay>();
    }

    /// <summary>Indexed, merged content. The only thing game code should read definitions from.</summary>
    public class ContentDb
    {
        public string GameId = "game", Title = "Game";
        public readonly Dictionary<string, double> Balance = new Dictionary<string, double>();
        public readonly List<CurrencyDef> Currencies = new List<CurrencyDef>();
        public readonly List<StatDef> Stats = new List<StatDef>();
        public readonly List<RarityDef> Rarities = new List<RarityDef>();
        public readonly List<RankDef> Ranks = new List<RankDef>();
        public readonly List<RegionDef> Regions = new List<RegionDef>();
        public readonly List<MissionDef> Missions = new List<MissionDef>();
        public readonly List<UpgradeDef> Upgrades = new List<UpgradeDef>();
        public readonly List<QuestDef> Quests = new List<QuestDef>();
        public readonly List<ClassDef> Classes = new List<ClassDef>();
        public readonly List<UnitDef> Units = new List<UnitDef>();
        public readonly List<ItemDef> Items = new List<ItemDef>();
        public readonly List<RecipeDef> Recipes = new List<RecipeDef>();
        public readonly List<TraitDef> Traits = new List<TraitDef>();
        public readonly List<EventDef> Events = new List<EventDef>();
        public readonly List<StationDef> Stations = new List<StationDef>();
        public readonly List<SynergyDef> Synergies = new List<SynergyDef>();
        public readonly List<string> Skins = new List<string>();
        public readonly List<CalendarDay> Calendar = new List<CalendarDay>();
        public List<Res> CalendarRewards(int streak) => Calendar.Count == 0 ? new List<Res>() : Calendar[Math.Max(0, streak - 1) % Calendar.Count].rewards;
        public NamePool Names = new NamePool();

        public readonly Dictionary<string, CurrencyDef> CurrencyMap = new Dictionary<string, CurrencyDef>();
        public readonly Dictionary<string, StatDef> StatMap = new Dictionary<string, StatDef>();
        public readonly Dictionary<string, RarityDef> RarityMap = new Dictionary<string, RarityDef>();
        public readonly Dictionary<string, TraitDef> TraitMap = new Dictionary<string, TraitDef>();
        public readonly Dictionary<string, SkillDef> SkillMap = new Dictionary<string, SkillDef>();
        public readonly Dictionary<string, ClassDef> ClassMap = new Dictionary<string, ClassDef>();
        public readonly Dictionary<string, UnitDef> UnitMap = new Dictionary<string, UnitDef>();
        public readonly Dictionary<string, ItemDef> ItemMap = new Dictionary<string, ItemDef>();
        public readonly Dictionary<string, RecipeDef> RecipeMap = new Dictionary<string, RecipeDef>();
        public readonly Dictionary<string, EnemyDef> EnemyMap = new Dictionary<string, EnemyDef>();
        public readonly Dictionary<string, LootTableDef> LootMap = new Dictionary<string, LootTableDef>();
        public readonly Dictionary<string, RegionDef> RegionMap = new Dictionary<string, RegionDef>();
        public readonly Dictionary<string, MissionDef> MissionMap = new Dictionary<string, MissionDef>();
        public readonly Dictionary<string, UpgradeDef> UpgradeMap = new Dictionary<string, UpgradeDef>();
        public readonly Dictionary<string, StationDef> StationMap = new Dictionary<string, StationDef>();
        public readonly Dictionary<string, RankDef> RankMap = new Dictionary<string, RankDef>();
        public readonly Dictionary<string, QuestDef> QuestMap = new Dictionary<string, QuestDef>();
        public readonly Dictionary<string, EventDef> EventMap = new Dictionary<string, EventDef>();

        public double Bal(string key, double def = 0) => Balance.TryGetValue(key, out var v) ? v : def;
        public bool IsCurrency(string id) => CurrencyMap.ContainsKey(id);
        public ItemDef Item(string id) => id != null && ItemMap.TryGetValue(id, out var d) ? d : null;
        public RarityDef Rarity(string id) => id != null && RarityMap.TryGetValue(id, out var d) ? d : (Rarities.Count > 0 ? Rarities[0] : null);
        public ClassDef Class(string id) => id != null && ClassMap.TryGetValue(id, out var d) ? d : null;
        public MissionDef Mission(string id) => id != null && MissionMap.TryGetValue(id, out var d) ? d : null;
        public UpgradeDef Upgrade(string id) => id != null && UpgradeMap.TryGetValue(id, out var d) ? d : null;
        public RankDef Rank(int i) => Ranks.Count == 0 ? null : Ranks[Mathf.Clamp(i, 0, Ranks.Count - 1)];

        public static ContentDb FromJson(IEnumerable<string> jsons)
        {
            var db = new ContentDb();
            foreach (var j in jsons) db.Merge(JsonUtility.FromJson<ContentPack>(j));
            db.Index();
            return db;
        }

        public static ContentDb FromResources(string folder)
        {
            var assets = Resources.LoadAll<TextAsset>(folder);
            Array.Sort(assets, (a, b) => string.CompareOrdinal(a.name, b.name));
            var list = new List<string>(); foreach (var a in assets) list.Add(a.text);
            return FromJson(list);
        }

        public void Merge(ContentPack p)
        {
            if (p == null) return;
            if (!string.IsNullOrEmpty(p.gameId)) GameId = p.gameId;
            if (!string.IsNullOrEmpty(p.title)) Title = p.title;
            foreach (var b in p.balance) Balance[b.id] = b.v;
            Currencies.AddRange(p.currencies); Stats.AddRange(p.stats); Rarities.AddRange(p.rarities); Traits.AddRange(p.traits);
            skillsTemp.AddRange(p.skills); Classes.AddRange(p.classes); Units.AddRange(p.units); Items.AddRange(p.items);
            Recipes.AddRange(p.recipes); enemiesTemp.AddRange(p.enemies); lootTemp.AddRange(p.loot);
            Regions.AddRange(p.regions); Missions.AddRange(p.missions); Upgrades.AddRange(p.upgrades); Stations.AddRange(p.stations);
            Ranks.AddRange(p.ranks); Quests.AddRange(p.quests); Events.AddRange(p.events); Synergies.AddRange(p.synergies);
            Skins.AddRange(p.skins); Calendar.AddRange(p.calendar);
            if (p.names != null) { Names.first.AddRange(p.names.first); Names.last.AddRange(p.names.last); }
        }
        readonly List<SkillDef> skillsTemp = new List<SkillDef>();
        readonly List<EnemyDef> enemiesTemp = new List<EnemyDef>();
        readonly List<LootTableDef> lootTemp = new List<LootTableDef>();

        public void Index()
        {
            foreach (var x in Currencies) CurrencyMap[x.id] = x;
            foreach (var x in Stats) StatMap[x.id] = x;
            foreach (var x in Rarities) RarityMap[x.id] = x;
            foreach (var x in Traits) TraitMap[x.id] = x;
            foreach (var x in skillsTemp) SkillMap[x.id] = x;
            foreach (var x in Classes) ClassMap[x.id] = x;
            foreach (var x in Units) UnitMap[x.id] = x;
            foreach (var x in Items) ItemMap[x.id] = x;
            foreach (var x in Recipes) RecipeMap[x.id] = x;
            foreach (var x in enemiesTemp) EnemyMap[x.id] = x;
            foreach (var x in lootTemp) LootMap[x.id] = x;
            foreach (var x in Regions) RegionMap[x.id] = x;
            foreach (var x in Missions) MissionMap[x.id] = x;
            foreach (var x in Upgrades) UpgradeMap[x.id] = x;
            foreach (var x in Stations) StationMap[x.id] = x;
            foreach (var x in Ranks) RankMap[x.id] = x;
            foreach (var x in Quests) QuestMap[x.id] = x;
            foreach (var x in Events) EventMap[x.id] = x;
            Regions.Sort((a, b) => a.order.CompareTo(b.order));
            Missions.Sort((a, b) => a.order.CompareTo(b.order));
            Quests.Sort((a, b) => a.order.CompareTo(b.order));
        }

        /// <summary>Checks every id reference in the content. Run in tests / editor menu.</summary>
        public List<string> Validate()
        {
            var e = new List<string>();
            void Chk(bool ok, string msg) { if (!ok) e.Add(msg); }
            void Res_(IEnumerable<Res> l, string ctx) { if (l != null) foreach (var r in l) Chk(r != null && (CurrencyMap.ContainsKey(r.id) || ItemMap.ContainsKey(r.id) || EnemyMap.ContainsKey(r.id) || r.id == "fame"), $"{ctx}: unknown resource '{r?.id}'"); }
            foreach (var c in Classes) { foreach (var s in c.skills) Chk(SkillMap.ContainsKey(s), $"class {c.id}: skill '{s}' missing"); foreach (var s in c.baseStats) Chk(StatMap.ContainsKey(s.id), $"class {c.id}: stat '{s.id}'"); }
            foreach (var u in Units) { Chk(ClassMap.ContainsKey(u.classId), $"unit {u.id}: class"); Chk(RarityMap.ContainsKey(u.rarity), $"unit {u.id}: rarity"); foreach (var t in u.traits) Chk(TraitMap.ContainsKey(t), $"unit {u.id}: trait {t}"); Res_(u.cost, "unit " + u.id); }
            foreach (var en in enemiesTemp) { foreach (var s in en.skills) Chk(SkillMap.ContainsKey(s), $"enemy {en.id}: skill '{s}'"); Chk(!string.IsNullOrEmpty(en.sprite), $"enemy {en.id}: sprite"); }
            foreach (var l in lootTemp) foreach (var en in l.entries) Chk(en.id == "@gear" || CurrencyMap.ContainsKey(en.id) || ItemMap.ContainsKey(en.id), $"loot {l.id}: '{en.id}'");
            foreach (var it in Items) { Chk(RarityMap.Count == 0 || it.tier >= 1, $"item {it.id}: tier"); if (it.IsGear) Chk(!string.IsNullOrEmpty(it.slot), $"item {it.id}: slot"); }
            foreach (var r in Recipes) { Chk(ItemMap.ContainsKey(r.result), $"recipe {r.id}: result"); Res_(r.cost, "recipe " + r.id); }
            foreach (var m in Missions)
            {
                Chk(RegionMap.ContainsKey(m.region), $"mission {m.id}: region '{m.region}'");
                if (!string.IsNullOrEmpty(m.loot)) Chk(LootMap.ContainsKey(m.loot), $"mission {m.id}: loot '{m.loot}'");
                foreach (var w in m.waves) foreach (var en in w.enemies) Chk(EnemyMap.ContainsKey(en.id), $"mission {m.id}: enemy '{en.id}'");
                Chk(m.kind == "expedition" || m.kind == "contract" || m.waves.Count > 0, $"mission {m.id}: no waves");
                Res_(m.rewards, "mission " + m.id); Res_(m.firstClear, "mission " + m.id);
            }
            foreach (var u in Upgrades) { Res_(u.baseCost, "upgrade " + u.id); }
            foreach (var s in Stations) Chk(UpgradeMap.ContainsKey(s.upgradeId), $"station {s.id}: upgrade '{s.upgradeId}'");
            foreach (var r in Ranks) if (!string.IsNullOrEmpty(r.exam)) Chk(MissionMap.ContainsKey(r.exam), $"rank {r.id}: exam '{r.exam}'");
            foreach (var q in Quests) { Res_(q.rewards, "quest " + q.id); foreach (var n in q.next) Chk(QuestMap.ContainsKey(n), $"quest {q.id}: next '{n}'"); Chk(q.goals.Count > 0, $"quest {q.id}: no goals"); }
            foreach (var ev in Events) foreach (var c in ev.choices) { Res_(c.cost, "event " + ev.id); foreach (var o in c.outcomes) { Res_(o.gain, "event " + ev.id); Res_(o.loss, "event " + ev.id); if (!string.IsNullOrEmpty(o.item)) Chk(ItemMap.ContainsKey(o.item), $"event {ev.id}: item {o.item}"); } }
            return e;
        }
    }
}
