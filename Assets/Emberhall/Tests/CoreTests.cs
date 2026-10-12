using System.Collections.Generic;
using System.Linq;
using ManagerKit;
using NUnit.Framework;
using UnityEngine;

namespace Emberhall.Tests
{
    public class CoreTests
    {
        GameContext c;
        double clock;

        [SetUp]
        public void Setup()
        {
            clock = 1_800_000_000; Util.ClockOverride = () => clock;
            c = GuildGame.Create();
            GuildGame.StartNew(c, "Test Guild", 0, "warrior");
        }
        [TearDown] public void Teardown() { Util.ClockOverride = null; }

        void Advance(double sec) { clock += sec; c.LastTick = 0; c.Tick(); }

        void BuildFacilities(params string[] ids)
        {
            c.Counters.Max("missions.won", 4); c.Wallet.Add("gold", 10000);
            c.Inventory.AddItem("iron_ore", 100, c.Rng);
            foreach (var id in ids)
            {
                var facility = c.Content.Upgrade(id);
                Assert.IsTrue(c.Upgrades.Start(facility), id + ": " + c.Upgrades.Blocker(facility));
                Advance(c.Timers.Remaining(c.Upgrades.Pending(id)) + 0.01);
                Assert.AreEqual(1, c.Upgrades.Level(id), id);
            }
        }

        double Resource(GameContext ctx, string id) => ctx.Content.IsCurrency(id) ? ctx.Wallet.Get(id)
            : id == "fame" ? ctx.Counters.Get(id) : ctx.Inventory.Count(id);

        Dictionary<string, double> Snapshot(GameContext ctx, IEnumerable<Res> resources) => resources
            .Select(r => r.id).Distinct().ToDictionary(id => id, id => Resource(ctx, id));

        void AssertResourceChange(GameContext ctx, Dictionary<string, double> before, IEnumerable<Res> resources, int sign)
        {
            foreach (var group in resources.GroupBy(r => r.id))
                Assert.AreEqual(before[group.Key] + sign * group.Sum(r => r.n), Resource(ctx, group.Key), 0.001, group.Key);
        }

        double EarnedXp(GameContext ctx, UnitInstance hero) => hero.xp
            + Enumerable.Range(1, hero.level - 1).Sum(level => ctx.Roster.XpToNext(level));

        MissionOutcome WinWithStrongHero(MissionDef mission)
        {
            var hero = c.Roster.All[0]; hero.level = 50; hero.rarity = "legendary"; hero.hp = -1; hero.traits.Clear(); c.Roster.Dirty();
            var party = new List<UnitInstance> { hero }; var setup = c.Missions.BuildSetup(mission, party, 42);
            var result = BattleSim.Run(setup); Assert.IsTrue(result.win, mission.id);
            return c.Missions.Complete(mission, party, setup, result);
        }

        [Test]
        public void ContentIsValid()
        {
            var errs = c.Content.Validate();
            Assert.IsEmpty(errs, string.Join("\n", errs));
            Assert.GreaterOrEqual(c.Content.Missions.Count, 30);
            Assert.GreaterOrEqual(c.Content.Quests.Count(q => q.type == "main"), 20);
        }

        [Test]
        public void JsonContentParsesEveryFile()
        {
            foreach (var ta in Resources.LoadAll<TextAsset>(GuildGame.ContentFolder))
            {
                var p = JsonUtility.FromJson<ContentPack>(ta.text);
                Assert.NotNull(p, ta.name);
            }
        }

        [Test]
        public void NewGameState()
        {
            Assert.AreEqual(1, c.Roster.Count);
            Assert.AreEqual(1, c.Upgrades.Level("tavern"));
            Assert.AreEqual(c.Content.CurrencyMap["gold"].start, c.Wallet.Get("gold"));
            Assert.AreEqual(3, c.Recruiter.Pool.Count);
            Assert.IsNotNull(c.Quests.CurrentMain);
            Assert.AreEqual("q_m01", c.Quests.CurrentMain.id);
        }

        [Test]
        public void SelectableStartersMatchTheCharterAndHaveNoOpeningBlocker()
        {
            var classes = new[] { "warrior", "archer", "mage" };
            var portraits = new[] { "chara_0", "chara_6", "chara_2" };
            for (int index = 0; index < classes.Length; index++)
                for (int seed = 1; seed <= 8; seed++)
                {
                    c.Rng = new Rng(seed); GuildGame.StartNew(c, "Opening", 0, classes[index]);
                    var hero = c.Roster.All.Single();
                    Assert.AreEqual(portraits[index], hero.sprite);
                    Assert.AreEqual(1, c.Roster.HpFrac(hero));
                    Assert.IsNull(c.Roster.ReadinessBlocker(hero));
                    Assert.IsTrue(c.Recruiter.Pool.Any(c.Recruiter.CanHire), classes[index] + " needs an affordable teammate");
                    Assert.AreEqual(0, c.Counters.Get("gear.equipped"), "Initial auto-equipping must not finish the guided gear step");
                    Assert.IsTrue(c.Missions.CanStartBattle(c.Content.Mission("m_r1_1"), new List<UnitInstance> { hero }, out var why), why);
                    Assert.IsNull(c.LastOffline);
                }
        }

        [Test]
        public void GuidedOpeningCreditsBattleAndGearCompletedBeforeTheirChapters()
        {
            var recruit = c.Recruiter.Pool.First(c.Recruiter.CanHire);
            Assert.IsTrue(c.Recruiter.Hire(recruit)); c.Inventory.AutoEquipAll();
            Assert.Greater(c.Counters.Get("gear.equipped"), 0);
            var mission = c.Content.Mission("m_r1_1"); var party = c.Roster.All.ToList();
            var setup = c.Missions.BuildSetup(mission, party, 42);
            Assert.IsTrue(c.Missions.Complete(mission, party, setup, BattleSim.Run(setup)).win);
            c.Quests.Tick(clock);
            Assert.IsTrue(c.Quests.Claim(c.Content.QuestMap["q_m01"]));
            Assert.AreEqual(QuestSystem.Ready, c.Quests.State("q_m02"), "The opening battle should count even before hiring rewards are claimed");
            Assert.IsTrue(c.Quests.Claim(c.Content.QuestMap["q_m02"]));
            Assert.AreEqual(QuestSystem.Ready, c.Quests.State("q_m03"), "Already equipped recruits must not stall the gear tutorial");
            Assert.IsTrue(c.Quests.Claim(c.Content.QuestMap["q_m03"]));
            Assert.AreEqual("q_m04", c.Quests.CurrentMain.id);
        }

        [Test]
        public void SaveRoundTripKeepsEverything()
        {
            c.Wallet.Add("gold", 1234);
            c.Counters.Add("missions.won", 3);
            var h = c.Roster.All[0]; c.Roster.AddXp(h, 500);
            c.Inventory.AddItem("w_sword_2", 1, c.Rng, "rare");
            c.Upgrades.Start(c.Content.Upgrade("tavern"));
            string json = c.SaveBlobs(out _);

            var c2 = GuildGame.Create();
            Assert.IsTrue(c2.LoadBlobs(json));
            Assert.AreEqual(c.Wallet.Get("gold"), c2.Wallet.Get("gold"));
            Assert.AreEqual(3, c2.Counters.Get("missions.won"));
            Assert.AreEqual(c.Roster.Count, c2.Roster.Count);
            Assert.AreEqual(h.level, c2.Roster.All[0].level);
            Assert.AreEqual(c.Inventory.Gear.Count, c2.Inventory.Gear.Count);
            Assert.AreEqual(c.Timers.Tasks.Count, c2.Timers.Tasks.Count);
            Assert.AreEqual(c.Recruiter.Pool.Count, c2.Recruiter.Pool.Count);
            Assert.AreEqual(c.Content.Rank(0).id, c2.Ranks.Current.id);
        }

