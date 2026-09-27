using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DuskAndDawn
{
    public class Weapon
    {
        public const int MaxReinforcement = 3;

        public string Name { get; }
        public int DiceCount { get; }
        public int DiceSides { get; }

        // The weapon's own bonus, before any Workshop reinforcement.
        public int BaseFlatBonus { get; }

        // Workshop reinforcement level (0 to MaxReinforcement). Each level is +1 damage per roll.
        public int Reinforcement { get; private set; }

        public int FlatBonus => BaseFlatBonus + Reinforcement;

        // 0 = starter, 1-3 = the Workshop level that makes it. Drives salvage value.
        public int Tier { get; }

        // Holy-steel weapons: extra damage per point of enemy corruption. 0 for normal weapons.
        public int CorruptionBonus { get; }

        // Content asset name of this weapon's 32x32 pixel icon (see Game1.GetWeaponIcon).
        // null = no art yet; screens draw an empty slot in its place.
        public string IconName { get; }

        public Weapon(string name, int tier, int diceCount, int diceSides, int flatBonus = 0, int corruptionBonus = 0, string iconName = null)
        {
            Name = name;
            Tier = tier;
            DiceCount = diceCount;
            DiceSides = diceSides;
            BaseFlatBonus = flatBonus;
            CorruptionBonus = corruptionBonus;
            IconName = iconName;
        }

        public string DisplayName => Reinforcement > 0 ? $"{Name} +{Reinforcement}" : Name;

        public string DiceLabel => FlatBonus != 0
            ? $"{DiceCount}d{DiceSides}+{FlatBonus}"
            : $"{DiceCount}d{DiceSides}";

        /// <summary>Short stat line for cards and recipes: dice plus average, or the holy bonus.</summary>
        public string StatLabel => IsHoly
            ? $"{DiceLabel} +{CorruptionBonus}/corruption"
            : $"{DiceLabel}  avg {AverageDamage:0.#}";

        public bool IsHoly => CorruptionBonus > 0;

        public float AverageDamage => AverageRoll(minFace: 1, extraDice: 0);

        /// <summary>Scraps you get back when a duplicate find is broken down.</summary>
        public int SalvageValue => 2 + Tier * 3;

        public void Reinforce() => Reinforcement = Math.Min(MaxReinforcement, Reinforcement + 1);

        public int RollDamage(Random random) => RollDamage(random, minFace: 1, extraDice: 0);

        /// <summary>Barracks-aware roll: every die shows at least minFace, and extraDice
        /// more of this weapon's dice are thrown on top of its normal count.</summary>
        public int RollDamage(Random random, int minFace, int extraDice)
        {
            int total = FlatBonus;
            for (int i = 0; i < DiceCount + extraDice; i++)
            {
                total += Math.Max(minFace, random.Next(1, DiceSides + 1));
            }
            return total;
        }

        /// <summary>Expected value of RollDamage with the same modifiers - used to decide
        /// whether a roll was "low" enough to spend a Barracks reroll on.</summary>
        public float AverageRoll(int minFace, int extraDice)
        {
            float perDie = 0f;
            for (int face = 1; face <= DiceSides; face++)
            {
                perDie += Math.Max(minFace, face);
            }
            perDie /= DiceSides;
            return FlatBonus + perDie * (DiceCount + extraDice);
        }

        // Each tier is a clear step up in average damage (starter 3.5, T1 ~6, T2 ~8.5,
        // T3 12+), so every Workshop level is worth the materials it costs.

        // ---- Tier 0: starter ----
        public static Weapon RustyKnife() => new Weapon("Rusty Knife", 0, 1, 6, iconName: "Dagger");

        // ---- Tier 1: Workshop Lv 1, found in the Outskirts ----
        public static Weapon WoodenClub() => new Weapon("Wooden Club", 1, 1, 8, 1, iconName: "Club");
        public static Weapon ScrapClub() => new Weapon("Scrap Club", 1, 2, 4, 1, iconName: "Club");

        // ---- Tier 2: Workshop Lv 2, found in the Church and the Keep ----
        public static Weapon IronSword() => new Weapon("Iron Sword", 2, 2, 6, 1);
        public static Weapon Cleaver() => new Weapon("Cleaver", 2, 1, 10, 3, iconName: "Cleaver");
        public static Weapon HandAxe() => new Weapon("Hand Axe", 2, 1, 12, 2, iconName: "Axe");

        // ---- Tier 3: Workshop Lv 3 only, never found ----
        public static Weapon HolyLance() => new Weapon("Holy Lance", 3, 2, 6, 2, corruptionBonus: 2);
        public static Weapon WarMaul() => new Weapon("War Maul", 3, 3, 6, 2);

        // Every weapon by name - how a save file turns names back into weapons.
        private static readonly Func<Weapon>[] AllFactories =
        {
            RustyKnife, WoodenClub, ScrapClub, IronSword, Cleaver, HandAxe, HolyLance, WarMaul
        };

        /// <summary>Rebuilds a saved weapon, reinforcement included. null for an unknown name
        /// (e.g. a weapon removed in a later version).</summary>
        public static Weapon Create(string name, int reinforcement = 0)
        {
            var weapon = AllFactories.Select(make => make()).FirstOrDefault(w => w.Name == name);
            if (weapon == null) return null;
            for (int i = 0; i < reinforcement; i++) weapon.Reinforce();
            return weapon;
        }
    }
}
