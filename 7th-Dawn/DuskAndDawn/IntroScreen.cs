using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using MonoGame.Extended.Screens;
using System;

namespace DuskAndDawn
{
    public class IntroScreen : GameScreen
    {
        private Game1 Game1 => (Game1)Game;

        private const float TypeSpeed = 26f;
        private const float TypeDelay = 0.6f;
        private const float FadeIn = 0.8f, FadeOut = 0.7f, Hold = 2.6f;
        private const float SettleTime = 1.2f;

        private const int ArtPixel = 2;
        private const int CardSupersample = 4;
        private const int CardPadding = 2;
        private static readonly Vector2 PictureCenter = new Vector2(640, 236);
        private const float TextTop = 470f, TextWidth = 640f;

        private static readonly string[] SlideText =
        {
            "Long ago, the sun kept the world warm, and the world kept its faith in the sun.",
            "Then the Sun Herald came down out of the light. And the light began to burn.",
            "The faithful knelt, and burned with him. The proud put on iron, and guarded his halls.",
            "The rest of us hid from the day, and learned to live by night.",
            "One house still stands. Each night, someone goes out into the ruins. Each morning, the house waits to see who comes home.",
            "Hold on for seven days. Build the house. Keep its hope alive - if hope runs out, it is over.",
            "And on the seventh night, the Castle opens its doors. The Sun Herald is waiting for his dawn."
        };

        private readonly Color[] _ambient = new Color[SlideText.Length];
        private RenderTarget2D _card;
        private int _index;
        private float _slideTime;
        private float _time;
        private float _titleTime = -1f;
        private bool _finished;
        private KeyboardState _previousKeyboard;
        private MouseState _previousMouse;

        public IntroScreen(Game game) : base(game)
        {
        }

        public override void Initialize()
        {
            base.Initialize();
            _previousKeyboard = Keyboard.GetState();
            _previousMouse = InputChecker.GetMouse();
            for (int i = 0; i < SlideText.Length; i++)
            {
                _ambient[i] = AmbientLight(Game1.GetIntroArt(i));
            }
        }

        public override void UnloadContent()
        {
            _card?.Dispose();
            base.UnloadContent();
        }

        private static float TypingDone(string text) => TypeDelay + text.Length / TypeSpeed;
        private static float SlideEnd(string text) => TypingDone(text) + Hold;

        public override void Update(GameTime gameTime)
        {
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            _time += dt;
            var keyboard = Keyboard.GetState();
            var previousKeyboard = _previousKeyboard;
            var mouse = InputChecker.GetMouse();
            bool Pressed(Keys key) => keyboard.IsKeyDown(key) && !previousKeyboard.IsKeyDown(key);
            bool alt = keyboard.IsKeyDown(Keys.LeftAlt) || keyboard.IsKeyDown(Keys.RightAlt);
            bool advance = Pressed(Keys.Space) || (Pressed(Keys.Enter) && !alt) || Pressed(Keys.Z)
                || (mouse.LeftButton == ButtonState.Released && _previousMouse.LeftButton == ButtonState.Pressed);
            _previousKeyboard = keyboard;
            _previousMouse = mouse;

            if (_finished || ScreenTransitions.IsTransitioning) return;

            if (Pressed(Keys.Escape))
            {
                Finish();
                return;
            }

            if (_titleTime >= 0f)
            {
                _titleTime += dt;
                if ((advance && _titleTime > 1.2f) || _titleTime > 9f) Finish();
                return;
            }

            string text = SlideText[_index];
            _slideTime += dt;
            if (advance)
            {
                if (_slideTime < TypingDone(text)) _slideTime = TypingDone(text);
                else if (_slideTime < SlideEnd(text)) _slideTime = SlideEnd(text);
            }

            if (_slideTime >= SlideEnd(text) + FadeOut)
            {
                _index++;
                _slideTime = 0f;
                if (_index >= SlideText.Length)
                {
                    _index = SlideText.Length - 1;
                    _titleTime = 0f;
                }
            }
        }

        private void Finish()
        {
            if (_finished) return;
            _finished = true;
            Game1.BeginRunAfterIntro();
        }

        public override void Draw(GameTime gameTime)
        {
            var device = GraphicsDevice;
            var spriteBatch = Game1.SpriteBatch;
            var font = Game1.Font;
            bool onTitle = _titleTime >= 0f;
            var art = onTitle ? null : Game1.GetIntroArt(_index);

            if (art != null) RenderCard(device, spriteBatch, art);

            device.Clear(Color.Black);
            UITheme.BeginCanvas(spriteBatch);
            if (onTitle)
            {
                DrawTitle(spriteBatch, font);
            }
            else
            {
                string text = SlideText[_index];
                float leave = MathHelper.Clamp((_slideTime - SlideEnd(text)) / FadeOut, 0f, 1f);
                float alpha = Math.Min(Anim.Intro(_slideTime, 0f, FadeIn), 1f - leave);
                if (art != null) DrawCard(spriteBatch, alpha, leave);
                DrawTypedText(spriteBatch, font, text, alpha);
            }

            const string skip = "Esc: skip";
            var skipSize = UITheme.MeasureString(font, skip) * 0.58f;
            UITheme.DrawTextWithShadow(spriteBatch, font, skip, new Vector2(1250 - skipSize.X, 690), new Color(120, 115, 115) * 0.7f, 0.58f);
            spriteBatch.End();
        }

