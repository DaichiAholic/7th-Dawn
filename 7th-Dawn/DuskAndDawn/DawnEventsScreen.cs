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
            _acceptButton = new Button(new RectangleF(300, 450, 280, 70), "Accept");
            _declineButton = new Button(new RectangleF(650, 450, 280, 70), "Decline");
            _continueButton = new Button(new RectangleF(475, 550, 280, 70), "Continue");
        }

        public override void Update(GameTime gameTime)
        {
            if (_playerState.IsGameOver)
            {
                ScreenManager.ReplaceScreen(new GameOverScreen(Game));
                return;
            }

            var mouse = Mouse.GetState();

            if (InputChecker.IsNewLeftClick(mouse, _previousMouse))
            {
                if (!_resolved)
                {
                    if (_acceptButton.Contains(mouse.X, mouse.Y))
                    {
                        ResolveAccept();
                        _resolved = true;
                    }
                    else if (_declineButton.Contains(mouse.X, mouse.Y))
                    {
                        _resultLog = "You let it pass.";
                        _resolved = true;
                    }
                }
                else if (_continueButton.Contains(mouse.X, mouse.Y))
                {
                    ScreenManager.ShowScreen(new BaseBuilding(Game, _playerState));
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
                _resultLog = "You can't afford that — the deal falls through.";
            }
        }

        public override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(new Color(230, 200, 160));

            var spriteBatch = Game1.SpriteBatch;
            var font = Game1.Font;
            spriteBatch.Begin();

            spriteBatch.DrawString(font, _event.Title, new Vector2(300, 200), Color.Black);
            spriteBatch.DrawString(font, _event.Description, new Vector2(300, 240), Color.Black);
            spriteBatch.DrawString(font, $"Price: {_event.CostLabel()}", new Vector2(300, 280), new Color(120, 30, 30));
            spriteBatch.DrawString(font, $"Reward: {_event.RewardLabel()}", new Vector2(300, 310), new Color(30, 100, 30));

            if (!_resolved)
            {
                DrawButton(spriteBatch, font, _acceptButton, new Color(80, 140, 90));
                DrawButton(spriteBatch, font, _declineButton, new Color(140, 80, 80));
            }
            else
            {
                spriteBatch.DrawString(font, _resultLog, new Vector2(300, 400), Color.Black);
                DrawButton(spriteBatch, font, _continueButton, new Color(90, 90, 140));
            }

            spriteBatch.End();
        }

        private void DrawButton(SpriteBatch spriteBatch, SpriteFont font, Button button, Color color)
        {
            spriteBatch.FillRectangle(button.Bounds, color);
            spriteBatch.DrawRectangle(button.Bounds, Color.White, 2f);
            spriteBatch.DrawString(font, button.Label, new Vector2(button.Bounds.X + 20, button.Bounds.Y + 20), Color.White);
        }
    }
}
