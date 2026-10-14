using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended;
using System;
using System.Collections.Generic;

namespace DuskAndDawn
{
    public enum UIFrame { Rect, Pill, Tag, Wood }

    public readonly record struct SlicedTexture(Texture2D Texture, int Left, int Top, int Right, int Bottom);

    public static class UITheme
    {
        private const int FramePixelSize = 3;

        private sealed record FrameSprite(SlicedTexture Fill, SlicedTexture? Outline, bool TileY, int OutlineInset, int WashInset);

        private static readonly Dictionary<UIFrame, FrameSprite> _frames = new Dictionary<UIFrame, FrameSprite>();
        private static readonly List<(int src, int srcLen, int dst, int dstLen)> _columns = new List<(int, int, int, int)>();
        private static readonly List<(int src, int srcLen, int dst, int dstLen)> _rows = new List<(int, int, int, int)>();

        public static void RegisterFrame(UIFrame frame, SlicedTexture fill, SlicedTexture? outline, bool tileY, int outlineInset = 0, int washInset = 1) =>
            _frames[frame] = new FrameSprite(fill, outline, tileY, outlineInset, washInset);

        public static void DrawFrame(SpriteBatch spriteBatch, RectangleF bounds, UIFrame frame, Color border, float alpha = 1f, float shadow = 0f, Color? wash = null)
        {
            if (alpha <= 0.001f || !_frames.TryGetValue(frame, out var sprite)) return;
            if (shadow > 0f) DrawSoftShadow(spriteBatch, bounds, 6f, shadow * alpha);
            DrawFrameLayers(spriteBatch, bounds, sprite, Color.White * alpha, border, wash ?? Color.Transparent);
        }

        public static void DrawFrameOutline(SpriteBatch spriteBatch, RectangleF bounds, UIFrame frame, Color border)
        {
            if (!_frames.TryGetValue(frame, out var sprite)) return;
            DrawFrameLayers(spriteBatch, bounds, sprite, Color.Transparent, border, Color.Transparent);
        }

        private static void DrawFrameLayers(SpriteBatch spriteBatch, RectangleF bounds, FrameSprite sprite, Color fill, Color border, Color wash)
        {
            int x = (int)MathF.Round(bounds.X * RenderScale), y = (int)MathF.Round(bounds.Y * RenderScale);
            int w = (int)MathF.Round((bounds.X + bounds.Width) * RenderScale) - x;
            int h = (int)MathF.Round((bounds.Y + bounds.Height) * RenderScale) - y;
            if (w <= 0 || h <= 0) return;

            int s = FramePixelSize;
            var f = sprite.Fill;
            while (s > 1 && ((f.Left + f.Right) * s > w || (f.Top + f.Bottom) * s > h)) s--;

            spriteBatch.End();
            BeginCanvas(spriteBatch, SamplerState.PointClamp);
            if (fill.A > 0) DrawNineSlice(spriteBatch, sprite.Fill, sprite.TileY, x, y, w, h, s, fill);
            if (wash.A > 0)
            {
                int i = sprite.WashInset * s;
                spriteBatch.FillRectangle(new RectangleF((x + i) / RenderScale, (y + i) / RenderScale, (w - i * 2) / RenderScale, (h - i * 2) / RenderScale), wash);
            }
            if (sprite.Outline is SlicedTexture outline && border.A > 0)
            {
                int o = sprite.OutlineInset * s;
                DrawNineSlice(spriteBatch, outline, sprite.TileY, x + o, y + o, w - o * 2, h - o * 2, s, OutlineTint(border));
            }
            spriteBatch.End();
            BeginCanvas(spriteBatch);
        }

        private static Color OutlineTint(Color color)
        {
            const float boost = 255f / 211f;
            int Channel(byte c) => Math.Min(color.A, (int)(c * boost));
            return new Color(Channel(color.R), Channel(color.G), Channel(color.B), color.A);
        }

