using Microsoft.Xna.Framework;
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
        public static float CanvasScale = 1f;
        public static Vector2 CanvasOffset = Vector2.Zero;

        public static MouseState GetMouse()
        {
            var raw = Mouse.GetState();
            int x = (int)((raw.X - CanvasOffset.X) / CanvasScale);
            int y = (int)((raw.Y - CanvasOffset.Y) / CanvasScale);
            return new MouseState(x, y, raw.ScrollWheelValue, raw.LeftButton, raw.MiddleButton, raw.RightButton, raw.XButton1, raw.XButton2);
        }

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
