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
    public partial class PreparationScreen : GameScreen, IGameplayScreen
    {
        private Game1 Game1 => (Game1)Game;
        private readonly PlayerState _playerState;

        private readonly List<Button> _landmarkButtons = new List<Button>();
        private readonly List<Button> _weaponButtons = new List<Button>();
        private readonly List<int> _weaponIndices = new List<int>();
        private int _weaponPage;
        private Button _prevPageButton, _nextPageButton;
        private Button _startButton;
        private Button _backButton;
        private bool _leaving;
        private MouseState _previousMouse;

        private float _elapsed;
        private const float RoadAt = 0.15f, RoadStep = 0.28f;

        private static readonly RectangleF MapPanel = new RectangleF(40, 64, 660, 632);
        private static readonly RectangleF LoadoutPanel = new RectangleF(720, 64, 520, 632);

        private static readonly Vector2 ShelterPoint = new Vector2(130, 610);

        private static readonly Vector2[] LandmarkPoints =
        {
            new Vector2(240, 470),
            new Vector2(410, 335),
            new Vector2(520, 225),
            new Vector2(620, 134)
        };

        private const float LandmarkRadius = 30f;
        private const float LandmarkHitHalfSize = 40f;
        private const float TooltipWidth = 340f;
        private const float TooltipPadding = 14f;

        private static readonly Vector2[] RiverPoints =
        {
            new Vector2(330, 64), new Vector2(310, 160), new Vector2(350, 250), new Vector2(300, 380),
            new Vector2(340, 470), new Vector2(310, 560), new Vector2(330, 696)
        };

        private static readonly Vector2[] TreePoints =
        {
            new Vector2(90, 200), new Vector2(112, 228), new Vector2(150, 182), new Vector2(200, 300),
            new Vector2(90, 400), new Vector2(470, 160), new Vector2(180, 120), new Vector2(520, 560),
            new Vector2(556, 598), new Vector2(600, 540), new Vector2(640, 420), new Vector2(560, 650),
            new Vector2(500, 450), new Vector2(230, 640)
        };

        private const int WeaponsPerRow = 2;
        private const float WeaponCardWidth = 232f;
        private const float WeaponCardHeight = 76f;
        private const int IconScale = 2;
        private const int MaxWeaponRows = 3;
        private const int WeaponsPerPage = WeaponsPerRow * MaxWeaponRows;
        private int WeaponPageCount => Math.Max(1, (_playerState.Inventory.Count + WeaponsPerPage - 1) / WeaponsPerPage);

        public PreparationScreen(Game game, PlayerState playerState) : base(game)
        {
            _playerState = playerState;
        }

        public override void Initialize()
        {
            base.Initialize();

            _previousMouse = InputChecker.GetMouse();

            if (DayInfo.IsFinalNight(_playerState.Day))
            {
                _playerState.SelectedDistrict = District.Castle;
            }
            else if (!_playerState.IsDistrictUnlocked(_playerState.SelectedDistrict))
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

            _weaponPage = Math.Max(0, _playerState.Inventory.IndexOf(_playerState.EquippedWeapon)) / WeaponsPerPage;
            _prevPageButton = new Button(new RectangleF(LoadoutPanel.X + 20, 360, 90, 22), "< Prev");
            _nextPageButton = new Button(new RectangleF(LoadoutPanel.Right - 110, 360, 90, 22), "Next >");
            LayoutWeapons();
            InitializeBelt();

            _backButton = new Button(new RectangleF(LoadoutPanel.X + 20, 620, 170, 60), "Back to Base");
            _startButton = new Button(new RectangleF(LoadoutPanel.X + 204, 620, LoadoutPanel.Width - 224, 60), DayInfo.IsFinalNight(_playerState.Day) ? "Face the Sun Herald" : "Head Into the Night");
        }

        private void LayoutWeapons()
        {
            _weaponButtons.Clear();
            _weaponIndices.Clear();
            _weaponPage = Math.Clamp(_weaponPage, 0, WeaponPageCount - 1);
            int first = _weaponPage * WeaponsPerPage;
            int shown = Math.Min(WeaponsPerPage, _playerState.Inventory.Count - first);
            for (int i = 0; i < shown; i++)
            {
                float x = LoadoutPanel.X + 20 + (i % WeaponsPerRow) * (WeaponCardWidth + 16);
                float y = 112 + (i / WeaponsPerRow) * (WeaponCardHeight + 10);
                _weaponButtons.Add(new Button(new RectangleF(x, y, WeaponCardWidth, WeaponCardHeight), _playerState.Inventory[first + i].Name));
                _weaponIndices.Add(first + i);
            }
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
            var mouse = InputChecker.GetMouse();

            bool hitStart = _startButton.Contains(mouse.X, mouse.Y);
            bool hitBack = _backButton.Contains(mouse.X, mouse.Y);
            _startButton.UpdateAnimation(dt, hitStart);
            _backButton.UpdateAnimation(dt, hitBack);
            foreach (var button in _weaponButtons)
            {
                button.UpdateAnimation(dt, button.Contains(mouse.X, mouse.Y));
            }
            bool paged = WeaponPageCount > 1;
            _prevPageButton.UpdateAnimation(dt, paged && _prevPageButton.Contains(mouse.X, mouse.Y));
            _nextPageButton.UpdateAnimation(dt, paged && _nextPageButton.Contains(mouse.X, mouse.Y));
            UpdateBelt(dt, mouse);
            foreach (var button in _landmarkButtons)
            {
                button.UpdateAnimation(dt, button.Contains(mouse.X, mouse.Y));
            }

            if (InputChecker.IsNewLeftClick(mouse, _previousMouse) && !_leaving)
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
                        _playerState.EquippedWeapon = _playerState.Inventory[_weaponIndices[i]];
                    }
                }

                if (paged && (_prevPageButton.Contains(mouse.X, mouse.Y) || _nextPageButton.Contains(mouse.X, mouse.Y)))
                {
                    bool next = _nextPageButton.Contains(mouse.X, mouse.Y);
                    (next ? _nextPageButton : _prevPageButton).TriggerPress();
                    _weaponPage = (_weaponPage + (next ? 1 : WeaponPageCount - 1)) % WeaponPageCount;
                    LayoutWeapons();
                }

                HandleBeltClick(mouse.X, mouse.Y);

                if (hitStart)
                {
                    _startButton.TriggerPress();
                    _leaving = true;
                    ScreenManager.ReplaceScreen(new NightScavengingScreen(Game, _playerState), ScreenTransitions.FadeTransition(GraphicsDevice));
                }
                else if (hitBack)
                {
                    _backButton.TriggerPress();
                    _leaving = true;
                    ScreenManager.ReplaceScreen(new BaseBuilding(Game, _playerState), ScreenTransitions.FadeTransition(GraphicsDevice));
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
            UITheme.BeginCanvas(spriteBatch);

            Backdrop.Sky(spriteBatch, new Color(22, 20, 36), new Color(40, 24, 30));
            UITheme.DrawGlow(spriteBatch, new Vector2(640, 760), 700f, new Color(255, 120, 60) * (0.12f + 0.03f * UITheme.PulseSine(totalSeconds, 0.6f)));
            int day = _playerState.Day;
            string heading = DayInfo.IsFinalNight(day) ? "Prepare for the Last Night" : "Prepare for the Night";
            float headIn = Anim.Intro(_elapsed, 0f, 0.4f);
            UITheme.DrawTextWithShadow(spriteBatch, font, heading, new Vector2(40, 22 - (1f - headIn) * 14f), Color.White * headIn);
            float headingWidth = UITheme.MeasureString(font, heading).X;
            string dayLine = DayInfo.IsFinalNight(day)
                ? $"{DayInfo.Label(day)}  -  every road leads to the Castle tonight"
                : $"{DayInfo.Label(day)}  -  the dark grows bolder each night";
            UITheme.DrawTextWithShadow(spriteBatch, font, dayLine, new Vector2(40 + headingWidth + 24, 27), (DayInfo.IsFinalNight(day) ? new Color(255, 190, 110) : new Color(190, 180, 205)) * Anim.Intro(_elapsed, 0.15f, 0.4f), 0.75f);

            DrawMap(spriteBatch, font, totalSeconds);

            float loadoutIn = Anim.Intro(_elapsed, 0.05f, 0.4f);
            UITheme.DrawFrame(spriteBatch, Anim.Slide(LoadoutPanel, loadoutIn, new Vector2(30, 0)), UIFrame.Wood, Color.White * (0.15f * loadoutIn), loadoutIn, shadow: 0.6f);
            DrawWeapons(spriteBatch, font);
            DrawBelt(spriteBatch, font);
            DrawDestination(spriteBatch, font);
            DrawStartButton(spriteBatch, font);

            DrawLandmarkTooltip(spriteBatch, font);

            spriteBatch.End();
        }

        private void DrawMap(SpriteBatch spriteBatch, SpriteFont font, float totalSeconds)
        {
            UITheme.DrawFrame(spriteBatch, MapPanel, UIFrame.Rect, new Color(150, 120, 70) * 0.7f, shadow: 0.6f);
            DrawScenery(spriteBatch, totalSeconds);

            var previous = ShelterPoint;
            int selectedIndex = Array.IndexOf(DistrictInfo.All, _playerState.SelectedDistrict);
            for (int i = 0; i < LandmarkPoints.Length; i++)
            {
                float t = MathHelper.Clamp((_elapsed - RoadAt - i * RoadStep) / RoadStep, 0f, 1f);
                if (t <= 0f) break;
                bool onRoute = i <= selectedIndex;
                bool reachable = onRoute || _playerState.IsDistrictUnlocked(DistrictInfo.All[i]);
                Color roadColor = !reachable ? Color.White * 0.15f
                    : onRoute ? new Color(255, 205, 140) * 0.9f
                    : new Color(225, 195, 140) * 0.55f;
                DrawDashedLine(spriteBatch, previous, Vector2.Lerp(previous, LandmarkPoints[i], t), roadColor, 3f, onRoute && reachable ? totalSeconds * 14f : 0f);
                previous = LandmarkPoints[i];
            }

            DrawShelter(spriteBatch, font);

            for (int i = 0; i < _landmarkButtons.Count; i++)
            {
                DrawLandmark(spriteBatch, font, i, totalSeconds);
            }

            UITheme.DrawTextWithShadow(spriteBatch, font, "Scavenging map", new Vector2(MapPanel.X + 18, MapPanel.Y + 14), new Color(225, 205, 165));
            UITheme.DrawTextWithShadow(spriteBatch, font, "Hover a landmark to scout it, click to choose.", new Vector2(MapPanel.X + 18, MapPanel.Y + MapPanel.Height - 30), new Color(190, 180, 165), 0.65f);

            var compass = new Vector2(MapPanel.X + MapPanel.Width - 40, MapPanel.Y + MapPanel.Height - 70);
            spriteBatch.DrawLine(compass + new Vector2(0, -18), compass + new Vector2(0, 18), new Color(225, 205, 165) * 0.6f, 2f);
            spriteBatch.DrawLine(compass + new Vector2(-12, 0), compass + new Vector2(12, 0), new Color(225, 205, 165) * 0.4f, 2f);
            UITheme.DrawTextWithShadow(spriteBatch, font, "N", compass + new Vector2(-6, -44), new Color(225, 205, 165), 0.75f);
        }

        private void DrawScenery(SpriteBatch spriteBatch, float totalSeconds)
        {
            for (float x = MapPanel.X + 60; x < MapPanel.X + MapPanel.Width; x += 60)
            {
                spriteBatch.DrawLine(new Vector2(x, MapPanel.Y + 4), new Vector2(x, MapPanel.Y + MapPanel.Height - 4), Color.White * 0.03f, 1f);
            }
            for (float y = MapPanel.Y + 60; y < MapPanel.Y + MapPanel.Height; y += 60)
            {
                spriteBatch.DrawLine(new Vector2(MapPanel.X + 4, y), new Vector2(MapPanel.X + MapPanel.Width - 4, y), Color.White * 0.03f, 1f);
            }

            for (int i = 0; i < RiverPoints.Length - 1; i++)
            {
                spriteBatch.DrawLine(RiverPoints[i], RiverPoints[i + 1], new Color(26, 44, 66), 12f);
            }
            for (int i = 1; i < RiverPoints.Length - 1; i++)
            {
                FillCircle(spriteBatch, RiverPoints[i], 6f, new Color(26, 44, 66));
            }
            for (int i = 0; i < RiverPoints.Length - 1; i++)
            {
                spriteBatch.DrawLine(RiverPoints[i], RiverPoints[i + 1], new Color(70, 110, 150) * 0.35f, 4f);
            }
            for (int g = 0; g < 6; g++)
            {
                float along = ((totalSeconds * 0.05f + g / 6f) % 1f) * (RiverPoints.Length - 1);
                int segment = (int)along;
                var glint = Vector2.Lerp(RiverPoints[segment], RiverPoints[segment + 1], along - segment);
                UITheme.DrawGlow(spriteBatch, glint, 10f, new Color(150, 200, 255) * 0.35f);
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
            var house = new RectangleF(ShelterPoint.X - 18, ShelterPoint.Y - 14, 36, 28);
            UITheme.DrawFrame(spriteBatch, house, UIFrame.Wood, new Color(235, 200, 140), shadow: 0.5f);
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

            float arrive = Anim.Pop(_elapsed, RoadAt + (index + 0.7f) * RoadStep, 0.4f);
            if (arrive <= 0.001f) return;
            float radius = LandmarkRadius * arrive * (1f + hover * 0.12f - button.PressAmount * 0.1f);
            if (radius < 1f) return;

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

            Color iconTint = unlocked ? Color.White : new Color(130, 125, 135);
            var material = MaterialIcon(DistrictInfo.SpecialtyMaterial(district));
            var landmark = Game1.GetDistrictIcon(district);
            if (landmark != null)
            {
                UITheme.DrawPixelIconFit(spriteBatch, landmark, center, radius * 1.45f, iconTint);
                var badge = center + new Vector2(radius * 0.74f, radius * 0.74f);
                FillCircle(spriteBatch, badge, 11f, Color.Black * 0.6f);
                FillCircle(spriteBatch, badge, 10f, UITheme.Darken(fill, 0.35f));
                DrawMaterialIcon(spriteBatch, material, badge, 17f, iconTint);
            }
            else
            {
                DrawMaterialIcon(spriteBatch, material, center, radius * 1.45f, iconTint);
            }

            if (selected)
            {
                DrawCenteredText(spriteBatch, font, "TONIGHT", new Vector2(center.X, center.Y - radius - 30), new Color(255, 215, 150), 0.65f);
            }

            string name = DistrictInfo.Name(district);
            var nameSize = UITheme.MeasureString(font, name) * 0.8f;
            var namePos = new Vector2(center.X, center.Y + radius + 8);
            UITheme.FillRoundedRect(spriteBatch, new RectangleF(namePos.X - nameSize.X / 2f - 6, namePos.Y - 2, nameSize.X + 12, nameSize.Y + 4), Color.Black * 0.35f, 6f);

            Color nameColor = unlocked ? Color.White : new Color(140, 135, 145);
            DrawCenteredText(spriteBatch, font, name, namePos, nameColor * MathHelper.Clamp(arrive, 0f, 1f), 0.8f);

            if (unlocked)
            {
                int tier = DistrictInfo.Corruption(district);
                const int maxTier = DistrictInfo.MaxCorruption;
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
                DrawCenteredText(spriteBatch, font, LockLabel(district), new Vector2(center.X, center.Y + radius + 30), new Color(160, 150, 160), 0.6f);
            }
        }

        private void DrawLandmarkTooltip(SpriteBatch spriteBatch, SpriteFont font)
        {
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
                UITheme.DrawFrame(spriteBatch, panel, UIFrame.Rect, UITheme.Brighten(accent, 0.3f) * alpha, alpha, shadow: 0.8f);
            }

            void Text(string text, float lineX, Color color, float scale)
            {
                if (draw) UITheme.DrawTextWithShadow(spriteBatch, font, text, new Vector2(lineX, y), color * alpha, scale);
            }

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

            Text(DistrictInfo.Name(district), left, Color.White, 1f);
            string corruption = $"Corruption {DistrictInfo.Corruption(district)}";
            float corruptionWidth = UITheme.MeasureString(font, corruption).X * 0.7f;
            Text(corruption, left + innerWidth - corruptionWidth, new Color(255, 180, 120), 0.7f);
            y += 28f;

            Wrapped(DistrictInfo.Tagline(district), new Color(210, 205, 215), 0.7f);
            Divider();

            if (district == District.Castle)
            {
                Wrapped("No rooms to search tonight - one hall, and the throne at the end of it.", new Color(225, 205, 165), 0.66f);
                y += 4f;
                Wrapped($"The Sun Herald: {EnemyRoster.SunHeraldHealth} health.", new Color(255, 225, 150), 0.66f);
                Wrapped("- Gathers light, then a SOLAR FLARE: Guard won't help. Smoke, or heal through it.", new Color(235, 200, 160), 0.62f);
                Wrapped("- BRAND: burns for 3 turns. Guard blocks it; a healing remedy puts it out.", new Color(235, 200, 160), 0.62f);
                Wrapped("- Calls his choir to fight beside him.", new Color(235, 200, 160), 0.62f);
                Wrapped("- At half health he ascends and hits harder.", new Color(235, 200, 160), 0.62f);
                Wrapped("Holy steel gets no bonus against him. The Dawnbreaker does.", new Color(200, 190, 210), 0.62f);
                Divider();
                Wrapped("Win, and the seventh dawn is yours. Fall, and the run ends. There is no running.", new Color(255, 170, 150), 0.66f);
                Divider();
                if (!unlocked) Wrapped(LockReason(district), new Color(235, 130, 115), 0.7f);
                else Wrapped("Tonight's destination", new Color(255, 215, 150), 0.7f);
                return y + TooltipPadding - topLeft.Y;
            }

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
                float amountWidth = UITheme.MeasureString(font, amount).X * 0.85f;
                Text(amount, left + innerWidth - amountWidth, color, 0.85f);
                y = textY + rowHeight;
            }

            y += 2f;
            Wrapped("Thorough searches roll twice. The Hoard rolls three times.", new Color(175, 175, 185), 0.65f);
            Divider();

            var (supplies, encounter, special, empty) = RoomGenerator.Odds(district, 0, day: _playerState.Day);
            Wrapped($"Near the entrance: {Percent(supplies)} supplies, {Percent(encounter)} fights, {Percent(special)} strange, {Percent(empty)} quiet", new Color(210, 210, 220), 0.62f);

            Wrapped($"Strange rooms: choices that trade time and risk for {DistrictInfo.EventTheme(district)}.", new Color(200, 185, 230), 0.62f);

            int day = _playerState.Day;
            int enemyHealth = (int)MathF.Round(DistrictInfo.EnemyHealth(district, 0) * DayInfo.EnemyHealthMultiplier(day));
            int enemyAttack = DistrictInfo.EnemyAttack(district, 0) + DayInfo.EnemyAttackBonus(day);
            Wrapped($"Wretches tonight: {enemyHealth}+ health, hit for {enemyAttack}+ and wind up heavy blows. Winning drops Scraps.", new Color(235, 150, 140), 0.62f);
            int penitents = EnemyRoster.PenitentChance(district);
            if (penitents > 0)
            {
                Wrapped($"Penitents ({penitents}% of fights): groups that cast holy fire and chant stuns.", new Color(235, 200, 140), 0.62f);
            }
            int knights = EnemyRoster.CastleKnightChance(district);
            if (knights > 0)
            {
                Wrapped($"Castle Knights ({knights}%): elites that hit hard and wind up often.", new Color(255, 200, 120), 0.62f);
            }
            Divider();

            if (!unlocked)
            {
                Wrapped(LockReason(district), new Color(235, 130, 115), 0.7f);
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

        private void DrawWeapons(SpriteBatch spriteBatch, SpriteFont font)
        {
            UITheme.DrawTextWithShadow(spriteBatch, font, "Choose your weapon", new Vector2(LoadoutPanel.X + 20, 78), Color.White * Anim.Intro(_elapsed, 0.1f, 0.4f));

            for (int i = 0; i < _weaponButtons.Count; i++)
            {
                var weapon = _playerState.Inventory[_weaponIndices[i]];
                var button = _weaponButtons[i];
                bool equipped = weapon == _playerState.EquippedWeapon;
                float hover = button.HoverAmount;
                float intro = Anim.Stagger(_elapsed, i, step: 0.06f, baseDelay: 0.15f, duration: 0.35f);
                if (intro <= 0.001f) continue;

                Color border = equipped ? Color.Gold : Color.Lerp(Color.White * 0.6f, Color.White, hover);

                float squash = button.PressAmount * 3f;
                var bounds = Anim.Slide(button.Bounds, intro, new Vector2(24, 0));
                var drawBounds = new RectangleF(bounds.X + squash, bounds.Y + squash / 2f - hover * 2f, bounds.Width - squash * 2f, bounds.Height - squash);

                if (equipped)
                {
                    float shine = UITheme.PulseSine(_elapsed, 1.5f);
                    UITheme.DrawGlow(spriteBatch, new Vector2(drawBounds.X + drawBounds.Width / 2f, drawBounds.Y + drawBounds.Height / 2f), drawBounds.Width * 0.6f, new Color(255, 200, 90) * ((0.12f + shine * 0.08f) * intro));
                }
                UITheme.DrawFrame(spriteBatch, drawBounds, UIFrame.Rect, border * intro, intro, shadow: 0.6f);

                float iconSize = 32 * IconScale;
                var iconPos = new Vector2(drawBounds.X + 10, drawBounds.Y + (drawBounds.Height - iconSize) / 2f);
                UITheme.DrawIconSlot(spriteBatch, Game1.GetWeaponIcon(weapon), iconPos, IconScale);

                float textX = drawBounds.X + 10 + iconSize + 12;
                UITheme.DrawTextWithShadow(spriteBatch, font, weapon.DisplayName, new Vector2(textX, drawBounds.Y + 8), Color.White * intro, 0.85f);

                string stats = weapon.IsHoly ? $"{weapon.DiceLabel}  +{weapon.CorruptionBonus}/corr" : $"{weapon.DiceLabel}  avg {weapon.AverageDamage:0.#}";
                UITheme.DrawTextWithShadow(spriteBatch, font, stats, new Vector2(textX, drawBounds.Y + 32), new Color(215, 215, 215) * intro, 0.75f);

                if (weapon.TraitLabel.Length > 0)
                {
                    UITheme.DrawTextWithShadow(spriteBatch, font, weapon.TraitLabel, new Vector2(textX, drawBounds.Y + drawBounds.Height - 22), TraitColor(weapon.Trait) * intro, 0.68f);
                }
                if (equipped)
                {
                    const string tag = "Equipped";
                    var tagSize = UITheme.MeasureString(font, tag) * 0.6f;
                    UITheme.DrawTextWithShadow(spriteBatch, font, tag, new Vector2(drawBounds.Right - tagSize.X - 10, drawBounds.Y + drawBounds.Height - 20), new Color(255, 235, 190) * intro, 0.6f);
                }
            }

            if (WeaponPageCount == 1 && _weaponButtons.Count <= WeaponsPerRow * 2)
            {
                int shown = -1;
                float bestHover = 0.5f;
                for (int i = 0; i < _weaponButtons.Count; i++)
                {
                    if (_weaponButtons[i].HoverAmount > bestHover) { bestHover = _weaponButtons[i].HoverAmount; shown = i; }
                }
                var described = shown >= 0 ? _playerState.Inventory[_weaponIndices[shown]] : _playerState.EquippedWeapon;
                if (described != null && described.TraitDescription.Length > 0)
                {
                    float y = 112 + ((_weaponButtons.Count + WeaponsPerRow - 1) / WeaponsPerRow) * (WeaponCardHeight + 10) + 6;
                    string text = $"{described.Name} - {described.TraitDescription}";
                    foreach (var line in TextLog.WrapText(font, text, (LoadoutPanel.Width - 40) / 0.64f))
                    {
                        UITheme.DrawTextWithShadow(spriteBatch, font, line, new Vector2(LoadoutPanel.X + 20, y), new Color(200, 195, 210) * Anim.Intro(_elapsed, 0.4f, 0.4f), 0.64f);
                        y += 18;
                    }
                }
            }

            if (WeaponPageCount > 1)
            {
                DrawSmallButton(spriteBatch, font, _prevPageButton);
                DrawSmallButton(spriteBatch, font, _nextPageButton);
                string page = $"Page {_weaponPage + 1}/{WeaponPageCount}  ({_playerState.Inventory.Count} weapons)";
                var size = UITheme.MeasureString(font, page) * 0.7f;
                UITheme.DrawTextWithShadow(spriteBatch, font, page, new Vector2(LoadoutPanel.X + LoadoutPanel.Width / 2f - size.X / 2f, 362), new Color(190, 185, 200), 0.7f);
            }
        }

        private void DrawDestination(SpriteBatch spriteBatch, SpriteFont font)
        {
            var district = _playerState.SelectedDistrict;
            float intro = Anim.Intro(_elapsed, 0.45f, 0.4f);
            if (intro <= 0.001f) return;
            var box = Anim.Slide(new RectangleF(LoadoutPanel.X + 20, 556, LoadoutPanel.Width - 40, 50), intro, new Vector2(0, 16));
            float pulse = UITheme.PulseSine(_elapsed, 1.2f);
            UITheme.DrawFrame(spriteBatch, box, UIFrame.Rect, new Color(255, 170, 90) * ((0.45f + pulse * 0.3f) * intro), intro, shadow: 0.4f);

            string specialty = DistrictInfo.SpecialtyMaterial(district);
            var landmark = Game1.GetDistrictIcon(district);
            if (landmark != null) UITheme.DrawPixelIconFit(spriteBatch, landmark, new Vector2(box.X + 26, box.Y + box.Height / 2f), 32f);
            else DrawMaterialIcon(spriteBatch, MaterialIcon(specialty), new Vector2(box.X + 26, box.Y + box.Height / 2f), 32f, Color.White);
            UITheme.DrawTextWithShadow(spriteBatch, font, $"Tonight: {DistrictInfo.Name(district)}", new Vector2(box.X + 50, box.Y + 5), Color.White, 0.85f);
            UITheme.DrawTextWithShadow(spriteBatch, font, $"Rich in {specialty}  -  Corruption {DistrictInfo.Corruption(district)}", new Vector2(box.X + 50, box.Y + 28), new Color(255, 200, 150), 0.62f);
        }

        private void DrawStartButton(SpriteBatch spriteBatch, SpriteFont font)
        {
            DrawBackButton(spriteBatch, font);

            float intro = Anim.Intro(_elapsed, 0.55f, 0.4f);
            if (intro <= 0.001f) return;
            float hover = _startButton.HoverAmount;
            Color border = Color.Lerp(new Color(120, 220, 135), new Color(200, 255, 210), hover);

            float squash = _startButton.PressAmount * 4f;
            var bounds = Anim.Slide(_startButton.Bounds, intro, new Vector2(0, 20));
            var drawBounds = new RectangleF(bounds.X + squash, bounds.Y + squash / 2f - hover * 2f, bounds.Width - squash * 2f, bounds.Height - squash);

            float breathe = UITheme.PulseSine(_elapsed, 1.6f);
            UITheme.DrawGlow(spriteBatch, new Vector2(drawBounds.X + drawBounds.Width / 2f, drawBounds.Y + drawBounds.Height / 2f), drawBounds.Width * 0.6f, new Color(120, 230, 140) * ((0.1f + breathe * 0.08f + hover * 0.1f) * intro));
            UITheme.DrawFrame(spriteBatch, drawBounds, UIFrame.Pill, border * intro, intro, shadow: 0.7f);
            var textSize = UITheme.MeasureString(font, _startButton.Label);
            var textPos = new Vector2(drawBounds.X + (drawBounds.Width - textSize.X) / 2f, drawBounds.Y + (drawBounds.Height - textSize.Y) / 2f);
            UITheme.DrawTextWithShadow(spriteBatch, font, _startButton.Label, textPos, Color.White * intro);
        }

        private void DrawBackButton(SpriteBatch spriteBatch, SpriteFont font)
        {
            float intro = Anim.Intro(_elapsed, 0.5f, 0.4f);
            if (intro <= 0.001f) return;
            float hover = _backButton.HoverAmount;
            Color border = Color.Lerp(Color.White * 0.6f, Color.White, hover);

            float squash = _backButton.PressAmount * 4f;
            var bounds = Anim.Slide(_backButton.Bounds, intro, new Vector2(0, 20));
            var drawBounds = new RectangleF(bounds.X + squash, bounds.Y + squash / 2f - hover * 2f, bounds.Width - squash * 2f, bounds.Height - squash);

            UITheme.DrawFrame(spriteBatch, drawBounds, UIFrame.Pill, border * intro, intro, shadow: 0.6f);
            var textSize = UITheme.MeasureString(font, _backButton.Label) * 0.9f;
            var textPos = new Vector2(drawBounds.X + (drawBounds.Width - textSize.X) / 2f, drawBounds.Y + (drawBounds.Height - textSize.Y) / 2f);
            UITheme.DrawTextWithShadow(spriteBatch, font, _backButton.Label, textPos, Color.White * intro, 0.9f);
        }

        private static Color LandmarkColor(District district) => district switch
        {
            District.VillageOutskirts => new Color(104, 132, 64),
            District.ChurchRuins => new Color(112, 78, 150),
            District.CastleKeep => new Color(150, 56, 48),
            District.Castle => new Color(176, 140, 60),
            _ => new Color(80, 80, 90)
        };

        private static Color TraitColor(WeaponTrait trait) => trait switch
        {
            WeaponTrait.Bleed => new Color(255, 140, 130),
            WeaponTrait.Stagger => new Color(170, 200, 255),
            WeaponTrait.Lifesteal => new Color(150, 230, 170),
            WeaponTrait.Sunbane => new Color(255, 220, 140),
            _ => Color.White
        };

        private string LockLabel(District district) =>
            district == District.Castle ? "Opens on the last night"
            : DayInfo.IsFinalNight(_playerState.Day) ? "Sealed tonight"
            : $"Locked - Archive Lv {DistrictInfo.RequiredArchiveLevel(district)}";

        private string LockReason(District district) =>
            district == District.Castle ? $"Opens only on the last night ({DayInfo.Label(DayInfo.FinalDay)}) - and then it's the only way in."
            : DayInfo.IsFinalNight(_playerState.Day) ? "Sealed tonight - the last night belongs to the Castle."
            : $"Locked - upgrade the Archive to Lv {DistrictInfo.RequiredArchiveLevel(district)}";

        private Texture2D MaterialIcon(string material) => material switch
        {
            "Food" => Game1.BreadTexture,
            "Planks" => Game1.PlanksTexture,
            "Scraps" => Game1.ScrapsTexture,
            _ => null
        };

        private static void DrawMaterialIcon(SpriteBatch spriteBatch, Texture2D texture, Vector2 center, float size, Color tint)
        {
            if (texture == null) return;
            float scale = size / Math.Max(texture.Width, texture.Height);
            var origin = new Vector2(texture.Width / 2f, texture.Height / 2f);
            spriteBatch.Draw(texture, center, null, tint, 0f, origin, scale, SpriteEffects.None, 0f);
        }

        private static void FillCircle(SpriteBatch spriteBatch, Vector2 center, float radius, Color color)
        {
            UITheme.FillRoundedRect(spriteBatch, new RectangleF(center.X - radius, center.Y - radius, radius * 2f, radius * 2f), color, radius);
        }

        private static void DrawDashedLine(SpriteBatch spriteBatch, Vector2 from, Vector2 to, Color color, float thickness, float phase = 0f)
        {
            const float dash = 10f, gap = 8f;
            float length = Vector2.Distance(from, to);
            if (length < 1f) return;
            var direction = (to - from) / length;
            for (float d = phase % (dash + gap) - (dash + gap); d < length; d += dash + gap)
            {
                float start = Math.Max(d, 0f);
                float end = Math.Min(d + dash, length);
                if (end > start) spriteBatch.DrawLine(from + direction * start, from + direction * end, color, thickness);
            }
        }

        private static void DrawCenteredText(SpriteBatch spriteBatch, SpriteFont font, string text, Vector2 topCenter, Color color, float scale)
        {
            float width = UITheme.MeasureString(font, text).X * scale;
            UITheme.DrawTextWithShadow(spriteBatch, font, text, new Vector2(topCenter.X - width / 2f, topCenter.Y), color, scale);
        }
    }
}
