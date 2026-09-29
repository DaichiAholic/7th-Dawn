using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DuskAndDawn
{
    /// <summary>
    /// Player options from the Settings screen. Saved as JSON in the user's app-data folder
    /// so they survive restarts; a missing or broken file just falls back to the defaults.
    /// </summary>
    public class GameSettings
    {
        public bool Fullscreen { get; set; } = true;
        public bool ScreenShake { get; set; } = true;
        public bool FastCombat { get; set; }

        /// <summary>Multiplier for combat animation lengths (and the action lock that waits on them).</summary>
        [JsonIgnore]
        public float CombatAnimScale => FastCombat ? 0.55f : 1f;

        public static GameSettings Current { get; private set; } = new GameSettings();

        private static string FilePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "7thDawn", "settings.json");

        public static void Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    Current = JsonSerializer.Deserialize<GameSettings>(File.ReadAllText(FilePath)) ?? new GameSettings();
                }
            }
            catch (Exception)
            {
                // Unreadable settings shouldn't stop the game from starting.
                Current = new GameSettings();
            }
        }

        public static void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                File.WriteAllText(FilePath, JsonSerializer.Serialize(Current, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception)
            {
                // Read-only or missing app-data folder: keep the settings for this session only.
            }
        }
    }
}
