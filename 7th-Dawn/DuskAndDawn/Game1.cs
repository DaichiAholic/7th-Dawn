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
            _graphics.PreferredBackBufferWidth = 1280;
            _graphics.PreferredBackBufferHeight = 720;
            _graphics.ApplyChanges();

            base.Initialize();

            SpriteBatch = new SpriteBatch(GraphicsDevice);

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
    }
}