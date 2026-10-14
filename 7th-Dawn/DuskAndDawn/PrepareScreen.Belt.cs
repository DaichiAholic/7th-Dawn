using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using System.Collections.Generic;
using System.Linq;

namespace DuskAndDawn
{
    public partial class PreparationScreen
    {
        private readonly List<Button> _beltSlotButtons = new List<Button>();
        private readonly List<(Button button, string itemName)> _stashChips = new List<(Button, string)>();
        private string _beltMessage = "";
        private readonly float[] _slotPackedAt = { -1f, -1f, -1f, -1f, -1f, -1f };
        private float _beltMessageAt = -1f;

        private const float BeltTop = 388f;
        private const float SlotTop = BeltTop + 26f, SlotHeight = 52f, SlotGap = 8f;
        private const float StashTop = SlotTop + SlotHeight + 10f;

        private void InitializeBelt()
        {
            float slotWidth = (LoadoutPanel.Width - 40 - SlotGap * (PlayerState.MaxBeltSlots - 1)) / PlayerState.MaxBeltSlots;
            _beltSlotButtons.Clear();
            for (int i = 0; i < PlayerState.MaxBeltSlots; i++)
            {
                _beltSlotButtons.Add(new Button(new RectangleF(LoadoutPanel.X + 20 + i * (slotWidth + SlotGap), SlotTop, slotWidth, SlotHeight), ""));
            }
            LayoutStash();
        }

        private void LayoutStash()
        {
            _stashChips.Clear();
            var font = ((Game1)Game).Font;
            float x = LoadoutPanel.X + 20, y = StashTop + 22;
            float right = LoadoutPanel.Right - 20;
            foreach (var group in _playerState.Items.GroupBy(item => item.Name))
            {
                string label = group.Count() > 1 ? $"{group.Key} x{group.Count()}" : group.Key;
                float width = UITheme.MeasureString(font, label).X * 0.68f + 20;
                if (x + width > right)
                {
                    x = LoadoutPanel.X + 20;
                    y += 30;
                    if (y > StashTop + 22 + 30) break;
                }
                _stashChips.Add((new Button(new RectangleF(x, y, width, 26), label), group.Key));
                x += width + 6;
            }
        }

        private void UpdateBelt(float dt, MouseState mouse)
        {
            for (int i = 0; i < _beltSlotButtons.Count; i++)
            {
                bool filled = i < _playerState.Belt.Count;
                _beltSlotButtons[i].UpdateAnimation(dt, filled && _beltSlotButtons[i].Contains(mouse.X, mouse.Y));
            }
            foreach (var (button, _) in _stashChips)
            {
                button.UpdateAnimation(dt, !_playerState.BeltFull && button.Contains(mouse.X, mouse.Y));
            }
        }

        private void HandleBeltClick(int x, int y)
        {
            for (int i = 0; i < _playerState.Belt.Count && i < _beltSlotButtons.Count; i++)
            {
                if (!_beltSlotButtons[i].Contains(x, y)) continue;
                _beltSlotButtons[i].TriggerPress();
                var item = _playerState.Belt[i];
                _playerState.Belt.RemoveAt(i);
                _playerState.Items.Add(item);
                _beltMessage = $"{item.Name} back in the stash.";
                _beltMessageAt = _elapsed;
                LayoutStash();
                return;
            }

            foreach (var (button, itemName) in _stashChips)
            {
                if (!button.Contains(x, y)) continue;
                button.TriggerPress();
                if (_playerState.BeltFull)
                {
                    _beltMessage = "Belt is full - unpack something first.";
                    _beltMessageAt = _elapsed;
                    return;
                }
                var item = _playerState.Items.Find(it => it.Name == itemName);
                _playerState.Items.Remove(item);
                _playerState.Belt.Add(item);
                _beltMessage = $"Packed a {item.Name}.";
                _beltMessageAt = _elapsed;
                if (_playerState.Belt.Count - 1 < _slotPackedAt.Length) _slotPackedAt[_playerState.Belt.Count - 1] = _elapsed;
                LayoutStash();
                return;
            }
        }