        private static void DrawNineSlice(SpriteBatch spriteBatch, SlicedTexture sliced, bool tileY, int x, int y, int w, int h, int s, Color color)
        {
            if (w <= 0 || h <= 0) return;
            var texture = sliced.Texture;
            Slice(_columns, sliced.Left, sliced.Right, texture.Width, w, s, tile: true);
            Slice(_rows, sliced.Top, sliced.Bottom, texture.Height, h, s, tileY);

            foreach (var row in _rows)
            {
                foreach (var column in _columns)
                {
                    if (column.dstLen <= 0 || row.dstLen <= 0 || column.srcLen <= 0 || row.srcLen <= 0) continue;
                    var position = new Vector2((x + column.dst) / RenderScale, (y + row.dst) / RenderScale);
                    var scale = new Vector2(column.dstLen / (float)column.srcLen, row.dstLen / (float)row.srcLen) / RenderScale;
                    var source = new Rectangle(column.src, row.src, column.srcLen, row.srcLen);
                    spriteBatch.Draw(texture, position, source, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                }
            }
        }

        private static void Slice(List<(int src, int srcLen, int dst, int dstLen)> slices, int lead, int trail, int textureSize, int size, int s, bool tile)
        {
            slices.Clear();
            int middle = textureSize - lead - trail;
            int leadLen = lead * s, trailLen = trail * s;
            if (leadLen + trailLen > size)
            {
                leadLen = size * lead / (lead + trail);
                trailLen = size - leadLen;
            }
            int span = size - leadLen - trailLen;

            slices.Add((0, lead, 0, leadLen));
            if (span > 0 && middle > 0)
            {
                if (!tile)
                {
                    slices.Add((lead, middle, leadLen, span));
                }
                else
                {
                    for (int d = 0; d < span; d += middle * s)
                    {
                        int len = Math.Min(middle * s, span - d);
                        slices.Add((lead, (len + s - 1) / s, leadLen + d, len));
                    }
                }
            }
            slices.Add((textureSize - trail, trail, size - trailLen, trailLen));
        }

        private const int CircleTextureSize = 128;
        private const int CircleRadius = CircleTextureSize / 2;
        private static readonly Rectangle CornerSource = new Rectangle(0, 0, CircleRadius, CircleRadius);

        private static Texture2D _circle;
        private static Texture2D _glow;
        public static bool IsLoaded => _circle != null;

        public static float RenderScale { get; private set; } = 1f;
        public static Matrix CanvasTransform { get; private set; } = Matrix.Identity;

        public static void SetRenderScale(float scale)
        {
            RenderScale = scale;
            CanvasTransform = Matrix.CreateScale(scale, scale, 1f);
        }

        public static void BeginCanvas(SpriteBatch spriteBatch, SamplerState samplerState = null) =>
            spriteBatch.Begin(samplerState: samplerState, transformMatrix: CanvasTransform);

        private static SpriteFont _baseFont;
        private static SpriteFont _hiResFont;

        public static void RegisterHiResFont(SpriteFont baseFont, SpriteFont hiResFont)
        {
            _baseFont = baseFont;
            _hiResFont = hiResFont;
        }

        private static bool HasHiRes(SpriteFont font) => _hiResFont != null && font == _baseFont && RenderScale != 1f;

        public static Vector2 MeasureString(SpriteFont font, string text) =>
            HasHiRes(font) ? _hiResFont.MeasureString(text) / RenderScale : font.MeasureString(text);

        private static Vector2 SnapToDevice(Vector2 point) => new Vector2(
            MathF.Round(point.X * RenderScale) / RenderScale,
            MathF.Round(point.Y * RenderScale) / RenderScale);

        public static void LoadContent(GraphicsDevice graphicsDevice)
        {
            _circle = BuildSoftCircle(graphicsDevice, CircleTextureSize);
            _glow = BuildRadialGlow(graphicsDevice, 128);
        }

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
                    float alpha = MathHelper.Clamp(radius - distance + 0.75f, 0f, 1.5f) / 1.5f;
                    data[y * size + x] = Color.White * alpha;
                }
            }

