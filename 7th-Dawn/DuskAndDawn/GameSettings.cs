using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DuskAndDawn
{
    public class GameSettings
    {
        public bool Fullscreen { get; set; } = true;
        public bool ScreenShake { get; set; } = true;
        public bool FastCombat { get; set; }
        public int MusicVolume { get; set; } = 100;
        public int SoundVolume { get; set; } = 100;

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
            }
        }
    }
}
