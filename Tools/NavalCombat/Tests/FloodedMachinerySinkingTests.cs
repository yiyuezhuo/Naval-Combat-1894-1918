using NavalCombatCore;

namespace NavalCombat.Tests;

public sealed class FloodedMachinerySinkingTests
{
    [Fact]
    public void DamageControlRatingAppliesDamageHitsAndDoesNotGoBelowZero()
    {
        var session = ScenarioFixture.LoadSession();
        var ship = session.GameState.shipLogs.First();

        ship.shipClass.damageControlRatingUnmodified = 3;
        ship.damageControlRatingHits = 2;
        Assert.Equal(1, ship.GetDamageControlRating());

        ship.damageControlRatingHits = 5;
        Assert.Equal(0, ship.GetDamageControlRating());
    }

    [Fact]
    public void SevereFloodingOnlyProcessesShipSubjects()
    {
        var severeFlooding = new SevereFloodingState();
        severeFlooding.DoOnClockTick(null, 1d);

        var session = ScenarioFixture.LoadSession();
        var ship = session.GameState.shipLogs.First();
        ship.operationalState = ShipOperationalState.Operational;

        var previousRandom = RandomUtils.rand;
        try
        {
            RandomUtils.rand = new FixedRandom(0.99d);
            severeFlooding.DoOnClockTick(ship, 1d);
        }
        finally
        {
            RandomUtils.rand = previousRandom;
        }

        Assert.Equal(ShipOperationalState.FloodingObstruction, ship.operationalState);
    }

    [Theory]
    [InlineData(3, 0.00d, false)]
    [InlineData(4, 0.74d, true)]
    [InlineData(4, 0.77d, false)]
    public void EightyPercentFloodingUsesSeventyFivePercentChancePerTurn(
        int floodedMachinerySpaces,
        double randomRoll,
        bool expectedSunk)
    {
        var session = ScenarioFixture.LoadSession();
        var ship = session.GameState.shipLogs.First();

        // Five machinery spaces: three engine rooms and two boiler rooms.
        ship.shipClass.speedKnotsEngineRoomsLevels = new List<double> { 20d, 15d, 10d };
        ship.shipClass.speedKnotsBoilerRooms = new List<double> { 20d, 10d };
        ship.dynamicStatus.engineRoomFloodingHits = Math.Min(3, floodedMachinerySpaces);
        ship.dynamicStatus.boilerRoomFloodingHits = Math.Max(0, floodedMachinerySpaces - 3);
        ship.damagePoint = 0d;
        ship.pendingDamagePoint = 0d;
        ship.mapState = MapState.Deployed;

        var previousRandom = RandomUtils.rand;
        try
        {
            RandomUtils.rand = new FixedRandom(randomRoll);
            ship.StepDamageResolution(120d);
        }
        finally
        {
            RandomUtils.rand = previousRandom;
        }

        Assert.Equal(expectedSunk ? MapState.Destroyed : MapState.Deployed, ship.mapState);
    }

    sealed class FixedRandom(double value) : Random
    {
        public override double NextDouble() => value;
    }
}
