using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended;
using System;
using System.Collections.Generic;

namespace DuskAndDawn
{
    public static class Anim
    {
        public static float Intro(float elapsed, float delay = 0f, float duration = 0.45f) =>
            UITheme.EaseOutCubic((elapsed - delay) / duration);

        public static float Stagger(float elapsed, int index, float step = 0.06f, float baseDelay = 0.1f, float duration = 0.4f) =>
            Intro(elapsed, baseDelay + index * step, duration);

        public static float Pop(float elapsed, float delay = 0f, float duration = 0.35f) =>
            UITheme.EaseOutBack(MathHelper.Clamp((elapsed - delay) / duration, 0f, 1f));

        public static RectangleF Slide(RectangleF bounds, float t, Vector2 offset) =>
            new RectangleF(bounds.X + offset.X * (1f - t), bounds.Y + offset.Y * (1f - t), bounds.Width, bounds.Height);

        public static RectangleF Scale(RectangleF bounds, float scale)
        {
            float w = bounds.Width * scale, h = bounds.Height * scale;
            return new RectangleF(bounds.X + (bounds.Width - w) / 2f, bounds.Y + (bounds.Height - h) / 2f, w, h);
        }

        public static float Flash(float now, float triggeredAt, float duration) =>
            triggeredAt < 0f || now < triggeredAt ? 0f : MathHelper.Clamp(1f - (now - triggeredAt) / duration, 0f, 1f);
    }

    public static class EmberButton
    {
        public static void Draw(SpriteBatch spriteBatch, SpriteFont font, Button button, float intro = 1f, float textScale = 1f, bool primary = false)
        {
            if (intro <= 0.001f) return;
            float hover = button.HoverAmount;
            var bounds = Anim.Slide(button.Bounds, intro, new Vector2(0, 22));
            float squash = button.PressAmount * 4f;
            float grow = hover * 5f;
            var b = new RectangleF(bounds.X + squash - grow, bounds.Y + squash / 2f - hover * 2f, bounds.Width - squash * 2f + grow * 2f, bounds.Height - squash);

            Color border = primary ? Color.Lerp(new Color(220, 110, 55), new Color(255, 170, 90), hover) : Color.Lerp(new Color(150, 75, 45), new Color(255, 150, 80), hover);

            if (hover > 0.01f || primary)
            {
                UITheme.DrawGlow(spriteBatch, new Vector2(b.X + b.Width / 2f, b.Y + b.Height / 2f), b.Width * 0.55f, new Color(255, 130, 60) * ((primary ? 0.1f : 0f) + 0.18f * hover) * intro);
            }
            UITheme.DrawFrame(spriteBatch, b, UIFrame.Pill, border * intro, intro, shadow: 0.7f, surface: primary ? Palette.EmberPanelBright : Palette.EmberPanel);

            var size = UITheme.MeasureString(font, button.Label) * textScale;
            UITheme.DrawTextWithShadow(spriteBatch, font, button.Label, new Vector2(b.X + (b.Width - size.X) / 2f, b.Y + (b.Height - size.Y) / 2f), Color.White * intro, textScale, shadowAlpha: 0.45f * intro);
        }
    }

    public class CountUp
    {
        private float _shown;
        private readonly float _speed;

        public CountUp(float start = 0f, float unitsPerSecond = 40f)
        {
            _shown = start;
            _speed = unitsPerSecond;
        }

        public int Value => (int)MathF.Round(_shown);

        public void Update(float dt, float target)
        {
            float step = Math.Max(_speed, Math.Abs(target - _shown) * 3f) * dt;
            _shown = UITheme.MoveTowards(_shown, target, step);
        }
    }

    public class ParticleField
    {
        private class Mote
        {
            public Vector2 Position, Velocity;
            public float Life, MaxLife, Size, Wobble;
        }