        [Test]
        public void DiskSaveSurvivesCorruptionViaBackup()
        {
            var dir = System.IO.Path.Combine(Application.temporaryCachePath, "savetest_" + System.Guid.NewGuid().ToString("N"));
            var sys = new SaveSystem("slot", dir);
            double initialGold = c.Wallet.Get("gold");
            c.Wallet.Add("gold", 500);
            Assert.IsTrue(sys.Save(c));
            c.Wallet.Add("gold", 100);
            Assert.IsTrue(sys.Save(c));                      // creates .bak with the first state
            System.IO.File.WriteAllText(sys.Path, "{ not json");
            var c2 = GuildGame.Create();
            Assert.IsTrue(sys.Load(c2));
            Assert.AreEqual(initialGold + 500, c2.Wallet.Get("gold")); // restored from backup
            sys.Delete();
        }

        [Test]
        public void TimersCompleteOfflineAndReportIsBuilt()
        {
            c.Wallet.Add("gold", 5000); c.Counters.Add("missions.won", 6); c.Inventory.AddItem("iron_ore", 20, c.Rng);
            var bs = c.Content.Upgrade("blacksmith");
            Assert.IsTrue(c.Upgrades.Start(bs), c.Upgrades.Blocker(bs));
            Assert.AreEqual(0, c.Upgrades.Level("blacksmith"));
            string json = c.SaveBlobs(out _);
            clock += 3 * 3600;                                // app closed for 3 hours
            var c2 = GuildGame.Create();
            Assert.IsTrue(c2.LoadBlobs(json));
            Assert.AreEqual(1, c2.LastOffline.finishedTasks);
            c2.LastTick = 0; c2.Tick();
            Assert.AreEqual(1, c2.Upgrades.Level("blacksmith"));
        }

        [Test]
        public void VaultAccruesAndCaps()
        {
            double rate = c.Vault.RatePerHour; Assert.Greater(rate, 0);
            Advance(3600);
            Assert.AreEqual(rate, c.Vault.Pending, 1.0);
            Advance(100 * 3600);
            Assert.AreEqual(c.Vault.Capacity, c.Vault.Pending, 0.01);
            double before = c.Wallet.Get("gold"); double got = c.Vault.Collect();
            Assert.AreEqual(before + got, c.Wallet.Get("gold"), 0.01);
            Assert.AreEqual(0, c.Vault.Pending, 0.01);
        }

        [Test]
        public void BattleIsDeterministicAndStartersWinFirstStage()
        {
            var party = c.Roster.All.Take(1).ToList();
            var m = c.Content.Mission("m_r1_1");
            var a = BattleSim.Run(c.Missions.BuildSetup(m, party, 42));
            var b = BattleSim.Run(c.Missions.BuildSetup(m, party, 42));
            Assert.AreEqual(a.win, b.win); Assert.AreEqual(a.actions, b.actions); Assert.AreEqual(a.log.Count, b.log.Count);
            Assert.IsTrue(a.win, "Single starter should be able to beat stage 1");
        }

        [Test]
        public void CompleteMissionGivesRewardsAndProgressesQuests()
        {
            var party = c.Roster.All.Take(1).ToList();
            var m = c.Content.Mission("m_r1_1");
            var setup = c.Missions.BuildSetup(m, party, 7);
            var res = BattleSim.Run(setup);
            double g0 = c.Wallet.Get("gold");
            var o = c.Missions.Complete(m, party, setup, res);
            Assert.IsTrue(o.win);
            Assert.Greater(c.Wallet.Get("gold"), g0);
            Assert.AreEqual(1, c.Missions.Clears("m_r1_1"));
            Assert.GreaterOrEqual(c.Counters.Get("fame"), 3);
            c.Counters.Add("hero.hired", 0); c.Recruiter.Pool[0].price = 1; c.Wallet.Add("gold", 500);
            Assert.IsTrue(c.Recruiter.Hire(c.Recruiter.Pool[0]));
            c.LastTick = 0; c.Tick(); c.Quests.Tick(clock + 10);
            Assert.AreEqual(QuestSystem.Ready, c.Quests.State("q_m01"));
            Assert.IsTrue(c.Quests.Claim(c.Content.QuestMap["q_m01"]));
            Assert.AreEqual(QuestSystem.Done, c.Quests.State("q_m01"));
        }

        [Test]
        public void EquipUnequipAndSell()
        {
            var u = c.Roster.All[0];
            var sword = c.Inventory.AddItem("w_sword_2", 1, c.Rng, "uncommon");
            c.Inventory.Unequip(u, 0);
            double atk0 = c.Roster.Stat(u, "atk");
            Assert.IsTrue(c.Inventory.Equip(u, sword));
            Assert.Greater(c.Roster.Stat(u, "atk"), atk0);
            Assert.IsFalse(c.Inventory.Sell(sword), "equipped items cannot be sold");
            c.Inventory.Unequip(u, 0);
            Assert.AreEqual(atk0, c.Roster.Stat(u, "atk"), 0.01);
            Assert.IsTrue(c.Inventory.Sell(sword));
        }

        [Test]
        public void WrongWeaponFamilyCannotBeEquipped()
        {
            var u = c.Roster.All[0]; u.classId = "warrior";
            var bow = c.Inventory.AddItem("w_bow_2", 1, c.Rng, "common");
            Assert.IsFalse(c.Inventory.CanEquip(u, bow));
        }

        [Test]
        public void TrainingGrantsXp()
        {
            c.Counters.Add("missions.won", 4); c.Wallet.Add("gold", 5000); c.Wallet.Add("iron_ore", 0);
            c.Inventory.AddItem("iron_ore", 30, c.Rng);
            var tr = c.Content.Upgrade("training");
            Assert.IsTrue(c.Upgrades.Start(tr), c.Upgrades.Blocker(tr));
            Advance(600);
            Assert.AreEqual(1, c.Upgrades.Level("training"));
            var u = c.Roster.All[0]; int lv = u.level;
            var st = c.Stations.ByKind("train");
            Assert.IsTrue(c.Stations.Start(st, u), c.Stations.Blocker(st, u));
            Assert.IsTrue(c.Roster.IsBusy(u));
            Advance(3600);
            Assert.Greater(u.level, lv);
            Assert.IsFalse(c.Roster.IsBusy(u));
        }

        [Test]
        public void ExpeditionRoundTrip()
        {
            var party = c.Roster.All.Take(1).ToList();
            var m = c.Missions.Board[0];
            Assert.IsTrue(c.Missions.StartExpedition(m, party, out var why), why);
            Assert.IsTrue(c.Roster.IsBusy(party[0]));
            Advance(m.duration + 10);
            var t = c.Missions.Expeditions.First(); Assert.IsTrue(t.done);
            double g0 = c.Wallet.Get("gold");
            var r = c.Missions.ClaimExpedition(t);
            Assert.IsFalse(c.Roster.IsBusy(party[0]));
            Assert.GreaterOrEqual(c.Wallet.Get("gold"), g0);
        }

