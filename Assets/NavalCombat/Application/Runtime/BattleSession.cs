using CoreUtils;
using GeographicLib;
using NavalCombatCore;

namespace NavalCombat.Application;

public enum BattlePhase
{
    Loading,
    ScenarioSelection,
    Briefing,
    Running,
    Ended,
    Faulted
}

public enum BattleParticipationMode
{
    None,
    FleetCommand,
    RolePlaying,
    Observer
}

public enum SimulationSpeed
{
    Paused = 0,
    Realtime = 1,
    Fast = 5,
    VeryFast = 20,
    Maximum = 60,
    Extreme = 120
}

public enum DoctrineDomain
{
    Maneuver,
    Fire,
    Searchlight
}

public enum DoctrineSetting
{
    Inherited,
    Manual,
    Automatic
}

public sealed class BattleSession
{
    public const string DefaultScenarioId = "RJH - Battle of Ulsan.scen.xml";
    public const string DefaultScenarioTitle = "RJH - Battle of Ulsan";

    readonly IScenarioDataSource scenarioDataSource;
    readonly List<GunneryShotResolvedEvent> pendingGunneryShots = [];
    readonly List<TorpedoLaunchedEvent> pendingTorpedoLaunches = [];
    double accumulatedSimulationSeconds;

    public BattleSession(IScenarioDataSource scenarioDataSource)
    {
        this.scenarioDataSource = scenarioDataSource ?? throw new ArgumentNullException(nameof(scenarioDataSource));
    }

    public BattlePhase Phase { get; private set; } = BattlePhase.Loading;
    public SimulationSpeed Speed { get; private set; } = SimulationSpeed.Paused;
    public SimulationSpeed? LastNonPausedSpeed { get; private set; }
    public NavalGameState GameState { get; private set; }
    public InitialMapView InitialMapView { get; private set; }
    public TimeSpan TimeZoneOffset { get; private set; }
    public DateTimeOffset ToScenarioTime(DateTime utcTime) =>
        new DateTimeOffset(DateTime.SpecifyKind(utcTime, DateTimeKind.Utc)).ToOffset(TimeZoneOffset);
    public string CurrentScenarioId { get; private set; }
    public string ScenarioTitle { get; private set; } = DefaultScenarioTitle;
    public BattleParticipationMode ParticipationMode { get; private set; }
    public string PlayerRootGroupId { get; private set; }
    public string PlayerCommandObjectId { get; private set; }
    public bool IsObserverMode => Phase is BattlePhase.Running or BattlePhase.Ended
        && ParticipationMode == BattleParticipationMode.Observer;
    public bool IsRolePlayingMode => Phase is BattlePhase.Running or BattlePhase.Ended
        && ParticipationMode == BattleParticipationMode.RolePlaying;
    public string SelectedObjectId { get; set; }
    public string ErrorMessage { get; private set; }
    public string ErrorDetails { get; private set; }
    public string EndReason { get; private set; }
    public VictoryStatus Victory { get; private set; }
    public event Action Changed;
    public event Action<IReadOnlyList<GunneryShotResolvedEvent>, SimulationSpeed> GunneryShotsResolved;
    public event Action<IReadOnlyList<TorpedoLaunchedEvent>, SimulationSpeed> TorpedoesLaunched;

    public IReadOnlyList<ShipGroup> RootGroups => GameState?.shipGroups?
        .Where(group => group != null && string.IsNullOrWhiteSpace(group.parentObjectId))
        .ToList() ?? [];

    public async Task LoadDefaultScenarioAsync(CancellationToken cancellationToken = default)
    {
        await LoadScenarioCoreAsync(
            DefaultScenarioId,
            () => scenarioDataSource.LoadDefaultAsync(cancellationToken),
            returnToScenarioSelectionOnFailure: false,
            cancellationToken);
    }

    public Task<bool> LoadScenarioAsync(string scenarioId, CancellationToken cancellationToken = default)
    {
        if (scenarioId == null) throw new ArgumentNullException(nameof(scenarioId));
        if (string.IsNullOrWhiteSpace(scenarioId)) throw new ArgumentException("Value cannot be empty or whitespace.", nameof(scenarioId));
        return LoadScenarioCoreAsync(
            scenarioId,
            () => scenarioDataSource.LoadScenarioAsync(scenarioId, cancellationToken),
            returnToScenarioSelectionOnFailure: true,
            cancellationToken);
    }

