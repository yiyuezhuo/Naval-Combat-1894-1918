using NavalCombatCore;

namespace NavalCombat.Tests;

public sealed class ScenarioPackageTests
{
    [Fact]
    public void UlsanPackageLoadsAllReferencedCatalogs()
    {
        var session = ScenarioFixture.LoadSession();

        Assert.Equal(10, session.GameState.shipLogs.Count);
        Assert.Equal(4, session.GameState.shipGroups.Count);
        Assert.Equal(ScenarioFixture.CatalogRecordCount("ShipClasses.xml"), session.GameState.shipClasses.Count);
        Assert.Equal(ScenarioFixture.CatalogRecordCount("Leaders.xml"), session.GameState.leaders.Count);
        Assert.Equal(ScenarioFixture.CatalogRecordCount("NamedShips.xml"), session.GameState.namedShips.Count);
        Assert.Equal(new DateTime(1904, 8, 13, 20, 20, 0, DateTimeKind.Utc), session.GameState.scenarioState.dateTime);
        Assert.Equal(new DateTime(1904, 8, 14, 0, 20, 0, DateTimeKind.Utc), session.GameState.scenarioState.endDateTime);
    }

    [Fact]
    public void CoreUsesUnityFreeFallbackServices()
    {
        ScenarioFixture.LoadSession();

        Assert.IsType<FallbackElevationProvider>(ElevationService.Instance.elevationProvider);
        Assert.IsType<FallbackOperationalRoutePlanner>(OperationalRoutePlannerService.Instance.routePlanner);
        Assert.Same(FallbackMaskChecker.Instance, NavalCombatServices.GetMaskCheckService());
    }

    [Fact]
    public void CoordinatesAndOneSecondMovementRetainDoublePrecision()
    {
        var session = ScenarioFixture.LoadSession();
        var ship = session.GameState.shipLogs.First();

        Assert.Equal(typeof(double), typeof(LatLon).GetField(nameof(LatLon.LatDeg))!.FieldType);
        Assert.Equal(typeof(double), typeof(LatLon).GetField(nameof(LatLon.LonDeg))!.FieldType);
        Assert.Equal(35.03874d, ship.position.LatDeg);
        Assert.NotEmpty(ship.shipClass.speedKnotsEngineRoomsLevels);
        Assert.True(ship.GetMaxSpeedKnots() > 0d);

        ship.position = new LatLon(35.03874d, 130.125992d);
        var before = ship.position.Clone();
        ship.headingDeg = 90d;
        ship.speedKnots = 10d;
        ship.StepTryMoveToNewPosition(1d);

        var movedMeters = MeasureUtils.ApproximateDistanceYards(before, ship.position) * MeasureUtils.yardToMeter;
        // The model's lightweight local-distance approximation is about 0.3%
        // low on an eastbound WGS84 step at 35 degrees north. A single-precision-backed
        // longitude would quantize this same step to roughly 5.55 metres here.
        Assert.InRange(movedMeters, 5.12d, 5.14d);
    }
}
