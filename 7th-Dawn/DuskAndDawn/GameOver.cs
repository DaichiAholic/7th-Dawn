using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using MonoGame.Extended.Screens;

namespace DuskAndDawn
{
    /// <summary>
    /// The end of a run, either way: Hope ran out (the night wins), or the seventh dawn
    /// broke with the house still standing - better still if the Hollow Knight fell.
    /// Enter starts a new run, Esc returns to the main menu.
    /// </summary>
    public class GameOverScreen : GameScreen
    {
        private Game1 Game1 => (Game1)Game;
        private readonly bool _victory;
        private readonly PlayerState _finalState;
        private KeyboardState _previousKeyboard;
        private float _elapsed;

        public GameOverScreen(Game game, bool victory, PlayerState finalState) : base(game)
        {
            _victory = victory;
            _finalState = finalState;
        }

        public override void Initialize()
        {
            base.Initialize();
            // An Esc/Enter still held from the previous screen shouldn't count as a new press.
            _previousKeyboard = Keyboard.GetState();
        }

        public override void Update(GameTime gameTime)
        {
            _elapsed += (float)gameTime.ElapsedGameTime.TotalSeconds;

            var keyboard = Keyboard.GetState();
            bool Pressed(Keys key) => keyboard.IsKeyDown(key) && !_previousKeyboard.IsKeyDown(key);

            if (!ScreenTransitions.IsTransitioning)
            {
                if (Pressed(Keys.Enter))
                {
                    Game1.StartNewRun();
                }
                else if (Pressed(Keys.Escape))
                {
                    Game1.EndRunToMainMenu();
                }
            }
            _previousKeyboard = keyboard;
        }

        public override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.Black);

            var spriteBatch = Game1.SpriteBatch;
            var font = Game1.Font;
            UITheme.BeginCanvas(spriteBatch);

            // A slow gradient and a gentle fade-in on the text: red dusk for a loss, gold
            // sunrise for a win.
            if (_victory)
            {
                UITheme.FillGradientRect(spriteBatch, new RectangleF(0, 0, 1280, 720), new Color(40, 30, 50), new Color(230, 150, 90), 12);
                float rise = UITheme.EaseOutCubic(MathHelper.Clamp(_elapsed / 3f, 0f, 1f));
                UITheme.DrawGlow(spriteBatch, new Vector2(640, 760 - rise * 120f), 420f, new Color(255, 220, 150) * 0.55f);
            }
            else
            {
                UITheme.FillGradientRect(spriteBatch, new RectangleF(0, 0, 1280, 720), new Color(30, 6, 10), Color.Black, 10);
            }

            float fadeIn = UITheme.EaseOutCubic(MathHelper.Clamp(_elapsed / 1.4f, 0f, 1f));
            float promptPulse = UITheme.PulseSine(_elapsed, 2.5f);

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

            Centered(headline, 280, Color.White * fadeIn, 1.5f);
            Centered(subline, 340, new Color(230, 220, 215) * fadeIn, 0.9f);

            if (_finalState != null)
            {
                string stats = $"Hope {_finalState.Hope}    Weapon: {_finalState.EquippedWeapon.DisplayName}    Base upgrades: {_finalState.UpgradesBought}";
                Centered(stats, 390, new Color(210, 200, 200) * fadeIn, 0.75f);
            }

            Color promptColor = Color.Lerp(new Color(170, 170, 170), Color.White, promptPulse) * fadeIn;
            Centered("Enter: new run     Esc: main menu", 460, promptColor, 1f);

            spriteBatch.End();

            void Centered(string text, float y, Color color, float scale)
            {
                var size = UITheme.MeasureString(font, text) * scale;
                UITheme.DrawTextWithShadow(spriteBatch, font, text, new Vector2(640 - size.X / 2f, y), color, scale);
            }
        }
    }
}