    public Task<bool> LoadCampaignScenarioAsync(string scenarioId, CancellationToken cancellationToken = default)
    {
        if (scenarioId == null) throw new ArgumentNullException(nameof(scenarioId));
        if (string.IsNullOrWhiteSpace(scenarioId)) throw new ArgumentException("Value cannot be empty or whitespace.", nameof(scenarioId));
        return LoadScenarioCoreAsync(
            scenarioId,
            () => scenarioDataSource.LoadScenarioAsync(scenarioId, cancellationToken),
            returnToScenarioSelectionOnFailure: false,
            cancellationToken);
    }

    public void LoadFromXml(string scenarioXml, string leadersXml, string shipClassesXml, string namedShipsXml)
    {
        var package = ScenarioPackage.LoadPackage(scenarioXml, leadersXml, shipClassesXml, namedShipsXml);
        AttachGameState(package.GameState);
        InitialMapView = package.InitialMapView;
        TimeZoneOffset = package.TimeZoneOffset;
        CurrentScenarioId ??= DefaultScenarioId;
        ScenarioTitle = GetScenarioTitle(CurrentScenarioId);
        ParticipationMode = BattleParticipationMode.None;
        PlayerRootGroupId = null;
        PlayerCommandObjectId = null;
        SelectedObjectId = null;
        accumulatedSimulationSeconds = 0;
        Speed = SimulationSpeed.Paused;
        LastNonPausedSpeed = null;
        EndReason = null;
        Victory = null;
        ErrorMessage = null;
        ErrorDetails = null;
        Phase = BattlePhase.Briefing;
        NotifyChanged();
    }

    public void ReturnToScenarioSelection()
    {
        Speed = SimulationSpeed.Paused;
        LastNonPausedSpeed = null;
        ParticipationMode = BattleParticipationMode.None;
        PlayerRootGroupId = null;
        PlayerCommandObjectId = null;
        SelectedObjectId = null;
        ErrorMessage = null;
        ErrorDetails = null;
        Phase = BattlePhase.ScenarioSelection;
        NotifyChanged();
    }

    public void StartBattle(string playerRootGroupId)
    {
        var playerGroup = RootGroups.FirstOrDefault(group => group.objectId == playerRootGroupId)
            ?? throw new ArgumentException("Select a valid player fleet.", nameof(playerRootGroupId));

        ParticipationMode = BattleParticipationMode.FleetCommand;
        PlayerRootGroupId = playerGroup.objectId;
        PlayerCommandObjectId = playerGroup.objectId;
        foreach (var root in RootGroups)
        {
            SetDoctrineValue(root.doctrine.maneuverAutomaticType,
                root.objectId == PlayerRootGroupId ? DoctrineSetting.Manual : DoctrineSetting.Automatic);
            SetDoctrineValue(root.doctrine.fireAutomaticType, DoctrineSetting.Automatic);
            SetDoctrineValue(root.doctrine.searchlightAutomaticType, DoctrineSetting.Automatic);
        }

        SelectedObjectId = playerGroup.objectId;
        Speed = SimulationSpeed.Paused;
        LastNonPausedSpeed = null;
        GameState.InitializeLeaderEmbarkation();
        Phase = BattlePhase.Running;
        NotifyChanged();
    }

    public void StartRolePlayingBattle(string commandObjectId)
    {
        if (commandObjectId == null) throw new ArgumentNullException(nameof(commandObjectId));
        if (string.IsNullOrWhiteSpace(commandObjectId)) throw new ArgumentException("Value cannot be empty or whitespace.", nameof(commandObjectId));
        var command = EntityManager.Instance.Get<IShipGroupMember>(commandObjectId)
            ?? throw new ArgumentException("Select a valid command or ship.", nameof(commandObjectId));
        var root = command.GetRootParent() as ShipGroup;
        if (root == null || !RootGroups.Any(candidate => candidate.objectId == root.objectId))
            throw new ArgumentException("The selected post does not belong to a scenario fleet.", nameof(commandObjectId));

        ParticipationMode = BattleParticipationMode.RolePlaying;
        PlayerRootGroupId = root.objectId;
        PlayerCommandObjectId = command.objectId;

        foreach (var fleetRoot in RootGroups)
        {
            SetAllAiDoctrine(fleetRoot);
            foreach (var member in fleetRoot.Walk<IShipGroupMember>())
            {
                SetDoctrineValue(member.doctrine.maneuverAutomaticType, DoctrineSetting.Inherited);
                SetDoctrineValue(member.doctrine.fireAutomaticType, DoctrineSetting.Inherited);
                SetDoctrineValue(member.doctrine.searchlightAutomaticType, DoctrineSetting.Inherited);
            }
        }
        SetDoctrineValue(command.doctrine.maneuverAutomaticType, DoctrineSetting.Manual);

        SelectedObjectId = command.objectId;
        Speed = SimulationSpeed.Paused;
        LastNonPausedSpeed = null;
        GameState.InitializeLeaderEmbarkation();
        Phase = BattlePhase.Running;
        NotifyChanged();
    }

