using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using MonoGame.Extended.Screens;

namespace DuskAndDawn
{
    public class Game1 : Game
    {
        private readonly GraphicsDeviceManager _graphics;
        private readonly ScreenManager _screenManager;

        // Every screen is laid out in 1280x720 units, but renders into a true 1920x1080
        // canvas: each SpriteBatch scales the layout up by RenderScale (see UITheme), so
        // shapes and text are drawn at full resolution rather than stretched. The canvas is
        // then fit to the real window/monitor - 1:1 on a 1080p screen, letterboxed to keep
        // 16:9 elsewhere - so the layouts never have to know the actual resolution.
        public const int CanvasWidth = 1280;
        public const int CanvasHeight = 720;
        public const int RenderWidth = 1920;
        public const int RenderHeight = 1080;
        public const float RenderScale = RenderWidth / (float)CanvasWidth;
        private RenderTarget2D _canvas;
        private Rectangle _canvasDestination;
        private KeyboardState _previousKeyboard;
        private MouseState _previousMouse;

        // Small menu icon in the top-right corner of every gameplay screen - the mouse route
        // to the pause menu, alongside Esc. Sits in the strip right of x=1240 that no screen uses.
        private readonly Button _menuButton = new Button(new RectangleF(1242, 6, 34, 34), "Menu");

        public SpriteBatch SpriteBatch { get; private set; }
        public SpriteFont Font { get; private set; }
        public PlayerState PlayerState { get; private set; }

        // ---- Sprite assets ----
        public Texture2D HpBarTexture { get; private set; }
        public Texture2D AttackedTexture { get; private set; }
        // Combat animations: horizontal sheets of 7 frames, 64x64 each.
        public Texture2D AttackTexture { get; private set; }   // player's basic attack slash
        public Texture2D SkillTexture { get; private set; }    // player's skill (Power Strike)
        public Texture2D DiceTexture { get; private set; }
        public Texture2D BreadTexture { get; private set; }
        public Texture2D PlanksTexture { get; private set; }
        public Texture2D ScrapsTexture { get; private set; }

        // 32x32 pixel-art weapon icons, keyed by asset name (Weapon.IconName).
        private static readonly string[] WeaponIconNames = { "Axe", "Cleaver", "Club", "Dagger" };
        private readonly Dictionary<string, Texture2D> _weaponIcons = new Dictionary<string, Texture2D>();

        /// <summary>The icon for a weapon, or null if it has no art yet.</summary>
        public Texture2D GetWeaponIcon(Weapon weapon)
        {
            if (weapon?.IconName == null) return null;
            return _weaponIcons.TryGetValue(weapon.IconName, out var texture) ? texture : null;
        }

        public Game1()
        {
            _graphics = new GraphicsDeviceManager(this);
            // Smooths the diagonal edges on rotated/thin primitives (window mullions, dice-log
            // lines, etc.) - a free visual upgrade that doesn't touch any drawing code.
            _graphics.PreferMultiSampling = true;
            Content.RootDirectory = "Content";
            IsMouseVisible = true;

            _screenManager = new ScreenManager();
            Components.Add(_screenManager);
        }

        protected override void Initialize()
        {
            GameSettings.Load();
            SetFullScreen(GameSettings.Current.Fullscreen);

            base.Initialize();

            SpriteBatch = new SpriteBatch(GraphicsDevice);
            UITheme.SetRenderScale(RenderScale);
            _canvas = new RenderTarget2D(GraphicsDevice, RenderWidth, RenderHeight, false,
                SurfaceFormat.Color, DepthFormat.None, GraphicsDevice.PresentationParameters.MultiSampleCount,
                RenderTargetUsage.DiscardContents);

            // Bakes the shared rounded-corner/shadow texture used by every screen's panels
            // and buttons. Must happen after the GraphicsDevice exists and before any screen
            // draws for the first time.
            UITheme.LoadContent(GraphicsDevice);

            Font = Content.Load<SpriteFont>("DefaultFont");
            Font.Spacing = 2f;

            // Same face at 1.5x size, so text is sharp on the 1920x1080 canvas (see UITheme).
            var hiResFont = Content.Load<SpriteFont>("DefaultFontHD");
            hiResFont.Spacing = Font.Spacing * RenderScale;
            UITheme.RegisterHiResFont(Font, hiResFont);

            HpBarTexture = Content.Load<Texture2D>("Hpbar");
            AttackedTexture = Content.Load<Texture2D>("Attacked");
            AttackTexture = Content.Load<Texture2D>("Attack");
            SkillTexture = Content.Load<Texture2D>("Skill");
            DiceTexture = Content.Load<Texture2D>("Dice");
            BreadTexture = Content.Load<Texture2D>("Bread");
            PlanksTexture = Content.Load<Texture2D>("Planks");
            ScrapsTexture = Content.Load<Texture2D>("Scraps");

            foreach (var iconName in WeaponIconNames)
            {
                _weaponIcons[iconName] = Content.Load<Texture2D>(iconName);
            }

            _screenManager.ShowScreen(new MainMenuScreen(this));
        }

