using CoreUtils;
using NavalCombat.Application;
using NavalCombatCore;

namespace NavalCombat.Tests;

public sealed class SpanishAmericanScenarioTests
{
    [Theory]
    [InlineData("Manila Bay", "US Asiatic Squadron", "George Dewey", "Olympia")]
    [InlineData("Manila Bay", "Spanish Pacific Squadron", "Patricio Montojo y Pasarón", "Reina Cristina")]
    [InlineData("Santiago de Cuba", "US Blockading Squadron", "Winfield Scott Schley", "Brooklyn")]
    [InlineData("Santiago de Cuba", "Spanish Caribbean Squadron", "Pascual Cervera y Topete", "Infanta María Teresa")]
    public void SquadronCommanderStartsAboardHistoricalFlagship(string battle, string squadron, string commander, string flagship)
    {
        var session = ScenarioFixture.LoadSession($"SAH - Battle of {battle}.scen.xml");
        session.StartObserverBattle();
        var game = session.GameState;
        var group = Assert.Single(game.shipGroups, g => g.name.english == squadron);
        Assert.Equal(commander, group.leader?.name.english);
        var posting = Assert.Single(game.shipLogs.SelectMany(s => s.embarkedLeaders.Select(e => (ship: s, entry: e))),
            p => p.entry.leaderObjectId == group.leader.objectId);
        Assert.Equal(flagship, posting.ship.namedShip.name.english);
        Assert.Equal(group.objectId, posting.entry.commandObjectId);
        Assert.Equal(LeaderPost.Commander, posting.entry.post);
    }

    [Fact]
    public void ManilaStartsWithFiveMobileSpanishShipsAndThreeImmobileShips()
    {
        var session = ScenarioFixture.LoadSession("SAH - Battle of Manila Bay.scen.xml");
        var ships = session.GameState.shipLogs;
        Assert.Equal(14, ships.Count);
        var spanish = ships.Where(s => s.shipClass.country == Country.Spain).ToList();
        Assert.Equal(8, spanish.Count);
        Assert.All(spanish, s => Assert.Equal(0, s.speedKnots));
        Assert.Equal(new[] { "Castilla", "Don Antonio de Ulloa", "Velasco" },
            spanish.Where(s => s.GetMaxSpeedKnots() == 0).Select(s => s.namedShip.name.english).Order());
        var velasco = spanish.Single(s => s.namedShip.name.english == "Velasco");
        var battery = Assert.Single(velasco.shipClass.batteryRecords);
        Assert.Equal("15cm/26.1 M1881", battery.name.english);
        Assert.Equal(2, battery.mountLocationRecords.Sum(m => m.barrels));
        Assert.Equal("7.5cm/28.7 M1883", Assert.Single(velasco.shipClass.rapidFireBatteryRecords).name.english);

        session.StartObserverBattle();
        session.SetSimulationSpeed(SimulationSpeed.Realtime);
        session.AdvanceWallClock(1);
        Assert.Equal(BattlePhase.Running, session.Phase);
    }

    [Fact]
    public void SantiagoStartsWithBothFleetsDeployedAndTheSpanishColumnUnderway()
    {
        var session = ScenarioFixture.LoadSession("SAH - Battle of Santiago de Cuba.scen.xml");
        var game = session.GameState;
        Assert.Equal(13, game.shipLogsOnMap.Count());
        var ships = game.shipLogsOnMap.ToDictionary(s => s.namedShip.name.english);
        Assert.Equal(16, ships["Brooklyn"].GetMaxSpeedKnots());
        var column = new[] { "Infanta María Teresa", "Vizcaya", "Cristóbal Colón", "Almirante Oquendo", "Furor", "Plutón" }
            .Select(name => ships[name]).ToArray();
        Assert.Equal(ControlMode.Independent, column[0].controlMode);
        for (var i = 0; i < column.Length; i++)
        {
            var ship = column[i];
            Assert.Equal(8, ship.speedKnots);
            Assert.True(ship.GetMaxSpeedKnots() >= 8);
            Assert.Empty(ship.manualRoute);
            if (i == 0) continue;
            var previous = column[i - 1];
            Assert.Equal(ControlMode.FollowTarget, ship.controlMode);
            Assert.Equal(previous.objectId, ship.followedTargetObjectId);
            Assert.InRange(MeasureUtils.ApproximateDistanceYards(ship.position, previous.position), 397, 403);
        }
        session.StartObserverBattle();
        session.SetSimulationSpeed(SimulationSpeed.Realtime);
        session.AdvanceWallClock(1);
        Assert.Equal(BattlePhase.Running, session.Phase);
        Assert.Equal(13, game.shipLogsOnMap.Count());
    }

    [Fact]
    public void ManilaGradesFleetLossesEvenWhenSpainSinksAnAmericanCruiser()
    {
        var game = ScenarioFixture.LoadSession("SAH - Battle of Manila Bay.scen.xml").GameState;
        var ships = game.shipLogs.ToDictionary(s => s.namedShip.name.english);
        // The report supplies ship-type counts, not ship names. This distribution
        // reproduces its casualty counts and displayed 2.29 loss exchange ratio.
        void Damage(string name, double fraction)
        {
            var ship = ships[name];
            ship.damagePoint = fraction * ship.shipClass.damagePoint;
        }

        ships["Baltimore"].mapState = MapState.Destroyed;
        ships["Petrel"].mapState = MapState.Destroyed;
        Damage("Raleigh", .75);
        Damage("Boston", .095);
        Damage("Concord", .523);
        foreach (var ship in ships.Values.Where(s => s.shipClass.country == Country.Spain))
        {
            if (ship.namedShip.name.english is "Velasco" or "Don Juan de Austria")
                Damage(ship.namedShip.name.english, .93);
            else
                ship.mapState = MapState.Destroyed;
        }
        var report = VictoryStatus.Generate(game);
        var us = report.sideVictoryStatuses.Single(s => s.sideObjectId == ships["Olympia"].parentObjectId);
        var spain = report.sideVictoryStatuses.Single(s => s.sideObjectId == ships["Velasco"].parentObjectId);
        Assert.Equal(2.29, us.otherOverSelfLossRatio, 2);
        Assert.Equal(.44, spain.otherOverSelfLossRatio, 2);
        Assert.Equal(VictoryLevel.MajorVictory, us.victoryLevel);
        Assert.Equal(VictoryLevel.MajorDefeat, spain.victoryLevel);
    }
}
