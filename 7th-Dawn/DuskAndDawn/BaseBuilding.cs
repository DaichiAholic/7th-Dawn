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
    /// <summary>
    /// Something a room can make: a weapon, an item, or (for the Feast) an action.
    /// RequiredLevel is the room level that unlocks it. BlockedReason lets a recipe refuse
    /// for reasons other than cost (e.g. Feast already held today) - null means allowed.
    /// </summary>
    public class Recipe
    {
        public string Name { get; }
        public string Detail { get; }
        public BaseRoomType Room { get; }
        public int RequiredLevel { get; }
        public int Food { get; }
        public int Planks { get; }
        public int Scraps { get; }

        // Set for Workshop recipes so the panel can show the weapon's icon. null for items.
        public Weapon SampleWeapon { get; }

        private readonly Func<PlayerState, string> _craft;
        private readonly Func<PlayerState, string> _blockedReason;

        public Recipe(string name, string detail, BaseRoomType room, int requiredLevel,
            int food, int planks, int scraps,
            Func<PlayerState, string> craft,
            Func<PlayerState, string> blockedReason = null,
            Weapon sampleWeapon = null)
        {
            SampleWeapon = sampleWeapon;
            Name = name;
            Detail = detail;
            Room = room;
            RequiredLevel = requiredLevel;
            Food = food;
            Planks = planks;
            Scraps = scraps;
            _craft = craft;
            _blockedReason = blockedReason;
        }

        public string CostLabel => BaseBuilding.FormatCost(Food, Planks, Scraps);

        public string BlockedReason(PlayerState state) => _blockedReason?.Invoke(state);

        public string Craft(PlayerState state) => _craft(state);

        // ---- Factories so the recipe table below stays one line per entry ----

        public static Recipe ForWeapon(BaseRoomType room, int level, int food, int planks, int scraps, Func<Weapon> make)
        {
            var sample = make();
            string detail = sample.IsHoly
                ? $"{sample.DiceLabel}, +{sample.CorruptionBonus} per corruption"
                : sample.DiceLabel;

            return new Recipe(sample.Name, detail, room, level, food, planks, scraps, state =>
            {
                state.Inventory.Add(make());
                return $"Crafted a {sample.Name}.";
            }, sampleWeapon: sample);
        }

        public static Recipe ForItem(BaseRoomType room, int level, int food, int planks, int scraps, Func<Item> make)
        {
            var sample = make();
            return new Recipe(sample.Name, sample.Description, room, level, food, planks, scraps, state =>
            {
                state.Items.Add(make());
                int owned = state.Items.Count(i => i.Name == sample.Name);
                return $"Made a {sample.Name} (you have {owned}).";
            });
        }
    }

    public class BaseBuilding : GameScreen
    {
        private Game1 Game1 => (Game1)Game;
        private readonly PlayerState _playerState;

        private const int MaxRoomLevel = 3;
        private const int FeastFoodCost = 10;
        private const int FeastHope = 15;

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

        // Everything the base can make, in display order.
        private static readonly List<Recipe> Recipes = new List<Recipe>
        {
            // Workshop - weapons
            Recipe.ForWeapon(BaseRoomType.Workshop, 1, 0, 3, 0, Weapon.WoodenClub),
            Recipe.ForWeapon(BaseRoomType.Workshop, 1, 0, 2, 3, Weapon.ScrapClub),
            Recipe.ForWeapon(BaseRoomType.Workshop, 2, 0, 3, 5, Weapon.IronSword),
            Recipe.ForWeapon(BaseRoomType.Workshop, 2, 0, 2, 6, Weapon.Cleaver),
            Recipe.ForWeapon(BaseRoomType.Workshop, 2, 0, 3, 4, Weapon.HandAxe),
            Recipe.ForWeapon(BaseRoomType.Workshop, 3, 0, 4, 10, Weapon.HolyLance),

            // Infirmary - remedies
            Recipe.ForItem(BaseRoomType.Infirmary, 1, 2, 0, 1, Item.Bandage),
            Recipe.ForItem(BaseRoomType.Infirmary, 2, 3, 0, 3, Item.Tonic),
            Recipe.ForItem(BaseRoomType.Infirmary, 2, 0, 1, 4, Item.SmokeFlask),
            Recipe.ForItem(BaseRoomType.Infirmary, 3, 5, 0, 6, Item.Elixir),
            Recipe.ForItem(BaseRoomType.Infirmary, 3, 4, 0, 5, Item.DawnTincture),

            // Kitchen
            Recipe.ForItem(BaseRoomType.Kitchen, 2, 4, 0, 0, Item.Rations),
            new Recipe("Feast", $"+{FeastHope} Hope, once per day", BaseRoomType.Kitchen, 3, FeastFoodCost, 0, 0,
                state =>
                {
                    state.FeastUsedToday = true;
                    state.ChangeHope(FeastHope);
                    return $"The house shares a feast. Hope +{FeastHope}.";
                },
                state =>
                {
                    if (state.FeastUsedToday) return "You've already feasted today.";
                    if (state.Hope >= PlayerState.MaxHope) return "Hope is already full.";
                    return null;
                })
        };

        private readonly Dictionary<BaseRoomType, Button> _roomButtons = new Dictionary<BaseRoomType, Button>();
        private Button _endDayButton;
        private string _statusLog = "";
        private bool _statusIsError;
        private MouseState _previousMouse;

        // ---- Hover state (UI feedback only, no game-logic effect) ----
        private BaseRoomType? _hoveredRoom;
        private bool _isEndDayHovered;

        // ---- Room detail panel (opens when a room is clicked) ----
        private BaseRoomType? _openRoom;
        private Button _upgradeButton;
        private Button _closeButton;
        private readonly List<(Button button, Recipe recipe)> _recipeButtons = new List<(Button, Recipe)>();
        private static readonly RectangleF DetailPanel = new RectangleF(240, 110, 800, 540);

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

            _upgradeButton = new Button(new RectangleF(DetailPanel.X + 30, DetailPanel.Y + 164, 360, 54), "Upgrade");
            _closeButton = new Button(new RectangleF(DetailPanel.X + DetailPanel.Width - 160, DetailPanel.Y + DetailPanel.Height - 64, 130, 46), "Close");

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
            bool clicked = InputChecker.IsNewLeftClick(mouse, _previousMouse);
            bool panelOpen = _openRoom.HasValue;

            // Hover state is recomputed every frame (not just on click) so panels can
            // react as soon as the mouse enters them, not only when clicked. The house
            // behind the detail panel stops reacting while the panel is open.
            _hoveredRoom = null;
            if (!panelOpen)
            {
                foreach (var room in AllRooms)
                {
                    if (_roomButtons[room].Contains(mouse.X, mouse.Y))
                    {
                        _hoveredRoom = room;
                        break;
                    }
                }
            }
            _isEndDayHovered = !panelOpen && _endDayButton.Contains(mouse.X, mouse.Y);

            // Every button eases its own hover/press animation forward each frame, so the
            // panels fade smoothly between states instead of snapping the instant the mouse
            // crosses their bounds.
            foreach (var room in AllRooms)
            {
                _roomButtons[room].UpdateAnimation(dt, room == _hoveredRoom);
            }
            _endDayButton.UpdateAnimation(dt, _isEndDayHovered);

            _upgradeButton.UpdateAnimation(dt, panelOpen && _upgradeButton.Contains(mouse.X, mouse.Y));
            _closeButton.UpdateAnimation(dt, panelOpen && _closeButton.Contains(mouse.X, mouse.Y));
            foreach (var (button, _) in _recipeButtons)
            {
                button.UpdateAnimation(dt, panelOpen && button.Contains(mouse.X, mouse.Y));
            }

            if (clicked)
            {
                if (panelOpen)
                {
                    HandleDetailPanelClick(mouse.X, mouse.Y);
                }
                else
                {
                    foreach (var room in AllRooms)
                    {
                        if (_roomButtons[room].Contains(mouse.X, mouse.Y))
                        {
                            _roomButtons[room].TriggerPress();
                            OpenRoom(room);
                            break;
                        }
                    }

                    if (_endDayButton.Contains(mouse.X, mouse.Y))
                    {
                        _endDayButton.TriggerPress();
                        ScreenManager.ShowScreen(new PreparationScreen(Game, _playerState), ScreenTransitions.FadeTransition(GraphicsDevice));
                    }
                }
            }

            // Any resource change this frame (from an upgrade or craft) triggers that
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

        // ---------- Room detail panel ----------

        private void OpenRoom(BaseRoomType room)
        {
            _openRoom = room;
            _statusLog = "";

            // Two columns of recipe buttons under the upgrade button - every recipe for the
            // room is listed, locked ones included, so players can see what's coming.
            _recipeButtons.Clear();
            var roomRecipes = Recipes.Where(r => r.Room == room).ToList();
            const float width = 360f, height = 60f, rowGap = 8f;
            for (int i = 0; i < roomRecipes.Count; i++)
            {
                float x = DetailPanel.X + 30 + (i % 2) * 380f;
                float y = DetailPanel.Y + 272 + (i / 2) * (height + rowGap);
                _recipeButtons.Add((new Button(new RectangleF(x, y, width, height), roomRecipes[i].Name), roomRecipes[i]));
            }
        }

        private void CloseRoom()
        {
            _openRoom = null;
            _recipeButtons.Clear();
            _statusLog = "";
        }

        private void HandleDetailPanelClick(int x, int y)
        {
            var room = _openRoom.Value;

            if (_closeButton.Contains(x, y) || !InputChecker.Contains(DetailPanel, x, y))
            {
                _closeButton.TriggerPress();
                CloseRoom();
                return;
            }

            if (_upgradeButton.Contains(x, y))
            {
                _upgradeButton.TriggerPress();
                TryUpgrade(room);
                return;
            }

            foreach (var (button, recipe) in _recipeButtons)
            {
                if (!button.Contains(x, y)) continue;

                button.TriggerPress();
                TryCraft(recipe);
                return;
            }
        }

        private void TryUpgrade(BaseRoomType room)
        {
            int level = _playerState.RoomLevels[room];
            if (level >= MaxRoomLevel)
            {
                SetStatus($"{room} is already at max level.", isError: true);
                return;
            }

            var (food, planks, scraps) = GetUpgradeCost(room, level);

            if (_playerState.TrySpend(food, planks, scraps))
            {
                _playerState.RoomLevels[room] = level + 1;
                SetStatus($"{room} upgraded to Lv {level + 1}. {UpgradeNote(room, level + 1)}", isError: false);
            }
            else
            {
                SetStatus($"Need {FormatCost(food, planks, scraps)} to upgrade.", isError: true);
            }
        }

        private void TryCraft(Recipe recipe)
        {
            int level = _playerState.RoomLevels[recipe.Room];
            if (level < recipe.RequiredLevel)
            {
                SetStatus($"{recipe.Name} needs {recipe.Room} Lv {recipe.RequiredLevel}.", isError: true);
                return;
            }

            string blocked = recipe.BlockedReason(_playerState);
            if (blocked != null)
            {
                SetStatus(blocked, isError: true);
                return;
            }

            if (!_playerState.TrySpend(recipe.Food, recipe.Planks, recipe.Scraps))
            {
                SetStatus($"Need {recipe.CostLabel} for {recipe.Name}.", isError: true);
                return;
            }

            SetStatus(recipe.Craft(_playerState), isError: false);
        }

        private void SetStatus(string message, bool isError)
        {
            _statusLog = message;
            _statusIsError = isError;
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

        public static string FormatCost(int food, int planks, int scraps)
        {
            var parts = new List<string>();
            if (food > 0) parts.Add($"{food}F");
            if (planks > 0) parts.Add($"{planks}P");
            if (scraps > 0) parts.Add($"{scraps}S");
            return parts.Count > 0 ? string.Join(" ", parts) : "Free";
        }

        // ---------- Room text ----------

        private static string RoomRole(BaseRoomType room) => room switch
        {
            BaseRoomType.Storage => "Caps how much you can keep. Overflow is lost at dawn.",
            BaseRoomType.Workshop => "Crafts weapons. Higher levels unlock stronger ones.",
            BaseRoomType.Infirmary => "Brews remedies you carry into the night.",
            BaseRoomType.Kitchen => "Cooks free Food every morning.",
            BaseRoomType.Barrack => "Trains your dice: rerolls, better faces, more dice.",
            BaseRoomType.Archive => "Maps the ruins. Opens new districts to scavenge.",
            _ => ""
        };

        private static string LevelDescription(BaseRoomType room, int level) => (room, level) switch
        {
            (BaseRoomType.Storage, 1) => "Holds 30 of each resource",
            (BaseRoomType.Storage, 2) => "Holds 60 of each resource",
            (BaseRoomType.Storage, _) => "Holds 100 of each resource",

            (BaseRoomType.Workshop, 1) => "Basic weapons: Wooden Club, Scrap Club",
            (BaseRoomType.Workshop, 2) => "Iron weapons: Iron Sword, Cleaver, Hand Axe",
            (BaseRoomType.Workshop, _) => "Holy steel: Holy Lance",

            (BaseRoomType.Infirmary, 1) => "Bandages",
            (BaseRoomType.Infirmary, 2) => "Tonics and Smoke Flasks",
            (BaseRoomType.Infirmary, _) => "Elixirs and Dawn Tinctures",

            (BaseRoomType.Kitchen, 1) => "+2 Food each morning",
            (BaseRoomType.Kitchen, 2) => "+4 Food each morning, Rations",
            (BaseRoomType.Kitchen, _) => "+6 Food each morning, Feast",

            (BaseRoomType.Barrack, 1) => "1 auto-reroll per fight",
            (BaseRoomType.Barrack, 2) => "2 rerolls per fight, dice never roll a 1",
            (BaseRoomType.Barrack, _) => "2 rerolls, no 1s, +1 extra die on attacks",

            (BaseRoomType.Archive, 1) => "Village Outskirts",
            (BaseRoomType.Archive, 2) => "+ Church Ruins, scout 1 room ahead",
            (BaseRoomType.Archive, _) => "+ Castle Keep, scout 2 rooms ahead",

            _ => ""
        };

        // Short line shown on each room tile so the house reads at a glance.
        private string TileSummary(BaseRoomType room) => room switch
        {
            BaseRoomType.Storage => $"Holds {_playerState.StorageCap} each",
            BaseRoomType.Workshop => "Crafts weapons",
            BaseRoomType.Infirmary => "Brews remedies",
            BaseRoomType.Kitchen => $"+{_playerState.KitchenDailyFood} Food per morning",
            BaseRoomType.Barrack => $"{_playerState.BarracksRerolls} reroll(s) per fight",
            BaseRoomType.Archive => $"{DistrictInfo.All.Count(_playerState.IsDistrictUnlocked)} district(s) open",
            _ => ""
        };

        private static string UpgradeNote(BaseRoomType room, int newLevel) => room switch
        {
            BaseRoomType.Archive when newLevel == 2 => "Church Ruins is now open.",
            BaseRoomType.Archive when newLevel == 3 => "Castle Keep is now open.",
            BaseRoomType.Workshop or BaseRoomType.Infirmary => "New recipes unlocked.",
            BaseRoomType.Kitchen when newLevel == 2 => "Rations unlocked.",
            BaseRoomType.Kitchen when newLevel == 3 => "Feast unlocked.",
            _ => ""
        };

        // ---------- Draw ----------

        public override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(new Color(32, 28, 34)); // Day stays safe, but the light is bleak rather than cheerful - the lore's holy light is a threat, not a comfort

            var spriteBatch = Game1.SpriteBatch;
            var font = Game1.Font;
            spriteBatch.Begin();

            DrawBackground(spriteBatch);
            DrawRooms(spriteBatch, font);
            if (!_openRoom.HasValue) DrawFooter(spriteBatch, font);
            DrawEndDayButton(spriteBatch, font);
            DrawHopeBar(spriteBatch, font, gameTime);
            DrawResourceIcons(spriteBatch, font);

            if (_openRoom.HasValue)
            {
                DrawDetailPanel(spriteBatch, font, _openRoom.Value);
            }

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
            // of the house instead of a floating UI square. Hover eases the tint and border
            // color in/out instead of snapping between two fixed states.
            Color topColor = maxed ? new Color(64, 84, 72) : new Color(70, 64, 68);
            Color bottomColor = maxed ? new Color(40, 56, 48) : new Color(40, 36, 40);
            Color borderColor = maxed ? new Color(40, 70, 50) : new Color(24, 20, 22);

            // Ember-glow border on hover - ties this back to the corruption-glow visual
            // language used for room "tells" at night. Maxed rooms glow too now, since
            // they're still clickable for crafting.
            borderColor = Color.Lerp(borderColor, new Color(230, 110, 55), hover);
            topColor = UITheme.Brighten(topColor, hover * 0.15f);
            bottomColor = UITheme.Brighten(bottomColor, hover * 0.15f);

            float borderThickness = MathHelper.Lerp(3f, 4f, hover);
            UITheme.DrawPanel(spriteBatch, bounds, topColor, bottomColor, borderColor, borderThickness, 14f, shadowStrength: 0.6f);

            // Plus-shaped window mullion, inset slightly from the rounded frame so it
            // doesn't poke past the corners.
            var midX = bounds.X + bounds.Width / 2f;
            var midY = bounds.Y + bounds.Height / 2f;
            spriteBatch.DrawLine(new Vector2(midX, bounds.Y + 10), new Vector2(midX, bounds.Y + bounds.Height - 10), Color.White * 0.3f, 2f);
            spriteBatch.DrawLine(new Vector2(bounds.X + 10, midY), new Vector2(bounds.X + bounds.Width - 10, midY), Color.White * 0.3f, 2f);

            UITheme.DrawTextWithShadow(spriteBatch, font, room.ToString(), new Vector2(bounds.X + 12, bounds.Y + 10), Color.White);
            UITheme.DrawTextWithShadow(spriteBatch, font, TileSummary(room), new Vector2(bounds.X + 12, bounds.Y + 38), new Color(235, 200, 160), 0.8f);

            if (maxed)
            {
                UITheme.DrawTextWithShadow(spriteBatch, font, "MAX", new Vector2(bounds.X + 12, bounds.Y + bounds.Height - 34), new Color(160, 225, 175));
            }
            else
            {
                var (food, planks, scraps) = GetUpgradeCost(room, level);
                bool canAfford = _playerState.CanAfford(food, planks, scraps);
                // Green when the player can afford the upgrade right now, red when they can't -
                // turns a mental subtraction into an instant glance.
                Color costColor = canAfford ? new Color(120, 220, 130) : new Color(230, 100, 90);

                UITheme.DrawTextWithShadow(spriteBatch, font, $"Lv {level}/{MaxRoomLevel}", new Vector2(bounds.X + 12, bounds.Y + bounds.Height - 58), new Color(220, 215, 210));
                UITheme.DrawTextWithShadow(spriteBatch, font, FormatCost(food, planks, scraps), new Vector2(bounds.X + 12, bounds.Y + bounds.Height - 30), costColor);
            }
        }

        private void DrawDetailPanel(SpriteBatch spriteBatch, SpriteFont font, BaseRoomType room)
        {
            int level = _playerState.RoomLevels[room];
            bool maxed = level >= MaxRoomLevel;
            var panel = DetailPanel;

            // Dim the house behind so the panel reads as the focus.
            UITheme.FillGradientRect(spriteBatch, new RectangleF(0, 0, 1280, 720), Color.Black * 0.55f, Color.Black * 0.55f, 1);
            UITheme.DrawPanel(spriteBatch, panel, new Color(62, 54, 58), new Color(34, 30, 34), new Color(200, 100, 55), 3f, 18f, shadowStrength: 0.9f);

            UITheme.DrawTextWithShadow(spriteBatch, font, $"{room}   Lv {level}/{MaxRoomLevel}", new Vector2(panel.X + 30, panel.Y + 24), Color.White, 1.2f);
            UITheme.DrawTextWithShadow(spriteBatch, font, RoomRole(room), new Vector2(panel.X + 30, panel.Y + 62), new Color(200, 190, 195));

            UITheme.DrawTextWithShadow(spriteBatch, font, $"Now:  {LevelDescription(room, level)}", new Vector2(panel.X + 30, panel.Y + 100), new Color(235, 220, 200));
            string nextLine = maxed ? "Next: Fully upgraded" : $"Next: {LevelDescription(room, level + 1)}";
            UITheme.DrawTextWithShadow(spriteBatch, font, nextLine, new Vector2(panel.X + 30, panel.Y + 128), new Color(170, 165, 160));

            // Upgrade button
            if (maxed)
            {
                _upgradeButton.Label = "Max level";
                DrawDetailButton(spriteBatch, font, _upgradeButton, new Color(64, 84, 72), new Color(40, 56, 48), null, Color.White, enabled: false);
            }
            else
            {
                var (food, planks, scraps) = GetUpgradeCost(room, level);
                bool canAfford = _playerState.CanAfford(food, planks, scraps);
                _upgradeButton.Label = $"Upgrade to Lv {level + 1}";
                Color costColor = canAfford ? new Color(120, 220, 130) : new Color(230, 100, 90);
                DrawDetailButton(spriteBatch, font, _upgradeButton, new Color(110, 70, 45), new Color(78, 48, 30), FormatCost(food, planks, scraps), costColor, enabled: true);
            }

            // Recipes
            if (_recipeButtons.Count > 0)
            {
                UITheme.DrawTextWithShadow(spriteBatch, font, "Craft", new Vector2(panel.X + 30, panel.Y + 240), Color.White);

                foreach (var (button, recipe) in _recipeButtons)
                {
                    bool unlocked = level >= recipe.RequiredLevel;
                    bool blocked = unlocked && recipe.BlockedReason(_playerState) != null;
                    bool canAfford = _playerState.CanAfford(recipe.Food, recipe.Planks, recipe.Scraps);

                    string rightText;
                    Color rightColor;
                    if (!unlocked)
                    {
                        rightText = $"Needs Lv {recipe.RequiredLevel}";
                        rightColor = new Color(160, 150, 150);
                    }
                    else
                    {
                        rightText = recipe.CostLabel;
                        rightColor = canAfford && !blocked ? new Color(120, 220, 130) : new Color(230, 100, 90);
                    }

                    DrawRecipeButton(spriteBatch, font, button, recipe, rightText, rightColor, unlocked && !blocked);
                }
            }

            // Status line inside the panel
            if (!string.IsNullOrEmpty(_statusLog))
            {
                Color statusColor = _statusIsError ? new Color(255, 170, 160) : new Color(170, 235, 180);
                UITheme.DrawTextWithShadow(spriteBatch, font, _statusLog, new Vector2(panel.X + 30, panel.Y + panel.Height - 50), statusColor, 0.85f);
            }

            DrawDetailButton(spriteBatch, font, _closeButton, new Color(80, 70, 74), new Color(56, 48, 52), null, Color.White, enabled: true);
        }

        private void DrawDetailButton(SpriteBatch spriteBatch, SpriteFont font, Button button, Color baseTop, Color baseBottom, string rightText, Color rightColor, bool enabled)
        {
            float hover = enabled ? button.HoverAmount : 0f;
            Color top = UITheme.Brighten(baseTop, hover * 0.2f);
            Color bottom = UITheme.Brighten(baseBottom, hover * 0.2f);
            Color border = Color.Lerp(Color.White * 0.5f, new Color(255, 140, 70), hover);

            float squash = button.PressAmount * 3f;
            var bounds = button.Bounds;
            var drawBounds = new RectangleF(bounds.X + squash, bounds.Y + squash / 2f, bounds.Width - squash * 2f, bounds.Height - squash);

            UITheme.DrawPanel(spriteBatch, drawBounds, top, bottom, border, MathHelper.Lerp(2f, 3f, hover), 10f, shadowStrength: 0.5f);

            var labelSize = font.MeasureString(button.Label);
            float textY = drawBounds.Y + (drawBounds.Height - labelSize.Y) / 2f;

            if (rightText == null)
            {
                UITheme.DrawTextWithShadow(spriteBatch, font, button.Label, new Vector2(drawBounds.X + (drawBounds.Width - labelSize.X) / 2f, textY), Color.White);
            }
            else
            {
                UITheme.DrawTextWithShadow(spriteBatch, font, button.Label, new Vector2(drawBounds.X + 14, textY), Color.White);
                var rightSize = font.MeasureString(rightText);
                UITheme.DrawTextWithShadow(spriteBatch, font, rightText, new Vector2(drawBounds.X + drawBounds.Width - rightSize.X - 14, textY), rightColor);
            }
        }

        private void DrawRecipeButton(SpriteBatch spriteBatch, SpriteFont font, Button button, Recipe recipe, string rightText, Color rightColor, bool available)
        {
            float hover = available ? button.HoverAmount : 0f;
            Color baseTop = available ? new Color(74, 66, 70) : new Color(48, 44, 48);
            Color baseBottom = available ? new Color(50, 44, 48) : new Color(34, 30, 34);
            Color top = UITheme.Brighten(baseTop, hover * 0.2f);
            Color bottom = UITheme.Brighten(baseBottom, hover * 0.2f);
            Color border = Color.Lerp(Color.White * (available ? 0.45f : 0.2f), new Color(255, 140, 70), hover);

            float squash = button.PressAmount * 3f;
            var bounds = button.Bounds;
            var drawBounds = new RectangleF(bounds.X + squash, bounds.Y + squash / 2f, bounds.Width - squash * 2f, bounds.Height - squash);

            UITheme.DrawPanel(spriteBatch, drawBounds, top, bottom, border, MathHelper.Lerp(1.5f, 3f, hover), 10f, shadowStrength: 0.4f);

            Color nameColor = available ? Color.White : new Color(150, 145, 145);
            Color detailColor = available ? new Color(200, 195, 190) : new Color(120, 115, 115);

            // Weapon recipes get their icon at native 32px on the left; text shifts over.
            float textX = drawBounds.X + 12;
            if (recipe.SampleWeapon != null)
            {
                UITheme.DrawIconSlot(spriteBatch, Game1.GetWeaponIcon(recipe.SampleWeapon), new Vector2(drawBounds.X + 14, drawBounds.Y + (drawBounds.Height - 32) / 2f), 1);
                textX = drawBounds.X + 56;
            }

            UITheme.DrawTextWithShadow(spriteBatch, font, recipe.Name, new Vector2(textX, drawBounds.Y + 8), nameColor);
            UITheme.DrawTextWithShadow(spriteBatch, font, recipe.Detail, new Vector2(textX, drawBounds.Y + 34), detailColor, 0.75f);

            var rightSize = font.MeasureString(rightText) * 0.85f;
            UITheme.DrawTextWithShadow(spriteBatch, font, rightText, new Vector2(drawBounds.X + drawBounds.Width - rightSize.X - 12, drawBounds.Y + 9), rightColor, 0.85f);
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

            // Storage cap under the resource row, so the limit is always in view.
            string capText = $"Storage: max {_playerState.StorageCap} each";
            var capSize = font.MeasureString(capText) * 0.8f;
            UITheme.DrawTextWithShadow(spriteBatch, font, capText, new Vector2(1010 - capSize.X / 2f, 116), new Color(200, 190, 195), 0.8f);
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

            // Count turns amber once it's sitting at the Storage cap - a nudge to upgrade.
            Color countColor = value >= _playerState.StorageCap ? new Color(255, 190, 90) : Color.White;

            var countText = value.ToString();
            float scale = 1.3f * popScale;
            var countSize = font.MeasureString(countText) * scale;
            UITheme.DrawTextWithShadow(spriteBatch, font, countText, new Vector2(centerX - countSize.X / 2f, 60), countColor, scale);

            var labelSize = font.MeasureString(label);
            UITheme.DrawTextWithShadow(spriteBatch, font, label, new Vector2(centerX - labelSize.X / 2f, 92), new Color(225, 225, 225));
        }

        private void DrawFoodIcon(SpriteBatch spriteBatch, float centerX)
        {
            DrawResourceSprite(spriteBatch, Game1.BreadTexture, centerX);
        }

        private void DrawPlanksIcon(SpriteBatch spriteBatch, float centerX)
        {
            DrawResourceSprite(spriteBatch, Game1.PlanksTexture, centerX);
        }

        private void DrawScrapsIcon(SpriteBatch spriteBatch, float centerX)
        {
            DrawResourceSprite(spriteBatch, Game1.ScrapsTexture, centerX);
        }

        // Sprites are 320x320 source canvases - scaled down and drawn from their own center so
        // they sit centered in the icon area of each 96x96 resource slot regardless of how
        // much transparent padding the source image has around the actual art.
        private void DrawResourceSprite(SpriteBatch spriteBatch, Texture2D texture, float centerX)
        {
            if (texture == null) return;
            const float scale = 0.2f; // 320px source -> 64px on screen, fits the slot with margin
            var origin = new Vector2(texture.Width / 2f, texture.Height / 2f);
            spriteBatch.Draw(texture, new Vector2(centerX, 40f), null, Color.White, 0f, origin, scale, SpriteEffects.None, 0f);
        }
    }
}