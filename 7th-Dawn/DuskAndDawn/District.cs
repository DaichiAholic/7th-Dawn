using System;
using System.Collections.Generic;

namespace DuskAndDawn
{
    public enum District
    {
        VillageOutskirts,
        ChurchRuins,
        CastleKeep,
        Castle
    }

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
            new ResourceDelta(food, planks, scraps).Describe();
    }

    public static class DistrictInfo
    {
        public static readonly District[] All =
        {
            District.VillageOutskirts, District.ChurchRuins, District.CastleKeep, District.Castle
        };

        public static readonly District[] Scavenging =
        {
            District.VillageOutskirts, District.ChurchRuins, District.CastleKeep
        };

        public static string Name(District district) => district switch
        {
            District.VillageOutskirts => "Village Outskirts",
            District.ChurchRuins => "Church Ruins",
            District.CastleKeep => "Castle Keep",
            District.Castle => "The Castle",
            _ => district.ToString()
        };

        public static string Tagline(District district) => district switch
        {
            District.VillageOutskirts => "Gluttony and Envy. Low corruption, full larders.",
            District.ChurchRuins => "Pride and Zealotry. Stranger rooms, old relics.",
            District.CastleKeep => "Pride and Tyranny. Brutal fights, iron by the cartload.",
            District.Castle => "The Sun Herald's court. On the last night its doors open, and he waits on his throne for his dawn.",
            _ => ""
        };

        public static int RequiredArchiveLevel(District district) => Math.Min(3, (int)district + 1);

        public const int MaxCorruption = 4;

        public static int Corruption(District district) => (int)district + 1;

        public static int EnemyHealth(District district, int tier) => district switch
        {
            District.ChurchRuins => 34 + tier * 4,
            District.CastleKeep => 50 + tier * 5,
            District.Castle => 54 + tier * 5,
            _ => 18 + tier * 3
        };

        public static int EnemyAttack(District district, int tier) => district switch
        {
            District.ChurchRuins => 8 + tier,
            District.CastleKeep => 12 + tier,
            District.Castle => 12 + tier,
            _ => 4 + tier
        };

        public static (int food, int planks, int scraps) CombatLoot(District district, int enemyMaxHealth, Random random)
        {
            int districtBonus = district switch
            {
                District.ChurchRuins => 1,
                District.CastleKeep => 3,
                District.Castle => 3,
                _ => 0
            };
            int scraps = enemyMaxHealth / 6 + districtBonus + random.Next(0, 3);
            int planks = random.Next(0, 2) + Math.Min(2, (int)district);
            return (0, planks, scraps);
        }

        public static MaterialYield Yield(District district) => district switch
        {
            District.VillageOutskirts => new MaterialYield(food: new LootRange(2, 4), planks: new LootRange(1, 2), scraps: new LootRange(0, 2)),
            District.ChurchRuins => new MaterialYield(food: new LootRange(1, 2), planks: new LootRange(2, 5), scraps: new LootRange(0, 2)),
            District.CastleKeep => new MaterialYield(food: new LootRange(1, 2), planks: new LootRange(1, 3), scraps: new LootRange(3, 5)),
            District.Castle => new MaterialYield(food: new LootRange(1, 3), planks: new LootRange(1, 3), scraps: new LootRange(3, 6)),
            _ => new MaterialYield(new LootRange(0, 2), new LootRange(0, 2), new LootRange(0, 2))
        };

        public static string SpecialtyMaterial(District district) => district switch
        {
            District.VillageOutskirts => "Food",
            District.ChurchRuins => "Planks",
            District.CastleKeep => "Scraps",
            District.Castle => "Scraps",
            _ => ""
        };

        public static (int food, int planks, int scraps) SpecialtyAmount(District district, int amount) => district switch
        {
            District.VillageOutskirts => (amount, 0, 0),
            District.ChurchRuins => (0, amount, 0),
            _ => (0, 0, amount)
        };

        public static string EventTheme(District district) => district switch
        {
            District.VillageOutskirts => "food and supplies",
            District.ChurchRuins => "timber and relics",
            District.CastleKeep => "metal - and traps",
            District.Castle => "the Herald's gifts, which always cost something",
            _ => "odds and ends"
        };

        public static (int supplies, int encounter, int special) WeightSkew(District district) => district switch
        {
            District.VillageOutskirts => (20, 0, 0),
            District.ChurchRuins => (10, 0, 10),
            District.CastleKeep => (5, 15, 0),
            District.Castle => (5, 20, 5),
            _ => (0, 0, 0)
        };

        private static readonly Dictionary<District, string[]> WretchNames = new Dictionary<District, string[]>
        {
            { District.VillageOutskirts, new[] { "Gluttonous Wretch", "Envious Wretch", "Starved Wretch" } },
            { District.ChurchRuins, new[] { "Zealot Wretch", "Proud Wretch", "Hollow Wretch" } },
            { District.CastleKeep, new[] { "Gilded Wretch", "Crowned Wretch", "Tyrant's Wretch" } },
            { District.Castle, new[] { "Sunscorched Wretch", "Herald's Wretch", "Blinded Wretch" } }
        };

        public static string RandomWretchName(District district, Random random)
        {
            var names = WretchNames[district];
            return names[random.Next(names.Length)];
        }
    }
}