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
        public const int StartingHope = 80;
        public int Hope = StartingHope;

        public const int KnockoutHopeLoss = 12;
        public const int FleeHopeLoss = 2;
        public const int HoardHope = 4;
        public const int HeraldSlainHope = 12;

        public int Day = 1;

        public bool HeraldSlain;

        public bool FellToHerald;

        public List<NightRecord> Nights { get; } = new List<NightRecord>();

        public int EnemiesDefeated => Nights.Sum(n => n.EnemiesDefeated);

        public int MaxHealth = 100;
        public int Health = 100;

        public List<Weapon> Inventory { get; } = new List<Weapon>();
        public Weapon EquippedWeapon;

        public List<Item> Items { get; } = new List<Item>();
        public List<Item> Belt { get; } = new List<Item>();

        public const int MaxBeltSlots = 6;
        public int BeltSlots => Math.Min(MaxBeltSlots, 1 + Level(BaseRoomType.Storage));

        public bool BeltFull => Belt.Count >= BeltSlots;

        public string GainItemAtNight(Item item)
        {
            if (!BeltFull)
            {
                Belt.Add(item);
                return "on your belt";
            }
            Items.Add(item);
            return "your belt is full, so it goes in your bag for home";
        }

        public int OwnedItemCount(string name) => Items.Count(i => i.Name == name) + Belt.Count(i => i.Name == name);

        public Dictionary<BaseRoomType, int> RoomLevels { get; } = new Dictionary<BaseRoomType, int>();

        public District SelectedDistrict = District.VillageOutskirts;

        public bool FeastUsedToday;

        public string LastMorningEventTitle;

        public PlayerState()
        {
            foreach (BaseRoomType room in Enum.GetValues(typeof(BaseRoomType)))
            {
                RoomLevels[room] = 1;
            }

            var starterWeapon = Weapon.RustyKnife();
            Inventory.Add(starterWeapon);
            EquippedWeapon = starterWeapon;

            Belt.Add(Item.Bandage());
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

        public ResourceDelta Apply(ResourceDelta delta)
        {
            int food = Math.Max(-Food, delta.Food);
            int planks = Math.Max(-Planks, delta.Planks);
            int scraps = Math.Max(-Scraps, delta.Scraps);
            Food += food;
            Planks += planks;
            Scraps += scraps;

            int hopeBefore = Hope;
            ChangeHope(delta.Hope);

            return new ResourceDelta(food, planks, scraps, Hope - hopeBefore);
        }

        public void AddResources(int food = 0, int planks = 0, int scraps = 0)
        {
            Food += food;
            Planks += planks;
            Scraps += scraps;
        }

        public const int MaxRoomLevel = 5;

        public int Level(BaseRoomType room) => RoomLevels[room];

        public int StorageCap => Level(BaseRoomType.Storage) switch
        {
            1 => 30,
            2 => 50,
            3 => 70,
            4 => 90,
            _ => 120
        };

        public bool StoragePackframes => Level(BaseRoomType.Storage) >= 3;

        public bool StorageRootCellar => Level(BaseRoomType.Storage) >= 4;

        public float SupplyCacheMultiplier => Level(BaseRoomType.Storage) >= 5 ? 1.25f : 1f;

        public (int food, int planks, int scraps) ApplyStorageCap()
        {
            int cap = StorageCap;
            int lostFood = StorageRootCellar ? 0 : Math.Max(0, Food - cap);
            int lostPlanks = Math.Max(0, Planks - cap);
            int lostScraps = Math.Max(0, Scraps - cap);

            Food -= lostFood;
            Planks -= lostPlanks;
            Scraps -= lostScraps;

            return (lostFood, lostPlanks, lostScraps);
        }

        public int KitchenDailyFood => Level(BaseRoomType.Kitchen) switch { 1 => 3, 2 => 6, 3 => 9, 4 => 11, _ => 13 };

        public int FeastHope => Level(BaseRoomType.Kitchen) >= 5 ? 22 : 15;

        public const int BaseUpkeep = 3;
        public const int HungerBaseHope = 10;
        public const int HungerHopePerFood = 4;
        public const int HungerStreakHope = 5;

        public int HungryMornings;

        public int UpgradesBought => RoomLevels.Values.Sum(level => level - 1);

        public int DailyUpkeep => UpkeepFor(UpgradesBought, Level(BaseRoomType.Kitchen));

        public int UpkeepIfUpgraded(BaseRoomType room) =>
            UpkeepFor(UpgradesBought + 1, Level(BaseRoomType.Kitchen) + (room == BaseRoomType.Kitchen ? 1 : 0));

        private static int UpkeepFor(int upgrades, int kitchenLevel) =>
            Math.Max(BaseUpkeep, BaseUpkeep + upgrades - (kitchenLevel >= 4 ? 2 : 0));

        public (int eaten, int shortfall, int hopeLost) EatUpkeep()
        {
            int needed = DailyUpkeep;
            int eaten = Math.Min(Food, needed);
            Food -= eaten;

            int shortfall = needed - eaten;
            if (shortfall == 0)
            {
                HungryMornings = 0;
                return (eaten, 0, 0);
            }

            HungryMornings++;
            int hopeBefore = Hope;
            ChangeHope(-HungerHopeCost(shortfall, HungryMornings));
            return (eaten, shortfall, hopeBefore - Hope);
        }

        public static int HungerHopeCost(int shortfall, int hungryStreak) =>
            shortfall <= 0 ? 0 : HungerBaseHope + shortfall * HungerHopePerFood + Math.Max(0, hungryStreak - 1) * HungerStreakHope;

        public int ProjectedHungerCost()
        {
            int shortfall = Math.Max(0, DailyUpkeep - Food - KitchenDailyFood);
            return HungerHopeCost(shortfall, HungryMornings + 1);
        }

        public int MaxReinforcement => Math.Min(Weapon.MaxReinforcement, Level(BaseRoomType.Workshop));

        public int WorkshopEdge => Level(BaseRoomType.Workshop) >= 4 ? 1 : 0;

        public static (int food, int planks, int scraps) ReinforceCost(int nextLevel) => nextLevel switch
        {
            1 => (0, 1, 5),
            2 => (1, 3, 12),
            3 => (2, 5, 22),
            4 => (3, 7, 30),
            _ => (4, 9, 38)
        };

        public int NightMaxHealth => 100 + (Level(BaseRoomType.Infirmary) >= 4 ? 20 : 0) + (Level(BaseRoomType.Infirmary) >= 5 ? 20 : 0);

        public float RemedyPotency => Level(BaseRoomType.Infirmary) >= 5 ? 1.5f : 1f;

        public int BarracksRerolls => Level(BaseRoomType.Barrack) switch { 1 => 1, >= 5 => 3, _ => 2 };
        public int BarracksMinFace => Level(BaseRoomType.Barrack) switch { 1 => 1, >= 5 => 3, _ => 2 };
        public int BarracksExtraDice => Level(BaseRoomType.Barrack) >= 3 ? 1 : 0;

        public bool BarracksRiposte => Level(BaseRoomType.Barrack) >= 4;

        public int ArchiveRevealDepth => Math.Min(2, Level(BaseRoomType.Archive) - 1);

        public bool ArchiveBestiary => Level(BaseRoomType.Archive) >= 4;

        public int ArchiveExtraNightMinutes => Level(BaseRoomType.Archive) >= 5 ? 60 : 0;

        public bool ArchiveReaches(District district) =>
            district != District.Castle && Level(BaseRoomType.Archive) >= DistrictInfo.RequiredArchiveLevel(district);

        public bool IsDistrictUnlocked(District district)
        {
            if (DayInfo.IsFinalNight(Day)) return district == District.Castle;
            return ArchiveReaches(district);
        }
    }

    public class NightRecord
    {
        public int Day { get; set; }
        public District District { get; set; }
        public int HopeAtDusk { get; set; }
        public int HopeAtDawn { get; set; }
        public int Food { get; set; }
        public int Planks { get; set; }
        public int Scraps { get; set; }
        public int EnemiesDefeated { get; set; }
        public int RoomsExplored { get; set; }
        public bool KnockedOut { get; set; }

        public int Haul => Food + Planks + Scraps;
    }
}
