using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MonoGame.Extended;

namespace DuskAndDawn
{
    public class Button
    {
        public RectangleF Bounds;
        public string Label;
        public bool Enabled = true;

        public float HoverAmount { get; private set; }
        private const float HoverSpeed = 9f;

        public float PressAmount { get; private set; }
        private const float PressDecaySpeed = 5f;

        public Button(RectangleF bounds, string label)
        {
            Bounds = bounds;
            Label = label;
        }

        public bool Contains(float x, float y)
        {
            return Enabled
                && x >= Bounds.X && x <= Bounds.X + Bounds.Width
                && y >= Bounds.Y && y <= Bounds.Y + Bounds.Height;
        }

        public void UpdateAnimation(float dt, bool isHovered)
        {
            float target = (isHovered && Enabled) ? 1f : 0f;
            HoverAmount = UITheme.MoveTowards(HoverAmount, target, HoverSpeed * dt);
            PressAmount = UITheme.MoveTowards(PressAmount, 0f, PressDecaySpeed * dt);
        }

        public void TriggerPress() => PressAmount = 1f;
    }
}
