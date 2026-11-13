using NavalCombat.Application;
using NavalCombat.Avalonia.Resources;
using NavalCombatCore;

namespace NavalCombat.Avalonia.ViewModels;

public sealed record TorpedoMountOption(string MountId, int MountNumber, int Loaded,
    IReadOnlyList<(double Start, double Coverage)> Arcs, TorpedoFireEvaluation Evaluation);

public sealed record TorpedoTargetSnapshot(string Id, string Name, bool Assigned,
    double X, double Y, double Heading, IReadOnlyList<TorpedoMountOption> Options)
{
    public TorpedoMountOption Best => Options.Where(o => o.Evaluation.CanFire).NavalMaxBy(o => o.Evaluation.Score)
        ?? Options.NavalMaxBy(o => o.Evaluation.Score);
}

public sealed record TorpedoAnalysisSnapshot(string ShooterName, DateTime Time, double Heading,
    int Remaining, int SalvoShots, double WaitSeconds, IReadOnlyList<TorpedoTargetSnapshot> Targets)
{
    public static TorpedoAnalysisSnapshot Create(BattleSession session, ShipLog shooter)
    {
        var projection = new MeasureUtils.LocalProjection(shooter.position.LatDeg, shooter.position.LonDeg);
        var targets = new List<TorpedoTargetSnapshot>();
        using var gunnery = GunneryFireContext.Begin();
        using var context = TorpedoAttackContext.Begin();
        foreach (var target in session.GameState.shipLogsOnMap.Where(t => session.GetSideId(t) != session.GetSideId(shooter)))
        {
            var options = new List<TorpedoMountOption>();
            for (var i = 0; i < shooter.torpedoSectorStatus.mountStatus.Count; i++)
            {
                var mount = shooter.torpedoSectorStatus.mountStatus[i];
                var arcs = mount.MountRecord.mountArcs.Select(a => (a.startDeg, a.CoverageDeg)).ToArray();
                foreach (var evaluation in TorpedoFireEvaluation.Evaluate(shooter, target, mount, context))
                    options.Add(new(mount.objectId, i + 1, mount.currentLoad, arcs, evaluation));
            }
            targets.Add(new(target.objectId, NavalDisplayNames.Ship(target),
                shooter.torpedoSectorStatus.mountStatus.Any(m => m.firingTargetObjectId == target.objectId),
                projection.LongitudeToX(target.position.LonDeg), projection.LatitudeToY(target.position.LatDeg),
                target.headingDeg, options.ToArray()));
        }
        return new(NavalDisplayNames.Ship(shooter), session.GameState.scenarioState.dateTime,
            shooter.headingDeg, shooter.torpedoSectorStatus.GetAmmunitionMagazinePlusLoaded(),
            shooter.torpedoSectorStatus.salvoShots, shooter.torpedoSectorStatus.salvoReassessmentSeconds,
            targets.OrderByDescending(t => t.Best?.Evaluation.Score ?? 0).ToArray());
    }
}
