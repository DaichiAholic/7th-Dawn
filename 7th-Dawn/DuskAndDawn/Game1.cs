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

            PlayerState = new PlayerState();

            _screenManager.ShowScreen(new BaseBuilding(this, PlayerState));
        }
    }
}
