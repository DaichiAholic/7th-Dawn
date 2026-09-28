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

        // Skips empty piles ("+3 Food, +1 Scraps"), and says "Nothing" if it all came up empty.
        public static string Describe(int food, int planks, int scraps) =>
            new ResourceDelta(food, planks, scraps).Describe();
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

        // ---- Enemies ----
        // Each district is a clear step up, sized so the gear you can build for it is what
        // makes it worth fighting in: the Outskirts are fair with a Tier 1 weapon, the Church
        // wants Tier 2, and the Keep wants Tier 3 (or a well-reinforced Tier 2).
        // `tier` is 0-6, growing with how deep into the maze the room is.
        public static int EnemyHealth(District district, int tier) => district switch
        {
            District.ChurchRuins => 34 + tier * 4,
            District.CastleKeep => 50 + tier * 5,
            _ => 18 + tier * 3
        };

        public static int EnemyAttack(District district, int tier) => district switch
        {
            District.ChurchRuins => 8 + tier,
            District.CastleKeep => 12 + tier,
            _ => 4 + tier
        };

        /// <summary>Scraps (and a few Planks) stripped from a defeated enemy. Scales with the
        /// enemy's health, so tougher fights pay more - and only a weapon that wins them
        /// without bleeding out turns that into a good night. The Keep's guards carry the most.</summary>
        public static (int food, int planks, int scraps) CombatLoot(District district, int enemyMaxHealth, Random random)
        {
            int districtBonus = district switch
            {
                District.ChurchRuins => 1,
                District.CastleKeep => 3,
                _ => 0
            };
            int scraps = enemyMaxHealth / 6 + districtBonus + random.Next(0, 3);
            int planks = random.Next(0, 2) + (int)district;
            return (0, planks, scraps);
        }

        // ---- Materials ----
        // Every district now yields all three materials, but each leans hard into one of
        // them, so picking a district is also picking what the base gets tonight:
        // the Outskirts' farms give Food, the Church's pews and rafters give Planks, and the
        // Keep's armouries give Scraps. One "find" (a quick search, or a Special cache)
        // rolls each range once; a thorough search rolls it twice, and the Hoard three times.

        public static MaterialYield Yield(District district) => district switch
        {
            // The world's been picked over for years: most finds are thin, and a pile can
            // come up empty. Specialties still stand out, but nothing is plentiful.
            District.VillageOutskirts => new MaterialYield(food: new LootRange(2, 4), planks: new LootRange(1, 2), scraps: new LootRange(0, 2)),
            District.ChurchRuins => new MaterialYield(food: new LootRange(1, 2), planks: new LootRange(2, 5), scraps: new LootRange(0, 2)),
            District.CastleKeep => new MaterialYield(food: new LootRange(1, 2), planks: new LootRange(1, 3), scraps: new LootRange(3, 5)),
            _ => new MaterialYield(new LootRange(0, 2), new LootRange(0, 2), new LootRange(0, 2))
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

        // What this district's Strange Room events tend to offer (see NightEventPool).
        // Weapons are never found out here - they come from the Workshop.
        public static string EventTheme(District district) => district switch
        {
            District.VillageOutskirts => "food and supplies",
            District.ChurchRuins => "timber and relics",
            District.CastleKeep => "metal - and traps",
            _ => "odds and ends"
        };

        // Added to RoomGenerator's baseline weights.
        public static (int supplies, int encounter, int special) WeightSkew(District district) => district switch
        {
            District.VillageOutskirts => (20, 0, 0),
            District.ChurchRuins => (10, 0, 10),
            District.CastleKeep => (5, 15, 0),
            _ => (0, 0, 0)
        };

        // Every district's common foe is a Wretch; the name says which sin hollowed it out.
        private static readonly Dictionary<District, string[]> WretchNames = new Dictionary<District, string[]>
        {
            { District.VillageOutskirts, new[] { "Gluttonous Wretch", "Envious Wretch", "Starved Wretch" } },
            { District.ChurchRuins, new[] { "Zealot Wretch", "Proud Wretch", "Hollow Wretch" } },
            { District.CastleKeep, new[] { "Gilded Wretch", "Crowned Wretch", "Tyrant's Wretch" } }
        };

        public static string RandomWretchName(District district, Random random)
        {
            var names = WretchNames[district];
            return names[random.Next(names.Length)];
        }
    }
}