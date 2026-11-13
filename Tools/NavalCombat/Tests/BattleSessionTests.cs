using NavalCombat.Application;
using NavalCombatCore;

namespace NavalCombat.Tests;

public sealed class BattleSessionTests
{
    [Fact]
    public void ConcludingABattleFreezesAndCapturesItsCurrentState()
    {
        var session = ScenarioFixture.LoadSession();
        session.StartBattle(session.RootGroups[0].objectId);

        Assert.True(session.ConcludeBattle("Returned to campaign."));

        Assert.Equal(BattlePhase.Ended, session.Phase);
        Assert.Equal(SimulationSpeed.Paused, session.Speed);
        Assert.Equal("Returned to campaign.", session.EndReason);
        Assert.NotNull(session.Victory);
    }

    [Fact]
    public void StartingBattleMakesPlayerManeuverManualAndEnemyAutomatic()
    {
        var session = ScenarioFixture.LoadSession();
        var player = session.RootGroups[0];
        var enemy = session.RootGroups[1];

        session.StartBattle(player.objectId);

        Assert.Equal(BattleParticipationMode.FleetCommand, session.ParticipationMode);
        Assert.Equal(player.objectId, session.PlayerCommandObjectId);
        Assert.Equal(AutomaticType.Manual, player.doctrine.GetManeuverAutomaticType());
        Assert.Equal(AutomaticType.Automatic, player.doctrine.GetFireAutomaticType());
        Assert.Equal(AutomaticType.Automatic, player.doctrine.GetSearchlightAutomaticType());
        Assert.Equal(AutomaticType.Automatic, enemy.doctrine.GetManeuverAutomaticType());
        Assert.Equal(AutomaticType.Automatic, enemy.doctrine.GetFireAutomaticType());
        Assert.Equal(AutomaticType.Automatic, enemy.doctrine.GetSearchlightAutomaticType());
    }

    [Fact]
    public void ObserverModeLeavesNoPlayerAndMakesEverySideAutomatic()
    {
        var session = ScenarioFixture.LoadSession();

        session.StartObserverBattle();

        Assert.Equal(BattleParticipationMode.Observer, session.ParticipationMode);
        Assert.True(session.IsObserverMode);
        Assert.Null(session.PlayerRootGroupId);
        Assert.Null(session.SelectedObjectId);
        Assert.Equal(BattlePhase.Running, session.Phase);
        Assert.Equal(SimulationSpeed.Paused, session.Speed);
        Assert.All(session.RootGroups, root =>
        {
            Assert.All(new[] { root }.Cast<IShipGroupMember>().Concat(root.Walk<IShipGroupMember>()), member =>
            {
                Assert.Equal(AutomaticType.Automatic, member.doctrine.GetManeuverAutomaticType());
                Assert.Equal(AutomaticType.Automatic, member.doctrine.GetFireAutomaticType());
                Assert.Equal(AutomaticType.Automatic, member.doctrine.GetSearchlightAutomaticType());
            });
            Assert.False(session.IsPlayerMember(root));
        });
    }

