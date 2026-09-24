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
    /// Phase 2: Night Scavenging. No player movement - a branching minimap you click through,
    /// a dawn clock ticking down per action (not real time), and dice-roll combat. This is
    /// where the dawn clock, room generation, and combat all interact.
    /// </summary>
    public class NightScavengingScreen : GameScreen
    {
        private Game1 Game1 => (Game1)Game;
        private readonly PlayerState _playerState;
        private readonly Random _random = new Random();
        private District _district;

        private const int LayerCount = 6;
        private const int NodesPerLayer = 2;

        private DawnTimer _dawnTimer;
        private NightMap _map;
        private int _currentDepth; // index of the next layer the player can click into

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
            var roomGenerator = new RoomGenerator(_district);
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

            // Every interactive element eases its own hover/press animation forward each
            // frame (even while its screen isn't the active one - that's harmless, it just
            // idles at rest) so nothing snaps between visual states.
            bool headBackHovered = _state == ExplorationState.Map && _headBackButton.Contains(mouse.X, mouse.Y);
            _headBackButton.UpdateAnimation(dt, headBackHovered);

            foreach (var layer in _map.Layers)
            {
                foreach (var node in layer)
                {
                    bool isHoverable = _state == ExplorationState.Map && node.Depth == _currentDepth
                        && !node.Visited && InputChecker.Contains(node.ScreenBounds, mouse.X, mouse.Y);
                    node.UpdateHover(dt, isHoverable);
                }
            }

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

            if (clicked)
            {
                switch (_state)
                {
                    case ExplorationState.Map:
                        HandleMapClick(mouse.X, mouse.Y);
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
            ScreenManager.ReplaceScreen(new Dawn(Game, _playerState, _roomsCleared, _currentDepth), ScreenTransitions.FadeTransition(GraphicsDevice));
        }

        // ---------- Encounter (combat) ----------

        private void StartEncounter(int depth)
        {
            var enemy = new Enemy(
                $"{DistrictInfo.RandomEnemyName(_district, _random)} (Depth {depth + 1})",
                maxHealth: 30 + depth * 3 + DistrictInfo.EnemyHealthBonus(_district),
                attackPower: 6 + depth + DistrictInfo.EnemyAttackBonus(_district),
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

        /// <summary>Fires the dice-roll popup and the enemy/player hit-flashes based on what
        /// CombatEncounter's last action actually did - castTexture (Attack.png or
        /// Skill.png) only plays if that action was a damage roll (it's ignored for
        /// Guard, items, etc. since LastRollWasAttack stays false for those).</summary>
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

            DrawClock(spriteBatch, font, totalSeconds);
            string weaponLine = $"Weapon: {_playerState.EquippedWeapon.Name} ({_playerState.EquippedWeapon.DiceLabel})";
            UITheme.DrawTextWithShadow(spriteBatch, font, weaponLine, new Vector2(40, 100), Color.LightGray);
            // 1x icon just after the weapon line - small, but crisp at native size.
            float weaponLineWidth = font.MeasureString(weaponLine).X;
            UITheme.DrawPixelIcon(spriteBatch, Game1.GetWeaponIcon(_playerState.EquippedWeapon), new Vector2(40 + weaponLineWidth + 8, 94), 1);
            UITheme.DrawTextWithShadow(spriteBatch, font, $"{DistrictInfo.Name(_district)}  -  Corruption {DistrictInfo.Corruption(_district)}", new Vector2(420, 28), new Color(255, 180, 120));
            DrawPlayerHealthBar(spriteBatch, font);

            switch (_state)
            {
                case ExplorationState.Map:
                    DrawMinimap(spriteBatch, font);
                    break;
                case ExplorationState.Encounter:
                    DrawCombat(spriteBatch, font, totalSeconds);
                    DrawMapFragment(spriteBatch, font);
                    break;
                case ExplorationState.Supplies:
                    DrawSupplies(spriteBatch, font);
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

        private void DrawMinimap(SpriteBatch spriteBatch, SpriteFont font)
        {
            for (int depth = 0; depth < _map.Layers.Count; depth++)
            {
                foreach (var node in _map.Layers[depth])
                {
                    Color top, bottom;
                    string label;

                    if (node.Visited)
                    {
                        top = new Color(66, 66, 66);
                        bottom = new Color(44, 44, 44);
                        label = node.Type.ToString();
                    }
                    else if (depth >= _currentDepth && depth <= _currentDepth + _playerState.ArchiveRevealDepth)
                    {
                        (top, bottom) = node.Type switch
                        {
                            RoomType.Supplies => (new Color(48, 108, 72), new Color(28, 70, 46)),
                            RoomType.Encounter => (new Color(130, 38, 38), new Color(90, 22, 22)),
                            RoomType.Special => (new Color(96, 50, 130), new Color(64, 30, 90)),
                            _ => (Color.Gray, Color.DarkGray)
                        };
                        label = node.Type.ToString();

                        // Rooms scouted ahead by the Archive show their type but stay dimmed,
                        // so it's clear only the current layer can be entered.
                        if (depth > _currentDepth)
                        {
                            top = UITheme.Darken(top, 0.45f);
                            bottom = UITheme.Darken(bottom, 0.45f);
                        }
                    }
                    else
                    {
                        top = new Color(34, 34, 46);
                        bottom = new Color(22, 22, 32);
                        label = "?";
                    }

                    // Clickable tiles ease brighter as the mouse hovers them, and their border
                    // warms toward an ember glow - a stand-in for the corruption-glow "tell"
                    // the design calls for on room doors, without needing new art.
                    if (node.HoverAmount > 0f)
                    {
                        top = UITheme.Brighten(top, node.HoverAmount * 0.25f);
                        bottom = UITheme.Brighten(bottom, node.HoverAmount * 0.25f);
                    }
                    Color borderColor = Color.Lerp(Color.White * 0.5f, new Color(255, 130, 60), node.HoverAmount);
                    float borderThickness = MathHelper.Lerp(1.5f, 3f, node.HoverAmount);

                    UITheme.DrawPanel(spriteBatch, node.ScreenBounds, top, bottom, borderColor, borderThickness, 10f, shadowStrength: 0.4f);
                    UITheme.DrawTextWithShadow(spriteBatch, font, label, new Vector2(node.ScreenBounds.X + 8, node.ScreenBounds.Y + 8), Color.White);
                }
            }

            DrawStyledButton(spriteBatch, font, _headBackButton, new Color(105, 82, 82), new Color(75, 56, 56));
        }

        private void DrawMapFragment(SpriteBatch spriteBatch, SpriteFont font)
        {
            // Small persistent reminder of where you are in tonight's map, even mid-fight or
            // mid-choice - a stand-in for the reference's parchment map-fragment icon.
            var box = new RectangleF(40, 610, 220, 90);
            UITheme.DrawPanel(spriteBatch, box, new Color(62, 50, 32), new Color(42, 34, 20), new Color(150, 120, 70), 2f, 12f, shadowStrength: 0.5f);
            UITheme.DrawTextWithShadow(spriteBatch, font, "Tonight's map", new Vector2(box.X + 10, box.Y + 10), new Color(225, 205, 165));
            UITheme.DrawTextWithShadow(spriteBatch, font, $"Depth {_currentDepth}/{_map.Layers.Count}", new Vector2(box.X + 10, box.Y + 38), new Color(225, 205, 165));
            UITheme.DrawTextWithShadow(spriteBatch, font, DistrictInfo.Name(_district), new Vector2(box.X + 10, box.Y + 64), new Color(200, 180, 140), 0.8f);
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

        private void DrawSupplies(SpriteBatch spriteBatch, SpriteFont font)
        {
            UITheme.DrawTextWithShadow(spriteBatch, font, "You find a supply cache.", new Vector2(60, 220), Color.White);
            UITheme.DrawTextWithShadow(spriteBatch, font, "What do you do?", new Vector2(60, 250), Color.LightGray);

            // Loot placeholder - swap for real item art once it exists.
            var lootBox = new RectangleF(60, 300, 260, 260);
            UITheme.DrawPanel(spriteBatch, lootBox, new Color(58, 50, 30), new Color(38, 32, 18), new Color(150, 118, 64), 3f, 14f, shadowStrength: 0.6f);

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
                UITheme.DrawTextWithShadow(spriteBatch, font, description, new Vector2(drawBounds.X + 12, drawBounds.Y + 45), new Color(215, 215, 215));
            }
        }
    }
}