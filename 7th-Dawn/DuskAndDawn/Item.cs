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
        Smoke,       // the enemy's next attack misses
        RestoreDawn  // give back Amount ticks of dawn time
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
        public static Item SmokeFlask() => new Item("Smoke Flask", "The enemy's next attack misses.", ItemEffect.Smoke);
        public static Item Elixir() => new Item("Elixir", "Restores all health.", ItemEffect.FullHeal);
        public static Item DawnTincture() => new Item("Dawn Tincture", "Gives back 3 ticks of dawn time.", ItemEffect.RestoreDawn, 3);

        // Kitchen
        public static Item Rations() => new Item("Rations", "Restores 30 health.", ItemEffect.Heal, 30);
    }
}