using System;
using System.Collections.Generic;

namespace DuskAndDawn
{
    /// <summary>
    /// Builds the enemies a room holds. District and depth set the base stats (see
    /// DistrictInfo), the day scales them up, and each kind then adjusts from there:
    /// Wretches are the baseline, Penitents are frail but come in groups, Knights are elites,
    /// and the Sun Herald is the fixed boss of the last night, in The Castle.
    /// </summary>
    public static class EnemyRoster
    {
        // % of fights that are a Penitent group instead of a single Wretch. The Church is
        // their home; some have wandered into the Keep.
        public static int PenitentChance(District district) => district switch
        {
            District.ChurchRuins => 25,
            District.CastleKeep => 20,
            District.Castle => 25,
            _ => 0
        };

        // % of fights that are a Castle Knight - an elite, not the boss. They garrison the
        // Keep, and the Herald's Castle is full of them.
        public static int CastleKnightChance(District district) => district switch
        {
            District.CastleKeep => 15,
            District.Castle => 30,
            _ => 0
        };

        private static readonly string[] CastleKnightNames = { "Oathbound Knight", "Gilded Knight", "Ashen Knight" };

        private static readonly string[] PenitentNames = { "Penitent of Ash", "Penitent of Thorns", "Penitent of Salt" };

        /// <summary>A fresh encounter for a room at `tier` (0-6, from its depth).</summary>
        public static List<Enemy> RollEncounter(District district, int tier, int day, Random random)
        {
            if (random.Next(100) < CastleKnightChance(district))
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
            // Light hits - their danger is the heavy blow they sometimes wind up. Summoned
            // ones are half-formed: a distraction, not a second boss.
            float attackScale = summoned ? 0.5f : 0.8f;
            if (summoned) health = Math.Max(10, health / 2);
            var enemy = new Enemy(name, EnemyKind.Wretch, health, Math.Max(2, (int)MathF.Round(attack * attackScale)),
                DistrictInfo.Corruption(district), summoned);
            enemy.PlanNextAction(random, day, 0);
            return enemy;
        }

        public static Enemy Penitent(District district, int tier, int day, string name, Random random, bool summoned = false)
        {
            var (health, attack) = BaseStats(district, tier, day);
            // Frail on their own, but they come in twos and threes and don't let up.
            // The Herald's choir are frailer still - a nuisance to deal with, not a second boss.
            float healthScale = summoned ? 0.22f : 0.45f;
            var enemy = new Enemy(name, EnemyKind.Penitent, Math.Max(8, (int)MathF.Round(health * healthScale)),
                Math.Max(2, (int)MathF.Round(attack * (summoned ? 0.35f : 0.5f))), DistrictInfo.Corruption(district), summoned);
            enemy.PlanNextAction(random, day, 0);
            return enemy;
        }

        private static readonly string[] ChoirNames = { "Choir Acolyte", "Choir Cantor", "Choir Novice" };

        /// <summary>A Penitent the Sun Herald calls from his choir. Drops nothing.</summary>
        public static Enemy ChoirAcolyte(District district, int day, Random random) =>
            Penitent(district, 1, day, ChoirNames[random.Next(ChoirNames.Length)], random, summoned: true);

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

        public const int SunHeraldHealth = 195;
        public const int SunHeraldAttack = 10;

        /// <summary>The last night's boss, on the Castle's Hoard. Fixed stats: his fight is
        /// about reading his moves - Guard the Brand, smoke or out-heal the Solar Flare, clear
        /// his choir or ignore it - and about the second phase from half health.
        /// He is the holy light itself, not a corruption of it: holy steel gets no bonus
        /// against him. Only the Dawnbreaker was made for this.</summary>
        public static Enemy SunHerald(Random random)
        {
            var herald = new Enemy("The Sun Herald", EnemyKind.Herald,
                maxHealth: SunHeraldHealth,
                attackPower: SunHeraldAttack,
                corruption: 0,
                isBoss: true);
            herald.PlanNextAction(random, DayInfo.FinalDay, 0);
            return herald;
        }

        private static (int health, int attack) BaseStats(District district, int tier, int day)
        {
            int health = (int)MathF.Round(DistrictInfo.EnemyHealth(district, tier) * DayInfo.EnemyHealthMultiplier(day));
            int attack = DistrictInfo.EnemyAttack(district, tier) + DayInfo.EnemyAttackBonus(day);
            return (health, attack);
        }
    }
}
