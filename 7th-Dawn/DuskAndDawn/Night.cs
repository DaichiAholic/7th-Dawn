using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using MonoGame.Extended.Screens;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DuskAndDawn
{
    public enum ExplorationState
    {
        Map,
        Encounter,
        Supplies,
        Event
    }

    public enum CombatMenu
    {
        TopLevel,
        Skills,
        Items
    }

    public partial class NightScavengingScreen : GameScreen, IGameplayScreen
    {
        private Game1 Game1 => (Game1)Game;
        private readonly PlayerState _playerState;
        private readonly Random _random = new Random();
        private District _district;

        private const int MapColumns = 8;
        private const int MapRows = 4;
        private static readonly RectangleF MapArea = new RectangleF(40, 224, 1200, 376);
        private const float RoomSize = 64f;

        private const int RoamChancePercent = 35;

        private const int RoomEntryMinutes = 30;
        private DawnTimer _dawnTimer;
        private float _displayedElapsed;
        private bool _warnedOfDawn;
        private NightMap _map;
        private MapNode _current;
        private int _roomsVisited;
        private bool _leaving;

        private readonly HashSet<MapNode> _reachable = new HashSet<MapNode>();

        private const float WalkSpeed = 420f;
        private List<MapNode> _walkPath;
        private int _walkIndex;
        private MapNode _cameFrom;
        private Vector2 _tokenPosition;
        private bool IsWalking => _walkPath != null;

        private MapNode _hoveredNode;
        private List<MapNode> _hoverPath;

        private const int EmberCount = 46;
        private readonly List<Ember> _embers = new List<Ember>();

        private ExplorationState _state = ExplorationState.Map;

        private ExplorationState _shownState = ExplorationState.Map;
        private float _stateTime;
        private readonly TextLog _textLog = new TextLog();
        private int _roomsCleared;
        private int _combatTurn;

        private Button _headBackButton;
        private MouseState _previousMouse;
        private KeyboardState _previousKeyboard;

        private LerpBar _playerHealthBar;

        private const float CollapseDuration = 2.6f;
        private float _collapseTimer = -1f;
        private string _collapseText = "";
        private int _startFood, _startPlanks, _startScraps;

        private bool _collapseEndsRun;

        private int _startHope;
        private int _enemiesDefeatedTonight;
        private bool _knockedOutTonight;
        private bool _nightRecorded;

        public NightScavengingScreen(Game game, PlayerState playerState) : base(game)
        {
            _playerState = playerState;
        }

        public override void Initialize()
        {
            base.Initialize();

            _previousMouse = InputChecker.GetMouse();
            _previousKeyboard = Keyboard.GetState();

            _playerState.MaxHealth = _playerState.NightMaxHealth;
            _playerState.Health = _playerState.MaxHealth;
            _playerHealthBar = new LerpBar(_playerState.Health, _playerState.MaxHealth);

            _district = _playerState.SelectedDistrict;
            _dawnTimer = new DawnTimer(DawnTimer.NightMinutes + _playerState.ArchiveExtraNightMinutes);
            _startFood = _playerState.Food;
            _startPlanks = _playerState.Planks;
            _startScraps = _playerState.Scraps;
            _startHope = _playerState.Hope;
            var roomGenerator = new RoomGenerator(_district, _random, _playerState.Day);
            _map = new NightMap(roomGenerator, _random, MapColumns, MapRows);
            LayoutMapNodes();

            bool finalNight = DayInfo.IsFinalNight(_playerState.Day);
            _throneRoom = finalNight && _district == District.Castle;

            _current = _map.Entrance;
            _current.Visited = true;
            _tokenPosition = _current.Center;
            RefreshVisibility();
            foreach (var node in _map.Nodes) node.UpdateAnimation(10f, false);

            if (_throneRoom) InitializeThrone();
            else _textLog.Push($"{_dawnTimer.ClockLabel}. You slip into the {DistrictInfo.Name(_district)}. The halls twist off into the dark.");

            for (int i = 0; i < EmberCount; i++)
            {
                _embers.Add(SpawnEmber(anywhere: true));
            }

            _headBackButton = new Button(new RectangleF(1000, 30, 240, 56), "Head Back Before Dawn");
            InitializePack();
        }

        private void LayoutMapNodes()
        {
            float pitchX = MapArea.Width / _map.Columns;
            float pitchY = MapArea.Height / _map.Rows;
            foreach (var node in _map.Nodes)
            {
                float cx = MapArea.X + pitchX * (node.Column + 0.5f);
                float cy = MapArea.Y + pitchY * (node.Row + 0.5f);
                node.ScreenBounds = new RectangleF(cx - RoomSize / 2f, cy - RoomSize / 2f, RoomSize, RoomSize);
            }
        }

        public override void Update(GameTime gameTime)
        {
            if (_playerState.IsGameOver)
            {
                RecordNight();
                Game1.EndRun(victory: false);
                return;
            }

            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            var mouse = InputChecker.GetMouse();
            bool clicked = InputChecker.IsNewLeftClick(mouse, _previousMouse);
            var keyboard = Keyboard.GetState();
            var previousKeyboard = _previousKeyboard;
            _previousKeyboard = keyboard;
            bool KeyPressed(Keys key) => keyboard.IsKeyDown(key) && !previousKeyboard.IsKeyDown(key);

            _playerHealthBar.Update(gameTime, _playerState.Health);
            _textLog.Update(gameTime);
            _castEffect.Update(gameTime);
            _playerHitFlash.Update(gameTime);
            _actionLock = Math.Max(0f, _actionLock - dt);
            _diceRollPopup.Update(gameTime);
            _enemyShake.Update(gameTime);
            _playerShake.Update(gameTime);
            UpdateEmbers(dt);
            _stateTime += dt;
            _displayedElapsed = UITheme.MoveTowards(_displayedElapsed, _dawnTimer.MinutesElapsed, 180f * dt);

            if (_collapseTimer >= 0f)
            {
                _collapseTimer -= dt;
                if (_collapseTimer < 0f)
                {
                    if (_collapseEndsRun)
                    {
                        RecordNight();
                        Game1.EndRun(victory: false);
                    }
                    else
                    {
                        GoToDawnReturn();
                    }
                }
                _previousMouse = mouse;
                return;
            }

            if (_pendingPlayerHitDelay >= 0f)
            {
                _pendingPlayerHitDelay -= dt;
                if (_pendingPlayerHitDelay <= 0f)
                {
                    FirePendingPlayerHit();
                }
            }
            if (_activeCombat != null)
            {
                foreach (var enemy in _activeCombat.Enemies)
                {
                    if (_enemyBars.TryGetValue(enemy, out var bar)) bar.Update(gameTime, enemy.Health);
                }
            }

            if (_throneRoom && _state == ExplorationState.Map)
            {
                UpdateThrone(dt, mouse, clicked, KeyPressed);
                _previousMouse = mouse;
                return;
            }

            bool onMap = _state == ExplorationState.Map && !_leaving;
            if (onMap && IsWalking)
            {
                UpdateWalk(dt);
            }

            bool mapInteractive = onMap && !IsWalking;
            bool headBackHovered = mapInteractive && !_packOpen && _headBackButton.Contains(mouse.X, mouse.Y);
            _headBackButton.UpdateAnimation(dt, headBackHovered);
            UpdatePack(dt, mouse, mapInteractive);

            _hoveredNode = null;
            foreach (var node in _map.Nodes)
            {
                bool isUnderMouse = mapInteractive && !_packOpen && node.Discovered && InputChecker.Contains(node.ScreenBounds, mouse.X, mouse.Y);
                if (isUnderMouse) _hoveredNode = node;
                node.UpdateAnimation(dt, isUnderMouse && _reachable.Contains(node));
            }
            _hoverPath = _hoveredNode != null && _reachable.Contains(_hoveredNode)
                ? _map.FindKnownPath(_current, _hoveredNode)
                : null;

            bool inCombat = _state == ExplorationState.Encounter;
            foreach (var button in _combatButtons)
            {
                button.UpdateAnimation(dt, inCombat && !IsActionLocked && button.Contains(mouse.X, mouse.Y));
            }

            bool inSupplies = _state == ExplorationState.Supplies;
            foreach (var button in _suppliesButtons)
            {
                button.UpdateAnimation(dt, inSupplies && button.Contains(mouse.X, mouse.Y));
            }

            bool inEvent = _state == ExplorationState.Event;
            foreach (var button in _eventButtons)
            {
                button.UpdateAnimation(dt, inEvent && button.Contains(mouse.X, mouse.Y));
            }

            if (inCombat && !_leaving && !IsActionLocked && _activeCombat != null && !ScreenTransitions.IsTransitioning)
            {
                HandleCombatKeys(KeyPressed, keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift));
            }

            if (clicked && !_leaving)
            {
                switch (_state)
                {
                    case ExplorationState.Map:
                        if (!IsWalking) HandleMapClick(mouse.X, mouse.Y);
                        break;
                    case ExplorationState.Encounter:
                        if (!IsActionLocked)
                        {
                            HandleCombatClick(mouse.X, mouse.Y);
                        }
                        break;
                    case ExplorationState.Supplies:
                        HandleSuppliesClick(mouse.X, mouse.Y);
                        break;
                    case ExplorationState.Event:
                        HandleEventClick(mouse.X, mouse.Y);
                        break;
                }
            }

            _previousMouse = mouse;
        }

        private void HandleMapClick(int x, int y)
        {
            if (HandlePackClick(x, y)) return;

            if (_headBackButton.Contains(x, y))
            {
                _headBackButton.TriggerPress();
                GoToDawnReturn();
                return;
            }

            if (_hoverPath != null && _hoverPath.Count > 0)
            {
                _walkPath = _hoverPath;
                _walkIndex = 0;
                _hoverPath = null;
            }
        }

        private void UpdateWalk(float dt)
        {
            float budget = WalkSpeed * dt;
            while (_walkPath != null && budget > 0f)
            {
                var target = _walkPath[_walkIndex].Center;
                var toTarget = target - _tokenPosition;
                float distance = toTarget.Length();

                if (distance > budget)
                {
                    _tokenPosition += toTarget / distance * budget;
                    return;
                }

                _tokenPosition = target;
                budget -= distance;
                _cameFrom = _current;
                _current = _walkPath[_walkIndex];
                _walkIndex++;

                if (_walkIndex >= _walkPath.Count)
                {
                    _walkPath = null;
                    ArriveAt(_current);
                }
            }
        }

        private void ArriveAt(MapNode node)
        {
            if (node.Visited)
            {
                RefreshVisibility();
                return;
            }

            node.Visited = true;
            _roomsVisited++;
            _dawnTimer.Spend(RoomEntryMinutes);
            RefreshVisibility();
            StirTheDark();
            WarnOfDawn();

            switch (node.Type)
            {
                case RoomType.Encounter:
                    StartEncounter(node);
                    break;
                case RoomType.Supplies:
                    StartSupplies();
                    break;
                case RoomType.Special:
                    StartEvent();
                    break;
                case RoomType.Hoard:
                    if (node.Enemies != null) StartEncounter(node);
                    else ResolveHoard();
                    break;
                default:
                    ResolveEmpty();
                    break;
            }

            if (_state == ExplorationState.Map && IsNightOver)
            {
                GoToDawnReturn();
            }
        }

        private bool IsNightOver => !_dawnTimer.HasTimeRemaining || _map.Nodes.All(n => n.Visited);

        private void WarnOfDawn()
        {
            if (_warnedOfDawn || !_dawnTimer.HasTimeRemaining || _dawnTimer.MinutesLeft > 60) return;
            _warnedOfDawn = true;
            _textLog.Push($"{_dawnTimer.ClockLabel}. The sky is starting to pale - under an hour until dawn.");
        }

        private void RefreshVisibility()
        {
            foreach (var node in _map.Nodes)
            {
                if (!node.Visited) continue;
                node.Discovered = node.Scouted = true;
                foreach (var link in node.Links)
                {
                    link.Discovered = link.Scouted = true;
                }
            }

            int scoutSteps = 1 + _playerState.ArchiveRevealDepth;
            foreach (var kvp in _map.StepsWithin(_current, scoutSteps + 1))
            {
                kvp.Key.Discovered = true;
                if (kvp.Value <= scoutSteps) kvp.Key.Scouted = true;
            }

            if (_playerState.ArchiveBestiary)
            {
                foreach (var node in _map.Nodes)
                {
                    if (node.Scouted && !node.Visited && node.Type == RoomType.Encounter && node.Enemies == null)
                    {
                        node.Enemies = EnemyRoster.RollEncounter(_district, EnemyStats.TierAt(node.Depth), _playerState.Day, _random);
                    }
                }
            }

            _reachable.Clear();
            foreach (var node in _map.Nodes)
            {
                if (node.Visited)
                {
                    _reachable.Add(node);
                    foreach (var link in node.Links) _reachable.Add(link);
                }
            }
            _reachable.Remove(_current);
        }

        private void StirTheDark()
        {
            if (_random.Next(100) >= RoamChancePercent) return;

            var prowlers = _map.Nodes
                .Where(n => n.Type == RoomType.Encounter && !n.Visited)
                .OrderBy(_ => _random.Next())
                .ToList();

            foreach (var prowler in prowlers)
            {
                var destinations = prowler.Links
                    .Where(l => !l.Visited && l.Type == RoomType.Empty)
                    .ToList();
                if (destinations.Count == 0) continue;

                var destination = destinations[_random.Next(destinations.Count)];
                prowler.Type = RoomType.Empty;
                destination.Type = RoomType.Encounter;
                destination.Enemies = prowler.Enemies;
                prowler.Enemies = null;
                prowler.StirAmount = 1f;
                destination.StirAmount = 1f;

                bool seen = prowler.Scouted || destination.Scouted;
                _textLog.Push(seen
                    ? $"Something shuffles between the rooms {DirectionFrom(_current, destination)} of you..."
                    : "Somewhere in the dark, something shifts its weight.");
                return;
            }
        }

        private static string DirectionFrom(MapNode from, MapNode to)
        {
            int dx = to.Column - from.Column;
            int dy = to.Row - from.Row;
            if (Math.Abs(dx) >= Math.Abs(dy)) return dx >= 0 ? "east" : "west";
            return dy >= 0 ? "south" : "north";
        }

        private void GoToDawnReturn()
        {
            if (_leaving) return;
            _leaving = true;
            RecordNight();
            ScreenManager.ReplaceScreen(new Dawn(Game, _playerState, _roomsCleared, _roomsVisited), ScreenTransitions.FadeTransition(GraphicsDevice));
        }

        private void RecordNight()
        {
            if (_nightRecorded) return;
            _nightRecorded = true;
            _playerState.Nights.Add(new NightRecord
            {
                Day = _playerState.Day,
                District = _district,
                HopeAtDusk = _startHope,
                HopeAtDawn = _playerState.Hope,
                Food = Math.Max(0, _playerState.Food - _startFood),
                Planks = Math.Max(0, _playerState.Planks - _startPlanks),
                Scraps = Math.Max(0, _playerState.Scraps - _startScraps),
                EnemiesDefeated = _enemiesDefeatedTonight,
                RoomsExplored = _throneRoom ? 1 : _map.Nodes.Count(n => n.Visited && n.Type != RoomType.Entrance),
                KnockedOut = _knockedOutTonight
            });
        }

        private Ember SpawnEmber(bool anywhere)
        {
            var ember = new Ember
            {
                Position = new Vector2(
                    (float)_random.NextDouble() * 1280f,
                    anywhere ? (float)_random.NextDouble() * 720f : 720f + (float)_random.NextDouble() * 30f),
                Velocity = new Vector2(
                    ((float)_random.NextDouble() - 0.5f) * 14f,
                    -10f - (float)_random.NextDouble() * 22f),
                MaxLife = 5f + (float)_random.NextDouble() * 7f,
                Size = 1.5f + (float)_random.NextDouble() * 2.5f,
                Wobble = (float)_random.NextDouble() * MathF.PI * 2f
            };
            ember.Life = anywhere ? (float)_random.NextDouble() * ember.MaxLife : 0f;
            return ember;
        }

        private void UpdateEmbers(float dt)
        {
            for (int i = 0; i < _embers.Count; i++)
            {
                var ember = _embers[i];
                ember.Life += dt;
                ember.Wobble += dt * 1.3f;
                ember.Position += (ember.Velocity + new Vector2(MathF.Sin(ember.Wobble) * 8f, 0f)) * dt;
                if (ember.IsDead || ember.Position.Y < -20f)
                {
                    _embers[i] = SpawnEmber(anywhere: false);
                }
            }
        }

        private static readonly string[] QuietHallLines =
        {
            "Dust and broken furniture. Nothing moves.",
            "A cold draft from somewhere further in.",
            "Old scratches on the wall, counting days.",
            "Rainwater drips through a hole in the ceiling.",
            "A child's shoe, alone in the middle of the floor.",
            "Your lantern throws long shadows down the hall."
        };

        private void ResolveEmpty()
        {
            string line = QuietHallLines[_random.Next(QuietHallLines.Length)];
            if (_random.Next(100) < 20)
            {
                int amount = _random.Next(1, 3) + DistrictInfo.Corruption(_district) - 1;
                var (food, planks, scraps) = DistrictInfo.SpecialtyAmount(_district, amount);
                _playerState.AddResources(food, planks, scraps);
                line += $" You pocket {amount} {DistrictInfo.SpecialtyMaterial(_district)} from the rubble.";
                Sound.Play(Sfx.ItemLoot);
            }

            _textLog.Push(line);
            _roomsCleared++;
            _state = ExplorationState.Map;
        }

        private void ResolveHoard()
        {
            var (food, planks, scraps) = DistrictInfo.Yield(_district).Roll(_random, times: 3);
            _playerState.AddResources(food, planks, scraps);
            Sound.Play(Sfx.ItemLoot);

            string line = $"The Hoard! {food} Food, {planks} Planks and {scraps} Scraps, stacked in the dark.";
            if (_random.Next(100) < 50)
            {
                var remedy = _random.Next(3) == 0 ? Item.Elixir() : Item.Tonic();
                line += $" And a {remedy.Name} - {_playerState.GainItemAtNight(remedy)}.";
            }

            _playerState.ChangeHope(PlayerState.HoardHope);
            line += $" Hope +{PlayerState.HoardHope}.";

            _textLog.Push(line);
            _roomsCleared++;
            _state = ExplorationState.Map;
        }
    }
}
