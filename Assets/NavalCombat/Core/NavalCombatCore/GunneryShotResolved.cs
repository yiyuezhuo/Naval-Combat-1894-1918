using System;

namespace NavalCombatCore
{
    public enum GunneryWeaponKind
    {
        Battery,
        RapidFiring
    }

    /// <summary>
    /// Immutable transient notification emitted after a gunnery round has been
    /// completely resolved. It is deliberately not part of serialized game state.
    /// </summary>
    public sealed record GunneryShotResolvedEvent(
        DateTime ResolutionTime,
        string ShooterId,
        string TargetId,
        string WeaponId,
        GunneryWeaponKind WeaponKind,
        double DistanceYards,
        double ShellDiameterInch,
        bool Hit,
        double ShooterLatitudeDeg,
        double ShooterLongitudeDeg,
        double TargetLatitudeDeg,
        double TargetLongitudeDeg);
}
