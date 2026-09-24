using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended;
using System;

namespace DuskAndDawn
{
    /// <summary>
    /// Shared visual toolkit used by every screen: rounded panels with soft drop shadows,
    /// vertical gradient fills, drop-shadowed text, and a couple of standard easing curves.
    /// Nothing in here touches game state or input - it only draws. Call UITheme.LoadContent()
    /// once, right after the GraphicsDevice exists (see Game1.Initialize), before any screen
    /// tries to draw with it.
    ///
    /// All of the rounded-corner drawing is built from one small baked circle texture (no new
    /// art asset needed - it's generated in code) sliced into quarters and mirrored into place,
    /// the same "compose primitives" approach the rest of the codebase already uses for its
    /// circle/triangle approximations.
    /// </summary>
    public static class UITheme
    {
        // Resolution of the baked corner circle - high enough that even a big panel's corner
        // still looks smooth after being stretched (SpriteBatch's default sampler is bilinear,
        // so this scales cleanly instead of looking blocky).
        private const int CircleTextureSize = 128;
        private const int CircleRadius = CircleTextureSize / 2;
        private static readonly Rectangle CornerSource = new Rectangle(0, 0, CircleRadius, CircleRadius);

        private static Texture2D _circle;
        private static Texture2D _glow;
        public static bool IsLoaded => _circle != null;

        public static void LoadContent(GraphicsDevice graphicsDevice)
        {
            _circle = BuildSoftCircle(graphicsDevice, CircleTextureSize);
            _glow = BuildRadialGlow(graphicsDevice, 128);
        }

        /// <summary>A soft light blob: fully bright in the middle, falling off smoothly to
        /// nothing at the edge. Used for lantern light, ember glows and room auras.</summary>
        private static Texture2D BuildRadialGlow(GraphicsDevice graphicsDevice, int size)
        {
            var texture = new Texture2D(graphicsDevice, size, size);
            var data = new Color[size * size];
            float radius = size / 2f;
            var center = new Vector2(radius, radius);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                    float t = MathHelper.Clamp(1f - distance / radius, 0f, 1f);
                    data[y * size + x] = Color.White * (t * t);
                }
            }

