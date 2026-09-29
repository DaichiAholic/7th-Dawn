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

        // Set for item recipes, so craft feedback can draw the right vial. null otherwise.
        public Item SampleItem { get; }

        // True for Reinforce Weapon, which upgrades the equipped weapon instead of making one.
        public bool IsReinforce { get; }

        private readonly Func<PlayerState, string> _craft;
        private readonly Func<PlayerState, string> _blockedReason;

        // Optional overrides for recipes whose cost/text depend on the player's state
        // (Reinforce Weapon works on whatever is equipped). null = use the fixed values.
        private readonly Func<PlayerState, (int food, int planks, int scraps)> _dynamicCost;
        private readonly Func<PlayerState, string> _dynamicDetail;

        public Recipe(string name, string detail, BaseRoomType room, int requiredLevel,
            int food, int planks, int scraps,
            Func<PlayerState, string> craft,
            Func<PlayerState, string> blockedReason = null,
            Weapon sampleWeapon = null,
            Item sampleItem = null,
            Func<PlayerState, (int food, int planks, int scraps)> dynamicCost = null,
            Func<PlayerState, string> dynamicDetail = null,
            bool isReinforce = false)
        {
            SampleWeapon = sampleWeapon;
            SampleItem = sampleItem;
            _dynamicCost = dynamicCost;
            _dynamicDetail = dynamicDetail;
            IsReinforce = isReinforce;
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

        public (int food, int planks, int scraps) GetCost(PlayerState state) =>
            _dynamicCost?.Invoke(state) ?? (Food, Planks, Scraps);

        public string CostLabel(PlayerState state)
        {
            var (food, planks, scraps) = GetCost(state);
            return BaseBuilding.FormatCost(food, planks, scraps);
        }

        public string GetDetail(PlayerState state) => _dynamicDetail?.Invoke(state) ?? Detail;

        /// <summary>The weapon whose icon this recipe shows: the one it makes, or for
        /// Reinforce the one it upgrades. null for item recipes.</summary>
        public Weapon IconWeapon(PlayerState state) => IsReinforce ? state.EquippedWeapon : SampleWeapon;

        public string BlockedReason(PlayerState state) => _blockedReason?.Invoke(state);

        public string Craft(PlayerState state) => _craft(state);

        // ---- Factories so the recipe table below stays one line per entry ----

        public static Recipe ForWeapon(BaseRoomType room, int level, int food, int planks, int scraps, Func<Weapon> make)
        {
            var sample = make();
            string detail = sample.TraitLabel.Length > 0 ? $"{sample.StatLabel}  {sample.TraitLabel}" : sample.StatLabel;
            return new Recipe(sample.Name, detail, room, level, food, planks, scraps, state =>
            {
                state.Inventory.Add(make());
                return $"Crafted a {sample.Name}. Equip it on the Prepare screen.";
            }, sampleWeapon: sample);
        }

        public static Recipe ForItem(BaseRoomType room, int level, int food, int planks, int scraps, Func<Item> make)
        {
            var sample = make();
            return new Recipe(sample.Name, sample.Description, room, level, food, planks, scraps, state =>
            {
                state.Items.Add(make());
                int owned = state.OwnedItemCount(sample.Name);
                return $"Made a {sample.Name} (you have {owned}). It's in the stash - pack it on the Prepare screen.";
            }, sampleItem: sample);
        }

        /// <summary>Workshop upgrade for the equipped weapon: +1 damage per roll per level,
        /// capped by the Workshop level. The price climbs steeply, so Scraps from fights
        /// always have somewhere to go once the weapon rack is full.</summary>
        public static Recipe ReinforceEquipped()
        {
            return new Recipe("Reinforce Weapon", "", BaseRoomType.Workshop, 1, 0, 0, 0,
                craft: state =>
                {
                    var weapon = state.EquippedWeapon;
                    weapon.Reinforce();
                    return $"Your {weapon.Name} is now +{weapon.Reinforcement} ({weapon.DiceLabel}).";
                },
                blockedReason: state =>
                {
                    var weapon = state.EquippedWeapon;
                    if (weapon.Reinforcement >= Weapon.MaxReinforcement) return $"Your {weapon.DisplayName} can't be reinforced further.";
                    if (weapon.Reinforcement >= state.MaxReinforcement) return $"Reinforcing past +{weapon.Reinforcement} needs Workshop Lv {weapon.Reinforcement + 1}.";
                    return null;
                },
                dynamicCost: state => PlayerState.ReinforceCost(state.EquippedWeapon.Reinforcement + 1),
                dynamicDetail: state =>
                {
                    var weapon = state.EquippedWeapon;
                    return weapon.Reinforcement >= Weapon.MaxReinforcement
                        ? $"{weapon.DisplayName} is fully reinforced"
                        : $"Equipped {weapon.DisplayName} -> +{weapon.Reinforcement + 1}";
                },
                isReinforce: true);
        }
    }

    public class BaseBuilding : GameScreen, IGameplayScreen
    {
        private Game1 Game1 => (Game1)Game;
        private readonly PlayerState _playerState;

        private const int MaxRoomLevel = PlayerState.MaxRoomLevel;
        private const int FeastFoodCost = 14;

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
            // Workshop - weapons. Good metal is rare, so the better the weapon, the more Scraps.
            // Each tier is a clear jump in damage and costs a few nights of the district it's
            // built for; Tier 3 is a full Lv 1 Storage of Scraps.
            Recipe.ForWeapon(BaseRoomType.Workshop, 1, 0, 5, 1, Weapon.WoodenClub),
            Recipe.ForWeapon(BaseRoomType.Workshop, 1, 0, 3, 6, Weapon.ScrapClub),
            Recipe.ForWeapon(BaseRoomType.Workshop, 2, 2, 5, 12, Weapon.IronSword),
            Recipe.ForWeapon(BaseRoomType.Workshop, 2, 2, 3, 14, Weapon.Cleaver),
            Recipe.ForWeapon(BaseRoomType.Workshop, 2, 2, 7, 10, Weapon.HandAxe),
            Recipe.ForWeapon(BaseRoomType.Workshop, 3, 4, 10, 30, Weapon.HolyLance),
            Recipe.ForWeapon(BaseRoomType.Workshop, 3, 4, 14, 26, Weapon.WarMaul),
            Recipe.ForWeapon(BaseRoomType.Workshop, 5, 6, 16, 40, Weapon.Dawnbreaker),
            Recipe.ReinforceEquipped(),

            // Infirmary - remedies, brewed from food stores and scavenged glass and cloth
            Recipe.ForItem(BaseRoomType.Infirmary, 1, 3, 0, 2, Item.Bandage),
            Recipe.ForItem(BaseRoomType.Infirmary, 2, 5, 0, 5, Item.Tonic),
            Recipe.ForItem(BaseRoomType.Infirmary, 2, 0, 2, 6, Item.SmokeFlask),
            Recipe.ForItem(BaseRoomType.Infirmary, 3, 8, 0, 10, Item.Elixir),
            Recipe.ForItem(BaseRoomType.Infirmary, 3, 6, 0, 8, Item.DawnTincture),
            Recipe.ForItem(BaseRoomType.Infirmary, 4, 3, 0, 9, Item.HolyWater),

            // Kitchen
            Recipe.ForItem(BaseRoomType.Kitchen, 2, 6, 0, 0, Item.Rations),
            new Recipe("Feast", "", BaseRoomType.Kitchen, 3, FeastFoodCost, 0, 0,
                state =>
                {
                    state.FeastUsedToday = true;
                    state.ChangeHope(state.FeastHope);
                    return $"The house shares a feast. Hope +{state.FeastHope}.";
                },
                state =>
                {
                    if (state.FeastUsedToday) return "You've already feasted today.";
                    if (state.Hope >= PlayerState.MaxHope) return "Hope is already full.";
                    return null;
                },
                dynamicDetail: state => $"+{state.FeastHope} Hope, once per day")
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
        // Tall enough for the room's five-level ladder above the upgrade button, and five
        // rows of recipes (the Workshop's nine) under it.
        private static readonly RectangleF DetailPanel = new RectangleF(240, 110, 800, 600);
        private const float LadderTop = 94f;
        private const float UpgradeTop = 200f;
        private const float RecipeTop = 286f;
        private const float RecipeHeight = 50f;

        // ---- Craft feedback ----
        // A successful craft gets a "Crafted!" card up top with the thing's icon, a burst of
        // sparks and a flash on the recipe button, a "+1" rising off it, and the spent
        // resources floating off their counters - so it can't be missed.
        private const float CraftToastDuration = 2.4f;
        private const float CraftFlashDuration = 0.8f;
        private float _craftToastTimer;
        private Recipe _craftToastRecipe;
        private string _craftToastDetail = "";
        private Button _craftFlashButton;
        private float _craftFlashTimer;
        private readonly Random _fxRandom = new Random();

        // ---- Ambient animation (rendering only) ----
        // Seconds since this screen appeared: drives the room tiles popping in one by one, the
        // detail panel fading up, and every idle loop (flickering windows, chimney smoke).
        private float _elapsed;
        private float _panelOpenedAt = -1f;
        private readonly ParticleField _dust = new ParticleField(34, new RectangleF(150, 236, 980, 380),
            new Vector2(-6, -4), new Vector2(6, 4), 0.8f, 1.8f, 6f, 12f, new Color(230, 210, 170), new Color(255, 200, 130), wobble: 4f);
        private readonly ParticleField _smoke = new ParticleField(14, new RectangleF(330, 60, 60, 110),
            new Vector2(-4, -22), new Vector2(8, -12), 6f, 11f, 3f, 5f, new Color(120, 110, 120), Color.Transparent, wobble: 8f, spawnAtBottom: true);

        private class Spark
        {
            public Vector2 Position, Velocity;
            public float Life, MaxLife, Size;
            public Color Color;
        }
        private readonly List<Spark> _sparks = new List<Spark>();

        private class Floater
        {
            public string Text;
            public Vector2 Position;
            public Color Color;
            public float Life;
            public float Scale;
        }
        private const float FloaterDuration = 1.3f;
        private readonly List<Floater> _floaters = new List<Floater>();

        // ---- Resource "pop" animation state ----
        // Tracks the last-seen value of each resource so a change (from an upgrade)
        // can trigger a brief pop-then-settle animation on that resource's count.
        private int _lastFood, _lastPlanks, _lastScraps;
        private float _foodPopTimer, _planksPopTimer, _scrapsPopTimer;
        private const float ResourcePopDuration = 0.25f;

        // Each room is its cutaway art at 2 screen pixels per art pixel (4/3 layout units on
        // the 1080p canvas), so the pixel art stays crisp. A row is its rooms side by side,
        // RoomGap apart and centred; the rows are sized from the art, so the Archive's wider
        // map room makes the upper floor the wider one. The row Ys are whole screen pixels.
        private const float RoomArtScale = 2f / Game1.RenderScale;
        private const float RoomGap = 24f;
        private const float UpperRowY = 246f;
        private const float GroundRowY = 434f;
        private float _roomHeight;

        public BaseBuilding(Game game, PlayerState playerState) : base(game)
        {
            _playerState = playerState;
        }

        public override void Initialize()
        {
            base.Initialize();

            // Every morning at the base is a save point (SaveGame skips a run that's over).
            SaveGame.Save(_playerState);

            // Seeds with the real current mouse state instead of a blank default, so a click
            // still held down from the previous screen doesn't read as a brand-new click here.
            _previousMouse = InputChecker.GetMouse();

            LayOutRow(UpperFloorRooms, UpperRowY);
            LayOutRow(GroundFloorRooms, GroundRowY);

            _endDayButton = new Button(new RectangleF(490, 655, 300, 55), "Prepare");

            _upgradeButton = new Button(new RectangleF(DetailPanel.X + 30, DetailPanel.Y + UpgradeTop, 360, 50), "Upgrade");
            _closeButton = new Button(new RectangleF(DetailPanel.X + DetailPanel.Width - 150, DetailPanel.Y + 20, 120, 42), "Close");

            // Snapshot starting resource values so the first frame never reads as a "change"
            // and fires a false pop animation.
            _lastFood = _playerState.Food;
            _lastPlanks = _playerState.Planks;
            _lastScraps = _playerState.Scraps;
        }

        private void LayOutRow(BaseRoomType[] rooms, float y)
        {
            // Sized from the Lv 1 art (every tier of a room is the same size); 192x128 if a
            // room has none.
            var sizes = rooms.Select(room =>
            {
                var art = Game1.GetRoomSprite(room, 1);
                return (art != null ? new Vector2(art.Width, art.Height) : new Vector2(192, 128)) * RoomArtScale;
            }).ToArray();
            float x = Game1.CanvasWidth / 2f - (sizes.Sum(size => size.X) + RoomGap * (rooms.Length - 1)) / 2f;
            for (int i = 0; i < rooms.Length; i++)
            {
                _roomButtons[rooms[i]] = new Button(new RectangleF(x, y, sizes[i].X, sizes[i].Y), rooms[i].ToString());
                _roomHeight = Math.Max(_roomHeight, sizes[i].Y);
                x += sizes[i].X + RoomGap;
            }
        }

        public override void Update(GameTime gameTime)
        {
            if (_playerState.IsGameOver)
            {
                Game1.EndRun(victory: false);
                return;
            }

            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            _elapsed += dt;
            _dust.Update(dt);
            _smoke.Update(dt);
            UpdateCraftEffects(dt);
            _foodPopTimer = MathF.Max(0f, _foodPopTimer - dt);
            _planksPopTimer = MathF.Max(0f, _planksPopTimer - dt);
            _scrapsPopTimer = MathF.Max(0f, _scrapsPopTimer - dt);

            var mouse = InputChecker.GetMouse();
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
                        SaveGame.Save(_playerState); // keep today's crafting and upgrades
                        // Prepare is a free round trip - its Back to Base button returns here.
                        ScreenManager.ReplaceScreen(new PreparationScreen(Game, _playerState), ScreenTransitions.FadeTransition(GraphicsDevice));
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
            _panelOpenedAt = _elapsed;
            _statusLog = "";

            // Two columns of recipe buttons under the upgrade button - every recipe for the
            // room is listed, locked ones included, so players can see what's coming.
            _recipeButtons.Clear();
            var roomRecipes = Recipes.Where(r => r.Room == room).ToList();
            // Sized so four rows (the Workshop's eight recipes) fit above the status line.
            const float width = 360f, height = RecipeHeight, rowGap = 5f;
            for (int i = 0; i < roomRecipes.Count; i++)
            {
                float x = DetailPanel.X + 30 + (i % 2) * 380f;
                float y = DetailPanel.Y + RecipeTop + (i / 2) * (height + rowGap);
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
                TryCraft(recipe, button);
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
                SpawnSpendFloaters(food, planks, scraps);
                SetStatus($"{room} upgraded to Lv {level + 1}. {UpgradeNote(room, level + 1)} Upkeep is now {_playerState.DailyUpkeep} Food.", isError: false);
            }
            else
            {
                SetStatus($"Need {FormatCost(food, planks, scraps)} to upgrade.", isError: true);
            }
        }

        private void TryCraft(Recipe recipe, Button button)
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

            var (food, planks, scraps) = recipe.GetCost(_playerState);
            if (!_playerState.TrySpend(food, planks, scraps))
            {
                SetStatus($"Need {recipe.CostLabel(_playerState)} for {recipe.Name}.", isError: true);
                return;
            }

            string message = recipe.Craft(_playerState);
            SetStatus(message, isError: false);
            SpawnSpendFloaters(food, planks, scraps);
            CelebrateCraft(recipe, button);
        }

        // ---------- Craft feedback ----------

        private void CelebrateCraft(Recipe recipe, Button button)
        {
            _craftToastRecipe = recipe;
            // One short line that fits the card: where it went, or what it did.
            var equipped = _playerState.EquippedWeapon;
            _craftToastDetail = recipe.IsReinforce ? $"{equipped.DisplayName} now rolls {equipped.DiceLabel}"
                : recipe.SampleWeapon != null ? "Equip it on the Prepare screen"
                : recipe.SampleItem != null ? $"In the stash ({OwnedCount(recipe)} owned) - pack it to use"
                : recipe.GetDetail(_playerState);
            _craftToastTimer = CraftToastDuration;
            _craftFlashButton = button;
            _craftFlashTimer = CraftFlashDuration;

            var center = new Vector2(button.Bounds.X + button.Bounds.Width / 2f, button.Bounds.Y + button.Bounds.Height / 2f);
            var palette = new[] { new Color(255, 225, 140), new Color(255, 250, 220), new Color(150, 240, 160), new Color(255, 170, 80) };
            for (int i = 0; i < 34; i++)
            {
                float angle = (float)(_fxRandom.NextDouble() * MathF.PI * 2f);
                float speed = 140f + (float)_fxRandom.NextDouble() * 260f;
                _sparks.Add(new Spark
                {
                    Position = center + new Vector2((float)(_fxRandom.NextDouble() - 0.5) * button.Bounds.Width * 0.6f, 0f),
                    Velocity = new Vector2(MathF.Cos(angle), MathF.Sin(angle) - 0.8f) * speed,
                    MaxLife = 0.55f + (float)_fxRandom.NextDouble() * 0.5f,
                    Size = 2f + (float)_fxRandom.NextDouble() * 2.5f,
                    Color = palette[_fxRandom.Next(palette.Length)]
                });
            }

            _floaters.Add(new Floater
            {
                Text = recipe.IsReinforce ? "+1 damage" : $"+1 {recipe.Name}",
                Position = new Vector2(button.Bounds.X + 16, button.Bounds.Y - 6),
                Color = new Color(150, 240, 160),
                Scale = 0.9f
            });
        }

        /// <summary>"-3" readouts rising off whichever resource counters just paid for something.</summary>
        private void SpawnSpendFloaters(int food, int planks, int scraps)
        {
            void Spawn(int amount, float centerX)
            {
                if (amount <= 0) return;
                _floaters.Add(new Floater
                {
                    Text = $"-{amount}",
                    Position = new Vector2(centerX + 20, 70),
                    Color = new Color(255, 130, 110),
                    Scale = 1.1f
                });
            }
            Spawn(food, 900);
            Spawn(planks, 1010);
            Spawn(scraps, 1120);
        }

        private void UpdateCraftEffects(float dt)
        {
            _craftToastTimer = MathF.Max(0f, _craftToastTimer - dt);
            _craftFlashTimer = MathF.Max(0f, _craftFlashTimer - dt);

            for (int i = _sparks.Count - 1; i >= 0; i--)
            {
                var spark = _sparks[i];
                spark.Life += dt;
                spark.Velocity += new Vector2(0f, 520f) * dt; // gravity
                spark.Velocity *= 1f - 1.5f * dt;             // drag
                spark.Position += spark.Velocity * dt;
                if (spark.Life >= spark.MaxLife) _sparks.RemoveAt(i);
            }

            for (int i = _floaters.Count - 1; i >= 0; i--)
            {
                var floater = _floaters[i];
                floater.Life += dt;
                floater.Position.Y -= 34f * dt;
                if (floater.Life >= FloaterDuration) _floaters.RemoveAt(i);
            }
        }

        private void SetStatus(string message, bool isError)
        {
            _statusLog = message;
            _statusIsError = isError;
        }

        private (int food, int planks, int scraps) GetUpgradeCost(BaseRoomType room, int currentLevel)
        {
            // Nothing is cheap after the end of the world: every upgrade needs Food for the
            // builders plus a real pile of salvage, and the price climbs steeply with each level.
            int tier = currentLevel;
            return room switch
            {
                // (tier 1-4: the level you're upgrading from - Lv 4 -> 5 costs four times Lv 1 -> 2)
                BaseRoomType.Storage => (5 * tier, 6 * tier, 5 * tier),
                BaseRoomType.Workshop => (5 * tier, 4 * tier, 7 * tier),
                BaseRoomType.Infirmary => (7 * tier, 2 * tier, 6 * tier),
                BaseRoomType.Kitchen => (6 * tier, 6 * tier, 2 * tier),
                BaseRoomType.Barrack => (8 * tier, 5 * tier, 3 * tier),
                BaseRoomType.Archive => (6 * tier, 2 * tier, 9 * tier),
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
            BaseRoomType.Storage => "Caps what you keep, your belt, and how much you haul.",
            BaseRoomType.Workshop => "Crafts, reinforces and sharpens weapons.",
            BaseRoomType.Infirmary => "Brews remedies and toughens you for the night.",
            BaseRoomType.Kitchen => "Feeds the house every morning, and holds feasts.",
            BaseRoomType.Barrack => "Trains your dice - and your guard.",
            BaseRoomType.Archive => "Maps the ruins: districts, scouting, enemies, time.",
            _ => ""
        };

        private static string LevelDescription(BaseRoomType room, int level) => (room, level) switch
        {
            (BaseRoomType.Storage, 1) => "Holds 30 of each; belt 2 slots",
            (BaseRoomType.Storage, 2) => "Holds 50; belt 3 slots",
            (BaseRoomType.Storage, 3) => "Holds 70; belt 4; packframes - Grab takes it all",
            (BaseRoomType.Storage, 4) => "Holds 90; belt 5; root cellar - Food never spoils",
            (BaseRoomType.Storage, _) => "Holds 120; belt 6; caches hold 25% more",

            (BaseRoomType.Workshop, 1) => "Clubs; reinforce weapons to +1",
            (BaseRoomType.Workshop, 2) => "Iron weapons; reinforce to +2",
            (BaseRoomType.Workshop, 3) => "Holy Lance and War Maul; reinforce to +3",
            (BaseRoomType.Workshop, 4) => "Whetstone - +1 damage on every hit; reinforce to +4",
            (BaseRoomType.Workshop, _) => "Dawnbreaker, the Herald-killer; reinforce to +5",

            (BaseRoomType.Infirmary, 1) => "Bandages",
            (BaseRoomType.Infirmary, 2) => "Tonics and Smoke Flasks",
            (BaseRoomType.Infirmary, 3) => "Elixirs and Dawn Tinctures",
            (BaseRoomType.Infirmary, 4) => "Holy Water; field kit - 120 health at night",
            (BaseRoomType.Infirmary, _) => "Surgeon's hands - heals +50%; 140 health at night",

            (BaseRoomType.Kitchen, 1) => "+3 Food each morning",
            (BaseRoomType.Kitchen, 2) => "+6 Food each morning; Rations",
            (BaseRoomType.Kitchen, 3) => "+9 Food each morning; Feast (+15 Hope)",
            (BaseRoomType.Kitchen, 4) => "+11 Food; smokehouse - upkeep 2 Food less",
            (BaseRoomType.Kitchen, _) => "+13 Food; grand feast - Feast gives +22 Hope",

            (BaseRoomType.Barrack, 1) => "1 auto-reroll per fight",
            (BaseRoomType.Barrack, 2) => "2 rerolls per fight, dice never roll a 1",
            (BaseRoomType.Barrack, 3) => "+1 extra die on every attack",
            (BaseRoomType.Barrack, 4) => "Riposte - blocking HEAVY or STUN strikes back",
            (BaseRoomType.Barrack, _) => "3 rerolls, dice never roll below 3",

            (BaseRoomType.Archive, 1) => "Village Outskirts",
            (BaseRoomType.Archive, 2) => "+ Church Ruins; scout 1 room ahead",
            (BaseRoomType.Archive, 3) => "+ Castle Keep; scout 2 rooms ahead",
            (BaseRoomType.Archive, 4) => "Bestiary - see what's in scouted enemy rooms",
            (BaseRoomType.Archive, _) => "Old roads - an extra hour before dawn",

            _ => ""
        };

        // Short line shown on each room tile so the house reads at a glance.
        private string TileSummary(BaseRoomType room) => room switch
        {
            BaseRoomType.Storage => $"Holds {_playerState.StorageCap}, belt {_playerState.BeltSlots}",
            BaseRoomType.Workshop => _playerState.WorkshopEdge > 0 ? $"Reinforce to +{_playerState.MaxReinforcement}, whetstone" : $"Reinforce to +{_playerState.MaxReinforcement}",
            BaseRoomType.Infirmary => $"{_playerState.NightMaxHealth} health at night",
            BaseRoomType.Kitchen => $"+{_playerState.KitchenDailyFood} Food per morning",
            BaseRoomType.Barrack => $"{_playerState.BarracksRerolls} reroll(s) per fight",
            BaseRoomType.Archive => $"{DistrictInfo.Scavenging.Count(_playerState.ArchiveReaches)} district(s) open",
            _ => ""
        };

        private static string UpgradeNote(BaseRoomType room, int newLevel) => room switch
        {
            BaseRoomType.Archive when newLevel == 2 => "Church Ruins is now open.",
            BaseRoomType.Archive when newLevel == 3 => "Castle Keep is now open.",
            BaseRoomType.Archive when newLevel == 4 => "Scouted enemy rooms now show who's inside.",
            BaseRoomType.Archive => "Nights last an hour longer.",
            BaseRoomType.Workshop when newLevel == 4 => "Whetstone: +1 damage on every hit.",
            BaseRoomType.Workshop when newLevel == 5 => "Dawnbreaker unlocked.",
            BaseRoomType.Workshop => "New weapons, and reinforcing goes one level higher.",
            BaseRoomType.Infirmary when newLevel == 4 => "Holy Water unlocked, and 120 health at night.",
            BaseRoomType.Infirmary when newLevel == 5 => "Remedies heal 50% more, and 140 health at night.",
            BaseRoomType.Infirmary => "New recipes unlocked.",
            BaseRoomType.Kitchen when newLevel == 2 => "Rations unlocked.",
            BaseRoomType.Kitchen when newLevel == 3 => "Feast unlocked.",
            BaseRoomType.Kitchen when newLevel == 4 => "The smokehouse cuts upkeep by 2 Food.",
            BaseRoomType.Kitchen when newLevel == 5 => "Feasts now give +22 Hope.",
            BaseRoomType.Storage when newLevel == 2 => "One more belt slot.",
            BaseRoomType.Storage when newLevel == 3 => "One more belt slot, and packframes: grabbing a cache takes it all.",
            BaseRoomType.Storage when newLevel == 4 => "One more belt slot, and a root cellar: Food never spoils.",
            BaseRoomType.Storage when newLevel == 5 => "One more belt slot, and caches hold 25% more.",
            BaseRoomType.Barrack when newLevel == 4 => "Riposte: a blocked HEAVY or STUN strikes back.",
            _ => ""
        };

        // ---------- Draw ----------

        public override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(new Color(32, 28, 34)); // Day stays safe, but the light is bleak rather than cheerful - the lore's holy light is a threat, not a comfort

            var spriteBatch = Game1.SpriteBatch;
            var font = Game1.Font;
            UITheme.BeginCanvas(spriteBatch);

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

            // Over everything, including the detail panel's dimming.
            DrawCraftEffects(spriteBatch, font);

            spriteBatch.End();
        }

        // Until there's painted house art, the house is drawn from shapes: a bleak daytime
        // sky (the holy light outside is a threat, not a comfort), the ruined town beyond,
        // and a timber house cut away to show its two floors. Drop a real sprite in here
        // later in place of DrawHouse.
        private void DrawBackground(SpriteBatch spriteBatch)
        {
            // Pale, washed-out sky with a harsh white glare high up - the light that burns.
            Backdrop.Sky(spriteBatch, new Color(92, 84, 96), new Color(40, 34, 40));
            float glare = UITheme.PulseSine(_elapsed, 0.5f);
            UITheme.DrawGlow(spriteBatch, new Vector2(1010, -40), 420f, new Color(255, 250, 230) * (0.18f + glare * 0.05f));

            Backdrop.DrawSkyline(spriteBatch, 600, 150, new Color(70, 62, 72), seed: 11, drift: MathF.Sin(_elapsed * 0.05f) * 6f);
            Backdrop.DrawSkyline(spriteBatch, 640, 90, new Color(48, 42, 50), seed: 29, drift: MathF.Sin(_elapsed * 0.05f) * 12f);

            DrawHouse(spriteBatch);
        }

        private void DrawHouse(SpriteBatch spriteBatch)
        {
            var wall = new RectangleF(150, 232, 980, 386);
            Color timber = new Color(58, 42, 36);
            Color timberDark = new Color(34, 24, 22);

            // Chimney (behind the roof) with smoke curling out of it.
            spriteBatch.FillRectangle(new RectangleF(334, 140, 40, 70), timberDark);
            spriteBatch.FillRectangle(new RectangleF(328, 134, 52, 10), new Color(46, 34, 30));
            _smoke.Draw(spriteBatch, 0.55f);

            // Roof: a dark pitched roof with a lighter ridge line and eaves overhang.
            Backdrop.FillTriangle(spriteBatch, new Vector2(640, 118), 238, 560, new Color(44, 30, 30));
            Backdrop.FillTriangle(spriteBatch, new Vector2(640, 132), 238, 520, new Color(60, 40, 38));
            spriteBatch.DrawLine(new Vector2(80, 238), new Vector2(640, 118), new Color(90, 62, 50), 5f);
            spriteBatch.DrawLine(new Vector2(640, 118), new Vector2(1200, 238), new Color(90, 62, 50), 5f);
            // Attic window, lit when the Archive has been worked.
            float attic = 0.35f + 0.1f * _playerState.Level(BaseRoomType.Archive) + UITheme.PulseSine(_elapsed, 1.3f) * 0.08f;
            UITheme.DrawGlow(spriteBatch, new Vector2(640, 190), 60f, new Color(255, 190, 110) * (attic * 0.5f));
            UITheme.FillCircle(spriteBatch, new Vector2(640, 190), 18f, timberDark);
            UITheme.FillCircle(spriteBatch, new Vector2(640, 190), 13f, new Color(255, 190, 110) * attic);

            // Walls: dark timber, corner posts, a post between each pair of rooms, and a floor
            // beam between the storeys.
            UITheme.FillGradientRect(spriteBatch, wall, new Color(52, 40, 38), new Color(34, 26, 26), 8);
            spriteBatch.FillRectangle(new RectangleF(wall.X - 5, wall.Y, 10, wall.Height), timber);
            spriteBatch.FillRectangle(new RectangleF(wall.Right - 5, wall.Y, 10, wall.Height), timber);
            foreach (var row in new[] { UpperFloorRooms, GroundFloorRooms })
            {
                for (int i = 1; i < row.Length; i++)
                {
                    float x = (_roomButtons[row[i - 1]].Bounds.Right + _roomButtons[row[i]].Bounds.X) / 2f;
                    var rowBounds = _roomButtons[row[i]].Bounds;
                    spriteBatch.FillRectangle(new RectangleF(x - 5, rowBounds.Y - 8, 10, rowBounds.Height + 16), timber);
                }
            }
            float floorBeamY = (UpperRowY + _roomHeight + GroundRowY) / 2f;
            spriteBatch.FillRectangle(new RectangleF(wall.X, floorBeamY - 4, wall.Width, 8), timber);
            spriteBatch.FillRectangle(new RectangleF(wall.X - 10, wall.Y - 6, wall.Width + 20, 8), timber);
            spriteBatch.FillRectangle(new RectangleF(wall.X - 10, wall.Bottom - 6, wall.Width + 20, 14), timberDark);

            // Motes of dust drifting in whatever light gets in.
            _dust.Draw(spriteBatch, 0.8f);
        }

        private void DrawRooms(SpriteBatch spriteBatch, SpriteFont font)
        {
            foreach (var room in AllRooms)
            {
                DrawRoomPanel(spriteBatch, font, room);
            }
        }

        private void DrawRoomPanel(SpriteBatch spriteBatch, SpriteFont font, BaseRoomType room)
        {
            var button = _roomButtons[room];
            int level = _playerState.RoomLevels[room];
            bool maxed = level >= MaxRoomLevel;
            float hover = button.HoverAmount;

            // Rooms pop into place one after another when the screen opens, and lift toward
            // you on hover.
            int index = Array.IndexOf(UpperFloorRooms, room) >= 0 ? Array.IndexOf(UpperFloorRooms, room) : 3 + Array.IndexOf(GroundFloorRooms, room);
            float intro = Anim.Stagger(_elapsed, index, step: 0.07f, baseDelay: 0.05f, duration: 0.4f);
            if (intro <= 0.001f) return;
            float pop = MathHelper.Lerp(0.88f, 1f, UITheme.EaseOutBack(intro));
            var bounds = Anim.Scale(button.Bounds, pop);
            bounds = new RectangleF(bounds.X, bounds.Y - hover * 4f, bounds.Width, bounds.Height);

            // Lamplight behind the pane: the better the room, the warmer and brighter it burns.
            float flicker = 1f + 0.06f * MathF.Sin(_elapsed * 6.1f + index) + 0.04f * MathF.Sin(_elapsed * 11.3f + index * 2f);
            var center = new Vector2(bounds.X + bounds.Width / 2f, bounds.Y + bounds.Height / 2f);
            UITheme.DrawGlow(spriteBatch, center, bounds.Width * (0.55f + level * 0.05f), new Color(255, 170, 90) * ((0.06f + level * 0.035f) * flicker * intro));

            // The room's cutaway art in a timber frame. Hover eases the frame to an ember glow
            // - the corruption-glow language used for room "tells" at night - and warms the
            // room a touch. Maxed rooms keep a green frame, and glow too on hover since
            // they're still clickable for crafting.
            Color frameColor = maxed ? new Color(60, 110, 75) : new Color(24, 20, 22);
            frameColor = Color.Lerp(frameColor, new Color(230, 110, 55), hover);
            float frame = MathHelper.Lerp(3f, 4f, hover);
            UITheme.DrawSoftShadow(spriteBatch, bounds, 4f, 0.6f);
            spriteBatch.FillRectangle(new RectangleF(bounds.X - frame, bounds.Y - frame, bounds.Width + frame * 2f, bounds.Height + frame * 2f), frameColor);

            var art = Game1.GetRoomSprite(room, level);
            if (art != null)
            {
                UITheme.DrawPixelArt(spriteBatch, art, bounds);
            }
            else
            {
                UITheme.FillGradientRect(spriteBatch, bounds, new Color(70, 64, 68), new Color(40, 36, 40));
            }
            spriteBatch.FillRectangle(bounds, new Color(255, 190, 120) * (0.07f * hover));

            // Darkened across the top so the name and summary read over the brickwork.
            UITheme.FillGradientRect(spriteBatch, new RectangleF(bounds.X, bounds.Y, bounds.Width, 66f), Color.Black * 0.6f, Color.Transparent, 22);

            UITheme.DrawTextWithShadow(spriteBatch, font, room.ToString(), new Vector2(bounds.X + 12, bounds.Y + 10), Color.White);
            UITheme.DrawTextWithShadow(spriteBatch, font, TileSummary(room), new Vector2(bounds.X + 12, bounds.Y + 38), new Color(235, 200, 160), 0.8f);

            // Level pips in the top-right corner: one lit per level reached.
            for (int pip = 0; pip < MaxRoomLevel; pip++)
            {
                var pipCenter = new Vector2(bounds.Right - 18 - (MaxRoomLevel - 1 - pip) * 14, bounds.Y + 20);
                if (pip < level)
                {
                    UITheme.DrawGlow(spriteBatch, pipCenter, 10f, new Color(255, 190, 90) * 0.6f);
                    UITheme.FillCircle(spriteBatch, pipCenter, 4.5f, new Color(255, 205, 120));
                }
                else
                {
                    // A dim socket, ringed in black so it shows against the dark brickwork.
                    UITheme.FillCircle(spriteBatch, pipCenter, 5.5f, Color.Black * 0.6f);
                    UITheme.FillCircle(spriteBatch, pipCenter, 3.5f, new Color(110, 100, 96));
                }
            }

            // Level and upgrade cost on a dark chip in the bottom-left corner, over the floor.
            var chipText = new List<(string text, Color color)>();
            if (maxed)
            {
                chipText.Add(("MAX", new Color(160, 225, 175)));
            }
            else
            {
                var (food, planks, scraps) = GetUpgradeCost(room, level);
                bool canAfford = _playerState.CanAfford(food, planks, scraps);
                // Green when the player can afford the upgrade right now, red when they can't -
                // turns a mental subtraction into an instant glance.
                Color costColor = canAfford ? new Color(120, 220, 130) : new Color(230, 100, 90);
                chipText.Add(($"Lv {level}/{MaxRoomLevel}", new Color(220, 215, 210)));
                chipText.Add((FormatCost(food, planks, scraps), costColor));
            }
            const float chipScale = 0.85f, chipSpacing = 12f;
            float chipTextWidth = chipText.Sum(part => UITheme.MeasureString(font, part.text).X * chipScale) + chipSpacing * (chipText.Count - 1);
            var chip = new RectangleF(bounds.X + 8, bounds.Bottom - 36, chipTextWidth + 20, 28);
            UITheme.FillRoundedRect(spriteBatch, chip, Color.Black * 0.62f, 8f);
            float textX = chip.X + 10;
            foreach (var (text, color) in chipText)
            {
                UITheme.DrawTextWithShadow(spriteBatch, font, text, new Vector2(textX, chip.Y + 4), color, chipScale);
                textX += UITheme.MeasureString(font, text).X * chipScale + chipSpacing;
            }
        }

        private void DrawDetailPanel(SpriteBatch spriteBatch, SpriteFont font, BaseRoomType room)
        {
            int level = _playerState.RoomLevels[room];
            bool maxed = level >= MaxRoomLevel;
            var panel = DetailPanel;

            // Dim the house behind so the panel reads as the focus.
            float dim = 0.55f * Anim.Intro(_elapsed, _panelOpenedAt, 0.2f);
            UITheme.FillGradientRect(spriteBatch, new RectangleF(0, 0, 1280, 720), Color.Black * dim, Color.Black * dim, 1);
            UITheme.DrawPanel(spriteBatch, panel, new Color(62, 54, 58), new Color(34, 30, 34), new Color(200, 100, 55), 3f, 18f, shadowStrength: 0.9f);

            UITheme.DrawTextWithShadow(spriteBatch, font, $"{room}   Lv {level}/{MaxRoomLevel}", new Vector2(panel.X + 30, panel.Y + 24), Color.White, 1.2f);
            UITheme.DrawTextWithShadow(spriteBatch, font, RoomRole(room), new Vector2(panel.X + 30, panel.Y + 62), new Color(200, 190, 195));

            // The whole ladder, so a build can be planned: reached levels lit, the next one
            // highlighted, the rest dim.
            for (int lv = 1; lv <= MaxRoomLevel; lv++)
            {
                bool reached = lv <= level;
                bool next = lv == level + 1;
                Color color = reached ? new Color(235, 220, 200) : next ? new Color(255, 190, 120) : new Color(140, 135, 135);
                string marker = reached ? "*" : next ? ">" : " ";
                UITheme.DrawTextWithShadow(spriteBatch, font, $"{marker} Lv {lv}:  {LevelDescription(room, lv)}", new Vector2(panel.X + 30, panel.Y + LadderTop + (lv - 1) * 20), color, 0.72f);
            }

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

                // Every level is another mouth to feed - say so before the player commits.
                int upkeep = _playerState.DailyUpkeep;
                int upkeepAfter = _playerState.UpkeepIfUpgraded(room);
                float noteX = _upgradeButton.Bounds.X + _upgradeButton.Bounds.Width + 24;
                UITheme.DrawTextWithShadow(spriteBatch, font, $"Upkeep {upkeep} -> {upkeepAfter} Food each morning", new Vector2(noteX, _upgradeButton.Bounds.Y + 2), new Color(235, 200, 160), 0.8f);
                string upkeepNote = upkeepAfter < upkeep ? "The smokehouse stretches every meal." : "A bigger house has more mouths to feed.";
                UITheme.DrawTextWithShadow(spriteBatch, font, upkeepNote, new Vector2(noteX, _upgradeButton.Bounds.Y + 28), new Color(170, 165, 160), 0.7f);
            }

            // Recipes
            if (_recipeButtons.Count > 0)
            {
                UITheme.DrawTextWithShadow(spriteBatch, font, "Craft", new Vector2(panel.X + 30, panel.Y + RecipeTop - 30), Color.White);

                foreach (var (button, recipe) in _recipeButtons)
                {
                    bool unlocked = level >= recipe.RequiredLevel;
                    bool blocked = unlocked && recipe.BlockedReason(_playerState) != null;
                    var (food, planks, scraps) = recipe.GetCost(_playerState);
                    bool canAfford = _playerState.CanAfford(food, planks, scraps);

                    string rightText;
                    Color rightColor;
                    if (!unlocked)
                    {
                        rightText = $"Needs Lv {recipe.RequiredLevel}";
                        rightColor = new Color(160, 150, 150);
                    }
                    else
                    {
                        rightText = recipe.CostLabel(_playerState);
                        rightColor = canAfford && !blocked ? new Color(120, 220, 130) : new Color(230, 100, 90);
                    }

                    DrawRecipeButton(spriteBatch, font, button, recipe, rightText, rightColor, unlocked && !blocked);
                }
            }

            // Status line inside the panel
            if (!string.IsNullOrEmpty(_statusLog))
            {
                Color statusColor = _statusIsError ? new Color(255, 170, 160) : new Color(170, 235, 180);
                UITheme.DrawTextWithShadow(spriteBatch, font, _statusLog, new Vector2(panel.X + 30, panel.Y + panel.Height - 30), statusColor, 0.85f);
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

            var labelSize = UITheme.MeasureString(font, button.Label);
            float textY = drawBounds.Y + (drawBounds.Height - labelSize.Y) / 2f;

            if (rightText == null)
            {
                UITheme.DrawTextWithShadow(spriteBatch, font, button.Label, new Vector2(drawBounds.X + (drawBounds.Width - labelSize.X) / 2f, textY), Color.White);
            }
            else
            {
                UITheme.DrawTextWithShadow(spriteBatch, font, button.Label, new Vector2(drawBounds.X + 14, textY), Color.White);
                var rightSize = UITheme.MeasureString(font, rightText);
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

            // The recipe's icon at native 32px on the left; text shifts over.
            float textX = drawBounds.X + 12;
            var icon = RecipeIcon(recipe);
            if (icon != null)
            {
                UITheme.DrawIconSlot(spriteBatch, icon, new Vector2(drawBounds.X + 14, drawBounds.Y + (drawBounds.Height - 32) / 2f), 1);
                textX = drawBounds.X + 56;
            }

            UITheme.DrawTextWithShadow(spriteBatch, font, recipe.Name, new Vector2(textX, drawBounds.Y + 4), nameColor, 0.95f);

            // How many you already have, right beside the name - amber for a weapon you'd be
            // duplicating (reinforce the one you have instead), green for stackable remedies.
            int owned = OwnedCount(recipe);
            if (owned > 0)
            {
                string ownedText = $"({owned})";
                float nameWidth = UITheme.MeasureString(font, recipe.Name).X * 0.95f;
                Color ownedColor = recipe.SampleWeapon != null ? new Color(255, 190, 90) : new Color(170, 220, 175);
                UITheme.DrawTextWithShadow(spriteBatch, font, ownedText, new Vector2(textX + nameWidth + 8, drawBounds.Y + 7), ownedColor, 0.72f);
            }

            UITheme.DrawTextWithShadow(spriteBatch, font, recipe.GetDetail(_playerState), new Vector2(textX, drawBounds.Y + 28), detailColor, 0.72f);

            var rightSize = UITheme.MeasureString(font, rightText) * 0.85f;
            UITheme.DrawTextWithShadow(spriteBatch, font, rightText, new Vector2(drawBounds.X + drawBounds.Width - rightSize.X - 12, drawBounds.Y + 5), rightColor, 0.85f);

            // Just crafted: a bright green flash that fades out.
            if (button == _craftFlashButton && _craftFlashTimer > 0f)
            {
                float flash = _craftFlashTimer / CraftFlashDuration;
                UITheme.DrawGlow(spriteBatch, new Vector2(drawBounds.X + drawBounds.Width / 2f, drawBounds.Y + drawBounds.Height / 2f), drawBounds.Width * 0.6f, new Color(140, 255, 160) * (0.35f * flash));
                UITheme.FillRoundedRect(spriteBatch, drawBounds, new Color(140, 255, 160) * (0.3f * flash), 10f);
            }
        }

        private int OwnedCount(Recipe recipe)
        {
            if (recipe.SampleWeapon != null) return _playerState.Inventory.Count(w => w.Name == recipe.Name);
            if (recipe.SampleItem != null) return _playerState.OwnedItemCount(recipe.Name);
            return 0;
        }

        private void DrawCraftEffects(SpriteBatch spriteBatch, SpriteFont font)
        {
            foreach (var spark in _sparks)
            {
                float alpha = 1f - spark.Life / spark.MaxLife;
                UITheme.DrawGlow(spriteBatch, spark.Position, spark.Size * 4f, spark.Color * (0.35f * alpha));
                UITheme.FillCircle(spriteBatch, spark.Position, spark.Size, spark.Color * alpha);
            }

            foreach (var floater in _floaters)
            {
                float alpha = 1f - MathF.Pow(floater.Life / FloaterDuration, 2f);
                UITheme.DrawTextWithShadow(spriteBatch, font, floater.Text, floater.Position, floater.Color * alpha, floater.Scale, shadowAlpha: 0.45f * alpha);
            }

            if (_craftToastTimer > 0f && _craftToastRecipe != null)
            {
                DrawCraftToast(spriteBatch, font);
            }
        }

        /// <summary>The "Crafted!" card between the Hope bar and the resource counters: drops in
        /// with a little overshoot, holds, then fades.</summary>
        private void DrawCraftToast(SpriteBatch spriteBatch, SpriteFont font)
        {
            float elapsed = CraftToastDuration - _craftToastTimer;
            float drop = UITheme.EaseOutBack(elapsed / 0.3f);
            float alpha = MathHelper.Clamp(_craftToastTimer / 0.45f, 0f, 1f) * MathHelper.Clamp(elapsed / 0.12f, 0f, 1f);

            var card = new RectangleF(500, -70 + 86 * drop, 340, 84);
            var gold = new Color(255, 215, 120);
            UITheme.DrawGlow(spriteBatch, new Vector2(card.X + card.Width / 2f, card.Y + card.Height / 2f), 230f, gold * (0.3f * alpha));
            UITheme.DrawPanel(spriteBatch, card, new Color(72, 96, 62) * alpha, new Color(40, 58, 36) * alpha, gold * alpha, 3f, 14f, shadowStrength: 0.8f * alpha);

            // Icon on the left: the recipe's pixel art at 2x, or a vial for anything without art.
            var iconCenter = new Vector2(card.X + 44, card.Y + card.Height / 2f);
            UITheme.FillRoundedRect(spriteBatch, new RectangleF(iconCenter.X - 34, iconCenter.Y - 34, 68, 68), Color.Black * (0.35f * alpha), 10f);
            var recipe = _craftToastRecipe;
            var icon = RecipeIcon(recipe);
            if (icon != null)
            {
                UITheme.DrawPixelIcon(spriteBatch, icon, iconCenter - new Vector2(32, 32), 64f / icon.Width, Color.White * alpha);
            }
            else
            {
                DrawVial(spriteBatch, iconCenter, VialColor(recipe.SampleItem), alpha);
            }

            float textX = card.X + 90;
            UITheme.DrawTextWithShadow(spriteBatch, font, "CRAFTED!", new Vector2(textX, card.Y + 10), gold * alpha, 0.75f, shadowAlpha: 0.45f * alpha);
            UITheme.DrawTextWithShadow(spriteBatch, font, recipe.Name, new Vector2(textX, card.Y + 30), Color.White * alpha, 1.1f, shadowAlpha: 0.45f * alpha);

            UITheme.DrawTextWithShadow(spriteBatch, font, _craftToastDetail, new Vector2(textX, card.Y + 60), new Color(210, 230, 200) * alpha, 0.62f, shadowAlpha: 0.45f * alpha);
        }

        /// <summary>The weapon a recipe makes (or reinforces), the item it makes, or bread for the
        /// Feast. null if that has no art.</summary>
        private Texture2D RecipeIcon(Recipe recipe)
        {
            var weapon = recipe.IconWeapon(_playerState);
            if (weapon != null) return Game1.GetWeaponIcon(weapon);
            if (recipe.SampleItem != null) return Game1.GetItemIcon(recipe.SampleItem.Name);
            return recipe.Room == BaseRoomType.Kitchen ? Game1.BreadTexture : null;
        }

        private static Color VialColor(Item item) => item?.Effect switch
        {
            ItemEffect.Heal => new Color(220, 70, 70),
            ItemEffect.FullHeal => new Color(255, 200, 80),
            ItemEffect.Smoke => new Color(165, 165, 180),
            ItemEffect.RestoreDawn => new Color(255, 150, 90),
            _ => new Color(150, 220, 160)
        };

        // For an item without art - a little glass vial stands in, tinted by what it does.
        private static void DrawVial(SpriteBatch spriteBatch, Vector2 center, Color liquid, float alpha)
        {
            var glass = new Color(220, 230, 240);
            UITheme.FillRoundedRect(spriteBatch, new RectangleF(center.X - 6, center.Y - 24, 12, 14), glass * (0.8f * alpha), 3f);        // neck
            UITheme.FillRoundedRect(spriteBatch, new RectangleF(center.X - 7, center.Y - 28, 14, 6), new Color(150, 105, 60) * alpha, 2f); // cork
            UITheme.FillCircle(spriteBatch, center + new Vector2(0, 6), 19f, glass * (0.8f * alpha));
            UITheme.FillCircle(spriteBatch, center + new Vector2(0, 8), 15f, liquid * alpha);
            UITheme.FillCircle(spriteBatch, center + new Vector2(-6, 0), 4f, Color.White * (0.6f * alpha));
        }

        private void DrawFooter(SpriteBatch spriteBatch, SpriteFont font)
        {
            if (string.IsNullOrEmpty(_statusLog)) return;

            var position = new Vector2(180, GroundRowY + _roomHeight + 18);
            var textSize = UITheme.MeasureString(font, _statusLog);
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

            // A slow ember breath around the button - the way out of the day is always calling.
            float breathe = UITheme.PulseSine(_elapsed, 1.6f);
            UITheme.DrawGlow(spriteBatch, new Vector2(drawBounds.X + drawBounds.Width / 2f, drawBounds.Y + drawBounds.Height / 2f), drawBounds.Width * 0.6f, new Color(255, 120, 50) * (0.1f + breathe * 0.08f + hover * 0.12f));
            UITheme.DrawPanel(spriteBatch, drawBounds, top, bottom, border, borderThickness, 12f, shadowStrength: 0.8f);

            var textSize = UITheme.MeasureString(font, button.Label);
            var textPos = new Vector2(
                drawBounds.X + (drawBounds.Width - textSize.X) / 2f,
                drawBounds.Y + (drawBounds.Height - textSize.Y) / 2f);
            UITheme.DrawTextWithShadow(spriteBatch, font, button.Label, textPos, Color.White);
        }

        // The 128x32 Hope bar art at 5 screen pixels per art pixel: its sparkle sits on art
        // rows 5-12 over the left end, the bar itself on rows 12-20.
        private const int HopeBarScreenScale = 5;
        private static readonly Vector2 HopeBarPosition = new Vector2(56, 4);

        private void DrawHopeBar(SpriteBatch spriteBatch, SpriteFont font, GameTime gameTime)
        {
            float ratio = _playerState.Hope / (float)PlayerState.MaxHope;
            // Slow reddish warning pulse once Hope runs low - purely cosmetic, doesn't touch
            // the actual game-over threshold or value.
            Color gaugeTint = Color.White;
            if (ratio < 0.25f)
            {
                float pulse = UITheme.PulseSine((float)gameTime.TotalGameTime.TotalSeconds, 4f);
                gaugeTint = Color.Lerp(Color.White, new Color(255, 120, 100), pulse * 0.7f);
            }
            UITheme.DrawPixelBar(spriteBatch, Game1.HopeBarFrame, Game1.HopeBarGauge, HopeBarPosition, HopeBarScreenScale, ratio, gaugeTint);

            // "Hope 80/100" just right of the sparkle, above the bar.
            float art = HopeBarScreenScale / UITheme.RenderScale;
            var labelPos = new Vector2(HopeBarPosition.X + 16 * art, HopeBarPosition.Y + 14);
            UITheme.DrawTextWithShadow(spriteBatch, font, "Hope", labelPos, Color.White);
            var value = $"{_playerState.Hope}/{PlayerState.MaxHope}";
            float hopeWidth = UITheme.MeasureString(font, "Hope ").X;
            UITheme.DrawTextWithShadow(spriteBatch, font, value, labelPos + new Vector2(hopeWidth + 4, 2), new Color(255, 215, 120), 0.85f);

            // The goal, always in view: which day this is, out of seven.
            string day = DayInfo.IsFinalNight(_playerState.Day) ? $"{DayInfo.Label(_playerState.Day)} - the last night" : DayInfo.Label(_playerState.Day);
            var daySize = UITheme.MeasureString(font, day) * 0.85f;
            float barRight = HopeBarPosition.X + (Game1.HopeBarFrame?.Width ?? 128) * art;
            UITheme.DrawTextWithShadow(spriteBatch, font, day, new Vector2(barRight - 4 - daySize.X, labelPos.Y + 2), new Color(255, 205, 150), 0.85f);
        }

        private void DrawResourceIcons(SpriteBatch spriteBatch, SpriteFont font)
        {
            DrawResourceSlot(spriteBatch, font, 900, "Food", _playerState.Food, DrawFoodIcon, _foodPopTimer, new Color(150, 60, 55));
            DrawResourceSlot(spriteBatch, font, 1010, "Planks", _playerState.Planks, DrawPlanksIcon, _planksPopTimer, new Color(120, 85, 50));
            DrawResourceSlot(spriteBatch, font, 1120, "Scraps", _playerState.Scraps, DrawScrapsIcon, _scrapsPopTimer, new Color(90, 90, 100));

            // Storage cap under the resource row, so the limit is always in view.
            string capText = $"Storage: max {_playerState.StorageCap} each";
            var capSize = UITheme.MeasureString(font, capText) * 0.8f;
            UITheme.DrawTextWithShadow(spriteBatch, font, capText, new Vector2(1010 - capSize.X / 2f, 116), new Color(200, 190, 195), 0.8f);

            // Tomorrow's food bill, next to the Kitchen's contribution - red when the Kitchen
            // plus the larder won't cover it and hunger will eat into Hope.
            int upkeep = _playerState.DailyUpkeep;
            int cooked = _playerState.KitchenDailyFood;
            bool shortTomorrow = _playerState.Food + cooked < upkeep;
            string upkeepText = $"Upkeep: {upkeep} Food/morning  (Kitchen +{cooked})";
            var upkeepSize = UITheme.MeasureString(font, upkeepText) * 0.75f;
            Color upkeepColor = shortTomorrow ? new Color(255, 140, 120) : new Color(235, 200, 160);
            UITheme.DrawTextWithShadow(spriteBatch, font, upkeepText, new Vector2(1010 - upkeepSize.X / 2f, 140), upkeepColor, 0.75f);

            if (shortTomorrow)
            {
                // Spell out the price - hunger is steep enough that it should never be a surprise.
                string warning = $"Bring back food tonight or hunger costs {_playerState.ProjectedHungerCost()} Hope";
                var warningSize = UITheme.MeasureString(font, warning) * 0.7f;
                UITheme.DrawTextWithShadow(spriteBatch, font, warning, new Vector2(1010 - warningSize.X / 2f, 162), new Color(255, 120, 100), 0.7f);
            }
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
            var countSize = UITheme.MeasureString(font, countText) * scale;
            UITheme.DrawTextWithShadow(spriteBatch, font, countText, new Vector2(centerX - countSize.X / 2f, 60), countColor, scale);

            var labelSize = UITheme.MeasureString(font, label);
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