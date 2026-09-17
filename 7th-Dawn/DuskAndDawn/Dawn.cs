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
                ScreenManager.ReplaceScreen(new GameOverScreen(Game), ScreenTransitions.FadeTransition(GraphicsDevice));
                return;
            }

            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            var mouse = Mouse.GetState();
            bool isHovered = _continueButton.Contains(mouse.X, mouse.Y);
            _continueButton.UpdateAnimation(dt, isHovered);

            if (InputChecker.IsNewLeftClick(mouse, _previousMouse) && isHovered)
            {
                _continueButton.TriggerPress();
                ScreenManager.ShowScreen(new DawnEventsScreen(Game, _playerState), ScreenTransitions.FadeTransition(GraphicsDevice));
            }
            _previousMouse = mouse;
        }

        public override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(new Color(224, 178, 128));

            var spriteBatch = Game1.SpriteBatch;
            var font = Game1.Font;
            spriteBatch.Begin();

            // Warm gradient sky standing in for the returning dawn light, instead of one
            // flat color swatch.
            UITheme.FillGradientRect(spriteBatch, new RectangleF(0, 0, 1280, 720), new Color(255, 205, 150), new Color(210, 150, 120), 12);

            var statsPanel = new RectangleF(260, 250, 760, 120);
            UITheme.DrawPanel(spriteBatch, statsPanel, new Color(70, 45, 35), new Color(48, 30, 24), new Color(150, 105, 70), 3f, 16f, shadowStrength: 0.7f);
            UITheme.DrawTextWithShadow(spriteBatch, font, $"Rooms resolved: {_roomsCleared} / {_roomsVisited}", new Vector2(statsPanel.X + 40, statsPanel.Y + 30), Color.White);
            UITheme.DrawTextWithShadow(spriteBatch, font, $"Food: {_playerState.Food}   Planks: {_playerState.Planks}   Scraps: {_playerState.Scraps}", new Vector2(statsPanel.X + 40, statsPanel.Y + 70), new Color(230, 220, 210));

            float hover = _continueButton.HoverAmount;
            Color top = Color.Lerp(new Color(95, 95, 150), new Color(120, 120, 180), hover);
            Color bottom = Color.Lerp(new Color(70, 70, 115), new Color(90, 90, 140), hover);
            Color border = Color.Lerp(Color.White * 0.8f, Color.White, hover);
            float borderThickness = MathHelper.Lerp(2f, 3f, hover);

            float squash = _continueButton.PressAmount * 4f;
            var bounds = _continueButton.Bounds;
            var drawBounds = new RectangleF(bounds.X + squash, bounds.Y + squash / 2f, bounds.Width - squash * 2f, bounds.Height - squash);

            UITheme.DrawPanel(spriteBatch, drawBounds, top, bottom, border, borderThickness, 14f, shadowStrength: 0.7f);
            var textSize = font.MeasureString(_continueButton.Label);
            var textPos = new Vector2(drawBounds.X + (drawBounds.Width - textSize.X) / 2f, drawBounds.Y + (drawBounds.Height - textSize.Y) / 2f);
            UITheme.DrawTextWithShadow(spriteBatch, font, _continueButton.Label, textPos, Color.White);

            spriteBatch.End();
        }
    }
}