            texture.SetData(data);
            return texture;
        }

        private static Texture2D BuildSoftCircle(GraphicsDevice graphicsDevice, int size)
        {
            var texture = new Texture2D(graphicsDevice, size, size);
            var data = new Color[size * size];
            float radius = size / 2f;
            var center = new Vector2(radius, radius);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                    // ~1.5px soft edge instead of a hard cutoff - this is what makes every
                    // rounded corner read as smooth rather than jagged, even after being
                    // stretched up to a large panel's corner radius.
                    float alpha = MathHelper.Clamp(radius - distance + 0.75f, 0f, 1.5f) / 1.5f;
                    data[y * size + x] = Color.White * alpha;
                }
            }

            texture.SetData(data);
            return texture;
        }

        // ---------- Easing & motion ----------
        // Single shared source for the curves used across every screen's hover/pop/fade
        // animations, instead of each screen keeping its own private copy.

        public static float EaseOutCubic(float t) => 1f - MathF.Pow(1f - MathHelper.Clamp(t, 0f, 1f), 3);

        public static float EaseInOutSine(float t)
        {
            t = MathHelper.Clamp(t, 0f, 1f);
            return -(MathF.Cos(MathF.PI * t) - 1f) / 2f;
        }

        // A tiny overshoot on the way in - good for a click/press "pop".
        public static float EaseOutBack(float t)
        {
            t = MathHelper.Clamp(t, 0f, 1f);
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float u = t - 1f;
            return 1f + c3 * (u * u * u) + c1 * (u * u);
        }

        /// <summary>Moves a value toward a target at a constant rate - the basic building
        /// block for every smooth (rather than instant) hover/fade transition.</summary>
        public static float MoveTowards(float current, float target, float maxDelta)
        {
            if (MathF.Abs(target - current) <= maxDelta) return target;
            return current + Math.Sign(target - current) * maxDelta;
        }

        /// <summary>0-1 oscillation for slow "breathing" glow effects (low-hope pulse, a
        /// warning bar, an idle button glimmer). Feed it accumulated seconds, not delta time.</summary>
        public static float PulseSine(float totalSeconds, float speed = 3f) =>
            (MathF.Sin(totalSeconds * speed) + 1f) / 2f;

        public static Color Brighten(Color color, float amount) => Color.Lerp(color, Color.White, MathHelper.Clamp(amount, 0f, 1f));
        public static Color Darken(Color color, float amount) => Color.Lerp(color, Color.Black, MathHelper.Clamp(amount, 0f, 1f));

        // ---------- Rounded rectangles ----------

        /// <summary>Clamps a requested radius so it never exceeds half the shorter side.</summary>
        public static float ClampRadius(RectangleF bounds, float radius) =>
            MathHelper.Clamp(radius, 0f, MathF.Min(bounds.Width, bounds.Height) / 2f);

        public static void FillRoundedRect(SpriteBatch spriteBatch, RectangleF bounds, Color color, float radius)
        {
            radius = ClampRadius(bounds, radius);
            if (radius < 0.5f || !IsLoaded)
            {
                spriteBatch.FillRectangle(bounds, color);
                return;
            }

            int r = (int)MathF.Ceiling(radius);
            DrawCorners(spriteBatch, bounds, r, color);

            spriteBatch.FillRectangle(new RectangleF(bounds.X + radius, bounds.Y, bounds.Width - radius * 2f, radius), color);
            spriteBatch.FillRectangle(new RectangleF(bounds.X + radius, bounds.Y + bounds.Height - radius, bounds.Width - radius * 2f, radius), color);
            spriteBatch.FillRectangle(new RectangleF(bounds.X, bounds.Y + radius, bounds.Width, bounds.Height - radius * 2f), color);
        }

        private static void DrawCorners(SpriteBatch spriteBatch, RectangleF bounds, int r, Color color)
        {
            spriteBatch.Draw(_circle, new Rectangle((int)bounds.X, (int)bounds.Y, r, r), CornerSource, color, 0f, Vector2.Zero, SpriteEffects.None, 0f);
            spriteBatch.Draw(_circle, new Rectangle((int)(bounds.X + bounds.Width) - r, (int)bounds.Y, r, r), CornerSource, color, 0f, Vector2.Zero, SpriteEffects.FlipHorizontally, 0f);
            spriteBatch.Draw(_circle, new Rectangle((int)bounds.X, (int)(bounds.Y + bounds.Height) - r, r, r), CornerSource, color, 0f, Vector2.Zero, SpriteEffects.FlipVertically, 0f);
            spriteBatch.Draw(_circle, new Rectangle((int)(bounds.X + bounds.Width) - r, (int)(bounds.Y + bounds.Height) - r, r, r), CornerSource, color, 0f, Vector2.Zero, SpriteEffects.FlipHorizontally | SpriteEffects.FlipVertically, 0f);
        }

        /// <summary>Rounded outline, drawn as a slightly larger rounded fill in the border
        /// color that the interior fill then sits on top of - call this before the fill.</summary>
        public static void DrawRoundedRectBorder(SpriteBatch spriteBatch, RectangleF bounds, Color color, float thickness, float radius)
        {
            if (thickness <= 0f) return;
            radius = ClampRadius(bounds, radius);
            var outer = new RectangleF(bounds.X - thickness, bounds.Y - thickness, bounds.Width + thickness * 2f, bounds.Height + thickness * 2f);
            FillRoundedRect(spriteBatch, outer, color, radius + thickness);
        }

        /// <summary>A soft, cheaply-faked blur: a few shrinking, fading rounded-rect layers
        /// offset down-and-right, instead of one hard-edged rectangle. Draw this BEFORE the
        /// panel/border/fill so the panel sits on top of it.</summary>
        public static void DrawSoftShadow(SpriteBatch spriteBatch, RectangleF bounds, float radius, float strength = 1f)
        {
            var offset = new Vector2(4f, 6f);
            radius = ClampRadius(bounds, radius);
            DrawShadowLayer(spriteBatch, bounds, radius, offset, 10f, 0.05f * strength);
            DrawShadowLayer(spriteBatch, bounds, radius, offset, 6f, 0.08f * strength);
            DrawShadowLayer(spriteBatch, bounds, radius, offset, 3f, 0.12f * strength);
        }

        private static void DrawShadowLayer(SpriteBatch spriteBatch, RectangleF bounds, float radius, Vector2 offset, float spread, float alpha)
        {
            var shadowBounds = new RectangleF(
                bounds.X - spread + offset.X, bounds.Y - spread + offset.Y,
                bounds.Width + spread * 2f, bounds.Height + spread * 2f);
            FillRoundedRect(spriteBatch, shadowBounds, Color.Black * alpha, radius + spread);
        }

        // ---------- Circles & glows ----------

        /// <summary>Solid, smooth-edged filled circle.</summary>
        public static void FillCircle(SpriteBatch spriteBatch, Vector2 center, float radius, Color color)
        {
            if (!IsLoaded || radius <= 0f) return;
            var bounds = new Rectangle((int)MathF.Round(center.X - radius), (int)MathF.Round(center.Y - radius), (int)MathF.Round(radius * 2f), (int)MathF.Round(radius * 2f));
            spriteBatch.Draw(_circle, bounds, color);
        }

        /// <summary>Soft radial light centered on a point. Stack a couple at different sizes
        /// for a hot core with a wide falloff.</summary>
        public static void DrawGlow(SpriteBatch spriteBatch, Vector2 center, float radius, Color color)
        {
            if (_glow == null || radius <= 0f) return;
            var bounds = new Rectangle((int)(center.X - radius), (int)(center.Y - radius), (int)(radius * 2f), (int)(radius * 2f));
            spriteBatch.Draw(_glow, bounds, color);
        }

        // ---------- Gradients ----------

        /// <summary>Flat vertical gradient (no rounding) built from stacked strips - the same
        /// scanline technique the codebase already uses for its circle/triangle approximations.</summary>
        public static void FillGradientRect(SpriteBatch spriteBatch, RectangleF bounds, Color topColor, Color bottomColor, int steps = 14)
        {
            if (bounds.Height <= 0f || bounds.Width <= 0f) return;
            for (int i = 0; i < steps; i++)
            {
                float t0 = i / (float)steps;
                float t1 = (i + 1) / (float)steps;
                var strip = new RectangleF(bounds.X, bounds.Y + bounds.Height * t0, bounds.Width, bounds.Height * (t1 - t0) + 1f);
                spriteBatch.FillRectangle(strip, Color.Lerp(topColor, bottomColor, (t0 + t1) / 2f));
            }
        }

        /// <summary>Vertical gradient fill inside a rounded rect - a cheap top-lit "glossy
        /// panel" look. Corners are tinted with the gradient color nearest their edge; the
        /// straight top/bottom/middle bands carry the real gradient.</summary>
        public static void FillRoundedRectGradient(SpriteBatch spriteBatch, RectangleF bounds, Color topColor, Color bottomColor, float radius, int steps = 14)
        {
            radius = ClampRadius(bounds, radius);
            if (radius < 0.5f || !IsLoaded)
            {
                FillGradientRect(spriteBatch, bounds, topColor, bottomColor, steps);
                return;
            }

            Color ColorAt(float y) => Color.Lerp(topColor, bottomColor, MathHelper.Clamp((y - bounds.Y) / bounds.Height, 0f, 1f));

            int r = (int)MathF.Ceiling(radius);
            Color topCornerTint = ColorAt(bounds.Y + radius * 0.4f);
            Color bottomCornerTint = ColorAt(bounds.Y + bounds.Height - radius * 0.4f);

            spriteBatch.Draw(_circle, new Rectangle((int)bounds.X, (int)bounds.Y, r, r), CornerSource, topCornerTint, 0f, Vector2.Zero, SpriteEffects.None, 0f);
            spriteBatch.Draw(_circle, new Rectangle((int)(bounds.X + bounds.Width) - r, (int)bounds.Y, r, r), CornerSource, topCornerTint, 0f, Vector2.Zero, SpriteEffects.FlipHorizontally, 0f);
            spriteBatch.Draw(_circle, new Rectangle((int)bounds.X, (int)(bounds.Y + bounds.Height) - r, r, r), CornerSource, bottomCornerTint, 0f, Vector2.Zero, SpriteEffects.FlipVertically, 0f);
            spriteBatch.Draw(_circle, new Rectangle((int)(bounds.X + bounds.Width) - r, (int)(bounds.Y + bounds.Height) - r, r, r), CornerSource, bottomCornerTint, 0f, Vector2.Zero, SpriteEffects.FlipHorizontally | SpriteEffects.FlipVertically, 0f);

            FillGradientRect(spriteBatch, new RectangleF(bounds.X + radius, bounds.Y, bounds.Width - radius * 2f, radius), topColor, ColorAt(bounds.Y + radius), 4);
            FillGradientRect(spriteBatch, new RectangleF(bounds.X, bounds.Y + radius, bounds.Width, bounds.Height - radius * 2f), ColorAt(bounds.Y + radius), ColorAt(bounds.Y + bounds.Height - radius), steps);
            FillGradientRect(spriteBatch, new RectangleF(bounds.X + radius, bounds.Y + bounds.Height - radius, bounds.Width - radius * 2f, radius), ColorAt(bounds.Y + bounds.Height - radius), bottomColor, 4);
        }

        /// <summary>The full "nice panel" combo: soft shadow, rounded border, rounded gradient
        /// fill - in the right order so each layer sits correctly on top of the last.</summary>
        public static void DrawPanel(SpriteBatch spriteBatch, RectangleF bounds, Color topColor, Color bottomColor, Color borderColor, float borderThickness, float radius, float shadowStrength = 1f)
        {
            if (shadowStrength > 0f)
            {
                DrawSoftShadow(spriteBatch, bounds, radius, shadowStrength);
            }
            DrawRoundedRectBorder(spriteBatch, bounds, borderColor, borderThickness, radius);
            FillRoundedRectGradient(spriteBatch, bounds, topColor, bottomColor, radius, 14);
        }

        // ---------- Pixel-art icons ----------

        /// <summary>Draws a small pixel-art sprite (weapon icons etc.) at a whole-number scale
        /// with point sampling, so it stays crisp instead of being blurred by the default
        /// linear filter. Briefly restarts the batch to switch sampler, then restores the
        /// default Begin() every screen uses. Safe to call with a null texture (draws nothing).</summary>
        public static void DrawPixelIcon(SpriteBatch spriteBatch, Texture2D texture, Vector2 topLeft, int scale)
        {
            if (texture == null) return;

            spriteBatch.End();
            spriteBatch.Begin(samplerState: SamplerState.PointClamp);
            // Snapped to whole pixels - a fractional position smears pixel art even with point sampling.
            var snapped = new Vector2(MathF.Round(topLeft.X), MathF.Round(topLeft.Y));
            spriteBatch.Draw(texture, snapped, null, Color.White, 0f, Vector2.Zero, (float)scale, SpriteEffects.None, 0f);
            spriteBatch.End();
            spriteBatch.Begin();
        }

        /// <summary>One frame of a pixel-art spritesheet, centered on a point, at a whole-number
        /// scale with point sampling. Used for the combat animations.</summary>
        public static void DrawPixelSprite(SpriteBatch spriteBatch, Texture2D texture, Rectangle source, Vector2 center, int scale)
        {
            if (texture == null) return;

            var topLeft = new Vector2(
                MathF.Round(center.X - source.Width * scale / 2f),
                MathF.Round(center.Y - source.Height * scale / 2f));

            spriteBatch.End();
            spriteBatch.Begin(samplerState: SamplerState.PointClamp);
            spriteBatch.Draw(texture, topLeft, source, Color.White, 0f, Vector2.Zero, (float)scale, SpriteEffects.None, 0f);
            spriteBatch.End();
            spriteBatch.Begin();
        }

        /// <summary>Icon with a dark rounded backing slot. The slot is drawn even when there's
        /// no icon yet, so cards keep the same layout for weapons still waiting on art.</summary>
        public static void DrawIconSlot(SpriteBatch spriteBatch, Texture2D texture, Vector2 topLeft, int scale, int nativeSize = 32)
        {
            float size = nativeSize * scale;
            var slot = new RectangleF(topLeft.X - 4, topLeft.Y - 4, size + 8, size + 8);
            FillRoundedRect(spriteBatch, slot, Color.Black * 0.35f, 8f);
            DrawPixelIcon(spriteBatch, texture, topLeft, scale);
        }

        // ---------- Text ----------

        public static void DrawTextWithShadow(SpriteBatch spriteBatch, SpriteFont font, string text, Vector2 position, Color color, float scale = 1f)
        {
            spriteBatch.DrawString(font, text, position + new Vector2(2f, 2f) * scale, Color.Black * 0.45f, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            spriteBatch.DrawString(font, text, position, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }
    }
}