    [Fact]
    public void RolePlayingGroupControlsOnlyItsOobSubtreeAndRebuildsAutomation()
    {
        var session = ScenarioFixture.LoadSession();
        var playerRoot = session.RootGroups.First(root => root.GetChildren().OfType<ShipGroup>().Any());
        var command = playerRoot.GetChildren().OfType<ShipGroup>().First();
        var commandedShip = command.Walk<ShipLog>().First();
        var outsideShip = playerRoot.Walk<ShipLog>().First(ship => !command.Walk<ShipLog>().Contains(ship));
        var enemyShip = session.RootGroups.First(root => root != playerRoot).Walk<ShipLog>().First();

        session.StartRolePlayingBattle(command.objectId);

        Assert.Equal(BattleParticipationMode.RolePlaying, session.ParticipationMode);
        Assert.True(session.IsRolePlayingMode);
        Assert.False(session.IsObserverMode);
        Assert.Equal(playerRoot.objectId, session.PlayerRootGroupId);
        Assert.Equal(command.objectId, session.PlayerCommandObjectId);
        Assert.Equal(command.objectId, session.SelectedObjectId);
        Assert.Equal(BattlePhase.Running, session.Phase);
        Assert.Equal(SimulationSpeed.Paused, session.Speed);
        Assert.True(session.IsPlayerMember(command));
        Assert.True(session.IsPlayerMember(commandedShip));
        Assert.False(session.IsPlayerMember(playerRoot));
        Assert.False(session.IsPlayerMember(outsideShip));
        Assert.True(session.IsPlayerSideMember(outsideShip));
        Assert.False(session.IsPlayerSideMember(enemyShip));

        Assert.Equal(AutomaticType.Automatic, playerRoot.doctrine.GetManeuverAutomaticType());
        Assert.Equal(AutomaticType.Manual, command.doctrine.GetManeuverAutomaticType());
        Assert.Equal(AutomaticType.Manual, commandedShip.doctrine.GetManeuverAutomaticType());
        Assert.Equal(AutomaticType.Automatic, outsideShip.doctrine.GetManeuverAutomaticType());
        Assert.False(command.doctrine.maneuverAutomaticType.isInherited);
        Assert.True(commandedShip.doctrine.maneuverAutomaticType.isInherited);
        Assert.All(session.RootGroups.SelectMany(root =>
                new[] { root }.Cast<IShipGroupMember>().Concat(root.Walk<IShipGroupMember>())),
            member =>
            {
                Assert.Equal(AutomaticType.Automatic, member.doctrine.GetFireAutomaticType());
                Assert.Equal(AutomaticType.Automatic, member.doctrine.GetSearchlightAutomaticType());
            });
    }

    [Fact]
    public void RolePlayingCaptainControlsOnlyOneShipButMayReferenceAnAiFriendlyShip()
    {
        var session = ScenarioFixture.LoadSession();
        var playerRoot = session.RootGroups.First(root => root.Walk<ShipLog>().Count() >= 2);
        var ships = playerRoot.Walk<ShipLog>().Where(ship => !ship.IsLandBattery()).Take(2).ToArray();
        var captainShip = ships[0];
        var friendlyAiShip = ships[1];

        session.StartRolePlayingBattle(captainShip.objectId);

        Assert.True(session.IsPlayerShip(captainShip));
        Assert.False(session.IsPlayerShip(friendlyAiShip));
        Assert.True(session.IsPlayerSideMember(friendlyAiShip));
        Assert.Throws<InvalidOperationException>(() =>
            session.IssueIndependentOrder(friendlyAiShip.objectId, 90d, 5d));

        captainShip.controlMode = ControlMode.Independent;
        captainShip.followedTargetObjectId = null;
        captainShip.relativeTargetObjectId = null;
        friendlyAiShip.controlMode = ControlMode.Independent;
        friendlyAiShip.followedTargetObjectId = null;
        friendlyAiShip.relativeTargetObjectId = null;
        session.SetFollowTarget(captainShip.objectId, friendlyAiShip.objectId);
        Assert.Equal(ControlMode.FollowTarget, captainShip.controlMode);
        Assert.Equal(friendlyAiShip.objectId, captainShip.followedTargetObjectId);
    }

    [Fact]
    public void RolePlayingFormationCommandsRejectAiControlledFriendlyMembersAtomically()
    {
        var session = ScenarioFixture.LoadSession();
        var playerRoot = session.RootGroups.First(root => root.GetChildren().OfType<ShipGroup>().Any());
        var command = playerRoot.GetChildren().OfType<ShipGroup>().First();
        var anchor = command.Walk<ShipLog>().First(ship => !ship.IsLandBattery());
        var outsideShip = playerRoot.Walk<ShipLog>()
            .First(ship => !command.Walk<ShipLog>().Contains(ship) && !ship.IsLandBattery());
        session.StartRolePlayingBattle(command.objectId);
        anchor.controlMode = ControlMode.Independent;
        outsideShip.controlMode = ControlMode.FollowTarget;
        outsideShip.followedTargetObjectId = anchor.objectId;
        outsideShip.followDistanceYards = 725d;

        var error = Assert.Throws<InvalidOperationException>(() =>
            session.ArrangeFollowFormation(anchor.objectId, 300d));

        Assert.Contains("outside the player's command", error.Message);
        Assert.Equal(ControlMode.FollowTarget, outsideShip.controlMode);
        Assert.Equal(anchor.objectId, outsideShip.followedTargetObjectId);
        Assert.Equal(725d, outsideShip.followDistanceYards);
    }

