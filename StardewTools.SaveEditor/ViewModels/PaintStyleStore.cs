using System.Collections.ObjectModel;
using System.Linq;

namespace StardewTools.SaveEditor.ViewModels;

/// <summary>A saved building-paint job - every real BuildingPaintColorEditor field across all 3
/// slots, so applying it reproduces the whole paint job, not just the picker-visible Hue/
/// Saturation part. See BuildingPaintColorDetailsViewModel.SaveAsStyle/ApplyStyle.</summary>
public sealed record SavedPaintStyle(string Name,
    bool Color1Default, int Color1Hue, int Color1Saturation, int Color1Lightness,
    bool Color2Default, int Color2Hue, int Color2Saturation, int Color2Lightness,
    bool Color3Default, int Color3Hue, int Color3Saturation, int Color3Lightness)
{
    public override string ToString() => Name;
}

/// <summary>Process-wide store of user-saved paint styles, persisted via AppSettings (same
/// "load once, mutate the in-memory list, re-save the whole list on every change" pattern
/// MapTabViewModel's RecentPlaceableItems already uses) - shared across every building's own
/// BuildingPaintColorDetailsViewModel instance, since a style saved from one building should be
/// reusable on any other paintable building, not scoped to the building it was saved from.
///
/// A real ObservableCollection, not a plain List behind an IReadOnlyList getter - the earlier
/// version returned the SAME list reference from every Styles access, so an ItemsControl bound to
/// it via "SavedStyles => PaintStyleStore.Styles" never refreshed after Add/Remove: Avalonia's
/// binding short-circuits re-assigning ItemsSource to a reference-equal value, and a plain List
/// has no change notification of its own for the ItemsControl to react to instead. Every
/// BuildingPaintColorDetailsViewModel now binds to this SAME collection instance directly, so its
/// own CollectionChanged events drive the UI - real user-reported bug ("the styles aren't
/// reusable" - saving one never showed up in the list).</summary>
public static class PaintStyleStore
{
    private static ObservableCollection<SavedPaintStyle>? _styles;

    public static ObservableCollection<SavedPaintStyle> Styles => _styles ??= Load();

    private static ObservableCollection<SavedPaintStyle> Load()
        => new(AppSettings.Load().PaintStyles
            .Select(r => new SavedPaintStyle(r.Name,
                r.Color1Default, r.Color1Hue, r.Color1Saturation, r.Color1Lightness,
                r.Color2Default, r.Color2Hue, r.Color2Saturation, r.Color2Lightness,
                r.Color3Default, r.Color3Hue, r.Color3Saturation, r.Color3Lightness)));

    public static void Add(SavedPaintStyle style)
    {
        Styles.Add(style);
        Persist();
    }

    public static void Remove(SavedPaintStyle style)
    {
        Styles.Remove(style);
        Persist();
    }

    private static void Persist()
    {
        var settings = AppSettings.Load();
        settings.PaintStyles = Styles
            .Select(s => new SavedPaintStyleRef
            {
                Name = s.Name,
                Color1Default = s.Color1Default, Color1Hue = s.Color1Hue, Color1Saturation = s.Color1Saturation, Color1Lightness = s.Color1Lightness,
                Color2Default = s.Color2Default, Color2Hue = s.Color2Hue, Color2Saturation = s.Color2Saturation, Color2Lightness = s.Color2Lightness,
                Color3Default = s.Color3Default, Color3Hue = s.Color3Hue, Color3Saturation = s.Color3Saturation, Color3Lightness = s.Color3Lightness,
            })
            .ToList();
        settings.Save();
    }
}
