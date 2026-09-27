using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DuskAndDawn
{
    public enum ItemEffect
    {
        Heal,        // restore Amount health
        FullHeal,    // restore to max health
        Smoke,       // every enemy misses this turn
        RestoreDawn, // turn the night clock back Amount minutes
        HolyWater    // Amount damage to every enemy in the fight
    }

    public class Item
    {
        public string Name { get; }
        public string Description { get; }
        public ItemEffect Effect { get; }
        public int Amount { get; }

        public Item(string name, string description, ItemEffect effect, int amount = 0)
        {
            Name = name;
            Description = description;
            Effect = effect;
            Amount = amount;
        }

        // Infirmary
        public static Item Bandage() => new Item("Bandage", "Restores 20 health.", ItemEffect.Heal, 20);
        public static Item Tonic() => new Item("Tonic", "Restores 45 health.", ItemEffect.Heal, 45);
        public static Item SmokeFlask() => new Item("Smoke Flask", "Every enemy misses this turn - even spells and heavy blows.", ItemEffect.Smoke);
        public static Item Elixir() => new Item("Elixir", "Restores all health.", ItemEffect.FullHeal);
        public static Item HolyWater() => new Item("Holy Water", "Splashes every enemy in the fight for 14 damage. Made for groups.", ItemEffect.HolyWater, 14);
        public static Item DawnTincture() => new Item("Dawn Tincture", "Turns the night clock back 90 minutes.", ItemEffect.RestoreDawn, 90);

        // Kitchen
        public static Item Rations() => new Item("Rations", "Restores 30 health.", ItemEffect.Heal, 30);

        private static readonly Func<Item>[] AllFactories = { Bandage, Tonic, SmokeFlask, Elixir, DawnTincture, HolyWater, Rations };

        /// <summary>A few words on what it does, for the combat Items menu. Heals show what
        /// they'll actually restore, including the Infirmary's Lv 5 bonus.</summary>
        public string ShortEffect(PlayerState state) => Effect switch
        {
            ItemEffect.Heal => $"Heal {HealAmount(state)} health",
            ItemEffect.FullHeal => "Heal to full health",
            ItemEffect.Smoke => "All enemies miss this turn",
            ItemEffect.RestoreDawn => $"Clock back {DawnTimer.FormatDuration(Amount)}",
            ItemEffect.HolyWater => $"{Amount} damage to every enemy",
            _ => ""
        };

        public int HealAmount(PlayerState state) => (int)MathF.Round(Amount * state.RemedyPotency);

        /// <summary>Rebuilds a saved item by name, or null if it no longer exists.</summary>
        public static Item Create(string name) =>
            AllFactories.Select(make => make()).FirstOrDefault(item => item.Name == name);
    }
}