using CoreUtils;

using NavalCombatCore;

namespace NavalCombat.Avalonia.Resources;

/// <summary>Selects archive text at the presentation boundary without changing entity identifiers.</summary>
public static class NavalDisplayNames
{
    public static string Text(GlobalString value)
    {
        if (value is null) return null;
        var preferred = NavalCombat.Unity.NavalText.Instance.Culture switch
        {
            "ja-JP" => value.japanese,
            "zh-CN" => value.chineseSimplified,
            "zh-TW" => value.chineseTraditional,
            _ => value.english
        };
        return new[] { preferred, value.english, value.japanese, value.chineseSimplified, value.chineseTraditional }
            .FirstOrDefault(text => !string.IsNullOrWhiteSpace(text)
                && !string.Equals(text.Trim(), "none", StringComparison.OrdinalIgnoreCase));
    }

    public static string Global(GlobalString value) => Text(value) ?? NavalCombatStrings.Text("Ui.Shell.Unnamed");
    public static string Compact(GlobalString value) => Global(value);
    public static string Ship(ShipLog ship) => Global(ship?.namedShip?.name);
    public static string Group(ShipGroup group) => Global(group?.name);
}
