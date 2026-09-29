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

        // Smooth hover animation: 0 = not hovered, 1 = fully hovered. Call UpdateAnimation()
        // once per frame with the current hover test result, then read HoverAmount when
        // drawing so hover states fade in/out instead of snapping between two fixed colors.
        public float HoverAmount { get; private set; }
        private const float HoverSpeed = 9f; // higher = snappier fade

        // A brief "press" pulse for click feedback - trigger it on click, it decays back to
        // 0 on its own. Draw code can use EaseOutBack(1 - PressAmount) for a little bounce.
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

        /// <summary>Advances the hover/press animations. Call once per frame per button,
        /// even for buttons that aren't part of the currently active menu.</summary>
        public void UpdateAnimation(float dt, bool isHovered)
        {
            float target = (isHovered && Enabled) ? 1f : 0f;
            HoverAmount = UITheme.MoveTowards(HoverAmount, target, HoverSpeed * dt);
            PressAmount = UITheme.MoveTowards(PressAmount, 0f, PressDecaySpeed * dt);
        }

        /// <summary>Call when this button is clicked, to trigger the brief press feedback pulse.</summary>
        public void TriggerPress() => PressAmount = 1f;
    }
}
