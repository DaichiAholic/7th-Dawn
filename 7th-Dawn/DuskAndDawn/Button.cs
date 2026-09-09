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
    }
}
