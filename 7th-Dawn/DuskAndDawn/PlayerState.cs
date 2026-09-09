using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DuskAndDawn
{
    public class PlayerState
    {
        public int Food = 10;
        public int Planks = 10;
        public int Scraps = 10;

        public const int MaxHope = 100;
        public int Hope = 100;

        public int MaxHealth = 100;
        public int Health = 100;

        public List<Weapon> Inventory { get; } = new List<Weapon>();
        public Weapon EquippedWeapon;

        public List<Item> Items { get; } = new List<Item>();

        public Dictionary<BaseRoomType, int> RoomLevels { get; } = new Dictionary<BaseRoomType, int>();

        public PlayerState()
        {
            foreach (BaseRoomType room in Enum.GetValues(typeof(BaseRoomType)))
            {
                RoomLevels[room] = 1;
            }

            var starterWeapon = Weapon.RustyKnife();
            Inventory.Add(starterWeapon);
            EquippedWeapon = starterWeapon;

            Items.Add(Item.Bandage());
        }


        public bool IsGameOver => Hope <= 0;

        public void ChangeHope(int amount)
        {
            Hope = Math.Clamp(Hope + amount, 0, MaxHope);
        }


        public bool TrySpend(int food = 0, int planks = 0, int scraps = 0)
        {
            if (Food < food || Planks < planks || Scraps < scraps) return false;
            Food -= food;
            Planks -= planks;
            Scraps -= scraps;
            return true;
        }

        public void AddResources(int food = 0, int planks = 0, int scraps = 0)
        {
            Food += food;
            Planks += planks;
            Scraps += scraps;
        }
    }
}
