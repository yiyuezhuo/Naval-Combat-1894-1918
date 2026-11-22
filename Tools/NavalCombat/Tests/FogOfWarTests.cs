using NavalCombatCore;

namespace NavalCombat.Tests;

public sealed class FogOfWarTests
{
    [Fact]
    public void ScoutSharesContactWithoutGivingAnotherShipAWeaponTarget()
    {
        var session = ScenarioFixture.LoadSession();
        var game = session.GameState;
        var root = session.RootGroups.First();
        var own = root.Walk<ShipLog>().Where(s => s.IsOnMap()).ToList();
        var shooter = own.First(s => s.torpedoSectorStatus.mountStatus.Count > 0);
        var scout = own.First(s => s != shooter);
        var enemy = session.RootGroups.Last().Walk<ShipLog>().First(s => s.IsOnMap());
        session.StartRolePlayingBattle(shooter.objectId);
        foreach (var ship in game.shipLogs.Where(s => s != shooter && s != scout && s != enemy))
            ship.mapState = MapState.NotDeployed;
        scout.position = new LatLon(35, 130);
        enemy.position = new LatLon(35, 130.001);
        shooter.position = new LatLon(35, 132);

        Assert.False(game.FogOfWarEnabled);
        game.SetFogOfWar(true);
        Assert.True(session.CanDisplay(scout));
        Assert.False(session.IsPlayerShip(scout));
        Assert.True(game.CanSee(scout, enemy));
        Assert.False(game.CanSee(shooter, enemy));
        Assert.True(session.CanDisplay(enemy));
        Assert.False(session.CanInspect(enemy));
        Assert.Throws<InvalidOperationException>(() => session.SetAttackConstraint(shooter.objectId, enemy.objectId));
        using (var context = TorpedoAttackContext.Begin())
        {
            var options = TorpedoFireEvaluation.Evaluate(shooter, enemy, shooter.torpedoSectorStatus.mountStatus[0], context);
            Assert.NotEmpty(options);
            Assert.All(options, o => Assert.Equal(TorpedoFireBlock.NoVisualContact, o.Blocks));
        }
        Assert.True(game.TryGetShipHostileProximityInfo(shooter, out var info));
        Assert.Same(enemy, info.nearestEnemy);

        shooter.position = scout.position.Clone();
        game.RefreshVisualContacts();
        session.SetAttackConstraint(shooter.objectId, enemy.objectId);
        var gun = shooter.batteryStatus.First().mountStatus.First();
        var tube = shooter.torpedoSectorStatus.mountStatus.First();
        gun.SetFiringTarget(enemy);
        tube.SetFiringTarget(enemy);
        shooter.ReplaceRouteFromPath([enemy.position]);
        shooter.SetAutomaticOperationalRouteState(enemy, enemy.position);

        shooter.position = new LatLon(35, 132);
        scout.mapState = MapState.Destroyed;
        game.RefreshVisualContacts();
        Assert.False(session.CanDisplay(enemy));
        Assert.Null(shooter.shipLevelFiringTargetObjectId);
        Assert.Null(gun.firingTargetObjectId);
        Assert.Null(tube.firingTargetObjectId);
        Assert.False(shooter.HasManualRoute());
        Assert.True(game.TryGetShipHostileProximityInfo(shooter, out info));
        Assert.Null(info.nearestEnemy);
        session.StartObserverBattle();
        Assert.True(session.CanDisplay(enemy));
        Assert.False(game.CanSee(shooter, enemy));
        game.SetFogOfWar(false);
        Assert.True(game.CanSee(shooter, enemy));
    }

    [Fact]
    public void SurpriseWakesOnLocalSightingEvenWithoutAnAwakeDistance()
    {
        var session = ScenarioFixture.LoadSession();
        var game = session.GameState;
        var root = session.RootGroups.First();
        var scout = root.Walk<ShipLog>().First(s => s.IsOnMap());
        var enemy = session.RootGroups.Last().Walk<ShipLog>().First(s => s.IsOnMap());
        foreach (var ship in game.shipLogs.Where(s => s != scout && s != enemy))
            ship.mapState = MapState.NotDeployed;
        scout.position = new LatLon(35, 130);
        enemy.position = new LatLon(35, 132);
        game.scenarioState.surpriseAttack = true;
        game.scenarioState.awakeDistanceYards = 1_000_000;
        scout.doctrine.awaking.isInherited = false;
        scout.doctrine.awaking.value = false;
        game.SetFogOfWar(true);
        game.scenarioState.dateTime = game.scenarioState.dateTime.Date.AddHours(6).AddSeconds(59);
        game.Step(1);
        Assert.False(scout.doctrine.GetAwaking());
        enemy.position = new LatLon(35, 130.001);
        game.scenarioState.awakeDistanceYards = 0;
        game.scenarioState.dateTime = game.scenarioState.dateTime.AddSeconds(59);
        game.Step(1);
        Assert.True(scout.doctrine.GetAwaking());
    }
}
