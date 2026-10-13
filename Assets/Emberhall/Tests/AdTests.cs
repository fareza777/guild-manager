using ManagerKit;
using NUnit.Framework;

namespace Emberhall.Tests
{
    public sealed class AdTests
    {
        GameContext c;
        AdRewards rewards;
        double clock;
        [SetUp] public void Setup()
        {
            clock = 1_800_000_000; Util.ClockOverride = () => clock;
            c = GuildGame.Create(); GuildGame.StartNew(c, "Ad tests", 0, "warrior"); rewards = c.Get<AdRewards>();
        }
        [TearDown] public void Cleanup() { Util.ClockOverride = null; }
        [Test] public void OfferingOrCancellingAVideoDoesNotGrantResources()
        {
            double gold = c.Wallet.Get("gold"), gems = c.Wallet.Get("gems");
            var ticket = rewards.SupplyTicket();
            Assert.AreEqual(gold, c.Wallet.Get("gold")); Assert.AreEqual(gems, c.Wallet.Get("gems"));
            rewards.Cancel(ticket);
            Assert.IsFalse(rewards.GrantEarned(ticket)); Assert.AreEqual(0, rewards.SuppliesToday);
            Assert.AreEqual(gold, c.Wallet.Get("gold"));
        }
        [Test] public void EarnedSupplyCallbackCreditsExactlyOnce()
        {
            double gold = c.Wallet.Get("gold"), gems = c.Wallet.Get("gems"); var ticket = rewards.SupplyTicket();
            Assert.IsTrue(rewards.GrantEarned(ticket)); Assert.IsFalse(rewards.GrantEarned(ticket));
            Assert.AreEqual(gold + 150, c.Wallet.Get("gold")); Assert.AreEqual(gems + 2, c.Wallet.Get("gems"));
            Assert.AreEqual(1, rewards.SuppliesToday);
        }
        [Test] public void SupplyCooldownAndDailyCapCannotBeBypassedByParallelTickets()
        {
            var first = rewards.SupplyTicket(); var concurrent = rewards.SupplyTicket();
            Assert.IsTrue(rewards.GrantEarned(first)); Assert.IsFalse(rewards.GrantEarned(concurrent)); Assert.IsNull(rewards.SupplyTicket());
            clock += 600; Assert.IsTrue(rewards.GrantEarned(rewards.SupplyTicket()));
            clock += 600; Assert.IsTrue(rewards.GrantEarned(rewards.SupplyTicket()));
            clock += 600; Assert.IsFalse(rewards.CanSupply); Assert.IsNull(rewards.SupplyTicket());
        }
        [Test] public void SupplyDailyResetStillHonorsCooldown()
        {
            clock = Util.Day(clock) * 86400 + 86390;
            Assert.IsTrue(rewards.GrantEarned(rewards.SupplyTicket()));
            clock += 20;
            Assert.AreEqual(0, rewards.SuppliesToday); Assert.IsFalse(rewards.CanSupply);
            clock += 580; Assert.IsTrue(rewards.CanSupply);
        }
        [Test] public void VictoryBonusIsHalfActualGoldAndCannotBeClaimedTwice()
        {
            double gold = c.Wallet.Get("gold"), gems = c.Wallet.Get("gems");
            var first = rewards.VictoryTicket("battle-a", 101); var duplicate = rewards.VictoryTicket("battle-a", 101);
            Assert.IsTrue(rewards.GrantEarned(first)); Assert.IsFalse(rewards.GrantEarned(duplicate));
            Assert.AreEqual(gold + 50, c.Wallet.Get("gold")); Assert.AreEqual(gems, c.Wallet.Get("gems"));
            Assert.IsNull(rewards.VictoryTicket("battle-a", 101)); Assert.IsNull(rewards.VictoryTicket("", 101));
        }
        [Test] public void GuildResetInvalidatesLateNativeCallbacks()
        {
            var ticket = rewards.SupplyTicket(); GuildGame.StartNew(c, "New guild", 1, "mage");
            double gold = c.Wallet.Get("gold"); Assert.IsFalse(rewards.GrantEarned(ticket)); Assert.AreEqual(gold, c.Wallet.Get("gold"));
        }
        [Test] public void ReloadPreservesReceiptsCapsAndCooldownButInvalidatesPendingCallbacks()
        {
            Assert.IsTrue(rewards.GrantEarned(rewards.SupplyTicket()));
            Assert.IsTrue(rewards.GrantEarned(rewards.VictoryTicket("saved-battle", 80)));
            var pending = rewards.VictoryTicket("pending-battle", 80);
            string json = c.SaveBlobs(out _); Assert.IsTrue(c.LoadBlobs(json));
            Assert.IsFalse(rewards.GrantEarned(pending)); Assert.IsTrue(rewards.Claimed("saved-battle"));
            Assert.AreEqual(1, rewards.SuppliesToday); Assert.IsFalse(rewards.CanSupply);
            clock += 600; Assert.IsTrue(rewards.CanSupply);
        }
        [Test] public void OlderSaveWithoutAdsBlobLoadsWithEmptyLedger()
        {
            string json = c.SaveBlobs(out _);
            var envelope = UnityEngine.JsonUtility.FromJson<SaveEnvelope>(json); envelope.blobs.RemoveAll(b => b.k == rewards.SaveKey);
            Assert.IsTrue(c.LoadBlobs(UnityEngine.JsonUtility.ToJson(envelope))); Assert.IsTrue(rewards.CanSupply);
            Assert.AreEqual(0, rewards.SuppliesToday);
        }
        [Test] public void InterstitialRequiresTutorialInventoryWarmupAndThreeDistinctCampaignBattles()
        {
            var p = new AdBreakPolicy(0); p.BattleFinished("one", true); p.BattleFinished("one", true); p.BattleFinished("exam", false); p.BattleFinished("two", true);
            Assert.IsFalse(p.CanShow(500, true, true, true)); p.BattleFinished("three", true);
            Assert.IsFalse(p.CanShow(119, true, true, true)); Assert.IsFalse(p.CanShow(120, false, true, true));
            Assert.IsFalse(p.CanShow(120, true, false, true)); Assert.IsFalse(p.CanShow(120, true, true, false));
            Assert.IsTrue(p.CanShow(120, true, true, true));
        }
        [Test] public void InterstitialCooldownSessionCapAndRewardedSuppressionAreEnforced()
        {
            var p = new AdBreakPolicy(0);
            for (int i = 0; i < 5; i++)
            {
                double now = 120 + i * 180;
                for (int b = 0; b < 3; b++) p.BattleFinished(i + "-" + b, true);
                Assert.IsTrue(p.CanShow(now, true, true, true)); p.InterstitialShown(now);
                Assert.IsFalse(p.CanShow(now + 179, true, true, true));
            }
            for (int b = 0; b < 3; b++) p.BattleFinished("extra" + b, true);
            Assert.IsFalse(p.CanShow(2000, true, true, true));
            p = new AdBreakPolicy(0); p.RewardedShown(200);
            for (int b = 0; b < 3; b++) p.BattleFinished("new" + b, true);
            Assert.IsFalse(p.CanShow(379, true, true, true)); Assert.IsTrue(p.CanShow(380, true, true, true));
        }
    }
}
