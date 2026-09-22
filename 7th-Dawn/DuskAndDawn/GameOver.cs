using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.Screens;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DuskAndDawn
{
    public class GameOverScreen : GameScreen
    {
        private Game1 Game1 => (Game1)Game;
        private KeyboardState _previousKeyboard;

        public GameOverScreen(Game game) : base(game) { }

        public override void Update(GameTime gameTime)
        {
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
            spriteBatch.DrawString(font, "Hope is gone. The night wins.", new Vector2(400, 320), Color.White);
            spriteBatch.DrawString(font, "Press Enter to start over.", new Vector2(400, 360), Color.LightGray);
            spriteBatch.End();
        }
    }
}
