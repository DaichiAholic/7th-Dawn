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
        Supplies
    }

    public enum CombatMenu
    {
        TopLevel,
        Skills,
        Items
    }

    /// <summary>
    /// Phase 2: Night Scavenging. The ruins are a small maze you explore room by room: winding
    /// corridors, corners, dead ends and a few loops, hidden under fog until your lantern (or
    /// the Archive's scouting) reaches them. The night runs on a clock from dusk (8 PM) to
    /// dawn (6 AM): entering a new room takes 30 minutes and every supply-cache choice has its
    /// own time cost, while walking back through explored rooms and fighting are free.
    /// Creatures roam between unexplored rooms as the night goes on. Being knocked out in a
    /// fight ends the night early and costs half of tonight's haul.
    /// </summary>
    public partial class NightScavengingScreen : GameScreen, IGameplayScreen
    {
        private Game1 Game1 => (Game1)Game;
        private readonly PlayerState _playerState;
        private readonly Random _random = new Random();
        private District _district;

        // ---- Maze layout ----
        private const int MapColumns = 8;
        private const int MapRows = 4;
        private static readonly RectangleF MapArea = new RectangleF(40, 224, 1200, 376);
        private const float RoomSize = 64f;

        private const int RoamChancePercent = 35; // per new room entered

        // ---- The night clock ----
        private const int RoomEntryMinutes = 30;
        private DawnTimer _dawnTimer;
        // The clock hands sweep to the new time instead of jumping - rendering only.
        private float _displayedElapsed;
        private bool _warnedOfDawn;
        private NightMap _map;
        private MapNode _current;
        private int _roomsVisited;
        private bool _leaving;

        // Rooms the player can click right now: every explored room, plus unexplored rooms
        // that border one (the frontier). Recomputed whenever the player arrives somewhere.
        private readonly HashSet<MapNode> _reachable = new HashSet<MapNode>();

        // ---- Walking ----
        // Moving is animated: the lantern token walks the corridors room by room instead
        // of teleporting, and input on the map is paused until it arrives.
        private const float WalkSpeed = 420f; // pixels per second
        private List<MapNode> _walkPath;
        private int _walkIndex;
        // The room you were in before this one - where fleeing takes you back to.
        private MapNode _cameFrom;
        private Vector2 _tokenPosition;
        private bool IsWalking => _walkPath != null;

        private MapNode _hoveredNode;
        private List<MapNode> _hoverPath;

        // ---- Atmosphere ----
        private const int EmberCount = 46;
        private readonly List<Ember> _embers = new List<Ember>();

        private ExplorationState _state = ExplorationState.Map;
        private readonly TextLog _textLog = new TextLog();
        private int _roomsCleared;
        private int _combatTurn;

        private Button _headBackButton;
        private MouseState _previousMouse;

        // Animated health bars - see LerpBar.cs
        private LerpBar _playerHealthBar;
        private LerpBar _enemyHealthBar;


        // ---- Knocked out ----
        // Losing a fight no longer just burns clock time, so it ends the night instead: a short
        // beat to read what happened, then home, carrying half of what you found.
        private const int KnockoutHopeLoss = 5;
        private const float CollapseDuration = 2.6f;
        private float _collapseTimer = -1f;
        private string _collapseText = "";
        private int _startFood, _startPlanks, _startScraps;


        public NightScavengingScreen(Game game, PlayerState playerState) : base(game)
        {
            _playerState = playerState;
        }

        public override void Initialize()
        {
            base.Initialize();

            // Seeds with the real current mouse state instead of a blank default, so a click
            // still held down from the previous screen doesn't read as a brand-new click here.
            _previousMouse = InputChecker.GetMouse();

            _playerState.Health = _playerState.MaxHealth; // rested at the base - full health tonight
            _playerHealthBar = new LerpBar(_playerState.Health, _playerState.MaxHealth);

            _district = _playerState.SelectedDistrict;
            _dawnTimer = new DawnTimer();
            _startFood = _playerState.Food;
            _startPlanks = _playerState.Planks;
            _startScraps = _playerState.Scraps;
            var roomGenerator = new RoomGenerator(_district, _random);
            _map = new NightMap(roomGenerator, _random, MapColumns, MapRows);
            LayoutMapNodes();

            _current = _map.Entrance;
            _current.Visited = true;
            _tokenPosition = _current.Center;
            RefreshVisibility();
            // Everything visible at the start is already faded in - only rooms found later
            // should drift out of the fog.
            foreach (var node in _map.Nodes) node.UpdateAnimation(10f, false);

            _textLog.Push($"{_dawnTimer.ClockLabel}. You slip into the {DistrictInfo.Name(_district)}. The halls twist off into the dark.");

            for (int i = 0; i < EmberCount; i++)
            {
                _embers.Add(SpawnEmber(anywhere: true));
            }

            _headBackButton = new Button(new RectangleF(1000, 30, 240, 56), "Head Back Before Dawn");
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
                Game1.EndRun(victory: false);
                return;
            }

            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            var mouse = InputChecker.GetMouse();
            bool clicked = InputChecker.IsNewLeftClick(mouse, _previousMouse);

            // Bars and one-shot effects animate every frame regardless of state, so they keep
            // catching up (or finish playing out) even right after combat ends.
            _playerHealthBar.Update(gameTime, _playerState.Health);
            _textLog.Update(gameTime);
            _castEffect.Update(gameTime);
            _playerHitFlash.Update(gameTime);
            _actionLock = Math.Max(0f, _actionLock - dt);
            _diceRollPopup.Update(gameTime);
            _enemyShake.Update(gameTime);
            _playerShake.Update(gameTime);
            UpdateEmbers(dt);
            // ~2 real seconds to sweep a full hour, so a room's 30 minutes reads as a quick tick forward.
            _displayedElapsed = UITheme.MoveTowards(_displayedElapsed, _dawnTimer.MinutesElapsed, 180f * dt);

            if (_collapseTimer >= 0f)
            {
                _collapseTimer -= dt;
                if (_collapseTimer < 0f) GoToDawnReturn();
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
                _enemyHealthBar?.Update(gameTime, _activeCombat.Enemy.Health);
            }

            bool onMap = _state == ExplorationState.Map && !_leaving;
            if (onMap && IsWalking)
            {
                UpdateWalk(dt);
            }

            // Every interactive element eases its own hover/press animation forward each
            // frame (even while its screen isn't the active one - that's harmless, it just
            // idles at rest) so nothing snaps between visual states.
            bool mapInteractive = onMap && !IsWalking;
            bool headBackHovered = mapInteractive && _headBackButton.Contains(mouse.X, mouse.Y);
            _headBackButton.UpdateAnimation(dt, headBackHovered);

            _hoveredNode = null;
            foreach (var node in _map.Nodes)
            {
                bool isUnderMouse = mapInteractive && node.Discovered && InputChecker.Contains(node.ScreenBounds, mouse.X, mouse.Y);
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
                }
            }

            _previousMouse = mouse;
        }

        // ---------- Map ----------

        private void HandleMapClick(int x, int y)
        {
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
                // Walking back through explored ground is free - nothing to resolve.
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
                    ResolveSpecial();
                    break;
                case RoomType.Hoard:
                    ResolveHoard();
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

        /// <summary>One log line the first time the clock drops under an hour.</summary>
        private void WarnOfDawn()
        {
            if (_warnedOfDawn || !_dawnTimer.HasTimeRemaining || _dawnTimer.MinutesLeft > 60) return;
            _warnedOfDawn = true;
            _textLog.Push($"{_dawnTimer.ClockLabel}. The sky is starting to pale - under an hour until dawn.");
        }

        /// <summary>What the player can see from where they stand. Explored rooms show their
        /// doorways (neighbors' types are known), the lantern plus the Archive's scouting
        /// reveals rooms further along the corridors, and one step beyond that shows only a
        /// silhouette.</summary>
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

        /// <summary>The ruins aren't static: after each new room, a creature may slip from its
        /// room into a neighboring empty hall. You only see it happen if you can see either
        /// room - otherwise you just hear it.</summary>
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
                destination.Enemy = prowler.Enemy;
                prowler.Enemy = null;
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
            ScreenManager.ReplaceScreen(new Dawn(Game, _playerState, _roomsCleared, _roomsVisited), ScreenTransitions.FadeTransition(GraphicsDevice));
        }

        // ---------- Atmosphere ----------

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

        // ---------- Special / Empty / Hoard ----------

        private void ResolveSpecial()
        {
            if (_random.Next(100) >= DistrictInfo.WeaponFindChance(_district))
            {
                var (food, planks, scraps) = DistrictInfo.Yield(_district).Roll(_random);
                _playerState.AddResources(food, planks, scraps);
                _textLog.Push($"A moment of quiet beauty in the dark, and a forgotten cache. {MaterialYield.Describe(food, planks, scraps)}.");
            }
            else
            {
                _textLog.Push(FindWeapon(out _));
            }

            _roomsCleared++;
            _state = ExplorationState.Map;
        }

        /// <summary>Rolls a weapon from this district's loot table. New weapons go on the rack;
        /// one you already own is broken down for Scraps instead of cluttering it.
        /// Returns the full log line; `brief` is a shorter version to tack onto another line.</summary>
        private string FindWeapon(out string brief)
        {
            var loot = DistrictInfo.WeaponLoot(_district);
            var weapon = loot[_random.Next(loot.Length)]();

            if (_playerState.Inventory.Any(owned => owned.Name == weapon.Name))
            {
                _playerState.AddResources(scraps: weapon.SalvageValue);
                brief = $"And a spare {weapon.Name}, broken down for {weapon.SalvageValue} Scraps.";
                return $"You find another {weapon.Name} and break it down for {weapon.SalvageValue} Scraps.";
            }

            _playerState.Inventory.Add(weapon);
            brief = $"And a {weapon.Name}.";
            return $"You find a {weapon.Name} ({weapon.StatLabel}) left behind by someone else.";
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
                // A stray bit of whatever this district is rich in - more in the deeper districts.
                int amount = _random.Next(1, 3) + DistrictInfo.Corruption(_district) - 1;
                var (food, planks, scraps) = DistrictInfo.SpecialtyAmount(_district, amount);
                _playerState.AddResources(food, planks, scraps);
                line += $" You pocket {amount} {DistrictInfo.SpecialtyMaterial(_district)} from the rubble.";
            }

            _textLog.Push(line);
            _roomsCleared++;
            _state = ExplorationState.Map;
        }

        private void ResolveHoard()
        {
            // Three finds' worth of this district's materials.
            var (food, planks, scraps) = DistrictInfo.Yield(_district).Roll(_random, times: 3);
            _playerState.AddResources(food, planks, scraps);

            string line = $"The Hoard! {food} Food, {planks} Planks and {scraps} Scraps, stacked in the dark.";
            if (_random.Next(100) < 50)
            {
                FindWeapon(out string brief);
                line += $" {brief}";
            }

            _textLog.Push(line);
            _roomsCleared++;
            _state = ExplorationState.Map;
        }

    }
}
