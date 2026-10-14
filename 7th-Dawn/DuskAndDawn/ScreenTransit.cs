using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended;
using MonoGame.Extended.Screens.Transitions;
using System;

namespace DuskAndDawn
{
    public static class ScreenTransitions
    {
        internal static EasedFadeTransition Playing;

        public static bool IsTransitioning => Playing != null;

        public static Transition FadeTransition(GraphicsDevice graphicsDevice)
        {
            return new EasedFadeTransition(graphicsDevice, Color.Black, 0.5f);
        }

        public static Transition FadeTransition(GraphicsDevice graphicsDevice, Color color, float duration)
        {
            return new EasedFadeTransition(graphicsDevice, color, duration);
        }
    }

    public class EasedFadeTransition : Transition
    {
        private readonly GraphicsDevice _graphicsDevice;
        private SpriteBatch _spriteBatch;
        private readonly Color _color;

        public EasedFadeTransition(GraphicsDevice graphicsDevice, Color color, float duration = 0.5f)
            : base(duration)
        {
            _graphicsDevice = graphicsDevice;
            _color = color;

            Completed += (_, _) =>
            {
                if (ScreenTransitions.Playing == this) ScreenTransitions.Playing = null;
            };
        }

        public override void Dispose() => _spriteBatch?.Dispose();

        private static float EaseOutCubic(float t) => 1f - MathF.Pow(1f - t, 3);

        public override void Draw(GameTime gameTime)
        {
            ScreenTransitions.Playing = this;
            _spriteBatch ??= new SpriteBatch(_graphicsDevice);
            float alpha = EaseOutCubic(Value);

            _spriteBatch.Begin(SpriteSortMode.Deferred, null, samplerState: SamplerState.PointClamp, null, null);
            _spriteBatch.FillRectangle(0, 0, _graphicsDevice.Viewport.Width, _graphicsDevice.Viewport.Height, _color * alpha);
            _spriteBatch.End();
        }
    }
}