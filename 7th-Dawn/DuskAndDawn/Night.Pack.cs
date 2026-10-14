using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using System.Collections.Generic;
using System.Linq;

namespace DuskAndDawn
{
    public partial class NightScavengingScreen
    {
        private Button _packButton;
        private bool _packOpen;
        private readonly List<(Button button, string itemName)> _packRows = new List<(Button, string)>();

        private static readonly RectangleF PackButtonBounds = new RectangleF(1000, 94, 240, 44);
        private const float PackPanelX = 820f, PackPanelWidth = 420f, PackTop = 148f;
        private const float PackRowHeight = 56f, PackRowGap = 6f;

        private const int PackUseMinutes = 15;

        private void InitializePack()
        {
            _packButton = new Button(PackButtonBounds, "Pack");
        }

        private float PackPanelHeight => 58f + _packRows.Count * (PackRowHeight + PackRowGap) + (_packRows.Count == 0 ? 30f : 0f) + 30f;

        private void OpenPack()
        {
            _packOpen = true;
            RebuildPackRows();
        }

        private void RebuildPackRows()
        {
            _packRows.Clear();
            float y = PackTop + 52f;
            foreach (var group in _playerState.Belt.GroupBy(item => item.Name))
            {
                int count = group.Count();
                string label = count > 1 ? $"{group.Key} x{count}" : group.Key;
                var bounds = new RectangleF(PackPanelX + 14, y, PackPanelWidth - 28, PackRowHeight);
                _packRows.Add((new Button(bounds, label), group.Key));
                y += PackRowHeight + PackRowGap;
            }
        }

        private void UpdatePack(float dt, MouseState mouse, bool mapInteractive)
        {
            _packButton.Label = _packOpen ? "Close Pack" : $"Pack ({_playerState.Belt.Count})";
            _packButton.UpdateAnimation(dt, mapInteractive && _packButton.Contains(mouse.X, mouse.Y));
            foreach (var (button, itemName) in _packRows)
            {
                bool usable = UsableNow(itemName, out _);
                button.UpdateAnimation(dt, mapInteractive && _packOpen && usable && button.Contains(mouse.X, mouse.Y));
            }
        }

        private bool HandlePackClick(int x, int y)
        {
            if (_packButton.Contains(x, y))
            {
                _packButton.TriggerPress();
                if (_packOpen) _packOpen = false;
                else OpenPack();
                return true;
            }

            if (!_packOpen) return false;

            foreach (var (button, itemName) in _packRows)
            {
                if (!button.Contains(x, y)) continue;
                button.TriggerPress();
                UsePackItem(itemName);
                return true;
            }

            var panel = new RectangleF(PackPanelX, PackTop, PackPanelWidth, PackPanelHeight);
            if (!InputChecker.Contains(panel, x, y)) _packOpen = false;
            return true;
        }

        private bool UsableNow(string itemName, out string reason)
        {
            var item = _playerState.Belt.Find(it => it.Name == itemName);
            reason = item == null ? "Used up"
                : item.WastedReason(_playerState, _dawnTimer)
                  ?? (!_dawnTimer.CanAfford(PackUseMinutes) ? "Dawn would break first" : null);
            return reason == null;
        }

        private void UsePackItem(string itemName)
        {
            if (!UsableNow(itemName, out string reason))
            {
                _textLog.Push($"{itemName}: {reason.ToLowerInvariant()}.");
                return;
            }

            var item = _playerState.Belt.Find(it => it.Name == itemName);
            string log = item.ApplyRemedy(_playerState, _dawnTimer);
            _playerState.Belt.Remove(item);
            _dawnTimer.Spend(PackUseMinutes);
            _textLog.Push($"You stop to gather yourself ({DawnTimer.FormatDuration(PackUseMinutes)}). {log}");
            RebuildPackRows();
            WarnOfDawn();

            if (IsNightOver)
            {
                _packOpen = false;
                GoToDawnReturn();
            }
        }

        private void DrawPack(SpriteBatch spriteBatch, SpriteFont font)
        {
            DrawStyledButton(spriteBatch, font, _packButton, Palette.NightBorder, Palette.NightPanel, accent: Palette.Gain);
            if (!_packOpen) return;

            var panel = new RectangleF(PackPanelX, PackTop, PackPanelWidth, PackPanelHeight);
            UITheme.DrawFrame(spriteBatch, panel, UIFrame.Rect, Palette.NightBorder, shadow: 0.9f, surface: Palette.NightPanel);
            UITheme.DrawTextWithShadow(spriteBatch, font, "Pack", new Vector2(panel.X + 16, panel.Y + 12), Color.White);
            UITheme.DrawTextWithShadow(spriteBatch, font, $"Patch yourself up between rooms. Each use takes {DawnTimer.FormatDuration(PackUseMinutes)}.", new Vector2(panel.X + 16, panel.Y + 34), new Color(175, 170, 195), 0.62f);

            if (_packRows.Count == 0)
            {
                UITheme.DrawTextWithShadow(spriteBatch, font, "Your belt is empty - pack remedies on the Prepare screen.", new Vector2(panel.X + 16, panel.Y + 60), new Color(200, 190, 190), 0.72f);
            }

            foreach (var (button, itemName) in _packRows)
            {
                var item = _playerState.Belt.Find(it => it.Name == itemName);
                bool usable = UsableNow(itemName, out string reason);
                float hover = button.HoverAmount;
                var b = button.Bounds;
                float squash = button.PressAmount * 3f;
                var draw = new RectangleF(b.X + squash, b.Y + squash / 2f, b.Width - squash * 2f, b.Height - squash);

                Color border = usable ? Color.Lerp(Palette.NightBorder, Palette.Gain, hover) : Palette.NightBorderSoft;
                UITheme.DrawFrame(spriteBatch, draw, UIFrame.Rect, border, shadow: 0.4f, surface: usable ? Palette.NightRaised : Palette.NightDeep);

                float textX = draw.X + 12;
                var icon = Game1.GetItemIcon(itemName);
                if (icon != null)
                {
                    UITheme.DrawIconSlot(spriteBatch, icon, new Vector2(draw.X + 12, draw.Y + (draw.Height - 32) / 2f), 1, tint: usable ? Color.White : Color.White * 0.45f);
                    textX = draw.X + 54;
                }
                UITheme.DrawTextWithShadow(spriteBatch, font, button.Label, new Vector2(textX, draw.Y + 6), usable ? Color.White : new Color(150, 145, 155), 0.9f);
                if (item != null)
                {
                    UITheme.DrawTextWithShadow(spriteBatch, font, item.ShortEffect(_playerState), new Vector2(textX, draw.Y + 32), usable ? new Color(170, 230, 180) : new Color(120, 130, 125), 0.66f);
                }

                string right = usable ? $"Use  ({DawnTimer.FormatDuration(PackUseMinutes)})" : reason;
                var rightSize = UITheme.MeasureString(font, right) * 0.7f;
                UITheme.DrawTextWithShadow(spriteBatch, font, right, new Vector2(draw.Right - rightSize.X - 12, draw.Y + 8), usable ? new Color(255, 205, 140) : new Color(200, 130, 120), 0.7f);
            }

            UITheme.DrawTextWithShadow(spriteBatch, font, $"Health {_playerState.Health}/{_playerState.MaxHealth}", new Vector2(panel.X + 16, panel.Bottom - 26), new Color(210, 200, 205), 0.72f);
        }
    }
}
