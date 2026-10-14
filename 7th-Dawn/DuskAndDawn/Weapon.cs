using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DuskAndDawn
{
    public enum WeaponTrait
    {
        None,
        Bleed,
        Stagger,
        Lifesteal,
        Sunbane
    }

    public class Weapon
    {
        public const int BleedTurns = 3;

        public const int MaxReinforcement = 5;

        public string Name { get; }
        public int DiceCount { get; }
        public int DiceSides { get; }

        public int BaseFlatBonus { get; }

        public int Reinforcement { get; private set; }

        public int FlatBonus => BaseFlatBonus + Reinforcement;

        public int Tier { get; }

        public int CorruptionBonus { get; }

        public string IconName { get; }

        public WeaponTrait Trait { get; }
        public int TraitPower { get; }

        public Weapon(string name, int tier, int diceCount, int diceSides, int flatBonus = 0, int corruptionBonus = 0, string iconName = null,
            WeaponTrait trait = WeaponTrait.None, int traitPower = 0)
        {
            Name = name;
            Tier = tier;
            DiceCount = diceCount;
            DiceSides = diceSides;
            BaseFlatBonus = flatBonus;
            CorruptionBonus = corruptionBonus;
            IconName = iconName;
            Trait = trait;
            TraitPower = traitPower;
        }

        public string TraitLabel => Trait switch
        {
            WeaponTrait.Bleed => $"Bleed {TraitPower}",
            WeaponTrait.Stagger => $"Stagger {TraitPower}%",
            WeaponTrait.Lifesteal => $"Drain {TraitPower}%",
            WeaponTrait.Sunbane => "Sunbane",
            _ => ""
        };

        public string TraitDescription => Trait switch
        {
            WeaponTrait.Bleed => $"Hits make the target bleed {TraitPower} a turn for {BleedTurns} turns.",
            WeaponTrait.Stagger => $"{TraitPower}% chance a hit makes the target lose its next move - even a wind-up.",
            WeaponTrait.Lifesteal => $"Heals you for {TraitPower}% of the damage it deals.",
            WeaponTrait.Sunbane => $"{TraitPower}% more damage against the Sun Herald.",
            _ => ""
        };

        public string DisplayName => Reinforcement > 0 ? $"{Name} +{Reinforcement}" : Name;

        public string DiceLabel => FlatBonus != 0
            ? $"{DiceCount}d{DiceSides}+{FlatBonus}"
            : $"{DiceCount}d{DiceSides}";

        public string StatLabel => IsHoly
            ? $"{DiceLabel} +{CorruptionBonus}/corruption"
            : $"{DiceLabel}  avg {AverageDamage:0.#}";

        public bool IsHoly => CorruptionBonus > 0;

        public float AverageDamage => AverageRoll(minFace: 1, extraDice: 0);

        public void Reinforce() => Reinforcement = Math.Min(MaxReinforcement, Reinforcement + 1);

        public int RollDamage(Random random) => RollDamage(random, minFace: 1, extraDice: 0);

        public int RollDamage(Random random, int minFace, int extraDice)
        {
            int total = FlatBonus;
            for (int i = 0; i < DiceCount + extraDice; i++)
            {
                total += Math.Max(minFace, random.Next(1, DiceSides + 1));
            }
            return total;
        }

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

        public static Weapon RustyKnife() => new Weapon("Rusty Knife", 0, 1, 6, iconName: "RustyKnife", trait: WeaponTrait.Bleed, traitPower: 1);

        public static Weapon WoodenClub() => new Weapon("Wooden Club", 1, 1, 8, 1, iconName: "Club", trait: WeaponTrait.Stagger, traitPower: 20);
        public static Weapon ScrapClub() => new Weapon("Scrap Club", 1, 2, 4, 1, iconName: "Club", trait: WeaponTrait.Stagger, traitPower: 25);

        public static Weapon IronSword() => new Weapon("Iron Sword", 2, 2, 6, 1, iconName: "IronSword", trait: WeaponTrait.Lifesteal, traitPower: 20);
        public static Weapon Cleaver() => new Weapon("Cleaver", 2, 1, 10, 3, iconName: "Cleaver", trait: WeaponTrait.Bleed, traitPower: 3);
        public static Weapon HandAxe() => new Weapon("Hand Axe", 2, 1, 12, 2, iconName: "Axe", trait: WeaponTrait.Bleed, traitPower: 2);

        public static Weapon HolyLance() => new Weapon("Holy Lance", 3, 2, 6, 2, corruptionBonus: 2, iconName: "HolyLance", trait: WeaponTrait.Lifesteal, traitPower: 15);
        public static Weapon WarMaul() => new Weapon("War Maul", 3, 3, 6, 2, iconName: "WarMaul", trait: WeaponTrait.Stagger, traitPower: 35);

        public static Weapon Dawnbreaker() => new Weapon("Dawnbreaker", 4, 3, 6, 3, corruptionBonus: 2, iconName: "Dawnbreaker", trait: WeaponTrait.Sunbane, traitPower: 30);

        private static readonly Func<Weapon>[] AllFactories =
        {
            RustyKnife, WoodenClub, ScrapClub, IronSword, Cleaver, HandAxe, HolyLance, WarMaul, Dawnbreaker
        };

        public static Weapon Create(string name, int reinforcement = 0)
        {
            var weapon = AllFactories.Select(make => make()).FirstOrDefault(w => w.Name == name);
            if (weapon == null) return null;
            for (int i = 0; i < reinforcement; i++) weapon.Reinforce();
            return weapon;
        }
    }
}
