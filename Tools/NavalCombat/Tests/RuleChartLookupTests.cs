using NavalCombatCore;

namespace NavalCombat.Tests;

public sealed class RuleChartLookupTests
{
    [Fact]
    public void SeaStateOffsetUsesDamagePointBands()
    {
        var offset = RuleChart.ResolveSeaStateOffset(2_235d, 7, out var blocked);

        Assert.Equal(-2d, offset);
        Assert.False(blocked);
    }

    [Fact]
    public void SeaStateOffsetClampsDamagePointsAboveLastBand()
    {
        var atLastBand = RuleChart.ResolveSeaStateOffset(5_000d, 10, out var blockedAtLastBand);
        var aboveLastBand = RuleChart.ResolveSeaStateOffset(50_000d, 10, out var blockedAboveLastBand);

        Assert.Equal(atLastBand, aboveLastBand);
        Assert.Equal(-6d, aboveLastBand);
        Assert.False(blockedAtLastBand);
        Assert.False(blockedAboveLastBand);
    }

    [Fact]
    public void VisibilitySizesClampToMinusOneAndFour()
    {
        static double Resolve(int observerSize, int targetSize) => RuleChart.GetVisibilityRangeYards(
            observerSize,
            targetSize,
            VisibilityDescription.ExceptionallyClear,
            DayNightLevel.Day);

        Assert.Equal(Resolve(-1, -1), Resolve(-2, -2));
        Assert.Equal(Resolve(4, 4), Resolve(5, 5));
        Assert.Equal(11_400d, Resolve(-2, -2));
        Assert.Equal(32_900d, Resolve(5, 5));
    }

    [Fact]
    public void HighExplosiveConversionClampsBelowMinimumCaliberAndPenetration()
    {
        var atMinimumCaliber = RuleChart.ResolvePenetrationHEPenetration(3d, 5d);
        var belowMinimumCaliber = RuleChart.ResolvePenetrationHEPenetration(2d, 5d);
        var atMinimumPenetration = RuleChart.ResolvePenetrationHEPenetration(3d, 0d);
        var belowMinimumPenetration = RuleChart.ResolvePenetrationHEPenetration(3d, -1d);

        Assert.Equal(atMinimumCaliber, belowMinimumCaliber);
        Assert.Equal(atMinimumPenetration, belowMinimumPenetration);
    }

    [Fact]
    public void ShellDamageFactorClampsToFirstAndLastBands()
    {
        static RuleChart.ShellDamageResult Resolve(double damageFactor) => RuleChart.ResolveShellDamageResult(
            damageFactor,
            HitPenDetType.PassThrough,
            AmmunitionType.ArmorPiercing);

        var belowMinimum = Resolve(-10d);
        var atMinimum = Resolve(5d);
        var atMaximum = Resolve(84d);
        var aboveMaximum = Resolve(1_000d);

        Assert.Equal(atMinimum.damagePoint, belowMinimum.damagePoint);
        Assert.Equal(atMinimum.damageEffectProb, belowMinimum.damageEffectProb);
        Assert.Equal(atMaximum.damagePoint, aboveMaximum.damagePoint);
        Assert.Equal(atMaximum.damageEffectProb, aboveMaximum.damageEffectProb);
    }
}
