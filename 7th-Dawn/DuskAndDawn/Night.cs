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

    /// <summary>
    /// A bar whose displayed value smoothly chases a real target value over time, instead of
    /// snapping to it instantly. Use this for anything you want to feel "juicy" when it
    /// changes (health bars) - keep plain instant bars (like the dawn clock) for things that
    /// should read as exact/immediate instead.
    ///
    /// This only affects rendering - it never touches the real value (PlayerState.Health,
    /// Enemy.Health, etc.), which stays instant for game logic. Call Update() once per frame
    /// with the current real value, then read Ratio when drawing.
    /// </summary>
    public class LerpBar
    {
        private const float CatchUpSpeed = 4f; // higher = the bar catches up to the real value faster

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

        /// <summary>0-1 fill ratio, ready to multiply against a bar's max width.</summary>
        public float Ratio => MaxValue <= 0 ? 0f : MathHelper.Clamp(DisplayedValue / MaxValue, 0f, 1f);
    }

    /// <summary>
    /// A 2-line action log: the newest message shows bright and steady; the previous one
    /// drifts upward and dims as the new one arrives, then vanishes outright the moment a
    /// third message pushes it out - there's only ever one "fading" slot, no stacking history.
    /// </summary>
    public class TextLog
    {
        private const float RiseSpeed = 18f;  // pixels/second the fading line drifts upward
        private const float FadeSpeed = 0.6f; // alpha lost per second
        private const float LineHeight = 24f; // vertical spacing between wrapped lines

        private string _current = "";
        private string _fading = "";
        private float _fadeAlpha;
        private float _fadeOffset;

        public void Push(string message)
        {
            if (string.IsNullOrEmpty(message)) return;

            _fading = _current;   // whatever was current becomes the dimming line...
            _fadeAlpha = 1f;
            _fadeOffset = 0f;
            _current = message;   // ...and the new message takes the bright slot
        }

        public void Update(GameTime gameTime)
        {
            if (_fadeAlpha <= 0f) return;

            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            _fadeAlpha = Math.Max(0f, _fadeAlpha - FadeSpeed * dt);
            _fadeOffset += RiseSpeed * dt;
        }

        /// <summary>Draws the current (bright) line(s) at basePosition, and the fading (dim,
        /// rising) line(s) above them while still visible. Long messages wrap to fit within
        /// maxWidth instead of running off the edge of the screen.</summary>
        public void Draw(SpriteBatch spriteBatch, SpriteFont font, Vector2 basePosition, float maxWidth)
        {
            var currentLines = string.IsNullOrEmpty(_current) ? null : WrapText(font, _current, maxWidth);

            if (_fadeAlpha > 0f && !string.IsNullOrEmpty(_fading))
            {
                var fadingLines = WrapText(font, _fading, maxWidth);
                float blockHeight = fadingLines.Count * LineHeight;
                float startY = basePosition.Y - _fadeOffset - blockHeight;
                for (int i = 0; i < fadingLines.Count; i++)
                {
                    spriteBatch.DrawString(font, fadingLines[i], new Vector2(basePosition.X, startY + i * LineHeight), Color.Gray * _fadeAlpha);
                }
            }

            if (currentLines != null)
            {
                for (int i = 0; i < currentLines.Count; i++)
                {
                    UITheme.DrawTextWithShadow(spriteBatch, font, currentLines[i], basePosition + new Vector2(0, LineHeight * i), Color.White);
                }
            }
        }

        // Splits on spaces and greedily packs words onto each line up to maxWidth, so a long
        // combat message wraps instead of running past the edge of the screen.
        private static List<string> WrapText(SpriteFont font, string text, float maxWidth)
        {
            var words = text.Split(' ');
            var lines = new List<string>();
            var line = new StringBuilder();

            foreach (var word in words)
            {
                string candidate = line.Length == 0 ? word : line.ToString() + " " + word;
                if (line.Length > 0 && font.MeasureString(candidate).X > maxWidth)
                {
                    lines.Add(line.ToString());
                    line.Clear();
                    line.Append(word);
                }
                else
                {
                    if (line.Length > 0) line.Append(' ');
                    line.Append(word);
                }
            }

            if (line.Length > 0) lines.Add(line.ToString());
            if (lines.Count == 0) lines.Add("");
            return lines;
        }
    }

    /// <summary>
    /// A one-shot frame animation played from a horizontal spritesheet of square frames
    /// (Attack.png, Skill.png, Attacked.png - 7 frames of 64x64 each). The frame size is read
    /// from the texture height, so a sheet with a different frame count just works.
    /// Drawn centered on a point, at a whole-number scale with point sampling so the pixel
    /// art stays crisp. One instance is reused per "slot" (enemy hit, player hit, cast) -
    /// calling Play again restarts it, so there's never more than one of a given effect at once.
    /// </summary>
    public class SpriteEffect
    {
        private Texture2D _texture;
        private int _frameSize;
        private int _frameCount;
        private Vector2 _center;
        private int _scale;
        private float _duration;
        private float _delay;          // seconds to wait before the first frame shows
        private float _elapsed = -1f;  // negative = not playing

        public bool IsPlaying => _elapsed >= 0f;

        /// <summary>Cuts the animation off immediately (used when a fight ends).</summary>
        public void Stop()
        {
            _elapsed = -1f;
            _delay = 0f;
        }

        /// <param name="duration">Total time for all frames, in seconds.</param>
        /// <param name="scale">Whole-number pixel scale (2 = 64px frames drawn at 128px).</param>
        /// <param name="delay">Optional wait before it starts - used to land the enemy's
        /// hit reaction a beat after the slash begins instead of on top of it.</param>
        public const int FrameSize = 64;

        public void Play(Texture2D texture, Vector2 center, float duration, int scale, float delay = 0f)
        {
            if (texture == null) return;

            _texture = texture;
            _frameSize = FrameSize;
            _frameCount = Math.Max(1, texture.Width / FrameSize);
            _center = center;
            _duration = duration;
            _scale = scale;
            _delay = delay;
            _elapsed = 0f;
        }

        public void Update(GameTime gameTime)
        {
            if (_elapsed < 0f) return;

            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (_delay > 0f)
            {
                _delay -= dt;
                return;
            }

            _elapsed += dt;
            if (_elapsed >= _duration) _elapsed = -1f;
        }

        public void Draw(SpriteBatch spriteBatch)
        {
            if (!IsPlaying || _delay > 0f || _texture == null) return;

            int frame = Math.Min(_frameCount - 1, (int)(_elapsed / _duration * _frameCount));
            var source = new Rectangle(frame * _frameSize, 0, _frameSize, _frameSize);
            UITheme.DrawPixelSprite(spriteBatch, _texture, source, _center, _scale);
        }
    }

    /// <summary>
    /// A brief "die icon + number" callout showing the result of a damage roll, drifting
    /// upward and fading out near where the roll happened.
    /// </summary>
    public class DiceRollPopup
    {
        private const float Duration = 1.1f;

        private Texture2D _texture;
        private int _value;
        private Vector2 _position;
        private float _elapsed = -1f;

        public void Play(Texture2D texture, int value, Vector2 position)
        {
            _texture = texture;
            _value = value;
            _position = position;
            _elapsed = 0f;
        }

        public void Update(GameTime gameTime)
        {
            if (_elapsed < 0f) return;
            _elapsed += (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (_elapsed >= Duration) _elapsed = -1f;
        }

        public void Draw(SpriteBatch spriteBatch, SpriteFont font)
        {
            if (_elapsed < 0f || _texture == null) return;

            float t = _elapsed / Duration;
            float rise = t * 26f;
            float alpha = t < 0.55f ? 1f : 1f - (t - 0.55f) / 0.45f;
            var drawPos = _position - new Vector2(0, rise);
            var color = Color.White * MathHelper.Clamp(alpha, 0f, 1f);

            const float iconScale = 0.2f; // 320px source -> ~64px on screen, sized to leave room for the number on top
            var origin = new Vector2(_texture.Width / 2f, _texture.Height / 2f);
            spriteBatch.Draw(_texture, drawPos, null, color, 0f, origin, iconScale, SpriteEffects.None, 0f);

            // Number stamped centered on the die face rather than off to the side.
            string text = _value.ToString();
            const float textScale = 1.2f;
            var textSize = font.MeasureString(text) * textScale;
            var textPos = drawPos - textSize / 2f;
            UITheme.DrawTextWithShadow(spriteBatch, font, text, textPos, color, textScale);
        }
    }

    /// <summary>
    /// A brief camera-shake-style jitter, for punctuating a hit with more than just a flash.
    /// Reads as a little jolt on top of whatever else is playing at that position, and decays
    /// to nothing over its short duration.
    /// </summary>
    public class ImpactShake
    {
        private const float Duration = 0.25f;
        private const float Magnitude = 6f;

        private static readonly Random RandomSource = new Random();
        private float _elapsed = -1f;

        public void Play() => _elapsed = 0f;

        public void Update(GameTime gameTime)
        {
            if (_elapsed < 0f) return;
            _elapsed += (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (_elapsed >= Duration) _elapsed = -1f;
        }

        /// <summary>A small random offset that decays to zero - add this to a draw position
        /// while the shake is playing; it's Vector2.Zero at rest, so it's always safe to add.</summary>
        public Vector2 Offset
        {
            get
            {
                if (_elapsed < 0f) return Vector2.Zero;
                float decay = 1f - (_elapsed / Duration);
                return new Vector2(
                    (float)(RandomSource.NextDouble() * 2f - 1f) * Magnitude * decay,
                    (float)(RandomSource.NextDouble() * 2f - 1f) * Magnitude * decay);
            }
        }
    }

    /// <summary>
    /// A drifting spark of ash/ember for the night backdrop. Purely decorative - spawned
    /// and recycled by NightScavengingScreen so the ruins never sit perfectly still.
    /// </summary>
    public class Ember
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public float Life;
        public float MaxLife;
        public float Size;
        public float Wobble;

        public bool IsDead => Life >= MaxLife;

        /// <summary>0 -> 1 -> 0 over its lifetime, so embers fade in and out instead of popping.</summary>
        public float Alpha
        {
            get
            {
                float t = MaxLife <= 0f ? 1f : Life / MaxLife;
                return MathHelper.Clamp(MathF.Sin(t * MathF.PI), 0f, 1f);
            }
        }
    }

    /// <summary>
    /// Phase 2: Night Scavenging. The ruins are a small maze you explore room by room: winding
    /// corridors, corners, dead ends and a few loops, hidden under fog until your lantern (or
    /// the Archive's scouting) reaches them. Entering a new room costs a tick of the dawn
    /// clock; walking back through rooms you've already explored is free. Creatures roam
    /// between unexplored rooms as the night goes on. Combat and supply caches work as before.
    /// </summary>
    public class NightScavengingScreen : GameScreen
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

        private DawnTimer _dawnTimer;
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

        // Combat sub-state
        private CombatEncounter _activeCombat;
        private CombatMenu _combatMenu = CombatMenu.TopLevel;
        private readonly List<Button> _combatButtons = new List<Button>();

        // Items menu groups identical items into one button ("Bandage x2"), so the button
        // label no longer matches an item name - this holds the real name for each button.
        private readonly List<string> _itemButtonNames = new List<string>();

        // Supplies sub-state
        private List<ChoiceOption> _suppliesOptions;
        private readonly List<Button> _suppliesButtons = new List<Button>();

        // ---- Combat sprite effects ----
        // All three sheets are 7 frames of 64x64. Timings are per whole animation.
        // Both the player's slash and the enemy's hit on the player play big, in the middle
        // of the screen, at the same scale so they read as equal-weight beats.
        private const float AttackAnimDuration = 0.35f;   // quick slash
        private const float SkillAnimDuration = 0.5f;     // heavier, reads as a bigger move
        private const float AttackedAnimDuration = 0.42f;
        private const int EffectScale = 4;                // native size: 64px per frame on screen

        // Combat actions are locked until the current exchange has finished playing (our
        // animation, then the enemy's hit), plus a short breather - so attacks can't be spammed.
        private const float ActionGap = 0.15f;
        private const float MinActionLock = 0.3f;
        private float _actionLock;
        private bool IsActionLocked => _actionLock > 0f;
        private static readonly Vector2 ScreenCenter = new Vector2(640, 360);

        private readonly SpriteEffect _castEffect = new SpriteEffect();     // Attack / Skill - when we hit the enemy
        private readonly SpriteEffect _playerHitFlash = new SpriteEffect(); // Attacked - when the enemy hits us
        private readonly DiceRollPopup _diceRollPopup = new DiceRollPopup();
        private readonly ImpactShake _enemyShake = new ImpactShake();  // punches up our own hit landing
        private readonly ImpactShake _playerShake = new ImpactShake(); // punches up the enemy's counter-hit landing

        // The enemy's counter-attack is resolved instantly under the hood (same method call
        // as our own action), but showing both hit-flashes at once reads as simultaneous
        // rather than two separate blows. This delays the player-hit-flash, its log line, and
        // its shake so the enemy's counter visibly lands a beat after ours instead of on top
        // of it.
        private const float EnemyCounterDelay = 0.45f;
        private float _pendingPlayerHitDelay = -1f;
        private string _pendingEnemyReplyText = "";

        private const float PlayerHpBarScale = 0.25f;
        private const float EnemyHpBarScale = 0.3125f;
        private static readonly Vector2 PlayerHpBarPosition = new Vector2(40, 122);
        private static readonly Vector2 EnemyHpBarPosition = new Vector2(480, 225);

        // HUD cards
        private static readonly RectangleF StatusCard = new RectangleF(18, 12, 400, 198);
        private static readonly RectangleF InfoCard = new RectangleF(434, 12, 548, 112);

        public NightScavengingScreen(Game game, PlayerState playerState) : base(game)
        {
            _playerState = playerState;
        }

        public override void Initialize()
        {
            base.Initialize();

            // Seeds with the real current mouse state instead of a blank default, so a click
            // still held down from the previous screen doesn't read as a brand-new click here.
            _previousMouse = Mouse.GetState();

            _playerState.Health = _playerState.MaxHealth; // rested at the base - full health tonight
            _playerHealthBar = new LerpBar(_playerState.Health, _playerState.MaxHealth);

            _district = _playerState.SelectedDistrict;
            _dawnTimer = new DawnTimer(startingBudget: 10);
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

            _textLog.Push($"You slip into the {DistrictInfo.Name(_district)}. The halls twist off into the dark.");

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
                ScreenManager.ReplaceScreen(new GameOverScreen(Game), ScreenTransitions.FadeTransition(GraphicsDevice));
                return;
            }

            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            var mouse = Mouse.GetState();
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
            _dawnTimer.SpendOnRoomEntry();
            RefreshVisibility();
            StirTheDark();

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

        // ---------- Encounter (combat) ----------

        private void StartEncounter(int depth)
        {
            // The maze runs deeper than the old 6-layer map; halve the step count so enemy
            // strength lands in the same range it was tuned for.
            int tier = Math.Min(6, depth / 2);
            var enemy = new Enemy(
                $"{DistrictInfo.RandomEnemyName(_district, _random)} (Depth {depth})",
                maxHealth: 30 + tier * 3 + DistrictInfo.EnemyHealthBonus(_district),
                attackPower: 6 + tier + DistrictInfo.EnemyAttackBonus(_district),
                corruption: DistrictInfo.Corruption(_district));
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
                    {
                        // Grouped by name, and a bit more compact than the other menus, since
                        // the Infirmary and Kitchen can stock up to six different items.
                        const float itemHeight = 48, itemGap = 8;
                        _itemButtonNames.Clear();
                        foreach (var group in _playerState.Items.GroupBy(it => it.Name))
                        {
                            int count = group.Count();
                            string itemLabel = count > 1 ? $"{group.Key} x{count}" : group.Key;
                            _combatButtons.Add(new Button(new RectangleF(x, y, width, itemHeight), itemLabel));
                            _itemButtonNames.Add(group.Key);
                            y += itemHeight + itemGap;
                        }
                        _combatButtons.Add(new Button(new RectangleF(x, y, width, itemHeight), "Back"));
                        break;
                    }
            }
        }

        private void HandleCombatClick(int x, int y)
        {
            foreach (var button in _combatButtons)
            {
                if (!button.Contains(x, y)) continue;

                button.TriggerPress();
                var label = button.Label;

                if (_combatMenu == CombatMenu.TopLevel)
                {
                    switch (label)
                    {
                        case "Attack":
                            _textLog.Push(_activeCombat.Attack());
                            _combatTurn++;
                            TriggerCombatEffects(Game1.AttackTexture);
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
                    TriggerCombatEffects(Game1.SkillTexture);
                }
                else if (_combatMenu == CombatMenu.Items)
                {
                    if (label == "Back") { _combatMenu = CombatMenu.TopLevel; LayoutCombatButtons(); return; }
                    int itemIndex = _combatButtons.IndexOf(button);
                    string itemName = itemIndex >= 0 && itemIndex < _itemButtonNames.Count ? _itemButtonNames[itemIndex] : label;
                    var item = _playerState.Items.Find(it => it.Name == itemName);
                    if (item != null)
                    {
                        _textLog.Push(_activeCombat.UseItem(item));
                        _combatTurn++;
                        TriggerCombatEffects(Game1.AttackTexture);
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

        /// <summary>Plays the delayed "enemy hits us" beat: Attacked animation, shake, and the
        /// enemy's log line.</summary>
        private void FirePendingPlayerHit()
        {
            _pendingPlayerHitDelay = -1f;
            _playerHitFlash.Play(Game1.AttackedTexture, ScreenCenter, AttackedAnimDuration, EffectScale);
            _playerShake.Play();
            if (!string.IsNullOrEmpty(_pendingEnemyReplyText))
            {
                _textLog.Push(_pendingEnemyReplyText);
                _pendingEnemyReplyText = "";
            }
        }

        /// <summary>Fires the dice-roll popup and the enemy/player hit-flashes based on what
        /// CombatEncounter's last action actually did - castTexture (Attack.png or
        /// Skill.png) only plays if that action was a damage roll (it's ignored for
        /// Guard, items, etc. since LastRollWasAttack stays false for those).</summary>
        private void TriggerCombatEffects(Texture2D castTexture)
        {
            // If the last enemy hit is still waiting on its delay when a new action comes in
            // (clicking faster than EnemyCounterDelay), play it now. Before, the new action
            // simply reset the timer, so with quick clicks the Attacked animation never played.
            if (_pendingPlayerHitDelay >= 0f)
            {
                FirePendingPlayerHit();
            }

            var portraitCenter = new Vector2(630, 440);

            if (_activeCombat.LastRollWasAttack)
            {
                float castDuration = castTexture == Game1.SkillTexture ? SkillAnimDuration : AttackAnimDuration;
                _castEffect.Play(castTexture, ScreenCenter, castDuration, EffectScale);
                _enemyShake.Play();
                _diceRollPopup.Play(Game1.DiceTexture, _activeCombat.LastPlayerRoll, portraitCenter + new Vector2(-70, -90));
            }

            if (_activeCombat.PlayerWasHit)
            {
                // Scheduled rather than played immediately - see EnemyCounterDelay above.
                _pendingPlayerHitDelay = EnemyCounterDelay;
                _pendingEnemyReplyText = _activeCombat.LastEnemyReplyText;
            }
            else if (!string.IsNullOrEmpty(_activeCombat.LastEnemyReplyText))
            {
                // No counter-attack to wait for (the enemy's already defeated) - nothing to
                // stagger against, so the resolution line shows right away.
                _textLog.Push(_activeCombat.LastEnemyReplyText);
            }

            // Lock the buttons until this whole exchange has played out.
            float ourPart = _activeCombat.LastRollWasAttack
                ? (castTexture == Game1.SkillTexture ? SkillAnimDuration : AttackAnimDuration)
                : 0f;
            float enemyPart = _activeCombat.PlayerWasHit ? EnemyCounterDelay + AttackedAnimDuration : 0f;
            _actionLock = Math.Max(MinActionLock, Math.Max(ourPart, enemyPart) + ActionGap);
        }

        private void EndCombat(bool fled)
        {
            if (!fled && _activeCombat.PlayerWon)
            {
                _roomsCleared++;
            }

            // The fight is over - cut any animation still playing and drop the queued enemy
            // hit (its log line still shows, so the last blow isn't lost from the log).
            _castEffect.Stop();
            _playerHitFlash.Stop();
            if (_pendingPlayerHitDelay >= 0f && !string.IsNullOrEmpty(_pendingEnemyReplyText))
            {
                _textLog.Push(_pendingEnemyReplyText);
            }
            _pendingPlayerHitDelay = -1f;
            _pendingEnemyReplyText = "";
            _actionLock = 0f;

            _activeCombat = null;
            _combatMenu = CombatMenu.TopLevel;
            _state = ExplorationState.Map;

            if (IsNightOver)
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
            // Richer districts add a flat bonus to every resource found.
            int bonus = DistrictInfo.LootBonus(_district);

            return new List<ChoiceOption>
            {
                new ChoiceOption("Search quickly", "Fast and safe - a modest find.", (state, rng) =>
                {
                    state.AddResources(food: rng.Next(1, 4) + bonus, scraps: rng.Next(1, 3) + bonus);
                    return "You grab what's in easy reach.";
                }),

                new ChoiceOption("Search thoroughly", "Slower, better odds - but noise draws attention.", (state, rng) =>
                {
                    state.AddResources(food: rng.Next(3, 7) + bonus, planks: rng.Next(2, 5) + bonus, scraps: rng.Next(2, 5) + bonus);
                    if (rng.Next(100) < 30)
                    {
                        state.Health = Math.Max(1, state.Health - 8);
                        return "You find a good haul, but the noise draws something - it clips you on the way out.";
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

                _suppliesButtons[i].TriggerPress();
                _textLog.Push(_suppliesOptions[i].Resolve(_playerState, _random));
                _roomsCleared++;
                _state = ExplorationState.Map;

                if (IsNightOver)
                {
                    GoToDawnReturn();
                }
                return;
            }
        }

        // ---------- Special / Empty / Hoard ----------

        private void ResolveSpecial()
        {
            if (_random.Next(100) < 50)
            {
                int bonus = DistrictInfo.LootBonus(_district);
                int food = _random.Next(2, 5) + bonus;
                int planks = _random.Next(1, 4) + bonus;
                _playerState.AddResources(food: food, planks: planks);
                _textLog.Push($"A moment of quiet beauty in the dark. You gather {food} Food and {planks} Planks.");
            }
            else
            {
                var weapon = Weapon.LootPool[_random.Next(Weapon.LootPool.Length)]();
                _playerState.Inventory.Add(weapon);
                _textLog.Push($"You find a {weapon.Name} ({weapon.DiceLabel}) left behind by someone else.");
            }

            _roomsCleared++;
            _state = ExplorationState.Map;
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
            if (_random.Next(100) < 25)
            {
                int scraps = 1 + DistrictInfo.LootBonus(_district);
                _playerState.AddResources(scraps: scraps);
                line += $" You pocket {scraps} Scraps from the rubble.";
            }

            _textLog.Push(line);
            _roomsCleared++;
            _state = ExplorationState.Map;
        }

        private void ResolveHoard()
        {
            int bonus = DistrictInfo.LootBonus(_district) * 2;
            int food = _random.Next(4, 9) + bonus;
            int planks = _random.Next(3, 7) + bonus;
            int scraps = _random.Next(3, 7) + bonus;
            _playerState.AddResources(food: food, planks: planks, scraps: scraps);

            string line = $"The Hoard! {food} Food, {planks} Planks and {scraps} Scraps, stacked in the dark.";
            if (_random.Next(100) < 50)
            {
                var weapon = Weapon.LootPool[_random.Next(Weapon.LootPool.Length)]();
                _playerState.Inventory.Add(weapon);
                line += $" And a {weapon.Name}.";
            }

            _textLog.Push(line);
            _roomsCleared++;
            _state = ExplorationState.Map;
        }

        // ---------- Draw ----------

        public override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(new Color(8, 8, 14)); // night: dark and safe by design

            var spriteBatch = Game1.SpriteBatch;
            var font = Game1.Font;
            float totalSeconds = (float)gameTime.TotalGameTime.TotalSeconds;
            spriteBatch.Begin();

            // Subtle gradient instead of a flat fill - just enough depth to read as a night
            // sky rather than a solid color swatch, while staying dark and calm by design.
            UITheme.FillGradientRect(spriteBatch, new RectangleF(0, 0, 1280, 720), new Color(14, 14, 24), new Color(4, 4, 8), 10);
            DrawEmbers(spriteBatch);

            DrawHud(spriteBatch, font, totalSeconds);

            switch (_state)
            {
                case ExplorationState.Map:
                    DrawMaze(spriteBatch, font, totalSeconds);
                    break;
                case ExplorationState.Encounter:
                    DrawCombat(spriteBatch, font, totalSeconds);
                    DrawMapFragment(spriteBatch, font);
                    break;
                case ExplorationState.Supplies:
                    DrawSupplies(spriteBatch, font, totalSeconds);
                    DrawMapFragment(spriteBatch, font);
                    break;
            }

            // Combat animations go on top of everything, centered on the screen - only while
            // a fight is actually on (EndCombat also stops them).
            if (_state == ExplorationState.Encounter)
            {
                _castEffect.Draw(spriteBatch);
                _playerHitFlash.Draw(spriteBatch);
            }

            spriteBatch.End();
        }

        private void DrawEmbers(SpriteBatch spriteBatch)
        {
            foreach (var ember in _embers)
            {
                float alpha = ember.Alpha;
                UITheme.DrawGlow(spriteBatch, ember.Position, ember.Size * 3f, new Color(255, 120, 50) * (0.25f * alpha));
                UITheme.FillCircle(spriteBatch, ember.Position, ember.Size * 0.6f, new Color(255, 190, 120) * (0.7f * alpha));
            }
        }

        // ---------- HUD ----------

        private void DrawHud(SpriteBatch spriteBatch, SpriteFont font, float totalSeconds)
        {
            // Two frosted cards instead of loose text on the background: your own status on
            // the left, where you are / what you're carrying in the middle.
            UITheme.DrawPanel(spriteBatch, StatusCard, new Color(26, 24, 38) * 0.92f, new Color(16, 15, 24) * 0.92f, new Color(80, 72, 100), 1.5f, 14f, shadowStrength: 0.6f);
            UITheme.DrawPanel(spriteBatch, InfoCard, new Color(26, 24, 38) * 0.92f, new Color(16, 15, 24) * 0.92f, new Color(80, 72, 100), 1.5f, 14f, shadowStrength: 0.6f);

            DrawClock(spriteBatch, font, totalSeconds);
            string weaponLine = $"Weapon: {_playerState.EquippedWeapon.Name} ({_playerState.EquippedWeapon.DiceLabel})";
            UITheme.DrawTextWithShadow(spriteBatch, font, weaponLine, new Vector2(40, 100), Color.LightGray);
            // 1x icon just after the weapon line - small, but crisp at native size.
            float weaponLineWidth = font.MeasureString(weaponLine).X;
            UITheme.DrawPixelIcon(spriteBatch, Game1.GetWeaponIcon(_playerState.EquippedWeapon), new Vector2(40 + weaponLineWidth + 8, 94), 1);
            DrawPlayerHealthBar(spriteBatch, font);

            // District + corruption, with a pip per corruption tier that glows like embers.
            var infoX = InfoCard.X + 18;
            UITheme.DrawTextWithShadow(spriteBatch, font, DistrictInfo.Name(_district), new Vector2(infoX, InfoCard.Y + 12), new Color(255, 180, 120));
            float pipX = infoX + font.MeasureString(DistrictInfo.Name(_district)).X + 20;
            int corruption = DistrictInfo.Corruption(_district);
            for (int i = 0; i < 3; i++)
            {
                var pip = new Vector2(pipX + i * 20, InfoCard.Y + 24);
                if (i < corruption)
                {
                    float glow = UITheme.PulseSine(totalSeconds + i * 0.6f, 2.2f);
                    UITheme.DrawGlow(spriteBatch, pip, 14f, new Color(255, 110, 40) * (0.35f + glow * 0.3f));
                    UITheme.FillCircle(spriteBatch, pip, 6f, new Color(255, 150, 70));
                }
                else
                {
                    UITheme.FillCircle(spriteBatch, pip, 6f, new Color(60, 54, 70));
                }
            }
            UITheme.DrawTextWithShadow(spriteBatch, font, "Corruption", new Vector2(pipX + 64, InfoCard.Y + 14), new Color(200, 170, 150), 0.8f);

            // Tonight's haul so far - icons from the existing resource art.
            float resourceY = InfoCard.Y + 48;
            float x = infoX;
            x = DrawResourceCounter(spriteBatch, font, Game1.BreadTexture, _playerState.Food, "Food", x, resourceY);
            x = DrawResourceCounter(spriteBatch, font, Game1.PlanksTexture, _playerState.Planks, "Planks", x, resourceY);
            DrawResourceCounter(spriteBatch, font, Game1.ScrapsTexture, _playerState.Scraps, "Scraps", x, resourceY);

            int explored = _map.Nodes.Count(n => n.Visited && n.Type != RoomType.Entrance);
            UITheme.DrawTextWithShadow(spriteBatch, font,
                $"Rooms explored {explored}/{_map.Nodes.Count - 1}     Deepest {DeepestVisited()}/{_map.MaxDepth}",
                new Vector2(infoX, InfoCard.Y + 84), new Color(190, 185, 210), 0.8f);
        }

        private int DeepestVisited() => _map.Nodes.Where(n => n.Visited).Select(n => n.Depth).DefaultIfEmpty(0).Max();

        private float DrawResourceCounter(SpriteBatch spriteBatch, SpriteFont font, Texture2D icon, int amount, string name, float x, float y)
        {
            const float iconSize = 28f;
            if (icon != null)
            {
                spriteBatch.Draw(icon, new Rectangle((int)x, (int)y, (int)iconSize, (int)iconSize), Color.White);
            }
            string text = $"{amount} {name}";
            UITheme.DrawTextWithShadow(spriteBatch, font, text, new Vector2(x + iconSize + 6, y + 2), Color.White, 0.9f);
            return x + iconSize + 6 + font.MeasureString(text).X * 0.9f + 26f;
        }

        private void DrawClock(SpriteBatch spriteBatch, SpriteFont font, float totalSeconds)
        {
            UITheme.DrawTextWithShadow(spriteBatch, font, $"Time until dawn: {_dawnTimer.RemainingBudget}/{_dawnTimer.MaxBudget}", new Vector2(40, 28), Color.White);

            var clockMax = new RectangleF(40, 60, 300, 20);
            float ratio = _dawnTimer.RemainingBudget / (float)_dawnTimer.MaxBudget;
            var clockFill = new RectangleF(40, 60, 300 * ratio, 20);

            UITheme.DrawSoftShadow(spriteBatch, clockMax, 10f, 0.4f);
            UITheme.FillRoundedRectGradient(spriteBatch, clockMax, Color.Black * 0.6f, Color.Black * 0.4f, 10f, 6);

            if (clockFill.Width > 1f)
            {
                Color top = new Color(255, 210, 140);
                Color bottom = new Color(225, 160, 80);
                if (ratio <= 0.2f)
                {
                    // Slow warning pulse once the dawn clock is nearly spent - cosmetic only,
                    // doesn't change the actual budget or thresholds.
                    float pulse = UITheme.PulseSine(totalSeconds, 5f);
                    top = Color.Lerp(top, Color.White, pulse * 0.3f);
                }
                UITheme.FillRoundedRectGradient(spriteBatch, clockFill, top, bottom, 10f, 6);
            }

            // Tick marks, one per unit of the budget, so "one room = one notch" is readable.
            for (int i = 1; i < _dawnTimer.MaxBudget; i++)
            {
                float tickX = clockMax.X + clockMax.Width * i / _dawnTimer.MaxBudget;
                spriteBatch.DrawLine(new Vector2(tickX, clockMax.Y + 4), new Vector2(tickX, clockMax.Y + clockMax.Height - 4), Color.Black * 0.35f, 2f);
            }

            UITheme.DrawRoundedRectBorder(spriteBatch, clockMax, Color.White * 0.8f, 2f, 10f);
        }

        // Hp_bar.png is a single "full" bar sprite (heart + red track), not a separate
        // empty/full pair. To show partial health without a second asset, this draws a dim
        // full-width copy as the track, then the same sprite - cropped from its left edge to
        // just the current-health fraction - at full brightness on top. Both draws share the
        // same position/scale, so the crop lines up exactly with the track underneath, and the
        // bar visually drains from the right while the heart and left cap stay put.
        private void DrawHpBarSprite(SpriteBatch spriteBatch, Vector2 position, float scale, float ratio)
        {
            var texture = Game1.HpBarTexture;
            if (texture == null) return;

            var fullSource = new Rectangle(0, 0, texture.Width, texture.Height);
            spriteBatch.Draw(texture, position, fullSource, Color.White * 0.35f, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);

            if (ratio > 0.01f)
            {
                int fillWidth = Math.Max(1, (int)(texture.Width * MathHelper.Clamp(ratio, 0f, 1f)));
                var fillSource = new Rectangle(0, 0, fillWidth, texture.Height);
                spriteBatch.Draw(texture, position, fillSource, Color.White, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            }
        }

        private void DrawPlayerHealthBar(SpriteBatch spriteBatch, SpriteFont font)
        {
            var shakenPosition = PlayerHpBarPosition + _playerShake.Offset;
            DrawHpBarSprite(spriteBatch, shakenPosition, PlayerHpBarScale, _playerHealthBar.Ratio);

            var texture = Game1.HpBarTexture;
            float dispW = texture != null ? texture.Width * PlayerHpBarScale : 0f;
            float dispH = texture != null ? texture.Height * PlayerHpBarScale : 0f;

            var label = $"{_playerState.Health}/{_playerState.MaxHealth}";
            var labelPos = new Vector2(shakenPosition.X + dispW + 14, shakenPosition.Y + dispH / 2f - 10);
            UITheme.DrawTextWithShadow(spriteBatch, font, label, labelPos, Color.White);
        }

        // ---------- Maze ----------

        private static Color RoomTop(RoomType type) => type switch
        {
            RoomType.Supplies => new Color(52, 116, 78),
            RoomType.Encounter => new Color(138, 40, 40),
            RoomType.Special => new Color(104, 56, 140),
            RoomType.Hoard => new Color(170, 128, 40),
            RoomType.Entrance => new Color(70, 84, 110),
            _ => new Color(58, 58, 70)
        };

        private static Color RoomBottom(RoomType type) => type switch
        {
            RoomType.Supplies => new Color(28, 70, 46),
            RoomType.Encounter => new Color(88, 22, 22),
            RoomType.Special => new Color(64, 30, 92),
            RoomType.Hoard => new Color(110, 78, 20),
            RoomType.Entrance => new Color(40, 50, 70),
            _ => new Color(36, 36, 46)
        };

        private void DrawMaze(SpriteBatch spriteBatch, SpriteFont font, float totalSeconds)
        {
            // The ruins' footprint: a dark slab the corridors are cut into.
            var slab = new RectangleF(MapArea.X - 12, MapArea.Y - 10, MapArea.Width + 24, MapArea.Height + 20);
            UITheme.DrawPanel(spriteBatch, slab, new Color(18, 17, 27), new Color(9, 9, 14), new Color(52, 48, 66), 1.5f, 18f, shadowStrength: 0.8f);

            // A far-off glimmer where the Hoard lies, even through the fog - something to aim for.
            if (!_map.Hoard.Visited)
            {
                float glimmer = UITheme.PulseSine(totalSeconds, 1.4f);
                UITheme.DrawGlow(spriteBatch, _map.Hoard.Center, 70f + glimmer * 12f, new Color(255, 200, 90) * (0.16f + glimmer * 0.10f));
            }

            // Faint marks where the fog still hides the grid - hints at how much is left
            // out there without giving away the layout (rubble cells get a mark too).
            float pitchX = MapArea.Width / _map.Columns;
            float pitchY = MapArea.Height / _map.Rows;
            for (int col = 0; col < _map.Columns; col++)
            {
                for (int row = 0; row < _map.Rows; row++)
                {
                    var cell = _map.At(col, row);
                    if (cell != null && cell.RevealAmount > 0.5f) continue;
                    var mark = new Vector2(MapArea.X + pitchX * (col + 0.5f), MapArea.Y + pitchY * (row + 0.5f));
                    UITheme.FillCircle(spriteBatch, mark, 2.5f, new Color(90, 86, 110) * 0.35f);
                }
            }

            // Lantern light: a warm, gently flickering pool around you, drawn under the
            // corridors so the stone near you reads as lit.
            float flicker = 1f + 0.04f * MathF.Sin(totalSeconds * 7.3f) + 0.03f * MathF.Sin(totalSeconds * 13.1f);
            UITheme.DrawGlow(spriteBatch, _tokenPosition, 260f * flicker, new Color(255, 160, 80) * 0.18f);
            UITheme.DrawGlow(spriteBatch, _tokenPosition, 120f * flicker, new Color(255, 180, 100) * 0.16f);

            DrawCorridors(spriteBatch, totalSeconds);

            foreach (var node in _map.Nodes)
            {
                if (node.RevealAmount <= 0.01f) continue;
                DrawRoom(spriteBatch, font, node, totalSeconds);
            }

            DrawToken(spriteBatch, totalSeconds);

            DrawBottomBar(spriteBatch, font);
            DrawStyledButton(spriteBatch, font, _headBackButton, new Color(120, 78, 64), new Color(84, 52, 44));

            // Last, so it's never covered by the bottom bar.
            if (_hoveredNode != null)
            {
                DrawRoomTooltip(spriteBatch, font, _hoveredNode);
            }
        }

        private void DrawCorridors(SpriteBatch spriteBatch, float totalSeconds)
        {
            // Which corridors the hovered route would walk, so they can be lit up.
            var routeEdges = new HashSet<(MapNode, MapNode)>();
            if (_hoverPath != null)
            {
                var previous = _current;
                foreach (var step in _hoverPath)
                {
                    routeEdges.Add((previous, step));
                    routeEdges.Add((step, previous));
                    previous = step;
                }
            }

            foreach (var a in _map.Nodes)
            {
                foreach (var b in a.Links)
                {
                    // Each corridor once.
                    if (a.Column * 100 + a.Row > b.Column * 100 + b.Row) continue;
                    if (!a.Discovered && !b.Discovered) continue;

                    var from = a.Center;
                    var to = b.Center;
                    float alpha;

                    if (a.Discovered && b.Discovered)
                    {
                        alpha = Math.Min(a.RevealAmount, b.RevealAmount);
                    }
                    else
                    {
                        // Only one end is known: draw a stub trailing off into the fog, so
                        // unexplored exits read as "this way goes somewhere".
                        if (!a.Discovered) (from, to) = (to, from);
                        to = Vector2.Lerp(from, to, 0.62f);
                        alpha = (a.Discovered ? a.RevealAmount : b.RevealAmount) * 0.55f;
                    }

                    if (alpha <= 0.01f) continue;

                    bool walked = a.Visited && b.Visited;
                    bool onRoute = routeEdges.Contains((a, b));

                    spriteBatch.DrawLine(from, to, new Color(4, 4, 8) * alpha, 20f);
                    Color floor = walked ? new Color(112, 84, 56) : new Color(46, 44, 58);
                    spriteBatch.DrawLine(from, to, floor * alpha, 11f);

                    if (onRoute)
                    {
                        float pulse = UITheme.PulseSine(totalSeconds, 6f);
                        spriteBatch.DrawLine(from, to, new Color(255, 150, 70) * (0.55f + pulse * 0.35f), 5f);
                    }
                    else if (walked)
                    {
                        spriteBatch.DrawLine(from, to, new Color(190, 140, 90) * 0.35f, 3f);
                    }
                }
            }
        }

        private void DrawRoom(SpriteBatch spriteBatch, SpriteFont font, MapNode node, float totalSeconds)
        {
            float alpha = node.RevealAmount;
            float hover = node.HoverAmount;
            bool reachable = _reachable.Contains(node);
            bool frontier = reachable && !node.Visited;

            // Hovered rooms lift slightly.
            float grow = hover * 5f;
            var bounds = new RectangleF(node.ScreenBounds.X - grow, node.ScreenBounds.Y - grow - hover * 2f, node.ScreenBounds.Width + grow * 2f, node.ScreenBounds.Height + grow * 2f);
            var center = new Vector2(bounds.X + bounds.Width / 2f, bounds.Y + bounds.Height / 2f);

            Color top, bottom;
            if (!node.Scouted)
            {
                top = new Color(30, 30, 42);
                bottom = new Color(20, 20, 30);
            }
            else
            {
                top = RoomTop(node.Type);
                bottom = RoomBottom(node.Type);
                if (node.Visited && node != _current)
                {
                    // Explored: drained of color, so the map shows at a glance what's left.
                    top = Color.Lerp(top, new Color(52, 52, 58), 0.7f);
                    bottom = Color.Lerp(bottom, new Color(34, 34, 40), 0.7f);
                }
            }

            if (hover > 0f)
            {
                top = UITheme.Brighten(top, hover * 0.22f);
                bottom = UITheme.Brighten(bottom, hover * 0.22f);
            }

            // Frontier rooms breathe with a faint ember edge so the next choices stand out.
            Color border = new Color(120, 116, 140) * 0.6f;
            float borderThickness = 1.5f;
            if (frontier)
            {
                float breathe = UITheme.PulseSine(totalSeconds + node.Phase, 2.4f);
                border = Color.Lerp(new Color(200, 140, 90) * 0.6f, new Color(255, 170, 90), breathe * 0.5f);
                borderThickness = 2f;
            }
            if (node == _current)
            {
                border = new Color(255, 225, 170);
                borderThickness = 2.5f;
            }
            border = Color.Lerp(border, new Color(255, 130, 60), hover);
            borderThickness = MathHelper.Lerp(borderThickness, 3.5f, hover);

            if (node.Scouted && node.Type == RoomType.Hoard && !node.Visited)
            {
                float glint = UITheme.PulseSine(totalSeconds + node.Phase, 2f);
                UITheme.DrawGlow(spriteBatch, center, 70f, new Color(255, 200, 90) * (0.25f + glint * 0.2f) * alpha);
            }
            if (node.Scouted && node.Type == RoomType.Encounter && !node.Visited)
            {
                float menace = UITheme.PulseSine(totalSeconds + node.Phase, 1.8f);
                UITheme.DrawGlow(spriteBatch, center, 58f, new Color(220, 40, 30) * (0.22f + menace * 0.2f) * alpha);
            }

            UITheme.DrawPanel(spriteBatch, bounds, top * alpha, bottom * alpha, border * alpha, borderThickness, 12f, shadowStrength: 0.5f * alpha);

            float iconAlpha = alpha * (node.Visited && node != _current ? 0.45f : 1f);
            if (!node.Scouted)
            {
                const string unknown = "?";
                var size = font.MeasureString(unknown) * 1.3f;
                UITheme.DrawTextWithShadow(spriteBatch, font, unknown, center - size / 2f, new Color(150, 150, 175) * alpha, 1.3f);
            }
            else
            {
                DrawRoomIcon(spriteBatch, node, center, iconAlpha, totalSeconds);
            }

            // Ripple when a creature moves in or out.
            if (node.StirAmount > 0.01f)
            {
                float t = 1f - node.StirAmount;
                float radius = RoomSize * 0.55f + t * 46f;
                spriteBatch.DrawCircle(center, radius, 28, new Color(255, 90, 60) * (node.StirAmount * alpha), 2.5f);
                spriteBatch.DrawCircle(center, radius * 0.75f, 28, new Color(255, 90, 60) * (node.StirAmount * 0.5f * alpha), 1.5f);
            }
        }

        /// <summary>A tiny primitive-drawn glyph per room type - no extra art needed.</summary>
        private void DrawRoomIcon(SpriteBatch spriteBatch, MapNode node, Vector2 c, float alpha, float totalSeconds)
        {
            switch (node.Type)
            {
                case RoomType.Supplies:
                    {
                        // A crate: planked box with a cross brace.
                        var box = new RectangleF(c.X - 15, c.Y - 12, 30, 24);
                        UITheme.FillRoundedRect(spriteBatch, box, new Color(170, 124, 70) * alpha, 3f);
                        spriteBatch.DrawRectangle(box, new Color(90, 60, 30) * alpha, 2f);
                        spriteBatch.DrawLine(new Vector2(box.Left + 2, box.Top + 2), new Vector2(box.Right - 2, box.Bottom - 2), new Color(110, 76, 40) * alpha, 2f);
                        spriteBatch.DrawLine(new Vector2(box.Right - 2, box.Top + 2), new Vector2(box.Left + 2, box.Bottom - 2), new Color(110, 76, 40) * alpha, 2f);
                        break;
                    }

                case RoomType.Encounter:
                    {
                        // Two eyes in the dark that blink now and then.
                        float cycle = (totalSeconds + node.Phase * 1.7f) % 4.2f;
                        bool blinking = cycle < 0.14f;
                        var eyeColor = new Color(255, 80, 60) * alpha;
                        foreach (float offset in new[] { -9f, 9f })
                        {
                            var eye = c + new Vector2(offset, 0f);
                            UITheme.DrawGlow(spriteBatch, eye, 16f, new Color(255, 60, 40) * (0.9f * alpha));
                            if (blinking)
                                spriteBatch.FillRectangle(new RectangleF(eye.X - 5, eye.Y - 1, 10, 2), eyeColor);
                            else
                                UITheme.FillCircle(spriteBatch, eye, 4.5f, eyeColor);
                        }
                        break;
                    }

                case RoomType.Special:
                    {
                        // A four-point sparkle that slowly breathes.
                        float s = 12f + UITheme.PulseSine(totalSeconds + node.Phase, 2.5f) * 4f;
                        var sparkle = new Color(230, 200, 255) * alpha;
                        UITheme.DrawGlow(spriteBatch, c, 28f, new Color(170, 110, 255) * (0.8f * alpha));
                        spriteBatch.DrawLine(c - new Vector2(0, s), c + new Vector2(0, s), sparkle, 2.5f);
                        spriteBatch.DrawLine(c - new Vector2(s, 0), c + new Vector2(s, 0), sparkle, 2.5f);
                        UITheme.FillCircle(spriteBatch, c, 4f, Color.White * alpha);
                        break;
                    }

                case RoomType.Hoard:
                    {
                        // A treasure chest.
                        var chest = new RectangleF(c.X - 16, c.Y - 8, 32, 20);
                        var lid = new RectangleF(c.X - 16, c.Y - 16, 32, 10);
                        UITheme.FillRoundedRect(spriteBatch, lid, new Color(150, 96, 40) * alpha, 5f);
                        UITheme.FillRoundedRect(spriteBatch, chest, new Color(128, 80, 32) * alpha, 3f);
                        spriteBatch.FillRectangle(new RectangleF(c.X - 16, c.Y - 8, 32, 3), new Color(240, 200, 90) * alpha);
                        spriteBatch.FillRectangle(new RectangleF(c.X - 3, c.Y - 6, 6, 8), new Color(255, 220, 110) * alpha);
                        break;
                    }

                case RoomType.Entrance:
                    {
                        // An arched doorway.
                        var door = new RectangleF(c.X - 10, c.Y - 6, 20, 20);
                        UITheme.FillCircle(spriteBatch, c + new Vector2(0, -6), 10f, new Color(12, 12, 18) * alpha);
                        spriteBatch.FillRectangle(door, new Color(12, 12, 18) * alpha);
                        spriteBatch.DrawLine(new Vector2(door.Left - 2, door.Bottom), new Vector2(door.Right + 2, door.Bottom), new Color(160, 170, 200) * alpha, 2f);
                        break;
                    }

                default:
                    {
                        // Quiet hall: a few scattered stones.
                        var stone = new Color(120, 120, 135) * alpha;
                        UITheme.FillCircle(spriteBatch, c + new Vector2(-9, 5), 3.5f, stone);
                        UITheme.FillCircle(spriteBatch, c + new Vector2(4, 8), 2.5f, stone);
                        UITheme.FillCircle(spriteBatch, c + new Vector2(8, -6), 3f, stone);
                        break;
                    }
            }
        }

        private void DrawToken(SpriteBatch spriteBatch, float totalSeconds)
        {
            // You: a small lantern-bearer bobbing as it walks (or idles).
            float bob = MathF.Sin(totalSeconds * (IsWalking ? 12f : 3f)) * (IsWalking ? 3f : 1.5f);
            // Stands in the room's lower-right corner so the room's icon stays readable.
            var pos = _tokenPosition + new Vector2(RoomSize * 0.38f, RoomSize * 0.38f + bob);

            UITheme.FillCircle(spriteBatch, pos + new Vector2(0, 10), 10f, Color.Black * 0.35f);
            UITheme.DrawGlow(spriteBatch, pos, 38f, new Color(255, 190, 110) * 0.85f);
            UITheme.FillCircle(spriteBatch, pos, 10f, new Color(255, 236, 200));
            UITheme.FillCircle(spriteBatch, pos, 7f, new Color(255, 190, 110));

            float flame = 3.5f + MathF.Sin(totalSeconds * 17f) * 0.8f;
            UITheme.FillCircle(spriteBatch, pos + new Vector2(0, -1), flame, new Color(255, 250, 225));
        }

        private void DrawRoomTooltip(SpriteBatch spriteBatch, SpriteFont font, MapNode node)
        {
            string title = node.Scouted ? RoomTypeInfo.Name(node.Type) : "Unknown room";
            string detail = node.Scouted ? RoomTypeInfo.Description(node.Type) : "Too dark to make out from here.";

            string action;
            Color actionColor;
            if (node == _current)
            {
                action = "You are here.";
                actionColor = new Color(255, 225, 170);
            }
            else if (!_reachable.Contains(node))
            {
                action = "No known way there yet.";
                actionColor = new Color(160, 160, 180);
            }
            else if (node.Visited)
            {
                int steps = _hoverPath?.Count ?? 0;
                action = $"Walk back ({steps} step{(steps == 1 ? "" : "s")}) - free";
                actionColor = new Color(170, 220, 170);
            }
            else
            {
                action = "Enter - costs 1 tick";
                actionColor = new Color(255, 180, 110);
            }

            string footer = $"Depth {node.Depth}";
            if (node.Scouted) footer += $"   {node.Links.Count} way{(node.Links.Count == 1 ? "" : "s")} out";
            if (node.Scouted && node.IsDeadEnd && node != _map.Entrance) footer += "  (dead end)";

            const float width = 320f, height = 112f;
            var anchor = node.ScreenBounds;
            float x = anchor.X + anchor.Width + 14;
            if (x + width > 1260) x = anchor.X - width - 14;
            float y = MathHelper.Clamp(anchor.Y - 20, MapArea.Y - 4, 700 - height);
            var box = new RectangleF(x, y, width, height);

            UITheme.DrawPanel(spriteBatch, box, new Color(34, 30, 46), new Color(20, 18, 28), new Color(150, 120, 90), 1.5f, 10f, shadowStrength: 0.8f);
            UITheme.DrawTextWithShadow(spriteBatch, font, title, new Vector2(box.X + 12, box.Y + 10), Color.White);
            UITheme.DrawTextWithShadow(spriteBatch, font, detail, new Vector2(box.X + 12, box.Y + 38), new Color(200, 196, 214), 0.72f);
            UITheme.DrawTextWithShadow(spriteBatch, font, action, new Vector2(box.X + 12, box.Y + 60), actionColor, 0.85f);
            UITheme.DrawTextWithShadow(spriteBatch, font, footer, new Vector2(box.X + 12, box.Y + 86), new Color(150, 146, 168), 0.72f);
        }

        private void DrawBottomBar(SpriteBatch spriteBatch, SpriteFont font)
        {
            // Journal: what just happened.
            var journal = new RectangleF(40, 616, 836, 88);
            UITheme.DrawPanel(spriteBatch, journal, new Color(26, 24, 38) * 0.92f, new Color(16, 15, 24) * 0.92f, new Color(80, 72, 100), 1.5f, 12f, shadowStrength: 0.5f);
            _textLog.Draw(spriteBatch, font, new Vector2(journal.X + 16, journal.Y + 56), maxWidth: journal.Width - 32f);

            // Legend.
            var legend = new RectangleF(892, 616, 348, 88);
            UITheme.DrawPanel(spriteBatch, legend, new Color(26, 24, 38) * 0.92f, new Color(16, 15, 24) * 0.92f, new Color(80, 72, 100), 1.5f, 12f, shadowStrength: 0.5f);
            var entries = new (RoomType type, string label)[]
            {
                (RoomType.Supplies, "Supplies"), (RoomType.Encounter, "Enemy"), (RoomType.Special, "Strange"),
                (RoomType.Hoard, "Hoard"), (RoomType.Empty, "Quiet"), (RoomType.Entrance, "Entrance")
            };
            for (int i = 0; i < entries.Length; i++)
            {
                float ex = legend.X + 16 + (i % 3) * 112;
                float ey = legend.Y + 16 + (i / 3) * 32;
                var swatch = new RectangleF(ex, ey + 2, 18, 18);
                UITheme.FillRoundedRectGradient(spriteBatch, swatch, RoomTop(entries[i].type), RoomBottom(entries[i].type), 4f, 4);
                UITheme.DrawTextWithShadow(spriteBatch, font, entries[i].label, new Vector2(ex + 26, ey), new Color(210, 206, 225), 0.8f);
            }
        }

        /// <summary>A shrunk-down copy of tonight's maze, shown while you're busy in a room
        /// (combat or a supply cache) so you never lose track of where you are.</summary>
        private void DrawMapFragment(SpriteBatch spriteBatch, SpriteFont font)
        {
            var box = new RectangleF(40, 604, 232, 100);
            UITheme.DrawPanel(spriteBatch, box, new Color(62, 50, 32), new Color(42, 34, 20), new Color(150, 120, 70), 2f, 12f, shadowStrength: 0.5f);
            UITheme.DrawTextWithShadow(spriteBatch, font, $"Tonight's map  -  Depth {_current.Depth}", new Vector2(box.X + 10, box.Y + 6), new Color(225, 205, 165), 0.75f);

            var area = new RectangleF(box.X + 10, box.Y + 30, box.Width - 20, box.Height - 38);
            float cellW = area.Width / _map.Columns;
            float cellH = area.Height / _map.Rows;
            Vector2 MiniCenter(MapNode n) => new Vector2(area.X + cellW * (n.Column + 0.5f), area.Y + cellH * (n.Row + 0.5f));

            foreach (var a in _map.Nodes)
            {
                if (!a.Discovered) continue;
                foreach (var b in a.Links)
                {
                    if (!b.Discovered) continue;
                    spriteBatch.DrawLine(MiniCenter(a), MiniCenter(b), new Color(120, 96, 60), 2f);
                }
            }

            foreach (var node in _map.Nodes)
            {
                if (!node.Discovered) continue;
                var c = MiniCenter(node);
                Color color = !node.Scouted ? new Color(90, 80, 70)
                    : node.Visited ? new Color(150, 130, 100)
                    : RoomTop(node.Type);
                spriteBatch.FillRectangle(new RectangleF(c.X - 3.5f, c.Y - 3.5f, 7, 7), color);
            }

            var here = MiniCenter(_current);
            UITheme.DrawGlow(spriteBatch, here, 10f, new Color(255, 220, 150) * 0.9f);
            UITheme.FillCircle(spriteBatch, here, 3.5f, Color.White);
        }

        private void DrawCombat(SpriteBatch spriteBatch, SpriteFont font, float totalSeconds)
        {
            UITheme.DrawTextWithShadow(spriteBatch, font, _activeCombat.Enemy.Name, new Vector2(480, 195), Color.White);
            UITheme.DrawTextWithShadow(spriteBatch, font, $"Turn {_combatTurn + 1}", new Vector2(1000, 195), Color.LightGray);
            UITheme.DrawTextWithShadow(spriteBatch, font, $"Rerolls: {_activeCombat.RerollsLeft}", new Vector2(1000, 225), new Color(200, 210, 255), 0.85f);

            var shakeOffset = _enemyShake.Offset;

            var enemyBarPosition = EnemyHpBarPosition + shakeOffset;
            DrawHpBarSprite(spriteBatch, enemyBarPosition, EnemyHpBarScale, _enemyHealthBar.Ratio);
            var enemyTexture = Game1.HpBarTexture;
            float enemyDispW = enemyTexture != null ? enemyTexture.Width * EnemyHpBarScale : 0f;
            float enemyDispH = enemyTexture != null ? enemyTexture.Height * EnemyHpBarScale : 0f;
            var enemyLabel = $"{_activeCombat.Enemy.Health}/{_activeCombat.Enemy.MaxHealth}";
            UITheme.DrawTextWithShadow(spriteBatch, font, enemyLabel, new Vector2(enemyBarPosition.X + enemyDispW + 14, enemyBarPosition.Y + enemyDispH / 2f - 10), Color.White);

            // Portrait placeholder - swap for real enemy art once it exists. The slow ember
            // pulse on its border stands in for the corruption-glow visual language used
            // elsewhere for enemy readability.
            var portrait = new RectangleF(480 + shakeOffset.X, 335 + shakeOffset.Y, 300, 210);
            float glow = UITheme.PulseSine(totalSeconds, 2.5f);
            Color emberBorder = Color.Lerp(new Color(150, 45, 40), new Color(255, 130, 60), glow * 0.5f);
            UITheme.DrawPanel(spriteBatch, portrait, new Color(45, 26, 30), new Color(28, 16, 19), emberBorder, 3f, 14f, shadowStrength: 0.6f);

            // Until there's enemy art, the same blinking eyes the map uses for enemy rooms
            // stare out of the portrait, bigger.
            var eyesCenter = new Vector2(portrait.X + portrait.Width / 2f, portrait.Y + portrait.Height / 2f - 10);
            bool blinking = totalSeconds % 3.6f < 0.15f;
            foreach (float offset in new[] { -34f, 34f })
            {
                var eye = eyesCenter + new Vector2(offset, 0);
                UITheme.DrawGlow(spriteBatch, eye, 56f, new Color(255, 60, 40) * (0.6f + glow * 0.3f));
                if (blinking)
                    spriteBatch.FillRectangle(new RectangleF(eye.X - 14, eye.Y - 2, 28, 4), new Color(255, 90, 60));
                else
                    UITheme.FillCircle(spriteBatch, eye, 12f, new Color(255, 90, 60));
            }

            // Roll readout over the portrait. The slash / hit animations are drawn last in
            // Draw(), centered on the screen, so they sit on top of everything.
            _diceRollPopup.Draw(spriteBatch, font);

            _textLog.Draw(spriteBatch, font, new Vector2(480, 575), maxWidth: 760f);

            foreach (var button in _combatButtons)
            {
                // Dimmed while locked, so it's clear the next action isn't ready yet.
                if (IsActionLocked)
                {
                    DrawStyledButton(spriteBatch, font, button, new Color(40, 40, 50), new Color(30, 30, 38));
                }
                else
                {
                    DrawStyledButton(spriteBatch, font, button, new Color(64, 64, 88), new Color(44, 44, 64));
                }
            }
        }

        private void DrawSupplies(SpriteBatch spriteBatch, SpriteFont font, float totalSeconds)
        {
            UITheme.DrawTextWithShadow(spriteBatch, font, "You find a supply cache.", new Vector2(60, 220), Color.White);
            UITheme.DrawTextWithShadow(spriteBatch, font, "What do you do?", new Vector2(60, 250), Color.LightGray);

            var lootBox = new RectangleF(60, 300, 260, 260);
            UITheme.DrawPanel(spriteBatch, lootBox, new Color(58, 50, 30), new Color(38, 32, 18), new Color(150, 118, 64), 3f, 14f, shadowStrength: 0.6f);

            // What might be inside, gently bobbing in a warm glow.
            var lootCenter = new Vector2(lootBox.X + lootBox.Width / 2f, lootBox.Y + lootBox.Height / 2f);
            UITheme.DrawGlow(spriteBatch, lootCenter, 130f, new Color(255, 190, 110) * 0.18f);
            var icons = new[] { Game1.BreadTexture, Game1.PlanksTexture, Game1.ScrapsTexture };
            var offsets = new[] { new Vector2(-55, -40), new Vector2(55, -40), new Vector2(0, 50) };
            for (int i = 0; i < icons.Length; i++)
            {
                if (icons[i] == null) continue;
                float bob = MathF.Sin(totalSeconds * 2f + i * 2.1f) * 4f;
                var c = lootCenter + offsets[i] + new Vector2(0, bob);
                const int size = 80;
                spriteBatch.Draw(icons[i], new Rectangle((int)(c.X - size / 2f), (int)(c.Y - size / 2f), size, size), Color.White);
            }

            _textLog.Draw(spriteBatch, font, new Vector2(60, 580), maxWidth: 740f);

            for (int i = 0; i < _suppliesButtons.Count; i++)
            {
                DrawStyledButton(spriteBatch, font, _suppliesButtons[i], new Color(66, 78, 66), new Color(46, 56, 46), _suppliesOptions[i].Description);
            }
        }

        /// <summary>Shared look for every clickable menu button on this screen: rounded
        /// gradient panel, hover-eased tint and border, and a small press-squash on click.
        /// Pass description to render a two-line button (title + a smaller detail line)
        /// like the supplies options use; omit it for a simple centered label.</summary>
        private void DrawStyledButton(SpriteBatch spriteBatch, SpriteFont font, Button button, Color baseTop, Color baseBottom, string description = null)
        {
            float hover = button.HoverAmount;
            Color top = UITheme.Brighten(baseTop, hover * 0.2f);
            Color bottom = UITheme.Brighten(baseBottom, hover * 0.2f);
            Color border = Color.Lerp(Color.White * 0.7f, Color.White, hover);
            float borderThickness = MathHelper.Lerp(2f, 3f, hover);

            // A brief inward squash while the press pulse decays, so a click reads as a
            // physical push rather than an instant color swap.
            float squash = button.PressAmount * 3f;
            var bounds = button.Bounds;
            var drawBounds = new RectangleF(bounds.X + squash, bounds.Y + squash / 2f, bounds.Width - squash * 2f, bounds.Height - squash);

            UITheme.DrawPanel(spriteBatch, drawBounds, top, bottom, border, borderThickness, 10f, shadowStrength: 0.5f);

            if (string.IsNullOrEmpty(description))
            {
                var textPos = new Vector2(drawBounds.X + 12, drawBounds.Y + (drawBounds.Height - font.MeasureString(button.Label).Y) / 2f);
                UITheme.DrawTextWithShadow(spriteBatch, font, button.Label, textPos, Color.White);
            }
            else
            {
                UITheme.DrawTextWithShadow(spriteBatch, font, button.Label, new Vector2(drawBounds.X + 12, drawBounds.Y + 10), Color.White);
                UITheme.DrawTextWithShadow(spriteBatch, font, description, new Vector2(drawBounds.X + 12, drawBounds.Y + 45), new Color(215, 215, 215), 0.85f);
            }
        }
    }
}
