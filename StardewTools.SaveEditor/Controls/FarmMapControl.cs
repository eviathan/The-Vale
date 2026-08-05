using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using StardewTools.SaveEditor.ViewModels;

namespace StardewTools.SaveEditor.Controls;

/// <summary>
/// Draws the farm's placed entities (trees, grass, resource clumps, objects) as a flat,
/// abstract top-down grid - colored dots scaled to their real tile position, not the
/// game's actual terrain art. Rendering the real tile graphics would mean parsing
/// Stardew's xTile map format and decoding its (DXT-compressed) spritesheets; this covers
/// what the save file actually tracks, which is what's editable.
/// </summary>
public sealed class FarmMapControl : Control
{
    public static readonly StyledProperty<IEnumerable<MapEntitySummary>?> EntitiesProperty =
        AvaloniaProperty.Register<FarmMapControl, IEnumerable<MapEntitySummary>?>(nameof(Entities));

    public static readonly StyledProperty<MapEntitySummary?> SelectedProperty =
        AvaloniaProperty.Register<FarmMapControl, MapEntitySummary?>(nameof(Selected), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<string> SeasonProperty =
        AvaloniaProperty.Register<FarmMapControl, string>(nameof(Season), "spring");

    public IEnumerable<MapEntitySummary>? Entities
    {
        get => GetValue(EntitiesProperty);
        set => SetValue(EntitiesProperty, value);
    }

    public MapEntitySummary? Selected
    {
        get => GetValue(SelectedProperty);
        set => SetValue(SelectedProperty, value);
    }

    public string Season
    {
        get => GetValue(SeasonProperty);
        set => SetValue(SeasonProperty, value);
    }

    static FarmMapControl()
    {
        AffectsRender<FarmMapControl>(EntitiesProperty, SelectedProperty, SeasonProperty);
    }

    private static readonly IReadOnlyDictionary<string, Color> SeasonBackgrounds = new Dictionary<string, Color>
    {
        ["spring"] = Color.Parse("#C9E4B0"),
        ["summer"] = Color.Parse("#A8D98B"),
        ["fall"] = Color.Parse("#E0C185"),
        ["winter"] = Color.Parse("#DDE6EC"),
    };

    private (double MinX, double MinY, double Scale)? _lastLayout;

    public override void Render(DrawingContext context)
    {
        var background = SeasonBackgrounds.GetValueOrDefault(Season.ToLowerInvariant(), Color.Parse("#C9E4B0"));
        context.FillRectangle(new SolidColorBrush(background), new Rect(Bounds.Size));

        var entities = Entities?.ToList();
        if (entities is null || entities.Count == 0)
        {
            _lastLayout = null;
            return;
        }

        var minX = entities.Min(e => e.Position.X);
        var maxX = entities.Max(e => e.Position.X);
        var minY = entities.Min(e => e.Position.Y);
        var maxY = entities.Max(e => e.Position.Y);

        var spanX = Math.Max(1, maxX - minX + 1);
        var spanY = Math.Max(1, maxY - minY + 1);
        var scale = Math.Max(1.0, Math.Min(Bounds.Width / spanX, Bounds.Height / spanY));
        _lastLayout = (minX, minY, scale);

        var dotSize = Math.Max(1.5, scale * 0.8);

        foreach (var entity in entities)
        {
            var x = (entity.Position.X - minX) * scale;
            var y = (entity.Position.Y - minY) * scale;
            var brush = new SolidColorBrush(Color.Parse(entity.ColorHex));
            context.FillRectangle(brush, new Rect(x, y, dotSize, dotSize));
        }

        if (Selected is { } selected)
        {
            var x = (selected.Position.X - minX) * scale;
            var y = (selected.Position.Y - minY) * scale;
            var pen = new Pen(Brushes.Red, 2);
            context.DrawRectangle(pen, new Rect(x - 2, y - 2, dotSize + 4, dotSize + 4));
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (_lastLayout is not { } layout || Entities is null)
            return;

        var point = e.GetPosition(this);
        var tileX = point.X / layout.Scale + layout.MinX;
        var tileY = point.Y / layout.Scale + layout.MinY;

        var nearest = Entities
            .Select(entity => (entity, distSq: Math.Pow(entity.Position.X - tileX, 2) + Math.Pow(entity.Position.Y - tileY, 2)))
            .OrderBy(e => e.distSq)
            .FirstOrDefault();

        if (nearest.entity is not null && nearest.distSq <= 1.0)
            Selected = nearest.entity;
    }
}
