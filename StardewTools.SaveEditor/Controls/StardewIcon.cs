using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using StardewTools.SaveEditor.MapAssets;

namespace StardewTools.SaveEditor.Controls;

/// <summary>A real in-game icon (see MenuChrome.IconSourceRects) rendered at a chosen on-screen
/// size, for buttons/labels that stand in for something the game itself has a real icon for
/// (Remove -> Trash, Back -> ArrowLeft, ...) instead of plain text.</summary>
public sealed class StardewIcon : Control
{
    public static readonly StyledProperty<IconKind> KindProperty =
        AvaloniaProperty.Register<StardewIcon, IconKind>(nameof(Kind));

    public static readonly StyledProperty<double> IconSizeProperty =
        AvaloniaProperty.Register<StardewIcon, double>(nameof(IconSize), 16.0);

    public IconKind Kind
    {
        get => GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    public double IconSize
    {
        get => GetValue(IconSizeProperty);
        set => SetValue(IconSizeProperty, value);
    }

    static StardewIcon()
    {
        AffectsRender<StardewIcon>(KindProperty, IconSizeProperty);
        AffectsMeasure<StardewIcon>(IconSizeProperty);
    }

    protected override Size MeasureOverride(Size availableSize) => new(IconSize, IconSize);

    public override void Render(DrawingContext context)
    {
        if (MenuChrome.TryGetIcon(Kind, out var bitmap, out var source))
        {
            // Preserve the icon's real aspect ratio (not every icon here is square - e.g. the
            // trash can is 18x26) instead of stretching it to a square IconSize x IconSize box.
            var scale = IconSize / System.Math.Max(source.Width, source.Height);
            var w = source.Width * scale;
            var h = source.Height * scale;
            var dest = new Rect((Bounds.Width - w) / 2, (Bounds.Height - h) / 2, w, h);
            context.DrawImage(bitmap, source, dest);
        }
    }
}
