using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Media;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DuskAndDawn
{
    public enum Sfx
    {
        Slash,
        Pierce,
        Smash,
        BlacksmithCrafting,
        BandageCrafting,
        PotionCrafting,
        Cooking,
        RoomUpgrade,
        ItemLoot
    }

    public static class Sound
    {
        private const float MusicVolume = 0.4f;
        private const int RepeatGuardMs = 60;

        private static readonly Dictionary<Sfx, string[]> SfxFiles = new Dictionary<Sfx, string[]>
        {
            [Sfx.Slash] = new[] { "sfx_slash_1", "sfx_slash_2" },
            [Sfx.Pierce] = new[] { "sfx_pierce_1", "sfx_pierce_2" },
            [Sfx.Smash] = new[] { "sfx_smash_1", "sfx_smash_2" },
            [Sfx.BlacksmithCrafting] = new[] { "sfx_blacksmith_crafting" },
            [Sfx.BandageCrafting] = new[] { "sfx_bandage_crafting" },
            [Sfx.PotionCrafting] = new[] { "sfx_potion_crafting" },
            [Sfx.Cooking] = new[] { "sfx_cooking" },
            [Sfx.RoomUpgrade] = new[] { "sfx_room_upgrade" },
            [Sfx.ItemLoot] = new[] { "sfx_item_loot" }
        };

        private static readonly Dictionary<Sfx, SoundEffect[]> _effects = new Dictionary<Sfx, SoundEffect[]>();
        private static readonly Dictionary<Sfx, long> _lastPlayedAt = new Dictionary<Sfx, long>();
        private static readonly Random _random = new Random();
        private static Song _combatMusic;

        public static void LoadContent(ContentManager content)
        {
            try
            {
                foreach (var (sfx, files) in SfxFiles)
                {
                    _effects[sfx] = files.Select(file => content.Load<SoundEffect>("Audio/" + file)).ToArray();
                }
                _combatMusic = content.Load<Song>("Audio/bgm_combat_encounter");
                MediaPlayer.IsRepeating = true;
                MediaPlayer.Volume = MusicVolume;
            }
            catch (NoAudioHardwareException)
            {
                _effects.Clear();
                _combatMusic = null;
            }
        }

        public static void Play(Sfx sfx)
        {
            if (!_effects.TryGetValue(sfx, out var variants)) return;

            long now = Environment.TickCount64;
            if (_lastPlayedAt.TryGetValue(sfx, out long last) && now - last < RepeatGuardMs) return;
            _lastPlayedAt[sfx] = now;

            variants[_random.Next(variants.Length)].Play();
        }

        public static void PlayCombatMusic()
        {
            if (_combatMusic == null) return;
            if (MediaPlayer.State == MediaState.Playing && MediaPlayer.Queue.ActiveSong == _combatMusic) return;
            MediaPlayer.Play(_combatMusic);
        }

        public static void StopMusic()
        {
            if (_combatMusic == null || MediaPlayer.State == MediaState.Stopped) return;
            MediaPlayer.Stop();
        }
    }
}
