using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DuskAndDawn
{
    /// <summary>
    /// The run on disk. Saved each morning when you reach the base and again when you end
    /// the day, so quitting at any point resumes from the base on that day (a night in
    /// progress is replayed from the start). Deleted when the run ends, win or lose.
    /// Lives next to settings.json in the user's app-data folder.
    /// </summary>
    public static class SaveGame
    {
        private const int CurrentVersion = 1;

        private static string FilePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "7thDawn", "savegame.json");

        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        };

        public static bool Exists => File.Exists(FilePath);

        public static void Save(PlayerState state)
        {
            if (state == null || state.IsGameOver) return;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                File.WriteAllText(FilePath, JsonSerializer.Serialize(SaveData.From(state), Options));
            }
            catch (Exception)
            {
                // Read-only or missing app-data folder: the run just isn't saved.
            }
        }

        /// <summary>The saved run, or null if there isn't one or it can't be read.</summary>
        public static PlayerState Load() => Read()?.ToPlayerState();

        /// <summary>Just the day of the saved run, for the main menu's Continue label.</summary>
        public static int? SavedDay => Read()?.Day;

        public static void Delete()
        {
            try
            {
                if (File.Exists(FilePath)) File.Delete(FilePath);
            }
            catch (Exception)
            {
                // Nothing useful to do - worst case, Continue offers a finished run once more.
            }
        }

        private static SaveData Read()
        {
            try
            {
                if (!File.Exists(FilePath)) return null;
                var data = JsonSerializer.Deserialize<SaveData>(File.ReadAllText(FilePath), Options);
                return data != null && data.Version == CurrentVersion ? data : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        // ---- File format ----

        public class SavedWeapon
        {
            public string Name { get; set; }
            public int Reinforcement { get; set; }
        }

        public class SaveData
        {
            public int Version { get; set; } = CurrentVersion;
            public DateTime SavedAt { get; set; }

            public int Day { get; set; }
            public int Hope { get; set; }
            public int Food { get; set; }
            public int Planks { get; set; }
            public int Scraps { get; set; }
            public int HungryMornings { get; set; }
            public bool FeastUsedToday { get; set; }
            public bool KnightSlain { get; set; }
            public string LastMorningEventTitle { get; set; }
            public District SelectedDistrict { get; set; }
            public Dictionary<BaseRoomType, int> RoomLevels { get; set; } = new Dictionary<BaseRoomType, int>();
            public List<SavedWeapon> Weapons { get; set; } = new List<SavedWeapon>();
            public int EquippedIndex { get; set; }
            public List<string> Items { get; set; } = new List<string>();

            public static SaveData From(PlayerState state) => new SaveData
            {
                SavedAt = DateTime.Now,
                Day = state.Day,
                Hope = state.Hope,
                Food = state.Food,
                Planks = state.Planks,
                Scraps = state.Scraps,
                HungryMornings = state.HungryMornings,
                FeastUsedToday = state.FeastUsedToday,
                KnightSlain = state.KnightSlain,
                LastMorningEventTitle = state.LastMorningEventTitle,
                SelectedDistrict = state.SelectedDistrict,
                RoomLevels = new Dictionary<BaseRoomType, int>(state.RoomLevels),
                Weapons = state.Inventory.Select(w => new SavedWeapon { Name = w.Name, Reinforcement = w.Reinforcement }).ToList(),
                EquippedIndex = state.Inventory.IndexOf(state.EquippedWeapon),
                Items = state.Items.Select(i => i.Name).ToList()
            };

            public PlayerState ToPlayerState()
            {
                var state = new PlayerState
                {
                    Day = Math.Clamp(Day, 1, DayInfo.FinalDay),
                    Hope = Math.Clamp(Hope, 1, PlayerState.MaxHope),
                    Food = Math.Max(0, Food),
                    Planks = Math.Max(0, Planks),
                    Scraps = Math.Max(0, Scraps),
                    HungryMornings = Math.Max(0, HungryMornings),
                    FeastUsedToday = FeastUsedToday,
                    KnightSlain = KnightSlain,
                    LastMorningEventTitle = LastMorningEventTitle,
                    SelectedDistrict = SelectedDistrict
                };

                foreach (var (room, level) in RoomLevels)
                {
                    state.RoomLevels[room] = Math.Clamp(level, 1, PlayerState.MaxRoomLevel);
                }

                // The constructor hands out a starter kit - replace it with what was saved,
                // skipping anything this version of the game doesn't know.
                var weapons = new List<Weapon>();
                Weapon equipped = null;
                for (int i = 0; i < Weapons.Count; i++)
                {
                    var weapon = Weapon.Create(Weapons[i].Name, Weapons[i].Reinforcement);
                    if (weapon == null) continue;
                    weapons.Add(weapon);
                    if (i == EquippedIndex) equipped = weapon;
                }
                if (weapons.Count > 0)
                {
                    state.Inventory.Clear();
                    state.Inventory.AddRange(weapons);
                    state.EquippedWeapon = equipped ?? weapons[0];
                }

                state.Items.Clear();
                state.Items.AddRange(Items.Select(Item.Create).Where(item => item != null));
                return state;
            }
        }
    }
}
