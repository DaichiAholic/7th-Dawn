using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.Screens;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MonoGame.Extended;

namespace DuskAndDawn
{
    public class DawnEventsScreen : GameScreen, IGameplayScreen
    {
        private Game1 Game1 => (Game1)Game;
        private readonly PlayerState _playerState;
        private readonly Random _random = new Random();
        private readonly MorningEvent _event;
        private readonly List<EventOption> _options;

        private readonly List<Button> _optionButtons = new List<Button>();
        private Button _continueButton;

        private bool _resolved;
        private string _resultText = "";
        private ResourceDelta _resultDelta;
        private MouseState _previousMouse;

        // The morning report: what the Kitchen cooked, what the house ate, and what Storage
        // couldn't hold. Each line is (text, isBadNews).
        private readonly List<(string text, bool bad)> _morningReport = new List<(string, bool)>();

        // Change popups on the resource bar ("+8" / "-5"), shown for a moment after the
        // morning report and again after the event resolves.
        private ResourceDelta _shownDelta;
        private float _deltaTimer;
        private const float DeltaDuration = 3f;

        // Entrance timing, and counters that tick from the old values to the new ones so
        // the morning's changes are visible happening rather than already done.
        private float _elapsed;
        private float _resolvedAt = -1f;
        private readonly CountUp _foodShown, _planksShown, _scrapsShown, _hopeShown;
        private readonly ParticleField _dust = new ParticleField(30, new RectangleF(0, 0, 1280, 720), new Vector2(-4, -8), new Vector2(6, 4),
            1f, 2.4f, 5f, 10f, new Color(255, 240, 210), Color.Transparent, wobble: 5f);
        private const float ReportAt = 0.35f, CardAt = 0.75f, OptionsAt = 1.0f;

        // ---- Layout ----
        private const float ContentX = 200f;
        private const float ContentWidth = 880f;
        private const float SlotWidth = 170f, SlotHeight = 76f, SlotGap = 16f, SlotY = 18f;
        private const float OptionsY = 372f, OptionHeight = 78f, OptionGap = 10f;

        private static readonly Color Ink = new Color(70, 45, 20);
        private static readonly Color GainColor = new Color(30, 120, 45);
        private static readonly Color LossColor = new Color(160, 40, 35);

        public DawnEventsScreen(Game game, PlayerState playerState) : base(game)
        {
            _playerState = playerState;

            // A new day. Done in the constructor so it happens exactly once per morning:
            // the Kitchen cooks, the household eats, Storage trims the rest, and the Feast
            // is available again.
            int foodBefore = _playerState.Food, hopeBefore = _playerState.Hope;

            // Another day begins - and another night survived weighs on everyone a little more.
            _playerState.Day++;
            int dread = DayInfo.DawnDread(_playerState.Day);
            _playerState.ChangeHope(-dread);
            _morningReport.Add(($"{DayInfo.Label(_playerState.Day)}. The dread builds: Hope -{dread}.", true));

            int cooked = _playerState.KitchenDailyFood;
            _playerState.AddResources(food: cooked);
            _morningReport.Add(($"The Kitchen cooked {cooked} Food.", false));

            var (eaten, shortfall, hopeLost) = _playerState.EatUpkeep();
            if (shortfall == 0)
            {
                _morningReport.Add(($"The household ate {eaten} Food (upkeep {_playerState.DailyUpkeep} a day).", false));
            }
            else
            {
                string streak = _playerState.HungryMornings > 1 ? $" ({_playerState.HungryMornings} hungry mornings running)" : "";
                _morningReport.Add(($"Needed {eaten + shortfall} Food, had {eaten}. Hunger costs {hopeLost} Hope{streak}.", true));
            }

            var (lostFood, _, _) = _playerState.ApplyStorageCap();
            if (lostFood > 0)
            {
                _morningReport.Add(($"Storage was full - {lostFood} Food spoiled.", true));
            }

            _playerState.FeastUsedToday = false;

            _shownDelta = new ResourceDelta(food: _playerState.Food - foodBefore, hope: _playerState.Hope - hopeBefore);
            _deltaTimer = DeltaDuration;
            _foodShown = new CountUp(foodBefore, 12f);
            _hopeShown = new CountUp(hopeBefore, 12f);
            _planksShown = new CountUp(_playerState.Planks, 12f);
            _scrapsShown = new CountUp(_playerState.Scraps, 12f);

            _event = MorningEventPool.GetRandom(_random, _playerState);
            _playerState.LastMorningEventTitle = _event.Title;

            _options = _event.Options.ToList();
            // Safety net - the pool always includes a free option, but never leave the
            // player stuck with nothing they can pick.
            if (!_options.Any(o => o.CanAfford(_playerState)))
            {
                _options.Add(new EventOption("Let it pass", "You let the moment pass."));
            }
        }

