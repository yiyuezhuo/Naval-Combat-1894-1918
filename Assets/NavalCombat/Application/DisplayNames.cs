using CoreUtils;
using NavalCombatCore;

namespace NavalCombat.Application;

public static class DisplayNames
{
    public static string Global(GlobalString value)
    {
        if (value == null)
            return "Unnamed";
        var english = First(value.english);
        return english ?? First(value.chineseSimplified, value.chineseTraditional, value.japanese) ?? "Unnamed";
    }

    public static string Compact(GlobalString value) =>
        First(value?.english, value?.chineseSimplified, value?.chineseTraditional, value?.japanese) ?? "Unnamed";

    public static string Ship(ShipLog ship) => Global(ship?.namedShip?.name);
    public static string Group(ShipGroup group) => Global(group?.name);

    static string First(params string[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
}
