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
        // slaying the Sun Herald are how it comes back.
        public const int MaxHope = 100;
        public const int StartingHope = 80;
        public int Hope = StartingHope;

        public const int KnockoutHopeLoss = 12;  // dragged home half-dead
        public const int FleeHopeLoss = 2;       // every retreat shakes the house a little
        public const int HoardHope = 4;          // a find like the Hoard gives everyone heart
        public const int HeraldSlainHope = 12;

        // ---- The goal: reach the seventh dawn ----
        // Day 1 is the first day at the base; its night is night 1. Surviving night 7 wins.
        public int Day = 1;

        // Slew the Sun Herald on the final night - the better ending.
        public bool HeraldSlain;

        // Fell to the Sun Herald - the run ends on the spot, whatever Hope is left.
        public bool FellToHerald;

        // ---- The run so far, for the summary at the end ----
        /// <summary>One entry per night played, added as the night ends.</summary>
        public List<NightRecord> Nights { get; } = new List<NightRecord>();

        public int EnemiesDefeated => Nights.Sum(n => n.EnemiesDefeated);

        public int MaxHealth = 100;
        public int Health = 100;

        public List<Weapon> Inventory { get; } = new List<Weapon>();
        public Weapon EquippedWeapon;

        // ---- Items: the stash at home, and the belt you carry ----
        // Crafted remedies go into the stash. Only what's on the belt comes into the night,
        // and the belt holds BeltSlots items - so every night starts with a packing decision.
        // Belt slots grow with the Storage room: better racks and pouches, one more slot a level.
        public List<Item> Items { get; } = new List<Item>();
        public List<Item> Belt { get; } = new List<Item>();

        public const int MaxBeltSlots = 6;
        public int BeltSlots => Math.Min(MaxBeltSlots, 1 + Level(BaseRoomType.Storage)); // 2 at Lv 1 ... 6 at Lv 5

        public bool BeltFull => Belt.Count >= BeltSlots;

        /// <summary>A remedy picked up at night goes on the belt if there's room, otherwise
        /// into your bag to be stashed at home. Returns where it went, for the log.</summary>
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

        /// <summary>How many of an item you have anywhere - stash and belt together.</summary>
        public int OwnedItemCount(string name) => Items.Count(i => i.Name == name) + Belt.Count(i => i.Name == name);

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

        // Every room runs Lv 1-5. Each level does something you can feel - a bigger number
        // AND, from Lv 3 on, a new ability - and with seven days you can't max everything,
        // so the rooms you push are the run you play.
        public const int MaxRoomLevel = 5;

        public int Level(BaseRoomType room) => RoomLevels[room];

        // ---- Storage: how much you keep, and how you haul it ----
        public int StorageCap => Level(BaseRoomType.Storage) switch
        {
            1 => 30,
            2 => 50,
            3 => 70,
            4 => 90,
            _ => 120
        };

        /// <summary>Lv 3 packframes: "Grab what's in reach" takes the whole cache.</summary>
        public bool StoragePackframes => Level(BaseRoomType.Storage) >= 3;

        /// <summary>Lv 4 root cellar: Food is never trimmed by the Storage cap.</summary>
        public bool StorageRootCellar => Level(BaseRoomType.Storage) >= 4;

        /// <summary>Lv 5 supply runs: every supply cache holds this much more.</summary>
        public float SupplyCacheMultiplier => Level(BaseRoomType.Storage) >= 5 ? 1.25f : 1f;

        /// <summary>Trims each resource down to the Storage cap and returns how much was
        /// lost, so the caller can tell the player.</summary>
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

        // ---- Kitchen: Food each morning, a leaner household, and feasts ----
        public int KitchenDailyFood => Level(BaseRoomType.Kitchen) switch { 1 => 3, 2 => 6, 3 => 9, 4 => 11, _ => 13 };

        /// <summary>Hope from a Feast (Lv 3), grander at Lv 5.</summary>
        public int FeastHope => Level(BaseRoomType.Kitchen) >= 5 ? 22 : 15;

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

        public int DailyUpkeep => UpkeepFor(UpgradesBought, Level(BaseRoomType.Kitchen));

        /// <summary>What the daily upkeep would become after upgrading `room` - the Kitchen's
        /// smokehouse can make an upgrade lower it.</summary>
        public int UpkeepIfUpgraded(BaseRoomType room) =>
            UpkeepFor(UpgradesBought + 1, Level(BaseRoomType.Kitchen) + (room == BaseRoomType.Kitchen ? 1 : 0));

        private static int UpkeepFor(int upgrades, int kitchenLevel) =>
            Math.Max(BaseUpkeep, BaseUpkeep + upgrades - (kitchenLevel >= 4 ? 2 : 0));

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

        // ---- Workshop: weapons, reinforcement and a whetstone ----
        // The Workshop level caps how far a weapon can be reinforced (+1 per level).
        public int MaxReinforcement => Math.Min(Weapon.MaxReinforcement, Level(BaseRoomType.Workshop));

        /// <summary>Lv 4 whetstone: every weapon roll deals this much more.</summary>
        public int WorkshopEdge => Level(BaseRoomType.Workshop) >= 4 ? 1 : 0;

        /// <summary>Cost to take a weapon to the given reinforcement level. Scraps-heavy and
        /// climbing steeply - the long-term sink for what fights drop.</summary>
        public static (int food, int planks, int scraps) ReinforceCost(int nextLevel) => nextLevel switch
        {
            1 => (0, 1, 5),
            2 => (1, 3, 12),
            3 => (2, 5, 22),
            4 => (3, 7, 30),
            _ => (4, 9, 38)
        };

        // ---- Infirmary: remedies, and a tougher you ----
        /// <summary>Health for tonight: +20 at Lv 4 (field kit) and again at Lv 5.</summary>
        public int NightMaxHealth => 100 + (Level(BaseRoomType.Infirmary) >= 4 ? 20 : 0) + (Level(BaseRoomType.Infirmary) >= 5 ? 20 : 0);

        /// <summary>Lv 5 surgeon's hands: healing remedies restore this much more.</summary>
        public float RemedyPotency => Level(BaseRoomType.Infirmary) >= 5 ? 1.5f : 1f;

        // ---- Barracks: dice training ----
        public int BarracksRerolls => Level(BaseRoomType.Barrack) switch { 1 => 1, >= 5 => 3, _ => 2 };
        public int BarracksMinFace => Level(BaseRoomType.Barrack) switch { 1 => 1, >= 5 => 3, _ => 2 };
        public int BarracksExtraDice => Level(BaseRoomType.Barrack) >= 3 ? 1 : 0;

        /// <summary>Lv 4 riposte: blocking a HEAVY blow or a STUN with Guard strikes back.</summary>
        public bool BarracksRiposte => Level(BaseRoomType.Barrack) >= 4;

        // ---- Archive: districts, scouting, a bestiary and old roads ----
        public int ArchiveRevealDepth => Math.Min(2, Level(BaseRoomType.Archive) - 1);

        /// <summary>Lv 4 bestiary: scouted enemy rooms show exactly what's waiting inside.</summary>
        public bool ArchiveBestiary => Level(BaseRoomType.Archive) >= 4;

        /// <summary>Lv 5 old roads: extra minutes before dawn each night.</summary>
        public int ArchiveExtraNightMinutes => Level(BaseRoomType.Archive) >= 5 ? 60 : 0;

        /// <summary>Whether the Archive's maps reach this district (The Castle is never on them).</summary>
        public bool ArchiveReaches(District district) =>
            district != District.Castle && Level(BaseRoomType.Archive) >= DistrictInfo.RequiredArchiveLevel(district);

        /// <summary>Where you can go tonight. The last night belongs to The Castle - it's the
        /// only way in, and the only night it opens.</summary>
        public bool IsDistrictUnlocked(District district)
        {
            if (DayInfo.IsFinalNight(Day)) return district == District.Castle;
            return ArchiveReaches(district);
        }
    }

    /// <summary>How one night went - kept for the run summary on the Game Over screen.</summary>
    public class NightRecord
    {
        public int Day { get; set; }
        public District District { get; set; }
        /// <summary>Hope when you set out, before anything tonight changed it.</summary>
        public int HopeAtDusk { get; set; }
        /// <summary>Hope when the night ended.</summary>
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
