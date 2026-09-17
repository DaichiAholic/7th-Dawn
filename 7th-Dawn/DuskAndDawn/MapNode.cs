using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MonoGame.Extended;

namespace DuskAndDawn
{
    public class MapNode
    {
        public RoomType Type { get; }
        public int Depth { get; }

        public bool Visited { get; set; }
        public RectangleF ScreenBounds { get; set; }

        // Smooth hover fade for the minimap tiles - same idea as Button.HoverAmount, kept
        // here instead since a map node isn't a clickable Button (its bounds come from the
        // layout pass, not a button list).
        public float HoverAmount { get; private set; }
        private const float HoverSpeed = 9f;

        public MapNode(RoomType type, int depth)
        {
            Type = type;
            Depth = depth;
        }

        public void UpdateHover(float dt, bool isHovered)
        {
            float target = isHovered ? 1f : 0f;
            HoverAmount = UITheme.MoveTowards(HoverAmount, target, HoverSpeed * dt);
        }
    }
}
