using Microsoft.Xna.Framework;
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

    public enum Music
    {
        Ambiance,
        Combat
    }

    public static class Sound
    {
        public const int VolumeStep = 10;
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

        private static readonly Dictionary<Music, (string file, float mix)> MusicFiles = new Dictionary<Music, (string, float)>
        {
            [Music.Ambiance] = ("bgm_ambiance", 0.8f),
            [Music.Combat] = ("bgm_combat_encounter", 0.4f)
        };

        private static readonly Dictionary<Sfx, SoundEffect[]> _effects = new Dictionary<Sfx, SoundEffect[]>();
        private static readonly Dictionary<Music, Song> _songs = new Dictionary<Music, Song>();
        private static readonly Dictionary<Sfx, long> _lastPlayedAt = new Dictionary<Sfx, long>();
        private static readonly Random _random = new Random();
        private static Music? _currentMusic;

        public static void LoadContent(ContentManager content)
        {
            try
            {
                foreach (var (sfx, files) in SfxFiles)
                {
                    _effects[sfx] = files.Select(file => content.Load<SoundEffect>("Audio/" + file)).ToArray();
                }
                foreach (var (music, (file, _)) in MusicFiles)
                {
                    _songs[music] = content.Load<Song>("Audio/" + file);
                }
                MediaPlayer.IsRepeating = true;
                ApplyVolume();
            }
            catch (NoAudioHardwareException)
            {
                _effects.Clear();
                _songs.Clear();
            }
        }

        public static void ApplyVolume()
        {
            if (_songs.Count == 0) return;
            SoundEffect.MasterVolume = MathHelper.Clamp(GameSettings.Current.SoundVolume / 100f, 0f, 1f);
            if (_currentMusic is Music music)
            {
                MediaPlayer.Volume = MusicFiles[music].mix * MathHelper.Clamp(GameSettings.Current.MusicVolume / 100f, 0f, 1f);
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

        public static void PlayMusic(Music music)
        {
            if (_currentMusic == music || !_songs.TryGetValue(music, out var song)) return;
            _currentMusic = music;
            ApplyVolume();
            MediaPlayer.Play(song);
        }
    }
}