        public override void Initialize()
        {
            base.Initialize();

            // Seeds with the real current mouse state instead of a blank default, so a click
            // still held down from the previous screen doesn't read as a brand-new click here.
            _previousMouse = InputChecker.GetMouse();

            _optionButtons.Clear();
            for (int i = 0; i < _options.Count; i++)
            {
                var bounds = new RectangleF(ContentX, OptionsY + i * (OptionHeight + OptionGap), ContentWidth, OptionHeight);
                _optionButtons.Add(new Button(bounds, _options[i].Label) { Enabled = _options[i].CanAfford(_playerState) });
            }

            _continueButton = new Button(new RectangleF(490, 500, 300, 60), "Continue");
        }

        public override void Update(GameTime gameTime)
        {
            if (_playerState.IsGameOver)
            {
                Game1.EndRun(victory: false);
                return;
            }

            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            _elapsed += dt;
            _dust.Update(dt);
            // The counters wait for the slots to land, then tick.
            if (_elapsed > 0.5f) _deltaTimer = Math.Max(0f, _deltaTimer - dt);
            if (_elapsed > 0.6f)
            {
                _foodShown.Update(dt, _playerState.Food);
                _planksShown.Update(dt, _playerState.Planks);
                _scrapsShown.Update(dt, _playerState.Scraps);
                _hopeShown.Update(dt, _playerState.Hope);
            }
            var mouse = InputChecker.GetMouse();

            // Options can't be picked until they've arrived on screen.
            bool optionsReady = _elapsed > OptionsAt + 0.2f;
            foreach (var button in _optionButtons)
            {
                button.UpdateAnimation(dt, optionsReady && !_resolved && button.Contains(mouse.X, mouse.Y));
            }
            _continueButton.UpdateAnimation(dt, _resolved && _continueButton.Contains(mouse.X, mouse.Y));

            if (InputChecker.IsNewLeftClick(mouse, _previousMouse) && optionsReady && !ScreenTransitions.IsTransitioning)
            {
                if (!_resolved)
                {
                    for (int i = 0; i < _optionButtons.Count; i++)
                    {
                        // Button.Contains is already false for options you can't afford.
                        if (!_optionButtons[i].Contains(mouse.X, mouse.Y)) continue;

                        _optionButtons[i].TriggerPress();
                        Choose(_options[i]);
                        break;
                    }
                }
                else if (_continueButton.Contains(mouse.X, mouse.Y))
                {
                    _continueButton.TriggerPress();
                    ScreenManager.ReplaceScreen(new BaseBuilding(Game, _playerState), ScreenTransitions.FadeTransition(GraphicsDevice));
                }
            }

            _previousMouse = mouse;
        }

        private void Choose(EventOption option)
        {
            var (text, applied) = option.Resolve(_playerState, _random);

            // Anything that pushed a resource over the Storage cap spills right away.
            var (lostFood, lostPlanks, lostScraps) = _playerState.ApplyStorageCap();
            applied += new ResourceDelta(-lostFood, -lostPlanks, -lostScraps);
            if (lostFood + lostPlanks + lostScraps > 0)
            {
                text += " Storage is full - some of it spills.";
            }

            _resultText = text;
            _resultDelta = applied;
            _shownDelta = applied;
            _deltaTimer = DeltaDuration;
            _resolved = true;
            _resolvedAt = _elapsed;

            // Keep Continue clear of a long result.
            float resultBottom = ResultPanel(Game1.Font).Bottom;
            var bounds = _continueButton.Bounds;
            _continueButton.Bounds = new RectangleF(bounds.X, Math.Max(500f, resultBottom + 20f), bounds.Width, bounds.Height);
        }

