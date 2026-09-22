using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using MonoGame.Extended.Screens;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DuskAndDawn
{
    public class Dawn : GameScreen
    {
        private Game1 Game1 => (Game1)Game;
        private readonly PlayerState _playerState;
        private readonly int _roomsCleared;
        private readonly int _roomsVisited;

        private Button _continueButton;
        private MouseState _previousMouse;

        public Dawn(Game game, PlayerState playerState, int roomsCleared, int roomsVisited) : base(game)
        {
            _playerState = playerState;
            _roomsCleared = roomsCleared;
            _roomsVisited = roomsVisited;
        }

        public override void Initialize()
        {
            base.Initialize();

            // Seeds with the real current mouse state instead of a blank default, so a click
            // still held down from the previous screen doesn't read as a brand-new click here.
            _previousMouse = Mouse.GetState();

            _continueButton = new Button(new RectangleF(490, 500, 300, 70), "Continue to Morning");
        }

        public override void Update(GameTime gameTime)
        {
            if (_playerState.IsGameOver)
            {
                ScreenManager.ReplaceScreen(new GameOverScreen(Game), ScreenTransitions.Fade(GraphicsDevice));
                return;
            }

            var mouse = Mouse.GetState();
            if (InputChecker.IsNewLeftClick(mouse, _previousMouse) && _continueButton.Contains(mouse.X, mouse.Y))
            {
                ScreenManager.ShowScreen(new DawnEventsScreen(Game, _playerState), ScreenTransitions.Fade(GraphicsDevice));
            }
            _previousMouse = mouse;
        }

        public override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(new Color(200, 160, 120));

            var spriteBatch = Game1.SpriteBatch;
            var font = Game1.Font;
            spriteBatch.Begin();

            spriteBatch.DrawString(font, $"Rooms resolved: {_roomsCleared} / {_roomsVisited}", new Vector2(300, 280), Color.Black);
            spriteBatch.DrawString(font, $"Food: {_playerState.Food}   Planks: {_playerState.Planks}   Scraps: {_playerState.Scraps}", new Vector2(300, 320), Color.Black);

            spriteBatch.FillRectangle(_continueButton.Bounds, new Color(90, 90, 140));
            spriteBatch.DrawRectangle(_continueButton.Bounds, Color.White, 2f);
            spriteBatch.DrawString(font, _continueButton.Label, new Vector2(_continueButton.Bounds.X + 20, _continueButton.Bounds.Y + 20), Color.White);

            spriteBatch.End();
        }
    }
}