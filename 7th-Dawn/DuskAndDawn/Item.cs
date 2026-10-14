using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DuskAndDawn
{
    public enum ItemEffect
    {
        Heal,
        FullHeal,
        Smoke,
        RestoreDawn,
        HolyWater
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

        public static Item Bandage() => new Item("Bandage", "Restores 20 health.", ItemEffect.Heal, 20);
        public static Item Tonic() => new Item("Tonic", "Restores 45 health.", ItemEffect.Heal, 45);
        public static Item SmokeFlask() => new Item("Smoke Flask", "Every enemy misses this turn - even spells and heavy blows.", ItemEffect.Smoke);
        public static Item Elixir() => new Item("Elixir", "Restores all health.", ItemEffect.FullHeal);
        public static Item HolyWater() => new Item("Holy Water", "Splashes every enemy in the fight for 14 damage. Made for groups.", ItemEffect.HolyWater, 14);
        public static Item DawnTincture() => new Item("Dawn Tincture", "Turns the night clock back 90 minutes.", ItemEffect.RestoreDawn, 90);

        public static Item Rations() => new Item("Rations", "Restores 30 health.", ItemEffect.Heal, 30);

        private static readonly Func<Item>[] AllFactories = { Bandage, Tonic, SmokeFlask, Elixir, DawnTincture, HolyWater, Rations };

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

        public bool UsableOutsideFight => Effect is ItemEffect.Heal or ItemEffect.FullHeal or ItemEffect.RestoreDawn;

        public string WastedReason(PlayerState state, DawnTimer timer) => Effect switch
        {
            ItemEffect.Heal or ItemEffect.FullHeal when state.Health >= state.MaxHealth => "You're unhurt",
            ItemEffect.RestoreDawn when timer.MinutesElapsed == 0 => "The night has barely begun",
            _ when !UsableOutsideFight => "Only in a fight",
            _ => null
        };

        public string ApplyRemedy(PlayerState state, DawnTimer timer)
        {
            switch (Effect)
            {
                case ItemEffect.Heal:
                    {
                        int before = state.Health;
                        state.Health = Math.Min(state.MaxHealth, state.Health + HealAmount(state));
                        return $"You use the {Name} and recover {state.Health - before} health.";
                    }
                case ItemEffect.FullHeal:
                    {
                        int before = state.Health;
                        state.Health = state.MaxHealth;
                        return $"You drink the {Name} and recover {state.Health - before} health.";
                    }
                case ItemEffect.RestoreDawn:
                    timer.Restore(Amount);
                    return $"You drink the {Name}. The clock slips back {DawnTimer.FormatDuration(Amount)}.";
                default:
                    return null;
            }
        }

        public static Item Create(string name) =>
            AllFactories.Select(make => make()).FirstOrDefault(item => item.Name == name);
    }
}