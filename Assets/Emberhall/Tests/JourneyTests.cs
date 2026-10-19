using System.Collections.Generic;
using ManagerKit;
using NUnit.Framework;
using UnityEngine;

namespace Emberhall.Tests
{
    public sealed class JourneyTests
    {
        GameContext c; Journey journey; double clock;
        [SetUp] public void Setup()
        {
            clock = 1_800_000_000; Util.ClockOverride = () => clock;
            c = GuildGame.Create(); GuildGame.StartNew(c, "Journey", 0, "warrior"); journey = c.Get<Journey>();
        }
        [TearDown] public void Cleanup() { Util.ClockOverride = null; }
        MissionDef Trail => c.Content.Mission("m_r1_4");
        void Spend(int runs)
        {
            for (int i = 0; i < runs; i++) { Assert.IsTrue(journey.Enter(Trail, out _)); journey.Result(Trail, true); }
        }
        [Test] public void OpeningFirstClearsAreFreeButReplaysUseStamina()
        {
            var first = c.Content.Mission("m_r1_1"); Assert.AreEqual(0, journey.Cost(first));
            Assert.IsTrue(journey.Enter(first, out _)); Assert.AreEqual(30, journey.Energy); journey.Result(first, false);
            Assert.AreEqual(0, journey.Cost(first));
            c.Missions.Load("{\"p\":[{\"id\":\"m_r1_1\",\"clears\":1,\"stars\":2}]}");
            Assert.AreEqual(2, journey.Cost(first));
            Assert.AreEqual(0, journey.Cost(c.Content.Mission("exam_e")));
            Assert.AreEqual(3, journey.Cost(c.Missions.TowerMission(1)));
        }
        [Test] public void PreviewingAndSelectingHeroesNeverSpendStamina()
        {
            var mission = c.Content.Mission("m_r1_1"); var hero = c.Roster.All[0];
            for (int i = 0; i < 3; i++) { c.Missions.RecommendedParty(mission); c.Missions.CanStartBattle(mission, new List<UnitInstance> { hero }, out _); }
            Assert.AreEqual(30, journey.Energy); Assert.IsFalse(journey.InBattle);
        }
        [Test] public void ARunChargesOnceAndRejectsAnOverlappingStart()
        {
            Assert.IsTrue(journey.Enter(Trail, out _)); Assert.IsFalse(journey.Enter(Trail, out _));
            Assert.AreEqual(28, journey.Energy); journey.Result(Trail, true); Assert.IsFalse(journey.InBattle);
            journey.Result(Trail, false); Assert.AreEqual(28, journey.Energy);
        }
        [Test] public void DefeatCostsOneAndDuplicateSettlementDoesNotRefundTwice()
        {
            Assert.IsTrue(journey.Enter(Trail, out _)); journey.Result(Trail, false); Assert.AreEqual(29, journey.Energy);
            journey.Result(Trail, false); Assert.AreEqual(29, journey.Energy);
        }
        [Test] public void StaminaRecoversOfflineWithoutBankingTimeAtTheCap()
        {
            clock += 3600; Assert.AreEqual(30, journey.Energy); Spend(2); Assert.AreEqual(26, journey.Energy);
            clock += 299; Assert.AreEqual(26, journey.Energy); clock++; Assert.AreEqual(27, journey.Energy);
            clock += 6000; Assert.AreEqual(30, journey.Energy);
            Spend(1); Assert.AreEqual(28, journey.Energy); Assert.Greater(journey.NextRecovery, 299);
        }
        [Test] public void ExhaustionBlocksEntryButLeavesFreeGuildActivitiesAvailable()
        {
            Spend(15); Assert.AreEqual(0, journey.Energy); Assert.IsFalse(journey.Enter(Trail, out var why)); Assert.IsNotEmpty(why);
            Assert.AreEqual(0, journey.Cost(c.Content.Mission("exam_e")));
            clock += 600; Assert.IsTrue(journey.Enter(Trail, out _)); Assert.AreEqual(0, journey.Energy);
        }
        [Test] public void CampRationIsFreeDailyAndCannotOverflowOrBeClaimedTwice()
        {
            Assert.IsFalse(journey.RestAtCamp()); Spend(5); Assert.AreEqual(20, journey.Energy);
            Assert.IsTrue(journey.RestAtCamp()); Assert.AreEqual(30, journey.Energy); Spend(5); Assert.IsFalse(journey.RestAtCamp());
            string save = c.SaveBlobs(out _); c.LoadBlobs(save); Assert.IsTrue(journey.CampUsed); Assert.IsFalse(journey.RestAtCamp());
            clock += 86400; Spend(5); Assert.IsTrue(journey.RestAtCamp());
        }
        [Test] public void RestartResolvesAnInterruptedRunOnceAndOldSavesStartFull()
        {
            Spend(2); Assert.IsTrue(journey.Enter(Trail, out _)); string json = c.SaveBlobs(out _);
            Assert.IsTrue(c.LoadBlobs(json)); Assert.AreEqual(25, journey.Energy); Assert.IsFalse(journey.InBattle);
            string settled = c.SaveBlobs(out _); c.LoadBlobs(settled); Assert.AreEqual(25, journey.Energy);
            var envelope = JsonUtility.FromJson<SaveEnvelope>(json); envelope.blobs.RemoveAll(b => b.k == journey.SaveKey);
            c.LoadBlobs(JsonUtility.ToJson(envelope)); Assert.AreEqual(30, journey.Energy);
        }
        [Test] public void BossMasteryRequiresEightTrailStarsButKeepsClearedBossesPlayable()
        {
            var boss = c.Content.Mission("m_r1_5"); Assert.IsNotNull(journey.MasteryBlocker(boss));
            c.Missions.Load("{\"p\":[{\"id\":\"m_r1_1\",\"clears\":1,\"stars\":3},{\"id\":\"m_r1_2\",\"clears\":1,\"stars\":3},{\"id\":\"m_r1_3\",\"clears\":1,\"stars\":2}]}");
            Assert.AreEqual(8, journey.RegionStars("r1")); Assert.IsNull(journey.MasteryBlocker(boss));
            c.Missions.Load("{\"p\":[{\"id\":\"m_r1_5\",\"clears\":1,\"stars\":1}]}"); Assert.IsNull(journey.MasteryBlocker(boss));
        }
        [Test] public void RankAloneCannotSkipThePreviousRegionalBoss()
        {
            c.Counters.Max("fame", 100000); // A high-rank imported guild still follows the road's story.
            c.Ranks.Load("{\"i\":1}");
            var region = c.Content.RegionMap["r2"];
            Assert.IsTrue(region.req.Exists(r => r.stat == "mission.clear.m_r1_5"));
            Assert.IsFalse(c.MeetsAll(region.req));
        }
        [Test] public void EveryPedestrianSegmentStaysOutsideTheFountainBasin()
        {
            var center = new Vector2(540, 800); var radius = new Vector2(190, 116);
            for (int i = 0; i < HallPath.Nodes; i++)
                foreach (int direction in new[] { -1, 1 })
                {
                    var a = HallPath.Point(center, radius, i); var b = HallPath.Point(center, radius, HallPath.Next(i, direction));
                    for (int step = 0; step <= 20; step++)
                    {
                        var p = Vector2.Lerp(a, b, step / 20f) - center;
                        Assert.Greater(p.x * p.x / (145 * 145) + p.y * p.y / (80 * 80), 1, $"Node {i}, direction {direction}, step {step}");
                    }
                }
        }
    }
}
