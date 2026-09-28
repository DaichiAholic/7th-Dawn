using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using MonoGame.Extended.Screens;
using System;

namespace DuskAndDawn
{
    /// <summary>
    /// The end of a run, either way: Hope ran out (the night wins), or the seventh dawn
    /// broke with the house still standing - better still if the Hollow Knight fell.
    /// New Run (or Enter) starts again, Main Menu (or Esc) goes back.
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
        private readonly CountUp[] _statCounters = { new CountUp(0f, 4f), new CountUp(0f, 30f), new CountUp(0f, 6f) };

        private const float TitleAt = 0.3f, StatsAt = 1.1f, ButtonsAt = 1.9f;

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
            }

            var keyboard = Keyboard.GetState();
            var mouse = InputChecker.GetMouse();
            bool Pressed(Keys key) => keyboard.IsKeyDown(key) && !_previousKeyboard.IsKeyDown(key);
            bool buttonsLive = _elapsed > ButtonsAt;

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
            else if (_finalState != null && _finalState.KnightSlain)
            {
                headline = "The seventh dawn. The Knight is dead.";
                subline = "The holy light comes up gold, and for once it doesn't burn.";
            }
            else
            {
                headline = "The seventh dawn breaks.";
                subline = "The house endured. Somewhere in the ruins, the Knight still waits.";
            }

            // Headline drops in and settles; the subline fades up after it.
            float titleIn = Anim.Intro(_elapsed, TitleAt, 0.8f);
            float titleFloat = MathF.Sin(_elapsed * 1.1f) * 2f * titleIn;
            Color titleGlow = _victory ? new Color(255, 210, 140) : new Color(200, 40, 40);
            UITheme.DrawGlow(spriteBatch, new Vector2(640, 196), 360f, titleGlow * (0.18f * titleIn));
            Centered(spriteBatch, font, headline, 170 - (1f - titleIn) * 30f + titleFloat, Color.White * titleIn, 1.6f);
            Centered(spriteBatch, font, subline, 238, new Color(235, 225, 215) * Anim.Intro(_elapsed, TitleAt + 0.5f, 0.8f), 0.9f);

            if (_finalState != null) DrawStats(spriteBatch, font);

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
            // Four tiles that pop in one by one, numbers counting up to the final tally.
            string[] labels = { "Days", "Hope", "Upgrades", "Weapon" };
            string[] values =
            {
                $"{_statCounters[0].Value} / {DayInfo.FinalDay}",
                _statCounters[1].Value.ToString(),
                _statCounters[2].Value.ToString(),
                _finalState.EquippedWeapon?.DisplayName ?? "-"
            };
            float[] widths = { 150, 150, 150, 260 };
            const float gap = 16, top = 318, height = 110;
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
                    UITheme.DrawTextWithShadow(spriteBatch, font, labels[i], new Vector2(tile.X + (tile.Width - labelSize.X) / 2f, tile.Y + 16), new Color(210, 195, 185) * t, 0.66f);
                    float valueScale = i == 3 ? 0.9f : 1.3f;
                    var valueSize = UITheme.MeasureString(font, values[i]) * valueScale;
                    UITheme.DrawTextWithShadow(spriteBatch, font, values[i], new Vector2(tile.X + (tile.Width - valueSize.X) / 2f, tile.Y + 62 - valueSize.Y / 2f + 8), Color.White * t, valueScale);
                }
                x += widths[i] + gap;
            }
        }

        private static void Centered(SpriteBatch spriteBatch, SpriteFont font, string text, float y, Color color, float scale)
        {
            var size = UITheme.MeasureString(font, text) * scale;
            UITheme.DrawTextWithShadow(spriteBatch, font, text, new Vector2(640 - size.X / 2f, y), color, scale);
        }
    }
}
