using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.Screens;

namespace DuskAndDawn
{
    public class Game1 : Game
    {
        private readonly GraphicsDeviceManager _graphics;
        private readonly ScreenManager _screenManager;

        // Every screen is laid out for a fixed 1280x720 canvas. The game draws into this
        // render target, then scales it to fill the real window/monitor (letterboxed to keep
        // the 16:9 shape), so the layouts never have to know the actual resolution.
        public const int CanvasWidth = 1280;
        public const int CanvasHeight = 720;
        private RenderTarget2D _canvas;
        private Rectangle _canvasDestination;
        private KeyboardState _previousKeyboard;

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
            SetFullScreen(true);

            base.Initialize();

            SpriteBatch = new SpriteBatch(GraphicsDevice);
            _canvas = new RenderTarget2D(GraphicsDevice, CanvasWidth, CanvasHeight, false,
                SurfaceFormat.Color, DepthFormat.None, GraphicsDevice.PresentationParameters.MultiSampleCount,
                RenderTargetUsage.DiscardContents);

            // Bakes the shared rounded-corner/shadow texture used by every screen's panels
            // and buttons. Must happen after the GraphicsDevice exists and before any screen
            // draws for the first time.
            UITheme.LoadContent(GraphicsDevice);

            Font = Content.Load<SpriteFont>("DefaultFont");
            Font.Spacing = 2f;

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

            PlayerState = new PlayerState();

            _screenManager.ShowScreen(new BaseBuilding(this, PlayerState));
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
            }
            _previousKeyboard = keyboard;

            UpdateCanvasDestination();
            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            // Screens (and their fade transitions) draw at 1280x720 into the canvas...
            GraphicsDevice.SetRenderTarget(_canvas);
            base.Draw(gameTime);

            // ...which is then scaled onto the real screen, with black bars if the aspect differs.
            GraphicsDevice.SetRenderTarget(null);
            GraphicsDevice.Clear(Color.Black);
            SpriteBatch.Begin(samplerState: SamplerState.LinearClamp);
            SpriteBatch.Draw(_canvas, _canvasDestination, Color.White);
            SpriteBatch.End();
        }
    }
}