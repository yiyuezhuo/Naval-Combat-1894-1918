using NavalCombat.Application;
using NavalCombatCore;
using System.Numerics;

namespace NavalCombat.Avalonia.Services;

public enum GunneryVisualMode
{
    None,
    Projectiles,
    ProjectilesWithTrails
}

public sealed class GunneryShellVisual
{
    public GunneryShellVisual(GunneryShotResolvedEvent shot, double durationWallSeconds)
    {
        Shot = shot ?? throw new ArgumentNullException(nameof(shot));
        DurationWallSeconds = durationWallSeconds;
    }

    public GunneryShotResolvedEvent Shot { get; }
    public double DurationWallSeconds { get; }
    public double ElapsedWallSeconds { get; internal set; }
    public const double BridgeFlashSeconds = .25;
    public const double MuzzleSmokeSeconds = .9;
    public const double RecoilSeconds = .55;
    internal int BarrelSequence { get; set; }
    internal (Vector3 Offset,Vector3 Direction)? Muzzle { get; set; }
    public double RecoilFraction => ElapsedWallSeconds<.08 ? Math.Clamp(ElapsedWallSeconds/.08,0,1)
        : Math.Clamp(1-(ElapsedWallSeconds-.08)/(RecoilSeconds-.08),0,1);
    public const double BridgeImpactSeconds = 1.2;
    public double BridgeFlightSeconds => Math.Max(.55, DurationWallSeconds * .8);
    public double BridgeLifetimeSeconds => Math.Max(DurationWallSeconds, BridgeFlightSeconds) + BridgeImpactSeconds;
    public double BridgeFlightProgress => Math.Clamp(ElapsedWallSeconds / BridgeFlightSeconds, 0, 1);
    public double BridgeTravelProgress(double distanceMeters)
    {
        var t=BridgeFlightProgress;
        var initial=Math.Clamp(VisualLaunchSpeed*BridgeFlightSeconds/Math.Max(1,distanceMeters),0,1);
        return t*(initial+(1-initial)*t);
    }
    const double VisualLaunchSpeed=760;
    public double BridgeImpactProgress => Math.Clamp((ElapsedWallSeconds - BridgeFlightSeconds) / BridgeImpactSeconds, 0, 1);
    public double Progress => Math.Clamp(
        ElapsedWallSeconds / Math.Max(DurationWallSeconds, double.Epsilon),
        0d,
        1d);
}

/// <summary>
/// Retimes already-resolved model shots into short, purely visual replays.
/// All access is expected to remain on the Avalonia UI thread.
/// </summary>
public sealed class GunneryVisualTracker
{
    public const int MaximumActiveShells = 5_000;
    public const double VisualShellSpeedMetersPerSecond = 760d;
    public const double MinimumDurationWallSeconds = 0.3d;
    public const double MaximumDurationWallSeconds = 3d;
    const double MetersPerYard = 0.9144d;

    readonly List<GunneryShellVisual> activeShells = [];
    readonly List<GunneryShellVisual> bridgeShells = [];
    readonly Dictionary<(string Ship,string Mount),int> barrelSequences=[];
    public IReadOnlyList<GunneryShellVisual> BridgeShells => bridgeShells;

    public IReadOnlyList<GunneryShellVisual> ActiveShells => activeShells;
    public bool HasActiveShells => bridgeShells.Count > 0;

    public bool Enqueue(
        IReadOnlyList<GunneryShotResolvedEvent> shots,
        SimulationSpeed simulationSpeed)
    {
        if (shots == null || shots.Count == 0)
            return false;

        var speedMultiplier = Math.Max(1d, (int)simulationSpeed);
        foreach (var shot in shots)
        {
            if (shot == null || !HasFinitePosition(shot))
                continue;

            var distanceYards = double.IsFinite(shot.DistanceYards)
                ? Math.Max(0d, shot.DistanceYards)
                : 0d;
            var physicalFlightSeconds = distanceYards * MetersPerYard
                / VisualShellSpeedMetersPerSecond;
            var duration = Math.Clamp(
                physicalFlightSeconds / speedMultiplier,
                MinimumDurationWallSeconds,
                MaximumDurationWallSeconds);
            var visual = new GunneryShellVisual(shot, duration);
            var key=(shot.ShooterId,shot.WeaponId);
            barrelSequences.TryGetValue(key,out var sequence);
            visual.BarrelSequence=sequence;
            barrelSequences[key]=(sequence+1)%2520; // divisible by typical barrel counts; bounded presentation state
            activeShells.Add(visual);
            bridgeShells.Add(visual);
        }

        var overflow = activeShells.Count - MaximumActiveShells;
        if (overflow > 0)
            activeShells.RemoveRange(0, overflow);
        if (bridgeShells.Count > MaximumActiveShells)
        {
            bridgeShells.RemoveRange(0, bridgeShells.Count - MaximumActiveShells);
            var retained = bridgeShells.ToHashSet();
            activeShells.RemoveAll(shell => !retained.Contains(shell));
        }
        return true;
    }

    public bool Advance(double elapsedWallSeconds)
    {
        if (bridgeShells.Count == 0
            || !double.IsFinite(elapsedWallSeconds)
            || elapsedWallSeconds <= 0d)
            return false;

        foreach (var shell in bridgeShells)
            shell.ElapsedWallSeconds += elapsedWallSeconds;
        activeShells.RemoveAll(shell =>
            shell.ElapsedWallSeconds >= shell.DurationWallSeconds);
        bridgeShells.RemoveAll(shell => shell.ElapsedWallSeconds >= shell.BridgeLifetimeSeconds);
        return true;
    }

    public bool Reset()
    {
        barrelSequences.Clear();
        if (bridgeShells.Count == 0)
            return false;
        activeShells.Clear();
        bridgeShells.Clear();
        return true;
    }

    static bool HasFinitePosition(GunneryShotResolvedEvent shot) =>
        double.IsFinite(shot.ShooterLatitudeDeg)
        && double.IsFinite(shot.ShooterLongitudeDeg)
        && double.IsFinite(shot.TargetLatitudeDeg)
        && double.IsFinite(shot.TargetLongitudeDeg);
}
