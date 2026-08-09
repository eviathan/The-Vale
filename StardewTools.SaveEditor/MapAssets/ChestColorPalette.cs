using System.Collections.Generic;
using Avalonia.Media;

namespace StardewTools.SaveEditor.MapAssets;

/// <summary>The exact 21 colors the real in-game chest color wheel (decompiled
/// StardewValley.Menus.DiscreteColorPicker.getColorFromSelection) offers - a player can never
/// actually choose any other RGB value for a chest's lid, even though Chest.draw()'s underlying
/// `playerChoiceColor.Value * alpha` tint would technically accept any color. Index 0 (black) is
/// the real "no custom color" sentinel (see ChestRowViewModel.Color remarks), not a genuine paint
/// choice - matches getColorFromSelection's own default-case fallback to Color.Black.</summary>
public static class ChestColorPalette
{
    public static readonly IReadOnlyList<Color> Colors = new[]
    {
        Color.FromRgb(0, 0, 0),
        Color.FromRgb(85, 85, 255),
        Color.FromRgb(119, 191, 255),
        Color.FromRgb(0, 170, 170),
        Color.FromRgb(0, 234, 175),
        Color.FromRgb(0, 170, 0),
        Color.FromRgb(159, 236, 0),
        Color.FromRgb(255, 234, 18),
        Color.FromRgb(255, 167, 18),
        Color.FromRgb(255, 105, 18),
        Color.FromRgb(255, 0, 0),
        Color.FromRgb(135, 0, 35),
        Color.FromRgb(255, 173, 199),
        Color.FromRgb(255, 117, 195),
        Color.FromRgb(172, 0, 198),
        Color.FromRgb(143, 0, 255),
        Color.FromRgb(89, 11, 142),
        Color.FromRgb(64, 64, 64),
        Color.FromRgb(100, 100, 100),
        Color.FromRgb(200, 200, 200),
        Color.FromRgb(254, 254, 254),
    };
}