        private List<string> ResultLines(SpriteFont font) => TextLog.WrapText(font, _resultText, ContentWidth - 40);

        private RectangleF ResultPanel(SpriteFont font) =>
            new RectangleF(ContentX, OptionsY, ContentWidth, 58 + ResultLines(font).Count * 26);

        // =====================================================================
        // Draw
        // =====================================================================

        public override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(new Color(235, 205, 165));

            var spriteBatch = Game1.SpriteBatch;
            var font = Game1.Font;
            UITheme.BeginCanvas(spriteBatch);

            // Morning light: a warm sky, the sun low on the right, the town faint in the haze.
            Backdrop.Sky(spriteBatch, new Color(250, 226, 188), new Color(214, 176, 136));
            UITheme.DrawGlow(spriteBatch, new Vector2(1180, 120), 420f, new Color(255, 245, 215) * (0.45f + UITheme.PulseSine(_elapsed, 0.5f) * 0.08f));
            Backdrop.DrawRays(spriteBatch, new Vector2(1180, 60), 700f, new Color(255, 248, 225) * 0.5f, _elapsed, 7);
            float drift = MathF.Sin(_elapsed * 0.05f);
            Backdrop.DrawSkyline(spriteBatch, 660, 150, new Color(206, 168, 130), seed: 11, drift: drift * 6f);
            Backdrop.DrawSkyline(spriteBatch, 700, 90, new Color(190, 150, 114), seed: 29, drift: drift * 12f);
            _dust.Draw(spriteBatch, 0.7f);

            DrawResourceBar(spriteBatch, font);
            float y = DrawMorningReport(spriteBatch, font, 128f);
            DrawEventCard(spriteBatch, font, y + 12f);

            if (!_resolved)
            {
                for (int i = 0; i < _optionButtons.Count; i++)
                {
                    DrawOption(spriteBatch, font, _optionButtons[i], _options[i], Anim.Stagger(_elapsed, i, step: 0.09f, baseDelay: OptionsAt, duration: 0.4f));
                }
            }
            else
            {
                DrawResult(spriteBatch, font);
            }

            spriteBatch.End();
        }

        // ---- Resource bar ----

        private void DrawResourceBar(SpriteBatch spriteBatch, SpriteFont font)
        {
            float totalWidth = SlotWidth * 4 + SlotGap * 3;
            float x = (1280f - totalWidth) / 2f;
            int cap = _playerState.StorageCap;

            DrawResourceSlot(spriteBatch, font, x, 0, "Food", _foodShown.Value, Game1.BreadTexture, _shownDelta.Food, _playerState.Food >= cap);
            DrawResourceSlot(spriteBatch, font, x + (SlotWidth + SlotGap), 1, "Planks", _planksShown.Value, Game1.PlanksTexture, _shownDelta.Planks, _playerState.Planks >= cap);
            DrawResourceSlot(spriteBatch, font, x + (SlotWidth + SlotGap) * 2, 2, "Scraps", _scrapsShown.Value, Game1.ScrapsTexture, _shownDelta.Scraps, _playerState.Scraps >= cap);
            DrawResourceSlot(spriteBatch, font, x + (SlotWidth + SlotGap) * 3, 3, "Hope", _hopeShown.Value, null, _shownDelta.Hope, false);

            string caption = $"Storage holds {cap} of each   -   Upkeep {_playerState.DailyUpkeep} Food every morning";
            var captionSize = UITheme.MeasureString(font, caption) * 0.7f;
            LightText(spriteBatch, font, caption, new Vector2(640f - captionSize.X / 2f, SlotY + SlotHeight + 8), Ink * Anim.Intro(_elapsed, 0.4f), 0.7f);
        }

