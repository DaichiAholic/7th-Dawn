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

        public Weapon(string name, int diceCount, int diceSides, int flatBonus = 0)
        {
            Name = name;
            DiceCount = diceCount;
            DiceSides = diceSides;
            FlatBonus = flatBonus;
        }

        public string DiceLabel => FlatBonus != 0
            ? $"{DiceCount}d{DiceSides}+{FlatBonus}"
            : $"{DiceCount}d{DiceSides}";

        public int RollDamage(Random random)
        {
            int total = FlatBonus;
            for (int i = 0; i < DiceCount; i++)
            {
                total += random.Next(1, DiceSides + 1);
            }
            return total;
        }

        public static Weapon RustyKnife() => new Weapon("Rusty Knife", 1, 6);
        public static Weapon IronSword() => new Weapon("Iron Sword", 2, 6);
        public static Weapon Cleaver() => new Weapon("Cleaver", 1, 10, 1);
        public static Weapon ScrapClub() => new Weapon("Scrap Club", 2, 4, 2);

        public static readonly Weapon[] LootPool = { IronSword(), Cleaver(), ScrapClub() };
    }
}
