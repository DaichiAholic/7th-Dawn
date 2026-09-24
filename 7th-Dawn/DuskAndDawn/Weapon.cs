using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DuskAndDawn
{
    public class Weapon
    {
        public string Name { get; }
        public int DiceCount { get; }
        public int DiceSides { get; }
        public int FlatBonus { get; }

        // Holy-steel weapons: extra damage per point of enemy corruption. 0 for normal weapons.
        public int CorruptionBonus { get; }

        // Content asset name of this weapon's 32x32 pixel icon (see Game1.GetWeaponIcon).
        // null = no art yet; screens draw an empty slot in its place.
        public string IconName { get; }

        public Weapon(string name, int diceCount, int diceSides, int flatBonus = 0, int corruptionBonus = 0, string iconName = null)
        {
            Name = name;
            DiceCount = diceCount;
            DiceSides = diceSides;
            FlatBonus = flatBonus;
            CorruptionBonus = corruptionBonus;
            IconName = iconName;
        }

        public string DiceLabel => FlatBonus != 0
            ? $"{DiceCount}d{DiceSides}+{FlatBonus}"
            : $"{DiceCount}d{DiceSides}";

        public bool IsHoly => CorruptionBonus > 0;

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

        // ---- Starter / loot ----
        public static Weapon RustyKnife() => new Weapon("Rusty Knife", 1, 6, iconName: "Dagger");
        public static Weapon IronSword() => new Weapon("Iron Sword", 2, 6);
        public static Weapon Cleaver() => new Weapon("Cleaver", 1, 10, 1, iconName: "Cleaver");
        public static Weapon ScrapClub() => new Weapon("Scrap Club", 2, 4, 2, iconName: "Club");
        public static Weapon HandAxe() => new Weapon("Hand Axe", 1, 12, iconName: "Axe");

        // ---- Workshop-only ----
        public static Weapon WoodenClub() => new Weapon("Wooden Club", 1, 8, iconName: "Club");
        public static Weapon HolyLance() => new Weapon("Holy Lance", 2, 6, 2, corruptionBonus: 2);

        // Factories rather than shared instances, so finding the same weapon twice gives
        // two separate weapons instead of the same object added to the inventory twice.
        public static readonly Func<Weapon>[] LootPool = { IronSword, Cleaver, ScrapClub, HandAxe };
    }
}