    public void StartObserverBattle()
    {
        if (RootGroups.Count == 0)
            throw new InvalidOperationException("The scenario has no fleet to observe.");

        ParticipationMode = BattleParticipationMode.Observer;
        PlayerRootGroupId = null;
        PlayerCommandObjectId = null;
        foreach (var root in RootGroups)
        {
            SetAllAiDoctrine(root);
            foreach (var member in root.Walk<IShipGroupMember>())
                SetAllAiDoctrine(member);
        }

        SelectedObjectId = null;
        Speed = SimulationSpeed.Paused;
        LastNonPausedSpeed = null;
        GameState.InitializeLeaderEmbarkation();
        Phase = BattlePhase.Running;
        NotifyChanged();
    }

    static void SetAllAiDoctrine(IShipGroupMember member)
    {
        SetDoctrineValue(member.doctrine.maneuverAutomaticType, DoctrineSetting.Automatic);
        SetDoctrineValue(member.doctrine.fireAutomaticType, DoctrineSetting.Automatic);
        SetDoctrineValue(member.doctrine.searchlightAutomaticType, DoctrineSetting.Automatic);
    }

    public void SetSimulationSpeed(SimulationSpeed speed)
    {
        if (Phase != BattlePhase.Running)
            return;
        if (speed != SimulationSpeed.Paused)
            LastNonPausedSpeed = speed;
        Speed = speed;
        accumulatedSimulationSeconds = 0;
        NotifyChanged();
    }

    public void TogglePause()
    {
        if (Phase != BattlePhase.Running)
            return;
        SetSimulationSpeed(Speed == SimulationSpeed.Paused
            ? LastNonPausedSpeed ?? SimulationSpeed.Maximum
            : SimulationSpeed.Paused);
    }

    public bool ConcludeBattle(string reason)
    {
        if (Phase == BattlePhase.Ended)
            return true;
        if (Phase != BattlePhase.Running || GameState == null)
            return false;
        EndBattle(string.IsNullOrWhiteSpace(reason) ? "Action concluded." : reason);
        NotifyChanged();
        return true;
    }

    public void AdvanceWallClock(double wallSeconds)
    {
        if (Phase != BattlePhase.Running || Speed == SimulationSpeed.Paused || GameState == null)
            return;

        accumulatedSimulationSeconds += Math.Max(0, wallSeconds) * (int)Speed;
        var steps = Math.Min(60, (int)Math.Floor(accumulatedSimulationSeconds));
        if (steps <= 0)
            return;

        accumulatedSimulationSeconds -= steps;
        var advanceSpeed = Speed;
        try
        {
            for (var i = 0; i < steps && Phase == BattlePhase.Running; i++)
            {
                GameState.Step(1d);
                var reason = GetAutoEndReason();
                if (reason != null)
                    EndBattle(reason);
            }
        }
        catch (Exception ex)
        {
            Speed = SimulationSpeed.Paused;
            Phase = BattlePhase.Faulted;
            ErrorMessage = $"The naval combat model failed to advance: {ex.Message}";
            ErrorDetails = BuildErrorDetails(ex, "Advance naval combat simulation", CurrentScenarioId);
        }

        PublishPendingGunneryShots(advanceSpeed);
        if (pendingTorpedoLaunches.Count > 0)
        {
            var launches = pendingTorpedoLaunches.ToArray(); pendingTorpedoLaunches.Clear();
            TorpedoesLaunched?.Invoke(launches, advanceSpeed);
        }
        NotifyChanged();
    }

    public void IssueIndependentOrder(string shipId, double headingDeg, double speedKnots)
    {
        var ship = RequireCommandableShip(shipId);
        if (!double.IsFinite(headingDeg) || !double.IsFinite(speedKnots))
            throw new ArgumentOutOfRangeException(nameof(headingDeg), "Course and speed must be finite values.");

        TakeManualManeuverControl(ship, ControlMode.Independent);
        ship.desiredHeadingDeg = MeasureUtils.NormalizeAngle(headingDeg);
        ship.desiredSpeedKnots = Math.Clamp(speedKnots, ship.GetMinSpeedKnots(), ship.GetMaxSpeedKnots());
        NotifyChanged();
    }

    public void SetDesiredHeading(string shipId, double headingDeg)
    {
        var ship = RequireCommandableShip(shipId);
        if (!double.IsFinite(headingDeg))
            throw new ArgumentOutOfRangeException(nameof(headingDeg), "Course must be a finite value.");
        ship.desiredHeadingDeg = MeasureUtils.NormalizeAngle(headingDeg);
        NotifyChanged();
    }

