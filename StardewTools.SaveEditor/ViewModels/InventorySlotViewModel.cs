using CommunityToolkit.Mvvm.ComponentModel;

namespace StardewTools.SaveEditor.ViewModels;

/// <summary>
/// One cell in the 12-wide, up-to-3-row inventory grid (matches the real game's own
/// InventoryMenu layout: capacity/rows columns, always drawn as a fixed grid with locked slots
/// beyond the player's actual backpack size shown as inaccessible rather than hidden). Index is
/// the real save slot position (see ItemListEditor.GetSlot) - the same index the game itself
/// uses for Farmer.Items[Index]. One instance per index is created once and reused for the
/// tab's lifetime; only IsLocked/Item change as the backpack size or contents change, so the
/// grid doesn't need to rebuild itself (and lose selection) on every edit.
/// </summary>
public partial class InventorySlotViewModel : ViewModelBase
{
    public int Index { get; }

    [ObservableProperty] private bool _isLocked;
    [ObservableProperty] private bool _isSelected;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private ItemRowViewModel? _item;

    public bool IsEmpty => Item is null;

    public InventorySlotViewModel(int index)
    {
        Index = index;
    }
}
