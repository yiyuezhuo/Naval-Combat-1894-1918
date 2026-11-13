using NavalCombatCore;

namespace NavalCombat.Tests;

public sealed class TorpedoFirePolicyTests
{
    [Fact]
    public void ReadOnlyEvaluationMatchesTheLaunchedSettingAndReportsAllBlocks()
    {
        var (game, ship, target) = Setup(200);
        var mount = ship.torpedoSectorStatus.mountStatus[0];
        using var context = TorpedoAttackContext.Begin();
        var ammunition = ship.torpedoSectorStatus.GetAmmunitionMagazinePlusLoaded();
        var evaluation = TorpedoFireEvaluation.BestEligible(TorpedoFireEvaluation.Evaluate(ship, target, mount, context));
        Assert.NotNull(evaluation);
        Assert.Equal(evaluation.Score, evaluation.TimeFactor * evaluation.RangeFactor, 10);
        Assert.Empty(game.launchedTorpedos);
        Assert.Equal(ammunition, ship.torpedoSectorStatus.GetAmmunitionMagazinePlusLoaded());
        Assert.Equal(0, ship.torpedoSectorStatus.salvoShots);
        mount.Step(0);
        var launched = Assert.Single(game.launchedTorpedos);
        Assert.Equal(evaluation.SpeedKnots, launched.speedKnots);
        Assert.Equal(evaluation.Azimuth, launched.headingDeg);
        mount.currentLoad = 0;
        mount.status = MountStatus.Disabled;
        ship.torpedoSectorStatus.salvoShots = 2;
        var blocked = Assert.Single(TorpedoFireEvaluation.Evaluate(ship, target, mount, context));
        Assert.True(blocked.Blocks.HasFlag(TorpedoFireBlock.Empty));
        Assert.True(blocked.Blocks.HasFlag(TorpedoFireBlock.MountUnavailable));
        Assert.True(blocked.Blocks.HasFlag(TorpedoFireBlock.Salvo));
        Assert.Null(TorpedoFireEvaluation.BestEligible([blocked]));
    }

    [Fact]
    public void LongTravelAndSmallRangeMarginReduceOpportunity()
    {
        Assert.True(TorpedoFirePolicy.Score(1500, 5000, 40) > TorpedoFirePolicy.Score(1500, 5000, 20));
        Assert.True(TorpedoFirePolicy.Score(1500, 5000, 40) > TorpedoFirePolicy.Score(1500, 1700, 40));
        Assert.False(TorpedoFirePolicy.ShouldFire(TorpedoFirePolicy.Score(940, 1000, 40), 10));
        Assert.Equal(0, TorpedoFirePolicy.Score(950, 1000, 40));
        Assert.Equal(0, TorpedoFirePolicy.Score(100, 1000, 0));
    }

    static (NavalGameState game, ShipLog ship, ShipLog target) Setup(double distanceYards)
    {
        var game = ScenarioFixture.LoadSession().GameState;
        var ship = game.shipLogs.First(s => s.torpedoSectorStatus.mountStatus.Count >= 2);
        var target = game.shipLogs.First(s => s != ship);
        ship.position.LatDeg = 0;
        ship.position.LonDeg = 0;
        ship.headingDeg = 0;
        target.position.LatDeg = distanceYards * 0.9144 / 111000;
        target.position.LonDeg = 0;
        target.speedKnots = 0;
        ship.doctrine.maximumFiringDistanceYardsForTorpedo.isInherited = false;
        ship.doctrine.maximumFiringDistanceYardsForTorpedo.value = new();
        ship.shipClass.torpedoSector.torpedoSettings = [new() { rangeYards = 5000, speedKnots = 40 }];
        ship.torpedoSectorStatus.ammunition = 0;
        foreach (var mount in ship.torpedoSectorStatus.mountStatus)
        {
            mount.status = MountStatus.Operational;
            mount.currentLoad = mount.barrels = 4;
            mount.MountRecord.mountArcs = [new() { startDeg = 0, CoverageDeg = 360 }];
            mount.SetFiringTarget(target);
        }
        return (game, ship, target);
    }

    static void Step(ShipLog ship, double seconds)
    {
        using var context = TorpedoAttackContext.Begin();
        ship.torpedoSectorStatus.Step(seconds);
    }

    [Fact]
    public void NearMaximumRangeDoesNotSpendAmmunitionAcrossRepeatedSteps()
    {
        var (game, ship, _) = Setup(4700);
        var initial = ship.torpedoSectorStatus.GetAmmunitionMagazinePlusLoaded();
        for (var i = 0; i < 200; i++) Step(ship, 1);
        Assert.Empty(game.launchedTorpedos);
        Assert.Equal(initial, ship.torpedoSectorStatus.GetAmmunitionMagazinePlusLoaded());
    }

    [Fact]
    public void AllMountsShareSalvoBudgetAndWaitBeforeReassessment()
    {
        var (game, ship, target) = Setup(200);
        Step(ship, 1);
        Assert.Equal(2, game.launchedTorpedos.Count);
        // Retargeting must not reset the ship's firing budget.
        foreach (var mount in ship.torpedoSectorStatus.mountStatus)
        {
            mount.SetFiringTarget(null);
            mount.SetFiringTarget(target);
        }
        for (var i = 0; i < 89; i++) Step(ship, 1);
        Assert.Equal(2, game.launchedTorpedos.Count);
        Step(ship, 1);
        Assert.Equal(4, game.launchedTorpedos.Count);
    }

    [Fact]
    public void LastTwoTorpedoesWaitForBetterOpportunity()
    {
        var (game, ship, target) = Setup(1500);
        foreach (var mount in ship.torpedoSectorStatus.mountStatus) mount.currentLoad = 0;
        ship.torpedoSectorStatus.mountStatus[0].currentLoad = 3;
        Step(ship, 1);
        Assert.Single(game.launchedTorpedos);
        Step(ship, 100);
        Assert.Single(game.launchedTorpedos);
        Assert.Equal(2, ship.torpedoSectorStatus.GetAmmunitionMagazinePlusLoaded());
        target.position.LatDeg /= 10;
        Step(ship, 1);
        Assert.Equal(2, game.launchedTorpedos.Count);
    }

    [Fact]
    public void SettingSelectionPrefersRangeMarginOverFirstEqualDistanceSolution()
    {
        var (game, ship, _) = Setup(1500);
        ship.shipClass.torpedoSector.torpedoSettings = [
            new() { rangeYards = 1600, speedKnots = 45 },
            new() { rangeYards = 5000, speedKnots = 40 }];
        Step(ship, 1);
        Assert.NotEmpty(game.launchedTorpedos);
        Assert.All(game.launchedTorpedos, torpedo => Assert.Equal(40, torpedo.speedKnots));
    }

    [Fact]
    public void DisabledMountCannotUseAnOldTargetToFire()
    {
        var (game, ship, _) = Setup(200);
        foreach (var mount in ship.torpedoSectorStatus.mountStatus) mount.status = MountStatus.Disabled;
        Step(ship, 1);
        Assert.Empty(game.launchedTorpedos);
    }
}
