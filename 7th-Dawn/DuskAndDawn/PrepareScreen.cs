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
    public class PreparationScreen : GameScreen
    {
        private Game1 Game1 => (Game1)Game;
        private readonly PlayerState _playerState;

        // One landmark per district. The button is only used for its hover/press animation
        // and square hitbox - it stays Enabled even for locked districts, so they can still
        // be hovered and scouted; the lock is checked on click instead.
        private readonly List<Button> _landmarkButtons = new List<Button>();
        private readonly List<Button> _weaponButtons = new List<Button>();
        private Button _startButton;
        private MouseState _previousMouse;

        // ---- Layout ----
        private static readonly RectangleF MapPanel = new RectangleF(40, 64, 660, 632);
        private static readonly RectangleF LoadoutPanel = new RectangleF(720, 64, 520, 632);

        private static readonly Vector2 ShelterPoint = new Vector2(130, 610);

        // Landmark centers, in DistrictInfo.All order - a road winding from the shelter up
        // toward the Keep, so distance on the map reads as danger.
        private static readonly Vector2[] LandmarkPoints =
        {
            new Vector2(240, 470), // Village Outskirts
            new Vector2(420, 330), // Church Ruins
            new Vector2(575, 185)  // Castle Keep
        };

        private const float LandmarkRadius = 30f;
        private const float LandmarkHitHalfSize = 40f;
        private const float TooltipWidth = 340f;
        private const float TooltipPadding = 14f;

        // Purely decorative scenery for the map.
        private static readonly Vector2[] RiverPoints =
        {
            new Vector2(330, 64), new Vector2(310, 160), new Vector2(350, 250), new Vector2(300, 380),
            new Vector2(340, 470), new Vector2(310, 560), new Vector2(330, 696)
        };

        private static readonly Vector2[] TreePoints =
        {
            new Vector2(90, 200), new Vector2(112, 228), new Vector2(150, 182), new Vector2(200, 300),
            new Vector2(90, 400), new Vector2(470, 160), new Vector2(180, 120), new Vector2(520, 560),
            new Vector2(556, 598), new Vector2(600, 540), new Vector2(640, 420), new Vector2(620, 640),
            new Vector2(500, 450), new Vector2(230, 640)
        };

        // Weapon cards: 2 per row, up to 4 rows. The Workshop can fill this over a long run.
        private const int WeaponsPerRow = 2;
        private const float WeaponCardWidth = 232f;
        private const float WeaponCardHeight = 76f;
        private const int IconScale = 2; // 32px icons drawn at 64px
        private const int MaxWeaponRows = 4;

        public PreparationScreen(Game game, PlayerState playerState) : base(game)
        {
            _playerState = playerState;
        }

        public override void Initialize()
        {
            base.Initialize();

            // Seeds with the real current mouse state instead of a blank default, so a click
            // still held down from the previous screen doesn't read as a brand-new click here.
            _previousMouse = Mouse.GetState();

            // If the saved choice is somehow locked (shouldn't happen, Archive never
            // downgrades), fall back to the Outskirts.
            if (!_playerState.IsDistrictUnlocked(_playerState.SelectedDistrict))
            {
                _playerState.SelectedDistrict = District.VillageOutskirts;
            }

            _landmarkButtons.Clear();
            for (int i = 0; i < DistrictInfo.All.Length; i++)
            {
                var point = LandmarkPoints[i];
                var bounds = new RectangleF(point.X - LandmarkHitHalfSize, point.Y - LandmarkHitHalfSize, LandmarkHitHalfSize * 2f, LandmarkHitHalfSize * 2f);
                _landmarkButtons.Add(new Button(bounds, DistrictInfo.Name(DistrictInfo.All[i])));
            }

            _weaponButtons.Clear();
            int shown = Math.Min(_playerState.Inventory.Count, WeaponsPerRow * MaxWeaponRows);
            for (int i = 0; i < shown; i++)
            {
                float x = LoadoutPanel.X + 20 + (i % WeaponsPerRow) * (WeaponCardWidth + 16);
                float y = 112 + (i / WeaponsPerRow) * (WeaponCardHeight + 10);
                _weaponButtons.Add(new Button(new RectangleF(x, y, WeaponCardWidth, WeaponCardHeight), _playerState.Inventory[i].Name));
            }

            _startButton = new Button(new RectangleF(LoadoutPanel.X + 20, 620, LoadoutPanel.Width - 40, 60), "Head Into the Night");
        }

        public override void Update(GameTime gameTime)
        {
            if (_playerState.IsGameOver)
            {
                ScreenManager.ReplaceScreen(new GameOverScreen(Game), ScreenTransitions.FadeTransition(GraphicsDevice));
                return;
            }

            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            var mouse = Mouse.GetState();

            bool hitStart = _startButton.Contains(mouse.X, mouse.Y);
            _startButton.UpdateAnimation(dt, hitStart);
            foreach (var button in _weaponButtons)
            {
                button.UpdateAnimation(dt, button.Contains(mouse.X, mouse.Y));
            }
            foreach (var button in _landmarkButtons)
            {
                button.UpdateAnimation(dt, button.Contains(mouse.X, mouse.Y));
            }

            if (InputChecker.IsNewLeftClick(mouse, _previousMouse))
            {
                for (int i = 0; i < _landmarkButtons.Count; i++)
                {
                    var district = DistrictInfo.All[i];
                    if (_landmarkButtons[i].Contains(mouse.X, mouse.Y) && _playerState.IsDistrictUnlocked(district))
                    {
                        _landmarkButtons[i].TriggerPress();
                        _playerState.SelectedDistrict = district;
                    }
                }

                for (int i = 0; i < _weaponButtons.Count; i++)
                {
                    if (_weaponButtons[i].Contains(mouse.X, mouse.Y))
                    {
                        _weaponButtons[i].TriggerPress();
                        _playerState.EquippedWeapon = _playerState.Inventory[i];
                    }
                }

                if (hitStart)
                {
                    _startButton.TriggerPress();
                    ScreenManager.ShowScreen(new NightScavengingScreen(Game, _playerState), ScreenTransitions.FadeTransition(GraphicsDevice));
                }
            }

            _previousMouse = mouse;
        }

        public override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(new Color(15, 15, 25));

            var spriteBatch = Game1.SpriteBatch;
            var font = Game1.Font;
            float totalSeconds = (float)gameTime.TotalGameTime.TotalSeconds;
            spriteBatch.Begin();

            UITheme.FillGradientRect(spriteBatch, new RectangleF(0, 0, 1280, 720), new Color(22, 20, 34), new Color(10, 9, 16), 10);
            UITheme.DrawTextWithShadow(spriteBatch, font, "Prepare for the Night", new Vector2(40, 22), Color.White);

            DrawMap(spriteBatch, font, totalSeconds);

            UITheme.DrawPanel(spriteBatch, LoadoutPanel, new Color(30, 28, 42), new Color(18, 17, 26), Color.White * 0.15f, 2f, 16f, shadowStrength: 0.6f);
            DrawWeapons(spriteBatch, font);
            DrawItemsSummary(spriteBatch, font);
            DrawDestination(spriteBatch, font);
            DrawStartButton(spriteBatch, font);

            // Last, so it sits over the loadout panel when a landmark near the edge is hovered.
            DrawLandmarkTooltip(spriteBatch, font);

            spriteBatch.End();
        }

        // =====================================================================
        // Map
        // =====================================================================

        private void DrawMap(SpriteBatch spriteBatch, SpriteFont font, float totalSeconds)
        {
            UITheme.DrawPanel(spriteBatch, MapPanel, new Color(30, 38, 44), new Color(16, 20, 26), new Color(150, 120, 70) * 0.7f, 2f, 16f, shadowStrength: 0.6f);
            DrawScenery(spriteBatch);

            // Road from the shelter through every district in order. Stretches leading to a
            // locked district stay dim, so how far you can reach tonight is readable at a glance.
            var previous = ShelterPoint;
            for (int i = 0; i < LandmarkPoints.Length; i++)
            {
                bool reachable = _playerState.IsDistrictUnlocked(DistrictInfo.All[i]);
                Color roadColor = reachable ? new Color(225, 195, 140) * 0.75f : Color.White * 0.15f;
                DrawDashedLine(spriteBatch, previous, LandmarkPoints[i], roadColor, 3f);
                previous = LandmarkPoints[i];
            }

            DrawShelter(spriteBatch, font);

            for (int i = 0; i < _landmarkButtons.Count; i++)
            {
                DrawLandmark(spriteBatch, font, i, totalSeconds);
            }

            UITheme.DrawTextWithShadow(spriteBatch, font, "Scavenging map", new Vector2(MapPanel.X + 18, MapPanel.Y + 14), new Color(225, 205, 165));
            UITheme.DrawTextWithShadow(spriteBatch, font, "Hover a landmark to scout it, click to choose.", new Vector2(MapPanel.X + 18, MapPanel.Y + MapPanel.Height - 30), new Color(190, 180, 165), 0.65f);

            // A little compass rose in the corner.
            var compass = new Vector2(MapPanel.X + MapPanel.Width - 40, MapPanel.Y + 50);
            spriteBatch.DrawLine(compass + new Vector2(0, -18), compass + new Vector2(0, 18), new Color(225, 205, 165) * 0.6f, 2f);
            spriteBatch.DrawLine(compass + new Vector2(-12, 0), compass + new Vector2(12, 0), new Color(225, 205, 165) * 0.4f, 2f);
            UITheme.DrawTextWithShadow(spriteBatch, font, "N", compass + new Vector2(-6, -44), new Color(225, 205, 165), 0.75f);
        }

        private void DrawScenery(SpriteBatch spriteBatch)
        {
            // Faint survey grid.
            for (float x = MapPanel.X + 60; x < MapPanel.X + MapPanel.Width; x += 60)
            {
                spriteBatch.DrawLine(new Vector2(x, MapPanel.Y + 4), new Vector2(x, MapPanel.Y + MapPanel.Height - 4), Color.White * 0.03f, 1f);
            }
            for (float y = MapPanel.Y + 60; y < MapPanel.Y + MapPanel.Height; y += 60)
            {
                spriteBatch.DrawLine(new Vector2(MapPanel.X + 4, y), new Vector2(MapPanel.X + MapPanel.Width - 4, y), Color.White * 0.03f, 1f);
            }

            // River - a wide dark band with a lighter current down the middle.
            for (int i = 0; i < RiverPoints.Length - 1; i++)
            {
                spriteBatch.DrawLine(RiverPoints[i], RiverPoints[i + 1], new Color(26, 44, 66), 12f);
            }
            // Round off the bends so the segments don't show notches at the joins.
            for (int i = 1; i < RiverPoints.Length - 1; i++)
            {
                FillCircle(spriteBatch, RiverPoints[i], 6f, new Color(26, 44, 66));
            }
            for (int i = 0; i < RiverPoints.Length - 1; i++)
            {
                spriteBatch.DrawLine(RiverPoints[i], RiverPoints[i + 1], new Color(70, 110, 150) * 0.35f, 4f);
            }

            foreach (var tree in TreePoints)
            {
                FillCircle(spriteBatch, tree + new Vector2(2, 3), 9f, Color.Black * 0.3f);
                FillCircle(spriteBatch, tree, 9f, new Color(28, 52, 38));
                FillCircle(spriteBatch, tree + new Vector2(-2, -2), 5f, new Color(42, 74, 52));
            }
        }

        private void DrawShelter(SpriteBatch spriteBatch, SpriteFont font)
        {
            // Warm lit window - home base, where every road starts.
            var house = new RectangleF(ShelterPoint.X - 18, ShelterPoint.Y - 14, 36, 28);
            UITheme.DrawPanel(spriteBatch, house, new Color(120, 86, 52), new Color(84, 58, 34), new Color(235, 200, 140), 2f, 5f, shadowStrength: 0.5f);
            UITheme.FillRoundedRect(spriteBatch, new RectangleF(ShelterPoint.X - 6, ShelterPoint.Y - 6, 12, 12), new Color(255, 215, 130), 2f);

            DrawCenteredText(spriteBatch, font, "Your Shelter", new Vector2(ShelterPoint.X, ShelterPoint.Y + 22), new Color(235, 215, 180), 0.7f);
        }

        private void DrawLandmark(SpriteBatch spriteBatch, SpriteFont font, int index, float totalSeconds)
        {
            var district = DistrictInfo.All[index];
            var button = _landmarkButtons[index];
            var center = LandmarkPoints[index];
            bool unlocked = _playerState.IsDistrictUnlocked(district);
            bool selected = unlocked && district == _playerState.SelectedDistrict;
            float hover = button.HoverAmount;

            // Press squash, then a slight grow on hover.
            float radius = LandmarkRadius * (1f + hover * 0.12f - button.PressAmount * 0.1f);

            // Selected: a slow ember halo, the same "corruption glow" tell used on the night map.
            if (selected)
            {
                float pulse = UITheme.PulseSine(totalSeconds, 2.5f);
                FillCircle(spriteBatch, center, radius + 18f + pulse * 6f, new Color(255, 120, 50) * (0.08f + pulse * 0.06f));
                FillCircle(spriteBatch, center, radius + 10f, new Color(255, 140, 70) * 0.18f);
            }
            else if (hover > 0f)
            {
                FillCircle(spriteBatch, center, radius + 12f * hover, Color.White * (0.08f * hover));
            }

            Color fill = unlocked ? LandmarkColor(district) : new Color(58, 56, 66);
            Color border = selected
                ? new Color(255, 170, 90)
                : Color.Lerp(unlocked ? Color.White * 0.6f : Color.White * 0.25f, Color.White, hover);

            FillCircle(spriteBatch, center + new Vector2(3, 5), radius, Color.Black * 0.35f);
            FillCircle(spriteBatch, center, radius + (selected ? 3f : 2f), border);
            FillCircle(spriteBatch, center, radius, UITheme.Brighten(fill, hover * 0.15f));
            FillCircle(spriteBatch, center + new Vector2(0, -radius * 0.25f), radius * 0.7f, Color.White * 0.06f);

            // The district's signature material sits in the landmark, so the map itself says
            // what each place is good for before you even hover it.
            var icon = MaterialIcon(DistrictInfo.SpecialtyMaterial(district));
            DrawMaterialIcon(spriteBatch, icon, center, radius * 1.45f, unlocked ? Color.White : new Color(130, 125, 135));

            if (selected)
            {
                DrawCenteredText(spriteBatch, font, "TONIGHT", new Vector2(center.X, center.Y - radius - 30), new Color(255, 215, 150), 0.65f);
            }

            // Dark plate behind the name so the road never cuts through the text.
            string name = DistrictInfo.Name(district);
            var nameSize = font.MeasureString(name) * 0.8f;
            var namePos = new Vector2(center.X, center.Y + radius + 8);
            UITheme.FillRoundedRect(spriteBatch, new RectangleF(namePos.X - nameSize.X / 2f - 6, namePos.Y - 2, nameSize.X + 12, nameSize.Y + 4), Color.Black * 0.35f, 6f);

            Color nameColor = unlocked ? Color.White : new Color(140, 135, 145);
            DrawCenteredText(spriteBatch, font, name, namePos, nameColor, 0.8f);

            if (unlocked)
            {
                // Corruption tier as ember pips under the name.
                int tier = DistrictInfo.Corruption(district);
                const int maxTier = 3;
                const float pip = 5f, gap = 6f;
                float startX = center.X - (maxTier * pip * 2f + (maxTier - 1) * gap) / 2f + pip;
                float pipY = center.Y + radius + 38;
                for (int t = 0; t < maxTier; t++)
                {
                    var pipCenter = new Vector2(startX + t * (pip * 2f + gap), pipY);
                    FillCircle(spriteBatch, pipCenter, pip, t < tier ? new Color(255, 140, 70) : Color.White * 0.15f);
                }
            }
            else
            {
                DrawCenteredText(spriteBatch, font, $"Locked - Archive Lv {DistrictInfo.RequiredArchiveLevel(district)}", new Vector2(center.X, center.Y + radius + 30), new Color(160, 150, 160), 0.6f);
            }
        }

        // =====================================================================
        // Landmark tooltip
        // =====================================================================

        private void DrawLandmarkTooltip(SpriteBatch spriteBatch, SpriteFont font)
        {
            // Whichever landmark is most hovered owns the tooltip, faded by its hover amount -
            // so moving between landmarks cross-fades instead of popping.
            int index = -1;
            float best = 0f;
            for (int i = 0; i < _landmarkButtons.Count; i++)
            {
                if (_landmarkButtons[i].HoverAmount > best)
                {
                    best = _landmarkButtons[i].HoverAmount;
                    index = i;
                }
            }
            if (index < 0) return;

            float alpha = UITheme.EaseOutCubic(best);
            var district = DistrictInfo.All[index];
            var center = LandmarkPoints[index];

            // Measure first, then place it beside the landmark, flipped to the left if it
            // would run off the right edge, and clamped vertically to the screen.
            float height = LayoutTooltip(spriteBatch, font, district, Vector2.Zero, 0f, 0f, draw: false);
            float x = center.X + LandmarkRadius + 22f;
            if (x + TooltipWidth > 1260f)
            {
                x = center.X - LandmarkRadius - 22f - TooltipWidth;
            }
            float y = MathHelper.Clamp(center.Y - 60f, 20f, 700f - height);
            var topLeft = new Vector2(x + (1f - alpha) * 10f, y);

            LayoutTooltip(spriteBatch, font, district, topLeft, height, alpha, draw: true);
        }

        /// <summary>Lays out the district tooltip top-down. With draw = false it only measures
        /// and returns the height, so the caller can size and position the panel first.</summary>
        private float LayoutTooltip(SpriteBatch spriteBatch, SpriteFont font, District district, Vector2 topLeft, float panelHeight, float alpha, bool draw)
        {
            bool unlocked = _playerState.IsDistrictUnlocked(district);
            bool selected = unlocked && district == _playerState.SelectedDistrict;
            float innerWidth = TooltipWidth - TooltipPadding * 2f;
            float left = topLeft.X + TooltipPadding;
            float y = topLeft.Y + TooltipPadding;

            if (draw)
            {
                var panel = new RectangleF(topLeft.X, topLeft.Y, TooltipWidth, panelHeight);
                Color accent = unlocked ? LandmarkColor(district) : new Color(90, 88, 96);
                UITheme.DrawPanel(spriteBatch, panel, new Color(34, 30, 44) * alpha, new Color(20, 18, 28) * alpha, UITheme.Brighten(accent, 0.3f) * alpha, 2f, 12f, shadowStrength: 0.8f * alpha);
            }

            void Text(string text, float lineX, Color color, float scale)
            {
                if (draw) UITheme.DrawTextWithShadow(spriteBatch, font, text, new Vector2(lineX, y), color * alpha, scale);
            }

            // Word-wrapped paragraph, advancing y by one line per wrapped row.
            void Wrapped(string text, Color color, float scale)
            {
                foreach (var line in TextLog.WrapText(font, text, innerWidth / scale))
                {
                    Text(line, left, color, scale);
                    y += 20f;
                }
            }

            void Divider()
            {
                y += 6f;
                if (draw) spriteBatch.DrawLine(new Vector2(left, y), new Vector2(left + innerWidth, y), Color.White * (0.15f * alpha), 1f);
                y += 8f;
            }

            // ---- Header ----
            Text(DistrictInfo.Name(district), left, Color.White, 1f);
            string corruption = $"Corruption {DistrictInfo.Corruption(district)}";
            float corruptionWidth = font.MeasureString(corruption).X * 0.7f;
            Text(corruption, left + innerWidth - corruptionWidth, new Color(255, 180, 120), 0.7f);
            y += 28f;

            Wrapped(DistrictInfo.Tagline(district), new Color(210, 205, 215), 0.7f);
            Divider();

            // ---- Materials ----
            Text("Materials per find", left, new Color(225, 205, 165), 0.75f);
            y += 24f;

            var materials = DistrictInfo.Yield(district);
            string specialty = DistrictInfo.SpecialtyMaterial(district);
            MaterialRow("Food", materials.Food);
            MaterialRow("Planks", materials.Planks);
            MaterialRow("Scraps", materials.Scraps);

            void MaterialRow(string name, LootRange range)
            {
                const float rowHeight = 30f;
                bool rich = name == specialty;
                if (draw)
                {
                    if (rich)
                    {
                        UITheme.FillRoundedRect(spriteBatch, new RectangleF(left - 4, y - 2, innerWidth + 8, rowHeight), new Color(255, 200, 110) * (0.12f * alpha), 6f);
                    }
                    DrawMaterialIcon(spriteBatch, MaterialIcon(name), new Vector2(left + 13, y + rowHeight / 2f - 1), 26f, Color.White * alpha);
                }

                Color color = rich ? new Color(255, 220, 150) : new Color(220, 220, 225);
                float textY = y;
                y += 3f;
                Text(name, left + 34, color, 0.8f);
                if (rich) Text("RICH", left + 120, new Color(255, 190, 90), 0.6f);
                string amount = range.ToString();
                float amountWidth = font.MeasureString(amount).X * 0.85f;
                Text(amount, left + innerWidth - amountWidth, color, 0.85f);
                y = textY + rowHeight;
            }

            y += 2f;
            Wrapped("Thorough searches roll twice. The Hoard rolls three times.", new Color(175, 175, 185), 0.65f);
            Divider();

            // ---- Rooms & enemies ----
            var (supplies, encounter, special, empty) = RoomGenerator.Odds(district, 0);
            Wrapped($"Near the entrance: {Percent(supplies)} supplies, {Percent(encounter)} fights, {Percent(special)} strange, {Percent(empty)} quiet", new Color(210, 210, 220), 0.62f);

            int weaponChance = DistrictInfo.WeaponFindChance(district);
            Wrapped($"Special rooms: {weaponChance}% weapon, {100 - weaponChance}% material cache", new Color(200, 185, 230), 0.62f);

            int hp = DistrictInfo.EnemyHealthBonus(district);
            int atk = DistrictInfo.EnemyAttackBonus(district);
            string enemies = hp == 0 && atk == 0 ? "Enemies: ordinary strength" : $"Enemies: +{hp} health, +{atk} attack";
            Wrapped(enemies, new Color(235, 150, 140), 0.62f);
            Divider();

            // ---- Footer ----
            if (!unlocked)
            {
                Wrapped($"Locked - upgrade the Archive to Lv {DistrictInfo.RequiredArchiveLevel(district)}", new Color(235, 130, 115), 0.7f);
            }
            else if (selected)
            {
                Wrapped("Tonight's destination", new Color(255, 215, 150), 0.7f);
            }
            else
            {
                Wrapped("Click to scavenge here tonight", new Color(150, 220, 160), 0.7f);
            }

            return y + TooltipPadding - topLeft.Y;
        }

        private static string Percent(float ratio) => $"{(int)MathF.Round(ratio * 100f)}%";

        // =====================================================================
        // Loadout
        // =====================================================================

        private void DrawWeapons(SpriteBatch spriteBatch, SpriteFont font)
        {
            UITheme.DrawTextWithShadow(spriteBatch, font, "Choose your weapon", new Vector2(LoadoutPanel.X + 20, 78), Color.White);

            for (int i = 0; i < _weaponButtons.Count; i++)
            {
                var weapon = _playerState.Inventory[i];
                var button = _weaponButtons[i];
                bool equipped = weapon == _playerState.EquippedWeapon;
                float hover = button.HoverAmount;

                Color top = equipped ? new Color(150, 112, 44) : new Color(66, 66, 78);
                Color bottom = equipped ? new Color(110, 80, 28) : new Color(46, 46, 56);
                Color border = equipped ? Color.Gold : Color.Lerp(Color.White * 0.6f, Color.White, hover);
                float borderThickness = equipped ? 3f : MathHelper.Lerp(1.5f, 3f, hover);

                if (!equipped && hover > 0f)
                {
                    top = UITheme.Brighten(top, hover * 0.2f);
                    bottom = UITheme.Brighten(bottom, hover * 0.2f);
                }

                float squash = button.PressAmount * 3f;
                var bounds = button.Bounds;
                var drawBounds = new RectangleF(bounds.X + squash, bounds.Y + squash / 2f, bounds.Width - squash * 2f, bounds.Height - squash);

                UITheme.DrawPanel(spriteBatch, drawBounds, top, bottom, border, borderThickness, 12f, shadowStrength: 0.6f);

                // Icon on the left (empty slot if the weapon has no art yet), text beside it.
                float iconSize = 32 * IconScale;
                var iconPos = new Vector2(drawBounds.X + 10, drawBounds.Y + (drawBounds.Height - iconSize) / 2f);
                UITheme.DrawIconSlot(spriteBatch, Game1.GetWeaponIcon(weapon), iconPos, IconScale);

                float textX = drawBounds.X + 10 + iconSize + 12;
                UITheme.DrawTextWithShadow(spriteBatch, font, weapon.Name, new Vector2(textX, drawBounds.Y + 8), Color.White, 0.85f);

                string stats = weapon.IsHoly ? $"{weapon.DiceLabel}  +{weapon.CorruptionBonus}/corr" : weapon.DiceLabel;
                UITheme.DrawTextWithShadow(spriteBatch, font, stats, new Vector2(textX, drawBounds.Y + 32), new Color(215, 215, 215), 0.75f);

                if (equipped)
                {
                    UITheme.DrawTextWithShadow(spriteBatch, font, "Equipped", new Vector2(textX, drawBounds.Y + drawBounds.Height - 22), new Color(255, 235, 190), 0.7f);
                }
            }

            if (_playerState.Inventory.Count > _weaponButtons.Count)
            {
                int hidden = _playerState.Inventory.Count - _weaponButtons.Count;
                UITheme.DrawTextWithShadow(spriteBatch, font, $"+{hidden} more not shown", new Vector2(LoadoutPanel.X + 20, 452), new Color(170, 165, 175), 0.75f);
            }
        }

        // Read-only summary of what's in the pack - items are crafted at the Infirmary and
        // Kitchen, and every one you own comes along.
        private void DrawItemsSummary(SpriteBatch spriteBatch, SpriteFont font)
        {
            float x = LoadoutPanel.X + 20;
            UITheme.DrawTextWithShadow(spriteBatch, font, "Pack", new Vector2(x, 480), Color.White, 0.9f);

            string summary = _playerState.Items.Count == 0
                ? "Empty - craft items at the Infirmary and Kitchen."
                : string.Join(", ", _playerState.Items
                    .GroupBy(item => item.Name)
                    .Select(g => g.Count() > 1 ? $"{g.Key} x{g.Count()}" : g.Key));

            const float scale = 0.75f;
            var lines = TextLog.WrapText(font, summary, (LoadoutPanel.Width - 40) / scale);
            for (int i = 0; i < Math.Min(lines.Count, 2); i++)
            {
                string line = (i == 1 && lines.Count > 2) ? lines[i] + " ..." : lines[i];
                UITheme.DrawTextWithShadow(spriteBatch, font, line, new Vector2(x, 508 + i * 20), new Color(210, 220, 200), scale);
            }
        }

        private void DrawDestination(SpriteBatch spriteBatch, SpriteFont font)
        {
            var district = _playerState.SelectedDistrict;
            var box = new RectangleF(LoadoutPanel.X + 20, 556, LoadoutPanel.Width - 40, 50);
            UITheme.DrawPanel(spriteBatch, box, UITheme.Darken(LandmarkColor(district), 0.45f), UITheme.Darken(LandmarkColor(district), 0.7f), new Color(255, 170, 90) * 0.6f, 2f, 10f, shadowStrength: 0.4f);

            string specialty = DistrictInfo.SpecialtyMaterial(district);
            DrawMaterialIcon(spriteBatch, MaterialIcon(specialty), new Vector2(box.X + 26, box.Y + box.Height / 2f), 32f, Color.White);
            UITheme.DrawTextWithShadow(spriteBatch, font, $"Tonight: {DistrictInfo.Name(district)}", new Vector2(box.X + 50, box.Y + 5), Color.White, 0.85f);
            UITheme.DrawTextWithShadow(spriteBatch, font, $"Rich in {specialty}  -  Corruption {DistrictInfo.Corruption(district)}", new Vector2(box.X + 50, box.Y + 28), new Color(255, 200, 150), 0.62f);
        }

        private void DrawStartButton(SpriteBatch spriteBatch, SpriteFont font)
        {
            float hover = _startButton.HoverAmount;
            Color top = Color.Lerp(new Color(85, 145, 95), new Color(105, 170, 115), hover);
            Color bottom = Color.Lerp(new Color(60, 110, 68), new Color(78, 132, 86), hover);
            Color border = Color.Lerp(Color.White * 0.8f, Color.White, hover);
            float borderThickness = MathHelper.Lerp(2f, 3f, hover);

            float squash = _startButton.PressAmount * 4f;
            var bounds = _startButton.Bounds;
            var drawBounds = new RectangleF(bounds.X + squash, bounds.Y + squash / 2f, bounds.Width - squash * 2f, bounds.Height - squash);

            UITheme.DrawPanel(spriteBatch, drawBounds, top, bottom, border, borderThickness, 14f, shadowStrength: 0.7f);
            var textSize = font.MeasureString(_startButton.Label);
            var textPos = new Vector2(drawBounds.X + (drawBounds.Width - textSize.X) / 2f, drawBounds.Y + (drawBounds.Height - textSize.Y) / 2f);
            UITheme.DrawTextWithShadow(spriteBatch, font, _startButton.Label, textPos, Color.White);
        }

        // =====================================================================
        // Helpers
        // =====================================================================

        private static Color LandmarkColor(District district) => district switch
        {
            District.VillageOutskirts => new Color(104, 132, 64),  // mossy fields
            District.ChurchRuins => new Color(112, 78, 150),       // stained-glass violet
            District.CastleKeep => new Color(150, 56, 48),         // banner crimson
            _ => new Color(80, 80, 90)
        };

        private Texture2D MaterialIcon(string material) => material switch
        {
            "Food" => Game1.BreadTexture,
            "Planks" => Game1.PlanksTexture,
            "Scraps" => Game1.ScrapsTexture,
            _ => null
        };

        // The material sprites are 320x320 canvases - scaled to fit `size` and drawn from
        // their own center, the same way the base screen's resource slots draw them.
        private static void DrawMaterialIcon(SpriteBatch spriteBatch, Texture2D texture, Vector2 center, float size, Color tint)
        {
            if (texture == null) return;
            float scale = size / Math.Max(texture.Width, texture.Height);
            var origin = new Vector2(texture.Width / 2f, texture.Height / 2f);
            spriteBatch.Draw(texture, center, null, tint, 0f, origin, scale, SpriteEffects.None, 0f);
        }

        // A circle is just a rounded rect whose corner radius is half its size.
        private static void FillCircle(SpriteBatch spriteBatch, Vector2 center, float radius, Color color)
        {
            UITheme.FillRoundedRect(spriteBatch, new RectangleF(center.X - radius, center.Y - radius, radius * 2f, radius * 2f), color, radius);
        }

        private static void DrawDashedLine(SpriteBatch spriteBatch, Vector2 from, Vector2 to, Color color, float thickness)
        {
            const float dash = 10f, gap = 8f;
            float length = Vector2.Distance(from, to);
            if (length < 1f) return;
            var direction = (to - from) / length;
            for (float d = 0f; d < length; d += dash + gap)
            {
                float end = Math.Min(d + dash, length);
                spriteBatch.DrawLine(from + direction * d, from + direction * end, color, thickness);
            }
        }

        private static void DrawCenteredText(SpriteBatch spriteBatch, SpriteFont font, string text, Vector2 topCenter, Color color, float scale)
        {
            float width = font.MeasureString(text).X * scale;
            UITheme.DrawTextWithShadow(spriteBatch, font, text, new Vector2(topCenter.X - width / 2f, topCenter.Y), color, scale);
        }
    }
}
