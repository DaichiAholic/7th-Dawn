using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended;
using System;
using System.Collections.Generic;

namespace DuskAndDawn
{
    // Night screen: Strange Rooms - a small story with a choice, themed by district
    // (see NightEventPool). Laid out like a supply cache: the story on the left, the
    // options on the right with their time cost.
    public partial class NightScavengingScreen
    {
        private NightEvent _event;
        private readonly List<Button> _eventButtons = new List<Button>();
        private readonly HashSet<string> _eventsSeenTonight = new HashSet<string>();

        private const float EventOptionX = 420f, EventOptionWidth = 820f, EventOptionHeight = 90f, EventOptionGap = 10f;

        private void StartEvent()
        {
            _event = NightEventPool.Pick(_district, _random, _eventsSeenTonight);

            _eventButtons.Clear();
            float y = 204f;
            foreach (var option in _event.Options)
            {
                _eventButtons.Add(new Button(new RectangleF(EventOptionX, y, EventOptionWidth, EventOptionHeight), option.Label)
                {
                    Enabled = option.BlockedReason(_playerState, _dawnTimer) == null
                });
                y += EventOptionHeight + EventOptionGap;
            }
            _state = ExplorationState.Event;
        }

        private void HandleEventClick(int x, int y)
        {
            for (int i = 0; i < _eventButtons.Count; i++)
            {
                // Button.Contains is already false for blocked options.
                if (!_eventButtons[i].Contains(x, y)) continue;

                _eventButtons[i].TriggerPress();
                ResolveEvent(_event.Options[i]);
                return;
            }
        }

        private void ResolveEvent(NightEventOption option)
        {
            _dawnTimer.Spend(option.Minutes);
            var result = option.Resolve(_playerState, _random, _current.Depth);
            _textLog.Push(result.Text);
            WarnOfDawn();

            // Some choices go wrong loudly - the fight decides whether the room counts as cleared.
            if (result.StartsFight)
            {
                StartEncounter(_current);
                return;
            }

            _roomsCleared++;
            _state = ExplorationState.Map;
            if (IsNightOver)
            {
                GoToDawnReturn();
            }
        }

        private void DrawEvent(SpriteBatch spriteBatch, SpriteFont font, float totalSeconds)
        {
            float headIn = Anim.Intro(_stateTime, 0f, 0.35f);
            UITheme.DrawTextWithShadow(spriteBatch, font, "A strange room.", new Vector2(60 - (1f - headIn) * 20f, 220), Color.White * headIn);
            UITheme.DrawTextWithShadow(spriteBatch, font, "Choose what to do here.", new Vector2(60 - (1f - headIn) * 20f, 250), Color.LightGray * headIn, 0.8f);

            // The story card, with the same breathing sparkle Strange Rooms have on the map.
            // It rises into place with a violet haze behind it.
            float cardIn = Anim.Intro(_stateTime, 0.05f, 0.45f);
            if (cardIn <= 0.001f) return;
            var card = Anim.Slide(new RectangleF(60, 280, 320, 300), cardIn, new Vector2(0, 24));
            UITheme.DrawGlow(spriteBatch, new Vector2(card.X + card.Width / 2f, card.Y + card.Height / 2f), 220f, new Color(150, 90, 230) * ((0.1f + 0.05f * UITheme.PulseSine(totalSeconds, 1.2f)) * cardIn));
            UITheme.DrawPanel(spriteBatch, card, new Color(52, 34, 70) * cardIn, new Color(32, 20, 46) * cardIn, new Color(170, 120, 230) * cardIn, 3f, 14f, shadowStrength: 0.6f * cardIn);
            var sparkleCenter = new Vector2(card.X + card.Width - 40, card.Y + 36);
            float s = 12f + UITheme.PulseSine(totalSeconds, 2.5f) * 4f;
            UITheme.DrawGlow(spriteBatch, sparkleCenter, 30f, new Color(170, 110, 255) * 0.8f);
            spriteBatch.DrawLine(sparkleCenter - new Vector2(0, s), sparkleCenter + new Vector2(0, s), new Color(230, 200, 255), 2.5f);
            spriteBatch.DrawLine(sparkleCenter - new Vector2(s, 0), sparkleCenter + new Vector2(s, 0), new Color(230, 200, 255), 2.5f);

            var titleLines = TextLog.WrapText(font, _event.Title, (card.Width - 80) / 1.05f);
            float textY = card.Y + 16;
            foreach (var line in titleLines)
            {
                UITheme.DrawTextWithShadow(spriteBatch, font, line, new Vector2(card.X + 18, textY), new Color(240, 225, 255) * cardIn, 1.05f);
                textY += 28;
            }
            textY += 8;
            foreach (var line in TextLog.WrapText(font, _event.Description, (card.Width - 36) / 0.8f))
            {
                UITheme.DrawTextWithShadow(spriteBatch, font, line, new Vector2(card.X + 18, textY), new Color(215, 205, 230) * cardIn, 0.8f);
                textY += 22;
            }

            for (int i = 0; i < _eventButtons.Count; i++)
            {
                DrawEventButton(spriteBatch, font, _eventButtons[i], _event.Options[i], Anim.Stagger(_stateTime, i, step: 0.08f, baseDelay: 0.2f, duration: 0.35f));
            }

            _textLog.Draw(spriteBatch, font, new Vector2(420, 636), maxWidth: 820f);
        }