        private void DrawResourceSlot(SpriteBatch spriteBatch, SpriteFont font, float x, int index, string label, int value, Texture2D icon, int delta, bool atCap)
        {
            // Slots drop in from above, one after another.
            float t = Anim.Stagger(_elapsed, index, step: 0.07f, baseDelay: 0.05f, duration: 0.4f);
            if (t <= 0.001f) return;
            float slotY = SlotY - (1f - t) * 30f;
            var slot = new RectangleF(x, slotY, SlotWidth, SlotHeight);
            // A warm flash while the number is still changing.
            float flash = _deltaTimer > 0f && delta != 0 ? _deltaTimer / DeltaDuration : 0f;
            Color border = Color.Lerp(new Color(150, 110, 70), delta > 0 ? new Color(140, 235, 150) : new Color(255, 130, 110), flash * 0.8f);
            UITheme.DrawPanel(spriteBatch, slot, new Color(98, 68, 44) * t, new Color(66, 44, 28) * t, border * t, 2f, 12f, shadowStrength: 0.5f * t);

            var iconCenter = new Vector2(x + 38, slotY + SlotHeight / 2f);
            if (icon != null)
            {
                float scale = 50f / Math.Max(icon.Width, icon.Height);
                spriteBatch.Draw(icon, iconCenter, null, Color.White, 0f, new Vector2(icon.Width / 2f, icon.Height / 2f), scale, SpriteEffects.None, 0f);
            }
            else
            {
                // Hope: its 32px pixel-art sparkle, kept crisp.
                UITheme.DrawPixelIconFit(spriteBatch, Game1.HopeIcon, iconCenter, 50f);
            }

            // Amber at the Storage cap (a nudge to spend or upgrade), red when Hope runs low.
            Color valueColor = atCap ? new Color(255, 200, 100)
                : (label == "Hope" && value < 30) ? new Color(255, 140, 130)
                : Color.White;
            UITheme.DrawTextWithShadow(spriteBatch, font, value.ToString(), new Vector2(x + 72, slotY + 10), valueColor, 1.3f);
            UITheme.DrawTextWithShadow(spriteBatch, font, label, new Vector2(x + 72, slotY + 48), new Color(235, 215, 190), 0.7f);

            if (_deltaTimer > 0f && delta != 0)
            {
                float life = _deltaTimer / DeltaDuration;
                float alpha = MathHelper.Clamp(life * 2f, 0f, 1f) * t;
                float rise = (1f - life) * 10f;
                string text = delta > 0 ? $"+{delta}" : delta.ToString();
                Color color = delta > 0 ? new Color(140, 235, 150) : new Color(255, 140, 120);
                var size = UITheme.MeasureString(font, text) * 0.85f;
                UITheme.DrawTextWithShadow(spriteBatch, font, text, new Vector2(x + SlotWidth - size.X - 10, slotY + 12 - rise), color * alpha, 0.85f);
            }
        }

        // ---- Morning report ----

        private float DrawMorningReport(SpriteBatch spriteBatch, SpriteFont font, float top)
        {
            // Tight enough for four lines (dread, cooking, eating, spoilage) above the event card.
            const float lineHeight = 22f, padding = 10f;
            var panel = new RectangleF(ContentX, top, ContentWidth, padding * 2 + _morningReport.Count * lineHeight);
            float panelIn = Anim.Intro(_elapsed, ReportAt - 0.15f, 0.35f);
            if (panelIn <= 0.001f) return panel.Y + panel.Height;
            UITheme.DrawPanel(spriteBatch, panel, new Color(255, 244, 222) * panelIn, new Color(236, 214, 180) * panelIn, new Color(150, 110, 70) * panelIn, 2f, 12f, shadowStrength: 0.4f * panelIn);

            // The report reads out line by line, each sliding in from the left.
            for (int i = 0; i < _morningReport.Count; i++)
            {
                var (text, bad) = _morningReport[i];
                float t = Anim.Stagger(_elapsed, i, step: 0.12f, baseDelay: ReportAt, duration: 0.35f);
                if (t <= 0.001f) continue;
                if (bad)
                {
                    // A red tick beside bad news.
                    UITheme.FillRoundedRect(spriteBatch, new RectangleF(panel.X + 8, panel.Y + padding + i * lineHeight + 3, 4, 14), LossColor * (0.8f * t), 2f);
                }
                LightText(spriteBatch, font, text, new Vector2(panel.X + 20 - (1f - t) * 14f, panel.Y + padding + i * lineHeight), (bad ? LossColor : Ink) * t, 0.85f);
            }

            return panel.Y + panel.Height;
        }

        // ---- Event ----

