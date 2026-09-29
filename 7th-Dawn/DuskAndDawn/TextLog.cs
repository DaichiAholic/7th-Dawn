using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Text;

namespace DuskAndDawn
{
    /// <summary>
    /// A 2-message action log. The newest message shows bright. When another arrives, the
    /// old one slides up above it and dims - but stays put and readable until the *next*
    /// message comes in, at which point it fades out and the cycle repeats. So the log always
    /// holds exactly what just happened and what happened right before it.
    /// </summary>
    public class TextLog
    {
        private const float LineHeight = 24f;     // vertical spacing between wrapped lines
        private const float ShiftSpeed = 5f;      // the slide/dim after a push takes ~0.2s
        private const float PreviousAlpha = 0.5f; // how dim the older message settles
        private const float OutgoingRise = 10f;   // pixels the pushed-out message drifts as it fades

        private string _current = "";
        private string _previous = "";
        private string _outgoing = ""; // the message being pushed out - only visible mid-shift
        private float _shift = 1f;     // 0 right after a push, eases to 1

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

        /// <summary>Draws the newest message at basePosition (wrapping downward), the previous
        /// one dimmed directly above it, and - only while a push is animating - the message
        /// that just got pushed out, fading away above that. Long messages wrap to maxWidth.</summary>
        public void Draw(SpriteBatch spriteBatch, SpriteFont font, Vector2 basePosition, float maxWidth)
        {
            float t = UITheme.EaseOutCubic(_shift);

            // Previous: slides up from the bright slot into the slot above it, dimming as it goes.
            float previousTop = basePosition.Y;
            if (!string.IsNullOrEmpty(_previous))
            {
                var lines = WrapText(font, _previous, maxWidth);
                previousTop = basePosition.Y - lines.Count * LineHeight * t;
                float alpha = MathHelper.Lerp(1f, PreviousAlpha, t);
                DrawLines(spriteBatch, font, lines, new Vector2(basePosition.X, previousTop), new Color(200, 196, 214) * alpha, alpha);
            }

            // Outgoing: the one before that, fading out above the previous as it arrives.
            if (_shift < 1f && !string.IsNullOrEmpty(_outgoing))
            {
                var lines = WrapText(font, _outgoing, maxWidth);
                float alpha = PreviousAlpha * (1f - t);
                float top = previousTop - lines.Count * LineHeight - OutgoingRise * t;
                DrawLines(spriteBatch, font, lines, new Vector2(basePosition.X, top), new Color(200, 196, 214) * alpha, alpha);
            }

            // Current: eases in from just below.
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

        // Splits on spaces and greedily packs words onto each line up to maxWidth, so a long
        // combat message wraps instead of running past the edge of the screen.
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
