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

        private readonly List<Button> _districtButtons = new List<Button>();
        private readonly List<Button> _weaponButtons = new List<Button>();
        private Button _startButton;
        private MouseState _previousMouse;

        // Weapon cards: 4 per row, up to 3 rows. The Workshop can fill this over a long run.
        private const int WeaponsPerRow = 4;
        private const float WeaponCardWidth = 260f;
        private const int IconScale = 2; // 32px icons drawn at 64px
        private const int MaxWeaponRows = 3;

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

            // If the saved choice is somehow locked (shouldn't happen, Archive never
            // downgrades), fall back to the Outskirts.
            if (!_playerState.IsDistrictUnlocked(_playerState.SelectedDistrict))
            {
                _playerState.SelectedDistrict = District.VillageOutskirts;
            }

            _districtButtons.Clear();
            for (int i = 0; i < DistrictInfo.All.Length; i++)
            {
                var district = DistrictInfo.All[i];
                var bounds = new RectangleF(100 + i * 370, 90, 350, 100);
                var button = new Button(bounds, DistrictInfo.Name(district));
                button.Enabled = _playerState.IsDistrictUnlocked(district);
                _districtButtons.Add(button);
            }

            _weaponButtons.Clear();
            int shown = Math.Min(_playerState.Inventory.Count, WeaponsPerRow * MaxWeaponRows);
            for (int i = 0; i < shown; i++)
            {
                float x = 100 + (i % WeaponsPerRow) * (WeaponCardWidth + 20);
                float y = 270 + (i / WeaponsPerRow) * 100;
                _weaponButtons.Add(new Button(new RectangleF(x, y, WeaponCardWidth, 88), _playerState.Inventory[i].Name));
            }

            _startButton = new Button(new RectangleF(490, 620, 300, 70), "Head Into the Night");
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
            foreach (var button in _weaponButtons)
            {
                button.UpdateAnimation(dt, button.Contains(mouse.X, mouse.Y));
            }
            foreach (var button in _districtButtons)
            {
                button.UpdateAnimation(dt, button.Contains(mouse.X, mouse.Y));
            }

            if (InputChecker.IsNewLeftClick(mouse, _previousMouse))
            {
                for (int i = 0; i < _districtButtons.Count; i++)
                {
                    // Button.Contains already returns false for locked (disabled) districts.
                    if (_districtButtons[i].Contains(mouse.X, mouse.Y))
                    {
                        _districtButtons[i].TriggerPress();
                        _playerState.SelectedDistrict = DistrictInfo.All[i];
                    }
                }

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

            DrawDistricts(spriteBatch, font);
            DrawWeapons(spriteBatch, font);
            DrawItemsSummary(spriteBatch, font);
            DrawStartButton(spriteBatch, font);

            spriteBatch.End();
        }

        private void DrawDistricts(SpriteBatch spriteBatch, SpriteFont font)
        {
            UITheme.DrawTextWithShadow(spriteBatch, font, "Choose where to scavenge", new Vector2(100, 56), Color.White);

            for (int i = 0; i < _districtButtons.Count; i++)
            {
                var district = DistrictInfo.All[i];
                var button = _districtButtons[i];
                bool unlocked = button.Enabled;
                bool selected = unlocked && district == _playerState.SelectedDistrict;
                float hover = button.HoverAmount;

                Color top, bottom, border;
                if (!unlocked)
                {
                    top = new Color(34, 32, 40);
                    bottom = new Color(24, 22, 30);
                    border = Color.White * 0.15f;
                }
                else if (selected)
                {
                    // Ember tones - the selected district "glows" like a corruption tell.
                    top = new Color(130, 62, 38);
                    bottom = new Color(90, 40, 24);
                    border = new Color(255, 140, 70);
                }
                else
                {
                    top = UITheme.Brighten(new Color(60, 56, 72), hover * 0.2f);
                    bottom = UITheme.Brighten(new Color(42, 40, 52), hover * 0.2f);
                    border = Color.Lerp(Color.White * 0.5f, Color.White, hover);
                }

                float squash = button.PressAmount * 3f;
                var bounds = button.Bounds;
                var drawBounds = new RectangleF(bounds.X + squash, bounds.Y + squash / 2f, bounds.Width - squash * 2f, bounds.Height - squash);
                UITheme.DrawPanel(spriteBatch, drawBounds, top, bottom, border, selected ? 3f : 2f, 14f, shadowStrength: 0.6f);

                Color nameColor = unlocked ? Color.White : new Color(130, 125, 135);
                UITheme.DrawTextWithShadow(spriteBatch, font, DistrictInfo.Name(district), new Vector2(drawBounds.X + 12, drawBounds.Y + 10), nameColor);

                if (unlocked)
                {
                    UITheme.DrawTextWithShadow(spriteBatch, font, $"Corruption {DistrictInfo.Corruption(district)}", new Vector2(drawBounds.X + 12, drawBounds.Y + 38), new Color(255, 180, 120), 0.8f);
                    UITheme.DrawTextWithShadow(spriteBatch, font, DistrictInfo.Tagline(district), new Vector2(drawBounds.X + 12, drawBounds.Y + 64), new Color(210, 205, 215), 0.65f);
                }
                else
                {
                    UITheme.DrawTextWithShadow(spriteBatch, font, $"Locked - needs Archive Lv {DistrictInfo.RequiredArchiveLevel(district)}", new Vector2(drawBounds.X + 12, drawBounds.Y + 50), new Color(150, 145, 155), 0.8f);
                }
            }
        }

        private void DrawWeapons(SpriteBatch spriteBatch, SpriteFont font)
        {
            UITheme.DrawTextWithShadow(spriteBatch, font, "Choose your weapon", new Vector2(100, 234), Color.White);

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

                // Icon on the left (empty slot if the weapon has no art yet), text beside it.
                float iconSize = 32 * IconScale;
                var iconPos = new Vector2(drawBounds.X + 12, drawBounds.Y + (drawBounds.Height - iconSize) / 2f);
                UITheme.DrawIconSlot(spriteBatch, Game1.GetWeaponIcon(weapon), iconPos, IconScale);

                float textX = drawBounds.X + 12 + iconSize + 12;
                UITheme.DrawTextWithShadow(spriteBatch, font, weapon.Name, new Vector2(textX, drawBounds.Y + 8), Color.White);

                string stats = weapon.IsHoly ? $"{weapon.DiceLabel}  +{weapon.CorruptionBonus}/corr" : weapon.DiceLabel;
                UITheme.DrawTextWithShadow(spriteBatch, font, stats, new Vector2(textX, drawBounds.Y + 36), new Color(215, 215, 215), 0.85f);

                if (equipped)
                {
                    UITheme.DrawTextWithShadow(spriteBatch, font, "Equipped", new Vector2(textX, drawBounds.Y + drawBounds.Height - 26), new Color(255, 235, 190), 0.8f);
                }
            }

            if (_playerState.Inventory.Count > _weaponButtons.Count)
            {
                int hidden = _playerState.Inventory.Count - _weaponButtons.Count;
                UITheme.DrawTextWithShadow(spriteBatch, font, $"+{hidden} more not shown", new Vector2(100, 564), new Color(170, 165, 175), 0.8f);
            }
        }

        // Read-only summary of what's in the pack - items are crafted at the Infirmary and
        // Kitchen, and every one you own comes along.
        private void DrawItemsSummary(SpriteBatch spriteBatch, SpriteFont font)
        {
            string summary = _playerState.Items.Count == 0
                ? "Pack: empty"
                : "Pack: " + string.Join(", ", _playerState.Items
                    .GroupBy(item => item.Name)
                    .Select(g => g.Count() > 1 ? $"{g.Key} x{g.Count()}" : g.Key));

            UITheme.DrawTextWithShadow(spriteBatch, font, summary, new Vector2(100, 590), new Color(210, 220, 200), 0.85f);
        }

        private void DrawStartButton(SpriteBatch spriteBatch, SpriteFont font)
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
    }
}