    public void SetDesiredSpeed(string shipId, double speedKnots)
    {
        var ship = RequireCommandableShip(shipId);
        if (!double.IsFinite(speedKnots))
            throw new ArgumentOutOfRangeException(nameof(speedKnots), "Speed must be a finite value.");
        ship.desiredSpeedKnots = Math.Clamp(speedKnots, ship.GetMinSpeedKnots(), ship.GetMaxSpeedKnots());
        NotifyChanged();
    }

    public void IssueIndependentCourseOrder(string shipId, double latitudeDeg, double longitudeDeg)
    {
        var ship = RequireCommandableShip(shipId);
        if (!double.IsFinite(latitudeDeg) || latitudeDeg is < -90d or > 90d || !double.IsFinite(longitudeDeg))
            throw new ArgumentOutOfRangeException(nameof(latitudeDeg), "The destination must be a valid geographic position.");

        Geodesic.WGS84.Inverse(
            ship.position.LatDeg,
            ship.position.LonDeg,
            latitudeDeg,
            longitudeDeg,
            out _,
            out var azimuthDeg,
            out _);
        if (!double.IsFinite(azimuthDeg))
            throw new InvalidOperationException("A course cannot be calculated for this map position.");

        TakeManualManeuverControl(ship, ControlMode.Independent);
        ship.desiredHeadingDeg = MeasureUtils.NormalizeAngle(azimuthDeg);
        ship.desiredSpeedKnots = Math.Clamp(
            ship.desiredSpeedKnots,
            ship.GetMinSpeedKnots(),
            ship.GetMaxSpeedKnots());
        NotifyChanged();
    }

    public void DetachShip(string shipId)
    {
        var ship = RequireCommandableShip(shipId);
        TakeManualManeuverControl(ship, ControlMode.Independent);
        NotifyChanged();
    }

    public void SetFollowTarget(string shipId, string targetShipId)
    {
        var ship = RequireCommandableShip(shipId);
        var target = RequireFriendlyControlTarget(ship, targetShipId);
        EnsureControlRelationshipIsValid(ship, target);
        TakeManualManeuverControl(ship, ControlMode.FollowTarget);
        ship.followedTargetObjectId = target.objectId;
        NotifyChanged();
    }

    public void SetRelativeTarget(string shipId, string targetShipId)
    {
        var ship = RequireCommandableShip(shipId);
        var target = RequireFriendlyControlTarget(ship, targetShipId);
        EnsureControlRelationshipIsValid(ship, target);
        TakeManualManeuverControl(ship, ControlMode.RelativeToTarget);
        ship.relativeTargetObjectId = target.objectId;
        NotifyChanged();
    }

    public void SetFollowParameters(string shipId, double distanceYards)
    {
        var ship = RequireCommandableShip(shipId);
        if (ship.GetEffectiveControlMode() != ControlMode.FollowTarget)
            throw new InvalidOperationException("The selected ship is not following another ship.");
        ValidatePositiveDistance(distanceYards, "Follow distance");
        ship.followDistanceYards = distanceYards;
        SetManualManeuverDoctrine(ship);
        NotifyChanged();
    }

    public void SetRelativeParameters(
        string shipId,
        double distanceYards,
        double azimuthDeg,
        bool absolute)
    {
        var ship = RequireCommandableShip(shipId);
        if (ship.GetEffectiveControlMode() != ControlMode.RelativeToTarget)
            throw new InvalidOperationException("The selected ship is not relative to another ship.");
        ValidatePositiveDistance(distanceYards, "Relative distance");
        if (!double.IsFinite(azimuthDeg))
            throw new ArgumentOutOfRangeException(nameof(azimuthDeg), "Relative bearing must be finite.");
        ship.relativeToTargetDistanceYards = distanceYards;
        ship.relativeToTargetAzimuth = MeasureUtils.NormalizeAngle(azimuthDeg);
        ship.relativeToAbsolute = absolute;
        SetManualManeuverDoctrine(ship);
        NotifyChanged();
    }

    public void ReverseControlChain(string shipId)
    {
        var ship = RequireCommandableShip(shipId);
        var members = CollectFormationTree(ship);
        EnsureFormationTreeIsCommandable(members);
        if (!GameState.TryReverseControlChain(ship, out var message))
            throw new InvalidOperationException(message);
        foreach (var member in members)
            SetManualManeuverDoctrine(member);
        NotifyChanged();
    }

