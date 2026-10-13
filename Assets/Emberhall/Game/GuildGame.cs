using System.Collections.Generic;
using ManagerKit;

namespace Emberhall
{
    /// <summary>Game-specific glue on top of the reusable ManagerKit: content pack, starting state.</summary>
    public static class GuildGame
    {
        public const string ContentFolder = "Content/emberhall";
        public static readonly string[] Emblems = { "#E8503A", "#3A8FE8", "#3AC27A", "#C45BE8", "#F2B632", "#E8E8F0" };

        public static GameContext Create(ContentDb db = null)
        {
            db = db ?? ContentDb.FromResources(ContentFolder);
            var c = new GameContext(db);
            c.Add(new AdRewards());
            c.Start();
            return c;
        }

        /// <summary>Resets everything and sets up a fresh guild with one starter hero.</summary>
        public static void StartNew(GameContext c, string guildName, int emblem, string starterClass)
        {
            c.NewGame();
            c.Meta.GuildName = guildName; c.Meta.Emblem = emblem;
            var cls = c.Content.Class(starterClass) ?? c.Content.Classes[0];
            var u = c.Recruiter.Generate(c.Rng, true);
            u.classId = cls.id; u.rarity = "uncommon"; u.level = 1; u.traits.Clear(); u.price = 0;
            // The hero arriving in the hall must match the portrait chosen on the charter.
            switch (cls.id)
            {
                case "warrior": u.name = "Aldric"; u.sprite = "chara_0"; break;
                case "archer": u.name = "Lyra"; u.sprite = "chara_6"; break;
                case "mage": u.name = "Elara"; u.sprite = "chara_2"; break;
            }
            u.hp = -1; u.xp = 0; u.promo = 0;
            u.mHp = u.mAtk = u.mDef = u.mSpd = 1.05;
            c.Roster.Add(u);
            foreach (var id in new[] { "w_sword_1", "w_bow_1", "w_staff_1", "w_spear_1", "w_dagger_1", "a_plate_1" }) c.Inventory.AddItem(id, 1, c.Rng, "common");
            c.Give(new Res("potion_s", 5)); c.Give(new Res("iron_ore", 12));
            c.Inventory.AutoEquip(u);
            c.Counters.Set("hero.hired", 1);
            c.Counters.Set("gear.equipped", 0);
            c.Roster.Dirty();
            c.Quests.Tick(Util.Now);
            c.Recruiter.EnsureAffordableRecruit();
            c.LastOffline = null;
            c.Changed();
        }
    }
}
