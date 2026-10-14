using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace DuskAndDawn
{
    public class LerpBar
    {
        private const float CatchUpSpeed = 4f;

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

        public float Ratio => MaxValue <= 0 ? 0f : MathHelper.Clamp(DisplayedValue / MaxValue, 0f, 1f);
    }

    public class SpriteEffect
    {
        private Texture2D _texture;
        private int _frameSize;
        private int _frameCount;
        private Vector2 _center;
        private int _scale;
        private float _duration;
        private float _delay;
        private float _elapsed = -1f;

        public bool IsPlaying => _elapsed >= 0f;

        public void Stop()
        {
            _elapsed = -1f;
            _delay = 0f;
        }

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

            const float iconScale = 0.2f;
            var origin = new Vector2(_texture.Width / 2f, _texture.Height / 2f);
            spriteBatch.Draw(_texture, drawPos, null, color, 0f, origin, iconScale, SpriteEffects.None, 0f);

            string text = _value.ToString();
            const float textScale = 1.2f;
            var textSize = UITheme.MeasureString(font, text) * textScale;
            var textPos = drawPos - textSize / 2f;
            UITheme.DrawTextWithShadow(spriteBatch, font, text, textPos, color, textScale);
        }
    }

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

    public class Ember
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public float Life;
        public float MaxLife;
        public float Size;
        public float Wobble;

        public bool IsDead => Life >= MaxLife;

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
