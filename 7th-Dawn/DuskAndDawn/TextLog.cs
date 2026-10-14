using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Text;

namespace DuskAndDawn
{
    public class TextLog
    {
        private const float LineHeight = 24f;
        private const float ShiftSpeed = 5f;
        private const float PreviousAlpha = 0.5f;
        private const float OutgoingRise = 10f;

        private string _current = "";
        private string _previous = "";
        private string _outgoing = "";
        private float _shift = 1f;

        public void Push(string message)
        {
            if (string.IsNullOrEmpty(message)) return;

            _outgoing = _previous;
            _previous = _current;
            _current = message;
            _shift = 0f;
        }

        public void Update(GameTime gameTime)
        {
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            _shift = Math.Min(1f, _shift + ShiftSpeed * dt);
        }

        public void Draw(SpriteBatch spriteBatch, SpriteFont font, Vector2 basePosition, float maxWidth)
        {
            float t = UITheme.EaseOutCubic(_shift);

            float previousTop = basePosition.Y;
            if (!string.IsNullOrEmpty(_previous))
            {
                var lines = WrapText(font, _previous, maxWidth);
                previousTop = basePosition.Y - lines.Count * LineHeight * t;
                float alpha = MathHelper.Lerp(1f, PreviousAlpha, t);
                DrawLines(spriteBatch, font, lines, new Vector2(basePosition.X, previousTop), new Color(200, 196, 214) * alpha, alpha);
            }

            if (_shift < 1f && !string.IsNullOrEmpty(_outgoing))
            {
                var lines = WrapText(font, _outgoing, maxWidth);
                float alpha = PreviousAlpha * (1f - t);
                float top = previousTop - lines.Count * LineHeight - OutgoingRise * t;
                DrawLines(spriteBatch, font, lines, new Vector2(basePosition.X, top), new Color(200, 196, 214) * alpha, alpha);
            }

            if (!string.IsNullOrEmpty(_current))
            {
                var lines = WrapText(font, _current, maxWidth);
                DrawLines(spriteBatch, font, lines, basePosition + new Vector2(0, (1f - t) * 8f), Color.White * t, t);
            }
        }

        private static void DrawLines(SpriteBatch spriteBatch, SpriteFont font, List<string> lines, Vector2 topLeft, Color color, float alpha)
        {
            for (int i = 0; i < lines.Count; i++)
            {
                UITheme.DrawTextWithShadow(spriteBatch, font, lines[i], topLeft + new Vector2(0, LineHeight * i), color, 1f, shadowAlpha: 0.45f * alpha);
            }
        }

        internal static List<string> WrapText(SpriteFont font, string text, float maxWidth)
        {
            var words = text.Split(' ');
            var lines = new List<string>();
            var line = new StringBuilder();

            foreach (var word in words)
            {
                string candidate = line.Length == 0 ? word : line.ToString() + " " + word;
                if (line.Length > 0 && UITheme.MeasureString(font, candidate).X > maxWidth)
                {
                    lines.Add(line.ToString());
                    line.Clear();
                    line.Append(word);
                }
                else
                {
                    if (line.Length > 0) line.Append(' ');
                    line.Append(word);
                }
            }

            if (line.Length > 0) lines.Add(line.ToString());
            if (lines.Count == 0) lines.Add("");
            return lines;
        }
    }
}
