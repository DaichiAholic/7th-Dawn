using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using MonoGame.Extended.Screens;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DuskAndDawn
{
    // Night screen: everything drawn - HUD, clock, maze, tooltips, combat and supply panels.
    public partial class NightScavengingScreen
    {
        // HUD cards
        private static readonly RectangleF StatusCard = new RectangleF(18, 12, 400, 198);
        private static readonly RectangleF InfoCard = new RectangleF(434, 12, 548, 112);

        // ---------- Draw ----------

        public override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(new Color(8, 8, 14)); // night: dark and safe by design

            var spriteBatch = Game1.SpriteBatch;
            var font = Game1.Font;
            float totalSeconds = (float)gameTime.TotalGameTime.TotalSeconds;
            UITheme.BeginCanvas(spriteBatch);

            if (_state != _shownState)
            {
                _shownState = _state;
                _stateTime = 0f;
            }

            // Subtle gradient instead of a flat fill - just enough depth to read as a night
            // sky rather than a solid color swatch, while staying dark and calm by design.
            Backdrop.Sky(spriteBatch, new Color(16, 15, 28), new Color(4, 4, 8));
            DrawEmbers(spriteBatch);

            DrawHud(spriteBatch, font, totalSeconds);

            switch (_state)
            {
                case ExplorationState.Map:
                    DrawMaze(spriteBatch, font, totalSeconds);
                    break;
                case ExplorationState.Encounter:
                    DrawCombat(spriteBatch, font, totalSeconds);
                    // A long Items list needs the space the mini-map uses.
                    if (_combatMenu != CombatMenu.Items) DrawMapFragment(spriteBatch, font);
                    break;
                case ExplorationState.Supplies:
                    DrawSupplies(spriteBatch, font, totalSeconds);
                    DrawMapFragment(spriteBatch, font);
                    break;
                case ExplorationState.Event:
                    DrawEvent(spriteBatch, font, totalSeconds);
                    DrawMapFragment(spriteBatch, font);
                    break;
            }

            // Combat animations go on top of everything, centered on the screen - only while
            // a fight is actually on (EndCombat also stops them).
            if (_state == ExplorationState.Encounter)
            {
                // A red pulse at the screen's edges while a HEAVY or STUN is wound up - a
                // warning you feel before you read it.
                if (_activeCombat.Enemies.Any(e => !e.IsDefeated && e.IntentIsThreat))
                {
                    float alarm = 0.55f + 0.45f * UITheme.PulseSine(totalSeconds, 3f);
                    Backdrop.Vignette(spriteBatch, new Color(200, 30, 20), alarm * Anim.Intro(_stateTime, 0.3f, 0.5f));
                }
                _castEffect.Draw(spriteBatch);
                _playerHitFlash.Draw(spriteBatch);
            }

            if (_collapseTimer >= 0f)
            {
                DrawCollapse(spriteBatch, font);
            }

            spriteBatch.End();
        }

        private void DrawCollapse(SpriteBatch spriteBatch, SpriteFont font)
        {
            float fade = MathHelper.Clamp((CollapseDuration - _collapseTimer) / 0.6f, 0f, 1f);
            UITheme.FillGradientRect(spriteBatch, new RectangleF(0, 0, 1280, 720), new Color(40, 0, 0) * (0.75f * fade), Color.Black * (0.85f * fade), 6);

            var lines = TextLog.WrapText(font, _collapseText, 760f);
            for (int i = 0; i < lines.Count; i++)
            {
                var size = UITheme.MeasureString(font, lines[i]);
                UITheme.DrawTextWithShadow(spriteBatch, font, lines[i], new Vector2(640 - size.X / 2f, 330 + i * 30), new Color(255, 200, 190) * fade);
            }
        }

        private void DrawEmbers(SpriteBatch spriteBatch)
        {
            foreach (var ember in _embers)
            {
                float alpha = ember.Alpha;
                UITheme.DrawGlow(spriteBatch, ember.Position, ember.Size * 3f, new Color(255, 120, 50) * (0.25f * alpha));
                UITheme.FillCircle(spriteBatch, ember.Position, ember.Size * 0.6f, new Color(255, 190, 120) * (0.7f * alpha));
            }
        }

        // ---------- HUD ----------

        private void DrawHud(SpriteBatch spriteBatch, SpriteFont font, float totalSeconds)
        {
            // Two frosted cards instead of loose text on the background: your own status on
            // the left, where you are / what you're carrying in the middle.
            UITheme.DrawPanel(spriteBatch, StatusCard, new Color(26, 24, 38) * 0.92f, new Color(16, 15, 24) * 0.92f, new Color(80, 72, 100), 1.5f, 14f, shadowStrength: 0.6f);
            UITheme.DrawPanel(spriteBatch, InfoCard, new Color(26, 24, 38) * 0.92f, new Color(16, 15, 24) * 0.92f, new Color(80, 72, 100), 1.5f, 14f, shadowStrength: 0.6f);

            DrawClock(spriteBatch, font, totalSeconds);
            string weaponLine = $"Weapon: {_playerState.EquippedWeapon.DisplayName} ({_playerState.EquippedWeapon.DiceLabel})";
            UITheme.DrawTextWithShadow(spriteBatch, font, weaponLine, new Vector2(40, 100), Color.LightGray);
            // 1x icon just after the weapon line - small, but crisp at native size.
            float weaponLineWidth = UITheme.MeasureString(font, weaponLine).X;
            UITheme.DrawPixelIcon(spriteBatch, Game1.GetWeaponIcon(_playerState.EquippedWeapon), new Vector2(40 + weaponLineWidth + 8, 94), 1);
            DrawPlayerHealthBar(spriteBatch, font);

            // District + corruption, with a pip per corruption tier that glows like embers.
            var infoX = InfoCard.X + 18;
            UITheme.DrawTextWithShadow(spriteBatch, font, DistrictInfo.Name(_district), new Vector2(infoX, InfoCard.Y + 12), new Color(255, 180, 120));
            string night = $"Night {_playerState.Day}/{DayInfo.FinalDay}";
            var nightSize = UITheme.MeasureString(font, night) * 0.8f;
            UITheme.DrawTextWithShadow(spriteBatch, font, night, new Vector2(InfoCard.Right - nightSize.X - 16, InfoCard.Y + 14),
                DayInfo.IsFinalNight(_playerState.Day) ? new Color(255, 200, 110) : new Color(200, 195, 220), 0.8f);
            float pipX = infoX + UITheme.MeasureString(font, DistrictInfo.Name(_district)).X + 20;
            int corruption = DistrictInfo.Corruption(_district);
            for (int i = 0; i < 3; i++)
            {
                var pip = new Vector2(pipX + i * 20, InfoCard.Y + 24);
                if (i < corruption)
                {
                    float glow = UITheme.PulseSine(totalSeconds + i * 0.6f, 2.2f);
                    UITheme.DrawGlow(spriteBatch, pip, 14f, new Color(255, 110, 40) * (0.35f + glow * 0.3f));
                    UITheme.FillCircle(spriteBatch, pip, 6f, new Color(255, 150, 70));
                }
                else
                {
                    UITheme.FillCircle(spriteBatch, pip, 6f, new Color(60, 54, 70));
                }
            }
            UITheme.DrawTextWithShadow(spriteBatch, font, "Corruption", new Vector2(pipX + 64, InfoCard.Y + 14), new Color(200, 170, 150), 0.8f);

            // Tonight's haul so far - icons from the existing resource art.
            float resourceY = InfoCard.Y + 48;
            float x = infoX;
            x = DrawResourceCounter(spriteBatch, font, Game1.BreadTexture, _playerState.Food, "Food", x, resourceY);
            x = DrawResourceCounter(spriteBatch, font, Game1.PlanksTexture, _playerState.Planks, "Planks", x, resourceY);
            DrawResourceCounter(spriteBatch, font, Game1.ScrapsTexture, _playerState.Scraps, "Scraps", x, resourceY);

            int explored = _map.Nodes.Count(n => n.Visited && n.Type != RoomType.Entrance);
            UITheme.DrawTextWithShadow(spriteBatch, font,
                $"Rooms explored {explored}/{_map.Nodes.Count - 1}     Deepest {DeepestVisited()}/{_map.MaxDepth}",
                new Vector2(infoX, InfoCard.Y + 84), new Color(190, 185, 210), 0.8f);
        }

        private int DeepestVisited() => _map.Nodes.Where(n => n.Visited).Select(n => n.Depth).DefaultIfEmpty(0).Max();

        private float DrawResourceCounter(SpriteBatch spriteBatch, SpriteFont font, Texture2D icon, int amount, string name, float x, float y)
        {
            const float iconSize = 28f;
            if (icon != null)
            {
                spriteBatch.Draw(icon, new Rectangle((int)x, (int)y, (int)iconSize, (int)iconSize), Color.White);
            }
            string text = $"{amount} {name}";
            UITheme.DrawTextWithShadow(spriteBatch, font, text, new Vector2(x + iconSize + 6, y + 2), Color.White, 0.9f);
            return x + iconSize + 6 + UITheme.MeasureString(font, text).X * 0.9f + 26f;
        }

        // Analog clock face in the status card: the night's arc runs from dusk at 8 o'clock,
        // clockwise over midnight, to dawn at 6 o'clock. The part already spent is dim; the
        // part left glows like lamplight and pulses once dawn is under an hour away.
        private static readonly Vector2 ClockCenter = new Vector2(62, 50);
        private const float ClockRadius = 34f;

        private static Vector2 ClockDirection(float turns) =>
            new Vector2(MathF.Sin(turns * MathF.PI * 2f), -MathF.Cos(turns * MathF.PI * 2f));

        private void DrawClock(SpriteBatch spriteBatch, SpriteFont font, float totalSeconds)
        {
            var center = ClockCenter;
            const float r = ClockRadius;
            int left = _dawnTimer.MinutesLeft;
            bool nearDawn = left <= 60;
            float pulse = UITheme.PulseSine(totalSeconds, 4f);

            // Dawn light creeping up behind the clock over the last two hours.
            float dawnGlow = MathHelper.Clamp(1f - left / 120f, 0f, 1f);
            if (dawnGlow > 0f)
            {
                UITheme.DrawGlow(spriteBatch, center, r + 34f, new Color(255, 150, 90) * (dawnGlow * (0.35f + pulse * 0.15f)));
            }

            UITheme.FillCircle(spriteBatch, center + new Vector2(2, 4), r + 3f, Color.Black * 0.4f);
            UITheme.FillCircle(spriteBatch, center, r + 3f, new Color(150, 120, 90));
            UITheme.FillCircle(spriteBatch, center, r, new Color(22, 22, 36));

            // The night's arc, 8 o'clock round to 6 o'clock (10 of the face's 12 hours).
            float duskTurn = (DawnTimer.DuskHour % 12) / 12f;
            float nightTurns = _dawnTimer.MaxMinutes / 720f;
            float progress = MathHelper.Clamp(_displayedElapsed / _dawnTimer.MaxMinutes, 0f, 1f);
            const int segments = 60;
            float arcRadius = r - 6f;
            Color remaining = nearDawn
                ? Color.Lerp(new Color(255, 170, 90), new Color(255, 235, 190), pulse * 0.6f)
                : new Color(255, 200, 120);
            for (int i = 0; i < segments; i++)
            {
                float t0 = i / (float)segments, t1 = (i + 1) / (float)segments;
                var a = center + ClockDirection(duskTurn + t0 * nightTurns) * arcRadius;
                var b = center + ClockDirection(duskTurn + t1 * nightTurns) * arcRadius;
                Color color = t1 <= progress ? new Color(70, 70, 96) : remaining;
                spriteBatch.DrawLine(a, b, color, 4f);
            }

            // Hour marks - longer at 12, 3, 6 and 9.
            for (int h = 0; h < 12; h++)
            {
                var dir = ClockDirection(h / 12f);
                float inner = h % 3 == 0 ? r - 14f : r - 11f;
                spriteBatch.DrawLine(center + dir * inner, center + dir * (r - 9f), Color.White * (h % 3 == 0 ? 0.7f : 0.35f), h % 3 == 0 ? 2f : 1f);
            }

            // Dawn marker: a little sun where the arc ends.
            var sun = center + ClockDirection(duskTurn + nightTurns) * arcRadius;
            UITheme.DrawGlow(spriteBatch, sun, 14f, new Color(255, 200, 120) * 0.8f);
            UITheme.FillCircle(spriteBatch, sun, 4f, new Color(255, 230, 170));

            // Hands, from the animated time so they sweep rather than jump.
            int clockMinutes = DawnTimer.ClockMinutesAt(_displayedElapsed);
            float minuteTurn = (clockMinutes % 60) / 60f;
            float hourTurn = (clockMinutes / 60f % 12f) / 12f;
            spriteBatch.DrawLine(center, center + ClockDirection(hourTurn) * (r * 0.48f), new Color(235, 225, 210), 3f);
            spriteBatch.DrawLine(center, center + ClockDirection(minuteTurn) * (r * 0.72f), new Color(235, 225, 210), 2f);
            UITheme.FillCircle(spriteBatch, center, 3.5f, new Color(255, 200, 120));

            // Readout beside the face.
            float textX = center.X + r + 18f;
            UITheme.DrawTextWithShadow(spriteBatch, font, _dawnTimer.ClockLabel, new Vector2(textX, 14), Color.White, 1.25f);
            string untilDawn = left > 0 ? $"{DawnTimer.FormatDuration(left)} until dawn" : "Dawn is breaking";
            Color untilColor = nearDawn ? Color.Lerp(new Color(255, 170, 110), Color.White, pulse * 0.3f) : new Color(255, 205, 150);
            UITheme.DrawTextWithShadow(spriteBatch, font, untilDawn, new Vector2(textX, 46), untilColor, 0.85f);
            UITheme.DrawTextWithShadow(spriteBatch, font, $"New room {DawnTimer.FormatDuration(RoomEntryMinutes)}.  Fights take no time.", new Vector2(textX, 70), new Color(170, 165, 190), 0.62f);
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

        // ---------- Maze ----------

        private static Color RoomTop(RoomType type) => type switch
        {
            RoomType.Supplies => new Color(52, 116, 78),
            RoomType.Encounter => new Color(138, 40, 40),
            RoomType.Special => new Color(104, 56, 140),
            RoomType.Hoard => new Color(170, 128, 40),
            RoomType.Entrance => new Color(70, 84, 110),
            _ => new Color(58, 58, 70)
        };

        private static Color RoomBottom(RoomType type) => type switch
        {
            RoomType.Supplies => new Color(28, 70, 46),
            RoomType.Encounter => new Color(88, 22, 22),
            RoomType.Special => new Color(64, 30, 92),
            RoomType.Hoard => new Color(110, 78, 20),
            RoomType.Entrance => new Color(40, 50, 70),
            _ => new Color(36, 36, 46)
        };

        private void DrawMaze(SpriteBatch spriteBatch, SpriteFont font, float totalSeconds)
        {
            // The ruins' footprint: a dark slab the corridors are cut into.
            var slab = new RectangleF(MapArea.X - 12, MapArea.Y - 10, MapArea.Width + 24, MapArea.Height + 20);
            UITheme.DrawPanel(spriteBatch, slab, new Color(18, 17, 27), new Color(9, 9, 14), new Color(52, 48, 66), 1.5f, 18f, shadowStrength: 0.8f);

            // A far-off glimmer where the Hoard lies, even through the fog - something to aim for.
            if (!_map.Hoard.Visited)
            {
                float glimmer = UITheme.PulseSine(totalSeconds, 1.4f);
                UITheme.DrawGlow(spriteBatch, _map.Hoard.Center, 70f + glimmer * 12f, new Color(255, 200, 90) * (0.16f + glimmer * 0.10f));
            }

            // Faint marks where the fog still hides the grid - hints at how much is left
            // out there without giving away the layout (rubble cells get a mark too).
            float pitchX = MapArea.Width / _map.Columns;
            float pitchY = MapArea.Height / _map.Rows;
            for (int col = 0; col < _map.Columns; col++)
            {
                for (int row = 0; row < _map.Rows; row++)
                {
                    var cell = _map.At(col, row);
                    if (cell != null && cell.RevealAmount > 0.5f) continue;
                    var mark = new Vector2(MapArea.X + pitchX * (col + 0.5f), MapArea.Y + pitchY * (row + 0.5f));
                    UITheme.FillCircle(spriteBatch, mark, 2.5f, new Color(90, 86, 110) * 0.35f);
                }
            }

            // Lantern light: a warm, gently flickering pool around you, drawn under the
            // corridors so the stone near you reads as lit.
            float flicker = 1f + 0.04f * MathF.Sin(totalSeconds * 7.3f) + 0.03f * MathF.Sin(totalSeconds * 13.1f);
            UITheme.DrawGlow(spriteBatch, _tokenPosition, 260f * flicker, new Color(255, 160, 80) * 0.18f);
            UITheme.DrawGlow(spriteBatch, _tokenPosition, 120f * flicker, new Color(255, 180, 100) * 0.16f);

            DrawCorridors(spriteBatch, totalSeconds);

            foreach (var node in _map.Nodes)
            {
                if (node.RevealAmount <= 0.01f) continue;
                DrawRoom(spriteBatch, font, node, totalSeconds);
            }

            DrawToken(spriteBatch, totalSeconds);

            DrawBottomBar(spriteBatch, font);
            DrawStyledButton(spriteBatch, font, _headBackButton, new Color(120, 78, 64), new Color(84, 52, 44));

            // Last, so it's never covered by the bottom bar.
            if (_hoveredNode != null)
            {
                DrawRoomTooltip(spriteBatch, font, _hoveredNode);
            }
            DrawPack(spriteBatch, font);
        }

        private void DrawCorridors(SpriteBatch spriteBatch, float totalSeconds)
        {
            // Which corridors the hovered route would walk, so they can be lit up.
            var routeEdges = new HashSet<(MapNode, MapNode)>();
            if (_hoverPath != null)
            {
                var previous = _current;
                foreach (var step in _hoverPath)
                {
                    routeEdges.Add((previous, step));
                    routeEdges.Add((step, previous));
                    previous = step;
                }
            }

            foreach (var a in _map.Nodes)
            {
                foreach (var b in a.Links)
                {
                    // Each corridor once.
                    if (a.Column * 100 + a.Row > b.Column * 100 + b.Row) continue;
                    if (!a.Discovered && !b.Discovered) continue;

                    var from = a.Center;
                    var to = b.Center;
                    float alpha;

                    if (a.Discovered && b.Discovered)
                    {
                        alpha = Math.Min(a.RevealAmount, b.RevealAmount);
                    }
                    else
                    {
                        // Only one end is known: draw a stub trailing off into the fog, so
                        // unexplored exits read as "this way goes somewhere".
                        if (!a.Discovered) (from, to) = (to, from);
                        to = Vector2.Lerp(from, to, 0.62f);
                        alpha = (a.Discovered ? a.RevealAmount : b.RevealAmount) * 0.55f;
                    }

                    if (alpha <= 0.01f) continue;

                    bool walked = a.Visited && b.Visited;
                    bool onRoute = routeEdges.Contains((a, b));

                    spriteBatch.DrawLine(from, to, new Color(4, 4, 8) * alpha, 20f);
                    Color floor = walked ? new Color(112, 84, 56) : new Color(46, 44, 58);
                    spriteBatch.DrawLine(from, to, floor * alpha, 11f);

                    if (onRoute)
                    {
                        float pulse = UITheme.PulseSine(totalSeconds, 6f);
                        spriteBatch.DrawLine(from, to, new Color(255, 150, 70) * (0.55f + pulse * 0.35f), 5f);
                    }
                    else if (walked)
                    {
                        spriteBatch.DrawLine(from, to, new Color(190, 140, 90) * 0.35f, 3f);
                    }
                }
            }
        }

        private void DrawRoom(SpriteBatch spriteBatch, SpriteFont font, MapNode node, float totalSeconds)
        {
            float alpha = node.RevealAmount;
            float hover = node.HoverAmount;
            bool reachable = _reachable.Contains(node);
            bool frontier = reachable && !node.Visited;

            // Hovered rooms lift slightly; newly found rooms pop up out of the fog.
            float grow = hover * 5f;
            var bounds = new RectangleF(node.ScreenBounds.X - grow, node.ScreenBounds.Y - grow - hover * 2f, node.ScreenBounds.Width + grow * 2f, node.ScreenBounds.Height + grow * 2f);
            if (alpha < 0.999f) bounds = Anim.Scale(bounds, MathHelper.Lerp(0.6f, 1f, UITheme.EaseOutBack(alpha)));
            var center = new Vector2(bounds.X + bounds.Width / 2f, bounds.Y + bounds.Height / 2f);

            Color top, bottom;
            if (!node.Scouted)
            {
                top = new Color(30, 30, 42);
                bottom = new Color(20, 20, 30);
            }
            else
            {
                top = RoomTop(node.Type);
                bottom = RoomBottom(node.Type);
                if (node.Visited && node != _current)
                {
                    // Explored: drained of color, so the map shows at a glance what's left.
                    top = Color.Lerp(top, new Color(52, 52, 58), 0.7f);
                    bottom = Color.Lerp(bottom, new Color(34, 34, 40), 0.7f);
                }
            }

            if (hover > 0f)
            {
                top = UITheme.Brighten(top, hover * 0.22f);
                bottom = UITheme.Brighten(bottom, hover * 0.22f);
            }

            // Frontier rooms breathe with a faint ember edge so the next choices stand out.
            Color border = new Color(120, 116, 140) * 0.6f;
            float borderThickness = 1.5f;
            if (frontier)
            {
                float breathe = UITheme.PulseSine(totalSeconds + node.Phase, 2.4f);
                border = Color.Lerp(new Color(200, 140, 90) * 0.6f, new Color(255, 170, 90), breathe * 0.5f);
                borderThickness = 2f;
            }
            if (node == _current)
            {
                border = new Color(255, 225, 170);
                borderThickness = 2.5f;
            }
            border = Color.Lerp(border, new Color(255, 130, 60), hover);
            borderThickness = MathHelper.Lerp(borderThickness, 3.5f, hover);

            if (node.Scouted && node.Type == RoomType.Hoard && !node.Visited)
            {
                float glint = UITheme.PulseSine(totalSeconds + node.Phase, 2f);
                UITheme.DrawGlow(spriteBatch, center, 70f, new Color(255, 200, 90) * (0.25f + glint * 0.2f) * alpha);
            }
            if (node.Scouted && node.Type == RoomType.Encounter && !node.Visited)
            {
                float menace = UITheme.PulseSine(totalSeconds + node.Phase, 1.8f);
                UITheme.DrawGlow(spriteBatch, center, 58f, new Color(220, 40, 30) * (0.22f + menace * 0.2f) * alpha);
            }

            UITheme.DrawPanel(spriteBatch, bounds, top * alpha, bottom * alpha, border * alpha, borderThickness, 12f, shadowStrength: 0.5f * alpha);

            float iconAlpha = alpha * (node.Visited && node != _current ? 0.45f : 1f);
            if (!node.Scouted)
            {
                const string unknown = "?";
                var size = UITheme.MeasureString(font, unknown) * 1.3f;
                UITheme.DrawTextWithShadow(spriteBatch, font, unknown, center - size / 2f, new Color(150, 150, 175) * alpha, 1.3f);
            }
            else
            {
                DrawRoomIcon(spriteBatch, node, center, iconAlpha, totalSeconds);
            }

            // Ripple when a creature moves in or out.
            if (node.StirAmount > 0.01f)
            {
                float t = 1f - node.StirAmount;
                float radius = RoomSize * 0.55f + t * 46f;
                spriteBatch.DrawCircle(center, radius, 28, new Color(255, 90, 60) * (node.StirAmount * alpha), 2.5f);
                spriteBatch.DrawCircle(center, radius * 0.75f, 28, new Color(255, 90, 60) * (node.StirAmount * 0.5f * alpha), 1.5f);
            }
        }

        /// <summary>A tiny primitive-drawn glyph per room type - no extra art needed.</summary>
        private void DrawRoomIcon(SpriteBatch spriteBatch, MapNode node, Vector2 c, float alpha, float totalSeconds)
        {
            switch (node.Type)
            {
                case RoomType.Supplies:
                    {
                        // A crate: planked box with a cross brace.
                        var box = new RectangleF(c.X - 15, c.Y - 12, 30, 24);
                        UITheme.FillRoundedRect(spriteBatch, box, new Color(170, 124, 70) * alpha, 3f);
                        spriteBatch.DrawRectangle(box, new Color(90, 60, 30) * alpha, 2f);
                        spriteBatch.DrawLine(new Vector2(box.Left + 2, box.Top + 2), new Vector2(box.Right - 2, box.Bottom - 2), new Color(110, 76, 40) * alpha, 2f);
                        spriteBatch.DrawLine(new Vector2(box.Right - 2, box.Top + 2), new Vector2(box.Left + 2, box.Bottom - 2), new Color(110, 76, 40) * alpha, 2f);
                        break;
                    }

                case RoomType.Encounter:
                    {
                        // Two eyes in the dark that blink now and then.
                        float cycle = (totalSeconds + node.Phase * 1.7f) % 4.2f;
                        bool blinking = cycle < 0.14f;
                        var eyeColor = new Color(255, 80, 60) * alpha;
                        foreach (float offset in new[] { -9f, 9f })
                        {
                            var eye = c + new Vector2(offset, 0f);
                            UITheme.DrawGlow(spriteBatch, eye, 16f, new Color(255, 60, 40) * (0.9f * alpha));
                            if (blinking)
                                spriteBatch.FillRectangle(new RectangleF(eye.X - 5, eye.Y - 1, 10, 2), eyeColor);
                            else
                                UITheme.FillCircle(spriteBatch, eye, 4.5f, eyeColor);
                        }
                        break;
                    }

                case RoomType.Special:
                    {
                        // A four-point sparkle that slowly breathes.
                        float s = 12f + UITheme.PulseSine(totalSeconds + node.Phase, 2.5f) * 4f;
                        var sparkle = new Color(230, 200, 255) * alpha;
                        UITheme.DrawGlow(spriteBatch, c, 28f, new Color(170, 110, 255) * (0.8f * alpha));
                        spriteBatch.DrawLine(c - new Vector2(0, s), c + new Vector2(0, s), sparkle, 2.5f);
                        spriteBatch.DrawLine(c - new Vector2(s, 0), c + new Vector2(s, 0), sparkle, 2.5f);
                        UITheme.FillCircle(spriteBatch, c, 4f, Color.White * alpha);
                        break;
                    }

                case RoomType.Hoard when node.HasKnight:
                    {
                        // The final night's Hoard: the Knight's helm, visor glowing.
                        float glow = UITheme.PulseSine(totalSeconds + node.Phase, 2f);
                        var helm = new RectangleF(c.X - 13, c.Y - 14, 26, 28);
                        UITheme.DrawGlow(spriteBatch, c, 30f, new Color(255, 110, 40) * ((0.5f + glow * 0.4f) * alpha));
                        UITheme.FillRoundedRect(spriteBatch, helm, new Color(150, 150, 166) * alpha, 7f);
                        spriteBatch.FillRectangle(new RectangleF(c.X - 9, c.Y - 2, 18, 4), new Color(255, 150, 70) * alpha);
                        foreach (float offset in new[] { -8f, 0f, 8f })
                        {
                            UITheme.FillCircle(spriteBatch, new Vector2(c.X + offset, c.Y - 17), 3f, new Color(240, 200, 90) * alpha);
                        }
                        break;
                    }

                case RoomType.Hoard:
                    {
                        // A treasure chest.
                        var chest = new RectangleF(c.X - 16, c.Y - 8, 32, 20);
                        var lid = new RectangleF(c.X - 16, c.Y - 16, 32, 10);
                        UITheme.FillRoundedRect(spriteBatch, lid, new Color(150, 96, 40) * alpha, 5f);
                        UITheme.FillRoundedRect(spriteBatch, chest, new Color(128, 80, 32) * alpha, 3f);
                        spriteBatch.FillRectangle(new RectangleF(c.X - 16, c.Y - 8, 32, 3), new Color(240, 200, 90) * alpha);
                        spriteBatch.FillRectangle(new RectangleF(c.X - 3, c.Y - 6, 6, 8), new Color(255, 220, 110) * alpha);
                        break;
                    }

                case RoomType.Entrance:
                    {
                        // An arched doorway.
                        var door = new RectangleF(c.X - 10, c.Y - 6, 20, 20);
                        UITheme.FillCircle(spriteBatch, c + new Vector2(0, -6), 10f, new Color(12, 12, 18) * alpha);
                        spriteBatch.FillRectangle(door, new Color(12, 12, 18) * alpha);
                        spriteBatch.DrawLine(new Vector2(door.Left - 2, door.Bottom), new Vector2(door.Right + 2, door.Bottom), new Color(160, 170, 200) * alpha, 2f);
                        break;
                    }

                default:
                    {
                        // Quiet hall: a few scattered stones.
                        var stone = new Color(120, 120, 135) * alpha;
                        UITheme.FillCircle(spriteBatch, c + new Vector2(-9, 5), 3.5f, stone);
                        UITheme.FillCircle(spriteBatch, c + new Vector2(4, 8), 2.5f, stone);
                        UITheme.FillCircle(spriteBatch, c + new Vector2(8, -6), 3f, stone);
                        break;
                    }
            }
        }

        private void DrawToken(SpriteBatch spriteBatch, float totalSeconds)
        {
            // You: a small lantern-bearer bobbing as it walks (or idles).
            float bob = MathF.Sin(totalSeconds * (IsWalking ? 12f : 3f)) * (IsWalking ? 3f : 1.5f);
            // Stands in the room's lower-right corner so the room's icon stays readable.
            var pos = _tokenPosition + new Vector2(RoomSize * 0.38f, RoomSize * 0.38f + bob);

            UITheme.FillCircle(spriteBatch, pos + new Vector2(0, 10), 10f, Color.Black * 0.35f);
            UITheme.DrawGlow(spriteBatch, pos, 38f, new Color(255, 190, 110) * 0.85f);
            UITheme.FillCircle(spriteBatch, pos, 10f, new Color(255, 236, 200));
            UITheme.FillCircle(spriteBatch, pos, 7f, new Color(255, 190, 110));

            float flame = 3.5f + MathF.Sin(totalSeconds * 17f) * 0.8f;
            UITheme.FillCircle(spriteBatch, pos + new Vector2(0, -1), flame, new Color(255, 250, 225));
        }

        /// <summary>Tooltip line for enemies known to be in a room - ones you ran from, or
        /// ones the Archive's bestiary identified - e.g. "2 Penitents, 22 HP each".</summary>
        private static string DescribeWaiting(List<Enemy> enemies)
        {
            var living = enemies.Where(e => !e.IsDefeated).ToList();
            bool wounded = living.Any(e => e.Health < e.MaxHealth);
            if (living.Count == 1)
            {
                var enemy = living[0];
                return wounded
                    ? $"{enemy.Name} - wounded, {enemy.Health}/{enemy.MaxHealth}"
                    : $"{enemy.Name} - {enemy.MaxHealth} HP, hits {enemy.AttackPower}";
            }
            if (wounded) return $"{living.Count} enemies waiting, wounded";
            return living.All(e => e.Kind == EnemyKind.Penitent)
                ? $"{living.Count} Penitents, {living[0].MaxHealth} HP each"
                : $"{living.Count} enemies inside";
        }

        private void DrawRoomTooltip(SpriteBatch spriteBatch, SpriteFont font, MapNode node)
        {
            string title = node.Scouted ? RoomTypeInfo.Name(node.Type) : "Unknown room";
            string detail = !node.Scouted ? "Too dark to make out from here."
                : node.HasKnight ? "The Hollow Knight guards it. Optional."
                : node.Enemies != null ? DescribeWaiting(node.Enemies)
                : RoomTypeInfo.Description(node.Type);

            string action;
            Color actionColor;
            if (node == _current)
            {
                action = "You are here.";
                actionColor = new Color(255, 225, 170);
            }
            else if (!_reachable.Contains(node))
            {
                action = "No known way there yet.";
                actionColor = new Color(160, 160, 180);
            }
            else if (node.Visited)
            {
                int steps = _hoverPath?.Count ?? 0;
                action = $"Walk back ({steps} step{(steps == 1 ? "" : "s")}) - free";
                actionColor = new Color(170, 220, 170);
            }
            else
            {
                action = _dawnTimer.MinutesLeft <= RoomEntryMinutes
                    ? "Enter - the last room before dawn"
                    : $"Enter - {DawnTimer.FormatDuration(RoomEntryMinutes)}, until {_dawnTimer.ClockLabelAfter(RoomEntryMinutes)}";
                actionColor = new Color(255, 180, 110);
            }

            string footer = $"Depth {node.Depth}";
            if (node.Scouted) footer += $"   {node.Links.Count} way{(node.Links.Count == 1 ? "" : "s")} out";
            if (node.Scouted && node.IsDeadEnd && node != _map.Entrance) footer += "  (dead end)";

            const float width = 320f, height = 112f;
            var anchor = node.ScreenBounds;
            float x = anchor.X + anchor.Width + 14;
            if (x + width > 1260) x = anchor.X - width - 14;
            float y = MathHelper.Clamp(anchor.Y - 20, MapArea.Y - 4, 700 - height);
            var box = new RectangleF(x, y, width, height);

            UITheme.DrawPanel(spriteBatch, box, new Color(34, 30, 46), new Color(20, 18, 28), new Color(150, 120, 90), 1.5f, 10f, shadowStrength: 0.8f);
            UITheme.DrawTextWithShadow(spriteBatch, font, title, new Vector2(box.X + 12, box.Y + 10), Color.White);
            UITheme.DrawTextWithShadow(spriteBatch, font, detail, new Vector2(box.X + 12, box.Y + 38), new Color(200, 196, 214), 0.72f);
            UITheme.DrawTextWithShadow(spriteBatch, font, action, new Vector2(box.X + 12, box.Y + 60), actionColor, 0.85f);
            UITheme.DrawTextWithShadow(spriteBatch, font, footer, new Vector2(box.X + 12, box.Y + 86), new Color(150, 146, 168), 0.72f);
        }

        private void DrawBottomBar(SpriteBatch spriteBatch, SpriteFont font)
        {
            // Journal: what just happened.
            var journal = new RectangleF(40, 616, 836, 88);
            UITheme.DrawPanel(spriteBatch, journal, new Color(26, 24, 38) * 0.92f, new Color(16, 15, 24) * 0.92f, new Color(80, 72, 100), 1.5f, 12f, shadowStrength: 0.5f);
            _textLog.Draw(spriteBatch, font, new Vector2(journal.X + 16, journal.Y + 56), maxWidth: journal.Width - 32f);

            // Legend.
            var legend = new RectangleF(892, 616, 348, 88);
            UITheme.DrawPanel(spriteBatch, legend, new Color(26, 24, 38) * 0.92f, new Color(16, 15, 24) * 0.92f, new Color(80, 72, 100), 1.5f, 12f, shadowStrength: 0.5f);
            var entries = new (RoomType type, string label)[]
            {
                (RoomType.Supplies, "Supplies"), (RoomType.Encounter, "Enemy"), (RoomType.Special, "Event"),
                (RoomType.Hoard, "Hoard"), (RoomType.Empty, "Quiet"), (RoomType.Entrance, "Entrance")
            };
            for (int i = 0; i < entries.Length; i++)
            {
                float ex = legend.X + 16 + (i % 3) * 112;
                float ey = legend.Y + 16 + (i / 3) * 32;
                var swatch = new RectangleF(ex, ey + 2, 18, 18);
                UITheme.FillRoundedRectGradient(spriteBatch, swatch, RoomTop(entries[i].type), RoomBottom(entries[i].type), 4f, 4);
                UITheme.DrawTextWithShadow(spriteBatch, font, entries[i].label, new Vector2(ex + 26, ey), new Color(210, 206, 225), 0.8f);
            }
        }

        /// <summary>A shrunk-down copy of tonight's maze, shown while you're busy in a room
        /// (combat or a supply cache) so you never lose track of where you are.</summary>
        private void DrawMapFragment(SpriteBatch spriteBatch, SpriteFont font)
        {
            var box = new RectangleF(40, 604, 232, 100);
            UITheme.DrawPanel(spriteBatch, box, new Color(62, 50, 32), new Color(42, 34, 20), new Color(150, 120, 70), 2f, 12f, shadowStrength: 0.5f);
            UITheme.DrawTextWithShadow(spriteBatch, font, $"Tonight's map  -  Depth {_current.Depth}", new Vector2(box.X + 10, box.Y + 6), new Color(225, 205, 165), 0.75f);

            var area = new RectangleF(box.X + 10, box.Y + 30, box.Width - 20, box.Height - 38);
            float cellW = area.Width / _map.Columns;
            float cellH = area.Height / _map.Rows;
            Vector2 MiniCenter(MapNode n) => new Vector2(area.X + cellW * (n.Column + 0.5f), area.Y + cellH * (n.Row + 0.5f));

            foreach (var a in _map.Nodes)
            {
                if (!a.Discovered) continue;
                foreach (var b in a.Links)
                {
                    if (!b.Discovered) continue;
                    spriteBatch.DrawLine(MiniCenter(a), MiniCenter(b), new Color(120, 96, 60), 2f);
                }
            }

            foreach (var node in _map.Nodes)
            {
                if (!node.Discovered) continue;
                var c = MiniCenter(node);
                Color color = !node.Scouted ? new Color(90, 80, 70)
                    : node.Visited ? new Color(150, 130, 100)
                    : RoomTop(node.Type);
                spriteBatch.FillRectangle(new RectangleF(c.X - 3.5f, c.Y - 3.5f, 7, 7), color);
            }

            var here = MiniCenter(_current);
            UITheme.DrawGlow(spriteBatch, here, 10f, new Color(255, 220, 150) * 0.9f);
            UITheme.FillCircle(spriteBatch, here, 3.5f, Color.White);
        }

        private void DrawCombat(SpriteBatch spriteBatch, SpriteFont font, float totalSeconds)
        {
            // Round info where the Head Back button sits on the map.
            UITheme.DrawTextWithShadow(spriteBatch, font, $"Turn {_combatTurn + 1}", new Vector2(1000, 24), Color.LightGray);
            UITheme.DrawTextWithShadow(spriteBatch, font, $"Rerolls: {_activeCombat.RerollsLeft}", new Vector2(1000, 54), new Color(200, 210, 255), 0.85f);
            if (_activeCombat.PlayerStunned)
            {
                float pulse = UITheme.PulseSine(totalSeconds, 5f);
                UITheme.DrawTextWithShadow(spriteBatch, font, "STUNNED", new Vector2(1000, 82), Color.Lerp(new Color(255, 200, 90), Color.White, pulse * 0.4f), 0.95f);
            }

            // Enemies drop in one after another as the fight opens.
            var enemies = _activeCombat.Enemies;
            for (int i = 0; i < enemies.Count; i++)
            {
                float intro = Anim.Stagger(_stateTime, i, step: 0.1f, baseDelay: 0.05f, duration: 0.45f);
                if (intro <= 0.001f) continue;
                var bounds = Anim.Slide(EnemyPanelBounds(i, enemies.Count), UITheme.EaseOutBack(intro), new Vector2(0, -40));
                DrawEnemyPanel(spriteBatch, font, enemies[i], bounds, totalSeconds, intro);
            }

            // Roll readout over the target. The slash / hit animations are drawn last in
            // Draw(), centered on the screen, so they sit on top of everything.
            _diceRollPopup.Draw(spriteBatch, font);

            _textLog.Draw(spriteBatch, font, new Vector2(300, 575), maxWidth: 940f);

            for (int i = 0; i < _combatButtons.Count; i++)
            {
                var button = _combatButtons[i];
                bool isItem = _combatMenu == CombatMenu.Items && i < _itemButtonNames.Count;

                // Dimmed while locked, so it's clear the next action isn't ready yet.
                // The action buttons slide in from the left edge.
                float intro = Anim.Stagger(_stateTime, i, step: 0.06f, baseDelay: 0.15f, duration: 0.35f);
                if (isItem)
                {
                    DrawItemButton(spriteBatch, font, button, _itemButtonNames[i]);
                }
                else if (IsActionLocked)
                {
                    DrawStyledButton(spriteBatch, font, button, new Color(40, 40, 50), new Color(30, 30, 38), intro: intro);
                }
                else if (button.Label == ShakeItOffLabel)
                {
                    DrawStyledButton(spriteBatch, font, button, new Color(120, 96, 40), new Color(84, 64, 24), intro: intro);
                }
                else
                {
                    DrawStyledButton(spriteBatch, font, button, new Color(64, 64, 88), new Color(44, 44, 64), intro: intro);
                }
            }

            DrawCombatHints(spriteBatch, font);
            DrawItemTooltip(spriteBatch, font);
        }

        /// <summary>An item in the combat Items menu: its name (and how many you carry) on
        /// top, and what it does underneath - so a choice mid-fight never means guessing.</summary>
        private void DrawItemButton(SpriteBatch spriteBatch, SpriteFont font, Button button, string itemName)
        {
            var item = _playerState.Belt.Find(it => it.Name == itemName);
            float hover = IsActionLocked ? 0f : button.HoverAmount;
            Color top = IsActionLocked ? new Color(40, 40, 50) : UITheme.Brighten(new Color(60, 70, 64), hover * 0.2f);
            Color bottom = IsActionLocked ? new Color(30, 30, 38) : UITheme.Brighten(new Color(40, 48, 44), hover * 0.2f);
            Color border = Color.Lerp(Color.White * 0.55f, Color.White, hover);

            float squash = button.PressAmount * 3f;
            var b = button.Bounds;
            var draw = new RectangleF(b.X + squash, b.Y + squash / 2f, b.Width - squash * 2f, b.Height - squash);
            UITheme.DrawPanel(spriteBatch, draw, top, bottom, border, MathHelper.Lerp(1.5f, 3f, hover), 10f, shadowStrength: 0.5f);

            UITheme.DrawTextWithShadow(spriteBatch, font, button.Label, new Vector2(draw.X + 12, draw.Y + 6), Color.White, 0.9f);
            if (item != null)
            {
                UITheme.DrawTextWithShadow(spriteBatch, font, item.ShortEffect(_playerState), new Vector2(draw.X + 12, draw.Y + 32), new Color(170, 230, 180), 0.66f);
            }
        }

        /// <summary>The full description of the hovered item, in a card beside the list.</summary>
        private void DrawItemTooltip(SpriteBatch spriteBatch, SpriteFont font)
        {
            if (_combatMenu != CombatMenu.Items || IsActionLocked) return;

            for (int i = 0; i < _itemButtonNames.Count && i < _combatButtons.Count; i++)
            {
                var button = _combatButtons[i];
                if (button.HoverAmount < 0.5f) continue;

                var item = _playerState.Belt.Find(it => it.Name == _itemButtonNames[i]);
                if (item == null) return;
                int owned = _playerState.Belt.Count(it => it.Name == item.Name);

                const float width = 320f, descScale = 0.72f;
                var lines = TextLog.WrapText(font, item.Description, (width - 28) / descScale);
                float height = 70 + lines.Count * 20;
                var card = new RectangleF(button.Bounds.Right + 14, Math.Min(button.Bounds.Y, 700 - height), width, height);

                UITheme.DrawPanel(spriteBatch, card, new Color(34, 30, 46), new Color(20, 18, 28), new Color(150, 200, 160), 1.5f, 10f, shadowStrength: 0.8f);
                UITheme.DrawTextWithShadow(spriteBatch, font, item.Name, new Vector2(card.X + 14, card.Y + 10), Color.White);
                for (int l = 0; l < lines.Count; l++)
                {
                    UITheme.DrawTextWithShadow(spriteBatch, font, lines[l], new Vector2(card.X + 14, card.Y + 40 + l * 20), new Color(210, 206, 225), descScale);
                }
                UITheme.DrawTextWithShadow(spriteBatch, font, $"Carrying {owned}. Using one takes your turn.", new Vector2(card.X + 14, card.Bottom - 26), new Color(160, 156, 178), 0.62f);
                return;
            }
        }

        /// <summary>A few small lines under the action buttons explaining how to answer
        /// what the enemies are showing - the rules the intent chips rely on.</summary>
        private void DrawCombatHints(SpriteBatch spriteBatch, SpriteFont font)
        {
            // The Items list can run long enough to reach the mini-map - no room for hints there.
            if (_combatButtons.Count == 0 || _combatMenu == CombatMenu.Items) return;
            float y = _combatButtons[_combatButtons.Count - 1].Bounds.Bottom + 14;

            string[] hints = _activeCombat.PlayerStunned
                ? new[] { "Bound by a chant - this turn is lost.", "Guard against STUN to stop it." }
                : _combatMenu == CombatMenu.Skills
                    ? new[] { "Power Strike: two rolls, but hits", "on you land 50% harder next turn.", "Guard: halves hits, fully blocks", "HEAVY and STUN. Spells ignore it." }
                    : _activeCombat.Enemies.Count(e => !e.IsDefeated) > 1
                        ? new[] { "Click an enemy to target it.", "Watch their next moves above them." }
                        : new[] { "Each enemy shows its next move.", "Guard blocks HEAVY and STUN." };

            foreach (var hint in hints)
            {
                UITheme.DrawTextWithShadow(spriteBatch, font, hint, new Vector2(42, y), new Color(175, 170, 195), 0.62f);
                y += 18;
            }
        }

        private static (Color top, Color bottom, Color border) EnemyPalette(EnemyKind kind) => kind switch
        {
            EnemyKind.Penitent => (new Color(58, 50, 40), new Color(34, 28, 22), new Color(200, 170, 110)),
            EnemyKind.Knight => (new Color(48, 48, 60), new Color(24, 24, 32), new Color(230, 190, 90)),
            _ => (new Color(45, 26, 30), new Color(28, 16, 19), new Color(150, 45, 40))
        };

        private void DrawEnemyPanel(SpriteBatch spriteBatch, SpriteFont font, Enemy enemy, RectangleF bounds, float totalSeconds, float intro = 1f)
        {
            if (enemy == _shakenEnemy)
            {
                var shake = _enemyShake.Offset;
                bounds = new RectangleF(bounds.X + shake.X, bounds.Y + shake.Y, bounds.Width, bounds.Height);
            }

            bool fallen = enemy.IsDefeated;
            bool targeted = !fallen && enemy == _activeCombat.Target;
            float glow = UITheme.PulseSine(totalSeconds, 2.5f);
            var (top, bottom, border) = EnemyPalette(enemy.Kind);

            // Another living enemy under the cursor lifts a little: click to target it.
            var mouse = InputChecker.GetMouse();
            bool pickable = !fallen && !targeted && _activeCombat.Enemies.Count(e => !e.IsDefeated) > 1 && InputChecker.Contains(bounds, mouse.X, mouse.Y);
            if (pickable)
            {
                bounds = new RectangleF(bounds.X, bounds.Y - 4f, bounds.Width, bounds.Height);
                border = Color.Lerp(border, new Color(255, 190, 130), 0.6f);
            }
            // The fallen sink a little as they fade.
            if (fallen) bounds = new RectangleF(bounds.X, bounds.Y + 10f, bounds.Width, bounds.Height);

            if (targeted)
            {
                UITheme.DrawGlow(spriteBatch, new Vector2(bounds.X + bounds.Width / 2f, bounds.Y + bounds.Height / 2f), bounds.Width * 0.7f, new Color(255, 130, 60) * ((0.14f + glow * 0.08f) * intro));
                border = Color.Lerp(new Color(255, 140, 70), new Color(255, 210, 140), glow * 0.5f);
            }
            float alpha = (fallen ? 0.35f : 1f) * intro;
            UITheme.DrawPanel(spriteBatch, bounds, top * alpha, bottom * alpha, border * alpha, targeted ? 3.5f : 2f, 14f, shadowStrength: 0.6f * alpha);

            // Name, and a tag for the boss / current target.
            float scale = UITheme.MeasureString(font, enemy.Name).X * 0.85f > bounds.Width - 24 ? 0.72f : 0.85f;
            UITheme.DrawTextWithShadow(spriteBatch, font, enemy.Name, new Vector2(bounds.X + 12, bounds.Y + 10), Color.White * alpha, scale);
            string tag = fallen ? "FALLEN" : enemy.IsBoss ? "BOSS" : enemy.IsElite ? "ELITE" : targeted ? "TARGET" : null;
            if (tag != null)
            {
                Color tagColor = fallen ? new Color(160, 150, 150) : enemy.IsBoss || enemy.IsElite ? new Color(255, 215, 110) : new Color(255, 170, 100);
                var tagSize = UITheme.MeasureString(font, tag) * 0.62f;
                UITheme.DrawTextWithShadow(spriteBatch, font, tag, new Vector2(bounds.Right - tagSize.X - 12, bounds.Y + 36), tagColor * intro, 0.62f);
            }

            // Health bar.
            var track = new RectangleF(bounds.X + 12, bounds.Y + 56, bounds.Width - 24, 16);
            float ratio = _enemyBars.TryGetValue(enemy, out var bar) ? bar.Ratio : enemy.Health / (float)enemy.MaxHealth;
            UITheme.FillRoundedRect(spriteBatch, track, Color.Black * (0.55f * alpha), 8f);
            if (ratio > 0.01f)
            {
                UITheme.FillRoundedRectGradient(spriteBatch, new RectangleF(track.X, track.Y, track.Width * ratio, track.Height), new Color(230, 80, 70) * alpha, new Color(150, 30, 30) * alpha, 8f, 4);
            }
            string hp = $"{enemy.Health}/{enemy.MaxHealth}";
            var hpSize = UITheme.MeasureString(font, hp) * 0.62f;
            UITheme.DrawTextWithShadow(spriteBatch, font, hp, new Vector2(track.X + (track.Width - hpSize.X) / 2f, track.Y - 1), Color.White * alpha, 0.62f);

            // Portrait placeholder until there's enemy art: a glyph per kind, breathing gently.
            float breathe = fallen ? 0f : MathF.Sin(totalSeconds * 1.6f + enemy.MaxHealth) * 3f;
            var face = new Vector2(bounds.X + bounds.Width / 2f, bounds.Y + 160 + breathe);
            DrawEnemyGlyph(spriteBatch, enemy, face, alpha, totalSeconds);

            if (fallen) return;

            // Intent chip: what it will do next, and how to answer it.
            var chip = new RectangleF(bounds.X + 12, bounds.Bottom - 96, bounds.Width - 24, 84);
            Color chipTop, chipBottom, chipBorder;
            if (enemy.Intent == IntentType.Spell)
            {
                (chipTop, chipBottom, chipBorder) = (new Color(70, 44, 96), new Color(44, 26, 64), new Color(190, 140, 255));
            }
            else if (enemy.IntentIsThreat)
            {
                float alarm = UITheme.PulseSine(totalSeconds, 4f);
                (chipTop, chipBottom) = (new Color(110, 34, 28), new Color(70, 18, 14));
                chipBorder = Color.Lerp(new Color(255, 120, 60), new Color(255, 220, 140), alarm * 0.6f);
            }
            else if (enemy.Intent == IntentType.CallForAid)
            {
                (chipTop, chipBottom, chipBorder) = (new Color(90, 72, 30), new Color(60, 46, 16), new Color(240, 200, 100));
            }
            else
            {
                (chipTop, chipBottom, chipBorder) = (new Color(40, 38, 52), new Color(26, 24, 34), new Color(140, 130, 160));
            }
            if (enemy.IntentIsThreat)
            {
                UITheme.DrawGlow(spriteBatch, new Vector2(chip.X + chip.Width / 2f, chip.Y + chip.Height / 2f), chip.Width * 0.6f, new Color(255, 80, 40) * (0.2f * intro));
            }
            UITheme.DrawPanel(spriteBatch, chip, chipTop * intro, chipBottom * intro, chipBorder * intro, 2f, 10f, shadowStrength: 0.4f * intro);

            UITheme.DrawTextWithShadow(spriteBatch, font, "Next:", new Vector2(chip.X + 10, chip.Y + 6), new Color(190, 180, 200) * intro, 0.6f);
            var labelSize = UITheme.MeasureString(font, enemy.IntentLabel) * 1.05f;
            UITheme.DrawTextWithShadow(spriteBatch, font, enemy.IntentLabel, new Vector2(chip.X + (chip.Width - labelSize.X) / 2f, chip.Y + 22), Color.White * intro, 1.05f);
            var hintSize = UITheme.MeasureString(font, enemy.IntentHint) * 0.66f;
            UITheme.DrawTextWithShadow(spriteBatch, font, enemy.IntentHint, new Vector2(chip.X + (chip.Width - hintSize.X) / 2f, chip.Y + 56), new Color(225, 215, 205) * intro, 0.66f);
        }

        /// <summary>Primitive-drawn stand-ins for enemy art: a Wretch's red eyes, a hooded
        /// Penitent under a halo, the Knight's visored helm.</summary>
        private static void DrawEnemyGlyph(SpriteBatch spriteBatch, Enemy enemy, Vector2 c, float alpha, float totalSeconds)
        {
            float glow = UITheme.PulseSine(totalSeconds + enemy.MaxHealth * 0.1f, 2.5f);
            bool blinking = (totalSeconds + enemy.MaxHealth * 0.37f) % 3.6f < 0.15f;

            switch (enemy.Kind)
            {
                case EnemyKind.Penitent:
                    {
                        // Halo, hood, and two pale eyes.
                        spriteBatch.DrawCircle(c + new Vector2(0, -58), 26f, 32, new Color(255, 220, 140) * ((0.5f + glow * 0.4f) * alpha), 3f);
                        UITheme.DrawGlow(spriteBatch, c + new Vector2(0, -58), 44f, new Color(255, 210, 120) * (0.25f * alpha));
                        UITheme.FillCircle(spriteBatch, c, 46f, new Color(20, 16, 14) * alpha);
                        UITheme.FillCircle(spriteBatch, c + new Vector2(0, 8), 34f, new Color(8, 6, 6) * alpha);
                        foreach (float offset in new[] { -12f, 12f })
                        {
                            var eye = c + new Vector2(offset, 6);
                            UITheme.DrawGlow(spriteBatch, eye, 22f, new Color(255, 230, 170) * ((0.5f + glow * 0.3f) * alpha));
                            if (!blinking) UITheme.FillCircle(spriteBatch, eye, 5f, new Color(255, 245, 210) * alpha);
                        }
                        break;
                    }

                case EnemyKind.Knight:
                    {
                        // Helm with a glowing visor slit, under a three-point crown.
                        var helm = new RectangleF(c.X - 46, c.Y - 44, 92, 96);
                        UITheme.FillRoundedRectGradient(spriteBatch, helm, new Color(140, 140, 156) * alpha, new Color(60, 60, 74) * alpha, 20f, 8);
                        var visor = new RectangleF(c.X - 34, c.Y - 4, 68, 10);
                        UITheme.DrawGlow(spriteBatch, new Vector2(c.X, c.Y + 1), 64f, new Color(255, 110, 40) * ((0.45f + glow * 0.35f) * alpha));
                        spriteBatch.FillRectangle(visor, new Color(255, 150, 70) * alpha);
                        foreach (float offset in new[] { -28f, 0f, 28f })
                        {
                            UITheme.FillCircle(spriteBatch, new Vector2(c.X + offset, c.Y - 54), offset == 0f ? 9f : 7f, new Color(240, 200, 90) * alpha);
                        }
                        break;
                    }

                default:
                    {
                        // A Wretch: two blinking red eyes in the dark.
                        foreach (float offset in new[] { -34f, 34f })
                        {
                            var eye = c + new Vector2(offset, 0);
                            UITheme.DrawGlow(spriteBatch, eye, 50f, new Color(255, 60, 40) * ((0.6f + glow * 0.3f) * alpha));
                            if (blinking)
                                spriteBatch.FillRectangle(new RectangleF(eye.X - 14, eye.Y - 2, 28, 4), new Color(255, 90, 60) * alpha);
                            else
                                UITheme.FillCircle(spriteBatch, eye, 12f, new Color(255, 90, 60) * alpha);
                        }
                        break;
                    }
            }
        }

        private void DrawSupplies(SpriteBatch spriteBatch, SpriteFont font, float totalSeconds)
        {
            float headIn = Anim.Intro(_stateTime, 0f, 0.35f);
            UITheme.DrawTextWithShadow(spriteBatch, font, "You find a supply cache.", new Vector2(60 - (1f - headIn) * 20f, 220), Color.White * headIn);
            UITheme.DrawTextWithShadow(spriteBatch, font, "Every choice here costs time.", new Vector2(60 - (1f - headIn) * 20f, 250), Color.LightGray * headIn, 0.8f);

            // What's actually inside - the same numbers every option below is working from.
            // The box pops open, then its contents rise into view one by one.
            float boxIn = Anim.Intro(_stateTime, 0.05f, 0.4f);
            var lootBox = Anim.Scale(new RectangleF(60, 280, 320, 240), MathHelper.Lerp(0.9f, 1f, UITheme.EaseOutBack(boxIn)));
            float shine = 0.12f + 0.05f * UITheme.PulseSine(totalSeconds, 1.5f);
            UITheme.DrawPanel(spriteBatch, lootBox, new Color(58, 50, 30) * boxIn, new Color(38, 32, 18) * boxIn, new Color(150, 118, 64) * boxIn, 3f, 14f, shadowStrength: 0.6f * boxIn);
            UITheme.DrawGlow(spriteBatch, new Vector2(lootBox.X + lootBox.Width / 2f, lootBox.Y + lootBox.Height / 2f), 150f, new Color(255, 190, 110) * (shine * boxIn));
            UITheme.DrawTextWithShadow(spriteBatch, font, "Inside", new Vector2(lootBox.X + 18, lootBox.Y + 14), new Color(235, 210, 160) * boxIn);

            var rows = new (Texture2D icon, int amount, string name)[]
            {
                (Game1.BreadTexture, _cache.Food, "Food"),
                (Game1.PlanksTexture, _cache.Planks, "Planks"),
                (Game1.ScrapsTexture, _cache.Scraps, "Scraps")
            };
            for (int i = 0; i < rows.Length; i++)
            {
                float rowIn = Anim.Stagger(_stateTime, i, step: 0.1f, baseDelay: 0.25f, duration: 0.35f);
                float rowY = lootBox.Y + 52 + i * 60 + (1f - rowIn) * 14f;
                float bob = MathF.Sin(totalSeconds * 2f + i * 2.1f) * 2f;
                if (rows[i].icon != null)
                {
                    spriteBatch.Draw(rows[i].icon, new Rectangle((int)lootBox.X + 20, (int)(rowY + bob), 52, 52), Color.White * rowIn);
                }
                UITheme.DrawTextWithShadow(spriteBatch, font, $"{rows[i].amount} {rows[i].name}", new Vector2(lootBox.X + 90, rowY + 12), Color.White * rowIn, 1.1f);
            }

            for (int i = 0; i < _suppliesButtons.Count; i++)
            {
                DrawSupplyButton(spriteBatch, font, _suppliesButtons[i], _supplyActions[i], Anim.Stagger(_stateTime, i, step: 0.07f, baseDelay: 0.15f, duration: 0.35f));
            }

            _textLog.Draw(spriteBatch, font, new Vector2(420, 636), maxWidth: 820f);
        }

        private void DrawSupplyButton(SpriteBatch spriteBatch, SpriteFont font, Button button, SupplyAction action, float intro = 1f)
        {
            if (intro <= 0.001f) return;
            bool enabled = button.Enabled;
            float hover = button.HoverAmount;
            Color baseTop = enabled ? new Color(66, 78, 66) : new Color(44, 46, 50);
            Color baseBottom = enabled ? new Color(46, 56, 46) : new Color(32, 34, 38);
            Color top = UITheme.Brighten(baseTop, hover * 0.2f);
            Color bottom = UITheme.Brighten(baseBottom, hover * 0.2f);
            Color border = enabled ? Color.Lerp(Color.White * 0.55f, Color.White, hover) : Color.White * 0.18f;

            float squash = button.PressAmount * 3f;
            var bounds = Anim.Slide(button.Bounds, intro, new Vector2(40, 0));
            var drawBounds = new RectangleF(bounds.X + squash + hover * 4f, bounds.Y + squash / 2f, bounds.Width - squash * 2f, bounds.Height - squash);
            UITheme.DrawPanel(spriteBatch, drawBounds, top * intro, bottom * intro, border * intro, MathHelper.Lerp(2f, 3f, hover), 10f, shadowStrength: (enabled ? 0.5f : 0.2f) * intro);
            DrawHoverAccent(spriteBatch, drawBounds, hover, new Color(150, 220, 150));

            Color titleColor = (enabled ? Color.White : new Color(140, 140, 150)) * intro;
            UITheme.DrawTextWithShadow(spriteBatch, font, button.Label, new Vector2(drawBounds.X + 14, drawBounds.Y + 8), titleColor, 0.95f);

            // Time cost (and what the clock will read after), or why it's unavailable.
            int minutes = SupplyMinutes(action);
            string right;
            Color rightColor;
            string blocked = SupplyBlockedReason(action);
            if (blocked != null)
            {
                right = blocked;
                rightColor = new Color(230, 120, 105);
            }
            else if (minutes == 0)
            {
                right = "No time";
                rightColor = new Color(170, 220, 170);
            }
            else if (!_dawnTimer.CanAfford(minutes))
            {
                right = $"{DawnTimer.FormatDuration(minutes)} - dawn breaks";
                rightColor = new Color(255, 160, 100);
            }
            else
            {
                right = $"{DawnTimer.FormatDuration(minutes)}  ->  {_dawnTimer.ClockLabelAfter(minutes)}";
                rightColor = new Color(255, 205, 140);
            }
            var rightSize = UITheme.MeasureString(font, right) * 0.8f;
            UITheme.DrawTextWithShadow(spriteBatch, font, right, new Vector2(drawBounds.X + drawBounds.Width - rightSize.X - 14, drawBounds.Y + 10), rightColor * intro, 0.8f);

            const float descScale = 0.72f;
            Color descColor = (enabled ? new Color(215, 215, 215) : new Color(120, 120, 130)) * intro;
            var lines = TextLog.WrapText(font, SupplyDescription(action), (drawBounds.Width - 28) / descScale);
            for (int i = 0; i < Math.Min(lines.Count, 2); i++)
            {
                UITheme.DrawTextWithShadow(spriteBatch, font, lines[i], new Vector2(drawBounds.X + 14, drawBounds.Y + 38 + i * 18), descColor, descScale);
            }
        }

        /// <summary>Shared look for every clickable menu button on this screen: rounded
        /// gradient panel, hover-eased tint and border, and a small press-squash on click.
        /// Pass description to render a two-line button (title + a smaller detail line)
        /// like the supplies options use; omit it for a simple centered label.</summary>
        private void DrawStyledButton(SpriteBatch spriteBatch, SpriteFont font, Button button, Color baseTop, Color baseBottom, string description = null, float intro = 1f)
        {
            if (intro <= 0.001f) return;
            float hover = button.HoverAmount;
            Color top = UITheme.Brighten(baseTop, hover * 0.2f);
            Color bottom = UITheme.Brighten(baseBottom, hover * 0.2f);
            Color border = Color.Lerp(Color.White * 0.7f, Color.White, hover);
            float borderThickness = MathHelper.Lerp(2f, 3f, hover);

            // A brief inward squash while the press pulse decays, so a click reads as a
            // physical push rather than an instant color swap.
            float squash = button.PressAmount * 3f;
            var bounds = Anim.Slide(button.Bounds, intro, new Vector2(-40, 0));
            // Hovered buttons nudge right, text following, with a bright bar on the left edge.
            var drawBounds = new RectangleF(bounds.X + squash, bounds.Y + squash / 2f, bounds.Width - squash * 2f, bounds.Height - squash);
            float textShift = hover * 6f;

            UITheme.DrawPanel(spriteBatch, drawBounds, top * intro, bottom * intro, border * intro, borderThickness, 10f, shadowStrength: 0.5f * intro);
            DrawHoverAccent(spriteBatch, drawBounds, hover, new Color(255, 190, 120));

            if (string.IsNullOrEmpty(description))
            {
                var textPos = new Vector2(drawBounds.X + 12 + textShift, drawBounds.Y + (drawBounds.Height - UITheme.MeasureString(font, button.Label).Y) / 2f);
                UITheme.DrawTextWithShadow(spriteBatch, font, button.Label, textPos, Color.White * intro);
            }
            else
            {
                UITheme.DrawTextWithShadow(spriteBatch, font, button.Label, new Vector2(drawBounds.X + 12 + textShift, drawBounds.Y + 10), Color.White * intro);
                UITheme.DrawTextWithShadow(spriteBatch, font, description, new Vector2(drawBounds.X + 12 + textShift, drawBounds.Y + 45), new Color(215, 215, 215) * intro, 0.85f);
            }
        }

        /// <summary>A short glowing bar inside a hovered button's left edge.</summary>
        private static void DrawHoverAccent(SpriteBatch spriteBatch, RectangleF bounds, float hover, Color color)
        {
            if (hover <= 0.01f) return;
            float height = (bounds.Height - 20) * hover;
            UITheme.FillRoundedRect(spriteBatch, new RectangleF(bounds.X + 5, bounds.Y + (bounds.Height - height) / 2f, 4, height), color * hover, 2f);
        }
    }
}