        private void DrawEventButton(SpriteBatch spriteBatch, SpriteFont font, Button button, NightEventOption option, float intro = 1f)
        {
            if (intro <= 0.001f) return;
            bool enabled = button.Enabled;
            float hover = button.HoverAmount;
            Color top = enabled ? UITheme.Brighten(new Color(70, 58, 86), hover * 0.2f) : new Color(44, 44, 50);
            Color bottom = enabled ? UITheme.Brighten(new Color(46, 38, 58), hover * 0.2f) : new Color(32, 32, 38);
            Color border = enabled ? Color.Lerp(Color.White * 0.55f, Color.White, hover) : Color.White * 0.18f;

            float squash = button.PressAmount * 3f;
            var b = Anim.Slide(button.Bounds, intro, new Vector2(40, 0));
            var draw = new RectangleF(b.X + squash + hover * 4f, b.Y + squash / 2f, b.Width - squash * 2f, b.Height - squash);
            UITheme.DrawPanel(spriteBatch, draw, top * intro, bottom * intro, border * intro, MathHelper.Lerp(2f, 3f, hover), 10f, shadowStrength: (enabled ? 0.5f : 0.2f) * intro);
            DrawHoverAccent(spriteBatch, draw, hover, new Color(200, 150, 255));

            UITheme.DrawTextWithShadow(spriteBatch, font, option.Label, new Vector2(draw.X + 14, draw.Y + 8), (enabled ? Color.White : new Color(140, 140, 150)) * intro, 0.95f);

            // Time cost (and what the clock will read after), or why it's unavailable.
            string blocked = option.BlockedReason(_playerState, _dawnTimer);
            string right = blocked
                ?? (option.Minutes == 0 ? "No time" : $"{DawnTimer.FormatDuration(option.Minutes)}  ->  {_dawnTimer.ClockLabelAfter(option.Minutes)}");
            Color rightColor = blocked != null ? new Color(230, 120, 105)
                : option.Minutes == 0 ? new Color(170, 220, 170) : new Color(255, 205, 140);
            var rightSize = UITheme.MeasureString(font, right) * 0.8f;
            UITheme.DrawTextWithShadow(spriteBatch, font, right, new Vector2(draw.Right - rightSize.X - 14, draw.Y + 10), rightColor * intro, 0.8f);

            const float detailScale = 0.72f;
            var lines = TextLog.WrapText(font, option.Detail, (draw.Width - 28) / detailScale);
            for (int i = 0; i < Math.Min(lines.Count, 2); i++)
            {
                UITheme.DrawTextWithShadow(spriteBatch, font, lines[i], new Vector2(draw.X + 14, draw.Y + 40 + i * 20),
                    (enabled ? new Color(215, 210, 225) : new Color(120, 120, 130)) * intro, detailScale);
            }
        }
    }
}
