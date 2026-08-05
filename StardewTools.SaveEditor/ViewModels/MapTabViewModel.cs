using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StardewTools.Core.Models;

namespace StardewTools.SaveEditor.ViewModels;

public partial class MapTabViewModel : ViewModelBase
{
    private FarmMapEditor? _map;

    [ObservableProperty] private MapEntitySummary? _selected;
    [ObservableProperty] private string _season = "";
    [ObservableProperty] private string _summary = "";

    public ObservableCollection<MapEntitySummary> Entities { get; } = new();

    public void Bind(SaveGameEditor save)
    {
        _map = save.Map;
        Season = save.Season;
        Selected = null;

        Entities.Clear();
        foreach (var tree in _map.Trees) Entities.Add(MapEntitySummary.FromTree(tree));
        foreach (var grass in _map.Grass) Entities.Add(MapEntitySummary.FromGrass(grass));
        foreach (var clump in _map.ResourceClumps) Entities.Add(MapEntitySummary.FromClump(clump));
        foreach (var obj in _map.Objects) Entities.Add(MapEntitySummary.FromObject(obj));

        var unmodeled = _map.UnmodeledTerrainFeatures;
        Summary = unmodeled.Count == 0
            ? $"{Entities.Count} placed entities."
            : $"{Entities.Count} placed entities. Also {unmodeled.Count} tile(s) of unmodeled terrain " +
              $"feature type(s) not shown: {string.Join(", ", unmodeled.Select(u => u.Type).Distinct())}.";
    }

    [RelayCommand]
    private void RemoveSelected()
    {
        if (_map is null || Selected is not { } entity)
            return;

        switch (entity.Source)
        {
            case TreeEditor tree: _map.Remove(tree); break;
            case GrassEditor grass: _map.Remove(grass); break;
            case ResourceClumpEditor clump: _map.Remove(clump); break;
            case PlacedObjectEditor obj: _map.Remove(obj); break;
        }

        Entities.Remove(entity);
        Selected = null;
    }
}
