using System;
using System.Collections.Generic;

namespace DuskAndDawn
{
    public static class EnemyRoster
    {
        private static readonly string[] CastleKnightNames = { "Oathbound Knight", "Gilded Knight", "Ashen Knight" };

        private static readonly string[] PenitentNames = { "Penitent of Ash", "Penitent of Thorns", "Penitent of Salt" };

        private static readonly string[] ChoirNames = { "Choir Acolyte", "Choir Cantor", "Choir Novice" };

        public static List<Enemy> RollEncounter(District district, int tier, int day, Random random)
        {
            var odds = EnemyStats.Districts[district];
            if (random.Next(100) < odds.KnightChance)
            {
                return new List<Enemy> { CastleKnight(district, tier, day, random) };
            }
            if (random.Next(100) < odds.PenitentChance)
            {
                bool big = tier >= EnemyStats.PenitentBigGroupFromTier && day >= EnemyStats.PenitentBigGroupFromDay;
                int count = big ? EnemyStats.PenitentBigGroup : EnemyStats.PenitentGroup;
                var group = new List<Enemy>();
                for (int i = 0; i < count; i++)
                {
                    group.Add(Penitent(district, tier, day, PenitentNames[i % PenitentNames.Length], random));
                }
                return group;
            }

            return new List<Enemy> { Wretch(district, tier, day, random) };
        }

        public static Enemy Wretch(District district, int tier, int day, Random random) =>
            Make(DistrictInfo.RandomWretchName(district, random), EnemyKind.Wretch, EnemyStats.Wretch, district, tier, day, random);

        public static Enemy Penitent(District district, int tier, int day, string name, Random random) =>
            Make(name, EnemyKind.Penitent, EnemyStats.Penitent, district, tier, day, random);

        public static Enemy CastleKnight(District district, int tier, int day, Random random) =>
            Make(CastleKnightNames[random.Next(CastleKnightNames.Length)], EnemyKind.Knight, EnemyStats.Knight, district, tier, day, random);

        public static Enemy ChoirAcolyte(District district, int day, Random random) =>
            Make(ChoirNames[random.Next(ChoirNames.Length)], EnemyKind.Penitent, EnemyStats.Choir, district, EnemyStats.ChoirTier, day, random, summoned: true);

        public static Enemy SunHerald(Random random)
        {
            var herald = new Enemy("The Sun Herald", EnemyKind.Herald, EnemyStats.HeraldHealth, EnemyStats.HeraldAttack, corruption: 0, isBoss: true);
            herald.PlanNextAction(random, DayInfo.FinalDay, 0);
            return herald;
        }

        private static Enemy Make(string name, EnemyKind kind, EnemyScale scale, District district, int tier, int day, Random random, bool summoned = false)
        {
            var (health, attack) = EnemyStats.For(scale, district, tier, day);
            var enemy = new Enemy(name, kind, health, attack, DistrictInfo.Corruption(district), summoned);
            enemy.PlanNextAction(random, day, 0);
            return enemy;
        }
    }
}
