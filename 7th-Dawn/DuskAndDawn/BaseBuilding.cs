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

        // Grouped into two rows so the house layout reads as two floors, like a real
        // building cutaway - purely a visual grouping, doesn't change any game logic.
        private static readonly BaseRoomType[] UpperFloorRooms =
        {
            BaseRoomType.Infirmary, BaseRoomType.Barrack, BaseRoomType.Archive
        };

        private static readonly BaseRoomType[] GroundFloorRooms =
        {
            BaseRoomType.Storage, BaseRoomType.Workshop, BaseRoomType.Kitchen
        };

        private static IEnumerable<BaseRoomType> AllRooms => UpperFloorRooms.Concat(GroundFloorRooms);

        private readonly Dictionary<BaseRoomType, Button> _roomButtons = new Dictionary<BaseRoomType, Button>();
        private Button _endDayButton;
        private string _statusLog = "";
        private MouseState _previousMouse;

        // ---- Hover state (UI feedback only, no game-logic effect) ----
        private BaseRoomType? _hoveredRoom;
        private bool _isEndDayHovered;

        // ---- Resource "pop" animation state ----
        // Tracks the last-seen value of each resource so a change (from an upgrade)
        // can trigger a brief pop-then-settle animation on that resource's count.
        private int _lastFood, _lastPlanks, _lastScraps;
        private float _foodPopTimer, _planksPopTimer, _scrapsPopTimer;
        private const float ResourcePopDuration = 0.25f;

        // Kept only as a text-anchor rect for the floor labels/footer below. The house art
        // itself is left out for now - drop the finished sprite in behind everything else
        // once it's ready, in place of the procedural wall/roof/ground this used to draw.
        private static readonly RectangleF HouseWall = new RectangleF(140, 222, 1000, 418);

        private const float PanelWidth = 280f;
        private const float PanelHeight = 140f;
        private static readonly float[] PanelX = { 180f, 500f, 820f };
        private const float UpperRowY = 252f;
        private const float GroundRowY = 432f;

        public BaseBuilding(Game game, PlayerState playerState) : base(game)
        {
            _playerState = playerState;
        }

        public override void Initialize()
        {
            base.Initialize();

            // Seeds with the real current mouse state instead of a blank default, so a click
            // still held down from the previous screen doesn't read as a brand-new click here.
            // (This screen was the one place still missing this fix.)
            _previousMouse = Mouse.GetState();

            for (int i = 0; i < UpperFloorRooms.Length; i++)
            {
                var bounds = new RectangleF(PanelX[i], UpperRowY, PanelWidth, PanelHeight);
                _roomButtons[UpperFloorRooms[i]] = new Button(bounds, UpperFloorRooms[i].ToString());
            }

            for (int i = 0; i < GroundFloorRooms.Length; i++)
            {
                var bounds = new RectangleF(PanelX[i], GroundRowY, PanelWidth, PanelHeight);
                _roomButtons[GroundFloorRooms[i]] = new Button(bounds, GroundFloorRooms[i].ToString());
            }

            _endDayButton = new Button(new RectangleF(490, 655, 300, 55), "End the Day");

            // Snapshot starting resource values so the first frame never reads as a "change"
            // and fires a false pop animation.
            _lastFood = _playerState.Food;
            _lastPlanks = _playerState.Planks;
            _lastScraps = _playerState.Scraps;
        }

        public override void Update(GameTime gameTime)
        {
            if (_playerState.IsGameOver)
            {
                ScreenManager.ReplaceScreen(new GameOverScreen(Game));
                return;
            }

            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            _foodPopTimer = MathF.Max(0f, _foodPopTimer - dt);
            _planksPopTimer = MathF.Max(0f, _planksPopTimer - dt);
            _scrapsPopTimer = MathF.Max(0f, _scrapsPopTimer - dt);

            var mouse = Mouse.GetState();

            // Hover state is recomputed every frame (not just on click) so panels can
            // react as soon as the mouse enters them, not only when clicked.
            _hoveredRoom = null;
            foreach (var room in AllRooms)
            {
                if (_roomButtons[room].Contains(mouse.X, mouse.Y))
                {
                    _hoveredRoom = room;
                    break;
                }
            }
            _isEndDayHovered = _endDayButton.Contains(mouse.X, mouse.Y);

            // Every button eases its own hover/press animation forward each frame, so the
            // panels fade smoothly between states instead of snapping the instant the mouse
            // crosses their bounds.
            foreach (var room in AllRooms)
            {
                _roomButtons[room].UpdateAnimation(dt, room == _hoveredRoom);
            }
            _endDayButton.UpdateAnimation(dt, _isEndDayHovered);

            if (InputChecker.IsNewLeftClick(mouse, _previousMouse))
            {
                foreach (var room in AllRooms)
                {
                    if (_roomButtons[room].Contains(mouse.X, mouse.Y))
                    {
                        _roomButtons[room].TriggerPress();
                        TryUpgrade(room);
                    }
                }

                if (_endDayButton.Contains(mouse.X, mouse.Y))
                {
                    _endDayButton.TriggerPress();
                    ScreenManager.ShowScreen(new PreparationScreen(Game, _playerState), ScreenTransitions.FadeTransition(GraphicsDevice));
                }
            }

            // Any resource change this frame (from an upgrade spend) triggers that
            // resource's pop animation.
            if (_playerState.Food != _lastFood)
            {
                _foodPopTimer = ResourcePopDuration;
                _lastFood = _playerState.Food;
            }
            if (_playerState.Planks != _lastPlanks)
            {
                _planksPopTimer = ResourcePopDuration;
                _lastPlanks = _playerState.Planks;
            }
            if (_playerState.Scraps != _lastScraps)
            {
                _scrapsPopTimer = ResourcePopDuration;
                _lastScraps = _playerState.Scraps;
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
                BaseRoomType.Kitchen => (6 * tier, 0, 0),
                BaseRoomType.Barrack => (3 * tier, 3 * tier, 0),
                BaseRoomType.Archive => (0, 0, 6 * tier),
                _ => (0, 0, 0)
            };
        }

        // ---------- Draw ----------

        public override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(new Color(32, 28, 34)); // Day stays safe, but the light is bleak rather than cheerful - the lore's holy light is a threat, not a comfort

            var spriteBatch = Game1.SpriteBatch;
            var font = Game1.Font;
            spriteBatch.Begin();

            DrawBackground(spriteBatch);
            DrawRooms(spriteBatch, font);
            DrawFooter(spriteBatch, font);
            DrawEndDayButton(spriteBatch, font);
            DrawHopeBar(spriteBatch, font, gameTime);
            DrawResourceIcons(spriteBatch, font);

            spriteBatch.End();
        }

        // House art is intentionally left out for now - the real sprite goes here later.
        // Just a bleak, muted gradient stands in for it: dark enough to match the lore (this
        // "safe" indoor scene sits under a holy light that's lethal the moment you step
        // outside) while staying lighter than Night, so the two phases stay visually distinct.
        private void DrawBackground(SpriteBatch spriteBatch)
        {
            UITheme.FillGradientRect(spriteBatch, new RectangleF(0, 0, 1280, 720), new Color(52, 46, 56), new Color(28, 24, 30), 10);
        }

        private void DrawRooms(SpriteBatch spriteBatch, SpriteFont font)
        {
            UITheme.DrawTextWithShadow(spriteBatch, font, "Upper Floor", new Vector2(HouseWall.X + 40, HouseWall.Y + 8), new Color(200, 190, 195));
            UITheme.DrawTextWithShadow(spriteBatch, font, "Ground Floor", new Vector2(HouseWall.X + 40, UpperRowY + PanelHeight + 8), new Color(200, 190, 195));

            foreach (var room in AllRooms)
            {
                DrawRoomPanel(spriteBatch, font, room);
            }
        }

        private void DrawRoomPanel(SpriteBatch spriteBatch, SpriteFont font, BaseRoomType room)
        {
            var button = _roomButtons[room];
            var bounds = button.Bounds;
            int level = _playerState.RoomLevels[room];
            bool maxed = level >= MaxRoomLevel;
            float hover = button.HoverAmount;

            // Window-style panel: a colored "pane" behind a dark frame, so it reads as part
            // of the house instead of a floating UI square. Hover now eases the tint and
            // border color in/out instead of snapping between two fixed states.
            Color topColor = maxed ? new Color(64, 84, 72) : new Color(70, 64, 68);
            Color bottomColor = maxed ? new Color(40, 56, 48) : new Color(40, 36, 40);
            Color borderColor = maxed ? new Color(40, 70, 50) : new Color(24, 20, 22);

            if (!maxed)
            {
                // Ember-glow border on hover instead of a gold trim - ties this back to the
                // corruption-glow visual language used for room "tells" at night.
                borderColor = Color.Lerp(borderColor, new Color(230, 110, 55), hover);
                topColor = UITheme.Brighten(topColor, hover * 0.15f);
                bottomColor = UITheme.Brighten(bottomColor, hover * 0.15f);
            }

            float borderThickness = MathHelper.Lerp(3f, 4f, hover);
            UITheme.DrawPanel(spriteBatch, bounds, topColor, bottomColor, borderColor, borderThickness, 14f, shadowStrength: 0.6f);

            // Plus-shaped window mullion, same idea as before, inset slightly from the
            // now-rounded frame so it doesn't poke past the corners.
            var midX = bounds.X + bounds.Width / 2f;
            var midY = bounds.Y + bounds.Height / 2f;
            spriteBatch.DrawLine(new Vector2(midX, bounds.Y + 10), new Vector2(midX, bounds.Y + bounds.Height - 10), Color.White * 0.3f, 2f);
            spriteBatch.DrawLine(new Vector2(bounds.X + 10, midY), new Vector2(bounds.X + bounds.Width - 10, midY), Color.White * 0.3f, 2f);

            UITheme.DrawTextWithShadow(spriteBatch, font, room.ToString(), new Vector2(bounds.X + 12, bounds.Y + 10), Color.White);

            if (maxed)
            {
                UITheme.DrawTextWithShadow(spriteBatch, font, "MAX", new Vector2(bounds.X + 12, bounds.Y + bounds.Height - 34), new Color(160, 225, 175));
            }
            else
            {
                var (food, planks, scraps) = GetUpgradeCost(room, level);
                bool canAfford = _playerState.Food >= food && _playerState.Planks >= planks && _playerState.Scraps >= scraps;
                // Green when the player can afford the upgrade right now, red when they can't -
                // turns a mental subtraction into an instant glance.
                Color costColor = canAfford ? new Color(120, 220, 130) : new Color(230, 100, 90);

                UITheme.DrawTextWithShadow(spriteBatch, font, $"Lv {level}/{MaxRoomLevel}", new Vector2(bounds.X + 12, bounds.Y + bounds.Height - 58), new Color(220, 215, 210));
                UITheme.DrawTextWithShadow(spriteBatch, font, $"{food}F {planks}P {scraps}S", new Vector2(bounds.X + 12, bounds.Y + bounds.Height - 30), costColor);
            }
        }

        private void DrawFooter(SpriteBatch spriteBatch, SpriteFont font)
        {
            if (string.IsNullOrEmpty(_statusLog)) return;

            var position = new Vector2(HouseWall.X + 40, GroundRowY + PanelHeight + 40);
            var textSize = font.MeasureString(_statusLog);
            var chip = new RectangleF(position.X - 14, position.Y - 8, textSize.X + 28, textSize.Y + 16);

            UITheme.DrawPanel(spriteBatch, chip, new Color(70, 32, 32), new Color(46, 20, 20), new Color(130, 45, 45), 2f, 10f, shadowStrength: 0.5f);
            UITheme.DrawTextWithShadow(spriteBatch, font, _statusLog, position, new Color(255, 210, 210));
        }

        private void DrawEndDayButton(SpriteBatch spriteBatch, SpriteFont font)
        {
            var button = _endDayButton;
            float hover = button.HoverAmount;

            Color top = Color.Lerp(new Color(58, 46, 42), new Color(78, 60, 50), hover);
            Color bottom = Color.Lerp(new Color(36, 28, 26), new Color(50, 38, 32), hover);
            Color border = Color.Lerp(new Color(200, 90, 45), new Color(255, 140, 70), hover);
            float borderThickness = MathHelper.Lerp(3f, 4f, hover);

            // A brief inward "squash" while the press pulse decays, so a click reads as a
            // physical push rather than an instant color swap.
            float squash = button.PressAmount * 4f;
            var bounds = button.Bounds;
            var drawBounds = new RectangleF(bounds.X + squash, bounds.Y + squash / 2f, bounds.Width - squash * 2f, bounds.Height - squash);

            UITheme.DrawPanel(spriteBatch, drawBounds, top, bottom, border, borderThickness, 12f, shadowStrength: 0.8f);

            var textSize = font.MeasureString(button.Label);
            var textPos = new Vector2(
                drawBounds.X + (drawBounds.Width - textSize.X) / 2f,
                drawBounds.Y + (drawBounds.Height - textSize.Y) / 2f);
            UITheme.DrawTextWithShadow(spriteBatch, font, button.Label, textPos, Color.White);
        }

        private void DrawHopeBar(SpriteBatch spriteBatch, SpriteFont font, GameTime gameTime)
        {
            UITheme.DrawTextWithShadow(spriteBatch, font, "Hope", new Vector2(60, 16), Color.White);

            var barMax = new RectangleF(60, 46, 420, 26);
            float ratio = _playerState.Hope / (float)PlayerState.MaxHope;
            var barFill = new RectangleF(60, 46, 420 * ratio, 26);

            UITheme.DrawSoftShadow(spriteBatch, barMax, 13f, 0.5f);
            UITheme.FillRoundedRectGradient(spriteBatch, barMax, Color.Black * 0.55f, Color.Black * 0.35f, 13f, 8);

            if (barFill.Width > 1f)
            {
                Color fillTop = new Color(225, 90, 165);
                Color fillBottom = new Color(175, 40, 115);
                if (ratio < 0.25f)
                {
                    // Slow warning pulse once Hope runs low - purely cosmetic, doesn't touch
                    // the actual game-over threshold or value.
                    float pulse = UITheme.PulseSine((float)gameTime.TotalGameTime.TotalSeconds, 4f);
                    fillTop = Color.Lerp(fillTop, Color.White, pulse * 0.25f);
                }
                UITheme.FillRoundedRectGradient(spriteBatch, barFill, fillTop, fillBottom, 13f, 8);
            }

            UITheme.DrawRoundedRectBorder(spriteBatch, barMax, Color.Black * 0.7f, 2f, 13f);

            var label = $"{_playerState.Hope}/{PlayerState.MaxHope}";
            var labelSize = font.MeasureString(label);
            var labelPos = new Vector2(barMax.X + (barMax.Width - labelSize.X) / 2f, barMax.Y + (barMax.Height - labelSize.Y) / 2f);
            UITheme.DrawTextWithShadow(spriteBatch, font, label, labelPos, Color.White);
        }

        private void DrawResourceIcons(SpriteBatch spriteBatch, SpriteFont font)
        {
            DrawResourceSlot(spriteBatch, font, 900, "Food", _playerState.Food, DrawFoodIcon, _foodPopTimer, new Color(150, 60, 55));
            DrawResourceSlot(spriteBatch, font, 1010, "Planks", _playerState.Planks, DrawPlanksIcon, _planksPopTimer, new Color(120, 85, 50));
            DrawResourceSlot(spriteBatch, font, 1120, "Scraps", _playerState.Scraps, DrawScrapsIcon, _scrapsPopTimer, new Color(90, 90, 100));
        }

        private void DrawResourceSlot(SpriteBatch spriteBatch, SpriteFont font, float centerX, string label, int value, Action<SpriteBatch, float> drawIcon, float popTimer, Color slotTint)
        {
            var slot = new RectangleF(centerX - 48, 8, 96, 96);
            UITheme.DrawPanel(spriteBatch, slot, UITheme.Darken(slotTint, 0.05f), UITheme.Darken(slotTint, 0.4f), Color.Black * 0.4f, 2f, 16f, shadowStrength: 0.5f);

            drawIcon(spriteBatch, centerX);

            // Pop-then-settle: scale jumps up the instant the resource changes, then eases
            // back down to normal over ResourcePopDuration. At rest (popTimer == 0) this is
            // a no-op and renders exactly as before.
            float elapsedRatio = 1f - (popTimer / ResourcePopDuration);
            float decay = 1f - UITheme.EaseOutCubic(elapsedRatio);
            float popScale = 1f + decay * 0.4f;

            var countText = value.ToString();
            float scale = 1.3f * popScale;
            var countSize = font.MeasureString(countText) * scale;
            UITheme.DrawTextWithShadow(spriteBatch, font, countText, new Vector2(centerX - countSize.X / 2f, 60), Color.White, scale);

            var labelSize = font.MeasureString(label);
            UITheme.DrawTextWithShadow(spriteBatch, font, label, new Vector2(centerX - labelSize.X / 2f, 92), new Color(225, 225, 225));
        }

        private void DrawFoodIcon(SpriteBatch spriteBatch, float centerX)
        {
            FillCircleApprox(spriteBatch, new Vector2(centerX, 36), 16f, new Color(190, 60, 50));
        }

        private void DrawPlanksIcon(SpriteBatch spriteBatch, float centerX)
        {
            for (int i = 0; i < 3; i++)
            {
                var plankRect = new RectangleF(centerX - 22, 22 + i * 10, 44, 7);
                spriteBatch.FillRectangle(plankRect, new Color(160, 110, 60));
            }
        }

        private void DrawScrapsIcon(SpriteBatch spriteBatch, float centerX)
        {
            spriteBatch.FillRectangle(new RectangleF(centerX - 16, 20, 32, 32), new Color(150, 150, 160));
            spriteBatch.FillRectangle(new RectangleF(centerX - 6, 30, 18, 14), new Color(95, 95, 105));
        }

        private static void FillCircleApprox(SpriteBatch spriteBatch, Vector2 center, float radius, Color color, int slices = 20)
        {
            for (int i = 0; i < slices; i++)
            {
                float t0 = i / (float)slices;
                float t1 = (i + 1) / (float)slices;
                float y0 = MathHelper.Lerp(-radius, radius, t0);
                float y1 = MathHelper.Lerp(-radius, radius, t1);
                float yMid = (y0 + y1) / 2f;
                float halfWidth = (float)Math.Sqrt(Math.Max(0f, radius * radius - yMid * yMid));

                spriteBatch.FillRectangle(new RectangleF(center.X - halfWidth, center.Y + y0, halfWidth * 2f, y1 - y0 + 1f), color);
            }
        }
    }
}