    [Fact]
    public void IndependentOrderTakesControlAndClampsValues()
    {
        var session = ScenarioFixture.LoadSession();
        var player = session.RootGroups[0];
        session.StartBattle(player.objectId);
        var ship = player.Walk<ShipLog>().First();
        ship.controlMode = ControlMode.FollowTarget;
        ship.followedTargetObjectId = "old-target";
        ship.relativeTargetObjectId = "relative-target";
        ship.manualRoute.Add(new LatLon(1, 1));

        session.IssueIndependentOrder(ship.objectId, -30d, 999d);

        Assert.Equal(ControlMode.Independent, ship.controlMode);
        Assert.Null(ship.followedTargetObjectId);
        Assert.Null(ship.relativeTargetObjectId);
        Assert.Empty(ship.manualRoute);
        Assert.False(ship.doctrine.maneuverAutomaticType.isInherited);
        Assert.Equal(AutomaticType.Manual, ship.doctrine.maneuverAutomaticType.value);
        Assert.Equal(330d, ship.desiredHeadingDeg, 2);
        Assert.Equal(ship.GetMaxSpeedKnots(), ship.desiredSpeedKnots, 2);
    }

    [Fact]
    public void DesiredTargetBindingsDoNotChangeControlRelationshipOrDoctrine()
    {
        var session = ScenarioFixture.LoadSession();
        var player = session.RootGroups.MaxBy(root => root.Walk<ShipLog>().Count());
        session.StartBattle(player.objectId);
        var ships = player.Walk<ShipLog>().Where(ship => !ship.IsLandBattery()).Take(2).ToArray();
        var ship = ships[1];
        ship.controlMode = ControlMode.FollowTarget;
        ship.followedTargetObjectId = ships[0].objectId;
        ship.doctrine.maneuverAutomaticType.isInherited = true;

        session.SetDesiredHeading(ship.objectId, 725d);
        session.SetDesiredSpeed(ship.objectId, 999d);

        Assert.Equal(5d, ship.desiredHeadingDeg, 6);
        Assert.Equal(ship.GetMaxSpeedKnots(), ship.desiredSpeedKnots, 6);
        Assert.Equal(ControlMode.FollowTarget, ship.controlMode);
        Assert.Equal(ships[0].objectId, ship.followedTargetObjectId);
        Assert.True(ship.doctrine.maneuverAutomaticType.isInherited);
    }

    [Fact]
    public void PauseToggleRestoresTheLastRunningSpeedOrDefaultsToSixtyTimes()
    {
        var session = ScenarioFixture.LoadSession();
        var playerId = session.RootGroups[0].objectId;
        session.StartBattle(playerId);

        Assert.Null(session.LastNonPausedSpeed);
        session.TogglePause();
        Assert.Equal(SimulationSpeed.Maximum, session.Speed);
        Assert.Equal(SimulationSpeed.Maximum, session.LastNonPausedSpeed);

        session.SetSimulationSpeed(SimulationSpeed.Fast);
        session.TogglePause();
        Assert.Equal(SimulationSpeed.Paused, session.Speed);
        Assert.Equal(SimulationSpeed.Fast, session.LastNonPausedSpeed);
        session.TogglePause();
        Assert.Equal(SimulationSpeed.Fast, session.Speed);

        session.StartBattle(playerId);
        Assert.Equal(SimulationSpeed.Paused, session.Speed);
        Assert.Null(session.LastNonPausedSpeed);
        session.TogglePause();
        Assert.Equal(SimulationSpeed.Maximum, session.Speed);
    }

