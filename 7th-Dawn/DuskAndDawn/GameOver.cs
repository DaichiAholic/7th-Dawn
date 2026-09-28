using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using MonoGame.Extended.Screens;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DuskAndDawn
{
    /// <summary>
    /// The end of a run, either way: Hope ran out (the night wins), or the seventh dawn
    /// broke with the house still standing - better still if the Sun Herald fell - or the
    /// Herald himself ended it. Shows how the run went night by night; New Run (or Enter)
    /// starts again, Main Menu (or Esc) goes back.
    /// </summary>
    public class GameOverScreen : GameScreen
    {
        private Game1 Game1 => (Game1)Game;
        private readonly bool _victory;
        private readonly PlayerState _finalState;
        private KeyboardState _previousKeyboard;
        private MouseState _previousMouse;
        private float _elapsed;

        private readonly Button _newRunButton = new Button(new RectangleF(420, 560, 210, 56), "New Run");
        private readonly Button _menuButton = new Button(new RectangleF(650, 560, 210, 56), "Main Menu");

        // Loss: ash falling through a red dusk. Win: gold motes rising into the sunrise.
        private readonly ParticleField _motes;
        private readonly CountUp[] _statCounters = { new CountUp(0f, 4f), new CountUp(0f, 30f), new CountUp(0f, 6f), new CountUp(0f, 10f) };

        private const float TitleAt = 0.3f, StatsAt = 0.9f, SummaryAt = 1.4f, ButtonsAt = 2.0f;

        // ---- Run summary: Hope over the nights, and the run's numbers ----
        private static readonly RectangleF SummaryPanel = new RectangleF(190, 262, 900, 276);
        private static readonly RectangleF ChartArea = new RectangleF(262, 322, 420, 150);
        private static readonly Color LineColor = new Color(255, 196, 120);
        private static readonly Color InkPrimary = new Color(240, 232, 225);
        private static readonly Color InkMuted = new Color(185, 172, 168);
        private int _hoveredPoint = -1;

        public GameOverScreen(Game game, bool victory, PlayerState finalState) : base(game)
        {
            _victory = victory;
            _finalState = finalState;
            _motes = victory
                ? new ParticleField(60, new RectangleF(0, 0, 1280, 720), new Vector2(-6, -30), new Vector2(6, -12), 1.5f, 3.5f, 4f, 9f,
                    new Color(255, 225, 160), new Color(255, 200, 120), wobble: 10f, spawnAtBottom: true)
                : new ParticleField(80, new RectangleF(0, -20, 1280, 740), new Vector2(-8, 10), new Vector2(8, 28), 1.2f, 3f, 5f, 11f,
                    new Color(150, 140, 140), Color.Transparent, wobble: 14f);
        }

        public override void Initialize()
        {
            base.Initialize();
            // An Esc/Enter/click still held from the previous screen shouldn't count as a new press.
            _previousKeyboard = Keyboard.GetState();
            _previousMouse = InputChecker.GetMouse();
        }

        public override void Update(GameTime gameTime)
        {
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            _elapsed += dt;
            _motes.Update(dt);

            if (_finalState != null && _elapsed > StatsAt)
            {
                _statCounters[0].Update(dt, _finalState.Day);
                _statCounters[1].Update(dt, _finalState.Hope);
                _statCounters[2].Update(dt, _finalState.UpgradesBought);
                _statCounters[3].Update(dt, _finalState.EnemiesDefeated);
            }

            var keyboard = Keyboard.GetState();
            var mouse = InputChecker.GetMouse();
            bool Pressed(Keys key) => keyboard.IsKeyDown(key) && !_previousKeyboard.IsKeyDown(key);
            bool buttonsLive = _elapsed > ButtonsAt;
            _hoveredPoint = _elapsed > SummaryAt ? PointUnder(mouse.X, mouse.Y) : -1;

            _newRunButton.UpdateAnimation(dt, buttonsLive && _newRunButton.Contains(mouse.X, mouse.Y));
            _menuButton.UpdateAnimation(dt, buttonsLive && _menuButton.Contains(mouse.X, mouse.Y));

            if (!ScreenTransitions.IsTransitioning && buttonsLive)
            {
                bool released = mouse.LeftButton == ButtonState.Released && _previousMouse.LeftButton == ButtonState.Pressed;
                if (Pressed(Keys.Enter) || (released && _newRunButton.Contains(mouse.X, mouse.Y)))
                {
                    _newRunButton.TriggerPress();
                    Game1.StartNewRun();
                }
                else if (Pressed(Keys.Escape) || (released && _menuButton.Contains(mouse.X, mouse.Y)))
                {
                    _menuButton.TriggerPress();
                    Game1.EndRunToMainMenu();
                }
            }
            _previousKeyboard = keyboard;
            _previousMouse = mouse;
        }

        public override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.Black);

            var spriteBatch = Game1.SpriteBatch;
            var font = Game1.Font;
            UITheme.BeginCanvas(spriteBatch);

            if (_victory) DrawSunrise(spriteBatch);
            else DrawDusk(spriteBatch);

            string headline, subline;
            if (!_victory)
            {
                headline = "Hope is gone. The night wins.";
                subline = _finalState == null ? "" : $"The house held out until day {_finalState.Day} of {DayInfo.FinalDay}.";
            }
            else if (_finalState != null && _finalState.HeraldSlain)
            {
                headline = "The seventh dawn. The Sun Herald is ash.";
                subline = "The holy light comes up gold, and for once it doesn't burn.";
            }
            else
            {
                headline = "The seventh dawn breaks.";
                subline = "The house endured. In the Castle, the Sun Herald still waits for his dawn.";
            }
            if (!_victory && _finalState != null && _finalState.FellToHerald)
            {
                headline = "The Sun Herald's dawn came instead.";
                subline = "His light burned the last of you away. The house waits for someone who won't come home.";
            }

            // Headline drops in and settles; the subline fades up after it.
            float titleIn = Anim.Intro(_elapsed, TitleAt, 0.8f);
            float titleFloat = MathF.Sin(_elapsed * 1.1f) * 2f * titleIn;
            Color titleGlow = _victory ? new Color(255, 210, 140) : new Color(200, 40, 40);
            UITheme.DrawGlow(spriteBatch, new Vector2(640, 86), 360f, titleGlow * (0.18f * titleIn));
            Centered(spriteBatch, font, headline, 60 - (1f - titleIn) * 30f + titleFloat, Color.White * titleIn, 1.6f);
            Centered(spriteBatch, font, subline, 118, new Color(235, 225, 215) * Anim.Intro(_elapsed, TitleAt + 0.5f, 0.8f), 0.85f);

            if (_finalState != null)
            {
                DrawStats(spriteBatch, font);
                DrawSummary(spriteBatch, font);
            }

            EmberButton.Draw(spriteBatch, font, _newRunButton, Anim.Intro(_elapsed, ButtonsAt, 0.45f), primary: true);
            EmberButton.Draw(spriteBatch, font, _menuButton, Anim.Intro(_elapsed, ButtonsAt + 0.08f, 0.45f));
            float hints = Anim.Intro(_elapsed, ButtonsAt + 0.4f, 0.6f);
            Centered(spriteBatch, font, "Enter: new run     Esc: main menu", 632, new Color(210, 200, 195) * (0.6f * hints), 0.62f);

            spriteBatch.End();
        }

        private void DrawSunrise(SpriteBatch spriteBatch)
        {
            // The sun climbs over the ruins and the sky warms with it.
            float rise = UITheme.EaseOutCubic(MathHelper.Clamp(_elapsed / 4f, 0f, 1f));
            Backdrop.Sky(spriteBatch, Color.Lerp(new Color(30, 22, 44), new Color(70, 50, 80), rise), Color.Lerp(new Color(160, 90, 70), new Color(250, 175, 110), rise));
            var sun = new Vector2(640, 600 - rise * 110f);
            UITheme.DrawGlow(spriteBatch, sun, 520f, new Color(255, 220, 150) * (0.35f + 0.25f * rise));
            Backdrop.DrawRays(spriteBatch, sun, 900f, new Color(255, 225, 170) * (0.6f * rise), _elapsed, 11);
            UITheme.FillCircle(spriteBatch, sun, 64f, new Color(255, 236, 190));
            Backdrop.DrawSkyline(spriteBatch, 610, 170, new Color(92, 58, 62), seed: 7, drift: MathF.Sin(_elapsed * 0.05f) * 5f);
            Backdrop.DrawSkyline(spriteBatch, 660, 100, new Color(46, 28, 36), seed: 21, drift: MathF.Sin(_elapsed * 0.05f) * 10f);
            _motes.Draw(spriteBatch, 0.9f);
        }

        private void DrawDusk(SpriteBatch spriteBatch)
        {
            // A blood-red dusk behind a dead town, ash coming down over everything.
            Backdrop.Sky(spriteBatch, new Color(12, 4, 8), new Color(70, 16, 18));
            float pulse = UITheme.PulseSine(_elapsed, 0.8f);
            UITheme.DrawGlow(spriteBatch, new Vector2(640, 660), 700f, new Color(160, 30, 24) * (0.22f + pulse * 0.06f));
            Backdrop.DrawSkyline(spriteBatch, 600, 170, new Color(30, 10, 14), seed: 7, drift: MathF.Sin(_elapsed * 0.05f) * 5f);
            Backdrop.DrawSkyline(spriteBatch, 650, 100, new Color(10, 4, 6), seed: 21, drift: MathF.Sin(_elapsed * 0.05f) * 10f);
            _motes.Draw(spriteBatch, 0.85f);
            Backdrop.Vignette(spriteBatch, Color.Black, 1f);
        }

        private void DrawStats(SpriteBatch spriteBatch, SpriteFont font)
        {
            // Tiles that pop in one by one, numbers counting up to the final tally.
            string[] labels = { "Days", "Hope", "Upgrades", "Enemies beaten", "Weapon" };
            string[] values =
            {
                $"{_statCounters[0].Value} / {DayInfo.FinalDay}",
                _statCounters[1].Value.ToString(),
                _statCounters[2].Value.ToString(),
                _statCounters[3].Value.ToString(),
                _finalState.EquippedWeapon?.DisplayName ?? "-"
            };
            float[] widths = { 130, 130, 130, 150, 236 };
            const float gap = 14, top = 160, height = 86;
            float total = gap * (widths.Length - 1);
            foreach (var w in widths) total += w;
            float x = 640 - total / 2f;

            for (int i = 0; i < labels.Length; i++)
            {
                float t = Anim.Stagger(_elapsed, i, step: 0.12f, baseDelay: StatsAt, duration: 0.45f);
                if (t > 0.001f)
                {
                    float pop = MathHelper.Lerp(0.85f, 1f, UITheme.EaseOutBack(t));
                    var tile = Anim.Scale(new RectangleF(x, top, widths[i], height), pop);
                    Color panelTop = _victory ? new Color(74, 50, 50) : new Color(40, 20, 24);
                    Color panelBottom = _victory ? new Color(48, 30, 34) : new Color(22, 10, 14);
                    Color border = _victory ? new Color(230, 170, 100) : new Color(150, 50, 45);
                    UITheme.DrawPanel(spriteBatch, tile, panelTop * t, panelBottom * t, border * t, 2f, 12f, shadowStrength: 0.6f * t);

                    var labelSize = UITheme.MeasureString(font, labels[i]) * 0.66f;
                    UITheme.DrawTextWithShadow(spriteBatch, font, labels[i], new Vector2(tile.X + (tile.Width - labelSize.X) / 2f, tile.Y + 12), new Color(210, 195, 185) * t, 0.66f);
                    float valueScale = i == 4 ? 0.85f : 1.25f;
                    var valueSize = UITheme.MeasureString(font, values[i]) * valueScale;
                    UITheme.DrawTextWithShadow(spriteBatch, font, values[i], new Vector2(tile.X + (tile.Width - valueSize.X) / 2f, tile.Y + 52 - valueSize.Y / 2f + 4), Color.White * t, valueScale);
                }
                x += widths[i] + gap;
            }
        }

        // ---------- Run summary ----------

        /// <summary>Hope at dusk each night, then where it ended - one point each.</summary>
        private List<(string label, int hope, NightRecord night)> ChartPoints()
        {
            var points = _finalState.Nights.Select(n => ($"N{n.Day}", n.HopeAtDusk, n)).ToList();
            points.Add(("End", _finalState.Hope, (NightRecord)null));
            return points;
        }

        private Vector2 PointPosition(int index, int count, int hope)
        {
            float x = count <= 1 ? ChartArea.Center.X : ChartArea.X + ChartArea.Width * index / (count - 1f);
            float y = ChartArea.Bottom - ChartArea.Height * MathHelper.Clamp(hope / (float)PlayerState.MaxHope, 0f, 1f);
            return new Vector2(x, y);
        }

        /// <summary>Which point's column the mouse is over (generous hit area), or -1.</summary>
        private int PointUnder(float mx, float my)
        {
            if (_finalState == null || _finalState.Nights.Count == 0) return -1;
            if (mx < ChartArea.X - 24 || mx > ChartArea.Right + 24 || my < ChartArea.Y - 20 || my > ChartArea.Bottom + 34) return -1;
            var points = ChartPoints();
            int best = -1;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < points.Count; i++)
            {
                float distance = Math.Abs(PointPosition(i, points.Count, points[i].hope).X - mx);
                if (distance < bestDistance) { bestDistance = distance; best = i; }
            }
            return best;
        }

        private void DrawSummary(SpriteBatch spriteBatch, SpriteFont font)
        {
            float t = Anim.Intro(_elapsed, SummaryAt, 0.5f);
            if (t <= 0.001f) return;
            var panel = Anim.Slide(SummaryPanel, t, new Vector2(0, 20));
            float dy = panel.Y - SummaryPanel.Y;
            Color panelTop = _victory ? new Color(58, 40, 42) : new Color(34, 18, 22);
            Color panelBottom = _victory ? new Color(38, 26, 30) : new Color(20, 10, 14);
            Color surface = Color.Lerp(panelTop, panelBottom, 0.5f);
            UITheme.DrawPanel(spriteBatch, panel, panelTop * (0.95f * t), panelBottom * (0.95f * t), new Color(170, 120, 80) * (0.8f * t), 2f, 14f, shadowStrength: 0.6f * t);

            UITheme.DrawTextWithShadow(spriteBatch, font, "Hope at nightfall", new Vector2(panel.X + 24, panel.Y + 16), InkPrimary * t, 0.8f);

            if (_finalState.Nights.Count == 0)
            {
                UITheme.DrawTextWithShadow(spriteBatch, font, "No nights recorded for this run.", new Vector2(ChartArea.X, ChartArea.Y + 60 + dy), InkMuted * t, 0.75f);
            }
            else
            {
                DrawHopeChart(spriteBatch, font, t, dy, surface);
            }
            DrawRunFacts(spriteBatch, font, t, new Vector2(panel.X + 540, panel.Y + 16));
        }

        private void DrawHopeChart(SpriteBatch spriteBatch, SpriteFont font, float t, float dy, Color surface)
        {
            var points = ChartPoints();
            Vector2 At(int i) => PointPosition(i, points.Count, points[i].hope) + new Vector2(0, dy);

            // Recessive grid: 0, 50 and 100 Hope.
            foreach (int level in new[] { 0, 50, 100 })
            {
                float y = ChartArea.Bottom - ChartArea.Height * level / 100f + dy;
                spriteBatch.DrawLine(new Vector2(ChartArea.X - 6, y), new Vector2(ChartArea.Right + 6, y), Color.White * ((level == 0 ? 0.18f : 0.07f) * t), 1f);
                string tick = level.ToString();
                var size = UITheme.MeasureString(font, tick) * 0.55f;
                UITheme.DrawTextWithShadow(spriteBatch, font, tick, new Vector2(ChartArea.X - 14 - size.X, y - size.Y / 2f), InkMuted * t, 0.55f);
            }

            // Hover crosshair behind the line.
            if (_hoveredPoint >= 0)
            {
                float x = At(_hoveredPoint).X;
                spriteBatch.DrawLine(new Vector2(x, ChartArea.Y - 8 + dy), new Vector2(x, ChartArea.Bottom + dy), Color.White * (0.22f * t), 1f);
            }

            // The line draws itself left to right as the panel settles.
            float reveal = Anim.Intro(_elapsed, SummaryAt + 0.2f, 0.9f) * (points.Count - 1);
            for (int i = 0; i < points.Count - 1; i++)
            {
                float segment = MathHelper.Clamp(reveal - i, 0f, 1f);
                if (segment <= 0f) break;
                spriteBatch.DrawLine(At(i), Vector2.Lerp(At(i), At(i + 1), segment), LineColor * t, 2f);
            }

            for (int i = 0; i < points.Count; i++)
            {
                if (points.Count > 1 && reveal < i - 0.001f) break;
                var p = At(i);
                bool hovered = i == _hoveredPoint;
                bool knockedOut = points[i].night?.KnockedOut == true;
                // A surface ring keeps markers crisp where they sit on the line. A night you
                // were knocked out on is drawn hollow.
                UITheme.FillCircle(spriteBatch, p, hovered ? 8f : 6f, surface * t);
                UITheme.FillCircle(spriteBatch, p, hovered ? 6f : 4.5f, LineColor * t);
                if (knockedOut) UITheme.FillCircle(spriteBatch, p, hovered ? 3f : 2.2f, surface * t);

                var labelSize = UITheme.MeasureString(font, points[i].label) * 0.55f;
                UITheme.DrawTextWithShadow(spriteBatch, font, points[i].label, new Vector2(p.X - labelSize.X / 2f, ChartArea.Bottom + dy + 10), (hovered ? InkPrimary : InkMuted) * t, 0.55f);
            }

            // Direct labels on the first and last points only.
            void ValueLabel(int i)
            {
                var p = At(i);
                string value = points[i].hope.ToString();
                var size = UITheme.MeasureString(font, value) * 0.62f;
                UITheme.DrawTextWithShadow(spriteBatch, font, value, new Vector2(p.X - size.X / 2f, p.Y - size.Y - 8), InkPrimary * t, 0.62f);
            }
            if (reveal >= points.Count - 1.001f || points.Count == 1)
            {
                ValueLabel(0);
                if (points.Count > 1) ValueLabel(points.Count - 1);
            }

            if (_hoveredPoint >= 0) DrawPointCard(spriteBatch, font, points[_hoveredPoint], At(_hoveredPoint), t);
        }

        /// <summary>Details for the hovered night: where, what came home, how Hope moved.</summary>
        private void DrawPointCard(SpriteBatch spriteBatch, SpriteFont font, (string label, int hope, NightRecord night) point, Vector2 anchor, float t)
        {
            var lines = new List<(string text, Color color)>();
            if (point.night == null)
            {
                lines.Add(("The end of the run", InkPrimary));
                lines.Add(($"Hope {point.hope}", InkMuted));
            }
            else
            {
                var n = point.night;
                lines.Add(($"Night {n.Day} - {DistrictInfo.Name(n.District)}", InkPrimary));
                lines.Add(($"Hope {n.HopeAtDusk} -> {n.HopeAtDawn}", InkMuted));
                lines.Add(($"Brought home {n.Food} Food, {n.Planks} Planks, {n.Scraps} Scraps", InkMuted));
                lines.Add(($"{n.EnemiesDefeated} enemies beaten, {n.RoomsExplored} rooms", InkMuted));
                if (n.KnockedOut) lines.Add(("Knocked out", new Color(255, 160, 140)));
            }

            const float scale = 0.6f, lineHeight = 18f;
            float width = lines.Max(l => UITheme.MeasureString(font, l.text).X * scale) + 24;
            float height = lines.Count * lineHeight + 16;
            float x = MathHelper.Clamp(anchor.X + 14, 200, 1080 - width);
            if (anchor.X + 14 + width > ChartArea.Right + 60) x = anchor.X - 14 - width;
            float y = MathHelper.Clamp(anchor.Y - height / 2f, SummaryPanel.Y + 6, SummaryPanel.Bottom - height - 6);
            var card = new RectangleF(x, y, width, height);
            UITheme.DrawPanel(spriteBatch, card, new Color(30, 24, 30) * t, new Color(20, 16, 22) * t, LineColor * (0.7f * t), 1.5f, 8f, shadowStrength: 0.8f * t);
            for (int i = 0; i < lines.Count; i++)
            {
                UITheme.DrawTextWithShadow(spriteBatch, font, lines[i].text, new Vector2(card.X + 12, card.Y + 8 + i * lineHeight), lines[i].color * t, scale);
            }
        }

        /// <summary>The run's headline facts, beside the chart.</summary>
        private void DrawRunFacts(SpriteBatch spriteBatch, SpriteFont font, float t, Vector2 topLeft)
        {
            var nights = _finalState.Nights;
            float y = topLeft.Y;
            void Line(string label, string value)
            {
                UITheme.DrawTextWithShadow(spriteBatch, font, label, new Vector2(topLeft.X, y), InkMuted * t, 0.62f);
                UITheme.DrawTextWithShadow(spriteBatch, font, value, new Vector2(topLeft.X, y + 17), InkPrimary * t, 0.78f);
                y += 44;
            }

            UITheme.DrawTextWithShadow(spriteBatch, font, "The run", new Vector2(topLeft.X, y), InkPrimary * t, 0.8f);
            y += 34;

            if (nights.Count == 0)
            {
                Line("Nights", "None recorded");
                return;
            }

            var best = nights.OrderByDescending(n => n.Haul).First();
            Line("Best haul", $"{best.Haul} materials, night {best.Day} ({DistrictInfo.Name(best.District)})");
            Line("Brought home in total", $"{nights.Sum(n => n.Haul)} materials from {nights.Sum(n => n.RoomsExplored)} rooms");
            int knockouts = nights.Count(n => n.KnockedOut);
            var favourite = nights.GroupBy(n => n.District).OrderByDescending(g => g.Count()).First();
            Line("Most nights in", $"{DistrictInfo.Name(favourite.Key)} ({favourite.Count()})  -  knocked out {knockouts}x");

            string herald = _finalState.HeraldSlain ? "Slain"
                : _finalState.FellToHerald ? "He won"
                : nights.Any(n => n.District == District.Castle) ? "Left waiting"
                : "Never reached";
            Line("The Sun Herald", herald);
        }

        private static void Centered(SpriteBatch spriteBatch, SpriteFont font, string text, float y, Color color, float scale)
        {
            var size = UITheme.MeasureString(font, text) * scale;
            UITheme.DrawTextWithShadow(spriteBatch, font, text, new Vector2(640 - size.X / 2f, y), color, scale);
        }
    }
}
