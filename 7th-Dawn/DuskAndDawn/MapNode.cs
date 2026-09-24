using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using MonoGame.Extended;

namespace DuskAndDawn
{
    public class MapNode
    {
        // Settable: a roaming creature can move from one room into a neighboring one.
        public RoomType Type { get; set; }

        public int Column { get; }
        public int Row { get; }

        /// <summary>Steps from the entrance along the maze's corridors.</summary>
        public int Depth { get; set; }

        /// <summary>Rooms this one has an open corridor to.</summary>
        public List<MapNode> Links { get; } = new List<MapNode>();
        public bool IsDeadEnd => Links.Count == 1;

        public bool Visited { get; set; }
        public bool Discovered { get; set; } // seen at all - drawn as a silhouette
        public bool Scouted { get; set; }    // type known

        public RectangleF ScreenBounds { get; set; }
        public Vector2 Center => new Vector2(ScreenBounds.X + ScreenBounds.Width / 2f, ScreenBounds.Y + ScreenBounds.Height / 2f);

        // Smooth hover fade for the minimap tiles - same idea as Button.HoverAmount, kept
        // here instead since a map node isn't a clickable Button (its bounds come from the
        // layout pass, not a button list).
        public float HoverAmount { get; private set; }
        private const float HoverSpeed = 9f;

        // 0 -> 1 fade-in the first time a room comes out of the fog.
        public float RevealAmount { get; private set; }
        private const float RevealSpeed = 2.5f;

        // Kicked to 1 when something moves into/out of this room; decays on its own and
        // drives a ripple so the change reads on the map.
        public float StirAmount { get; set; }
        private const float StirDecay = 0.7f;

        // Small per-room phase offset so idle animations (eyes blinking, glints) don't all
        // pulse in lockstep.
        public float Phase { get; }

        public MapNode(RoomType type, int column, int row, float phase)
        {
            Type = type;
            Column = column;
            Row = row;
            Phase = phase;
        }

        public void Link(MapNode other)
        {
            if (other == null || other == this || Links.Contains(other)) return;
            Links.Add(other);
            other.Links.Add(this);
        }

        public void UpdateAnimation(float dt, bool isHovered)
        {
            float target = isHovered ? 1f : 0f;
            HoverAmount = UITheme.MoveTowards(HoverAmount, target, HoverSpeed * dt);
            RevealAmount = UITheme.MoveTowards(RevealAmount, Discovered ? 1f : 0f, RevealSpeed * dt);
            StirAmount = UITheme.MoveTowards(StirAmount, 0f, StirDecay * dt);
        }
    }
}