    [Theory]
    [InlineData(0.01d, 0d, 0d)]
    [InlineData(0d, 0.01d, 90d)]
    [InlineData(-0.01d, 0d, 180d)]
    [InlineData(0d, -0.01d, 270d)]
    public void MapCourseOrderDetachesAndPreservesDesiredSpeed(
        double latitudeOffset,
        double longitudeOffset,
        double expectedHeadingDeg)
    {
        var session = ScenarioFixture.LoadSession();
        var player = session.RootGroups.MaxBy(root => root.Walk<ShipLog>().Count());
        session.StartBattle(player.objectId);
        var ship = player.Walk<ShipLog>().First(candidate => !candidate.IsLandBattery());
        ship.controlMode = ControlMode.FollowTarget;
        ship.followedTargetObjectId = "old-target";
        ship.relativeTargetObjectId = "old-relative-target";
        ship.manualRoute.Add(new LatLon(1d, 1d));
        ship.desiredSpeedKnots = 7.5d;

        session.IssueIndependentCourseOrder(
            ship.objectId,
            ship.position.LatDeg + latitudeOffset,
            ship.position.LonDeg + longitudeOffset);

        Assert.Equal(ControlMode.Independent, ship.controlMode);
        Assert.Null(ship.followedTargetObjectId);
        Assert.Null(ship.relativeTargetObjectId);
        Assert.Empty(ship.manualRoute);
        Assert.Equal(expectedHeadingDeg, ship.desiredHeadingDeg, 1);
        Assert.Equal(7.5d, ship.desiredSpeedKnots, 2);
        Assert.Equal(AutomaticType.Manual, ship.doctrine.GetManeuverAutomaticType());
    }

    [Fact]
    public void FollowAndRelativeCommandsValidateTheControlTreeAndKeepParametersEditable()
    {
        var session = ScenarioFixture.LoadSession();
        var player = session.RootGroups.MaxBy(root => root.Walk<ShipLog>().Count());
        var enemy = session.RootGroups.First(root => root != player);
        session.StartBattle(player.objectId);
        var ships = player.Walk<ShipLog>().Where(ship => !ship.IsLandBattery()).Take(3).ToArray();
        Assert.Equal(3, ships.Length);

        session.SetFollowTarget(ships[1].objectId, ships[0].objectId);
        session.SetFollowParameters(ships[1].objectId, 725d);
        Assert.Equal(ControlMode.FollowTarget, ships[1].controlMode);
        Assert.Equal(ships[0].objectId, ships[1].followedTargetObjectId);
        Assert.Equal(725d, ships[1].followDistanceYards);

        session.SetRelativeTarget(ships[2].objectId, ships[1].objectId);
        session.SetRelativeParameters(ships[2].objectId, 410d, -45d, true);
        Assert.Equal(ControlMode.RelativeToTarget, ships[2].controlMode);
        Assert.Equal(ships[1].objectId, ships[2].relativeTargetObjectId);
        Assert.Equal(410d, ships[2].relativeToTargetDistanceYards);
        Assert.Equal(315d, ships[2].relativeToTargetAzimuth);
        Assert.True(ships[2].relativeToAbsolute);

        Assert.Throws<InvalidOperationException>(() =>
            session.SetFollowTarget(ships[0].objectId, ships[2].objectId));
        Assert.Throws<InvalidOperationException>(() =>
            session.SetFollowTarget(ships[0].objectId, enemy.Walk<ShipLog>().First().objectId));
    }

