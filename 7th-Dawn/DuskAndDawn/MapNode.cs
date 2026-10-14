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
        public RoomType Type { get; set; }

        public int Column { get; }
        public int Row { get; }

        public int Depth { get; set; }

        public List<MapNode> Links { get; } = new List<MapNode>();
        public bool IsDeadEnd => Links.Count == 1;

        public bool Visited { get; set; }

        public List<Enemy> Enemies { get; set; }

        public bool HasBoss => Enemies != null && Enemies.Exists(e => e.IsBoss && !e.IsDefeated);
        public bool Discovered { get; set; }
        public bool Scouted { get; set; }

        public RectangleF ScreenBounds { get; set; }
        public Vector2 Center => new Vector2(ScreenBounds.X + ScreenBounds.Width / 2f, ScreenBounds.Y + ScreenBounds.Height / 2f);

        public float HoverAmount { get; private set; }
        private const float HoverSpeed = 9f;

        public float RevealAmount { get; private set; }
        private const float RevealSpeed = 2.5f;

        public float StirAmount { get; set; }
        private const float StirDecay = 0.7f;

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