        /// <summary>Pushes the current GameSettings to the window (Settings screen).</summary>
        public void ApplySettings()
        {
            if (_graphics.IsFullScreen != GameSettings.Current.Fullscreen)
            {
                SetFullScreen(GameSettings.Current.Fullscreen);
            }
        }

        /// <summary>True while a run's screens are on the stack (e.g. under the main menu).</summary>
        public bool HasRunInProgress => _screenManager.Screens.Any(screen => screen is IGameplayScreen);

        private bool IsGameplayActive => _screenManager.ActiveScreen is IGameplayScreen && !ScreenTransitions.IsTransitioning;

        /// <summary>Throws away any run in progress and starts a fresh one at the base.</summary>
        public void StartNewRun()
        {
            PlayerState = new PlayerState();
            var fade = ScreenTransitions.FadeTransition(GraphicsDevice);
            // Cleared at the fade's midpoint (just before the new screen is pushed), so the
            // old screens stay visible while the fade darkens.
            fade.StateChanged += (_, _) => _screenManager.ClearScreens();
            _screenManager.ShowScreen(new BaseBuilding(this, PlayerState), fade);
        }

        /// <summary>Ends the run and returns to the title screen (used after a game over).</summary>
        public void EndRunToMainMenu()
        {
            PlayerState = null;
            var fade = ScreenTransitions.FadeTransition(GraphicsDevice);
            fade.StateChanged += (_, _) => _screenManager.ClearScreens();
            _screenManager.ShowScreen(new MainMenuScreen(this), fade);
        }

        /// <summary>Borderless fullscreen at the monitor's resolution, or a 1280x720 window.</summary>
        private void SetFullScreen(bool fullScreen)
        {
            if (fullScreen)
            {
                var mode = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
                // Borderless rather than an exclusive mode switch: no flicker when toggling,
                // and alt-tabbing out behaves.
                _graphics.HardwareModeSwitch = false;
                _graphics.PreferredBackBufferWidth = mode.Width;
                _graphics.PreferredBackBufferHeight = mode.Height;
            }
            else
            {
                _graphics.PreferredBackBufferWidth = CanvasWidth;
                _graphics.PreferredBackBufferHeight = CanvasHeight;
            }
            _graphics.IsFullScreen = fullScreen;
            _graphics.ApplyChanges();
        }

        /// <summary>Largest 16:9 area that fits the back buffer, centered. Also tells
        /// InputChecker how to map the mouse back onto the canvas.</summary>
        private void UpdateCanvasDestination()
        {
            var pp = GraphicsDevice.PresentationParameters;
            float scale = System.Math.Min(pp.BackBufferWidth / (float)CanvasWidth, pp.BackBufferHeight / (float)CanvasHeight);
            int width = (int)(CanvasWidth * scale);
            int height = (int)(CanvasHeight * scale);
            _canvasDestination = new Rectangle((pp.BackBufferWidth - width) / 2, (pp.BackBufferHeight - height) / 2, width, height);

            InputChecker.CanvasScale = scale;
            InputChecker.CanvasOffset = new Vector2(_canvasDestination.X, _canvasDestination.Y);
        }