        private void RenderCard(GraphicsDevice device, SpriteBatch spriteBatch, Texture2D art)
        {
            int width = art.Width * CardSupersample + CardPadding * 2;
            int height = art.Height * CardSupersample + CardPadding * 2;
            if (_card == null || _card.Width != width || _card.Height != height)
            {
                _card?.Dispose();
                _card = new RenderTarget2D(device, width, height);
            }

            var previous = device.GetRenderTargets();
            device.SetRenderTarget(_card);
            device.Clear(Color.Transparent);
            spriteBatch.Begin(samplerState: SamplerState.PointClamp);
            spriteBatch.Draw(art, new Vector2(CardPadding), null, Color.White, 0f, Vector2.Zero, CardSupersample, SpriteEffects.None, 0f);
            spriteBatch.End();
            device.SetRenderTargets(previous);
        }

        private void DrawCard(SpriteBatch spriteBatch, float alpha, float leave)
        {
            if (alpha <= 0.001f) return;
            float settle = 1f - UITheme.EaseOutCubic(_slideTime / SettleTime);
            float sway = MathF.Sin(_time * 1.1f) * 0.9f + MathF.Sin(_time * 2.3f + 1.7f) * 0.3f - settle * 2.5f;
            var drift = new Vector2(MathF.Sin(_time * 0.7f + 0.4f) * 3f, MathF.Sin(_time * 1.5f) * 5f + settle * 22f - leave * 12f);
            float zoom = 1f + MathF.Sin(_time * 0.9f) * 0.012f - settle * 0.04f + leave * 0.02f;

            var center = PictureCenter + drift;
            float scale = ArtPixel / (UITheme.RenderScale * CardSupersample) * zoom;
            var size = new Vector2(_card.Width, _card.Height) * scale;

            UITheme.DrawGlow(spriteBatch, center, size.X * 1.15f, _ambient[_index] * alpha);
            UITheme.DrawSoftShadow(spriteBatch, new RectangleF(center.X - size.X / 2f, center.Y - size.Y / 2f, size.X, size.Y), 6f, alpha);
            spriteBatch.Draw(_card, center, null, Color.White * alpha, MathHelper.ToRadians(sway), new Vector2(_card.Width, _card.Height) / 2f, scale, SpriteEffects.None, 0f);
        }

        private static Color AmbientLight(Texture2D art)
        {
            if (art == null) return Color.Transparent;
            var pixels = new Color[art.Width * art.Height];
            art.GetData(pixels);
            var sum = Vector3.Zero;
            foreach (var pixel in pixels) sum += pixel.ToVector3();
            var average = sum / pixels.Length;
            float peak = MathF.Max(average.X, MathF.Max(average.Y, average.Z));
            if (peak <= 0f) return Color.Transparent;
            float luminance = Vector3.Dot(average, new Vector3(0.299f, 0.587f, 0.114f));
            return new Color(average / peak) * (0.12f + luminance * 0.35f);
        }

        private void DrawTypedText(SpriteBatch spriteBatch, SpriteFont font, string text, float alpha)
        {
            const float scale = 1.1f, lineHeight = 34f;
            var lines = TextLog.WrapText(font, text, TextWidth / scale);
            int remaining = (int)MathF.Max(0f, (_slideTime - TypeDelay) * TypeSpeed);
            float y = TextTop;
            foreach (var line in lines)
            {
                if (remaining <= 0) break;
                string shown = remaining >= line.Length ? line : line.Substring(0, remaining);
                remaining -= line.Length + 1;
                float width = UITheme.MeasureString(font, line).X * scale;
                UITheme.DrawTextWithShadow(spriteBatch, font, shown, new Vector2(640f - width / 2f, y), Color.White * alpha, scale);
                y += lineHeight;
            }
        }

        private void DrawTitle(SpriteBatch spriteBatch, SpriteFont font)
        {
            float t = Anim.Intro(_titleTime, 0.2f, 1.6f);
            float glow = UITheme.PulseSine(_titleTime, 0.8f);
            UITheme.DrawGlow(spriteBatch, new Vector2(640, 300), 420f, new Color(255, 150, 70) * ((0.18f + glow * 0.06f) * t));
            float titleY = 250 - (1f - t) * 20f;
            MenuScreen.DrawLogo(spriteBatch, Game1.Logo, new Vector2(640, titleY - 52), t, glow);
            const string title = "7th Dawn";
            const int titlePixel = 5;
            var size = UITheme.MeasurePixelText(title, titlePixel);
            UITheme.DrawPixelText(spriteBatch, title, new Vector2(640 - size.X / 2f, titleY), new Color(255, 212, 160) * t, titlePixel, shadowAlpha: 0.45f * t);

            const string sub = "Seven nights. One house. Hold on to Hope.";
            float subIn = Anim.Intro(_titleTime, 1.0f, 1.2f);
            var subSize = UITheme.MeasureString(font, sub) * 0.9f;
            UITheme.DrawTextWithShadow(spriteBatch, font, sub, new Vector2(640 - subSize.X / 2f, 372), new Color(210, 195, 190) * subIn, 0.9f);

            float promptIn = Anim.Intro(_titleTime, 2.0f, 0.8f) * (0.55f + 0.45f * UITheme.PulseSine(_titleTime, 2f));
            const string prompt = "Press Enter";
            var promptSize = UITheme.MeasureString(font, prompt);
            UITheme.DrawTextWithShadow(spriteBatch, font, prompt, new Vector2(640 - promptSize.X / 2f, 470), Color.White * promptIn);
        }
    }
}
