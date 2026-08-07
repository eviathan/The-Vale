using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using StardewTools.SaveEditor.MapAssets;

namespace StardewTools.SaveEditor.Controls;

/// <summary>
/// A ContentControl chromed with the game's own tan/wood dialog border (Maps/MenuTiles.png,
/// the same source rect and 9-slice composite IClickableMenu.drawTextureBox uses for tooltips/
/// crafting/dialog windows - see MenuChrome remarks) instead of a plain Border. Renders the 9
/// pieces directly via DrawImage, the same technique FarmMapControl already uses for its own
/// sprite rendering - corners at a fixed on-screen size (CornerRenderSize), edges stretched
/// along one axis to fill the gap between corners, center stretched both axes to fill the rest.
/// Falls back to drawing nothing extra (just the content) if the bundled asset is somehow
/// missing, rather than throwing - a missing texture shouldn't take the whole tab down.
/// </summary>
public sealed class StardewPanel : ContentControl
{
    public static readonly StyledProperty<double> CornerRenderSizeProperty =
        AvaloniaProperty.Register<StardewPanel, double>(nameof(CornerRenderSize), 16.0);

    public double CornerRenderSize
    {
        get => GetValue(CornerRenderSizeProperty);
        set => SetValue(CornerRenderSizeProperty, value);
    }

    static StardewPanel()
    {
        AffectsRender<StardewPanel>(CornerRenderSizeProperty, BoundsProperty);
        PaddingProperty.OverrideDefaultValue<StardewPanel>(new Thickness(20));
    }

    public override void Render(DrawingContext context)
    {
        if (MenuChrome.MenuTiles is { } bitmap)
        {
            var c = CornerRenderSize;
            var w = Bounds.Width;
            var h = Bounds.Height;
            var src = MenuChrome.BorderSourceRect;
            var s = MenuChrome.BorderCornerSize;

            var srcTopLeft = new Rect(src.X, src.Y, s, s);
            var srcTop = new Rect(src.X + s, src.Y, s, s);
            var srcTopRight = new Rect(src.X + 2 * s, src.Y, s, s);
            var srcLeft = new Rect(src.X, src.Y + s, s, s);
            var srcCenter = new Rect(src.X + s, src.Y + s, s, s);
            var srcRight = new Rect(src.X + 2 * s, src.Y + s, s, s);
            var srcBottomLeft = new Rect(src.X, src.Y + 2 * s, s, s);
            var srcBottom = new Rect(src.X + s, src.Y + 2 * s, s, s);
            var srcBottomRight = new Rect(src.X + 2 * s, src.Y + 2 * s, s, s);

            context.DrawImage(bitmap, srcTopLeft, new Rect(0, 0, c, c));
            context.DrawImage(bitmap, srcTopRight, new Rect(w - c, 0, c, c));
            context.DrawImage(bitmap, srcBottomLeft, new Rect(0, h - c, c, c));
            context.DrawImage(bitmap, srcBottomRight, new Rect(w - c, h - c, c, c));

            if (w > 2 * c)
            {
                context.DrawImage(bitmap, srcTop, new Rect(c, 0, w - 2 * c, c));
                context.DrawImage(bitmap, srcBottom, new Rect(c, h - c, w - 2 * c, c));
            }

            if (h > 2 * c)
            {
                context.DrawImage(bitmap, srcLeft, new Rect(0, c, c, h - 2 * c));
                context.DrawImage(bitmap, srcRight, new Rect(w - c, c, c, h - 2 * c));
            }

            if (w > 2 * c && h > 2 * c)
                context.DrawImage(bitmap, srcCenter, new Rect(c, c, w - 2 * c, h - 2 * c));
        }

        base.Render(context);
    }
}
