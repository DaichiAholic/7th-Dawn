using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DuskAndDawn
{
    // Night screen: the last night. No maze, no scavenging - the Castle's doors close behind
    // you and there is one hall, one throne, and the Sun Herald on it. A few lines set the
    // scene, then you walk up to him. Win and the seventh dawn is yours; fall and the run ends.
    public partial class NightScavengingScreen
    {
        private bool _throneRoom;
        private float _throneTime;
        private bool _throneRevealed;   // skipped ahead: every line shown at once
        private Button _approachButton;

        // Set by the Herald's defeat: the collapse beat plays in gold, then dawn.
        private bool _collapseIsVictory;

        private static readonly string[] ThroneLines =
        {
            "The doors of the Castle close behind you.",
            "At the end of the hall, a second sun is rising.",
            "It has waited seven days for you."
        };

        private const float ThroneTypeSpeed = 32f;   // characters per second
        private const float ThroneLinePause = 0.7f;

        /// <summary>When each line starts typing, and when the whole intro is done.</summary>
        private (float[] starts, float done) ThroneTimeline()
        {
            var starts = new float[ThroneLines.Length];
            float t = 0.8f;
            for (int i = 0; i < ThroneLines.Length; i++)
            {
                starts[i] = t;
                t += ThroneLines[i].Length / ThroneTypeSpeed + ThroneLinePause;
            }
            return (starts, t);
        }

        private float ThroneIntroDone => ThroneTimeline().done;
        private bool ThroneReady => _throneRevealed || _throneTime >= ThroneIntroDone + 0.8f;

        private void InitializeThrone()
        {
            _approachButton = new Button(new RectangleF(490, 618, 300, 56), "Approach the throne");
            _textLog.Push($"{_dawnTimer.ClockLabel}. The last night. The Castle has only one room left that matters.");
        }

        private void UpdateThrone(float dt, MouseState mouse, bool clicked, Func<Keys, bool> keyPressed)
        {
            _throneTime += dt;
            bool ready = ThroneReady;
            _approachButton.UpdateAnimation(dt, ready && _approachButton.Contains(mouse.X, mouse.Y));

            bool advance = clicked || keyPressed(Keys.Space) || keyPressed(Keys.Enter);
            if (!advance || _leaving || ScreenTransitions.IsTransitioning) return;

            if (!ready)
            {
                // First press shows everything; the next one walks up to him.
                _throneRevealed = true;
                return;
            }
            if (clicked && !_approachButton.Contains(mouse.X, mouse.Y)) return;

            _approachButton.TriggerPress();
            _map.Hoard.Enemies = new List<Enemy> { EnemyRoster.SunHerald(_random) };
            _current = _map.Hoard;
            StartEncounter(_map.Hoard);
        }

        /// <summary>The Herald falls: a golden beat, then the seventh dawn.</summary>
        private void WinThrone()
        {
            _collapseIsVictory = true;
            _collapseText = "The Sun Herald falls to his knees, and his light goes out like a snuffed candle. For the first time in seven days, the dark feels kind.";
            _collapseTimer = CollapseDuration + 1f;
            _state = ExplorationState.Map;
        }

        // ---------- Drawing ----------

        /// <summary>The throne hall: pillars marching toward a blinding far wall, a red runner
        /// up the steps, his Knights standing guard and his choir kneeling. `herald` puts him
        /// on the throne (before the fight); during it the throne stands empty and blazing.</summary>
        private void DrawThroneHall(SpriteBatch spriteBatch, float totalSeconds, bool herald)
        {
            var center = new Vector2(640, 300);
            float pulse = UITheme.PulseSine(totalSeconds, 0.7f);
            float introLight = _state == ExplorationState.Map ? Anim.Intro(_throneTime, 0f, 2.2f) : 1f;

            Backdrop.Sky(spriteBatch, new Color(22, 14, 18), new Color(6, 4, 6));
            UITheme.DrawGlow(spriteBatch, center, 520f, new Color(255, 190, 100) * ((0.28f + pulse * 0.06f) * introLight));
            Backdrop.DrawRays(spriteBatch, center + new Vector2(0, 40), 760f, new Color(255, 214, 150) * (0.5f * introLight), totalSeconds * 0.6f, 13);

            // The runner, widening toward you.
            for (float y = 400; y < 720; y += 4)
            {
                float t = (y - 400) / 320f;
                float half = MathHelper.Lerp(38, 190, t);
                spriteBatch.FillRectangle(new RectangleF(640 - half, y, half * 2, 4.5f), Color.Lerp(new Color(110, 26, 30), new Color(52, 10, 14), t));
            }

            // The throne: a tall back with a sunburst crest, on three steps.
            for (int step = 0; step < 3; step++)
            {
                float w = 240 + step * 70, y = 382 + step * 12;
                spriteBatch.FillRectangle(new RectangleF(640 - w / 2f, y, w, 13), Color.Lerp(new Color(70, 52, 40), new Color(40, 28, 24), step / 2f));
            }
            var back = new RectangleF(582, 150, 116, 235);
            UITheme.FillRoundedRectGradient(spriteBatch, back, new Color(120, 90, 50), new Color(60, 40, 26), 18f, 8);
            UITheme.FillCircle(spriteBatch, new Vector2(640, 168), 30f, new Color(230, 190, 110));
            UITheme.FillCircle(spriteBatch, new Vector2(640, 168), 22f, new Color(255, 230, 170));

            // Pillars, near ones bigger and darker, lit on the side facing the throne.
            for (int i = 3; i >= 0; i--)
            {
                float offset = 150 + i * 125, width = 26 + i * 16;
                float top = 90 - i * 40, bottom = 470 + i * 70;
                Color stone = Color.Lerp(new Color(64, 48, 44), new Color(20, 14, 16), i / 3f);
                foreach (int side in new[] { -1, 1 })
                {
                    float x = 640 + side * offset - width / 2f;
                    spriteBatch.FillRectangle(new RectangleF(x, top, width, bottom - top), stone);
                    float litX = side < 0 ? x + width - 4 : x;
                    spriteBatch.FillRectangle(new RectangleF(litX, top, 4, bottom - top), new Color(255, 200, 120) * ((0.35f - i * 0.07f) * introLight));
                }
            }

            // His guard: Knights either side of the steps, the choir kneeling below.
            var knight = Game1.GetEnemySprite(EnemyKind.Knight);
            var penitent = Game1.GetEnemySprite(EnemyKind.Penitent);
            Color silhouette = new Color(70, 55, 50);
            if (knight != null)
            {
                UITheme.DrawPixelIconFit(spriteBatch, knight, new Vector2(460, 330), 150f, silhouette);
                UITheme.DrawPixelIconFit(spriteBatch, knight, new Vector2(820, 330), 150f, silhouette);
            }
            if (penitent != null)
            {
                UITheme.DrawPixelIconFit(spriteBatch, penitent, new Vector2(330, 520), 170f, silhouette * 0.9f);
                UITheme.DrawPixelIconFit(spriteBatch, penitent, new Vector2(950, 520), 170f, silhouette * 0.9f);
            }

            var sprite = Game1.GetEnemySprite(EnemyKind.Herald);
            if (herald && sprite != null)
            {
                float rise = Anim.Intro(_throneTime, 0.3f, 2.4f);
                float bob = MathF.Sin(totalSeconds * 1.2f) * 4f;
                var at = new Vector2(640, 300 + (1f - rise) * 40f + bob);
                UITheme.DrawGlow(spriteBatch, at + new Vector2(0, -70), 240f, new Color(255, 220, 140) * ((0.45f + pulse * 0.2f) * rise));
                UITheme.DrawPixelIconFit(spriteBatch, sprite, at, 300f, Color.White * rise);
            }

            Backdrop.Vignette(spriteBatch, Color.Black, 1.2f);
        }

        /// <summary>Before the fight: the lines type out one by one, his name comes up, then the
        /// only way forward.</summary>
        private void DrawThroneIntro(SpriteBatch spriteBatch, SpriteFont font, float totalSeconds)
        {
            var (starts, done) = ThroneTimeline();
            float time = _throneRevealed ? float.MaxValue : _throneTime;

            // A dark band behind the text so it reads over the light.
            UITheme.FillGradientRect(spriteBatch, new RectangleF(0, 470, 1280, 250), Color.Black * 0f, Color.Black * 0.85f, 10);

            float y = 480;
            for (int i = 0; i < ThroneLines.Length; i++)
            {
                if (time < starts[i]) break;
                int shown = Math.Min(ThroneLines[i].Length, (int)((time - starts[i]) * ThroneTypeSpeed));
                string text = ThroneLines[i].Substring(0, shown);
                var size = UITheme.MeasureString(font, ThroneLines[i]);
                UITheme.DrawTextWithShadow(spriteBatch, font, text, new Vector2(640 - size.X / 2f, y), new Color(240, 228, 215));
                y += 34;
            }

            // His name, burned in over the throne once the lines are done.
            float title = _throneRevealed ? 1f : Anim.Intro(_throneTime, done - 0.2f, 1f);
            if (title > 0.001f)
            {
                const string name = "THE SUN HERALD";
                var size = UITheme.MeasureString(font, name) * 2f;
                float flicker = 0.85f + 0.15f * MathF.Sin(totalSeconds * 9f);
                UITheme.DrawGlow(spriteBatch, new Vector2(640, 86), size.X * 0.7f, new Color(255, 170, 80) * (0.35f * title * flicker));
                UITheme.DrawTextWithShadow(spriteBatch, font, name, new Vector2(640 - size.X / 2f, 60 - (1f - title) * 16f), new Color(255, 232, 180) * title, 2f);
            }

            if (ThroneReady)
            {
                float intro = _throneRevealed ? 1f : Anim.Intro(_throneTime, done + 0.8f, 0.5f);
                var b = _approachButton.Bounds;
                float breathe = UITheme.PulseSine(totalSeconds, 1.4f);
                UITheme.DrawGlow(spriteBatch, new Vector2(b.X + b.Width / 2f, b.Y + b.Height / 2f), b.Width * 0.6f, new Color(255, 170, 90) * ((0.1f + breathe * 0.1f) * intro));
                EmberButton.Draw(spriteBatch, font, _approachButton, intro, primary: true);
                const string hint = "There is no way back.";
                var hintSize = UITheme.MeasureString(font, hint) * 0.62f;
                UITheme.DrawTextWithShadow(spriteBatch, font, hint, new Vector2(640 - hintSize.X / 2f, 684), new Color(200, 180, 170) * (0.7f * intro), 0.62f);
            }
            else
            {
                const string skip = "Space: continue";
                var size = UITheme.MeasureString(font, skip) * 0.58f;
                UITheme.DrawTextWithShadow(spriteBatch, font, skip, new Vector2(1240 - size.X, 690), new Color(160, 150, 150) * 0.6f, 0.58f);
            }
        }
    }
}
