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

        public MapNode(RoomType type, int depth)
        {
            Type = type;
            Depth = depth;
        }
    }
}
