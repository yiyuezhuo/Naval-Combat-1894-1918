using System;
using System.Collections.Generic;
using System.Linq;

namespace NavalCombatCore;

[Flags]
public enum TorpedoFireBlock
{
    None = 0, MountUnavailable = 1, Empty = 2, Doctrine = 4, Unsafe = 8,
    NoSolution = 16, Range = 32, Arc = 64, Surprise = 128,
    Salvo = 256, Score = 512, Reserve = 1024
}

/// <summary>Read-only evaluation shared by launch execution and inspection.</summary>
public sealed record TorpedoFireEvaluation(
    double SpeedKnots, double RangeYards, double CurrentDistanceYards,
    bool HasSolution, double Azimuth, double InterceptionYards, double ArrivalSeconds,
    double TimeFactor, double RangeFactor, double Score, double Threshold, TorpedoFireBlock Blocks)
{
    public bool CanFire => Blocks == TorpedoFireBlock.None;

    public static IReadOnlyList<TorpedoFireEvaluation> Evaluate(
        ShipLog shooter, ShipLog target, TorpedoMountStatusRecord mount, TorpedoAttackContext context)
    {
        var sector = shooter.torpedoSectorStatus;
        var remaining = sector.GetAmmunitionMagazinePlusLoaded();
        var distance = MeasureStats.Approximation.CalculateDistanceKmAndBearingDeg(
            shooter.position.LatDeg, shooter.position.LonDeg, target.position.LatDeg, target.position.LonDeg).Item1
            * 1000 * MeasureUtils.meterToYard;
        var common = TorpedoFireBlock.None;
        if (!mount.IsOperational()) common |= TorpedoFireBlock.MountUnavailable;
        if (mount.currentLoad <= 0 || remaining <= 0) common |= TorpedoFireBlock.Empty;
        if (!shooter.doctrine.GetMaximumFiringDistanceYardsForTorpedo().IsGreaterThanIfSpecified(distance)) common |= TorpedoFireBlock.Doctrine;
        if (!sector.CanLaunch()) common |= TorpedoFireBlock.Salvo;
        if (shooter.IsSurpriseRestricted()) common |= TorpedoFireBlock.Surprise;
        var results = new List<TorpedoFireEvaluation>();
        foreach (var setting in shooter.shipClass.torpedoSector.torpedoSettings)
        {
            var supplementary = context.GetOrCalculateFireComplexSupplementary(shooter, target, setting.speedKnots);
            var solution = supplementary.interceptionPointSolverResult;
            var blocks = common;
            if (!supplementary.isSafeToFire) blocks |= TorpedoFireBlock.Unsafe;
            else if (!solution.success) blocks |= TorpedoFireBlock.NoSolution;
            var score = 0d;
            var timeFactor = 0d;
            var rangeFactor = 0d;
            if (solution.success)
            {
                if (solution.distanceYards >= setting.rangeYards) blocks |= TorpedoFireBlock.Range;
                if (!mount.MountRecord.IsInArc(MeasureUtils.NormalizeAngle(solution.azimuth - shooter.headingDeg))) blocks |= TorpedoFireBlock.Arc;
                timeFactor = TorpedoFirePolicy.TimeFactor(solution.arrivalSeconds);
                rangeFactor = setting.rangeYards > 0 ? TorpedoFirePolicy.RangeFactor(solution.distanceYards / setting.rangeYards) : 0;
                score = TorpedoFirePolicy.Score(solution.distanceYards, setting.rangeYards, setting.speedKnots);
                if (!TorpedoFirePolicy.ShouldFire(score, remaining))
                    blocks |= remaining <= TorpedoFirePolicy.ReserveCount ? TorpedoFireBlock.Reserve : TorpedoFireBlock.Score;
            }
            results.Add(new(setting.speedKnots, setting.rangeYards, distance, solution.success,
                solution.azimuth, solution.distanceYards, solution.arrivalSeconds,
                timeFactor, rangeFactor, score, TorpedoFirePolicy.Threshold(remaining), blocks));
        }
        return results;
    }

    public static TorpedoFireEvaluation BestEligible(IEnumerable<TorpedoFireEvaluation> evaluations)
        => evaluations.Where(e => e.CanFire).MaxBy(e => e.Score);
}
