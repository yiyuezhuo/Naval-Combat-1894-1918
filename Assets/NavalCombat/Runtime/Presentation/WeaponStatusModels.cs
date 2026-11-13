namespace NavalCombat.Avalonia.ViewModels;
public sealed record WeaponFiringGroupViewModel(
    string TargetName,
    string Allocation,
    string Timing,
    string Ammunition = "",
    string FireControlState = "");

public sealed record AmmunitionStockViewModel(
    string Text,
    bool IsInUse,
    bool IsDepleted,
    bool HasLeadingSeparator)
{
    public double Opacity => IsDepleted ? 0.45d : 1d;
}

public sealed class BatteryWeaponStatusViewModel(
    string name,
    string caliber,
    string barrels,
    IEnumerable<AmmunitionStockViewModel> ammunition,
    IEnumerable<WeaponFiringGroupViewModel> firingGroups)
{
    public string Name { get; } = name;
    public string Caliber { get; } = caliber;
    public string Barrels { get; } = barrels;
    public IReadOnlyList<AmmunitionStockViewModel> Ammunition { get; } = ammunition.ToList();
    public IReadOnlyList<WeaponFiringGroupViewModel> FiringGroups { get; } = firingGroups.ToList();
}

public sealed class RapidFiringWeaponStatusViewModel(
    string name,
    string label,
    string barrels,
    string ammunition,
    IEnumerable<WeaponFiringGroupViewModel> firingGroups)
{
    public string Name { get; } = name;
    public string Label { get; } = label;
    public string Barrels { get; } = barrels;
    public string Ammunition { get; } = ammunition;
    public IReadOnlyList<WeaponFiringGroupViewModel> FiringGroups { get; } = firingGroups.ToList();
}

public sealed class TorpedoWeaponStatusViewModel(
    string name,
    string tubes,
    string ammunition,
    string reloads,
    IEnumerable<WeaponFiringGroupViewModel> firingGroups)
{
    public string Name { get; } = name;
    public string Tubes { get; } = tubes;
    public string Ammunition { get; } = ammunition;
    public string Reloads { get; } = reloads;
    public IReadOnlyList<WeaponFiringGroupViewModel> FiringGroups { get; } = firingGroups.ToList();
}

public sealed record DamageAlertViewModel(string Text);

