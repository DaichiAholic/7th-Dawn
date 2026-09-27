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
    /// Marks the screens that make up a run (day, preparation, night, dawn). Only these can
    /// be paused, and a run counts as "in progress" while one is on the screen stack.
    /// </summary>
    public interface IGameplayScreen { }

    /// <summary>
    /// Shared behaviour for the main, pause and settings menus: a vertical stack of buttons
    /// driven by mouse or keyboard (Up/Down/Enter, Esc for back).
    ///
    /// Buttons fire on mouse *release*, not press. When a menu closes, the screen underneath
    /// resumes with the mouse-state it had before the menu opened, so a button still held
    /// down would read there as a fresh click on whatever sits under the cursor.
    /// </summary>
    public abstract class MenuScreen : GameScreen
    {
        protected Game1 Game1 => (Game1)Game;

        protected class MenuEntry
        {
            public Button Button;
            public Func<string> Label;
            public Action OnSelect;

            // Destructive entries (abandon run, quit) ask for a second click while this is true.
            public Func<bool> NeedsConfirm;
            public string ConfirmLabel;
        }

        protected readonly List<MenuEntry> Entries = new List<MenuEntry>();
        protected float Elapsed { get; private set; }

        private const float EntryWidth = 380f;
        private const float EntryHeight = 56f;
        private const float EntryGap = 14f;
        private const float ConfirmWindow = 3f;

        private readonly bool _isOverlay;
        private Screen _beneath;
        private bool _beneathDrewWhenInactive;

        private MouseState _previousMouse;
        private KeyboardState _previousKeyboard;
        private MenuEntry _pressedEntry;
        private int _focusIndex = -1;
        private MenuEntry _armedEntry;
        private float _armedTimer;

        /// <param name="isOverlay">Overlays keep the screen beneath them drawn (dimmed) behind
        /// their panel, like the pause menu over the night map.</param>
        protected MenuScreen(Game game, bool isOverlay) : base(game)
        {
            _isOverlay = isOverlay;
        }

        /// <summary>Top edge of the button column.</summary>
        protected abstract float EntriesTop { get; }

        /// <summary>Called once from Initialize - add entries with AddEntry.</summary>
        protected abstract void BuildEntries();

        /// <summary>Esc. Default does nothing.</summary>
        protected virtual void OnBack() { }

        protected abstract void DrawContent(SpriteBatch spriteBatch, SpriteFont font);

        protected void AddEntry(string label, Action onSelect, Func<bool> needsConfirm = null, string confirmLabel = null)
        {
            AddEntry(() => label, onSelect, needsConfirm, confirmLabel);
        }

        protected void AddEntry(Func<string> label, Action onSelect, Func<bool> needsConfirm = null, string confirmLabel = null)
        {
            float y = EntriesTop + Entries.Count * (EntryHeight + EntryGap);
            var bounds = new RectangleF(640 - EntryWidth / 2f, y, EntryWidth, EntryHeight);
            Entries.Add(new MenuEntry
            {
                Button = new Button(bounds, label()),
                Label = label,
                OnSelect = onSelect,
                NeedsConfirm = needsConfirm,
                ConfirmLabel = confirmLabel
            });
        }

        /// <summary>Bottom edge of the button column, for laying out anything below it.</summary>
        protected float EntriesBottom => EntriesTop + Entries.Count * (EntryHeight + EntryGap) - EntryGap;

        public override void Initialize()
        {
            base.Initialize();

            // Not pushed yet at this point, so ActiveScreen is still the screen underneath.
            if (_isOverlay && ScreenManager.ActiveScreen != null)
            {
                _beneath = ScreenManager.ActiveScreen;
                _beneathDrewWhenInactive = _beneath.DrawWhenInactive;
                _beneath.DrawWhenInactive = true;
            }

            _previousMouse = InputChecker.GetMouse();
            _previousKeyboard = Keyboard.GetState();
            BuildEntries();
        }

        public override void Dispose()
        {
            if (_beneath != null)
            {
                _beneath.DrawWhenInactive = _beneathDrewWhenInactive;
                _beneath = null;
            }
            base.Dispose();
        }

        public override void Update(GameTime gameTime)
        {
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            Elapsed += dt;

            if (_armedEntry != null)
            {
                _armedTimer -= dt;
                if (_armedTimer <= 0f) _armedEntry = null;
            }

            var mouse = InputChecker.GetMouse();
            var keyboard = Keyboard.GetState();
            bool mouseMoved = mouse.X != _previousMouse.X || mouse.Y != _previousMouse.Y;

            for (int i = 0; i < Entries.Count; i++)
            {
                var entry = Entries[i];
                bool hovered = entry.Button.Contains(mouse.X, mouse.Y);
                if (hovered && mouseMoved) _focusIndex = i;
                entry.Button.Label = entry == _armedEntry ? entry.ConfirmLabel : entry.Label();
                entry.Button.UpdateAnimation(dt, hovered || i == _focusIndex);
            }

            // Input waits while a fade plays: the fade is already taking us somewhere, and a
            // second screen change requested now would be refused or land in the wrong place.
            if (!ScreenTransitions.IsTransitioning)
            {
                HandleMouse(mouse);
                HandleKeyboard(keyboard);
            }

            _previousMouse = mouse;
            _previousKeyboard = keyboard;
        }

        private void HandleMouse(MouseState mouse)
        {
            if (InputChecker.IsNewLeftClick(mouse, _previousMouse))
            {
                _pressedEntry = null;
                foreach (var entry in Entries)
                {
                    if (!entry.Button.Contains(mouse.X, mouse.Y)) continue;
                    _pressedEntry = entry;
                    entry.Button.TriggerPress();
                    break;
                }
            }
            else if (mouse.LeftButton == ButtonState.Released && _previousMouse.LeftButton == ButtonState.Pressed)
            {
                var pressed = _pressedEntry;
                _pressedEntry = null;
                if (pressed != null && pressed.Button.Contains(mouse.X, mouse.Y))
                {
                    Select(pressed);
                }
            }
        }

        private void HandleKeyboard(KeyboardState keyboard)
        {
            bool Pressed(Keys key) => keyboard.IsKeyDown(key) && !_previousKeyboard.IsKeyDown(key);

            if (Pressed(Keys.Escape))
            {
                OnBack();
                return;
            }

            if (Entries.Count == 0) return;

            if (Pressed(Keys.Down) || Pressed(Keys.S))
            {
                _focusIndex = (_focusIndex + 1) % Entries.Count;
            }
            else if (Pressed(Keys.Up) || Pressed(Keys.W))
            {
                _focusIndex = _focusIndex <= 0 ? Entries.Count - 1 : _focusIndex - 1;
            }
            // Alt+Enter is the fullscreen toggle (see Game1), not a menu selection.
            bool alt = keyboard.IsKeyDown(Keys.LeftAlt) || keyboard.IsKeyDown(Keys.RightAlt);
            if (((Pressed(Keys.Enter) && !alt) || Pressed(Keys.Space)) && _focusIndex >= 0)
            {
                Entries[_focusIndex].Button.TriggerPress();
                Select(Entries[_focusIndex]);
            }
        }

        private void Select(MenuEntry entry)
        {
            if (entry.NeedsConfirm != null && entry.NeedsConfirm() && _armedEntry != entry)
            {
                _armedEntry = entry;
                _armedTimer = ConfirmWindow;
                return;
            }

            _armedEntry = null;
            entry.OnSelect();
        }

        public override void Draw(GameTime gameTime)
        {
            var spriteBatch = Game1.SpriteBatch;
            var font = Game1.Font;

            // Full-screen menus clear; overlays draw on top of the (inactive) screen beneath.
            if (!_isOverlay) GraphicsDevice.Clear(Color.Black);

            UITheme.BeginCanvas(spriteBatch);
            DrawContent(spriteBatch, font);
            foreach (var entry in Entries)
            {
                DrawEntry(spriteBatch, font, entry);
            }
            spriteBatch.End();
        }

        private void DrawEntry(SpriteBatch spriteBatch, SpriteFont font, MenuEntry entry)
        {
            var button = entry.Button;
            float hover = button.HoverAmount;
            bool armed = entry == _armedEntry;

            // Same ember language as the base's Prepare button; armed (confirm) entries go red.
            Color top = armed ? new Color(120, 40, 36) : Color.Lerp(new Color(58, 46, 42), new Color(84, 64, 52), hover);
            Color bottom = armed ? new Color(80, 24, 22) : Color.Lerp(new Color(36, 28, 26), new Color(54, 40, 34), hover);
            Color border = Color.Lerp(new Color(170, 80, 45), new Color(255, 140, 70), armed ? 1f : hover);
            float borderThickness = MathHelper.Lerp(2f, 3.5f, hover);

            float squash = button.PressAmount * 4f;
            var bounds = button.Bounds;
            var drawBounds = new RectangleF(bounds.X + squash, bounds.Y + squash / 2f, bounds.Width - squash * 2f, bounds.Height - squash);

            UITheme.DrawPanel(spriteBatch, drawBounds, top, bottom, border, borderThickness, 12f, shadowStrength: 0.7f);

            var textSize = UITheme.MeasureString(font, button.Label);
            var textPos = new Vector2(
                drawBounds.X + (drawBounds.Width - textSize.X) / 2f,
                drawBounds.Y + (drawBounds.Height - textSize.Y) / 2f);
            UITheme.DrawTextWithShadow(spriteBatch, font, button.Label, textPos, Color.White);
        }

        // ---------- Shared drawing helpers ----------

        protected static void DrawCenteredText(SpriteBatch spriteBatch, SpriteFont font, string text, float y, Color color, float scale = 1f)
        {
            var size = UITheme.MeasureString(font, text) * scale;
            UITheme.DrawTextWithShadow(spriteBatch, font, text, new Vector2(640 - size.X / 2f, y), color, scale);
        }

        /// <summary>Dims whatever is underneath and draws the central panel overlays sit on.</summary>
        protected static void DrawOverlayPanel(SpriteBatch spriteBatch, RectangleF panel)
        {
            UITheme.FillGradientRect(spriteBatch, new RectangleF(0, 0, 1280, 720), Color.Black * 0.6f, Color.Black * 0.6f, 1);
            UITheme.DrawPanel(spriteBatch, panel, new Color(62, 54, 58), new Color(34, 30, 34), new Color(200, 100, 55), 3f, 18f, shadowStrength: 0.9f);
        }
    }

    /// <summary>The title screen: Continue (when a run is in progress), New Game, Settings, Exit.</summary>
    public class MainMenuScreen : MenuScreen
    {
        public MainMenuScreen(Game game) : base(game, isOverlay: false) { }

        protected override float EntriesTop => 330f;

        protected override void BuildEntries()
        {
            int? savedDay = SaveGame.SavedDay;
            if (Game1.HasRunInProgress)
            {
                // The paused run sits right under this menu on the screen stack.
                AddEntry("Continue", () => ScreenManager.CloseScreen(ScreenTransitions.FadeTransition(GraphicsDevice)));
            }
            else if (savedDay.HasValue)
            {
                AddEntry($"Continue (Day {savedDay.Value})", Game1.ContinueSavedRun);
            }

            AddEntry("New Game", Game1.StartNewRun,
                needsConfirm: () => Game1.HasRunInProgress || SaveGame.Exists, confirmLabel: "Abandon your run?");
            AddEntry("Settings", () => ScreenManager.ShowScreen(new SettingsScreen(Game)));
            // Runs save at the base each morning, so quitting only loses the current night.
            AddEntry("Exit", Game.Exit,
                needsConfirm: () => Game1.HasRunInProgress, confirmLabel: "Quit to desktop?");
        }

        protected override void DrawContent(SpriteBatch spriteBatch, SpriteFont font)
        {
            UITheme.FillGradientRect(spriteBatch, new RectangleF(0, 0, 1280, 720), new Color(34, 18, 24), new Color(8, 6, 12), 12);

            // A low ember glow along the horizon - the dawn the title is named for.
            float glow = UITheme.PulseSine(Elapsed, 0.8f);
            UITheme.FillGradientRect(spriteBatch, new RectangleF(0, 520, 1280, 200), Color.Transparent, new Color(140, 55, 30) * (0.35f + glow * 0.15f), 10);

            float fadeIn = UITheme.EaseOutCubic(MathHelper.Clamp(Elapsed / 1.2f, 0f, 1f));
            DrawCenteredText(spriteBatch, font, "7th Dawn", 140, new Color(255, 205, 150) * fadeIn, 3f);
            DrawCenteredText(spriteBatch, font, "Scavenge by night. Rebuild by day. Hold on to Hope.", 250, new Color(200, 185, 185) * fadeIn, 0.9f);

            UITheme.DrawTextWithShadow(spriteBatch, font, "v1.4", new Vector2(24, 684), new Color(140, 130, 135), 0.75f);
            const string hint = "Esc in game opens the menu   F11 fullscreen";
            var hintSize = UITheme.MeasureString(font, hint) * 0.75f;
            UITheme.DrawTextWithShadow(spriteBatch, font, hint, new Vector2(1256 - hintSize.X, 684), new Color(140, 130, 135), 0.75f);
        }
    }

    /// <summary>In-run pause overlay, opened with Esc or the Menu button.</summary>
    public class PauseMenuScreen : MenuScreen
    {
        private static readonly RectangleF Panel = new RectangleF(420, 150, 440, 430);

        public PauseMenuScreen(Game game) : base(game, isOverlay: true) { }

        protected override float EntriesTop => Panel.Y + 90;

        protected override void BuildEntries()
        {
            AddEntry("Resume", Resume);
            AddEntry("Settings", () => ScreenManager.ShowScreen(new SettingsScreen(Game)));
            // The run stays on the stack under the main menu, so Continue picks it back up.
            AddEntry("Main Menu", () => ScreenManager.ReplaceScreen(new MainMenuScreen(Game), ScreenTransitions.FadeTransition(GraphicsDevice)));
            AddEntry("Exit Game", Game.Exit, needsConfirm: () => true, confirmLabel: "Quit? (resume at the base)");
        }

        protected override void OnBack() => Resume();

        private void Resume() => ScreenManager.CloseScreen();

        protected override void DrawContent(SpriteBatch spriteBatch, SpriteFont font)
        {
            DrawOverlayPanel(spriteBatch, Panel);
            DrawCenteredText(spriteBatch, font, "Paused", Panel.Y + 28, Color.White, 1.4f);
        }
    }

    /// <summary>Options screen, usable from the main menu or the pause menu. Changes apply
    /// and save immediately.</summary>
    public class SettingsScreen : MenuScreen
    {
        private static readonly RectangleF Panel = new RectangleF(400, 130, 480, 470);

        public SettingsScreen(Game game) : base(game, isOverlay: true) { }

        protected override float EntriesTop => Panel.Y + 90;

        private static GameSettings Settings => GameSettings.Current;

        protected override void BuildEntries()
        {
            AddEntry(() => $"Fullscreen: {OnOff(Settings.Fullscreen)}", () =>
            {
                Settings.Fullscreen = !Settings.Fullscreen;
                Game1.ApplySettings();
                GameSettings.Save();
            });
            AddEntry(() => $"Screen Shake: {OnOff(Settings.ScreenShake)}", () =>
            {
                Settings.ScreenShake = !Settings.ScreenShake;
                GameSettings.Save();
            });
            AddEntry(() => $"Combat Speed: {(Settings.FastCombat ? "Fast" : "Normal")}", () =>
            {
                Settings.FastCombat = !Settings.FastCombat;
                GameSettings.Save();
            });
            AddEntry("Back", OnBack);
        }

        private static string OnOff(bool value) => value ? "On" : "Off";

        protected override void OnBack() => ScreenManager.CloseScreen();

        protected override void DrawContent(SpriteBatch spriteBatch, SpriteFont font)
        {
            DrawOverlayPanel(spriteBatch, Panel);
            DrawCenteredText(spriteBatch, font, "Settings", Panel.Y + 28, Color.White, 1.4f);
            DrawCenteredText(spriteBatch, font, "Changes are saved automatically.", EntriesBottom + 22, new Color(180, 170, 175), 0.75f);
        }
    }
}