    public void ArrangeRelativeFormation(string shipId, RelativeFormationRequest request)
    {
        var ship = RequireCommandableShip(shipId);
        if (request == null) throw new ArgumentNullException(nameof(request));
        ValidatePositiveDistance(request.distanceYards, "Relative formation distance");
        if (!double.IsFinite(request.angleDeg))
            throw new ArgumentOutOfRangeException(nameof(request), "Relative formation bearing must be finite.");

        var normalizedRequest = new RelativeFormationRequest
        {
            mode = request.mode,
            angleDeg = MeasureUtils.NormalizeAngle(request.angleDeg),
            distanceYards = request.distanceYards,
            isSymmetric = request.isSymmetric,
            absolute = request.absolute,
        };
        var members = CollectFormationTree(ship);
        EnsureFormationTreeIsCommandable(members);
        GameState.ApplyRelativeFormation(ship, normalizedRequest);
        foreach (var member in members)
            SetManualManeuverDoctrine(member);
        NotifyChanged();
    }

    public void ArrangeFollowFormation(string shipId, double distanceYards)
    {
        var ship = RequireCommandableShip(shipId);
        ValidatePositiveDistance(distanceYards, "Follow formation distance");
        var members = CollectFormationTree(ship);
        EnsureFormationTreeIsCommandable(members);
        GameState.ApplyFollowFormation(ship, distanceYards);
        foreach (var member in members)
            SetManualManeuverDoctrine(member);
        NotifyChanged();
    }

    public void SetAttackConstraint(string shipId, string targetShipId)
    {
        var ship = RequireDeployedPlayerShip(shipId);
        var target = EntityManager.Instance.Get<ShipLog>(targetShipId)
            ?? throw new ArgumentException("The target ship does not exist.", nameof(targetShipId));
        if (target.mapState != MapState.Deployed || target.operationalState != ShipOperationalState.Operational)
            throw new InvalidOperationException("The target ship is not currently available for attack.");
        if (GetSideId(target) == GetSideId(ship))
            throw new InvalidOperationException("A friendly ship cannot be selected as an attack target.");
        if (!GameState.CanSee(ship, target)) throw new InvalidOperationException("No visual contact from the firing ship.");
        ship.shipLevelFiringTargetObjectId = target.objectId;
        NotifyChanged();
    }

    public void CancelAttackConstraint(string shipId)
    {
        var ship = RequireDeployedPlayerShip(shipId);
        ship.shipLevelFiringTargetObjectId = null;
        NotifyChanged();
    }

    public void ReturnManeuverControlToDoctrine(string shipId)
    {
        var ship = RequirePlayerShip(shipId);
        ship.doctrine.maneuverAutomaticType.isInherited = true;
        NotifyChanged();
    }