            texture.SetData(data);
            return texture;
        }

        public static float EaseOutCubic(float t) => 1f - MathF.Pow(1f - MathHelper.Clamp(t, 0f, 1f), 3);

        public static float EaseInOutSine(float t)
        {
            t = MathHelper.Clamp(t, 0f, 1f);
            return -(MathF.Cos(MathF.PI * t) - 1f) / 2f;
        }

        public static float EaseOutBack(float t)
        {
            t = MathHelper.Clamp(t, 0f, 1f);
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float u = t - 1f;
            return 1f + c3 * (u * u * u) + c1 * (u * u);
        }

        public static float MoveTowards(float current, float target, float maxDelta)
        {
            if (MathF.Abs(target - current) <= maxDelta) return target;
            return current + Math.Sign(target - current) * maxDelta;
        }

        public static float PulseSine(float totalSeconds, float speed = 3f) =>
            (MathF.Sin(totalSeconds * speed) + 1f) / 2f;

        public static Color Brighten(Color color, float amount) => Color.Lerp(color, Color.White, MathHelper.Clamp(amount, 0f, 1f));
        public static Color Darken(Color color, float amount) => Color.Lerp(color, Color.Black, MathHelper.Clamp(amount, 0f, 1f));

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

        public static void FillCircle(SpriteBatch spriteBatch, Vector2 center, float radius, Color color)
        {
            if (!IsLoaded || radius <= 0f) return;
            var bounds = new Rectangle((int)MathF.Round(center.X - radius), (int)MathF.Round(center.Y - radius), (int)MathF.Round(radius * 2f), (int)MathF.Round(radius * 2f));
            spriteBatch.Draw(_circle, bounds, color);
        }

        public static void DrawGlow(SpriteBatch spriteBatch, Vector2 center, float radius, Color color)
        {
            if (_glow == null || radius <= 0f) return;
            var bounds = new Rectangle((int)(center.X - radius), (int)(center.Y - radius), (int)(radius * 2f), (int)(radius * 2f));
            spriteBatch.Draw(_glow, bounds, color);
        }

        public static void FillGradientRect(SpriteBatch spriteBatch, RectangleF bounds, Color topColor, Color bottomColor, int steps = 14)
        {
            if (bounds.Height <= 0f || bounds.Width <= 0f) return;
            float overlap = topColor.A == 255 && bottomColor.A == 255 ? 1f : 0f;
            for (int i = 0; i < steps; i++)
            {
                float t0 = i / (float)steps;
                float t1 = (i + 1) / (float)steps;
                float y0 = bounds.Y + bounds.Height * t0, y1 = bounds.Y + bounds.Height * t1;
                var strip = new RectangleF(bounds.X, y0, bounds.Width, y1 - y0 + overlap);
                spriteBatch.FillRectangle(strip, Color.Lerp(topColor, bottomColor, (t0 + t1) / 2f));
            }
        }

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

        public static void DrawPixelIcon(SpriteBatch spriteBatch, Texture2D texture, Vector2 topLeft, float scale, Color? tint = null)
        {
            if (texture == null) return;

            spriteBatch.End();
            BeginCanvas(spriteBatch, SamplerState.PointClamp);
            var snapped = SnapToDevice(topLeft);
            spriteBatch.Draw(texture, snapped, null, tint ?? Color.White, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            spriteBatch.End();
            BeginCanvas(spriteBatch);
        }

        public static void DrawPixelSprite(SpriteBatch spriteBatch, Texture2D texture, Rectangle source, Vector2 center, int scale)
        {
            if (texture == null) return;

            var topLeft = SnapToDevice(new Vector2(
                center.X - source.Width * scale / 2f,
                center.Y - source.Height * scale / 2f));

            spriteBatch.End();
            BeginCanvas(spriteBatch, SamplerState.PointClamp);
            spriteBatch.Draw(texture, topLeft, source, Color.White, 0f, Vector2.Zero, (float)scale, SpriteEffects.None, 0f);
            spriteBatch.End();
            BeginCanvas(spriteBatch);
        }

        public static void DrawPixelIconFit(SpriteBatch spriteBatch, Texture2D texture, Vector2 center, float size, Color? tint = null)
        {
            if (texture == null) return;
            int nativeSize = Math.Max(texture.Width, texture.Height);
            float screenScale = Math.Max(1f, MathF.Round(size / nativeSize * RenderScale));
            float scale = screenScale / RenderScale;
            var topLeft = center - new Vector2(texture.Width, texture.Height) * (scale / 2f);

            spriteBatch.End();
            BeginCanvas(spriteBatch, SamplerState.PointClamp);
            spriteBatch.Draw(texture, SnapToDevice(topLeft), null, tint ?? Color.White, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            spriteBatch.End();
            BeginCanvas(spriteBatch);
        }

        public static void DrawPixelArt(SpriteBatch spriteBatch, Texture2D texture, RectangleF bounds)
        {
            if (texture == null) return;
            var scale = new Vector2(bounds.Width / texture.Width, bounds.Height / texture.Height);

            spriteBatch.End();
            BeginCanvas(spriteBatch, SamplerState.PointClamp);
            spriteBatch.Draw(texture, SnapToDevice(bounds.Position), null, Color.White, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            spriteBatch.End();
            BeginCanvas(spriteBatch);
        }

        public static void DrawPixelBar(SpriteBatch spriteBatch, Texture2D frame, Texture2D gauge, Vector2 topLeft, int screenScale, float ratio, Color? gaugeTint = null)
        {
            if (frame == null || gauge == null) return;
            const int GaugeInset = 3;
            float scale = screenScale / RenderScale;
            var position = SnapToDevice(topLeft);
            ratio = MathHelper.Clamp(ratio, 0f, 1f);
            int filled = (int)MathF.Round((gauge.Width - GaugeInset * 2) * ratio);
            if (ratio > 0f) filled = Math.Max(1, filled);

            spriteBatch.End();
            BeginCanvas(spriteBatch, SamplerState.PointClamp);
            spriteBatch.Draw(gauge, position, null, Color.Black * 0.6f, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            if (filled > 0)
            {
                var source = new Rectangle(0, 0, GaugeInset + filled, gauge.Height);
                spriteBatch.Draw(gauge, position, source, gaugeTint ?? Color.White, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            }
            spriteBatch.Draw(frame, position, null, Color.White, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            spriteBatch.End();
            BeginCanvas(spriteBatch);
        }

        public static void DrawIconSlot(SpriteBatch spriteBatch, Texture2D texture, Vector2 topLeft, int scale, int nativeSize = 32, Color? tint = null)
        {
            float size = nativeSize * scale;
            var slot = new RectangleF(topLeft.X - 4, topLeft.Y - 4, size + 8, size + 8);
            FillRoundedRect(spriteBatch, slot, Color.Black * 0.35f, 8f);
            if (texture != null) DrawPixelIcon(spriteBatch, texture, topLeft, size / texture.Width, tint);
        }

        public static void DrawTextWithShadow(SpriteBatch spriteBatch, SpriteFont font, string text, Vector2 position, Color color, float scale = 1f, float shadowAlpha = 0.45f)
        {
            var shadowOffset = new Vector2(2f, 2f) * scale;
            if (HasHiRes(font))
            {
                font = _hiResFont;
                scale /= RenderScale;
                position = SnapToDevice(position);
            }
            spriteBatch.DrawString(font, text, position + shadowOffset, Color.Black * shadowAlpha, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            spriteBatch.DrawString(font, text, position, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }
    }
}