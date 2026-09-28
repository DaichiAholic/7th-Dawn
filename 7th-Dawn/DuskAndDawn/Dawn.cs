using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using MonoGame.Extended.Screens;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DuskAndDawn
{
    public class Dawn : GameScreen, IGameplayScreen
    {
        private Game1 Game1 => (Game1)Game;
        private readonly PlayerState _playerState;
        private readonly int _roomsCleared;
        private readonly int _roomsVisited;

        // What the Storage cap cut off from tonight's haul.
        private readonly int _lostFood, _lostPlanks, _lostScraps;

        private Button _continueButton;
        private MouseState _previousMouse;

        public Dawn(Game game, PlayerState playerState, int roomsCleared, int roomsVisited) : base(game)
        {
            _playerState = playerState;
            _roomsCleared = roomsCleared;
            _roomsVisited = roomsVisited;

            // The night's haul can exceed Storage - this is the moment the overflow is lost.
            // Done in the constructor so it runs exactly once per dawn.
            (_lostFood, _lostPlanks, _lostScraps) = _playerState.ApplyStorageCap();
        }

        // Dawn sparkles drifting up, and the tallies counting up as the screen settles.
        private readonly ParticleField _sparkles = new ParticleField(45, new RectangleF(0, 200, 1280, 520), new Vector2(-5, -24), new Vector2(5, -10),
            1.2f, 3f, 4f, 8f, new Color(255, 235, 190), new Color(255, 200, 130), wobble: 8f, spawnAtBottom: true);
        private readonly CountUp _food = new CountUp(0f, 30f), _planks = new CountUp(0f, 30f), _scraps = new CountUp(0f, 30f), _rooms = new CountUp(0f, 8f);
        private float _elapsed;

        private const float PanelAt = 0.8f, ButtonAt = 1.4f;

        public override void Initialize()
        {
            base.Initialize();

            // Seeds with the real current mouse state instead of a blank default, so a click
            // still held down from the previous screen doesn't read as a brand-new click here.
            _previousMouse = InputChecker.GetMouse();

            // After the last night there is no next morning - this dawn is the win.
            string label = IsFinalDawn ? "Greet the Seventh Dawn" : "Continue to Morning";
            _continueButton = new Button(new RectangleF(480, 520, 320, 64), label);
        }

        public override void Update(GameTime gameTime)
        {
            if (_playerState.IsGameOver)
            {
                Game1.EndRun(victory: false);
                return;
            }

            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            _elapsed += dt;
            _sparkles.Update(dt);
            if (_elapsed > PanelAt)
            {
                _rooms.Update(dt, _roomsCleared);
                _food.Update(dt, _playerState.Food);
                _planks.Update(dt, _playerState.Planks);
                _scraps.Update(dt, _playerState.Scraps);
            }

            var mouse = InputChecker.GetMouse();
            bool ready = _elapsed > ButtonAt;
            bool isHovered = ready && _continueButton.Contains(mouse.X, mouse.Y);
            _continueButton.UpdateAnimation(dt, isHovered);

            if (InputChecker.IsNewLeftClick(mouse, _previousMouse) && isHovered && !ScreenTransitions.IsTransitioning)
            {
                _continueButton.TriggerPress();
                if (IsFinalDawn)
                {
                    Game1.EndRun(victory: true);
                }
                else
                {
                    ScreenManager.ReplaceScreen(new DawnEventsScreen(Game, _playerState), ScreenTransitions.FadeTransition(GraphicsDevice));
                }
            }
            _previousMouse = mouse;
        }

        private bool IsFinalDawn => DayInfo.IsFinalNight(_playerState.Day);

        public override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(new Color(224, 178, 128));

            var spriteBatch = Game1.SpriteBatch;
            var font = Game1.Font;
            UITheme.BeginCanvas(spriteBatch);

            DrawSunrise(spriteBatch);

            string headline = IsFinalDawn ? "The seventh dawn breaks." : $"Dawn. Night {_playerState.Day} of {DayInfo.FinalDay} survived.";
            string subline = IsFinalDawn
                ? (_playerState.KnightSlain ? "The Hollow Knight is dead, and the light comes up gold." : "You made it. The house is still standing.")
                : $"{DayInfo.FinalDay - _playerState.Day} more night{(DayInfo.FinalDay - _playerState.Day == 1 ? "" : "s")} until the seventh dawn.";

            float titleIn = Anim.Intro(_elapsed, 0.25f, 0.7f);
            var headSize = UITheme.MeasureString(font, headline) * 1.4f;
            UITheme.DrawTextWithShadow(spriteBatch, font, headline, new Vector2(640 - headSize.X / 2f, 96 - (1f - titleIn) * 24f), new Color(70, 34, 18) * titleIn, 1.4f, shadowAlpha: 0.2f * titleIn);
            float subIn = Anim.Intro(_elapsed, 0.55f, 0.7f);
            var subSize = UITheme.MeasureString(font, subline);
            UITheme.DrawTextWithShadow(spriteBatch, font, subline, new Vector2(640 - subSize.X / 2f, 150), new Color(100, 54, 28) * subIn, 1f, shadowAlpha: 0.15f * subIn);

            DrawTally(spriteBatch, font);

            float buttonIn = Anim.Intro(_elapsed, ButtonAt, 0.45f);
            if (buttonIn > 0.99f)
            {
                // A slow breathing glow once it's ready, so it reads as the way forward.
                float breathe = UITheme.PulseSine(_elapsed, 1.6f);
                var b = _continueButton.Bounds;
                UITheme.DrawGlow(spriteBatch, new Vector2(b.X + b.Width / 2f, b.Y + b.Height / 2f), b.Width * 0.62f, new Color(255, 170, 90) * (0.12f + breathe * 0.1f));
            }
            EmberButton.Draw(spriteBatch, font, _continueButton, buttonIn, primary: true);

            spriteBatch.End();
        }

        /// <summary>Night gives way: the sky warms from indigo to gold over the first seconds
        /// while the sun climbs out from behind the ruins.</summary>
        private void DrawSunrise(SpriteBatch spriteBatch)
        {
            float rise = UITheme.EaseOutCubic(MathHelper.Clamp(_elapsed / 2.6f, 0f, 1f));
            Backdrop.Sky(spriteBatch,
                Color.Lerp(new Color(40, 34, 70), new Color(255, 212, 160), rise),
                Color.Lerp(new Color(120, 70, 90), new Color(236, 150, 110), rise));

            var sun = new Vector2(640, 720 - rise * 150f);
            UITheme.DrawGlow(spriteBatch, sun, 560f, new Color(255, 236, 190) * (0.25f + rise * 0.35f));
            Backdrop.DrawRays(spriteBatch, sun, 900f, new Color(255, 240, 200) * (0.7f * rise), _elapsed, 12);
            UITheme.FillCircle(spriteBatch, sun, 70f, new Color(255, 244, 214));

            float drift = MathF.Sin(_elapsed * 0.05f);
            Backdrop.DrawSkyline(spriteBatch, 630, 160, Color.Lerp(new Color(60, 40, 60), new Color(200, 130, 110), rise * 0.6f), seed: 7, drift: drift * 5f);
            Backdrop.DrawSkyline(spriteBatch, 670, 90, Color.Lerp(new Color(30, 20, 34), new Color(120, 70, 66), rise * 0.6f), seed: 21, drift: drift * 10f);
            _sparkles.Draw(spriteBatch, rise);
        }

        /// <summary>The night's tally: rooms resolved as a filling bar, then the stores, each
        /// tile popping in with its count running up to the real total.</summary>
        private void DrawTally(SpriteBatch spriteBatch, SpriteFont font)
        {
            float panelIn = Anim.Intro(_elapsed, PanelAt - 0.3f, 0.5f);
            if (panelIn <= 0.001f) return;
            var panel = Anim.Slide(new RectangleF(290, 200, 700, 280), panelIn, new Vector2(0, 30));
            UITheme.DrawPanel(spriteBatch, panel, new Color(70, 45, 35) * panelIn, new Color(44, 28, 22) * panelIn, new Color(190, 130, 80) * panelIn, 3f, 16f, shadowStrength: 0.7f * panelIn);

            // Rooms resolved, as a bar that fills.
            float x = panel.X + 32, y = panel.Y + 24;
            UITheme.DrawTextWithShadow(spriteBatch, font, $"Rooms resolved  {_rooms.Value} / {_roomsVisited}", new Vector2(x, y), Color.White * panelIn, 0.9f);
            var bar = new RectangleF(x, y + 34, panel.Width - 64, 12);
            UITheme.FillRoundedRect(spriteBatch, bar, Color.Black * (0.4f * panelIn), 6f);
            float fill = _roomsVisited > 0 ? MathHelper.Clamp(_rooms.Value / (float)_roomsVisited, 0f, 1f) : 0f;
            if (fill > 0.01f)
            {
                UITheme.FillRoundedRect(spriteBatch, new RectangleF(bar.X, bar.Y, bar.Width * fill, bar.Height), new Color(240, 170, 90) * panelIn, 6f);
            }

            // The stores, one tile each.
            var textures = new[] { Game1.BreadTexture, Game1.PlanksTexture, Game1.ScrapsTexture };
            string[] labels = { "Food", "Planks", "Scraps" };
            int[] values = { _food.Value, _planks.Value, _scraps.Value };
            const float tileW = 196, tileH = 104, tileGap = 22;
            float tileX = panel.X + (panel.Width - (tileW * 3 + tileGap * 2)) / 2f;
            for (int i = 0; i < 3; i++)
            {
                float t = Anim.Stagger(_elapsed, i, step: 0.12f, baseDelay: PanelAt, duration: 0.4f);
                if (t > 0.001f)
                {
                    var tile = Anim.Scale(new RectangleF(tileX + i * (tileW + tileGap), panel.Y + 92, tileW, tileH), MathHelper.Lerp(0.8f, 1f, UITheme.EaseOutBack(t)));
                    UITheme.DrawPanel(spriteBatch, tile, new Color(90, 62, 48) * t, new Color(62, 42, 32) * t, new Color(150, 105, 70) * t, 2f, 12f, shadowStrength: 0.4f * t);
                    var texture = textures[i];
                    if (texture != null)
                    {
                        spriteBatch.Draw(texture, new Vector2(tile.X + 46, tile.Y + tile.Height / 2f), null, Color.White * t, 0f,
                            new Vector2(texture.Width / 2f, texture.Height / 2f), 0.2f, SpriteEffects.None, 0f);
                    }
                    bool atCap = values[i] >= _playerState.StorageCap;
                    UITheme.DrawTextWithShadow(spriteBatch, font, values[i].ToString(), new Vector2(tile.X + 92, tile.Y + 20), (atCap ? new Color(255, 200, 120) : Color.White) * t, 1.4f);
                    UITheme.DrawTextWithShadow(spriteBatch, font, labels[i], new Vector2(tile.X + 94, tile.Y + 64), new Color(220, 200, 185) * t, 0.75f);
                }
            }

            // Storage verdict, last.
            float verdictIn = Anim.Intro(_elapsed, PanelAt + 0.5f, 0.4f);
            string storageLine = LostLabel();
            Color storageColor = storageLine == null ? new Color(170, 225, 170) : new Color(255, 160, 140);
            storageLine ??= $"Everything fit in Storage (max {_playerState.StorageCap} each).";
            var size = UITheme.MeasureString(font, storageLine) * 0.8f;
            UITheme.DrawTextWithShadow(spriteBatch, font, storageLine, new Vector2(panel.X + (panel.Width - size.X) / 2f, panel.Bottom - 50), storageColor * verdictIn, 0.8f);
        }

        /// <summary>"Storage overflowed - lost 4 Food, 2 Scraps." or null if nothing was lost.</summary>
        private string LostLabel()
        {
            var parts = new List<string>();
            if (_lostFood > 0) parts.Add($"{_lostFood} Food");
            if (_lostPlanks > 0) parts.Add($"{_lostPlanks} Planks");
            if (_lostScraps > 0) parts.Add($"{_lostScraps} Scraps");
            return parts.Count > 0 ? "Storage overflowed - lost " + string.Join(", ", parts) + "." : null;
        }
    }
}