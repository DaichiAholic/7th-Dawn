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

        // ---- Hope: lose it all and the run is over ----
        // Every Hope rule lives here. It starts short of full so gains always count, and it
        // drains steadily: dread each dawn (DayInfo.DawnDread, 3 rising to 8), hunger (below),
        // knockouts and running away. Feasts, generous morning choices, reaching the Hoard and
        // slaying the Knight are how it comes back.
        public const int MaxHope = 100;
        public const int StartingHope = 80;
        public int Hope = StartingHope;

        public const int KnockoutHopeLoss = 12;  // dragged home half-dead
        public const int FleeHopeLoss = 2;       // every retreat shakes the house a little
        public const int HoardHope = 4;          // a find like the Hoard gives everyone heart
        public const int KnightSlainHope = 12;

        // ---- The goal: reach the seventh dawn ----
        // Day 1 is the first day at the base; its night is night 1. Surviving night 7 wins.
        public int Day = 1;

        // Slain the Hollow Knight on the final night - the better ending.
        public bool KnightSlain;

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

        // Title of yesterday's morning event, so the same one doesn't come up twice in a row.
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

        /// <summary>Applies a signed change to every resource and Hope. Losses stop at zero
        /// (and Hope at its max), so this returns what actually changed - use that for any
        /// "+8 Food" readout rather than the requested delta.</summary>
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
        public int KitchenDailyFood => Level(BaseRoomType.Kitchen) * 3;

        // ---- Upkeep: the household eats every morning ----
        // A bare base eats BaseUpkeep Food, and every room level bought on top of Lv 1 is
        // another mouth to feed, so growing the base always costs Food on top of its price.
        // Going hungry is brutal: any shortfall costs a flat chunk of Hope, more for every
        // missing Food, and more again for each hungry morning in a row.
        public const int BaseUpkeep = 3;
        public const int HungerBaseHope = 10;
        public const int HungerHopePerFood = 4;
        public const int HungerStreakHope = 5;

        // Consecutive mornings the household couldn't be fed. Reset by a full meal.
        public int HungryMornings;

        public int UpgradesBought => RoomLevels.Values.Sum(level => level - 1);

        public int DailyUpkeep => BaseUpkeep + UpgradesBought;

        /// <summary>Feeds the household for the day. Returns how much was eaten, how much
        /// was missing, and the Hope that hunger cost.</summary>
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

        /// <summary>Hope lost for going `shortfall` Food short on the Nth hungry morning
        /// in a row. Also used to warn the player ahead of time.</summary>
        public static int HungerHopeCost(int shortfall, int hungryStreak) =>
            shortfall <= 0 ? 0 : HungerBaseHope + shortfall * HungerHopePerFood + Math.Max(0, hungryStreak - 1) * HungerStreakHope;

        /// <summary>What tomorrow morning's hunger would cost if nothing else changes -
        /// for the warnings on the base screen.</summary>
        public int ProjectedHungerCost()
        {
            int shortfall = Math.Max(0, DailyUpkeep - Food - KitchenDailyFood);
            return HungerHopeCost(shortfall, HungryMornings + 1);
        }

        // ---- Workshop: weapon reinforcement ----
        // The Workshop level caps how far a weapon can be reinforced (+1 per level).
        public int MaxReinforcement => Math.Min(Weapon.MaxReinforcement, Level(BaseRoomType.Workshop));

        /// <summary>Cost to take a weapon to the given reinforcement level. Scraps-heavy and
        /// climbing steeply - the long-term sink for what fights drop.</summary>
        public static (int food, int planks, int scraps) ReinforceCost(int nextLevel) => nextLevel switch
        {
            1 => (0, 1, 5),
            2 => (1, 3, 12),
            _ => (2, 5, 22)
        };

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