using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.Screens.Transitions;
using Microsoft.Xna.Framework; // was: using System.Drawing;

namespace DuskAndDawn
{
    public static class ScreenTransitions
    {
        public static FadeTransition Fade(GraphicsDevice graphicsDevice)
        {
            return new FadeTransition(graphicsDevice, Color.Black, 0.4f);
        }
    }
}