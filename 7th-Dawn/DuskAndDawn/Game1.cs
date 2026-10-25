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

        public const int CanvasWidth = 1280;
        public const int CanvasHeight = 720;
        public const int RenderWidth = 1920;
        public const int RenderHeight = 1080;
        public const float RenderScale = RenderWidth / (float)CanvasWidth;
        private RenderTarget2D _canvas;
        private Rectangle _canvasDestination;
        private KeyboardState _previousKeyboard;
        private MouseState _previousMouse;

        private readonly Button _menuButton = new Button(new RectangleF(1242, 6, 34, 34), "Menu");

        public SpriteBatch SpriteBatch { get; private set; }
        public SpriteFont Font { get; private set; }
        public PlayerState PlayerState { get; private set; }

        public Texture2D HpBarFrame { get; private set; }
        public Texture2D HpBarGauge { get; private set; }
        public Texture2D HopeBarFrame { get; private set; }
        public Texture2D HopeBarGauge { get; private set; }
        public Texture2D HopeIcon { get; private set; }
        public Texture2D AttackedTexture { get; private set; }
        public Texture2D AttackTexture { get; private set; }
        public Texture2D SkillTexture { get; private set; }
        public Texture2D DiceTexture { get; private set; }
        public Texture2D BreadTexture { get; private set; }
        public Texture2D PlanksTexture { get; private set; }
        public Texture2D ScrapsTexture { get; private set; }

        private static readonly string[] WeaponIconNames = { "Axe", "Cleaver", "Club", "Dagger", "RustyKnife", "IronSword", "HolyLance", "Dawnbreaker", "WarMaul" };

        private readonly Dictionary<District, Texture2D> _districtIcons = new Dictionary<District, Texture2D>();

        public Texture2D GetDistrictIcon(District district) =>
            _districtIcons.TryGetValue(district, out var icon) ? icon : null;

        private readonly Dictionary<EnemyKind, Texture2D> _enemySprites = new Dictionary<EnemyKind, Texture2D>();

        public Texture2D GetEnemySprite(EnemyKind kind) =>
            _enemySprites.TryGetValue(kind, out var sprite) ? sprite : null;

        private static readonly Dictionary<BaseRoomType, string> RoomArtNames = new Dictionary<BaseRoomType, string>
        {
            [BaseRoomType.Storage] = "Storage",
            [BaseRoomType.Workshop] = "Workshop",
            [BaseRoomType.Infirmary] = "Infirmary",
            [BaseRoomType.Kitchen] = "Kitchen",
            [BaseRoomType.Barrack] = "Bed_Room",
            [BaseRoomType.Archive] = "plan"
        };
        private const int RoomArtTiers = 3;
        private readonly Dictionary<(BaseRoomType room, int tier), Texture2D> _roomSprites = new Dictionary<(BaseRoomType, int), Texture2D>();

        public Texture2D GetRoomSprite(BaseRoomType room, int level)
        {
            int tier = MathHelper.Clamp((level + 1) / 2, 1, RoomArtTiers);
            return _roomSprites.TryGetValue((room, tier), out var sprite) ? sprite : null;
        }

        private static readonly string[] ItemIconNames = { "Bandage", "Tonic", "Smoke Flask", "Elixir", "Holy Water", "Dawn Tincture", "Rations" };
        private readonly Dictionary<string, Texture2D> _itemIcons = new Dictionary<string, Texture2D>();

        public Texture2D GetItemIcon(string itemName) =>
            itemName != null && _itemIcons.TryGetValue(itemName, out var icon) ? icon : null;

        private readonly Dictionary<string, Texture2D> _weaponIcons = new Dictionary<string, Texture2D>();

        public Texture2D GetWeaponIcon(Weapon weapon)
        {
            if (weapon?.IconName == null) return null;
            return _weaponIcons.TryGetValue(weapon.IconName, out var texture) ? texture : null;
        }

        public Game1()
        {
            _graphics = new GraphicsDeviceManager(this);
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

            UITheme.LoadContent(GraphicsDevice);

            Font = Content.Load<SpriteFont>("DefaultFont");
            Font.Spacing = 2f;

            var hiResFont = Content.Load<SpriteFont>("DefaultFontHD");
            hiResFont.Spacing = Font.Spacing * RenderScale;
            UITheme.RegisterHiResFont(Font, hiResFont);

            HpBarFrame = Content.Load<Texture2D>("UI/HpBarFrame");
            HpBarGauge = Content.Load<Texture2D>("UI/HpBarGauge");
            HopeBarFrame = Content.Load<Texture2D>("UI/HopeBarFrame");
            HopeBarGauge = Content.Load<Texture2D>("UI/HopeBarGauge");
            HopeIcon = Content.Load<Texture2D>("UI/HopeIcon");
            var rectOutline = new SlicedTexture(Content.Load<Texture2D>("UI/FrameRectOutline"), 2, 1, 2, 1);
            UITheme.RegisterFrame(UIFrame.Rect, new SlicedTexture(Content.Load<Texture2D>("UI/FrameRect"), 2, 6, 2, 9), rectOutline, tileY: false);
            UITheme.RegisterFrame(UIFrame.Pill, new SlicedTexture(Content.Load<Texture2D>("UI/FramePill"), 4, 5, 4, 6), new SlicedTexture(Content.Load<Texture2D>("UI/FramePillOutline"), 4, 4, 4, 4), tileY: false, washInset: 4);
            UITheme.RegisterFrame(UIFrame.Tag, new SlicedTexture(Content.Load<Texture2D>("UI/FrameTag"), 5, 3, 5, 3), new SlicedTexture(Content.Load<Texture2D>("UI/FrameTagOutline"), 5, 3, 5, 3), tileY: true, washInset: 3);
            UITheme.RegisterFrame(UIFrame.Wood, new SlicedTexture(Content.Load<Texture2D>("UI/FrameWood"), 4, 4, 4, 4), rectOutline, tileY: true, outlineInset: 3, washInset: 4);
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
            foreach (var itemName in ItemIconNames)
            {
                _itemIcons[itemName] = Content.Load<Texture2D>("Items/" + itemName.Replace(" ", ""));
            }
            _districtIcons[District.VillageOutskirts] = Content.Load<Texture2D>("Village");
            _districtIcons[District.ChurchRuins] = Content.Load<Texture2D>("Church");
            _districtIcons[District.CastleKeep] = Content.Load<Texture2D>("Keep");
            _districtIcons[District.Castle] = Content.Load<Texture2D>("TheCastle");
            _enemySprites[EnemyKind.Herald] = Content.Load<Texture2D>("SunHerald");
            _enemySprites[EnemyKind.Penitent] = Content.Load<Texture2D>("Penitent");
            _enemySprites[EnemyKind.Knight] = Content.Load<Texture2D>("Knight");
            _enemySprites[EnemyKind.Wretch] = Content.Load<Texture2D>("Wretch");
            foreach (var (room, artName) in RoomArtNames)
            {
                for (int tier = 1; tier <= RoomArtTiers; tier++)
                {
                    _roomSprites[(room, tier)] = Content.Load<Texture2D>($"Base/{artName}_LV{tier}");
                }
            }
            Sound.LoadContent(Content);

            _screenManager.ShowScreen(new MainMenuScreen(this));
        }

        public void ApplySettings()
        {
            if (_graphics.IsFullScreen != GameSettings.Current.Fullscreen)
            {
                SetFullScreen(GameSettings.Current.Fullscreen);
            }
        }

        public bool HasRunInProgress => _screenManager.Screens.Any(screen => screen is IGameplayScreen);

        private bool IsGameplayActive => _screenManager.ActiveScreen is IGameplayScreen && !ScreenTransitions.IsTransitioning;

        private bool IsCombatShowing
        {
            get
            {
                var screens = _screenManager.Screens;
                for (int i = screens.Count - 1; i >= 0; i--)
                {
                    if (screens[i] is MainMenuScreen) return false;
                    if (screens[i] is NightScavengingScreen night) return night.InCombat;
                }
                return false;
            }
        }

        public void StartNewRun(bool withIntro = false)
        {
            PlayerState = new PlayerState();
            var fade = ScreenTransitions.FadeTransition(GraphicsDevice);
            fade.StateChanged += (_, _) => _screenManager.ClearScreens();
            _screenManager.ShowScreen(withIntro ? new IntroScreen(this) : new BaseBuilding(this, PlayerState), fade);
        }

        public void BeginRunAfterIntro()
        {
            PlayerState ??= new PlayerState();
            _screenManager.ReplaceScreen(new BaseBuilding(this, PlayerState), ScreenTransitions.FadeTransition(GraphicsDevice));
        }

        public void ContinueSavedRun()
        {
            var saved = SaveGame.Load();
            if (saved == null) return;
            PlayerState = saved;
            var fade = ScreenTransitions.FadeTransition(GraphicsDevice);
            fade.StateChanged += (_, _) => _screenManager.ClearScreens();
            _screenManager.ShowScreen(new BaseBuilding(this, PlayerState), fade);
        }

        public void EndRun(bool victory)
        {
            if (ScreenTransitions.IsTransitioning || _screenManager.ActiveScreen is GameOverScreen) return;
            SaveGame.Delete();
            _screenManager.ReplaceScreen(new GameOverScreen(this, victory, PlayerState), ScreenTransitions.FadeTransition(GraphicsDevice));
        }

        public void EndRunToMainMenu()
        {
            PlayerState = null;
            var fade = ScreenTransitions.FadeTransition(GraphicsDevice);
            fade.StateChanged += (_, _) => _screenManager.ClearScreens();
            _screenManager.ShowScreen(new MainMenuScreen(this), fade);
        }

        private void SetFullScreen(bool fullScreen)
        {
            if (fullScreen)
            {
                var mode = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
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

                bool ctrl = keyboard.IsKeyDown(Keys.LeftControl) || keyboard.IsKeyDown(Keys.RightControl);
                bool shift = keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift);
                bool f7Pressed = keyboard.IsKeyDown(Keys.F7) && !_previousKeyboard.IsKeyDown(Keys.F7);
                if (ctrl && shift && f7Pressed && _screenManager.ActiveScreen is BaseBuilding or PreparationScreen && PlayerState.Day < DayInfo.FinalDay)
                {
                    PlayerState.Day = DayInfo.FinalDay;
                    _screenManager.ReplaceScreen(new BaseBuilding(this, PlayerState), ScreenTransitions.FadeTransition(GraphicsDevice));
                }
            }

            _previousKeyboard = keyboard;
            _previousMouse = mouse;

            base.Update(gameTime);
            Sound.PlayMusic(IsCombatShowing ? Music.Combat : Music.Ambiance);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.SetRenderTarget(_canvas);
            GraphicsDevice.Clear(Color.Black);
            base.Draw(gameTime);

            if (IsGameplayActive)
            {
                UITheme.BeginCanvas(SpriteBatch);
                DrawMenuButton();
                SpriteBatch.End();
            }

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

            Color border = Color.Lerp(new Color(170, 80, 45), new Color(255, 140, 70), hover);
            UITheme.DrawFrame(SpriteBatch, drawBounds, UIFrame.Rect, border, 0.9f, shadow: 0.5f, surface: Palette.EmberPanel);

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