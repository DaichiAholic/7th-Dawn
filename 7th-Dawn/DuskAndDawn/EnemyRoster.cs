using System;
using System.Collections.Generic;

namespace DuskAndDawn
{
    /// <summary>
    /// Builds the enemies a room holds. District and depth set the base stats (see
    /// DistrictInfo), the day scales them up, and each kind then adjusts from there:
    /// Wretches are the baseline, Penitents are frail but come in groups, and the Knight is
    /// a fixed boss for the final night.
    /// </summary>
    public static class EnemyRoster
    {
        // % of fights that are a Penitent group instead of a single Wretch. The Church is
        // their home; some have wandered into the Keep.
        public static int PenitentChance(District district) => district switch
        {
            District.ChurchRuins => 25,
            District.CastleKeep => 20,
            _ => 0
        };

        // % of Castle Keep fights that are a Castle Knight - an elite, not the boss.
        public const int CastleKnightChance = 15;

        private static readonly string[] CastleKnightNames = { "Oathbound Knight", "Gilded Knight", "Ashen Knight" };

        private static readonly string[] PenitentNames = { "Penitent of Ash", "Penitent of Thorns", "Penitent of Salt" };

        /// <summary>A fresh encounter for a room at `tier` (0-6, from its depth).</summary>
        public static List<Enemy> RollEncounter(District district, int tier, int day, Random random)
        {
            if (district == District.CastleKeep && random.Next(100) < CastleKnightChance)
            {
                return new List<Enemy> { CastleKnight(district, tier, day, random) };
            }
            if (random.Next(100) < PenitentChance(district))
            {
                int count = tier >= 3 && day >= 5 ? 3 : 2;
                var group = new List<Enemy>();
                for (int i = 0; i < count; i++)
                {
                    group.Add(Penitent(district, tier, day, PenitentNames[i % PenitentNames.Length], random));
                }
                return group;
            }

            return new List<Enemy> { Wretch(district, tier, day, random) };
        }

        public static Enemy Wretch(District district, int tier, int day, Random random, bool summoned = false)
        {
            var (health, attack) = BaseStats(district, tier, day);
            string name = summoned ? "Summoned Wretch" : DistrictInfo.RandomWretchName(district, random);
            // Light hits - their danger is the heavy blow they sometimes wind up. The Knight's
            // summons are half-formed: a distraction, not a second boss.
            float attackScale = summoned ? 0.5f : 0.8f;
            if (summoned) health = Math.Max(10, health / 2);
            var enemy = new Enemy(name, EnemyKind.Wretch, health, Math.Max(2, (int)MathF.Round(attack * attackScale)),
                DistrictInfo.Corruption(district), summoned);
            enemy.PlanNextAction(random, day, 0);
            return enemy;
        }

        public static Enemy Penitent(District district, int tier, int day, string name, Random random)
        {
            var (health, attack) = BaseStats(district, tier, day);
            // Frail on their own, but they come in twos and threes and don't let up.
            var enemy = new Enemy(name, EnemyKind.Penitent, Math.Max(8, (int)MathF.Round(health * 0.45f)),
                Math.Max(2, (int)MathF.Round(attack * 0.5f)), DistrictInfo.Corruption(district));
            enemy.PlanNextAction(random, day, 0);
            return enemy;
        }

        /// <summary>A Keep elite: half again a Wretch's health, hits 25% harder and winds up
        /// often, but never calls for aid. Drops loot to match its health.</summary>
        public static Enemy CastleKnight(District district, int tier, int day, Random random)
        {
            var (health, attack) = BaseStats(district, tier, day);
            var knight = new Enemy(CastleKnightNames[random.Next(CastleKnightNames.Length)], EnemyKind.Knight,
                (int)MathF.Round(health * 1.5f), Math.Max(3, attack), DistrictInfo.Corruption(district));
            knight.PlanNextAction(random, day, 0);
            return knight;
        }

        /// <summary>The final night's boss. Fixed rather than depth-scaled - it guards the
        /// Hoard wherever you go - but tougher in the deeper districts.</summary>
        public static Enemy Knight(District district, Random random)
        {
            int districtIndex = (int)district;
            var knight = new Enemy("The Hollow Knight", EnemyKind.Knight,
                maxHealth: 100 + 20 * districtIndex,
                attackPower: 9 + 2 * districtIndex,
                corruption: DistrictInfo.Corruption(district),
                isBoss: true);
            knight.PlanNextAction(random, DayInfo.FinalDay, 0);
            return knight;
        }

        private static (int health, int attack) BaseStats(District district, int tier, int day)
        {
            int health = (int)MathF.Round(DistrictInfo.EnemyHealth(district, tier) * DayInfo.EnemyHealthMultiplier(day));
            int attack = DistrictInfo.EnemyAttack(district, tier) + DayInfo.EnemyAttackBonus(day);
            return (health, attack);
        }
    }
}
