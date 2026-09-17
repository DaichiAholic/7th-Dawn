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

        public PreparationScreen(Game game, PlayerState playerState) : base(game)
        {
            _playerState = playerState;
        }

        public override void Initialize()
        {
            base.Initialize();

            // Seeds with the real current mouse state instead of a blank default, so a click
            // still held down from the previous screen doesn't read as a brand-new click here.
            _previousMouse = Mouse.GetState();

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
                ScreenManager.ReplaceScreen(new GameOverScreen(Game), ScreenTransitions.FadeTransition(GraphicsDevice));
                return;
            }

            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            var mouse = Mouse.GetState();

            bool hitStart = _startButton.Contains(mouse.X, mouse.Y);
            _startButton.UpdateAnimation(dt, hitStart);
            for (int i = 0; i < _weaponButtons.Count; i++)
            {
                _weaponButtons[i].UpdateAnimation(dt, _weaponButtons[i].Contains(mouse.X, mouse.Y));
            }

            if (InputChecker.IsNewLeftClick(mouse, _previousMouse))
            {
                for (int i = 0; i < _weaponButtons.Count; i++)
                {
                    if (_weaponButtons[i].Contains(mouse.X, mouse.Y))
                    {
                        _weaponButtons[i].TriggerPress();
                        _playerState.EquippedWeapon = _playerState.Inventory[i];
                    }
                }

                if (hitStart)
                {
                    _startButton.TriggerPress();
                    ScreenManager.ShowScreen(new NightScavengingScreen(Game, _playerState), ScreenTransitions.FadeTransition(GraphicsDevice));
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

            UITheme.FillGradientRect(spriteBatch, new RectangleF(0, 0, 1280, 720), new Color(22, 20, 34), new Color(10, 9, 16), 10);
            UITheme.DrawTextWithShadow(spriteBatch, font, "Choose your weapon", new Vector2(100, 190), Color.White);

            for (int i = 0; i < _weaponButtons.Count; i++)
            {
                var weapon = _playerState.Inventory[i];
                var button = _weaponButtons[i];
                bool equipped = weapon == _playerState.EquippedWeapon;
                float hover = button.HoverAmount;

                Color top = equipped ? new Color(150, 112, 44) : new Color(66, 66, 78);
                Color bottom = equipped ? new Color(110, 80, 28) : new Color(46, 46, 56);
                Color border = equipped ? Color.Gold : Color.Lerp(Color.White * 0.6f, Color.White, hover);
                float borderThickness = equipped ? 3f : MathHelper.Lerp(1.5f, 3f, hover);

                if (!equipped && hover > 0f)
                {
                    top = UITheme.Brighten(top, hover * 0.2f);
                    bottom = UITheme.Brighten(bottom, hover * 0.2f);
                }

                float squash = button.PressAmount * 3f;
                var bounds = button.Bounds;
                var drawBounds = new RectangleF(bounds.X + squash, bounds.Y + squash / 2f, bounds.Width - squash * 2f, bounds.Height - squash);

                UITheme.DrawPanel(spriteBatch, drawBounds, top, bottom, border, borderThickness, 14f, shadowStrength: 0.6f);
                UITheme.DrawTextWithShadow(spriteBatch, font, weapon.Name, new Vector2(drawBounds.X + 10, drawBounds.Y + 10), Color.White);
                UITheme.DrawTextWithShadow(spriteBatch, font, weapon.DiceLabel, new Vector2(drawBounds.X + 10, drawBounds.Y + 45), new Color(215, 215, 215));
                if (equipped)
                {
                    UITheme.DrawTextWithShadow(spriteBatch, font, "Equipped", new Vector2(drawBounds.X + 10, drawBounds.Y + drawBounds.Height - 32), new Color(255, 235, 190));
                }
            }

            {
                float hover = _startButton.HoverAmount;
                Color top = Color.Lerp(new Color(85, 145, 95), new Color(105, 170, 115), hover);
                Color bottom = Color.Lerp(new Color(60, 110, 68), new Color(78, 132, 86), hover);
                Color border = Color.Lerp(Color.White * 0.8f, Color.White, hover);
                float borderThickness = MathHelper.Lerp(2f, 3f, hover);

                float squash = _startButton.PressAmount * 4f;
                var bounds = _startButton.Bounds;
                var drawBounds = new RectangleF(bounds.X + squash, bounds.Y + squash / 2f, bounds.Width - squash * 2f, bounds.Height - squash);

                UITheme.DrawPanel(spriteBatch, drawBounds, top, bottom, border, borderThickness, 14f, shadowStrength: 0.7f);
                var textSize = font.MeasureString(_startButton.Label);
                var textPos = new Vector2(drawBounds.X + (drawBounds.Width - textSize.X) / 2f, drawBounds.Y + (drawBounds.Height - textSize.Y) / 2f);
                UITheme.DrawTextWithShadow(spriteBatch, font, _startButton.Label, textPos, Color.White);
            }

            spriteBatch.End();
        }
    }
}
