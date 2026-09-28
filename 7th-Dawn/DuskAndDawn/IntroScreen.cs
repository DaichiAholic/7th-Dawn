using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using MonoGame.Extended.Screens;
using System;
using System.Collections.Generic;

namespace DuskAndDawn
{
    /// <summary>
    /// The story so far, told before a new run: a framed picture over black, the text typing
    /// out beneath it, one slide fading into the next - then the title. Each picture is drawn
    /// small (320x180) and scaled up without smoothing, so it reads as old pixel art, and
    /// toned sepia like a faded photograph.
    /// Space / Enter / click finishes the line or moves on; Esc skips the whole thing.
    /// </summary>
    public class IntroScreen : GameScreen
    {
        private Game1 Game1 => (Game1)Game;

        private const int SceneWidth = 320, SceneHeight = 180;
        private const float TypeSpeed = 26f;     // characters per second
        private const float TypeDelay = 0.6f;    // after the picture starts fading in
        private const float FadeIn = 0.8f, FadeOut = 0.7f, Hold = 2.6f;
        private static readonly Rectangle PictureBox = new Rectangle(320, 48, 640, 360);

        private static readonly BlendState Multiply = new BlendState
        {
            ColorSourceBlend = Blend.DestinationColor,
            ColorDestinationBlend = Blend.Zero,
            AlphaSourceBlend = Blend.Zero,
            AlphaDestinationBlend = Blend.One
        };

        private class Slide
        {
            public string Text;
            public Action<SpriteBatch, float> Draw;
        }

        private readonly List<Slide> _slides = new List<Slide>();
        private RenderTarget2D _scene;
        private int _index;
        private float _slideTime;
        private float _titleTime = -1f;   // >= 0 once the slides are done and the title is up
        private bool _finished;
        private KeyboardState _previousKeyboard;
        private MouseState _previousMouse;
        private readonly Random _stars = new Random(7);
        private readonly Vector2[] _starPoints = new Vector2[60];

        public IntroScreen(Game game) : base(game)
        {
            for (int i = 0; i < _starPoints.Length; i++)
            {
                _starPoints[i] = new Vector2(_stars.Next(0, 1280), _stars.Next(0, 420));
            }

            Add("Long ago, the sun kept the world warm, and the world kept its faith in the sun.", DrawSunrise);
            Add("Then the Sun Herald came down out of the light. And the light began to burn.", DrawHeraldDescends);
            Add("The faithful knelt, and burned with him. The proud put on iron, and guarded his halls.", DrawHisServants);
            Add("The rest of us hid from the day, and learned to live by night.", DrawNight);
            Add("One house still stands. Each night, someone goes out into the ruins. Each morning, the house waits to see who comes home.", DrawHouse);
            Add("Hold on for seven days. Build the house. Keep its hope alive - if hope runs out, it is over.", DrawTally);
            Add("And on the seventh night, the Castle opens its doors. The Sun Herald is waiting for his dawn.", DrawCastle);
        }

        private void Add(string text, Action<SpriteBatch, float> draw) => _slides.Add(new Slide { Text = text, Draw = draw });

        public override void Initialize()
        {
            base.Initialize();
            // A key or click still held from the menu shouldn't skip the first slide.
            _previousKeyboard = Keyboard.GetState();
            _previousMouse = InputChecker.GetMouse();
        }

        public override void UnloadContent()
        {
            _scene?.Dispose();
            base.UnloadContent();
        }

        // ---------- Timing ----------

        private float TypingDone(Slide slide) => TypeDelay + slide.Text.Length / TypeSpeed;
        private float SlideEnd(Slide slide) => TypingDone(slide) + Hold;

