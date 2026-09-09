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
    public enum ExplorationState
    {
        Map,
        Encounter,
        Supplies
    }

    public enum CombatMenu
    {
        TopLevel,
        Skills,
        Items
    }

    public class LerpBar
    {
        private const float CatchUpSpeed = 2f; // higher = the bar catches up to the real value faster

        public float DisplayedValue { get; private set; }
        public float MaxValue { get; }

        public LerpBar(float initialValue, float maxValue)
        {
            DisplayedValue = initialValue;
            MaxValue = maxValue;
        }

        public void Update(GameTime gameTime, float targetValue)
        {
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            float t = Math.Min(1f, CatchUpSpeed * dt);
            DisplayedValue = MathHelper.Lerp(DisplayedValue, targetValue, t);
        }

       
        public float Ratio => MaxValue <= 0 ? 0f : MathHelper.Clamp(DisplayedValue / MaxValue, 0f, 1f);
    }

    public class TextLog
    {
        private const float RiseSpeed = 18f;  
        private const float FadeSpeed = 0.6f; 

        private string _current = "";
        private string _fading = "";
        private float _fadeAlpha;
        private float _fadeOffset;

        public void Push(string message)
        {
            if (string.IsNullOrEmpty(message)) return;

            _fading = _current;   
            _fadeAlpha = 1f;
            _fadeOffset = 0f;
            _current = message;  
        }

        public void Update(GameTime gameTime)
        {
            if (_fadeAlpha <= 0f) return;

            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            _fadeAlpha = Math.Max(0f, _fadeAlpha - FadeSpeed * dt);
            _fadeOffset += RiseSpeed * dt;
        }


        public void Draw(SpriteBatch spriteBatch, SpriteFont font, Vector2 basePosition)
        {
            if (_fadeAlpha > 0f && !string.IsNullOrEmpty(_fading))
            {
                var fadingPos = basePosition - new Vector2(0, 26 + _fadeOffset);
                spriteBatch.DrawString(font, _fading, fadingPos, Color.Gray * _fadeAlpha);
            }

            if (!string.IsNullOrEmpty(_current))
            {
                spriteBatch.DrawString(font, _current, basePosition, Color.White);
            }
        }
    }


    public class NightScavengingScreen : GameScreen
    {
        private Game1 Game1 => (Game1)Game;
        private readonly PlayerState _playerState;
        private readonly Random _random = new Random();

        private const int LayerCount = 6;
        private const int NodesPerLayer = 2;

        private DawnTimer _dawnTimer;
        private NightMap _map;
        private int _currentDepth; 

        private ExplorationState _state = ExplorationState.Map;
        private readonly TextLog _textLog = new TextLog();
        private int _roomsCleared;
        private int _combatTurn;

        private Button _headBackButton;
        private MouseState _previousMouse;


        private LerpBar _playerHealthBar;
        private LerpBar _enemyHealthBar;


        private CombatEncounter _activeCombat;
        private CombatMenu _combatMenu = CombatMenu.TopLevel;
        private readonly List<Button> _combatButtons = new List<Button>();


        private List<ChoiceOption> _suppliesOptions;
        private readonly List<Button> _suppliesButtons = new List<Button>();

        public NightScavengingScreen(Game game, PlayerState playerState) : base(game)
        {
            _playerState = playerState;
        }

        public override void Initialize()
        {
            base.Initialize();

            _playerState.Health = _playerState.MaxHealth;
            _playerHealthBar = new LerpBar(_playerState.Health, _playerState.MaxHealth);

            _dawnTimer = new DawnTimer(startingBudget: 10);
            var roomGenerator = new RoomGenerator();
            _map = new NightMap(roomGenerator, LayerCount, NodesPerLayer);
            LayoutMapNodes();

            _headBackButton = new Button(new RectangleF(1000, 30, 220, 50), "Head Back Before Dawn");
        }

        private void LayoutMapNodes()
        {
            for (int depth = 0; depth < _map.Layers.Count; depth++)
            {
                var layer = _map.Layers[depth];
                for (int i = 0; i < layer.Count; i++)
                {
                    float x = 150 + depth * 170;
                    float y = 220 + i * 200;
                    layer[i].ScreenBounds = new RectangleF(x, y, 130, 130);
                }
            }
        }

        public override void Update(GameTime gameTime)
        {
            if (_playerState.IsGameOver)
            {
                ScreenManager.ReplaceScreen(new GameOverScreen(Game));
                return;
            }

            var mouse = Mouse.GetState();
            bool clicked = InputChecker.IsNewLeftClick(mouse, _previousMouse);


            _playerHealthBar.Update(gameTime, _playerState.Health);
            _textLog.Update(gameTime);
            if (_activeCombat != null)
            {
                _enemyHealthBar?.Update(gameTime, _activeCombat.Enemy.Health);
            }

            if (clicked)
            {
                switch (_state)
                {
                    case ExplorationState.Map:
                        HandleMapClick(mouse.X, mouse.Y);
                        break;
                    case ExplorationState.Encounter:
                        HandleCombatClick(mouse.X, mouse.Y);
                        break;
                    case ExplorationState.Supplies:
                        HandleSuppliesClick(mouse.X, mouse.Y);
                        break;
                }
            }

            _previousMouse = mouse;
        }

        // ---------- Map ----------

        private void HandleMapClick(int x, int y)
        {
            if (_headBackButton.Contains(x, y))
            {
                GoToDawnReturn();
                return;
            }

            if (_currentDepth >= _map.Layers.Count) return;

            foreach (var node in _map.Layers[_currentDepth])
            {
                if (InputChecker.Contains(node.ScreenBounds, x, y))
                {
                    TravelTo(node);
                    return;
                }
            }
        }

        private void TravelTo(MapNode node)
        {
            node.Visited = true;
            _dawnTimer.SpendOnRoomEntry();
            _currentDepth = node.Depth + 1;

            switch (node.Type)
            {
                case RoomType.Encounter:
                    StartEncounter(node.Depth);
                    break;
                case RoomType.Supplies:
                    StartSupplies();
                    break;
                case RoomType.Special:
                    ResolveSpecial();
                    break;
            }

            if (_state == ExplorationState.Map && (!_dawnTimer.HasTimeRemaining || _currentDepth >= _map.Layers.Count))
            {
                GoToDawnReturn();
            }
        }

        private void GoToDawnReturn()
        {
            ScreenManager.ReplaceScreen(new Dawn(Game, _playerState, _roomsCleared, _currentDepth));
        }

        // ---------- Encounter (combat) ----------

        private void StartEncounter(int depth)
        {
            var enemy = new Enemy($"Corrupted Wretch (Depth {depth + 1})", maxHealth: 30 + depth * 3, attackPower: 6 + depth);
            _activeCombat = new CombatEncounter(enemy, _playerState, _dawnTimer, _random);
            _enemyHealthBar = new LerpBar(enemy.MaxHealth, enemy.MaxHealth);
            _combatMenu = CombatMenu.TopLevel;
            _combatTurn = 0;
            LayoutCombatButtons();
            _state = ExplorationState.Encounter;
        }

        private void LayoutCombatButtons()
        {
            _combatButtons.Clear();
            const float x = 40, width = 240, height = 60, gap = 14;
            float y = 230;

            switch (_combatMenu)
            {
                case CombatMenu.TopLevel:
                    _combatButtons.Add(new Button(new RectangleF(x, y, width, height), "Attack")); y += height + gap;
                    _combatButtons.Add(new Button(new RectangleF(x, y, width, height), "Skills")); y += height + gap;
                    _combatButtons.Add(new Button(new RectangleF(x, y, width, height), "Items")); y += height + gap;
                    _combatButtons.Add(new Button(new RectangleF(x, y, width, height), "Flee"));
                    break;

                case CombatMenu.Skills:
                    _combatButtons.Add(new Button(new RectangleF(x, y, width, height), "Power Strike (2 ticks)")); y += height + gap;
                    _combatButtons.Add(new Button(new RectangleF(x, y, width, height), "Guard")); y += height + gap;
                    _combatButtons.Add(new Button(new RectangleF(x, y, width, height), "Back"));
                    break;

                case CombatMenu.Items:
                    for (int i = 0; i < _playerState.Items.Count; i++)
                    {
                        _combatButtons.Add(new Button(new RectangleF(x, y, width, height), _playerState.Items[i].Name));
                        y += height + gap;
                    }
                    _combatButtons.Add(new Button(new RectangleF(x, y, width, height), "Back"));
                    break;
            }
        }

        private void HandleCombatClick(int x, int y)
        {
            foreach (var button in _combatButtons)
            {
                if (!button.Contains(x, y)) continue;

                var label = button.Label;

                if (_combatMenu == CombatMenu.TopLevel)
                {
                    switch (label)
                    {
                        case "Attack":
                            _textLog.Push(_activeCombat.Attack());
                            _combatTurn++;
                            break;
                        case "Skills":
                            _combatMenu = CombatMenu.Skills;
                            LayoutCombatButtons();
                            return;
                        case "Items":
                            _combatMenu = CombatMenu.Items;
                            LayoutCombatButtons();
                            return;
                        case "Flee":
                            _textLog.Push(_activeCombat.Flee());
                            EndCombat(fled: true);
                            return;
                    }
                }
                else if (_combatMenu == CombatMenu.Skills)
                {
                    if (label == "Back") { _combatMenu = CombatMenu.TopLevel; LayoutCombatButtons(); return; }
                    var skill = label.StartsWith("Power") ? SkillType.PowerStrike : SkillType.Guard;
                    _textLog.Push(_activeCombat.UseSkill(skill));
                    _combatTurn++;
                }
                else if (_combatMenu == CombatMenu.Items)
                {
                    if (label == "Back") { _combatMenu = CombatMenu.TopLevel; LayoutCombatButtons(); return; }
                    var item = _playerState.Items.Find(it => it.Name == label);
                    if (item != null)
                    {
                        _textLog.Push(_activeCombat.UseItem(item));
                        _combatTurn++;
                    }
                    _combatMenu = CombatMenu.TopLevel;
                }

                if (_activeCombat.IsOver)
                {
                    EndCombat(fled: false);
                }
                else
                {
                    LayoutCombatButtons();
                }

                return;
            }
        }

        private void EndCombat(bool fled)
        {
            if (!fled)
            {
                if (_activeCombat.PlayerWon)
                {
                    _roomsCleared++;
                    _playerState.ChangeHope(2);
                }
                else
                {
                    _playerState.ChangeHope(-15);
                }
            }

            _activeCombat = null;
            _combatMenu = CombatMenu.TopLevel;
            _state = ExplorationState.Map;

            if (!_dawnTimer.HasTimeRemaining || _currentDepth >= _map.Layers.Count)
            {
                GoToDawnReturn();
            }
        }

        // ---------- Supplies ----------

        private void StartSupplies()
        {
            _suppliesOptions = BuildSuppliesOptions();
            _suppliesButtons.Clear();
            const float x = 820, width = 380, height = 130, gap = 16;
            float y = 230;
            for (int i = 0; i < _suppliesOptions.Count; i++)
            {
                _suppliesButtons.Add(new Button(new RectangleF(x, y, width, height), _suppliesOptions[i].Title));
                y += height + gap;
            }
            _state = ExplorationState.Supplies;
        }

        private List<ChoiceOption> BuildSuppliesOptions()
        {
            return new List<ChoiceOption>
            {
                new ChoiceOption("Search quickly", "Fast and safe - a modest find.", (state, rng) =>
                {
                    state.AddResources(food: rng.Next(1, 4), scraps: rng.Next(1, 3));
                    return "You grab what's in easy reach.";
                }),

                new ChoiceOption("Search thoroughly", "Slower, better odds - but noise draws attention.", (state, rng) =>
                {
                    state.AddResources(food: rng.Next(3, 7), planks: rng.Next(2, 5), scraps: rng.Next(2, 5));
                    if (rng.Next(100) < 30)
                    {
                        state.ChangeHope(-5);
                        return "You find a good haul, but the noise costs you some nerve.";
                    }
                    return "You find a good haul and slip away clean.";
                }),

                new ChoiceOption("Leave it", "No risk, no reward - just move on.", (state, rng) =>
                {
                    return "You decide it isn't worth the time.";
                })
            };
        }

        private void HandleSuppliesClick(int x, int y)
        {
            for (int i = 0; i < _suppliesButtons.Count; i++)
            {
                if (!_suppliesButtons[i].Contains(x, y)) continue;

                _textLog.Push(_suppliesOptions[i].Resolve(_playerState, _random));
                _roomsCleared++;
                _state = ExplorationState.Map;

                if (!_dawnTimer.HasTimeRemaining || _currentDepth >= _map.Layers.Count)
                {
                    GoToDawnReturn();
                }
                return;
            }
        }

        // ---------- Special ----------

        private void ResolveSpecial()
        {
            if (_random.Next(100) < 50)
            {
                _playerState.ChangeHope(10);
                _textLog.Push("A moment of quiet beauty in the dark. Hope +10.");
            }
            else
            {
                var weapon = Weapon.LootPool[_random.Next(Weapon.LootPool.Length)];
                _playerState.Inventory.Add(weapon);
                _textLog.Push($"You find a {weapon.Name} ({weapon.DiceLabel}) left behind by someone else.");
            }

            _roomsCleared++;
            _state = ExplorationState.Map;
        }

        // ---------- Draw ----------

        public override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(new Color(8, 8, 14)); 

            var spriteBatch = Game1.SpriteBatch;
            var font = Game1.Font;
            spriteBatch.Begin();

            DrawClock(spriteBatch, font);
            spriteBatch.DrawString(font, $"Weapon: {_playerState.EquippedWeapon.Name} ({_playerState.EquippedWeapon.DiceLabel})", new Vector2(40, 100), Color.LightGray);
            DrawPlayerHealthBar(spriteBatch, font);

            switch (_state)
            {
                case ExplorationState.Map:
                    DrawMinimap(spriteBatch, font);
                    break;
                case ExplorationState.Encounter:
                    DrawCombat(spriteBatch, font);
                    DrawMapFragment(spriteBatch, font);
                    break;
                case ExplorationState.Supplies:
                    DrawSupplies(spriteBatch, font);
                    DrawMapFragment(spriteBatch, font);
                    break;
            }

            spriteBatch.End();
        }

        private void DrawClock(SpriteBatch spriteBatch, SpriteFont font)
        {
            spriteBatch.DrawString(font, $"Time until dawn: {_dawnTimer.RemainingBudget}/{_dawnTimer.MaxBudget}", new Vector2(40, 30), Color.White);
            var clockMax = new RectangleF(40, 60, 300, 20);
            var clockFill = new RectangleF(40, 60, 300 * (_dawnTimer.RemainingBudget / (float)_dawnTimer.MaxBudget), 20);
            spriteBatch.FillRectangle(clockMax, Color.Black * 0.5f);
            spriteBatch.FillRectangle(clockFill, new Color(255, 200, 120));
            spriteBatch.DrawRectangle(clockMax, Color.White, 2f);
        }

        private void DrawPlayerHealthBar(SpriteBatch spriteBatch, SpriteFont font)
        {
            spriteBatch.DrawString(font, $"Health: {_playerState.Health}/{_playerState.MaxHealth}", new Vector2(40, 135), Color.LightGray);
            var barMax = new RectangleF(40, 165, 300, 20);
            var barFill = new RectangleF(40, 165, 300 * _playerHealthBar.Ratio, 20);
            spriteBatch.FillRectangle(barMax, Color.Black * 0.5f);
            spriteBatch.FillRectangle(barFill, new Color(200, 50, 50));
            spriteBatch.DrawRectangle(barMax, Color.White, 2f);
        }

        private void DrawMinimap(SpriteBatch spriteBatch, SpriteFont font)
        {
            for (int depth = 0; depth < _map.Layers.Count; depth++)
            {
                foreach (var node in _map.Layers[depth])
                {
                    Color color;
                    string label;

                    if (node.Visited)
                    {
                        color = new Color(60, 60, 60);
                        label = node.Type.ToString();
                    }
                    else if (depth == _currentDepth)
                    {
                        color = node.Type switch
                        {
                            RoomType.Supplies => new Color(40, 90, 60),
                            RoomType.Encounter => new Color(110, 30, 30),
                            RoomType.Special => new Color(80, 40, 110),
                            _ => Color.Gray
                        };
                        label = node.Type.ToString();
                    }
                    else
                    {
                        color = new Color(30, 30, 40);
                        label = "?";
                    }

                    spriteBatch.FillRectangle(node.ScreenBounds, color);
                    spriteBatch.DrawRectangle(node.ScreenBounds, Color.White * 0.5f, 1f);
                    spriteBatch.DrawString(font, label, new Vector2(node.ScreenBounds.X + 8, node.ScreenBounds.Y + 8), Color.White);
                }
            }

            spriteBatch.FillRectangle(_headBackButton.Bounds, new Color(90, 70, 70));
            spriteBatch.DrawRectangle(_headBackButton.Bounds, Color.White, 2f);
            spriteBatch.DrawString(font, _headBackButton.Label, new Vector2(_headBackButton.Bounds.X + 10, _headBackButton.Bounds.Y + 14), Color.White);
        }

        private void DrawMapFragment(SpriteBatch spriteBatch, SpriteFont font)
        {

            var box = new RectangleF(40, 610, 200, 90);
            spriteBatch.FillRectangle(box, new Color(55, 45, 30));
            spriteBatch.DrawRectangle(box, new Color(150, 120, 70), 2f);
            spriteBatch.DrawString(font, "Tonight's map", new Vector2(box.X + 10, box.Y + 10), new Color(220, 200, 160));
            spriteBatch.DrawString(font, $"Depth {_currentDepth}/{_map.Layers.Count}", new Vector2(box.X + 10, box.Y + 40), new Color(220, 200, 160));
        }

        private void DrawCombat(SpriteBatch spriteBatch, SpriteFont font)
        {
            spriteBatch.DrawString(font, _activeCombat.Enemy.Name, new Vector2(480, 210), Color.White);
            spriteBatch.DrawString(font, $"Turn {_combatTurn + 1}", new Vector2(1000, 210), Color.LightGray);

            // Portrait placeholder - swap for real enemy art once it exists.
            var portrait = new RectangleF(480, 250, 300, 260);
            spriteBatch.FillRectangle(portrait, new Color(35, 20, 25));
            spriteBatch.DrawRectangle(portrait, new Color(120, 40, 40), 2f);

            // Vertical enemy health bar beside the portrait, like the reference - fills from
            // the bottom up so it drains from the top as health drops.
            const float barX = 800, barY = 250, barW = 30, barH = 260;
            float filledHeight = barH * _enemyHealthBar.Ratio;
            var barOuter = new RectangleF(barX, barY, barW, barH);
            var barFill = new RectangleF(barX, barY + (barH - filledHeight), barW, filledHeight);
            spriteBatch.FillRectangle(barOuter, Color.Black * 0.5f);
            spriteBatch.FillRectangle(barFill, Color.OrangeRed);
            spriteBatch.DrawRectangle(barOuter, Color.White, 2f);

            _textLog.Draw(spriteBatch, font, new Vector2(480, 545));

            foreach (var button in _combatButtons)
            {
                spriteBatch.FillRectangle(button.Bounds, new Color(60, 60, 80));
                spriteBatch.DrawRectangle(button.Bounds, Color.White, 2f);
                spriteBatch.DrawString(font, button.Label, new Vector2(button.Bounds.X + 10, button.Bounds.Y + 18), Color.White);
            }
        }

        private void DrawSupplies(SpriteBatch spriteBatch, SpriteFont font)
        {
            spriteBatch.DrawString(font, "You find a supply cache.", new Vector2(60, 220), Color.White);
            spriteBatch.DrawString(font, "What do you do?", new Vector2(60, 250), Color.LightGray);

            // Loot placeholder - swap for real item art once it exists.
            var lootBox = new RectangleF(60, 300, 260, 260);
            spriteBatch.FillRectangle(lootBox, new Color(45, 40, 25));
            spriteBatch.DrawRectangle(lootBox, new Color(140, 110, 60), 2f);

            _textLog.Draw(spriteBatch, font, new Vector2(60, 580));

            for (int i = 0; i < _suppliesButtons.Count; i++)
            {
                var button = _suppliesButtons[i];
                var option = _suppliesOptions[i];
                spriteBatch.FillRectangle(button.Bounds, new Color(60, 70, 60));
                spriteBatch.DrawRectangle(button.Bounds, Color.White, 2f);
                spriteBatch.DrawString(font, option.Title, new Vector2(button.Bounds.X + 10, button.Bounds.Y + 10), Color.White);
                spriteBatch.DrawString(font, option.Description, new Vector2(button.Bounds.X + 10, button.Bounds.Y + 45), Color.LightGray);
            }
        }
    }
}
