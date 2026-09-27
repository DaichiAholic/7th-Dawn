using Microsoft.Xna.Framework; // was: using System.Drawing;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended;
using MonoGame.Extended.Screens.Transitions;
using System;

namespace DuskAndDawn
{
    public static class ScreenTransitions
    {
        // The fade currently on screen, if any. Menus and the pause key hold off while one is
        // playing, since a screen opened mid-fade would be the one the fade then closes.
        internal static EasedFadeTransition Playing;

        public static bool IsTransitioning => Playing != null;

        // Drop-in replacement for every existing call site - same signature, now eased.
        public static Transition FadeTransition(GraphicsDevice graphicsDevice)
        {
            return new EasedFadeTransition(graphicsDevice, Color.Black, 0.5f);
        }

        // Optional overload for moments that want a different color/speed,
        // e.g. a faster reddish snap for Night -> Day.
        public static Transition FadeTransition(GraphicsDevice graphicsDevice, Color color, float duration)
        {
            return new EasedFadeTransition(graphicsDevice, color, duration);
        }
    }

    // Same job as MonoGame.Extended's built-in FadeTransition, but the alpha
    // follows an ease-out-cubic curve instead of a linear ramp.
    public class EasedFadeTransition : Transition
    {
        private readonly GraphicsDevice _graphicsDevice;
        private readonly SpriteBatch _spriteBatch;
        private readonly Color _color;

        public EasedFadeTransition(GraphicsDevice graphicsDevice, Color color, float duration = 0.5f)
            : base(duration)
        {
            _graphicsDevice = graphicsDevice;
            _color = color;
            _spriteBatch = new SpriteBatch(graphicsDevice);

            Completed += (_, _) =>
            {
                if (ScreenTransitions.Playing == this) ScreenTransitions.Playing = null;
            };
        }

        public override void Dispose() => _spriteBatch.Dispose();

        private static float EaseOutCubic(float t) => 1f - MathF.Pow(1f - t, 3);

        public override void Draw(GameTime gameTime)
        {
            // Only a transition the ScreenManager actually runs gets drawn, so this is the
            // reliable "a fade is playing" signal (a refused transition never gets here).
            ScreenTransitions.Playing = this;
            float alpha = EaseOutCubic(Value);

            _spriteBatch.Begin(SpriteSortMode.Deferred, null, samplerState: SamplerState.PointClamp, null, null);
            _spriteBatch.FillRectangle(0, 0, _graphicsDevice.Viewport.Width, _graphicsDevice.Viewport.Height, _color * alpha);
            _spriteBatch.End();
        }
    }
}