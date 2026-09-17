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
    public class GameOverScreen : GameScreen
    {
        private Game1 Game1 => (Game1)Game;
        private KeyboardState _previousKeyboard;
        private float _elapsed;

        public GameOverScreen(Game game) : base(game) { }

        public override void Update(GameTime gameTime)
        {
            _elapsed += (float)gameTime.ElapsedGameTime.TotalSeconds;

            var keyboard = Keyboard.GetState();
            if (keyboard.IsKeyDown(Keys.Enter) && !_previousKeyboard.IsKeyDown(Keys.Enter))
            {
                ScreenManager.ShowScreen(new PreparationScreen(Game, new PlayerState()));
            }
            _previousKeyboard = keyboard;
        }

        public override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.Black);

            var spriteBatch = Game1.SpriteBatch;
            var font = Game1.Font;
            spriteBatch.Begin();

            // A slow gradient and a gentle fade-in on the text, instead of the words simply
            // appearing instantly on a flat black screen.
            UITheme.FillGradientRect(spriteBatch, new RectangleF(0, 0, 1280, 720), new Color(30, 6, 10), Color.Black, 10);

            float fadeIn = UITheme.EaseOutCubic(MathHelper.Clamp(_elapsed / 1.4f, 0f, 1f));
            float promptPulse = UITheme.PulseSine(_elapsed, 2.5f);

            const string headline = "Hope is gone. The night wins.";
            const string prompt = "Press Enter to start over.";

            var headlineSize = font.MeasureString(headline);
            var headlinePos = new Vector2(640 - headlineSize.X / 2f, 320);
            UITheme.DrawTextWithShadow(spriteBatch, font, headline, headlinePos, Color.White * fadeIn);

            var promptSize = font.MeasureString(prompt);
            var promptPos = new Vector2(640 - promptSize.X / 2f, 365);
            Color promptColor = Color.Lerp(new Color(170, 170, 170), Color.White, promptPulse) * fadeIn;
            UITheme.DrawTextWithShadow(spriteBatch, font, prompt, promptPos, promptColor);

            spriteBatch.End();
        }
    }
}
