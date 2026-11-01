using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Media;
using System;
using System.Collections.Generic;
using System.IO;
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
        ItemLoot,
        BossAttack,
        BossSkill,
        PenitentAttack,
        PenitentSkill,
        KnightWretchAttack
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

        private const int MaxVoices = 10;
        private const float ChokeFade = 0.06f;
        private const float PitchVariation = 0.04f;
        private const float VolumeVariation = 0.1f;
        private const float MusicFadeOut = 0.6f;

        private const float HitLoudness = -14f;
        private const float EnemyLoudness = -14f;
        private const float BossLoudness = -12f;
        private const float HouseLoudness = -18f;

        private static readonly Dictionary<Sfx, (string[] files, float loudness, float pitch)> SfxFiles = new Dictionary<Sfx, (string[], float, float)>
        {
            [Sfx.Slash] = (new[] { "sfx_slash_1", "sfx_slash_2" }, HitLoudness, 0f),
            [Sfx.Pierce] = (new[] { "sfx_pierce_1", "sfx_pierce_2" }, HitLoudness, 0f),
            [Sfx.Smash] = (new[] { "sfx_smash_1", "sfx_smash_2" }, HitLoudness, 0f),
            [Sfx.BlacksmithCrafting] = (new[] { "sfx_blacksmith_crafting" }, HouseLoudness, 0f),
            [Sfx.BandageCrafting] = (new[] { "sfx_bandage_crafting" }, HouseLoudness, 0f),
            [Sfx.PotionCrafting] = (new[] { "sfx_potion_crafting" }, HouseLoudness, 0f),
            [Sfx.Cooking] = (new[] { "sfx_cooking" }, HouseLoudness, 0f),
            [Sfx.RoomUpgrade] = (new[] { "sfx_room_upgrade" }, HouseLoudness, 0f),
            [Sfx.ItemLoot] = (new[] { "sfx_item_loot" }, HouseLoudness, 0f),
            [Sfx.BossAttack] = (new[] { "sfx_boss_attack" }, BossLoudness, -0.25f),
            [Sfx.BossSkill] = (new[] { "sfx_boss_skill" }, BossLoudness, 0f),
            [Sfx.PenitentAttack] = (new[] { "sfx_penitent_attack" }, EnemyLoudness, 0f),
            [Sfx.PenitentSkill] = (new[] { "sfx_penitent_skill" }, EnemyLoudness, 0f),
            [Sfx.KnightWretchAttack] = (new[] { "sfx_knight_wretch_attack" }, EnemyLoudness, 0f)
        };

        private static readonly Dictionary<Music, (string file, float mix, float fadeIn)> MusicFiles = new Dictionary<Music, (string, float, float)>
        {
            [Music.Ambiance] = ("bgm_ambiance", 0.8f, 2f),
            [Music.Combat] = ("bgm_combat_encounter", 0.4f, 0.3f)
        };

        private sealed class Voice
        {
            public Sfx Group;
            public SoundEffectInstance Instance;
            public float Volume;
            public bool Fading;
            public float FadeLeft;
        }

        private static readonly Dictionary<Sfx, SoundEffect[]> _effects = new Dictionary<Sfx, SoundEffect[]>();
        private static readonly Dictionary<Music, Song> _songs = new Dictionary<Music, Song>();
        private static readonly Dictionary<Sfx, long> _lastPlayedAt = new Dictionary<Sfx, long>();
        private static readonly Dictionary<Sfx, int> _lastVariant = new Dictionary<Sfx, int>();
        private static readonly List<Voice> _voices = new List<Voice>();
        private static readonly Random _random = new Random();
        private static Music? _currentMusic, _wantedMusic;
        private static float _musicFade;

        public static void LoadContent(ContentManager content)
        {
            try
            {
                foreach (var (sfx, (files, loudness, _)) in SfxFiles)
                {
                    _effects[sfx] = files.Select(file => LoadClip(content, "Audio/" + file, loudness)).ToArray();
                }
                foreach (var (music, (file, _, _)) in MusicFiles)
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
            ApplyMusicVolume();
        }

        private static void ApplyMusicVolume()
        {
            if (_currentMusic is Music music)
            {
                MediaPlayer.Volume = MusicFiles[music].mix * _musicFade * MathHelper.Clamp(GameSettings.Current.MusicVolume / 100f, 0f, 1f);
            }
        }

        public static void Play(Sfx sfx, float pitch = 0f)
        {
            if (!_effects.TryGetValue(sfx, out var variants)) return;

            long now = Environment.TickCount64;
            if (_lastPlayedAt.TryGetValue(sfx, out long last) && now - last < RepeatGuardMs) return;
            _lastPlayedAt[sfx] = now;

            var group = ChokeGroup(sfx);
            foreach (var voice in _voices)
            {
                if (voice.Group == group) FadeOut(voice);
            }
            var playing = _voices.Where(v => !v.Fading).ToList();
            if (playing.Count >= MaxVoices) FadeOut(playing[0]);

            var instance = variants[PickVariant(sfx, variants.Length)].CreateInstance();
            float volume = 1f - (float)_random.NextDouble() * VolumeVariation;
            instance.Volume = volume;
            instance.Pitch = MathHelper.Clamp(SfxFiles[sfx].pitch + pitch + ((float)_random.NextDouble() * 2f - 1f) * PitchVariation, -1f, 1f);
            try
            {
                instance.Play();
            }
            catch (InstancePlayLimitException)
            {
                instance.Dispose();
                return;
            }
            _voices.Add(new Voice { Group = group, Instance = instance, Volume = volume });
        }

        private static Sfx ChokeGroup(Sfx sfx) => sfx is Sfx.Pierce or Sfx.Smash ? Sfx.Slash : sfx;

        private static int PickVariant(Sfx sfx, int count)
        {
            int index = _random.Next(count);
            if (count > 1 && _lastVariant.TryGetValue(sfx, out int previous) && index == previous)
            {
                index = (index + 1 + _random.Next(count - 1)) % count;
            }
            _lastVariant[sfx] = index;
            return index;
        }

        private static void FadeOut(Voice voice)
        {
            if (voice.Fading) return;
            voice.Fading = true;
            voice.FadeLeft = ChokeFade;
        }

        public static void PlayMusic(Music music) => _wantedMusic = music;

        public static void Update(float dt)
        {
            for (int i = _voices.Count - 1; i >= 0; i--)
            {
                var voice = _voices[i];
                if (voice.Fading)
                {
                    voice.FadeLeft -= dt;
                    voice.Instance.Volume = voice.Volume * MathHelper.Clamp(voice.FadeLeft / ChokeFade, 0f, 1f);
                }
                if (voice.Instance.State == SoundState.Stopped || voice.Fading && voice.FadeLeft <= 0f)
                {
                    voice.Instance.Stop();
                    voice.Instance.Dispose();
                    _voices.RemoveAt(i);
                }
            }

            if (_wantedMusic is not Music wanted || !_songs.TryGetValue(wanted, out var song)) return;
            if (_currentMusic != wanted)
            {
                _musicFade -= dt / MusicFadeOut;
                if (_currentMusic == null || _musicFade <= 0f)
                {
                    _currentMusic = wanted;
                    _musicFade = 0f;
                    MediaPlayer.Play(song);
                }
            }
            else
            {
                _musicFade = Math.Min(1f, _musicFade + dt / MusicFiles[wanted].fadeIn);
            }
            ApplyMusicVolume();
        }

        // The content pipeline stores each effect as plain 16-bit PCM, so it is read back here to
        // cut the silence around it (some clips carry seconds of it) and level it to a loudness.
        private static SoundEffect LoadClip(ContentManager content, string asset, float loudness)
        {
            try
            {
                using var reader = new BinaryReader(TitleContainer.OpenStream(Path.Combine(content.RootDirectory, asset + ".xnb")));
                if (new string(reader.ReadChars(3)) != "XNB") throw new InvalidDataException(asset);
                reader.ReadBytes(2);
                if ((reader.ReadByte() & 0xC0) != 0) throw new InvalidDataException(asset + " is compressed");
                reader.ReadInt32();
                int readers = reader.Read7BitEncodedInt();
                for (int i = 0; i < readers; i++)
                {
                    reader.ReadString();
                    reader.ReadInt32();
                }
                reader.Read7BitEncodedInt();
                reader.Read7BitEncodedInt();

                var format = reader.ReadBytes(reader.ReadInt32());
                if (BitConverter.ToUInt16(format, 0) != 1 || BitConverter.ToUInt16(format, 14) != 16) throw new InvalidDataException(asset + " is not 16-bit PCM");
                int channels = BitConverter.ToUInt16(format, 2);
                int rate = BitConverter.ToInt32(format, 4);
                var bytes = reader.ReadBytes(reader.ReadInt32());
                var samples = new short[bytes.Length / 2];
                Buffer.BlockCopy(bytes, 0, samples, 0, samples.Length * 2);
                return Shape(samples, rate, channels, loudness);
            }
            catch (Exception e) when (e is IOException or InvalidDataException or ArgumentException)
            {
                return content.Load<SoundEffect>(asset);
            }
        }

        private static SoundEffect Shape(short[] samples, int rate, int channels, float loudness)
        {
            int frames = samples.Length / channels;
            float FrameLevel(int frame)
            {
                int level = 0;
                for (int c = 0; c < channels; c++) level = Math.Max(level, Math.Abs((int)samples[frame * channels + c]));
                return level / 32768f;
            }

            float Rms(int from, int to)
            {
                double sum = 0;
                for (int i = from * channels; i < to * channels; i++) sum += (double)samples[i] * samples[i];
                return (float)(Math.Sqrt(sum / Math.Max(1, (to - from) * channels)) / 32768.0);
            }

            float peak = 0f;
            for (int f = 0; f < frames; f++) peak = Math.Max(peak, FrameLevel(f));
            if (peak <= 0f) return new SoundEffect(ToBytes(samples), rate, (AudioChannels)channels);

            int step = rate / 100;
            int first = 0;
            while (first + step < frames && Rms(first, first + step) < peak * 0.01f) first += step;
            int last = frames - 1;
            while (last > first && FrameLevel(last) < peak * 0.0032f) last--;
            int start = Math.Max(0, first - step);
            int end = Math.Min(frames, last + rate * 8 / 100);

            int window = rate / 10;
            float loudest = 0f;
            for (int from = start; from < end; from += window / 4)
            {
                loudest = Math.Max(loudest, Rms(from, Math.Min(end, from + window)));
            }
            float gain = (float)Math.Pow(10, (loudness - 20 * Math.Log10(Math.Max(loudest, 1e-6))) / 20);
            gain = Math.Min(gain, 0.89f / peak);

            int fadeIn = start > 0 ? rate * 3 / 1000 : 0;
            int fadeOut = rate * 40 / 1000;
            var shaped = new short[(end - start) * channels];
            for (int f = start; f < end; f++)
            {
                float envelope = 1f;
                if (f - start < fadeIn) envelope = (f - start) / (float)fadeIn;
                if (end - f < fadeOut) envelope = Math.Min(envelope, (end - f) / (float)fadeOut);
                for (int c = 0; c < channels; c++)
                {
                    float value = samples[f * channels + c] * gain * envelope;
                    shaped[(f - start) * channels + c] = (short)Math.Clamp(value, short.MinValue, short.MaxValue);
                }
            }
            return new SoundEffect(ToBytes(shaped), rate, (AudioChannels)channels);
        }

        private static byte[] ToBytes(short[] samples)
        {
            var bytes = new byte[samples.Length * 2];
            Buffer.BlockCopy(samples, 0, bytes, 0, bytes.Length);
            return bytes;
        }
    }
}
