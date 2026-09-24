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

        // Chosen on the Prepare screen, read by the Night screen.
        public District SelectedDistrict = District.VillageOutskirts;

        // Kitchen Lv 3 Feast can only be held once per day. Reset each morning.
        public bool FeastUsedToday;

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

        public bool CanAfford(int food = 0, int planks = 0, int scraps = 0)
        {
            return Food >= food && Planks >= planks && Scraps >= scraps;
        }

        // Deliberately NOT capped - the night haul can go over the Storage cap, and the
        // overflow is only lost at Dawn (see ApplyStorageCap). That's what makes "one more
        // room?" a real question even when a run is going well.
        public void AddResources(int food = 0, int planks = 0, int scraps = 0)
        {
            Food += food;
            Planks += planks;
            Scraps += scraps;
        }

        // =====================================================================
        // Room effects - every "what does this room do at this level" number lives here,
        // so screens just read a property instead of switching on levels themselves.
        // =====================================================================

        public int Level(BaseRoomType room) => RoomLevels[room];

        // ---- Storage: max of each resource ----
        public int StorageCap => Level(BaseRoomType.Storage) switch
        {
            1 => 30,
            2 => 60,
            _ => 100
        };

        /// <summary>Trims each resource down to the Storage cap and returns how much was
        /// lost, so the caller can tell the player.</summary>
        public (int food, int planks, int scraps) ApplyStorageCap()
        {
            int cap = StorageCap;
            int lostFood = Math.Max(0, Food - cap);
            int lostPlanks = Math.Max(0, Planks - cap);
            int lostScraps = Math.Max(0, Scraps - cap);

            Food -= lostFood;
            Planks -= lostPlanks;
            Scraps -= lostScraps;

            return (lostFood, lostPlanks, lostScraps);
        }

        // ---- Kitchen: free Food each morning ----
        public int KitchenDailyFood => Level(BaseRoomType.Kitchen) * 2;

        // ---- Barracks: dice training ----
        public int BarracksRerolls => Level(BaseRoomType.Barrack) >= 2 ? 2 : 1;
        public int BarracksMinFace => Level(BaseRoomType.Barrack) >= 2 ? 2 : 1;
        public int BarracksExtraDice => Level(BaseRoomType.Barrack) >= 3 ? 1 : 0;

        // ---- Archive: districts + minimap scouting ----
        public int ArchiveRevealDepth => Level(BaseRoomType.Archive) - 1;

        public bool IsDistrictUnlocked(District district)
        {
            return Level(BaseRoomType.Archive) >= DistrictInfo.RequiredArchiveLevel(district);
        }
    }
}