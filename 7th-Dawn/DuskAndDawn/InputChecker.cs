using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MonoGame.Extended;

namespace DuskAndDawn
{
    public static class InputChecker
    {
        public static bool IsNewLeftClick(MouseState current, MouseState previous)
        {
            return current.LeftButton == ButtonState.Pressed
                && previous.LeftButton == ButtonState.Released;
        }

        public static bool Contains(RectangleF rect, float x, float y)
        {
            return x >= rect.X && x <= rect.X + rect.Width
                && y >= rect.Y && y <= rect.Y + rect.Height;
        }
    }
}
