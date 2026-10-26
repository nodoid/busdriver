using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace BusDriver.Game.Core;

public enum TextAlign
{
    Left,
    Centre,
    Right
}

/// <summary>Small immediate-mode UI layer used by every screen.</summary>
public sealed class Ui
{
    private readonly Assets _assets;
    private readonly SpriteBatch _batch;

    public Ui(Assets assets, SpriteBatch batch)
    {
        _assets = assets;
        _batch = batch;
    }

    public Assets Assets => _assets;

    public SpriteBatch Batch => _batch;

    public static Color FromArgb(uint argb) =>
        new((byte)(argb >> 16), (byte)(argb >> 8), (byte)argb, (byte)(argb >> 24));

    public void Fill(Rectangle rectangle, Color colour) =>
        _batch.Draw(_assets.Pixel, rectangle, colour);

    public void Line(Vector2 a, Vector2 b, Color colour, float thickness = 1f)
    {
        var edge = b - a;
        var length = edge.Length();
        if (length < 0.01f)
        {
            return;
        }

        var angle = MathF.Atan2(edge.Y, edge.X);
        _batch.Draw(_assets.Pixel, a, null, colour, angle,
            new Vector2(0f, 0.5f), new Vector2(length, thickness), SpriteEffects.None, 0f);
    }

    public void Text(
        string font,
        string text,
        Vector2 position,
        Color colour,
        float scale = 1f,
        TextAlign align = TextAlign.Left)
    {
        var face = _assets.Font(font);
        var x = position.X;
        if (align != TextAlign.Left)
        {
            var width = face.MeasureLineWidth(text, scale);
            x = align == TextAlign.Centre ? x - width * 0.5f : x - width;
        }

        var pen = x;
        foreach (var c in text)
        {
            if (c == '\n')
            {
                continue;
            }

            if (!face.TryGetGlyph(c, out var glyph))
            {
                face.TryGetGlyph('?', out glyph);
            }

            if (glyph is not null)
            {
                _batch.Draw(
                    face.Atlas,
                    new Vector2(pen + glyph.OffsetX * scale, position.Y + glyph.OffsetY * scale),
                    glyph.Source,
                    colour,
                    0f,
                    Vector2.Zero,
                    scale,
                    SpriteEffects.None,
                    0f);
                pen += glyph.Advance * scale;
            }
        }
    }

    public void TextBlock(
        string font,
        string text,
        Vector2 position,
        Color colour,
        float scale = 1f,
        float lineSpacing = 1.12f)
    {
        var face = _assets.Font(font);
        var y = position.Y;
        foreach (var line in text.Split('\n'))
        {
            Text(font, line, new Vector2(position.X, y), colour, scale);
            y += face.LineHeight * scale * lineSpacing;
        }
    }

    public void TextCentred(
        string font,
        string text,
        float centreX,
        float y,
        Color colour,
        float scale = 1f) =>
        Text(font, text, new Vector2(centreX, y), colour, scale, TextAlign.Centre);

    public float TextWidth(string font, string text, float scale = 1f) =>
        _assets.Font(font).MeasureLineWidth(text, scale);

    public Vector2 TextSize(string font, string text, float scale = 1f) =>
        _assets.Font(font).Measure(text, scale);

    /// <summary>Nine-slice a 512px panel or button so corners stay round.</summary>
    public void NineSlice(
        Texture2D texture,
        Rectangle destination,
        Color colour,
        int sourceCorner = 48,
        int sourceInset = 8)
    {
        var w = texture.Width;
        var h = texture.Height;
        var left = Math.Clamp(sourceCorner, 1, w / 2);
        var top = Math.Clamp(sourceCorner, 1, h / 2);
        var inset = Math.Clamp(sourceInset, 0, Math.Min(left, top) - 1);
        // Keep at least a third of each dimension for the stretchable middle;
        // otherwise a button shorter than two source corners loses its face.
        var destCornerX = Math.Min(left, destination.Width / 3);
        var destCornerY = Math.Min(top, destination.Height / 3);

        // Corners.
        DrawPart(texture, new Rectangle(0, 0, left, top),
            new Rectangle(destination.X, destination.Y, destCornerX, destCornerY), colour);
        DrawPart(texture, new Rectangle(w - left, 0, left, top),
            new Rectangle(destination.Right - destCornerX, destination.Y, destCornerX, destCornerY), colour);
        DrawPart(texture, new Rectangle(0, h - top, left, top),
            new Rectangle(destination.X, destination.Bottom - destCornerY, destCornerX, destCornerY), colour);
        DrawPart(texture, new Rectangle(w - left, h - top, left, top),
            new Rectangle(destination.Right - destCornerX, destination.Bottom - destCornerY,
                destCornerX, destCornerY), colour);

        // Edges, inset past the rounded corners.
        var midSourceW = w - left * 2;
        var midSourceH = h - top * 2;
        var midDestW = destination.Width - destCornerX * 2;
        var midDestH = destination.Height - destCornerY * 2;

        if (midDestW > 0)
        {
            DrawPart(texture, new Rectangle(left, 0, midSourceW, inset),
                new Rectangle(destination.X + destCornerX, destination.Y, midDestW, Math.Min(inset, destCornerY)), colour);
            DrawPart(texture, new Rectangle(left, h - inset, midSourceW, inset),
                new Rectangle(destination.X + destCornerX, destination.Bottom - Math.Min(inset, destCornerY), midDestW,
                    Math.Min(inset, destCornerY)), colour);
        }

        if (midDestH > 0)
        {
            DrawPart(texture, new Rectangle(0, top, inset, midSourceH),
                new Rectangle(destination.X, destination.Y + destCornerY, Math.Min(inset, destCornerX), midDestH), colour);
            DrawPart(texture, new Rectangle(w - inset, top, inset, midSourceH),
                new Rectangle(destination.Right - Math.Min(inset, destCornerX), destination.Y + destCornerY,
                    Math.Min(inset, destCornerX), midDestH), colour);
        }

        if (midDestW > 0 && midDestH > 0)
        {
            DrawPart(texture, new Rectangle(left, top, midSourceW, midSourceH),
                new Rectangle(destination.X + destCornerX, destination.Y + destCornerY,
                    midDestW, midDestH), colour);
        }
    }

    public void Panel(Rectangle destination, Color? tint = null) =>
        _batch.Draw(_assets.Tex("panel"), destination, tint ?? Color.White);

    public void Button(
        Rectangle destination,
        string label,
        bool highlighted = false,
        bool pressed = false,
        string font = "body",
        Color? textColour = null)
    {
        var tint = highlighted
            ? new Color(255, 236, 170)
            : Color.White;
        if (pressed)
        {
            tint = tint * 0.86f;
        }

        // Simple stretch: a few pixels of corner distortion is invisible at
        // phone scale and it guarantees the whole button face is drawn.
        _batch.Draw(_assets.Tex(pressed ? "button_pressed" : "button"), destination, tint);
        Text(font, label,
            new Vector2(destination.Center.X, destination.Center.Y - _assets.Font(font).LineHeight * 0.5f),
            textColour ?? new Color(38, 32, 24),
            1f, TextAlign.Centre);
    }

    private void DrawPart(Texture2D texture, Rectangle source, Rectangle destination, Color colour)
    {
        if (destination.Width <= 0 || destination.Height <= 0)
        {
            return;
        }

        _batch.Draw(texture, destination, source, colour);
    }
}
