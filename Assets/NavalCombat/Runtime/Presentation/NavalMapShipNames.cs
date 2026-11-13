using NavalCombatCore;

namespace NavalCombat.Avalonia.Resources;

public enum ShipNamePrefixMode { None, Classic, Extended }

public static class NavalMapShipNames
{
    public static string Format(ShipLog ship, ShipNamePrefixMode mode, bool trim) =>
        Format(NavalDisplayNames.Ship(ship), ship?.shipClass, mode, trim);

    public static string Format(string name, ShipClass shipClass, ShipNamePrefixMode mode, bool trim)
    {
        var label = trim ? TrimSuffix(name) : name;
        string prefix = null;
        if (mode != ShipNamePrefixMode.None && shipClass != null)
        {
            ShipClass.acronymForShipType.TryGetValue(shipClass.type, out prefix);
            if (mode == ShipNamePrefixMode.Extended)
                prefix = shipClass.extraShipType switch
                {
                    ExtraShipType.ProtectedCruiser => "CP",
                    ExtraShipType.UnprotectedCruiser => "CU",
                    _ => prefix
                };
        }
        return string.IsNullOrEmpty(prefix) ? label : $"{prefix} {label}";
    }

    static string TrimSuffix(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        var end = name.TrimEnd().Length;
        var removed = false;
        while (end > 0 && name[end - 1] is ')' or '）')
        {
            var stack = new Stack<char>();
            var start = -1;
            for (var i = end - 1; i >= 0; i--)
            {
                var c = name[i];
                if (c is ')' or '）') stack.Push(c == ')' ? '(' : '（');
                else if (c is '(' or '（')
                {
                    if (stack.Count == 0 || stack.Pop() != c) break;
                    if (stack.Count == 0) { start = i; break; }
                }
            }
            if (start < 0) break;
            removed = true;
            end = start;
            while (end > 0 && char.IsWhiteSpace(name[end - 1])) end--;
        }
        return removed && end > 0 ? name[..end] : name;
    }
}