        protected override void Update(GameTime gameTime)
        {
            // F11 or Alt+Enter flips between fullscreen and a window.
            var keyboard = Keyboard.GetState();
            bool altEnter = keyboard.IsKeyDown(Keys.Enter) && !_previousKeyboard.IsKeyDown(Keys.Enter)
                && (keyboard.IsKeyDown(Keys.LeftAlt) || keyboard.IsKeyDown(Keys.RightAlt));
            bool f11 = keyboard.IsKeyDown(Keys.F11) && !_previousKeyboard.IsKeyDown(Keys.F11);
            if (altEnter || f11)
            {
                SetFullScreen(!_graphics.IsFullScreen);
                GameSettings.Current.Fullscreen = _graphics.IsFullScreen;
                GameSettings.Save();
            }

            UpdateCanvasDestination();

            // Esc or the Menu button pauses. This runs before the screens update (base.Update),
            // so the gameplay screen never sees the key press or click that opened the menu.
            var mouse = InputChecker.GetMouse();
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            bool gameplay = IsGameplayActive;
            bool menuHovered = gameplay && _menuButton.Contains(mouse.X, mouse.Y);
            _menuButton.UpdateAnimation(dt, menuHovered);

            if (gameplay)
            {
                bool escPressed = keyboard.IsKeyDown(Keys.Escape) && !_previousKeyboard.IsKeyDown(Keys.Escape);
                bool menuClicked = menuHovered && InputChecker.IsNewLeftClick(mouse, _previousMouse);
                if (escPressed || menuClicked)
                {
                    if (menuClicked) _menuButton.TriggerPress();
                    _screenManager.ShowScreen(new PauseMenuScreen(this));
                }
            }

            _previousKeyboard = keyboard;
            _previousMouse = mouse;

            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            // Screens (and their fade transitions) draw into the 1920x1080 canvas...
            GraphicsDevice.SetRenderTarget(_canvas);
            // Screens clear the frame themselves; this only matters for the instant during a
            // fade when the screen stack is empty (see StartNewRun).
            GraphicsDevice.Clear(Color.Black);
            base.Draw(gameTime);

            if (IsGameplayActive)
            {
                UITheme.BeginCanvas(SpriteBatch);
                DrawMenuButton();
                SpriteBatch.End();
            }

            // ...which is then fit onto the real screen, with black bars if the aspect differs.
            // At exactly 1920x1080 this is a pixel-for-pixel copy.
            GraphicsDevice.SetRenderTarget(null);
            GraphicsDevice.Clear(Color.Black);
            bool exactFit = _canvasDestination.Width == RenderWidth && _canvasDestination.Height == RenderHeight;
            SpriteBatch.Begin(samplerState: exactFit ? SamplerState.PointClamp : SamplerState.LinearClamp);
            SpriteBatch.Draw(_canvas, _canvasDestination, Color.White);
            SpriteBatch.End();
        }

        private void DrawMenuButton()
        {
            float hover = _menuButton.HoverAmount;
            float squash = _menuButton.PressAmount * 3f;
            var bounds = _menuButton.Bounds;
            var drawBounds = new RectangleF(bounds.X + squash, bounds.Y + squash / 2f, bounds.Width - squash * 2f, bounds.Height - squash);

            Color top = Color.Lerp(new Color(58, 46, 42), new Color(84, 64, 52), hover) * 0.9f;
            Color bottom = Color.Lerp(new Color(36, 28, 26), new Color(54, 40, 34), hover) * 0.9f;
            Color border = Color.Lerp(new Color(170, 80, 45), new Color(255, 140, 70), hover);
            UITheme.DrawPanel(SpriteBatch, drawBounds, top, bottom, border, MathHelper.Lerp(1.5f, 2.5f, hover), 8f, shadowStrength: 0.5f);

            // Three-bar "menu" glyph.
            Color bar = Color.Lerp(new Color(225, 215, 210), Color.White, hover);
            float barWidth = drawBounds.Width * 0.5f;
            float barX = drawBounds.X + (drawBounds.Width - barWidth) / 2f;
            float centerY = drawBounds.Y + drawBounds.Height / 2f;
            for (int i = -1; i <= 1; i++)
            {
                UITheme.FillRoundedRect(SpriteBatch, new RectangleF(barX, centerY + i * 7f - 1.5f, barWidth, 3f), bar, 1.5f);
            }
        }
    }
}