        [Test]
        public void RankPromotionRequiresFameAndExam()
        {
            Assert.IsFalse(c.Ranks.CanPromote);
            c.Counters.Add("fame", 120);
            Assert.IsTrue(c.Ranks.FameReached);
            Assert.IsFalse(c.Ranks.CanPromote, "exam not cleared yet");
            var exam = c.Content.Mission("exam_e");
            var party = new List<UnitInstance> { c.Roster.All[0] };
            // cheat strong hero to pass the exam
            party[0].level = 12; party[0].rarity = "legendary"; c.Roster.Dirty();
            var setup = c.Missions.BuildSetup(exam, party, 5);
            var res = BattleSim.Run(setup);
            c.Missions.Complete(exam, party, setup, res);
            Assert.AreEqual(res.win, c.Ranks.CanPromote);
        }

        [Test]
        public void ModifiersStackFromUpgradesAndRanks()
        {
            double hr0 = c.Mods.Add("income.gold_hr");
            c.Counters.Add("fame", 100);
            var fixedRank = typeof(RankSystem).GetField("index", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            fixedRank.SetValue(c.Ranks, 1); c.Mods.Dirty();
            Assert.Greater(c.Mods.Add("income.gold_hr"), hr0);
            Assert.AreEqual(1, c.Mods.Add("roster.cap"));
        }

        [Test]
        public void TowerMissionsScale()
        {
            var f1 = c.Missions.TowerMission(1); var f30 = c.Missions.TowerMission(30); var f5 = c.Missions.TowerMission(5);
            Assert.Greater(f30.level, f1.level);
            Assert.IsTrue(f5.boss);
            Assert.Greater(f30.rewards[0].n, f1.rewards[0].n);
            foreach (var w in f30.waves) Assert.Greater(w.enemies.Count, 0);
        }

        [Test]
        public void RecruitingChargesGoldAndRespectsRosterCap()
        {
            c.Wallet.Add("gold", 100000);
            int cap = c.Roster.Capacity;
            while (c.Roster.Count < cap) { var cand = c.Recruiter.Pool[0]; Assert.IsTrue(c.Recruiter.Hire(cand)); }
            Assert.IsFalse(c.Recruiter.Hire(c.Recruiter.Pool[0]), "roster full");
        }

        [Test]
        public void EveryStarterHasAFairOpeningAndEarnsItsFirstSkill()
        {
            foreach (string cls in new[] { "warrior", "knight", "archer", "mage", "cleric", "rogue" })
            {
                c.Rng = new Rng(2026); GuildGame.StartNew(c, "Opening", 0, cls);
                var party = c.Roster.All.ToList(); var mission = c.Content.Mission("m_r1_1");
                for (int seed = 1; seed <= 12; seed++)
                    Assert.IsTrue(BattleSim.Run(c.Missions.BuildSetup(mission, party, seed)).win, cls + " seed " + seed);
                var setup = c.Missions.BuildSetup(mission, party, 42);
                c.Missions.Complete(mission, party, setup, BattleSim.Run(setup));
                Assert.AreEqual(2, party[0].level, cls + " should gain its first level from the opening mission");
                Assert.GreaterOrEqual(c.Missions.MakeHero(party[0], 0, null).skills.Count, 2, cls);
            }
        }

        [Test]
        public void DoctrineTradeoffsChangeStatsAndActualTargetSelection()
        {
            var party = c.Roster.All.ToList(); var mission = c.Content.Mission("m_r1_1");
            var normal = c.Missions.BuildSetup(mission, party, 42);
            var assault = c.Missions.BuildSetup(mission, party, 42, BattleDoctrine.Assault);
            var guard = c.Missions.BuildSetup(mission, party, 42, BattleDoctrine.Guard);
            Assert.Greater(assault.heroes[0].atk, normal.heroes[0].atk);
            Assert.Less(assault.heroes[0].def, normal.heroes[0].def);
            Assert.Greater(guard.heroes[0].def, normal.heroes[0].def);
            Assert.Greater(guard.heroes[0].shield, 0);
            var weak = TestCombatant(1, 100, 5, 1); weak.hp = 25;
            var threat = TestCombatant(1, 200, 60, 1);
            var attacker = TestCombatant(0, 100, 10, 100);
            var result = BattleSim.Run(new BattleSetup { doctrine = BattleDoctrine.Guard, maxTurns = 1, heroes = new List<Combatant> { attacker }, waves = new List<List<Combatant>> { new List<Combatant> { weak, threat } } });
            Assert.AreEqual(threat.id, result.log.First(e => e.type == "act").hits[0].target);
        }

        static Combatant TestCombatant(int side, double hp, double atk, double spd)
        {
            return new Combatant { side = side, hp = hp, maxHp = hp, atk = atk, spd = spd, skills = new List<SkillDef> { new SkillDef { id = "test_attack", power = 1 } } };
        }

        [Test]
        public void ShieldBuffIsAppliedOnceAndKeepsItsDuration()
        {
            var hero = TestCombatant(0, 100, 10, 100);
            hero.skills.Add(new SkillDef { id = "test_shield", type = "buff", target = "self", status = "shield", statusV = 2, statusT = 3, cd = 4 });
            var enemy = TestCombatant(1, 1000, 1, 1);
            BattleSim.Run(new BattleSetup { maxTurns = 1, heroes = new List<Combatant> { hero }, waves = new List<List<Combatant>> { new List<Combatant> { enemy } } });
            Assert.AreEqual(20, hero.shield, "A single shield cast must not be doubled");
            Assert.AreEqual(3, hero.st.Single(x => x.type == "shield").turns);
        }

        [Test]
        public void OneTurnAttackBuffAffectsTheActionBeforeExpiring()
        {
            int Damage(bool buff)
            {
                var hero = TestCombatant(0, 100, 100, 100);
                if (buff) hero.st.Add(new Status { type = "atk_up", v = 1, turns = 1 });
                var enemy = TestCombatant(1, 10000, 1, 1);
                var result = BattleSim.Run(new BattleSetup { seed = 42, maxTurns = 1, heroes = new List<Combatant> { hero }, waves = new List<List<Combatant>> { new List<Combatant> { enemy } } });
                Assert.IsFalse(hero.Has("atk_up"));
                return result.log.First(e => e.type == "act").hits[0].dmg;
            }
            Assert.AreEqual(Damage(false) * 2, Damage(true), 1);
        }

        [Test]
        public void StunAndPoisonRespectTheirSpecifiedTurns()
        {
            var hero = TestCombatant(0, 1000, 1, 100);
            hero.st.Add(new Status { type = "stun", turns = 2 });
            hero.st.Add(new Status { type = "poison", v = 5, turns = 3 });
            var enemy = TestCombatant(1, 10000, 1, 1);
            var result = BattleSim.Run(new BattleSetup { maxTurns = 5, heroes = new List<Combatant> { hero }, waves = new List<List<Combatant>> { new List<Combatant> { enemy } } });
            Assert.AreEqual(2, result.log.Count(e => e.skill == "stunned"));
            Assert.AreEqual(3, result.log.Count(e => e.type == "tick"));
        }

        [Test]
        public void HealerPrioritizesAnEndangeredAllyOverBlessing()
        {
            var healer = TestCombatant(0, 100, 10, 100);
            healer.skills.Add(new SkillDef { id = "test_heal", type = "heal", target = "ally_lowest", power = 2, cd = 2 });
            healer.skills.Add(new SkillDef { id = "test_bless", type = "buff", target = "ally_all", status = "atk_up", statusV = 0.2, statusT = 3, cd = 4 });
            var ally = TestCombatant(0, 100, 1, 1); ally.hp = 10;
            var enemy = TestCombatant(1, 10000, 1, 1);
            var result = BattleSim.Run(new BattleSetup { maxTurns = 1, heroes = new List<Combatant> { healer, ally }, waves = new List<List<Combatant>> { new List<Combatant> { enemy } } });
            Assert.AreEqual("test_heal", result.log.First(e => e.type == "act").skill);
            Assert.Greater(ally.hp, 10);
        }

        [Test]
        public void FacilityAndResearchModifiersInvalidateCachedHeroStats()
        {
            var hero = c.Roster.All[0]; double before = c.Roster.Stat(hero, "atk");
            c.Upgrades.Load("{\"l\":[{\"k\":\"r_tactics\",\"v\":1}]}"); c.Mods.Dirty();
            Assert.Greater(c.Roster.Stat(hero, "atk"), before, "Research must take effect without equipping or leveling a hero");
        }

        [Test]
        public void PartiesRejectDuplicatesAndForeignHeroes()
        {
            var hero = c.Roster.All[0]; var mission = c.Content.Mission("m_r1_1");
            Assert.IsFalse(c.Missions.CanStartBattle(mission, new List<UnitInstance> { hero, hero }, out _));
            Assert.IsFalse(c.Missions.CanStartBattle(mission, new List<UnitInstance> { c.Recruiter.Pool[0] }, out _));
            Assert.IsFalse(c.Missions.StartExpedition(c.Missions.Board[0], new List<UnitInstance> { hero, hero }, out _));
        }

        [Test]
        public void ExpeditionsCannotPayBeforeReturningOrPayTwice()
        {
            var party = c.Roster.All.ToList(); var mission = c.Missions.Board[0];
            Assert.IsTrue(c.Missions.StartExpedition(mission, party, out var why), why);
            var task = c.Missions.Expeditions.Single(); double before = c.Wallet.Get("gold");
            Assert.IsNull(c.Missions.ClaimExpedition(task)); Assert.AreEqual(before, c.Wallet.Get("gold"));
            Advance(mission.duration + 10);
            Assert.IsNotNull(c.Missions.ClaimExpedition(task)); double paid = c.Wallet.Get("gold");
            Assert.IsNull(c.Missions.ClaimExpedition(task)); Assert.AreEqual(paid, c.Wallet.Get("gold"));
        }

        [Test]
        public void ExpeditionForecastIsLockedAtDepartureAndSurvivesSaving()
        {
            var party = c.Roster.All.ToList(); var mission = c.Missions.Board[0];
            double forecast = c.Missions.SuccessChance(mission, party);
            Assert.IsTrue(c.Missions.StartExpedition(mission, party, out _));
            Assert.AreEqual(forecast, c.Missions.Expeditions.Single().successChance);
            string save = c.SaveBlobs(out _); var loaded = GuildGame.Create();
            Assert.IsTrue(loaded.LoadBlobs(save));
            var task = loaded.Missions.Expeditions.Single();
            Assert.AreEqual(forecast, task.successChance);
            var hero = loaded.Roster.All[0]; hero.level = 30; loaded.Roster.Dirty();
            double arrivalChance = loaded.Missions.SuccessChance(JsonUtility.FromJson<MissionDef>(task.data), loaded.Roster.All);
            Assert.GreaterOrEqual(arrivalChance, forecast);
            clock += mission.duration + 10; loaded.LastTick = 0; loaded.Tick();
            var rng = new Rng(task.seed); double roll = rng.D();
            int expected = roll < forecast * 0.28 ? 2 : roll < forecast ? 1 : 0;
            Assert.AreEqual(expected, loaded.Missions.ClaimExpedition(task).outcome);
        }

        [Test]
        public void TavernRotatesExpiredCandidatesAndRejectsAlreadyHiredCandidates()
        {
            c.Wallet.Add("gold", 10000); var candidate = c.Recruiter.Pool[0];
            Assert.IsTrue(c.Recruiter.Hire(candidate)); double paid = c.Wallet.Get("gold");
            Assert.IsFalse(c.Recruiter.Hire(candidate)); Assert.AreEqual(paid, c.Wallet.Get("gold"));
            var old = c.Recruiter.Pool.Select(u => u.uid).ToList();
            Advance(c.Recruiter.Interval + 1);
            Assert.IsFalse(c.Recruiter.Pool.Any(u => old.Contains(u.uid)));
        }

        [Test]
        public void HealthyHeroesDoNotWastePotionsAndGearCannotBeSoldAsAStack()
        {
            var hero = c.Roster.All[0]; int potions = c.Inventory.Count("potion_s");
            Assert.IsFalse(c.Inventory.Use("potion_s", hero)); Assert.AreEqual(potions, c.Inventory.Count("potion_s"));
            double gold = c.Wallet.Get("gold");
            Assert.IsFalse(c.Inventory.SellStack("w_sword_1", 1)); Assert.AreEqual(gold, c.Wallet.Get("gold"));
        }

        [Test]
        public void FacilityUpgradeChargesOnceAndCompletesOnlyAtItsDeadline()
        {
            c.Wallet.Add("gold", 1000); c.Counters.Set("missions.won", 2);
            var tavern = c.Content.Upgrade("tavern"); var cost = c.Upgrades.Cost(tavern, 2);
            var before = Snapshot(c, cost);
            Assert.IsTrue(c.Upgrades.Start(tavern)); AssertResourceChange(c, before, cost, -1);
            var task = c.Upgrades.Pending("tavern"); double paid = c.Wallet.Get("gold");
            Assert.IsFalse(c.Upgrades.Start(tavern));
            Assert.IsFalse(c.Upgrades.Start(c.Content.Upgrade("blacksmith")), "The only builder is already occupied");
            Assert.AreEqual(paid, c.Wallet.Get("gold")); Assert.AreEqual(1, c.Upgrades.Level("tavern"));
            Advance(c.Timers.Remaining(task) - 0.5);
            Assert.AreEqual(1, c.Upgrades.Level("tavern")); Assert.AreEqual(0, c.Counters.Get("upgrade.done"));
            Advance(0.5);
            Assert.AreEqual(2, c.Upgrades.Level("tavern")); Assert.IsNull(c.Upgrades.Pending("tavern"));
            Assert.AreEqual(1, c.Counters.Get("upgrade.done"));
            Advance(60); Assert.AreEqual(1, c.Counters.Get("upgrade.done"));
            Assert.IsFalse(c.Upgrades.Start(tavern), "Rank F facilities stop at level 2");
            Assert.AreEqual(paid, c.Wallet.Get("gold"));
        }

        [Test]
        public void RejectedFacilityUpgradeDoesNotConsumeAnAffordablePartOfItsCost()
        {
            c.Counters.Set("missions.won", 2); c.Wallet.Add("gold", 1000);
            Assert.IsTrue(c.Inventory.Remove("iron_ore", c.Inventory.Count("iron_ore") - 4));
            var blacksmith = c.Content.Upgrade("blacksmith"); var cost = c.Upgrades.Cost(blacksmith, 1);
            var before = Snapshot(c, cost);
            Assert.IsFalse(c.Upgrades.Start(blacksmith));
            foreach (var resource in before) Assert.AreEqual(resource.Value, Resource(c, resource.Key), resource.Key);
            Assert.IsNull(c.Upgrades.Pending("blacksmith")); Assert.AreEqual(0, c.Counters.Get("upgrade.started"));
        }

        [Test]
        public void ResearchUsesItsOwnSlotAndUpdatesCachedHeroStatsOnCompletion()
        {
            // A built Library is the fixture; research and construction still use their real timers and costs.
            c.Upgrades.Load("{\"l\":[{\"k\":\"tavern\",\"v\":1},{\"k\":\"library\",\"v\":1}]}"); c.Mods.Dirty();
            c.Wallet.Add("gold", 10000); c.Inventory.AddItem("essence", 10, c.Rng);
            var hero = c.Roster.All[0]; double atk = c.Roster.Stat(hero, "atk");
            var tactics = c.Content.Upgrade("r_tactics"); var cost = c.Upgrades.Cost(tactics, 1); var before = Snapshot(c, cost);
            Assert.IsTrue(c.Upgrades.Start(tactics)); AssertResourceChange(c, before, cost, -1);
            Assert.IsTrue(c.Upgrades.Start(c.Content.Upgrade("tavern")), "A scholar must not occupy the builder slot");
            double paid = c.Wallet.Get("gold");
            Assert.IsFalse(c.Upgrades.Start(c.Content.Upgrade("r_fortitude")));
            Assert.AreEqual(paid, c.Wallet.Get("gold")); Assert.AreEqual(atk, c.Roster.Stat(hero, "atk"));
            Advance(c.Timers.Remaining(c.Upgrades.Pending("r_tactics")) + 0.01);
            Assert.AreEqual(1, c.Upgrades.Level("r_tactics")); Assert.Greater(c.Roster.Stat(hero, "atk"), atk);
            Assert.AreEqual(2, c.Counters.Get("upgrade.done")); Assert.AreEqual(1, c.Counters.Get("research.done"));
        }

        [Test]
        public void TimerSpeedupChargesOnceAndFinishesTheFacilityImmediately()
        {
            c.Wallet.Add("gold", 1000); c.Wallet.Add("gems", 100);
            Assert.IsTrue(c.Upgrades.Start(c.Content.Upgrade("tavern")));
            var task = c.Upgrades.Pending("tavern"); int price = c.Timers.SpeedCost(task); double gems = c.Wallet.Get("gems");
            Assert.Greater(price, 0); Assert.IsTrue(c.Timers.Speedup(task));
            Assert.AreEqual(gems - price, c.Wallet.Get("gems")); Assert.AreEqual(2, c.Upgrades.Level("tavern"));
            Assert.IsFalse(c.Timers.Speedup(task)); Assert.AreEqual(gems - price, c.Wallet.Get("gems"));
            Assert.AreEqual(1, c.Counters.Get("speedups")); Assert.AreEqual(1, c.Counters.Get("upgrade.done"));
        }

        [Test]
        public void TrainingReservesASlotAndRejectsRepeatedOrOverCapacityPayments()
        {
            BuildFacilities("training"); Assert.IsTrue(c.Recruiter.Hire(c.Recruiter.Pool[0]));
            var station = c.Stations.ByKind("train"); var hero = c.Roster.All[0]; var other = c.Roster.All[1];
            var cost = c.Stations.Cost(station, hero); var before = Snapshot(c, cost);
            double expectedXp = c.Roster.XpToNext(hero.level) * station.value * (1 + c.Mods.Pct("train.xp"));
            double xp = EarnedXp(c, hero);
            Assert.IsTrue(c.Stations.Start(station, hero)); AssertResourceChange(c, before, cost, -1);
            double paid = c.Wallet.Get("gold");
            Assert.IsFalse(c.Stations.Start(station, hero)); Assert.IsFalse(c.Stations.Start(station, other));
            Assert.AreEqual(paid, c.Wallet.Get("gold")); Assert.IsTrue(c.Roster.IsBusy(hero));
            Assert.IsFalse(c.Missions.CanStartBattle(c.Content.Mission("m_r1_1"), new List<UnitInstance> { hero }, out _));
            var task = c.Timers.UnitTask(hero.uid); Advance(c.Timers.Remaining(task) - 0.5);
            Assert.AreEqual(xp, EarnedXp(c, hero)); Advance(0.5);
            Assert.AreEqual(xp + expectedXp, EarnedXp(c, hero), 0.001);
            Assert.IsFalse(c.Roster.IsBusy(hero)); Assert.AreEqual(c.Stations.Slots(station), c.Stations.Free(station));
        }

        [Test]
        public void TrainingAndHealingFinishExactlyOnceAfterAnOfflineReload()
        {
            BuildFacilities("training", "infirmary"); Assert.IsTrue(c.Recruiter.Hire(c.Recruiter.Pool[0]));
            var trainee = c.Roster.All[0]; var patient = c.Roster.All[1]; c.Roster.SetHp(patient, c.Roster.MaxHp(patient) * 0.4);
            Assert.IsTrue(c.Stations.Start(c.Stations.ByKind("train"), trainee));
            double gold = c.Wallet.Get("gold"); Assert.IsTrue(c.Stations.Start(c.Stations.ByKind("heal"), patient));
            Assert.AreEqual(gold, c.Wallet.Get("gold"), "Infirmary healing is free");
            double end = c.Timers.Tasks.Max(task => task.end); string save = c.SaveBlobs(out _);
            clock = end + 1; var loaded = GuildGame.Create(); Assert.IsTrue(loaded.LoadBlobs(save));
            Assert.AreEqual(2, loaded.LastOffline.finishedTasks); loaded.Tick();
            var trained = loaded.Roster.Get(trainee.uid); var healed = loaded.Roster.Get(patient.uid);
            Assert.Greater(trained.level, trainee.level); Assert.AreEqual(1, loaded.Roster.HpFrac(healed), 0.001);
            Assert.IsFalse(loaded.Roster.IsBusy(trained)); Assert.IsFalse(loaded.Roster.IsBusy(healed));
            double xp = EarnedXp(loaded, trained); clock += 1000; loaded.LastTick = 0; loaded.Tick();
            Assert.AreEqual(xp, EarnedXp(loaded, trained));
            Assert.AreEqual(1, loaded.Counters.Get("train.done")); Assert.AreEqual(1, loaded.Counters.Get("heal.done"));
        }

        [Test]
        public void HealthyHealingAndCappedTrainingDoNotCreateTasksOrChargeGold()
        {
            BuildFacilities("training", "infirmary"); var hero = c.Roster.All[0]; double gold = c.Wallet.Get("gold");
            Assert.IsFalse(c.Stations.Start(c.Stations.ByKind("heal"), hero));
            hero.level = c.Roster.LevelCap(hero); c.Roster.Dirty();
            Assert.IsFalse(c.Stations.Start(c.Stations.ByKind("train"), hero));
            Assert.AreEqual(gold, c.Wallet.Get("gold")); Assert.IsEmpty(c.Timers.Tasks);
        }

        [Test]
        public void CraftingChargesOnceAndProducesItsQuantityOnceAfterOfflineReload()
        {
            BuildFacilities("blacksmith"); var recipe = c.Content.RecipeMap["rc_potion_s"];
            Assert.IsFalse(c.Crafting.Craft(c.Content.RecipeMap["rc_w_sword_2"]), "Tier 2 sword needs a stronger Blacksmith");
            int items = c.Inventory.Count(recipe.result); var before = Snapshot(c, recipe.cost);
            Assert.IsTrue(c.Crafting.Craft(recipe)); AssertResourceChange(c, before, recipe.cost, -1);
            double paid = c.Wallet.Get("gold"); Assert.IsFalse(c.Crafting.Craft(recipe)); Assert.AreEqual(paid, c.Wallet.Get("gold"));
            var task = c.Crafting.Active.Single(); Advance(c.Timers.Remaining(task) - 0.5);
            Assert.AreEqual(items, c.Inventory.Count(recipe.result)); string save = c.SaveBlobs(out _);
            clock = task.end + 1; var loaded = GuildGame.Create(); Assert.IsTrue(loaded.LoadBlobs(save));
            Assert.AreEqual(1, loaded.LastOffline.finishedTasks); loaded.Tick();
            Assert.AreEqual(items + recipe.n, loaded.Inventory.Count(recipe.result)); Assert.IsEmpty(loaded.Crafting.Active);
            clock += 1000; loaded.LastTick = 0; loaded.Tick();
            Assert.AreEqual(items + recipe.n, loaded.Inventory.Count(recipe.result));
            Assert.AreEqual(recipe.n, loaded.Counters.Get("crafted")); Assert.AreEqual(paid, loaded.Wallet.Get("gold"));
        }

        [Test]
        public void EnhancementChargesItsActualCostImprovesEquippedStatsAndStopsAtCap()
        {
            BuildFacilities("blacksmith"); var hero = c.Roster.All[0]; var gear = c.Inventory.AddItem("w_sword_2", 1, c.Rng, "common");
            Assert.IsTrue(c.Inventory.Equip(hero, gear)); double atk = c.Roster.Stat(hero, "atk");
            int cap = c.Crafting.MaxEnh; Assert.LessOrEqual(cap, 4, "Early enhancements are guaranteed");
            for (int level = 1; level <= cap; level++)
            {
                var cost = c.Crafting.EnhCost(gear); var before = Snapshot(c, cost);
                Assert.AreEqual(1, c.Crafting.Enhance(gear)); Assert.AreEqual(level, gear.enh);
                AssertResourceChange(c, before, cost, -1);
            }
            Assert.Greater(c.Roster.Stat(hero, "atk"), atk);
            double gold = c.Wallet.Get("gold"); int ore = c.Inventory.Count("iron_ore");
            Assert.AreEqual(-1, c.Crafting.Enhance(gear)); Assert.AreEqual(gold, c.Wallet.Get("gold")); Assert.AreEqual(ore, c.Inventory.Count("iron_ore"));
            Assert.AreEqual(cap, c.Counters.Get("enhance.success"));
        }

        [TestCase("sale")]
        [TestCase("salvage")]
        public void RemovedGearCannotPayTwiceOrConsumeEnhancementResources(string disposal)
        {
            c.Wallet.Add("gold", 1000); var gear = c.Inventory.AddItem("w_sword_2", 1, c.Rng, "rare");
            gear.locked = true; Assert.IsFalse(c.Inventory.Sell(gear)); Assert.IsFalse(c.Crafting.Salvage(gear));
            gear.locked = false; Assert.IsTrue(c.Inventory.Equip(c.Roster.All[0], gear));
            Assert.IsFalse(c.Inventory.Sell(gear)); Assert.IsFalse(c.Crafting.Salvage(gear)); c.Inventory.Unequip(c.Roster.All[0], 0);
            double before = c.Wallet.Get("gold"); var yield = c.Crafting.SalvageYield(gear); var materials = Snapshot(c, yield);
            if (disposal == "sale")
            {
                double value = c.Inventory.SellValue(gear); Assert.IsTrue(c.Inventory.Sell(gear));
                Assert.AreEqual(before + value, c.Wallet.Get("gold"));
            }
            else { Assert.IsTrue(c.Crafting.Salvage(gear)); AssertResourceChange(c, materials, yield, 1); }
            Assert.IsNull(c.Inventory.Get(gear.uid));
            double gold = c.Wallet.Get("gold"); int ore = c.Inventory.Count("iron_ore"), essence = c.Inventory.Count("essence");
            Assert.IsFalse(c.Inventory.Sell(gear)); Assert.IsFalse(c.Crafting.Salvage(gear)); Assert.AreEqual(-1, c.Crafting.Enhance(gear));
            Assert.AreEqual(gold, c.Wallet.Get("gold")); Assert.AreEqual(ore, c.Inventory.Count("iron_ore")); Assert.AreEqual(essence, c.Inventory.Count("essence"));
        }

        [TestCase("gold")]
        [TestCase("gems")]
        public void MarketPurchasesKeepSoldStateAcrossSaveAndRejectExpiredOffers(string currency)
        {
            c.Tick(); c.Wallet.Add("gold", 100000); c.Wallet.Add("gems", 1000);
            var offer = c.Market.Offers.First(o => o.cur == currency); double money = c.Wallet.Get(currency); int count = c.Inventory.Count(offer.itemId);
            Assert.IsTrue(c.Market.Buy(offer)); Assert.AreEqual(money - offer.price, c.Wallet.Get(currency));
            Assert.AreEqual(count + offer.qty, c.Inventory.Count(offer.itemId)); Assert.IsFalse(c.Market.Buy(offer));
            var loaded = GuildGame.Create(); Assert.IsTrue(loaded.LoadBlobs(c.SaveBlobs(out _))); loaded.Tick();
            var sold = loaded.Market.Offers.Single(o => o.sold); double paid = loaded.Wallet.Get(currency);
            Assert.IsFalse(loaded.Market.Buy(sold)); Assert.AreEqual(paid, loaded.Wallet.Get(currency));
            var stale = loaded.Market.Offers.First(o => !o.sold); double funds = loaded.Wallet.Get(stale.cur);
            clock += 86400; loaded.LastTick = 0; loaded.Tick();
            Assert.IsFalse(loaded.Market.Offers.Contains(stale)); Assert.IsFalse(loaded.Market.Buy(stale));
            Assert.AreEqual(funds, loaded.Wallet.Get(stale.cur)); Assert.AreEqual(1, loaded.Counters.Get("market.bought"));
        }

        [Test]
        public void MarketFullBagRejectsGearWithoutChargingAndStillAllowsSupplies()
        {
            c.Wallet.Add("gold", 1000);
            c.Market.Load("{\"day\":" + Util.Day(clock) + ",\"offers\":[{\"itemId\":\"w_sword_1\",\"rarity\":\"common\",\"price\":100,\"cur\":\"gold\",\"qty\":1},{\"itemId\":\"potion_s\",\"price\":50,\"cur\":\"gold\",\"qty\":2}]}");
            c.Inventory.AddItem("w_sword_1", c.Inventory.Capacity - c.Inventory.Used, c.Rng, "common");
            double gold = c.Wallet.Get("gold"); int items = c.Inventory.Used, potions = c.Inventory.Count("potion_s");
            var gear = c.Market.Offers[0]; Assert.IsFalse(c.Market.Buy(gear)); Assert.IsFalse(gear.sold);
            Assert.AreEqual(gold, c.Wallet.Get("gold")); Assert.AreEqual(items, c.Inventory.Used);
            Assert.IsTrue(c.Market.Buy(c.Market.Offers[1])); Assert.AreEqual(potions + 2, c.Inventory.Count("potion_s"));
            Assert.AreEqual(gold - 50, c.Wallet.Get("gold"));
        }

        [Test]
        public void DailyLoginPaysOnceAcrossReloadAndResetsTheStreakAfterAMissedDay()
        {
            c.Tick(); Assert.AreEqual(1, c.Daily.Streak); var reward = c.Daily.TodayReward; var before = Snapshot(c, reward);
            Assert.IsTrue(c.Daily.Claim()); AssertResourceChange(c, before, reward, 1); Assert.IsFalse(c.Daily.Claim());
            var loaded = GuildGame.Create(); Assert.IsTrue(loaded.LoadBlobs(c.SaveBlobs(out _))); loaded.Tick();
            Assert.IsTrue(loaded.Daily.Claimed); Assert.IsFalse(loaded.Daily.Claim()); Assert.AreEqual(1, loaded.Counters.Get("login.claims"));
            clock = (Util.Day(clock) + 1) * 86400 + 1; loaded.LastTick = 0; loaded.Tick();
            Assert.AreEqual(2, loaded.Daily.Streak); Assert.IsFalse(loaded.Daily.Claimed);
            reward = loaded.Daily.TodayReward; before = Snapshot(loaded, reward);
            Assert.IsTrue(loaded.Daily.Claim()); AssertResourceChange(loaded, before, reward, 1);
            clock += 2 * 86400; loaded.LastTick = 0; loaded.Tick();
            Assert.AreEqual(1, loaded.Daily.Streak); Assert.IsFalse(loaded.Daily.Claimed);
        }

        [Test]
        public void DailyQuestsRespectFacilityRequirementsAndResetTheirProgressBaselines()
        {
            c.Tick();
            for (int seed = 1; seed <= 24; seed++)
            {
                c.Quests.RollDailies(c.Daily.DailyCount, seed); var selected = c.Quests.Open("daily").ToList();
                Assert.AreEqual(c.Daily.DailyCount, selected.Count);
                Assert.AreEqual(selected.Count, selected.Select(q => q.id).Distinct().Count());
                Assert.IsTrue(selected.All(q => c.MeetsAll(q.req)), "Unavailable facility daily selected with seed " + seed);
            }
            c.Quests.RollDailies(c.Content.Quests.Count(q => q.type == "daily"), 1); var quest = c.Content.QuestMap["d_win3"];
            c.Counters.Add("missions.won", 3); c.Quests.Tick(clock); Assert.AreEqual(QuestSystem.Ready, c.Quests.State(quest.id));
            var before = Snapshot(c, quest.rewards); Assert.IsTrue(c.Quests.Claim(quest)); AssertResourceChange(c, before, quest.rewards, 1);
            Assert.IsFalse(c.Quests.Claim(quest));
            Advance(86400); c.Quests.RollDailies(c.Content.Quests.Count(q => q.type == "daily"), 2);
            Assert.AreEqual(QuestSystem.Active, c.Quests.State(quest.id)); Assert.AreEqual(0, c.Quests.Progress(quest, quest.goals[0]));
            c.Counters.Add("missions.won", 3); c.Quests.Tick(clock + 2);
            Assert.AreEqual(QuestSystem.Ready, c.Quests.State(quest.id));
        }

        [Test]
        public void EventsKeepUnaffordableChoicesPendingAndResolvePaidRewardsOnlyOnce()
        {
            var ev = c.Content.EventMap["ev_merchant"]; c.Events.Load("{\"pending\":[\"ev_merchant\"],\"next\":1800000900}");
            Assert.IsTrue(c.Wallet.Spend("gold", c.Wallet.Get("gold"))); Assert.IsNull(c.Events.Resolve(ev, 0));
            Assert.Contains(ev.id, c.Events.Pending.ToList()); Assert.AreEqual(0, c.Counters.Get("events.done"));
            c.Wallet.Add("gold", 100); var loaded = GuildGame.Create(); Assert.IsTrue(loaded.LoadBlobs(c.SaveBlobs(out _)));
            double gold = loaded.Wallet.Get("gold"); int potions = loaded.Inventory.Count("potion_m"), tomes = loaded.Inventory.Count("tome_s");
            Assert.IsNull(loaded.Events.Resolve(ev, -1)); Assert.IsNull(loaded.Events.Resolve(ev, ev.choices.Count));
            Assert.IsNotNull(loaded.Events.Resolve(ev, 0)); Assert.AreEqual(gold - 100, loaded.Wallet.Get("gold"));
            Assert.AreEqual(potions + 2, loaded.Inventory.Count("potion_m")); Assert.AreEqual(tomes + 1, loaded.Inventory.Count("tome_s"));
            Assert.IsNull(loaded.Events.Resolve(ev, 0)); Assert.AreEqual(1, loaded.Counters.Get("events.done"));
            var again = GuildGame.Create(); Assert.IsTrue(again.LoadBlobs(loaded.SaveBlobs(out _)));
            Assert.IsNull(again.Events.Resolve(ev, 0)); Assert.AreEqual(potions + 2, again.Inventory.Count("potion_m"));
        }

        [Test]
        public void EveryEventHasAnAvailableFallbackChoiceSoItsPopupCannotStrandThePlayer()
        {
            foreach (var ev in c.Content.Events)
                Assert.IsTrue(ev.choices.Any(choice => choice.req.Count == 0 && choice.cost.All(cost => cost.n <= 0)
                    && choice.outcomes.Count > 0), ev.id + " must have a free choice with no extra prerequisites");
        }

        [Test]
        public void EarnedRankPromotionUpdatesCapacityOnceAndPersistsWithItsExam()
        {
            int capacity = c.Roster.Capacity; double income = c.Vault.RatePerHour;
            c.Counters.Set("fame", c.Ranks.Next.fame); Assert.IsFalse(c.Ranks.Promote());
            WinWithStrongHero(c.Content.Mission(c.Ranks.ExamId));
            Assert.IsTrue(c.Ranks.Promote()); Assert.AreEqual(1, c.Ranks.Index); Assert.Greater(c.Roster.Capacity, capacity); Assert.Greater(c.Vault.RatePerHour, income);
            Assert.IsFalse(c.Ranks.Promote(), "The next rank still needs its own fame and exam"); Assert.AreEqual(1, c.Counters.Get("rank.promotions"));
            var loaded = GuildGame.Create(); Assert.IsTrue(loaded.LoadBlobs(c.SaveBlobs(out _)));
            Assert.AreEqual(c.Ranks.Index, loaded.Ranks.Index); Assert.AreEqual(c.Roster.Capacity, loaded.Roster.Capacity);
            Assert.IsTrue(loaded.Missions.Cleared("exam_e")); Assert.AreEqual(c.Vault.RatePerHour, loaded.Vault.RatePerHour);
        }

        [Test]
        public void TowerBestCannotRegressAndSurvivesSaving()
        {
            WinWithStrongHero(c.Missions.TowerMission(5)); Assert.AreEqual(5, c.Missions.TowerBest);
            WinWithStrongHero(c.Missions.TowerMission(1)); Assert.AreEqual(5, c.Missions.TowerBest); Assert.AreEqual(5, c.Counters.Get("tower.best"));
            var loaded = GuildGame.Create(); Assert.IsTrue(loaded.LoadBlobs(c.SaveBlobs(out _)));
            Assert.AreEqual(5, loaded.Missions.TowerBest); Assert.AreEqual(1, loaded.Missions.Clears("tower_5"));
            Assert.AreEqual(c.Missions.Stars("tower_5"), loaded.Missions.Stars("tower_5"));
        }

        [Test]
        public void FirstClearAndThreeStarBonusesDoNotPayAgainOnReplay()
        {
            var mission = c.Content.Mission("m_r1_1"); var first = WinWithStrongHero(mission);
            Assert.IsTrue(first.first); Assert.AreEqual(3, first.stars);
            double gems = c.Wallet.Get("gems"), stars = c.Counters.Get("missions.stars");
            var replay = WinWithStrongHero(mission); Assert.IsFalse(replay.first); Assert.AreEqual(3, replay.stars);
            Assert.AreEqual(gems, c.Wallet.Get("gems")); Assert.AreEqual(stars, c.Counters.Get("missions.stars"));
            Assert.AreEqual(2, c.Missions.Clears(mission.id));
        }

        [Test]
        public void OfflineExpeditionDefersPayoutUntilClaimAndReleasesItsPartyOnlyOnce()
        {
            var hero = c.Roster.All[0]; var mission = c.Missions.Board[0];
            Assert.IsTrue(c.Missions.StartExpedition(mission, new List<UnitInstance> { hero }, out var why), why);
            var task = c.Missions.Expeditions.Single(); string save = c.SaveBlobs(out _); double gold = c.Wallet.Get("gold");
            clock = task.end + 1; var loaded = GuildGame.Create(); Assert.IsTrue(loaded.LoadBlobs(save));
            Assert.AreEqual(1, loaded.LastOffline.expeditionsReady); Assert.AreEqual(0, loaded.LastOffline.finishedTasks);
            loaded.Tick(); var ready = loaded.Missions.Expeditions.Single(); var returning = loaded.Roster.Get(hero.uid);
            Assert.IsTrue(ready.done); Assert.IsTrue(loaded.Roster.IsBusy(returning)); Assert.AreEqual(gold, loaded.Wallet.Get("gold"));
            var result = loaded.Missions.ClaimExpedition(ready); Assert.NotNull(result);
            double payout = result.res.Where(r => r.id == "gold").Sum(r => r.n)
                + result.drops.Where(d => d.res != null && d.res.id == "gold").Sum(d => d.res.n);
            Assert.AreEqual(gold + payout, loaded.Wallet.Get("gold")); Assert.IsFalse(loaded.Roster.IsBusy(returning));
            Assert.IsNull(loaded.Missions.ClaimExpedition(ready)); Assert.AreEqual(gold + payout, loaded.Wallet.Get("gold"));
            var again = GuildGame.Create(); Assert.IsTrue(again.LoadBlobs(loaded.SaveBlobs(out _)));
            Assert.IsEmpty(again.Missions.Expeditions); Assert.IsFalse(again.Roster.IsBusy(again.Roster.Get(hero.uid)));
        }

        [Test]
        public void DismissalRejectsLastNullAndForeignHeroesWithoutChangingTheRoster()
        {
            var hero = c.Roster.All[0]; var foreign = c.Recruiter.Pool[0];
            Assert.IsFalse(c.Roster.CanDismiss(hero)); Assert.IsNotNull(c.Roster.DismissBlocker(hero));
            Assert.IsFalse(c.Roster.Dismiss(hero)); Assert.IsFalse(c.Roster.Dismiss(null)); Assert.IsFalse(c.Roster.Dismiss(foreign));
            // A stale object with a member's UID must not remove the current saved instance.
            Assert.IsFalse(c.Roster.Dismiss(new UnitInstance { uid = hero.uid }));
            Assert.AreEqual(1, c.Roster.Count); Assert.AreSame(hero, c.Roster.Get(hero.uid));
        }

        [TestCase("train")]
        [TestCase("heal")]
        [TestCase("expedition")]
        public void BusyHeroesCannotBeDismissedAndRemainAttachedToTheirTask(string kind)
        {
            BuildFacilities("training", "infirmary"); Assert.IsTrue(c.Recruiter.Hire(c.Recruiter.Pool[0]));
            var hero = c.Roster.All[0];
            if (kind == "expedition")
                Assert.IsTrue(c.Missions.StartExpedition(c.Missions.Board[0], new List<UnitInstance> { hero }, out var why), why);
            else
            {
                if (kind == "heal") c.Roster.SetHp(hero, c.Roster.MaxHp(hero) * 0.4);
                Assert.IsTrue(c.Stations.Start(c.Stations.ByKind(kind), hero));
            }
            var task = c.Timers.UnitTask(hero.uid); Assert.NotNull(task); Assert.IsFalse(c.Roster.CanDismiss(hero));
            Assert.IsFalse(c.Roster.Dismiss(hero)); Assert.AreSame(hero, c.Roster.Get(task.units.Single()));
            Advance(c.Timers.Remaining(task) + 0.01);
            if (kind == "expedition")
            {
                Assert.IsFalse(c.Roster.Dismiss(hero), "Returned expedition heroes stay busy until their return is claimed");
                Assert.NotNull(c.Missions.ClaimExpedition(task));
            }
            Assert.IsTrue(c.Roster.Dismiss(hero)); Assert.IsNull(c.Roster.Get(hero.uid)); Assert.IsEmpty(c.Timers.Tasks);
        }

        [Test]
        public void SuccessfulDismissalReturnsEquipmentToTheBagAndCannotRepeat()
        {
            c.Wallet.Add("gold", 10000); Assert.IsTrue(c.Recruiter.Hire(c.Recruiter.Pool[0]));
            var hero = c.Roster.All[0]; var equipment = hero.equip.Where(uid => !string.IsNullOrEmpty(uid)).Select(c.Inventory.Get).ToList();
            Assert.IsNotEmpty(equipment); int count = c.Inventory.Used; double gold = c.Wallet.Get("gold");
            Assert.IsTrue(c.Roster.Dismiss(hero)); Assert.AreEqual(1, c.Roster.Count); Assert.AreEqual(count, c.Inventory.Used);
            foreach (var item in equipment) { Assert.AreSame(item, c.Inventory.Get(item.uid)); Assert.IsTrue(string.IsNullOrEmpty(item.owner)); }
            Assert.IsTrue(hero.equip.All(string.IsNullOrEmpty)); Assert.IsFalse(c.Roster.Dismiss(hero)); Assert.AreEqual(gold, c.Wallet.Get("gold"));
            var loaded = GuildGame.Create(); Assert.IsTrue(loaded.LoadBlobs(c.SaveBlobs(out _))); Assert.IsNull(loaded.Roster.Get(hero.uid));
            foreach (var item in equipment) Assert.IsTrue(string.IsNullOrEmpty(loaded.Inventory.Get(item.uid).owner));
        }

        [Test]
        public void HeroPromotionRejectsNullForeignAndBusyHeroesWithoutCharging()
        {
            BuildFacilities("training"); c.Inventory.AddItem("essence", 20, c.Rng);
            var hero = c.Roster.All[0]; hero.level = c.Roster.LevelCap(hero) - 1; c.Roster.Dirty();
            Assert.IsTrue(c.Stations.Start(c.Stations.ByKind("train"), hero));
            hero.level = c.Roster.LevelCap(hero); c.Roster.Dirty();
            var candidate = c.Recruiter.Pool[0]; candidate.level = c.Roster.LevelCap(candidate);
            double gold = c.Wallet.Get("gold"); int essence = c.Inventory.Count("essence");
            Assert.IsFalse(c.Roster.CanPromote(null)); Assert.IsFalse(c.Roster.Promote(null));
            Assert.IsFalse(c.Roster.CanPromote(candidate)); Assert.IsFalse(c.Roster.Promote(candidate));
            Assert.IsFalse(c.Roster.Promote(new UnitInstance { uid = hero.uid, level = hero.level }));
            Assert.IsFalse(c.Roster.CanPromote(hero)); Assert.IsFalse(c.Roster.Promote(hero));
            Assert.AreEqual(gold, c.Wallet.Get("gold")); Assert.AreEqual(essence, c.Inventory.Count("essence")); Assert.AreEqual(0, hero.promo);
            Advance(c.Timers.Remaining(c.Timers.UnitTask(hero.uid)) + 0.01);
            var cost = c.Roster.PromoCost(hero); var before = Snapshot(c, cost); double health = c.Roster.HpFrac(hero);
            Assert.IsTrue(c.Roster.Promote(hero)); AssertResourceChange(c, before, cost, -1);
            Assert.AreEqual(1, hero.promo); Assert.AreEqual(health, c.Roster.HpFrac(hero), 0.001);
            double paid = c.Wallet.Get("gold"); Assert.IsFalse(c.Roster.Promote(hero)); Assert.AreEqual(paid, c.Wallet.Get("gold"));
        }
    }
}
