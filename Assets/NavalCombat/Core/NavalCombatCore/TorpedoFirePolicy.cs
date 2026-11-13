using System;

namespace NavalCombatCore;

/// <summary>Opportunity heuristic, not a calibrated probability of hitting.</summary>
public static class TorpedoFirePolicy
{
    public const int SalvoLimit = 2;
    public const double ReassessmentSeconds = 90;
    public const int ReserveCount = 2;
    public const double NormalThreshold = 0.4;
    public const double ReserveThreshold = 0.7;

    public static double TimeFactor(double seconds) => 120 / (120 + seconds);
    public static double RangeFactor(double fraction) => fraction <= 0.6 ? 1
        : fraction <= 0.8 ? 1 - (fraction - 0.6) * 2.5
        : Math.Max(0, 0.5 * (0.95 - fraction) / 0.15);
    public static double Threshold(int remaining) => remaining <= ReserveCount ? ReserveThreshold : NormalThreshold;

    public static double Score(double interceptionYards, double rangeYards, double speedKnots)
    {
        if (!double.IsFinite(interceptionYards) || !double.IsFinite(rangeYards)
            || !double.IsFinite(speedKnots) || interceptionYards < 0 || rangeYards <= 0 || speedKnots <= 0)
            return 0;
        var fraction = interceptionYards / rangeYards;
        // Full credit through 60%; half credit at 80%; no launch at 95%.
        var margin = RangeFactor(fraction);
        var arrivalSeconds = interceptionYards / (speedKnots * MeasureUtils.navalMileToYard / 3600);
        return margin * TimeFactor(arrivalSeconds);
    }

    public static bool ShouldFire(double score, int remaining)
        => remaining > 0 && score >= Threshold(remaining);
}