        public override void Update(GameTime gameTime)
        {
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            var keyboard = Keyboard.GetState();
            var mouse = InputChecker.GetMouse();
            bool Pressed(Keys key) => keyboard.IsKeyDown(key) && !_previousKeyboard.IsKeyDown(key);
            bool advance = Pressed(Keys.Space) || Pressed(Keys.Enter) || Pressed(Keys.Z)
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

            var slide = _slides[_index];
            _slideTime += dt;
            if (advance)
            {
                // First press finishes the line; the next one moves on.
                if (_slideTime < TypingDone(slide)) _slideTime = TypingDone(slide);
                else if (_slideTime < SlideEnd(slide)) _slideTime = SlideEnd(slide);
            }

            if (_slideTime >= SlideEnd(slide) + FadeOut)
            {
                _index++;
                _slideTime = 0f;
                if (_index >= _slides.Count)
                {
                    _index = _slides.Count - 1;
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

        // ---------- Drawing ----------

        public override void Draw(GameTime gameTime)
        {
            var device = GraphicsDevice;
            var spriteBatch = Game1.SpriteBatch;
            var font = Game1.Font;
            var slide = _slides[_index];
            bool onTitle = _titleTime >= 0f;

            // 1. The picture, drawn small into its own target.
            if (!onTitle)
            {
                _scene ??= new RenderTarget2D(device, SceneWidth, SceneHeight);
                var previous = device.GetRenderTargets();
                device.SetRenderTarget(_scene);
                device.Clear(Color.Black);
                spriteBatch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: Matrix.CreateScale(SceneWidth / 1280f));
                slide.Draw(spriteBatch, _slideTime);
                spriteBatch.End();
                // Sepia, like a photograph left in the sun.
                spriteBatch.Begin(blendState: Multiply);
                spriteBatch.FillRectangle(new RectangleF(0, 0, SceneWidth, SceneHeight), new Color(255, 226, 186));
                spriteBatch.End();
                device.SetRenderTargets(previous);
            }

            // 2. Black, the picture scaled up crisp, the words beneath it.
            device.Clear(Color.Black);
            if (!onTitle)
            {
                float alpha = Math.Min(Anim.Intro(_slideTime, 0f, FadeIn), 1f - MathHelper.Clamp((_slideTime - SlideEnd(slide)) / FadeOut, 0f, 1f));
                UITheme.BeginCanvas(spriteBatch, SamplerState.PointClamp);
                spriteBatch.Draw(_scene, PictureBox, Color.White * alpha);
                spriteBatch.End();

                UITheme.BeginCanvas(spriteBatch);
                DrawTypedText(spriteBatch, font, slide, alpha);
            }
            else
            {
                UITheme.BeginCanvas(spriteBatch);
                DrawTitle(spriteBatch, font);
            }

            const string skip = "Esc: skip";
            var skipSize = UITheme.MeasureString(font, skip) * 0.58f;
            UITheme.DrawTextWithShadow(spriteBatch, font, skip, new Vector2(1250 - skipSize.X, 690), new Color(120, 115, 115) * 0.7f, 0.58f);
            spriteBatch.End();
        }

        private void DrawTypedText(SpriteBatch spriteBatch, SpriteFont font, Slide slide, float alpha)
        {
            const float scale = 1.1f, lineHeight = 34f;
            var lines = TextLog.WrapText(font, slide.Text, PictureBox.Width / scale);
            int remaining = (int)MathF.Max(0f, (_slideTime - TypeDelay) * TypeSpeed);
            float y = PictureBox.Bottom + 34;
            foreach (var line in lines)
            {
                if (remaining <= 0) break;
                string shown = remaining >= line.Length ? line : line.Substring(0, remaining);
                remaining -= line.Length + 1;
                UITheme.DrawTextWithShadow(spriteBatch, font, shown, new Vector2(PictureBox.X, y), Color.White * alpha, scale);
                y += lineHeight;
            }
        }

        private void DrawTitle(SpriteBatch spriteBatch, SpriteFont font)
        {
            float t = Anim.Intro(_titleTime, 0.2f, 1.6f);
            float glow = UITheme.PulseSine(_titleTime, 0.8f);
            UITheme.DrawGlow(spriteBatch, new Vector2(640, 300), 420f, new Color(255, 150, 70) * ((0.18f + glow * 0.06f) * t));
            const string title = "7th Dawn";
            var size = UITheme.MeasureString(font, title) * 3.2f;
            UITheme.DrawTextWithShadow(spriteBatch, font, title, new Vector2(640 - size.X / 2f, 250 - (1f - t) * 20f), new Color(255, 212, 160) * t, 3.2f);

            const string sub = "Seven nights. One house. Hold on to Hope.";
            float subIn = Anim.Intro(_titleTime, 1.0f, 1.2f);
            var subSize = UITheme.MeasureString(font, sub) * 0.9f;
            UITheme.DrawTextWithShadow(spriteBatch, font, sub, new Vector2(640 - subSize.X / 2f, 372), new Color(210, 195, 190) * subIn, 0.9f);

            float promptIn = Anim.Intro(_titleTime, 2.0f, 0.8f) * (0.55f + 0.45f * UITheme.PulseSine(_titleTime, 2f));
            const string prompt = "Press Enter";
            var promptSize = UITheme.MeasureString(font, prompt);
            UITheme.DrawTextWithShadow(spriteBatch, font, prompt, new Vector2(640 - promptSize.X / 2f, 470), Color.White * promptIn);
        }

        // ---------- The pictures (drawn in 1280x720 space, shrunk into 320x180) ----------

        private void DrawSprite(SpriteBatch spriteBatch, Texture2D texture, Vector2 center, float scale, Color color)
        {
            if (texture == null) return;
            spriteBatch.Draw(texture, center, null, color, 0f, new Vector2(texture.Width / 2f, texture.Height / 2f), scale, SpriteEffects.None, 0f);
        }

        private void DrawStars(SpriteBatch spriteBatch, float t, float alpha)
        {
            for (int i = 0; i < _starPoints.Length; i++)
            {
                float twinkle = 0.5f + 0.5f * MathF.Sin(t * 1.5f + i);
                spriteBatch.FillRectangle(new RectangleF(_starPoints[i].X, _starPoints[i].Y, 4, 4), Color.White * (alpha * twinkle));
            }
        }

        private void DrawSunrise(SpriteBatch spriteBatch, float t)
        {
            Backdrop.Sky(spriteBatch, new Color(250, 200, 140), new Color(230, 140, 90));
            var sun = new Vector2(640, 470 - t * 10f);
            UITheme.DrawGlow(spriteBatch, sun, 520f, new Color(255, 236, 190) * 0.6f);
            Backdrop.DrawRays(spriteBatch, sun, 900f, new Color(255, 245, 210), t, 11);
            UITheme.FillCircle(spriteBatch, sun, 120f, new Color(255, 245, 215));
            Backdrop.DrawSkyline(spriteBatch, 560, 190, new Color(140, 90, 70), seed: 7, drift: t * 4f);
            Backdrop.DrawSkyline(spriteBatch, 620, 110, new Color(80, 50, 40), seed: 21, drift: t * 8f);
        }

        private void DrawHeraldDescends(SpriteBatch spriteBatch, float t)
        {
            Backdrop.Sky(spriteBatch, new Color(255, 170, 90), new Color(150, 40, 30));
            float descend = Anim.Intro(t, 0f, 4f);
            var at = new Vector2(640, 150 + descend * 170f);
            UITheme.DrawGlow(spriteBatch, at, 600f, new Color(255, 230, 160) * 0.7f);
            Backdrop.DrawRays(spriteBatch, at + new Vector2(0, 100), 1000f, new Color(255, 240, 200), t * 2f, 13);
            DrawSprite(spriteBatch, Game1.GetEnemySprite(EnemyKind.Herald), at, 4f, Color.White);
            Backdrop.DrawSkyline(spriteBatch, 600, 170, new Color(110, 30, 20), seed: 7, drift: 0f);
            Backdrop.DrawSkyline(spriteBatch, 650, 100, new Color(50, 12, 10), seed: 21, drift: 0f);
        }

        private void DrawHisServants(SpriteBatch spriteBatch, float t)
        {
            Backdrop.Sky(spriteBatch, new Color(90, 40, 30), new Color(20, 8, 8));
            UITheme.DrawGlow(spriteBatch, new Vector2(640, -60), 700f, new Color(255, 200, 120) * 0.5f);
            Backdrop.DrawRays(spriteBatch, new Vector2(640, -40), 900f, new Color(255, 220, 150) * 0.7f, t, 9);
            spriteBatch.FillRectangle(new RectangleF(0, 600, 1280, 120), new Color(30, 14, 12));
            float pan = t * 6f;
            DrawSprite(spriteBatch, Game1.GetEnemySprite(EnemyKind.Penitent), new Vector2(390 + pan, 450), 3.6f, Color.White);
            DrawSprite(spriteBatch, Game1.GetEnemySprite(EnemyKind.Knight), new Vector2(890 - pan, 400), 3.6f, Color.White);
        }

        private void DrawNight(SpriteBatch spriteBatch, float t)
        {
            Backdrop.Sky(spriteBatch, new Color(24, 26, 52), new Color(8, 8, 18));
            DrawStars(spriteBatch, t, 0.8f);
            UITheme.DrawGlow(spriteBatch, new Vector2(1010, 150), 160f, new Color(220, 225, 255) * 0.35f);
            UITheme.FillCircle(spriteBatch, new Vector2(1010, 150), 56f, new Color(225, 228, 240));
            Backdrop.DrawSkyline(spriteBatch, 560, 190, new Color(34, 34, 56), seed: 11, drift: 0f);
            Backdrop.DrawSkyline(spriteBatch, 630, 110, new Color(14, 14, 24), seed: 29, drift: 0f);
            // A lantern picking its way through the ruins.
            var lantern = new Vector2(180 + t * 70f, 640 + MathF.Sin(t * 3f) * 4f);
            UITheme.DrawGlow(spriteBatch, lantern, 120f, new Color(255, 190, 110) * 0.6f);
            UITheme.FillCircle(spriteBatch, lantern, 10f, new Color(255, 225, 160));
        }

        private void DrawHouse(SpriteBatch spriteBatch, float t)
        {
            Backdrop.Sky(spriteBatch, new Color(28, 26, 48), new Color(10, 9, 18));
            DrawStars(spriteBatch, t, 0.6f);
            spriteBatch.FillRectangle(new RectangleF(0, 620, 1280, 100), new Color(18, 14, 18));
            // The house: timber walls, a pitched roof, a chimney, and two windows still lit.
            spriteBatch.FillRectangle(new RectangleF(760, 190, 50, 110), new Color(40, 28, 26));
            Backdrop.FillTriangle(spriteBatch, new Vector2(640, 200), 360, 290, new Color(58, 38, 36));
            spriteBatch.FillRectangle(new RectangleF(420, 358, 440, 262), new Color(70, 50, 44));
            float flicker = 0.85f + 0.15f * MathF.Sin(t * 7f);
            foreach (float x in new[] { 500f, 700f })
            {
                UITheme.DrawGlow(spriteBatch, new Vector2(x + 40, 450), 140f, new Color(255, 190, 110) * (0.45f * flicker));
                spriteBatch.FillRectangle(new RectangleF(x, 410, 80, 80), new Color(255, 205, 130) * flicker);
                spriteBatch.FillRectangle(new RectangleF(x + 37, 410, 6, 80), new Color(70, 50, 44));
            }
            spriteBatch.FillRectangle(new RectangleF(610, 520, 60, 100), new Color(40, 28, 26));
        }

        private void DrawTally(SpriteBatch spriteBatch, float t)
        {
            // Seven marks scratched into the wall, one after another.
            Backdrop.Sky(spriteBatch, new Color(80, 60, 50), new Color(40, 28, 24));
            UITheme.DrawGlow(spriteBatch, new Vector2(640, 640), 400f, new Color(255, 180, 100) * 0.35f);
            Color mark = new Color(235, 220, 200);
            int shown = (int)MathHelper.Clamp((t - 0.4f) / 0.45f, 0f, 7f);
            for (int i = 0; i < shown; i++)
            {
                if (i == 4)
                {
                    spriteBatch.DrawLine(new Vector2(330, 500), new Vector2(640, 220), mark, 12f);
                    continue;
                }
                float x = i < 4 ? 360 + i * 80 : 760 + (i - 5) * 80;
                spriteBatch.DrawLine(new Vector2(x, 210), new Vector2(x + 8, 510), mark, 12f);
            }
            // A candle below the marks.
            float flame = MathF.Sin(t * 9f) * 3f;
            spriteBatch.FillRectangle(new RectangleF(1000, 520, 30, 90), new Color(230, 220, 200));
            UITheme.DrawGlow(spriteBatch, new Vector2(1015, 500 + flame), 90f, new Color(255, 200, 120) * 0.8f);
            UITheme.FillCircle(spriteBatch, new Vector2(1015, 500 + flame), 12f, new Color(255, 230, 170));
        }

        private void DrawCastle(SpriteBatch spriteBatch, float t)
        {
            // The camera drifts up as a second sun rises behind the Castle.
            float rise = Anim.Intro(t, 0f, 5f);
            Backdrop.Sky(spriteBatch, Color.Lerp(new Color(30, 20, 40), new Color(120, 60, 50), rise), Color.Lerp(new Color(80, 40, 40), new Color(250, 170, 100), rise));
            var sun = new Vector2(640, 520 - rise * 200f);
            UITheme.DrawGlow(spriteBatch, sun, 520f, new Color(255, 225, 160) * (0.3f + rise * 0.4f));
            Backdrop.DrawRays(spriteBatch, sun, 900f, new Color(255, 235, 190) * rise, t, 12);
            UITheme.FillCircle(spriteBatch, sun, 90f, new Color(255, 240, 200));
            spriteBatch.FillRectangle(new RectangleF(0, 600, 1280, 120), new Color(30, 18, 20));
            DrawSprite(spriteBatch, Game1.GetDistrictIcon(District.Castle), new Vector2(640, 470), 14f, Color.White);
        }
    }
}