        private void DrawEventCard(SpriteBatch spriteBatch, SpriteFont font, float top)
        {
            const float descScale = 0.85f;
            var lines = TextLog.WrapText(font, _event.Description, (ContentWidth - 60) / descScale);
            float bottom = OptionsY - 12f;
            float t = Anim.Intro(_elapsed, CardAt, 0.45f);
            if (t <= 0.001f) return;
            var panel = Anim.Slide(new RectangleF(ContentX, top, ContentWidth, bottom - top), t, new Vector2(0, 20));
            UITheme.DrawPanel(spriteBatch, panel, new Color(255, 250, 238) * t, new Color(232, 216, 188) * t, new Color(150, 110, 70) * t, 3f, 16f, shadowStrength: 0.6f * t);
            // An ember accent down the card's left edge.
            UITheme.FillRoundedRect(spriteBatch, new RectangleF(panel.X + 12, panel.Y + 18, 5, panel.Height - 36), new Color(220, 120, 60) * (0.85f * t), 2.5f);

            LightText(spriteBatch, font, _event.Title, new Vector2(panel.X + 30, panel.Y + 16), Color.Black * t, 1.15f);
            for (int i = 0; i < lines.Count; i++)
            {
                LightText(spriteBatch, font, lines[i], new Vector2(panel.X + 30, panel.Y + 54 + i * 24), Ink * t, descScale);
            }
        }

        private void DrawOption(SpriteBatch spriteBatch, SpriteFont font, Button button, EventOption option, float intro)
        {
            if (intro <= 0.001f) return;
            bool enabled = button.Enabled;
            float hover = button.HoverAmount;

            Color top = enabled ? UITheme.Brighten(new Color(255, 248, 232), hover * 0.3f) : new Color(222, 210, 190);
            Color bottom = enabled ? UITheme.Brighten(new Color(236, 220, 192), hover * 0.3f) : new Color(206, 192, 170);
            Color border = enabled ? Color.Lerp(new Color(150, 110, 70), new Color(220, 110, 50), hover) : new Color(170, 150, 130);
            float thickness = enabled ? MathHelper.Lerp(2f, 3f, hover) : 2f;

            // Options slide in from the right; a hovered one lifts and shifts right a touch
            // with an ember bar on its edge, so the choice under the cursor is unmistakable.
            float squash = button.PressAmount * 3f;
            var bounds = Anim.Slide(button.Bounds, intro, new Vector2(40, 0));
            var drawBounds = new RectangleF(bounds.X + squash + hover * 6f, bounds.Y + squash / 2f - hover * 2f, bounds.Width - squash * 2f, bounds.Height - squash);
            if (hover > 0.01f)
            {
                UITheme.DrawGlow(spriteBatch, new Vector2(drawBounds.X + drawBounds.Width / 2f, drawBounds.Y + drawBounds.Height / 2f), drawBounds.Width * 0.4f, new Color(255, 170, 90) * (0.2f * hover));
            }
            UITheme.DrawPanel(spriteBatch, drawBounds, top * intro, bottom * intro, border * intro, thickness, 12f, shadowStrength: (enabled ? 0.5f : 0.2f) * intro);
            if (hover > 0.01f)
            {
                UITheme.FillRoundedRect(spriteBatch, new RectangleF(drawBounds.X + 6, drawBounds.Y + 12, 5, drawBounds.Height - 24), new Color(220, 110, 50) * hover, 2.5f);
            }

            Color labelColor = enabled ? Color.Black : new Color(120, 105, 90);
            float x = drawBounds.X + 20 + hover * 4f;
            LightText(spriteBatch, font, option.Label, new Vector2(x, drawBounds.Y + 12), labelColor * intro);

            // Second line: what you pay, then what happens.
            float lineY = drawBounds.Y + 44;
            const float scale = 0.8f;
            float dim = (enabled ? 1f : 0.55f) * intro;

            if (!option.Cost.IsEmpty)
            {
                x = DrawRun(spriteBatch, font, "Pay:", x, lineY, Ink * dim, scale);
                x = DrawRun(spriteBatch, font, string.Join(", ", option.Cost.Parts().Select(p => p.text.TrimStart('+'))), x, lineY, LossColor * dim, scale);
                x = DrawRun(spriteBatch, font, "  ->", x, lineY, Ink * dim, scale);
            }

            if (option.IsGamble)
            {
                x = DrawRun(spriteBatch, font, $"{option.Chance}%:", x, lineY, Ink * dim, scale);
                x = DrawDelta(spriteBatch, font, option.Outcome, x, lineY, scale, dim);
                x = DrawRun(spriteBatch, font, $"  or  {100 - option.Chance}%:", x, lineY, Ink * dim, scale);
                DrawDelta(spriteBatch, font, option.FailOutcome, x, lineY, scale, dim);
            }
            else
            {
                DrawDelta(spriteBatch, font, option.Outcome, x, lineY, scale, dim);
            }

            if (!enabled)
            {
                string reason = option.Cost.Hope > 0 && _playerState.Hope <= option.Cost.Hope ? "Not enough Hope" : "Can't afford";
                var size = UITheme.MeasureString(font, reason) * 0.8f;
                LightText(spriteBatch, font, reason, new Vector2(drawBounds.X + drawBounds.Width - size.X - 20, drawBounds.Y + 14), LossColor * intro, 0.8f);
            }
        }

