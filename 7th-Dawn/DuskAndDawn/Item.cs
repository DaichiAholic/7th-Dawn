using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DuskAndDawn
{
    public class Item
    {
        public string Name { get; }
        public string Description { get; }
        public int HealAmount { get; }

        public Item(string name, string description, int healAmount)
        {
            Name = name;
            Description = description;
            HealAmount = healAmount;
        }

        public static Item Bandage() => new Item("Bandage", "Restores 20 health.", 20);
    }
}