    [Fact]
    public void FormationAndAttackCommandsUseTheExistingCoreState()
    {
        var session = ScenarioFixture.LoadSession();
        var player = session.RootGroups.MaxBy(root => root.Walk<ShipLog>().Count());
        var enemy = session.RootGroups.First(root => root != player);
        session.StartBattle(player.objectId);
        var ships = player.Walk<ShipLog>().Where(ship => !ship.IsLandBattery()).Take(3).ToArray();
        var target = enemy.Walk<ShipLog>().First(ship => ship.mapState == MapState.Deployed);
        foreach (var playerShip in player.Walk<ShipLog>())
        {
            playerShip.controlMode = ControlMode.Independent;
            playerShip.followedTargetObjectId = null;
            playerShip.relativeTargetObjectId = null;
        }

        session.SetFollowTarget(ships[1].objectId, ships[0].objectId);
        session.SetRelativeTarget(ships[2].objectId, ships[1].objectId);
        session.ArrangeFollowFormation(ships[0].objectId, 600d);
        Assert.Equal(ships[0].objectId, ships[1].followedTargetObjectId);
        Assert.Equal(ships[1].objectId, ships[2].followedTargetObjectId);
        Assert.All(ships, ship => Assert.Equal(AutomaticType.Manual, ship.doctrine.GetManeuverAutomaticType()));

        session.ReverseControlChain(ships[0].objectId);
        Assert.Equal(ControlMode.Independent, ships[2].controlMode);
        Assert.Equal(ships[2].objectId, ships[1].followedTargetObjectId);
        Assert.Equal(ships[1].objectId, ships[0].followedTargetObjectId);

        session.SetAttackConstraint(ships[0].objectId, target.objectId);
        Assert.Equal(target.objectId, ships[0].shipLevelFiringTargetObjectId);
        Assert.Same(target, ships[0].GetShipLevelFiringTarget());
        session.CancelAttackConstraint(ships[0].objectId);
        Assert.Null(ships[0].shipLevelFiringTargetObjectId);
    }

    [Fact]
    public void EnemyShipsAndGroupsAreReadOnly()
    {
        var session = ScenarioFixture.LoadSession();
        var player = session.RootGroups[0];
        var enemy = session.RootGroups[1];
        session.StartBattle(player.objectId);
        var enemyShip = enemy.Walk<ShipLog>().First();

        Assert.Throws<InvalidOperationException>(() => session.IssueIndependentOrder(enemyShip.objectId, 90, 10));
        Assert.Throws<InvalidOperationException>(() => session.SetGroupDoctrine(enemy.objectId, DoctrineDomain.Maneuver, DoctrineSetting.Manual));
    }

    [Fact]
    public void DoctrineCanOverrideAndReturnToInheritance()
    {
        var session = ScenarioFixture.LoadSession();
        var player = session.RootGroups[0];
        session.StartBattle(player.objectId);

        session.SetGroupDoctrine(player.objectId, DoctrineDomain.Maneuver, DoctrineSetting.Automatic);
        Assert.Equal(DoctrineSetting.Automatic, session.GetGroupDoctrineSetting(player, DoctrineDomain.Maneuver));
        Assert.Equal(AutomaticType.Automatic, player.doctrine.GetManeuverAutomaticType());

        session.SetGroupDoctrine(player.objectId, DoctrineDomain.Maneuver, DoctrineSetting.Inherited);
        Assert.Equal(DoctrineSetting.Inherited, session.GetGroupDoctrineSetting(player, DoctrineDomain.Maneuver));
        Assert.Equal(AutomaticType.Manual, player.doctrine.GetManeuverAutomaticType());

        var ship = player.Walk<ShipLog>().First();
        session.IssueIndependentOrder(ship.objectId, 180, 8);
        session.ReturnManeuverControlToDoctrine(ship.objectId);
        Assert.True(ship.doctrine.maneuverAutomaticType.isInherited);
    }

    [Fact]
    public void MaximumSpeedAdvancesExactlyFiveSimulatedMinutes()
    {
        var session = ScenarioFixture.LoadSession();
        session.StartBattle(session.RootGroups[0].objectId);
        RandomUtils.SetSeed(1904);
        var start = session.GameState.scenarioState.dateTime;

        session.SetSimulationSpeed(SimulationSpeed.Maximum);
        for (var i = 0; i < 50; i++)
            session.AdvanceWallClock(0.1d);

        Assert.Equal(start.AddMinutes(5), session.GameState.scenarioState.dateTime);
        Assert.Equal(BattlePhase.Running, session.Phase);
    }

