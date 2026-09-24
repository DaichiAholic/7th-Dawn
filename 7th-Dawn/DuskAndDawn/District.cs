using System;
using System.Collections.Generic;

namespace DuskAndDawn
{
    public enum District
    {
        VillageOutskirts,
        ChurchRuins,
        CastleKeep
    }

    /// <summary>
    /// Per-district tuning. District identity lives in these numbers (room weight skews,
    /// enemy stats, corruption tier, loot bonus) rather than in new art or systems.
    /// A district is unlocked once the Archive reaches its RequiredArchiveLevel.
    /// </summary>
    public static class DistrictInfo
    {
        public static readonly District[] All =
        {
            District.VillageOutskirts, District.ChurchRuins, District.CastleKeep
        };

        public static string Name(District district) => district switch
        {
            District.VillageOutskirts => "Village Outskirts",
            District.ChurchRuins => "Church Ruins",
            District.CastleKeep => "Castle Keep",
            _ => district.ToString()
        };

        public static string Tagline(District district) => district switch
        {
            District.VillageOutskirts => "Gluttony and Envy. Low corruption, plenty of supplies.",
            District.ChurchRuins => "Pride and Zealotry. Stranger rooms, tougher faithful.",
            District.CastleKeep => "Pride and Tyranny. Brutal fights, the richest hauls.",
            _ => ""
        };

        // Archive Lv 1 opens the Outskirts, Lv 2 the Church, Lv 3 the Keep.
        public static int RequiredArchiveLevel(District district) => (int)district + 1;

        // Corruption tier 1-3. Holy weapons scale off this, and it's the number the
        // ember-glow visual language is meant to express.
        public static int Corruption(District district) => (int)district + 1;

        public static int EnemyHealthBonus(District district) => district switch
        {
            District.ChurchRuins => 10,
            District.CastleKeep => 20,
            _ => 0
        };

        public static int EnemyAttackBonus(District district) => district switch
        {
            District.ChurchRuins => 2,
            District.CastleKeep => 4,
            _ => 0
        };

        // Added on top of every Supplies / Special resource find in this district.
        public static int LootBonus(District district) => district switch
        {
            District.ChurchRuins => 1,
            District.CastleKeep => 2,
            _ => 0
        };

        // Added to RoomGenerator's baseline weights.
        public static (int supplies, int encounter, int special) WeightSkew(District district) => district switch
        {
            District.VillageOutskirts => (10, 0, 0),
            District.ChurchRuins => (0, 0, 10),
            District.CastleKeep => (-5, 15, 0),
            _ => (0, 0, 0)
        };

        private static readonly Dictionary<District, string[]> EnemyNames = new Dictionary<District, string[]>
        {
            { District.VillageOutskirts, new[] { "Gluttonous Wretch", "Envious Husk", "Starved Hound" } },
            { District.ChurchRuins, new[] { "Zealot Shade", "Proud Acolyte", "Hollow Choirboy" } },
            { District.CastleKeep, new[] { "Tyrant's Guard", "Gilded Knight", "Crowned Ruin" } }
        };

        public static string RandomEnemyName(District district, Random random)
        {
            var names = EnemyNames[district];
            return names[random.Next(names.Length)];
        }
    }
}