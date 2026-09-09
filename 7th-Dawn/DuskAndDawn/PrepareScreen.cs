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
    public class PreparationScreen : GameScreen
    {
        private Game1 Game1 => (Game1)Game;
        private readonly PlayerState _playerState;

        private readonly List<Button> _weaponButtons = new List<Button>();
        private Button _startButton;
        private MouseState _previousMouse;

        private int _clickCount;
        private string _lastClickInfo = "no clicks yet";

        public PreparationScreen(Game game, PlayerState playerState) : base(game)
        {
            _playerState = playerState;
        }

        public override void Initialize()
        {
            base.Initialize();

            _weaponButtons.Clear();
            for (int i = 0; i < _playerState.Inventory.Count; i++)
            {
                var bounds = new RectangleF(100 + i * 220, 260, 200, 140);
                _weaponButtons.Add(new Button(bounds, _playerState.Inventory[i].Name));
            }

            _startButton = new Button(new RectangleF(490, 520, 300, 70), "Head Into the Night");
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

                _clickCount++;
                bool hitStart = _startButton.Contains(mouse.X, mouse.Y);
                _lastClickInfo = $"click #{_clickCount} at ({mouse.X},{mouse.Y}) - inside Start button: {hitStart}";


                for (int i = 0; i < _weaponButtons.Count; i++)
                {
                    if (_weaponButtons[i].Contains(mouse.X, mouse.Y))
                    {
                        _playerState.EquippedWeapon = _playerState.Inventory[i];
                    }
                }

                if (hitStart)
                {
                    ScreenManager.ShowScreen(new NightScavengingScreen(Game, _playerState));
                }
            }

            _previousMouse = mouse;
        }

        public override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(new Color(15, 15, 25)); 

            var spriteBatch = Game1.SpriteBatch;
            var font = Game1.Font;
            spriteBatch.Begin();

            spriteBatch.DrawString(font, "Choose your weapon", new Vector2(100, 190), Color.White);

            for (int i = 0; i < _weaponButtons.Count; i++)
            {
                var weapon = _playerState.Inventory[i];
                var button = _weaponButtons[i];
                bool equipped = weapon == _playerState.EquippedWeapon;

                spriteBatch.FillRectangle(button.Bounds, equipped ? new Color(120, 90, 40) : new Color(60, 60, 70));
                spriteBatch.DrawRectangle(button.Bounds, equipped ? Color.Gold : Color.White, equipped ? 3f : 1f);
                spriteBatch.DrawString(font, weapon.Name, new Vector2(button.Bounds.X + 10, button.Bounds.Y + 10), Color.White);
                spriteBatch.DrawString(font, weapon.DiceLabel, new Vector2(button.Bounds.X + 10, button.Bounds.Y + 45), Color.LightGray);
            }

            spriteBatch.FillRectangle(_startButton.Bounds, new Color(80, 140, 90));
            spriteBatch.DrawRectangle(_startButton.Bounds, Color.White, 2f);
            spriteBatch.DrawString(font, _startButton.Label, new Vector2(_startButton.Bounds.X + 20, _startButton.Bounds.Y + 22), Color.White);

            //Debug
            var mouseNow = Mouse.GetState();
            spriteBatch.DrawString(font, $"Live mouse: ({mouseNow.X},{mouseNow.Y})   Left button: {mouseNow.LeftButton}", new Vector2(20, 20), Color.Lime);
            spriteBatch.DrawString(font, _lastClickInfo, new Vector2(20, 50), Color.Lime);
            spriteBatch.DrawString(font, $"Start button bounds: X {_startButton.Bounds.X}-{_startButton.Bounds.X + _startButton.Bounds.Width}, Y {_startButton.Bounds.Y}-{_startButton.Bounds.Y + _startButton.Bounds.Height}", new Vector2(20, 80), Color.Lime);
            //END DEBUG

            spriteBatch.End();
        }
    }
}