        private readonly List<Mote> _motes = new List<Mote>();
        private readonly Random _random;
        private readonly RectangleF _area;
        private readonly Vector2 _velocityMin, _velocityMax;
        private readonly float _sizeMin, _sizeMax, _lifeMin, _lifeMax, _wobble;
        private readonly Color _core, _glow;
        private readonly bool _spawnAtBottom;

        public ParticleField(int count, RectangleF area, Vector2 velocityMin, Vector2 velocityMax,
            float sizeMin, float sizeMax, float lifeMin, float lifeMax, Color core, Color glow,
            float wobble = 6f, bool spawnAtBottom = false, int seed = 0)
        {
            _random = seed == 0 ? new Random() : new Random(seed);
            _area = area;
            _velocityMin = velocityMin;
            _velocityMax = velocityMax;
            _sizeMin = sizeMin;
            _sizeMax = sizeMax;
            _lifeMin = lifeMin;
            _lifeMax = lifeMax;
            _core = core;
            _glow = glow;
            _wobble = wobble;
            _spawnAtBottom = spawnAtBottom;
            for (int i = 0; i < count; i++) _motes.Add(Spawn(anywhere: true));
        }

        private float Range(float a, float b) => a + (float)_random.NextDouble() * (b - a);

        private Mote Spawn(bool anywhere)
        {
            var mote = new Mote
            {
                Position = new Vector2(Range(_area.Left, _area.Right),
                    anywhere || !_spawnAtBottom ? Range(_area.Top, _area.Bottom) : _area.Bottom),
                Velocity = new Vector2(Range(_velocityMin.X, _velocityMax.X), Range(_velocityMin.Y, _velocityMax.Y)),
                MaxLife = Range(_lifeMin, _lifeMax),
                Size = Range(_sizeMin, _sizeMax),
                Wobble = Range(0f, MathF.PI * 2f)
            };
            mote.Life = anywhere ? Range(0f, mote.MaxLife) : 0f;
            return mote;
        }

        public void Update(float dt)
        {
            for (int i = 0; i < _motes.Count; i++)
            {
                var m = _motes[i];
                m.Life += dt;
                m.Wobble += dt * 1.3f;
                m.Position += (m.Velocity + new Vector2(MathF.Sin(m.Wobble) * _wobble, 0f)) * dt;
                bool outside = m.Position.Y < _area.Top - 20 || m.Position.Y > _area.Bottom + 20
                    || m.Position.X < _area.Left - 20 || m.Position.X > _area.Right + 20;
                if (m.Life >= m.MaxLife || outside) _motes[i] = Spawn(anywhere: false);
            }
        }

        public void Draw(SpriteBatch spriteBatch, float alpha = 1f)
        {
            foreach (var m in _motes)
            {
                float fade = MathHelper.Clamp(MathF.Sin(m.Life / m.MaxLife * MathF.PI), 0f, 1f) * alpha;
                if (_glow.A > 0) UITheme.DrawGlow(spriteBatch, m.Position, m.Size * 3.2f, _glow * (0.3f * fade));
                UITheme.FillCircle(spriteBatch, m.Position, m.Size * 0.6f, _core * (0.8f * fade));
            }
        }
    }

    public static class Backdrop
    {
        public static void FillTriangle(SpriteBatch spriteBatch, Vector2 apex, float baseY, float halfWidth, Color color)
        {
            float height = baseY - apex.Y;
            if (height <= 0) return;
            for (float y = 0; y < height; y += 2f)
            {
                float w = halfWidth * (y / height);
                spriteBatch.FillRectangle(new RectangleF(apex.X - w, apex.Y + y, w * 2f, 2.5f), color);
            }
        }

