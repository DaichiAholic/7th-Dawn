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
    public class BaseBuilding : GameScreen
    {
        private Game1 Game1 => (Game1)Game;
        private readonly PlayerState _playerState;

        private const int MaxRoomLevel = 3;

        private static readonly BaseRoomType[] AllRooms =
        {
            BaseRoomType.Storage, BaseRoomType.Workshop, BaseRoomType.Infirmary,
            BaseRoomType.Hearth, BaseRoomType.FungalGarden, BaseRoomType.Dormitory,
            BaseRoomType.Archive
        };

        private readonly Dictionary<BaseRoomType, Button> _roomButtons = new Dictionary<BaseRoomType, Button>();
        private Button _endDayButton;
        private string _statusLog = "";
        private MouseState _previousMouse;

        public BaseBuilding(Game game, PlayerState playerState) : base(game)
        {
            _playerState = playerState;
        }

        public override void Initialize()
        {
            base.Initialize();

            for (int i = 0; i < AllRooms.Length; i++)
            {
                var bounds = new RectangleF(60 + i * 165, 260, 145, 145);
                _roomButtons[AllRooms[i]] = new Button(bounds, AllRooms[i].ToString());
            }

            _endDayButton = new Button(new RectangleF(490, 500, 300, 70), "End the Day");
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
                foreach (var room in AllRooms)
                {
                    if (_roomButtons[room].Contains(mouse.X, mouse.Y))
                    {
                        TryUpgrade(room);
                    }
                }

                if (_endDayButton.Contains(mouse.X, mouse.Y))
                {
                    ScreenManager.ShowScreen(new PreparationScreen(Game, _playerState));
                }
            }

            _previousMouse = mouse;
        }

        private void TryUpgrade(BaseRoomType room)
        {
            int level = _playerState.RoomLevels[room];
            if (level >= MaxRoomLevel)
            {
                _statusLog = $"{room} is already at max level.";
                return;
            }

            var (food, planks, scraps) = GetUpgradeCost(room, level);

            if (_playerState.TrySpend(food, planks, scraps))
            {
                _playerState.RoomLevels[room] = level + 1;
                _statusLog = $"{room} upgraded to level {level + 1}.";
            }
            else
            {
                _statusLog = $"Not enough resources to upgrade {room} (needs {food} Food, {planks} Planks, {scraps} Scraps).";
            }
        }

        private (int food, int planks, int scraps) GetUpgradeCost(BaseRoomType room, int currentLevel)
        {
            int tier = currentLevel;
            return room switch
            {
                BaseRoomType.Storage => (0, 3 * tier, 3 * tier),
                BaseRoomType.Workshop => (0, 2 * tier, 4 * tier),
                BaseRoomType.Infirmary => (3 * tier, 0, 3 * tier),
                BaseRoomType.Hearth => (6 * tier, 0, 0),
                BaseRoomType.FungalGarden => (3 * tier, 3 * tier, 0),
                BaseRoomType.Dormitory => (0, 6 * tier, 0),
                BaseRoomType.Archive => (0, 0, 6 * tier),
                _ => (0, 0, 0)
            };
        }

        public override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(new Color(255, 240, 210)); 

            var spriteBatch = Game1.SpriteBatch;
            var font = Game1.Font;
            spriteBatch.Begin();

            DrawTopBar(spriteBatch, font);

            foreach (var room in AllRooms)
            {
                var button = _roomButtons[room];
                int level = _playerState.RoomLevels[room];
                bool maxed = level >= MaxRoomLevel;

                spriteBatch.FillRectangle(button.Bounds, maxed ? new Color(150, 200, 150) : new Color(200, 180, 150));
                spriteBatch.DrawRectangle(button.Bounds, Color.Black, 2f);
                spriteBatch.DrawString(font, room.ToString(), new Vector2(button.Bounds.X + 8, button.Bounds.Y + 8), Color.Black);
                spriteBatch.DrawString(font, $"Lv {level}", new Vector2(button.Bounds.X + 8, button.Bounds.Y + 105), Color.Black);
            }

            spriteBatch.FillRectangle(_endDayButton.Bounds, new Color(90, 90, 140));
            spriteBatch.DrawRectangle(_endDayButton.Bounds, Color.White, 2f);
            spriteBatch.DrawString(font, _endDayButton.Label, new Vector2(_endDayButton.Bounds.X + 20, _endDayButton.Bounds.Y + 20), Color.White);

            if (!string.IsNullOrEmpty(_statusLog))
            {
                spriteBatch.DrawString(font, _statusLog, new Vector2(60, 620), Color.DarkRed);
            }

            spriteBatch.End();
        }

        private void DrawTopBar(SpriteBatch spriteBatch, SpriteFont font)
        {
            spriteBatch.DrawString(font, $"Food: {_playerState.Food}   Planks: {_playerState.Planks}   Scraps: {_playerState.Scraps}",
                new Vector2(60, 40), Color.Black);

            var hopeBarMax = new RectangleF(60, 80, 400, 24);
            var hopeBarFill = new RectangleF(60, 80, 400 * (_playerState.Hope / (float)PlayerState.MaxHope), 24);
            spriteBatch.FillRectangle(hopeBarMax, Color.Black * 0.3f);
            spriteBatch.FillRectangle(hopeBarFill, new Color(200, 60, 140));
            spriteBatch.DrawRectangle(hopeBarMax, Color.Black, 2f);
            spriteBatch.DrawString(font, $"Hope: {_playerState.Hope}/{PlayerState.MaxHope}", new Vector2(470, 82), Color.Black);
        }
    }
}
