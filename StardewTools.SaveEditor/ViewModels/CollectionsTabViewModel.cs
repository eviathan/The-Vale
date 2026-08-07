using CommunityToolkit.Mvvm.ComponentModel;
using StardewTools.Core.Models;
using StardewTools.SaveEditor.MapAssets;

namespace StardewTools.SaveEditor.ViewModels;

/// <summary>
/// The game's own "Collections" page - Shipping/Minerals/Cooking (have you actually shipped/
/// found/cooked each one, distinct from the Recipes tab's "do you know how") plus Fish/
/// Artifacts. Fish/Artifacts are backed by a different, array-valued dictionary type
/// (ArrayCountDictionaryEditor) with no real populated save example anywhere on this machine to
/// verify against - the shape is derived directly from the game's own serializer source and
/// Farmer.caughtFish/foundArtifact rather than guessed from a field declaration alone (see
/// PlayerEditor.FishCaught/ArtifactsFound remarks), but flagging it as the one part of this tab
/// still worth double-checking in-game if something looks off.
/// </summary>
public partial class CollectionsTabViewModel : ViewModelBase
{
    [ObservableProperty] private CollectionSectionViewModel? _shipping;
    [ObservableProperty] private CollectionSectionViewModel? _minerals;
    [ObservableProperty] private CollectionSectionViewModel? _cooking;
    [ObservableProperty] private CollectionSectionViewModel? _fish;
    [ObservableProperty] private CollectionSectionViewModel? _artifacts;

    public void Bind(SaveGameEditor save)
    {
        var shipped = save.Player.ShippedItems;
        Shipping = new CollectionSectionViewModel(CollectionCatalogs.ShippableItems, i => i.Value.ToString(), shipped.Contains, id => shipped.Add(id), shipped.Remove);

        var minerals = save.Player.MineralsFound;
        Minerals = new CollectionSectionViewModel(CollectionCatalogs.Minerals, i => i.Value.ToString(), minerals.Contains, id => minerals.Add(id), minerals.Remove);

        var cooked = save.Player.RecipesCooked;
        Cooking = new CollectionSectionViewModel(CollectionCatalogs.CookedDishes, i => i.Value.ToString(), cooked.Contains, id => cooked.Add(id), cooked.Remove);

        var fishCaught = save.Player.FishCaught;
        Fish = new CollectionSectionViewModel(CollectionCatalogs.Fish, i => $"(O){i.Value}", fishCaught.Contains, id => fishCaught.Add(id, 1, 1), fishCaught.Remove);

        var artifacts = save.Player.ArtifactsFound;
        Artifacts = new CollectionSectionViewModel(CollectionCatalogs.Artifacts, i => i.Value.ToString(), artifacts.Contains, id => artifacts.Add(id, 1, 1), artifacts.Remove);
    }
}