        // ---- Result ----

        private void DrawResult(SpriteBatch spriteBatch, SpriteFont font)
        {
            var lines = ResultLines(font);
            // The outcome pops in where the options were.
            float t = Anim.Intro(_elapsed, _resolvedAt, 0.35f);
            var panel = Anim.Scale(ResultPanel(font), MathHelper.Lerp(0.94f, 1f, UITheme.EaseOutBack(t)));
            bool good = _resultDelta.Parts().All(p => p.gain);
            Color accent = _resultDelta.IsEmpty ? new Color(150, 110, 70) : good ? GainColor : LossColor;
            UITheme.DrawPanel(spriteBatch, panel, new Color(255, 250, 238) * t, new Color(232, 216, 188) * t, Color.Lerp(new Color(150, 110, 70), accent, 0.5f) * t, 2.5f, 12f, shadowStrength: 0.5f * t);

            for (int i = 0; i < lines.Count; i++)
            {
                LightText(spriteBatch, font, lines[i], new Vector2(panel.X + 20, panel.Y + 14 + i * 26), Color.Black * t);
            }

            float y = panel.Y + 18 + lines.Count * 26;
            float resultIn = Anim.Intro(_elapsed, _resolvedAt + 0.2f, 0.35f);
            float x = DrawRun(spriteBatch, font, "Result:", panel.X + 20, y, Ink * resultIn, 0.85f);
            DrawDelta(spriteBatch, font, _resultDelta, x, y, 0.85f, resultIn);

            EmberButton.Draw(spriteBatch, font, _continueButton, Anim.Intro(_elapsed, _resolvedAt + 0.35f, 0.4f), primary: true);
        }

        // ---- Text helpers ----

        // Text on the light parchment panels - a much softer shadow than the default.
        private static void LightText(SpriteBatch spriteBatch, SpriteFont font, string text, Vector2 position, Color color, float scale = 1f) =>
            UITheme.DrawTextWithShadow(spriteBatch, font, text, position, color, scale, shadowAlpha: 0.12f);

        /// <summary>Draws one run of text and returns the x where the next run should start.</summary>
        private static float DrawRun(SpriteBatch spriteBatch, SpriteFont font, string text, float x, float y, Color color, float scale)
        {
            LightText(spriteBatch, font, text, new Vector2(x, y), color, scale);
            return x + UITheme.MeasureString(font, text + " ").X * scale;
        }

        /// <summary>A delta as colored runs: gains green, losses red, "Nothing" if empty.</summary>
        private static float DrawDelta(SpriteBatch spriteBatch, SpriteFont font, ResourceDelta delta, float x, float y, float scale, float dim)
        {
            if (delta.IsEmpty)
            {
                return DrawRun(spriteBatch, font, "Nothing happens", x, y, Ink * (0.7f * dim), scale);
            }

            var parts = delta.Parts().ToList();
            for (int i = 0; i < parts.Count; i++)
            {
                string text = i < parts.Count - 1 ? parts[i].text + "," : parts[i].text;
                x = DrawRun(spriteBatch, font, text, x, y, (parts[i].gain ? GainColor : LossColor) * dim, scale);
            }
            return x;
        }
    }
}
