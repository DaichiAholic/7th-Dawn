using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace DuskAndDawn
{
    /// <summary>
    /// A bar whose displayed value smoothly chases a real target value over time, instead of
    /// snapping to it instantly. Use this for anything you want to feel "juicy" when it
    /// changes (health bars) - keep plain instant bars (like the dawn clock) for things that
    /// should read as exact/immediate instead.
    ///
    /// This only affects rendering - it never touches the real value (PlayerState.Health,
    /// Enemy.Health, etc.), which stays instant for game logic. Call Update() once per frame
    /// with the current real value, then read Ratio when drawing.
    /// </summary>
    public class LerpBar
    {
        private const float CatchUpSpeed = 4f; // higher = the bar catches up to the real value faster

        public float DisplayedValue { get; private set; }
        public float MaxValue { get; }

        public LerpBar(float initialValue, float maxValue)
        {
            DisplayedValue = initialValue;
            MaxValue = maxValue;
        }

        public void Update(GameTime gameTime, float targetValue)
        {
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            float t = Math.Min(1f, CatchUpSpeed * dt);
            DisplayedValue = MathHelper.Lerp(DisplayedValue, targetValue, t);
        }

        /// <summary>0-1 fill ratio, ready to multiply against a bar's max width.</summary>
        public float Ratio => MaxValue <= 0 ? 0f : MathHelper.Clamp(DisplayedValue / MaxValue, 0f, 1f);
    }

    /// <summary>
    /// A one-shot frame animation played from a horizontal spritesheet of square frames
    /// (Attack.png, Skill.png, Attacked.png - 7 frames of 64x64 each). The frame size is read
    /// from the texture height, so a sheet with a different frame count just works.
    /// Drawn centered on a point, at a whole-number scale with point sampling so the pixel
    /// art stays crisp. One instance is reused per "slot" (enemy hit, player hit, cast) -
    /// calling Play again restarts it, so there's never more than one of a given effect at once.
    /// </summary>
    public class SpriteEffect
    {
        private Texture2D _texture;
        private int _frameSize;
        private int _frameCount;
        private Vector2 _center;
        private int _scale;
        private float _duration;
        private float _delay;          // seconds to wait before the first frame shows
        private float _elapsed = -1f;  // negative = not playing

        public bool IsPlaying => _elapsed >= 0f;

        /// <summary>Cuts the animation off immediately (used when a fight ends).</summary>
        public void Stop()
        {
            _elapsed = -1f;
            _delay = 0f;
        }

        /// <param name="duration">Total time for all frames, in seconds.</param>
        /// <param name="scale">Whole-number pixel scale (2 = 64px frames drawn at 128px).</param>
        /// <param name="delay">Optional wait before it starts - used to land the enemy's
        /// hit reaction a beat after the slash begins instead of on top of it.</param>
        public const int FrameSize = 64;

        public void Play(Texture2D texture, Vector2 center, float duration, int scale, float delay = 0f)
        {
            if (texture == null) return;

            _texture = texture;
            _frameSize = FrameSize;
            _frameCount = Math.Max(1, texture.Width / FrameSize);
            _center = center;
            _duration = duration;
            _scale = scale;
            _delay = delay;
            _elapsed = 0f;
        }

        public void Update(GameTime gameTime)
        {
            if (_elapsed < 0f) return;

            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (_delay > 0f)
            {
                _delay -= dt;
                return;
            }

            _elapsed += dt;
            if (_elapsed >= _duration) _elapsed = -1f;
        }

        public void Draw(SpriteBatch spriteBatch)
        {
            if (!IsPlaying || _delay > 0f || _texture == null) return;

            int frame = Math.Min(_frameCount - 1, (int)(_elapsed / _duration * _frameCount));
            var source = new Rectangle(frame * _frameSize, 0, _frameSize, _frameSize);
            UITheme.DrawPixelSprite(spriteBatch, _texture, source, _center, _scale);
        }
    }

    /// <summary>
    /// A brief "die icon + number" callout showing the result of a damage roll, drifting
    /// upward and fading out near where the roll happened.
    /// </summary>
    public class DiceRollPopup
    {
        private const float Duration = 1.1f;

        private Texture2D _texture;
        private int _value;
        private Vector2 _position;
        private float _elapsed = -1f;

        public void Play(Texture2D texture, int value, Vector2 position)
        {
            _texture = texture;
            _value = value;
            _position = position;
            _elapsed = 0f;
        }

        public void Update(GameTime gameTime)
        {
            if (_elapsed < 0f) return;
            _elapsed += (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (_elapsed >= Duration) _elapsed = -1f;
        }

        public void Draw(SpriteBatch spriteBatch, SpriteFont font)
        {
            if (_elapsed < 0f || _texture == null) return;

            float t = _elapsed / Duration;
            float rise = t * 26f;
            float alpha = t < 0.55f ? 1f : 1f - (t - 0.55f) / 0.45f;
            var drawPos = _position - new Vector2(0, rise);
            var color = Color.White * MathHelper.Clamp(alpha, 0f, 1f);

            const float iconScale = 0.2f; // 320px source -> ~64px on screen, sized to leave room for the number on top
            var origin = new Vector2(_texture.Width / 2f, _texture.Height / 2f);
            spriteBatch.Draw(_texture, drawPos, null, color, 0f, origin, iconScale, SpriteEffects.None, 0f);

            // Number stamped centered on the die face rather than off to the side.
            string text = _value.ToString();
            const float textScale = 1.2f;
            var textSize = UITheme.MeasureString(font, text) * textScale;
            var textPos = drawPos - textSize / 2f;
            UITheme.DrawTextWithShadow(spriteBatch, font, text, textPos, color, textScale);
        }
    }

    /// <summary>
    /// A brief camera-shake-style jitter, for punctuating a hit with more than just a flash.
    /// Reads as a little jolt on top of whatever else is playing at that position, and decays
    /// to nothing over its short duration.
    /// </summary>
    public class ImpactShake
    {
        private const float Duration = 0.25f;
        private const float Magnitude = 6f;

        private static readonly Random RandomSource = new Random();
        private float _elapsed = -1f;

        public void Play() => _elapsed = 0f;

        public void Update(GameTime gameTime)
        {
            if (_elapsed < 0f) return;
            _elapsed += (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (_elapsed >= Duration) _elapsed = -1f;
        }

        /// <summary>A small random offset that decays to zero - add this to a draw position
        /// while the shake is playing; it's Vector2.Zero at rest, so it's always safe to add.</summary>
        public Vector2 Offset
        {
            get
            {
                if (_elapsed < 0f || !GameSettings.Current.ScreenShake) return Vector2.Zero;
                float decay = 1f - (_elapsed / Duration);
                return new Vector2(
                    (float)(RandomSource.NextDouble() * 2f - 1f) * Magnitude * decay,
                    (float)(RandomSource.NextDouble() * 2f - 1f) * Magnitude * decay);
            }
        }
    }

    /// <summary>
    /// A drifting spark of ash/ember for the night backdrop. Purely decorative - spawned
    /// and recycled by NightScavengingScreen so the ruins never sit perfectly still.
    /// </summary>
    public class Ember
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public float Life;
        public float MaxLife;
        public float Size;
        public float Wobble;

        public bool IsDead => Life >= MaxLife;

        /// <summary>0 -> 1 -> 0 over its lifetime, so embers fade in and out instead of popping.</summary>
        public float Alpha
        {
            get
            {
                float t = MaxLife <= 0f ? 1f : Life / MaxLife;
                return MathHelper.Clamp(MathF.Sin(t * MathF.PI), 0f, 1f);
            }
        }
    }
}
