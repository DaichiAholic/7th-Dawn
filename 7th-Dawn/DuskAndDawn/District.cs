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

    /// <summary>An inclusive min-max roll for one material.</summary>
    public readonly struct LootRange
    {
        public int Min { get; }
        public int Max { get; }

        public LootRange(int min, int max)
        {
            Min = min;
            Max = max;
        }

        public int Roll(Random random) => random.Next(Min, Max + 1);

        public override string ToString() => Min == Max ? Min.ToString() : $"{Min}-{Max}";
    }

    /// <summary>What one material find in a district can hold.</summary>
    public readonly struct MaterialYield
    {
        public LootRange Food { get; }
        public LootRange Planks { get; }
        public LootRange Scraps { get; }

        public MaterialYield(LootRange food, LootRange planks, LootRange scraps)
        {
            Food = food;
            Planks = planks;
            Scraps = scraps;
        }

        /// <summary>Rolls every range `times` times and sums them.</summary>
        public (int food, int planks, int scraps) Roll(Random random, int times = 1)
        {
            int food = 0, planks = 0, scraps = 0;
            for (int i = 0; i < times; i++)
            {
                food += Food.Roll(random);
                planks += Planks.Roll(random);
                scraps += Scraps.Roll(random);
            }
            return (food, planks, scraps);
        }

        public static string Describe(int food, int planks, int scraps) =>
            $"+{food} Food, +{planks} Planks, +{scraps} Scraps";
    }

    /// <summary>
    /// Per-district tuning. District identity lives in these numbers (room weight skews,
    /// enemy stats, corruption tier, material yields) rather than in new art or systems.
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
            District.VillageOutskirts => "Gluttony and Envy. Low corruption, full larders.",
            District.ChurchRuins => "Pride and Zealotry. Stranger rooms, old relics.",
            District.CastleKeep => "Pride and Tyranny. Brutal fights, iron by the cartload.",
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

        // ---- Materials ----
        // Every district now yields all three materials, but each leans hard into one of
        // them, so picking a district is also picking what the base gets tonight:
        // the Outskirts' farms give Food, the Church's pews and rafters give Planks, and the
        // Keep's armouries give Scraps. One "find" (a quick search, or a Special cache)
        // rolls each range once; a thorough search rolls it twice, and the Hoard three times.

        public static MaterialYield Yield(District district) => district switch
        {
            District.VillageOutskirts => new MaterialYield(food: new LootRange(3, 6), planks: new LootRange(2, 4), scraps: new LootRange(1, 3)),
            District.ChurchRuins => new MaterialYield(food: new LootRange(2, 4), planks: new LootRange(4, 7), scraps: new LootRange(2, 4)),
            District.CastleKeep => new MaterialYield(food: new LootRange(2, 4), planks: new LootRange(3, 5), scraps: new LootRange(5, 8)),
            _ => new MaterialYield(new LootRange(1, 3), new LootRange(1, 3), new LootRange(1, 3))
        };

        // The material this district is known for - highlighted on the Prepare map.
        public static string SpecialtyMaterial(District district) => district switch
        {
            District.VillageOutskirts => "Food",
            District.ChurchRuins => "Planks",
            District.CastleKeep => "Scraps",
            _ => ""
        };

        // Splits an amount of this district's specialty material into (food, planks, scraps),
        // for small stray finds like a Quiet Hall's rubble.
        public static (int food, int planks, int scraps) SpecialtyAmount(District district, int amount) => district switch
        {
            District.VillageOutskirts => (amount, 0, 0),
            District.ChurchRuins => (0, amount, 0),
            _ => (0, 0, amount)
        };

        // Chance (0-100) that a Special room holds a weapon instead of a material cache.
        public static int WeaponFindChance(District district) => district switch
        {
            District.VillageOutskirts => 30,
            District.ChurchRuins => 60,
            District.CastleKeep => 45,
            _ => 50
        };

        // Added to RoomGenerator's baseline weights.
        public static (int supplies, int encounter, int special) WeightSkew(District district) => district switch
        {
            District.VillageOutskirts => (20, 0, 0),
            District.ChurchRuins => (10, 0, 10),
            District.CastleKeep => (5, 15, 0),
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