    [Fact]
    public void ExtremeSpeedAdvancesExactlyTwoMinutesAcrossUiSizedTicks()
    {
        var session = ScenarioFixture.LoadSession();
        session.StartBattle(session.RootGroups[0].objectId);
        RandomUtils.SetSeed(1904);
        var start = session.GameState.scenarioState.dateTime;

        session.SetSimulationSpeed(SimulationSpeed.Extreme);
        for (var tick = 0; tick < 10; tick++)
            session.AdvanceWallClock(0.1d);

        Assert.Equal(start.AddMinutes(2), session.GameState.scenarioState.dateTime);
        Assert.Equal(BattlePhase.Running, session.Phase);
    }

    [Fact]
    public void ExtremeSpeedRetainsWorkBeyondThePerCallbackStepLimit()
    {
        var session = ScenarioFixture.LoadSession();
        session.StartBattle(session.RootGroups[0].objectId);
        RandomUtils.SetSeed(1904);
        var start = session.GameState.scenarioState.dateTime;

        session.SetSimulationSpeed(SimulationSpeed.Extreme);
        session.AdvanceWallClock(1d);
        Assert.Equal(start.AddSeconds(60), session.GameState.scenarioState.dateTime);

        session.AdvanceWallClock(0d);
        Assert.Equal(start.AddSeconds(120), session.GameState.scenarioState.dateTime);
    }

    [Fact]
    public void ResolvedGunneryEventsAreForwardedInWallClockBatches()
    {
        var session = ScenarioFixture.LoadSession();
        session.StartObserverBattle();
        RandomUtils.SetSeed(1904);
        var battleStart = session.GameState.scenarioState.dateTime;
        var directEvents = new List<GunneryShotResolvedEvent>();
        var forwardedEvents = new List<GunneryShotResolvedEvent>();
        session.GameState.GunneryShotResolved += directEvents.Add;
        session.GunneryShotsResolved += (events, _) => forwardedEvents.AddRange(events);

        session.SetSimulationSpeed(SimulationSpeed.Extreme);
        for (var interval = 0; interval < 10 && session.Phase == BattlePhase.Running; interval++)
            session.AdvanceWallClock(0.5d);

        Assert.NotEmpty(directEvents);
        Assert.Equal(directEvents, forwardedEvents);
        Assert.All(directEvents, resolvedEvent =>
        {
            Assert.False(string.IsNullOrWhiteSpace(resolvedEvent.ShooterId));
            Assert.False(string.IsNullOrWhiteSpace(resolvedEvent.TargetId));
            Assert.False(string.IsNullOrWhiteSpace(resolvedEvent.WeaponId));
            Assert.True(resolvedEvent.ResolutionTime >= battleStart);
            Assert.True(double.IsFinite(resolvedEvent.ShooterLatitudeDeg));
            Assert.True(double.IsFinite(resolvedEvent.ShooterLongitudeDeg));
            Assert.True(double.IsFinite(resolvedEvent.TargetLatitudeDeg));
            Assert.True(double.IsFinite(resolvedEvent.TargetLongitudeDeg));
        });
    }

    [Fact]
    public void ActualTorpedoLaunchesAreForwardedOnceAndDoNotChangeSimulation()
    {
        string Run(bool observe)
        {
            var session = ScenarioFixture.LoadSession("SJH - Torpedo Attack Day 2, Weihaiwei.scen.xml");
            var direct = new List<TorpedoLaunchedEvent>();
            var forwarded = new List<TorpedoLaunchedEvent>();
            if (observe)
            {
                session.GameState.TorpedoLaunched += direct.Add;
                session.TorpedoesLaunched += (events, _) => forwarded.AddRange(events);
            }
            session.StartObserverBattle(); RandomUtils.SetSeed(1895);
            session.SetSimulationSpeed(SimulationSpeed.Extreme);
            for (var i = 0; i < 80 && session.Phase == BattlePhase.Running; i++) session.AdvanceWallClock(.5);
            if (observe)
            {
                Assert.NotEmpty(direct); Assert.Equal(direct, forwarded);
                Assert.Equal(direct.Count, direct.Select(e => e.TorpedoId).Distinct().Count());
                Assert.All(direct, e => { Assert.False(string.IsNullOrEmpty(e.ShooterId)); Assert.True(double.IsFinite(e.LatitudeDeg)); });
                session.SetSimulationSpeed(SimulationSpeed.Paused);
                var count = direct.Count; session.AdvanceWallClock(10); Assert.Equal(count, direct.Count);
            }
            return string.Join(";", session.GameState.shipLogs.Select(s => $"{s.objectId}:{s.mapState}:{s.position.LatDeg:R}:{s.position.LonDeg:R}"));
        }
        Assert.Equal(Run(false), Run(true));
    }

