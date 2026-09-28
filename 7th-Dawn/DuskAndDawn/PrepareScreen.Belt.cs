using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using System.Collections.Generic;
using System.Linq;

namespace DuskAndDawn
{
    // Prepare screen: the belt. Only what's on the belt comes into the night, and it only
    // holds so much - so each night starts with a choice of what to carry. Remedies wait in
    // the stash at home; click one to pack it, click a belt slot to unpack. More slots can
    // be bought here.
    public partial class PreparationScreen
    {
        private readonly List<Button> _beltSlotButtons = new List<Button>();
        private readonly List<(Button button, string itemName)> _stashChips = new List<(Button, string)>();
        private Button _buySlotButton;
        private string _beltMessage = "";

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
            _buySlotButton = new Button(new RectangleF(LoadoutPanel.Right - 220, BeltTop - 4, 200, 26), "");
            LayoutStash();
        }

        /// <summary>One chip per kind of item in the stash, flowing left to right over two rows.</summary>
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
                    if (y > StashTop + 22 + 30) break; // two rows is all there's room for
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
            bool canBuy = _playerState.BeltSlots < PlayerState.MaxBeltSlots;
            _buySlotButton.UpdateAnimation(dt, canBuy && _buySlotButton.Contains(mouse.X, mouse.Y));
        }

        private void HandleBeltClick(int x, int y)
        {
            // Unpack: a filled belt slot goes back to the stash.
            for (int i = 0; i < _playerState.Belt.Count && i < _beltSlotButtons.Count; i++)
            {
                if (!_beltSlotButtons[i].Contains(x, y)) continue;
                _beltSlotButtons[i].TriggerPress();
                var item = _playerState.Belt[i];
                _playerState.Belt.RemoveAt(i);
                _playerState.Items.Add(item);
                _beltMessage = $"{item.Name} back in the stash.";
                LayoutStash();
                return;
            }

            // Pack: a stash item goes onto the belt, if there's room.
            foreach (var (button, itemName) in _stashChips)
            {
                if (!button.Contains(x, y)) continue;
                button.TriggerPress();
                if (_playerState.BeltFull)
                {
                    _beltMessage = "Belt is full - unpack something or buy a slot.";
                    return;
                }
                var item = _playerState.Items.Find(it => it.Name == itemName);
                _playerState.Items.Remove(item);
                _playerState.Belt.Add(item);
                _beltMessage = $"Packed a {item.Name}.";
                LayoutStash();
                return;
            }

            if (_buySlotButton.Contains(x, y) && _playerState.BeltSlots < PlayerState.MaxBeltSlots)
            {
                _buySlotButton.TriggerPress();
                var (food, planks, scraps) = PlayerState.BeltSlotCost(_playerState.BeltSlots + 1);
                if (_playerState.TrySpend(food, planks, scraps))
                {
                    _playerState.BeltSlots++;
                    _beltMessage = $"Your belt now holds {_playerState.BeltSlots}.";
                }
                else
                {
                    _beltMessage = $"Need {BaseBuilding.FormatCost(food, planks, scraps)} for another slot.";
                }
            }
        }

        private void DrawBelt(SpriteBatch spriteBatch, SpriteFont font)
        {
            float x = LoadoutPanel.X + 20;
            UITheme.DrawTextWithShadow(spriteBatch, font, $"Belt  {_playerState.Belt.Count}/{_playerState.BeltSlots}", new Vector2(x, BeltTop), Color.White, 0.9f);

            // Buy a slot.
            if (_playerState.BeltSlots < PlayerState.MaxBeltSlots)
            {
                var (food, planks, scraps) = PlayerState.BeltSlotCost(_playerState.BeltSlots + 1);
                bool afford = _playerState.CanAfford(food, planks, scraps);
                _buySlotButton.Label = $"+1 slot: {BaseBuilding.FormatCost(food, planks, scraps)}";
                DrawSmallButton(spriteBatch, font, _buySlotButton, afford ? new Color(120, 220, 130) : new Color(230, 110, 100));
            }

            for (int i = 0; i < _beltSlotButtons.Count; i++)
            {
                var button = _beltSlotButtons[i];
                var b = button.Bounds;
                bool unlocked = i < _playerState.BeltSlots;
                bool filled = i < _playerState.Belt.Count;

                if (!unlocked)
                {
                    // A slot you could buy: faint outline only.
                    UITheme.DrawRoundedRectBorder(spriteBatch, b, Color.White * 0.12f, 1.5f, 8f);
                    continue;
                }

                float hover = button.HoverAmount;
                Color top = filled ? UITheme.Brighten(new Color(60, 76, 64), hover * 0.2f) : new Color(28, 26, 36);
                Color bottom = filled ? UITheme.Brighten(new Color(40, 52, 44), hover * 0.2f) : new Color(20, 18, 26);
                Color border = filled ? Color.Lerp(new Color(150, 200, 160), Color.White, hover) : Color.White * 0.3f;
                UITheme.DrawPanel(spriteBatch, b, top, bottom, border, filled ? 2f : 1.5f, 8f, shadowStrength: 0.3f);

                if (!filled)
                {
                    const string empty = "empty";
                    var size = UITheme.MeasureString(font, empty) * 0.6f;
                    UITheme.DrawTextWithShadow(spriteBatch, font, empty, new Vector2(b.X + (b.Width - size.X) / 2f, b.Y + (b.Height - size.Y) / 2f), Color.White * 0.35f, 0.6f);
                    continue;
                }

                // Item name, wrapped to fit the slot.
                var lines = TextLog.WrapText(font, _playerState.Belt[i].Name, (b.Width - 8) / 0.6f);
                float lineY = b.Y + (b.Height - lines.Count * 16) / 2f;
                foreach (var line in lines)
                {
                    var size = UITheme.MeasureString(font, line) * 0.6f;
                    UITheme.DrawTextWithShadow(spriteBatch, font, line, new Vector2(b.X + (b.Width - size.X) / 2f, lineY), Color.White, 0.6f);
                    lineY += 16;
                }
            }

            // The stash.
            string stashTitle = _playerState.Items.Count == 0
                ? "Stash: empty"
                : "Stash - click to pack";
            UITheme.DrawTextWithShadow(spriteBatch, font, stashTitle, new Vector2(x, StashTop), new Color(190, 185, 200), 0.64f);
            foreach (var (button, _) in _stashChips)
            {
                bool canPack = !_playerState.BeltFull;
                float hover = button.HoverAmount;
                var b = button.Bounds;
                UITheme.DrawPanel(spriteBatch, b, canPack ? UITheme.Brighten(new Color(62, 58, 76), hover * 0.25f) : new Color(40, 38, 48),
                    canPack ? UITheme.Brighten(new Color(44, 40, 56), hover * 0.25f) : new Color(30, 28, 36),
                    Color.Lerp(Color.White * 0.35f, Color.White, hover), 1.5f, 8f, shadowStrength: 0.2f);
                UITheme.DrawTextWithShadow(spriteBatch, font, button.Label, new Vector2(b.X + 10, b.Y + 4), canPack ? Color.White : new Color(150, 145, 155), 0.68f);
            }

            if (!string.IsNullOrEmpty(_beltMessage))
            {
                var size = UITheme.MeasureString(font, _beltMessage) * 0.62f;
                UITheme.DrawTextWithShadow(spriteBatch, font, _beltMessage, new Vector2(LoadoutPanel.Right - 20 - size.X, StashTop), new Color(255, 205, 150), 0.62f);
            }
        }

        /// <summary>A compact button for paging and buying slots.</summary>
        private static void DrawSmallButton(SpriteBatch spriteBatch, SpriteFont font, Button button, Color? textColor = null)
        {
            float hover = button.HoverAmount;
            var b = button.Bounds;
            UITheme.DrawPanel(spriteBatch, b, UITheme.Brighten(new Color(58, 54, 72), hover * 0.25f), UITheme.Brighten(new Color(40, 36, 52), hover * 0.25f),
                Color.Lerp(Color.White * 0.4f, Color.White, hover), 1.5f, 8f, shadowStrength: 0.3f);
            var size = UITheme.MeasureString(font, button.Label) * 0.66f;
            UITheme.DrawTextWithShadow(spriteBatch, font, button.Label, new Vector2(b.X + (b.Width - size.X) / 2f, b.Y + (b.Height - size.Y) / 2f), textColor ?? Color.White, 0.66f);
        }
    }
}