        public static void DrawSkyline(SpriteBatch spriteBatch, float groundY, float maxHeight, Color color, int seed, float drift = 0f)
        {
            var random = new Random(seed);
            float x = -80f + drift;
            while (x < 1340f)
            {
                float width = 40f + (float)random.NextDouble() * 80f;
                float height = maxHeight * (0.25f + (float)random.NextDouble() * 0.6f);
                int kind = random.Next(10);
                var body = new RectangleF(x, groundY - height, width, height + 2f);
                spriteBatch.FillRectangle(body, color);

                if (kind < 4)
                {
                    float roofHeight = width * 0.45f;
                    FillTriangle(spriteBatch, new Vector2(x + width / 2f + (kind == 0 ? width * 0.2f : 0f), body.Y - roofHeight), body.Y + 1f, width / 2f, color);
                }
                else if (kind == 4)
                {
                    float towerWidth = width * 0.35f;
                    var tower = new RectangleF(x + width * 0.3f, body.Y - maxHeight * 0.45f, towerWidth, maxHeight * 0.45f + 1f);
                    spriteBatch.FillRectangle(tower, color);
                    FillTriangle(spriteBatch, new Vector2(tower.X + towerWidth / 2f, tower.Y - maxHeight * 0.4f), tower.Y + 1f, towerWidth / 2f + 2f, color);
                }
                else if (kind == 5)
                {
                    for (int c = 0; c < 4; c++)
                    {
                        if (c % 2 == 0) spriteBatch.FillRectangle(new RectangleF(x + c * width / 4f, body.Y - 8f, width / 4f, 9f), color);
                    }
                    spriteBatch.FillRectangle(new RectangleF(x + width - 18f, body.Y - maxHeight * 0.3f, 18f, maxHeight * 0.3f + 1f), color);
                }
                else if (kind == 6)
                {
                    for (int j = 0; j < 4; j++)
                    {
                        float h = 6f + (float)random.NextDouble() * 18f;
                        spriteBatch.FillRectangle(new RectangleF(x + j * width / 4f, body.Y - h, width / 4f + 0.5f, h + 1f), color);
                    }
                }
                x += width - 2f;
            }
            spriteBatch.FillRectangle(new RectangleF(0, groundY, 1280, 720 - groundY), color);
        }

        public static void Sky(SpriteBatch spriteBatch, Color top, Color bottom, float height = 720f)
        {
            const float strip = 6f;
            for (float y = 0; y < height; y += strip)
            {
                spriteBatch.FillRectangle(new RectangleF(0, y, 1280, strip + 0.5f), Color.Lerp(top, bottom, (y + strip / 2f) / height));
            }
        }

        public static void Vignette(SpriteBatch spriteBatch, Color color, float strength)
        {
            if (strength <= 0.001f) return;
            const int bands = 10;
            for (int i = 0; i < bands; i++)
            {
                float inset = i * 9f;
                float alpha = strength * (1f - i / (float)bands) * 0.22f;
                spriteBatch.FillRectangle(new RectangleF(0, inset, 1280, 9f), color * alpha);
                spriteBatch.FillRectangle(new RectangleF(0, 720 - inset - 9f, 1280, 9f), color * alpha);
                spriteBatch.FillRectangle(new RectangleF(inset, 0, 9f, 720), color * alpha);
                spriteBatch.FillRectangle(new RectangleF(1280 - inset - 9f, 0, 9f, 720), color * alpha);
            }
        }

        public static void DrawRays(SpriteBatch spriteBatch, Vector2 origin, float length, Color color, float time, int count = 9)
        {
            for (int i = 0; i < count; i++)
            {
                float angle = MathF.PI + (i + 0.5f) / count * MathF.PI + MathF.Sin(time * 0.15f + i) * 0.04f;
                var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
                float flicker = 0.6f + 0.4f * MathF.Sin(time * 0.6f + i * 1.7f);
                spriteBatch.DrawLine(origin, origin + direction * length, color * (0.35f * flicker), 26f);
                spriteBatch.DrawLine(origin, origin + direction * length * 0.8f, color * (0.25f * flicker), 10f);
            }
        }
    }
}