    public void SetGroupDoctrine(string groupId, DoctrineDomain domain, DoctrineSetting setting)
    {
        var group = EntityManager.Instance.Get<ShipGroup>(groupId)
            ?? throw new ArgumentException("The ship group does not exist.", nameof(groupId));
        if (!IsPlayerMember(group))
            throw new InvalidOperationException("Enemy ship-group doctrine is read-only.");

        switch (domain)
        {
            case DoctrineDomain.Maneuver:
                SetDoctrineValue(group.doctrine.maneuverAutomaticType, setting);
                break;
            case DoctrineDomain.Fire:
                SetDoctrineValue(group.doctrine.fireAutomaticType, setting);
                break;
            case DoctrineDomain.Searchlight:
                SetDoctrineValue(group.doctrine.searchlightAutomaticType, setting);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(domain));
        }
        NotifyChanged();
    }

    public DoctrineSetting GetGroupDoctrineSetting(ShipGroup group, DoctrineDomain domain)
    {
        var value = domain switch
        {
            DoctrineDomain.Maneuver => group.doctrine.maneuverAutomaticType,
            DoctrineDomain.Fire => group.doctrine.fireAutomaticType,
            DoctrineDomain.Searchlight => group.doctrine.searchlightAutomaticType,
            _ => throw new ArgumentOutOfRangeException(nameof(domain))
        };
        return value.isInherited
            ? DoctrineSetting.Inherited
            : value.value == AutomaticType.Automatic ? DoctrineSetting.Automatic : DoctrineSetting.Manual;
    }

    public AutomaticType GetEffectiveDoctrine(Doctrine doctrine, DoctrineDomain domain) => domain switch
    {
        DoctrineDomain.Maneuver => doctrine.GetManeuverAutomaticType(),
        DoctrineDomain.Fire => doctrine.GetFireAutomaticType(),
        DoctrineDomain.Searchlight => doctrine.GetSearchlightAutomaticType(),
        _ => throw new ArgumentOutOfRangeException(nameof(domain))
    };

    public bool IsPlayerMember(IShipGroupMember member)
    {
        if (!IsPlayerSideMember(member) || PlayerCommandObjectId == null)
            return false;
        if (ParticipationMode == BattleParticipationMode.FleetCommand)
            return true;
        if (ParticipationMode != BattleParticipationMode.RolePlaying)
            return false;

        for (IShipGroupMember current = member; current != null; current = current.GetParentGroup())
        {
            if (current.objectId == PlayerCommandObjectId)
                return true;
        }
        return false;
    }

    public bool HasPlayerFog => GameState?.FogOfWarEnabled == true && PlayerRootGroupId != null && !IsObserverMode;
    public bool CanInspect(IShipGroupMember member) => !HasPlayerFog || IsPlayerSideMember(member);
    public bool CanDisplay(ShipLog ship) => !HasPlayerFog || ship != null &&
        (IsPlayerSideMember(ship) || GameState.SideHasContact(PlayerRootGroupId, ship));
    public bool IsContact(ShipLog ship) => HasPlayerFog && ship != null && !IsPlayerSideMember(ship);

    public bool CanPresentShot(GunneryShotResolvedEvent shot) => !HasPlayerFog ||
        (CanDisplay(EntityManager.Instance.Get<ShipLog>(shot.ShooterId)) &&
         CanDisplay(EntityManager.Instance.Get<ShipLog>(shot.TargetId)));

    public bool IsPlayerShip(ShipLog ship) => IsPlayerMember(ship);

    public ShipLog GetInitialSelectedShip(string observerCommandObjectId = null)
    {
        var candidates = GameState.shipLogs.Where(IsPlayerShip).ToList();
        if (candidates.Count == 0) candidates = GameState.shipLogsOnMap.ToList();
        var commandId = IsObserverMode ? observerCommandObjectId : PlayerCommandObjectId;
        var command = EntityManager.Instance.Get<ShipGroup>(commandId);
        if (command != null)
        {
            var flagship = candidates
                .Where(ship => GameState.shipLogsOnMap.Contains(ship))
                .SelectMany(ship => ship.embarkedLeaders
                    .Where(entry => entry.commandObjectId == commandId && entry.post == LeaderPost.Commander)
                    .Select(entry => new { ship, entry }))
                .OrderByDescending(candidate => candidate.entry.leaderObjectId == command.leader?.objectId)
                .ThenBy(candidate => candidate.entry.GetDisplayPriority())
                .ThenBy(candidate => candidate.ship.objectId, StringComparer.Ordinal)
                .Select(candidate => candidate.ship).FirstOrDefault();
            if (flagship != null) return flagship;
        }
        return candidates.OrderByDescending(ship => ship.GetEffectiveControlMode() == ControlMode.Independent)
            .ThenByDescending(ship => ship.shipClass.displacementTons).FirstOrDefault();
    }
    public bool IsPlayerSideMember(IShipGroupMember member) => member != null
        && PlayerRootGroupId != null
        && member.GetRootParent()?.objectId == PlayerRootGroupId;
    public string GetSideId(IShipGroupMember member) => member?.GetRootParent()?.objectId;
    public IEnumerable<ShipLog> GetShipsForGroup(ShipGroup group) => group?.Walk<ShipLog>() ?? [];

    public IEnumerable<(ShipLog ship, ShipLogLog log)> GetBattleLogs(int limit = 80)
    {
        if (GameState?.shipLogs == null)
            yield break;
        foreach (var entry in GameState.shipLogs
            .Where(ship => ship != null && CanInspect(ship))
            .SelectMany(ship => ship.logs.Select(log => (ship, log)))
            .OrderByDescending(entry => entry.log.time)
            .Take(limit))
        {
            yield return entry;
        }
    }

    public Task RestartAsync() => RestartAsync(CancellationToken.None);

    public async Task RestartAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(CurrentScenarioId)
            || string.Equals(CurrentScenarioId, DefaultScenarioId, StringComparison.OrdinalIgnoreCase))
        {
            await LoadDefaultScenarioAsync(cancellationToken);
            return;
        }
        await LoadScenarioAsync(CurrentScenarioId, cancellationToken);
    }

    static void TakeManualManeuverControl(ShipLog ship, ControlMode controlMode)
    {
        ship.controlMode = controlMode;
        ship.followedTargetObjectId = null;
        ship.relativeTargetObjectId = null;
        ship.ClearManualRoute();
        ship.ClearAutomaticOperationalRouteState();
        SetManualManeuverDoctrine(ship);
    }

    static void SetManualManeuverDoctrine(ShipLog ship)
    {
        ship.doctrine.maneuverAutomaticType.isInherited = false;
        ship.doctrine.maneuverAutomaticType.value = AutomaticType.Manual;
    }

    static void ValidatePositiveDistance(double distanceYards, string label)
    {
        if (!double.IsFinite(distanceYards) || distanceYards <= 0d)
            throw new ArgumentOutOfRangeException(nameof(distanceYards), $"{label} must be greater than 0 yards.");
    }

    ShipLog RequireDeployedPlayerShip(string shipId)
    {
        var ship = RequirePlayerShip(shipId);
        if (ship.mapState != MapState.Deployed || ship.operationalState != ShipOperationalState.Operational)
            throw new InvalidOperationException("This ship cannot currently accept orders.");
        return ship;
    }

    ShipLog RequireFriendlyControlTarget(ShipLog ship, string targetShipId)
    {
        var target = EntityManager.Instance.Get<ShipLog>(targetShipId)
            ?? throw new ArgumentException("The reference ship does not exist.", nameof(targetShipId));
        if (!IsPlayerSideMember(target) || GetSideId(target) != GetSideId(ship))
            throw new InvalidOperationException("The reference ship must belong to the same friendly fleet.");
        if (target.mapState != MapState.Deployed
            || target.operationalState != ShipOperationalState.Operational
            || target.IsLandBattery())
        {
            throw new InvalidOperationException("The reference ship cannot currently control another ship.");
        }
        return target;
    }

    static void EnsureControlRelationshipIsValid(ShipLog ship, ShipLog target)
    {
        switch (ship.CheckGiveControlTo(target))
        {
            case ShipLog.CheckGiveControlToResult.Ok:
                return;
            case ShipLog.CheckGiveControlToResult.LoopingDetected:
                throw new InvalidOperationException("Invalid control relationship: a control loop would be created.");
            case ShipLog.CheckGiveControlToResult.DifferentGroupRootParent:
                throw new InvalidOperationException("Invalid control relationship: ships belong to different fleet commands.");
            default:
                throw new InvalidOperationException("The requested control relationship is invalid.");
        }
    }

    List<ShipLog> CollectFormationTree(ShipLog anchor)
    {
        var ships = GameState.shipLogsOnMap.Where(ship => ship != null).ToList();
        var result = new List<ShipLog>();
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var queue = new Queue<ShipLog>();
        queue.Enqueue(anchor);
        visited.Add(anchor.objectId);
        while (queue.Count > 0)
        {
            var parent = queue.Dequeue();
            result.Add(parent);
            foreach (var child in ships.Where(candidate => candidate.GetControlPredecessor() == parent))
            {
                if (visited.Add(child.objectId))
                    queue.Enqueue(child);
            }
        }
        return result;
    }

    void EnsureFormationTreeIsCommandable(IEnumerable<ShipLog> members)
    {
        var outsideCommand = members.FirstOrDefault(member => !IsPlayerShip(member));
        if (outsideCommand != null)
        {
            throw new InvalidOperationException(
                $"The formation includes {outsideCommand.GetMemberName()}, which is outside the player's command.");
        }
    }

    ShipLog RequirePlayerShip(string shipId)
    {
        var ship = EntityManager.Instance.Get<ShipLog>(shipId)
            ?? throw new ArgumentException("The ship does not exist.", nameof(shipId));
        if (!IsPlayerShip(ship))
            throw new InvalidOperationException("Orders cannot be issued to an enemy ship.");
        return ship;
    }

    ShipLog RequireCommandableShip(string shipId)
    {
        var ship = RequireDeployedPlayerShip(shipId);
        if (ship.IsLandBattery())
            throw new InvalidOperationException("A land battery cannot accept movement orders.");
        return ship;
    }

    static void SetDoctrineValue(Inheriable<AutomaticType> value, DoctrineSetting setting)
    {
        if (setting == DoctrineSetting.Inherited)
        {
            value.isInherited = true;
            return;
        }

        value.isInherited = false;
        value.value = setting == DoctrineSetting.Automatic ? AutomaticType.Automatic : AutomaticType.Manual;
    }

    string GetAutoEndReason()
    {
        var state = GameState.scenarioState;
        if (state.hasEndDateTime && state.dateTime >= state.endDateTime)
            return "The scenario end time has been reached.";

        var operationalRoots = GetOperationalRootGroups();
        if (!state.disableFirstRemainOneOperationalFleetPrompt && operationalRoots.Count <= 1)
            return "Only one operational fleet remains.";

        var members = operationalRoots
            .Select(ships => ((IShipGroupMember)ships[0]).GetRootParent())
            .Where(member => member != null)
            .ToList();
        if (GameState.AreOperationalRootGroupsDisengaged(members))
            return "The opposing fleets have disengaged.";
        return null;
    }

    List<List<ShipLog>> GetOperationalRootGroups()
    {
        var roots = RootGroups;
        if (roots.Count == 0)
        {
            return GameState.shipLogs
                .Where(IsOperationalShip)
                .GroupBy(ship => ((IShipGroupMember)ship).GetRootParent()?.objectId ?? ship.objectId)
                .Select(group => group.ToList())
                .ToList();
        }

        return roots
            .Select(root => root.Walk<ShipLog>().Where(IsOperationalShip).ToList())
            .Where(ships => ships.Count > 0)
            .ToList();
    }

    static bool IsOperationalShip(ShipLog ship) => ship != null
        && ship.mapState == MapState.Deployed
        && ship.operationalState == ShipOperationalState.Operational
        && (ship.GetMaxSpeedKnots() > 0 || (ship.IsLandBattery() && ship.isLandTarget));

    void EndBattle(string reason)
    {
        Speed = SimulationSpeed.Paused;
        EndReason = reason;
        Victory = VictoryStatus.Generate(GameState);
        Phase = BattlePhase.Ended;
    }

    void NotifyChanged() => Changed?.Invoke();

    void AttachGameState(NavalGameState gameState)
    {
        if (GameState != null)
        {
            GameState.GunneryShotResolved -= OnGunneryShotResolved;
            GameState.TorpedoLaunched -= OnTorpedoLaunched;
        }

        pendingGunneryShots.Clear(); pendingTorpedoLaunches.Clear();
        GameState = gameState;
        if (GameState != null)
        {
            GameState.GunneryShotResolved += OnGunneryShotResolved;
            GameState.TorpedoLaunched += OnTorpedoLaunched;
        }
    }

    void OnTorpedoLaunched(TorpedoLaunchedEvent launched)
    {
        if (!HasPlayerFog || CanDisplay(EntityManager.Instance.Get<ShipLog>(launched.ShooterId))) pendingTorpedoLaunches.Add(launched);
    }

    void OnGunneryShotResolved(GunneryShotResolvedEvent resolvedEvent)
    {
        if (resolvedEvent != null && CanPresentShot(resolvedEvent))
            pendingGunneryShots.Add(resolvedEvent);
    }

    void PublishPendingGunneryShots(SimulationSpeed advanceSpeed)
    {
        if (pendingGunneryShots.Count == 0)
            return;

        var batch = pendingGunneryShots.ToArray();
        pendingGunneryShots.Clear();
        GunneryShotsResolved?.Invoke(batch, advanceSpeed);
    }

    async Task<bool> LoadScenarioCoreAsync(
        string scenarioId,
        Func<Task<ScenarioXmlBundle>> loadBundle,
        bool returnToScenarioSelectionOnFailure,
        CancellationToken cancellationToken)
    {
        Phase = BattlePhase.Loading;
        ErrorMessage = null;
        ErrorDetails = null;
        NotifyChanged();

        try
        {
            var bundle = await loadBundle();
            cancellationToken.ThrowIfCancellationRequested();
            CurrentScenarioId = scenarioId;
            ScenarioTitle = GetScenarioTitle(scenarioId);
            LoadFromXml(bundle.ScenarioXml, bundle.LeadersXml, bundle.ShipClassesXml, bundle.NamedShipsXml);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            Phase = returnToScenarioSelectionOnFailure
                ? BattlePhase.ScenarioSelection
                : BattlePhase.Faulted;
            ErrorMessage = ex.Message;
            ErrorDetails = BuildErrorDetails(ex, "Load naval combat scenario", scenarioId);
            NotifyChanged();
            return false;
        }
    }

    string BuildErrorDetails(Exception exception, string operation, string scenarioId)
    {
        var lines = new List<string>
        {
            "NAVAL COMBAT ERROR REPORT",
            $"Timestamp (UTC): {DateTimeOffset.UtcNow:O}",
            $"Operation: {operation}",
            $"Scenario: {scenarioId ?? CurrentScenarioId ?? "(unknown)"}",
            $"Phase: {Phase}",
            $"Participation mode: {ParticipationMode}",
            $"Player root group: {PlayerRootGroupId ?? "(none)"}",
            $"Player command: {PlayerCommandObjectId ?? "(none)"}",
            $"Simulation speed: {Speed}",
            $"Runtime: {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}",
            $"Operating system: {System.Runtime.InteropServices.RuntimeInformation.OSDescription}",
            $"Process architecture: {System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture}",
            "",
            "EXCEPTION",
            exception.ToString()
        };
        return string.Join(Environment.NewLine, lines);
    }

    static string GetScenarioTitle(string scenarioId)
    {
        var fileName = Path.GetFileName(scenarioId);
        const string suffix = ".scen.xml";
        return fileName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
            ? fileName[..^suffix.Length]
            : Path.GetFileNameWithoutExtension(fileName);
    }
}