        private void DrawBelt(SpriteBatch spriteBatch, SpriteFont font)
        {
            float sectionIn = Anim.Intro(_elapsed, 0.3f, 0.4f);
            if (sectionIn <= 0.001f) return;
            float x = LoadoutPanel.X + 20;
            UITheme.DrawTextWithShadow(spriteBatch, font, $"Belt  {_playerState.Belt.Count}/{_playerState.BeltSlots}", new Vector2(x, BeltTop), Color.White * sectionIn, 0.9f);

            string slotHint = _playerState.BeltSlots < PlayerState.MaxBeltSlots
                ? $"Storage Lv {_playerState.Level(BaseRoomType.Storage) + 1} adds a slot"
                : "Belt fully expanded";
            var hintSize = UITheme.MeasureString(font, slotHint) * 0.62f;
            UITheme.DrawTextWithShadow(spriteBatch, font, slotHint, new Vector2(LoadoutPanel.Right - 20 - hintSize.X, BeltTop + 4), new Color(190, 185, 200), 0.62f);

            for (int i = 0; i < _beltSlotButtons.Count; i++)
            {
                var button = _beltSlotButtons[i];
                bool unlocked = i < _playerState.BeltSlots;
                bool filled = i < _playerState.Belt.Count;
                float intro = Anim.Stagger(_elapsed, i, step: 0.05f, baseDelay: 0.35f, duration: 0.3f);
                if (intro <= 0.001f) continue;
                float pop = MathHelper.Lerp(0.8f, 1f, UITheme.EaseOutBack(intro));
                if (filled && _slotPackedAt[i] >= 0f) pop *= MathHelper.Lerp(0.85f, 1f, Anim.Pop(_elapsed, _slotPackedAt[i], 0.3f));
                var b = Anim.Scale(button.Bounds, pop);
                b = new RectangleF(b.X, b.Y - button.HoverAmount * 2f, b.Width, b.Height);

                if (!unlocked)
                {
                    UITheme.DrawFrameOutline(spriteBatch, b, UIFrame.Rect, Color.White * 0.12f);
                    continue;
                }

                float hover = button.HoverAmount;
                Color border = filled ? Color.Lerp(new Color(150, 200, 160), Color.White, hover) : Color.White * 0.3f;
                float flash = filled && _slotPackedAt[i] >= 0f ? Anim.Flash(_elapsed, _slotPackedAt[i], 0.5f) : 0f;
                if (flash > 0f)
                {
                    UITheme.DrawGlow(spriteBatch, new Vector2(b.X + b.Width / 2f, b.Y + b.Height / 2f), b.Width * 0.9f, new Color(150, 230, 160) * (0.35f * flash));
                }
                UITheme.DrawFrame(spriteBatch, b, UIFrame.Rect, border * intro, (filled ? 1f : 0.7f) * intro, shadow: 0.3f);

                if (!filled)
                {
                    const string empty = "empty";
                    var size = UITheme.MeasureString(font, empty) * 0.6f;
                    UITheme.DrawTextWithShadow(spriteBatch, font, empty, new Vector2(b.X + (b.Width - size.X) / 2f, b.Y + (b.Height - size.Y) / 2f), Color.White * 0.35f, 0.6f);
                    continue;
                }

                string itemName = _playerState.Belt[i].Name;
                var icon = Game1.GetItemIcon(itemName);
                if (icon != null)
                {
                    UITheme.DrawPixelIcon(spriteBatch, icon, new Vector2(b.X + (b.Width - 32) / 2f, b.Y + 2), 1);
                    var nameSize = UITheme.MeasureString(font, itemName) * 0.5f;
                    UITheme.DrawTextWithShadow(spriteBatch, font, itemName, new Vector2(b.X + (b.Width - nameSize.X) / 2f, b.Bottom - nameSize.Y - 3), Color.White, 0.5f);
                    continue;
                }

                var lines = TextLog.WrapText(font, itemName, (b.Width - 8) / 0.6f);
                float lineY = b.Y + (b.Height - lines.Count * 16) / 2f;
                foreach (var line in lines)
                {
                    var size = UITheme.MeasureString(font, line) * 0.6f;
                    UITheme.DrawTextWithShadow(spriteBatch, font, line, new Vector2(b.X + (b.Width - size.X) / 2f, lineY), Color.White, 0.6f);
                    lineY += 16;
                }
            }

            string stashTitle = _playerState.Items.Count == 0
                ? "Stash: empty"
                : "Stash - click to pack";
            UITheme.DrawTextWithShadow(spriteBatch, font, stashTitle, new Vector2(x, StashTop), new Color(190, 185, 200) * sectionIn, 0.64f);
            for (int c = 0; c < _stashChips.Count; c++)
            {
                var button = _stashChips[c].button;
                bool canPack = !_playerState.BeltFull;
                float hover = button.HoverAmount;
                float chipIn = Anim.Stagger(_elapsed, c, step: 0.04f, baseDelay: 0.45f, duration: 0.3f);
                if (chipIn <= 0.001f) continue;
                var b = Anim.Slide(button.Bounds, chipIn, new Vector2(0, 10));
                b = new RectangleF(b.X, b.Y - hover * 2f, b.Width, b.Height);
                UITheme.DrawFrame(spriteBatch, b, UIFrame.Tag, Color.Lerp(new Color(150, 110, 70), new Color(255, 170, 90), hover) * chipIn, (canPack ? 1f : 0.6f) * chipIn, shadow: 0.2f);
                UITheme.DrawTextWithShadow(spriteBatch, font, button.Label, new Vector2(b.X + 10, b.Y + 4), (canPack ? new Color(60, 38, 22) : new Color(120, 100, 85)) * chipIn, 0.68f, shadowAlpha: 0.12f * chipIn);
            }

            if (!string.IsNullOrEmpty(_beltMessage))
            {
                float t = Anim.Intro(_elapsed, _beltMessageAt, 0.25f);
                var size = UITheme.MeasureString(font, _beltMessage) * 0.62f;
                UITheme.DrawTextWithShadow(spriteBatch, font, _beltMessage, new Vector2(LoadoutPanel.Right - 20 - size.X + (1f - t) * 12f, StashTop), new Color(255, 205, 150) * t, 0.62f);
            }
        }

        private static void DrawSmallButton(SpriteBatch spriteBatch, SpriteFont font, Button button, Color? textColor = null)
        {
            float hover = button.HoverAmount;
            var b = button.Bounds;
            UITheme.DrawFrame(spriteBatch, b, UIFrame.Rect, Color.Lerp(Color.White * 0.4f, Color.White, hover), shadow: 0.3f);
            var size = UITheme.MeasureString(font, button.Label) * 0.66f;
            UITheme.DrawTextWithShadow(spriteBatch, font, button.Label, new Vector2(b.X + (b.Width - size.X) / 2f, b.Y + (b.Height - size.Y) / 2f), textColor ?? Color.White, 0.66f);
        }
    }
}