    [Fact]
    public void CoreStepProcessesSurpriseCountdownOnlyAtMinuteRollover()
    {
        var session = LoadSurpriseCountdownSession(3);
        var state = session.GameState;
        var ship = state.shipLogsOnMap.Single();
        state.scenarioState.dateTime = new DateTime(1904, 1, 1, 12, 0, 58, DateTimeKind.Utc);

        state.Step(1d);
        Assert.Equal(3, ship.doctrine.GetAwakeCountdownMinutes());

        state.Step(1d);
        Assert.Equal(2, ship.doctrine.GetAwakeCountdownMinutes());
    }

    [Fact]
    public void CoreStepSplitsLargeAdvanceAcrossEveryMinuteBoundary()
    {
        var session = LoadSurpriseCountdownSession(4);
        var state = session.GameState;
        var ship = state.shipLogsOnMap.Single();
        var start = new DateTime(1904, 1, 1, 12, 0, 30, DateTimeKind.Utc);
        state.scenarioState.dateTime = start;

        state.Step(120d);

        Assert.Equal(start.AddSeconds(120), state.scenarioState.dateTime);
        Assert.Equal(2, ship.doctrine.GetAwakeCountdownMinutes());
    }

    [Fact]
    public void CoreStepWakesShipAttackedDuringTheCompletedMinute()
    {
        var session = LoadSurpriseCountdownSession(3);
        var state = session.GameState;
        var ship = state.shipLogsOnMap.Single();
        state.scenarioState.dateTime = new DateTime(1904, 1, 1, 12, 0, 59, DateTimeKind.Utc);
        ship.doctrine.awaking.value = false;
        ship.MarkAttackedAtCurrentMinute();

        state.Step(1d);

        Assert.True(ship.doctrine.GetAwaking());
        Assert.Equal(3, ship.doctrine.GetAwakeCountdownMinutes());
    }

    [Fact]
    public void ScenarioEndPausesAndGeneratesVictoryReport()
    {
        var session = ScenarioFixture.LoadSession();
        session.StartBattle(session.RootGroups[0].objectId);
        session.GameState.scenarioState.dateTime = session.GameState.scenarioState.endDateTime.AddSeconds(-1);

        session.SetSimulationSpeed(SimulationSpeed.Realtime);
        session.AdvanceWallClock(1d);

        Assert.Equal(BattlePhase.Ended, session.Phase);
        Assert.Equal(SimulationSpeed.Paused, session.Speed);
        Assert.NotNull(session.Victory);
        Assert.Equal(2, session.Victory.sideVictoryStatuses.Count);
    }

    static BattleSession LoadSurpriseCountdownSession(int countdownMinutes)
    {
        var session = ScenarioFixture.LoadSession("TT - Tutorial 1 - Single Ship.scen.xml");
        var state = session.GameState;
        var ship = state.shipLogsOnMap.Single();
        state.scenarioState.surpriseAttack = true;
        state.scenarioState.awakeDistanceYards = 0d;
        ship.doctrine.awakeCountdownMinutes.isInherited = false;
        ship.doctrine.awakeCountdownMinutes.value = countdownMinutes;
        ship.doctrine.awaking.isInherited = false;
        ship.doctrine.awaking.value = true;
        return session;
    }
}
