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
    public class DawnEventsScreen : GameScreen
    {
        private Game1 Game1 => (Game1)Game;
        private readonly PlayerState _playerState;
        private readonly MorningEvent _event;

        private Button _acceptButton;
        private Button _declineButton;
        private Button _continueButton;

        private bool _resolved;
        private string _resultLog = "";
        private MouseState _previousMouse;

        public DawnEventsScreen(Game game, PlayerState playerState) : base(game)
        {
            _playerState = playerState;
            _event = MorningEventPool.GetRandom(new Random());
        }

        public override void Initialize()
        {
            base.Initialize();

            // Seeds with the real current mouse state instead of a blank default, so a click
            // still held down from the previous screen doesn't read as a brand-new click here.
            _previousMouse = Mouse.GetState();

            _acceptButton = new Button(new RectangleF(300, 450, 280, 70), "Accept");
            _declineButton = new Button(new RectangleF(650, 450, 280, 70), "Decline");
            _continueButton = new Button(new RectangleF(475, 550, 280, 70), "Continue");
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

            if (!_resolved)
            {
                _acceptButton.UpdateAnimation(dt, _acceptButton.Contains(mouse.X, mouse.Y));
                _declineButton.UpdateAnimation(dt, _declineButton.Contains(mouse.X, mouse.Y));
            }
            else
            {
                _continueButton.UpdateAnimation(dt, _continueButton.Contains(mouse.X, mouse.Y));
            }

            if (InputChecker.IsNewLeftClick(mouse, _previousMouse))
            {
                if (!_resolved)
                {
                    if (_acceptButton.Contains(mouse.X, mouse.Y))
                    {
                        _acceptButton.TriggerPress();
                        ResolveAccept();
                        _resolved = true;
                    }
                    else if (_declineButton.Contains(mouse.X, mouse.Y))
                    {
                        _declineButton.TriggerPress();
                        _resultLog = "You let it pass.";
                        _resolved = true;
                    }
                }
                else if (_continueButton.Contains(mouse.X, mouse.Y))
                {
                    _continueButton.TriggerPress();
                    ScreenManager.ShowScreen(new BaseBuilding(Game, _playerState), ScreenTransitions.FadeTransition(GraphicsDevice));
                }
            }

            _previousMouse = mouse;
        }

        private void ResolveAccept()
        {
            bool canAfford = _playerState.Food >= _event.FoodCost
                           && _playerState.Planks >= _event.PlanksCost
                           && _playerState.Scraps >= _event.ScrapsCost;

            if (canAfford)
            {
                _playerState.TrySpend(_event.FoodCost, _event.PlanksCost, _event.ScrapsCost);
                _playerState.AddResources(_event.FoodReward, _event.PlanksReward, _event.ScrapsReward);
                _playerState.ChangeHope(_event.HopeReward - _event.HopeCost);
                _resultLog = $"Deal made: gained {_event.RewardLabel()}.";
            }
            else
            {
                _resultLog = "You can't afford that - the deal falls through.";
            }
        }

        public override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(new Color(235, 205, 165));

            var spriteBatch = Game1.SpriteBatch;
            var font = Game1.Font;
            spriteBatch.Begin();

            UITheme.FillGradientRect(spriteBatch, new RectangleF(0, 0, 1280, 720), new Color(245, 218, 178), new Color(215, 180, 140), 10);

            var eventPanel = new RectangleF(260, 170, 760, 200);
            UITheme.DrawPanel(spriteBatch, eventPanel, new Color(255, 250, 238), new Color(232, 216, 188), new Color(150, 110, 70), 3f, 16f, shadowStrength: 0.6f);

            UITheme.DrawTextWithShadow(spriteBatch, font, _event.Title, new Vector2(eventPanel.X + 40, eventPanel.Y + 30), Color.Black);
            UITheme.DrawTextWithShadow(spriteBatch, font, _event.Description, new Vector2(eventPanel.X + 40, eventPanel.Y + 70), Color.Black);
            UITheme.DrawTextWithShadow(spriteBatch, font, $"Price: {_event.CostLabel()}", new Vector2(eventPanel.X + 40, eventPanel.Y + 110), new Color(140, 30, 30));
            UITheme.DrawTextWithShadow(spriteBatch, font, $"Reward: {_event.RewardLabel()}", new Vector2(eventPanel.X + 40, eventPanel.Y + 140), new Color(20, 110, 30));

            if (!_resolved)
            {
                DrawButton(spriteBatch, font, _acceptButton, new Color(90, 155, 100), new Color(64, 118, 72));
                DrawButton(spriteBatch, font, _declineButton, new Color(160, 90, 90), new Color(120, 60, 60));
            }
            else
            {
                var resultPanel = new RectangleF(300, 390, 680, 60);
                UITheme.DrawPanel(spriteBatch, resultPanel, new Color(255, 250, 238), new Color(232, 216, 188), new Color(150, 110, 70), 2f, 12f, shadowStrength: 0.5f);
                UITheme.DrawTextWithShadow(spriteBatch, font, _resultLog, new Vector2(resultPanel.X + 20, resultPanel.Y + 16), Color.Black);
                DrawButton(spriteBatch, font, _continueButton, new Color(95, 95, 150), new Color(70, 70, 115));
            }

            spriteBatch.End();
        }

        private void DrawButton(SpriteBatch spriteBatch, SpriteFont font, Button button, Color baseTop, Color baseBottom)
        {
            float hover = button.HoverAmount;
            Color top = UITheme.Brighten(baseTop, hover * 0.2f);
            Color bottom = UITheme.Brighten(baseBottom, hover * 0.2f);
            Color border = Color.Lerp(Color.White * 0.8f, Color.White, hover);
            float borderThickness = MathHelper.Lerp(2f, 3f, hover);

            float squash = button.PressAmount * 4f;
            var bounds = button.Bounds;
            var drawBounds = new RectangleF(bounds.X + squash, bounds.Y + squash / 2f, bounds.Width - squash * 2f, bounds.Height - squash);

            UITheme.DrawPanel(spriteBatch, drawBounds, top, bottom, border, borderThickness, 14f, shadowStrength: 0.6f);
            var textSize = font.MeasureString(button.Label);
            var textPos = new Vector2(drawBounds.X + (drawBounds.Width - textSize.X) / 2f, drawBounds.Y + (drawBounds.Height - textSize.Y) / 2f);
            UITheme.DrawTextWithShadow(spriteBatch, font, button.Label, textPos, Color.White);
        }
    }
}
