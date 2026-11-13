using CoreUtils;
using NavalCombatCore;
using NavalCombat.Avalonia.Resources;
using NavalCombat.Avalonia.ViewModels;
namespace NavalCombat.Unity
{
public sealed partial class NavalGame
{
    readonly List<BatteryWeaponStatusViewModel> BatteryWeapons=new();
    readonly List<RapidFiringWeaponStatusViewModel> RapidFiringWeapons=new();
    readonly List<TorpedoWeaponStatusViewModel> TorpedoWeapons=new();
    readonly List<DamageAlertViewModel> DamageAlerts=new();
    void RebuildSelectedWeaponStatuses()
    {
        BatteryWeapons.Clear();
        RapidFiringWeapons.Clear();
        TorpedoWeapons.Clear();
        var ship = SelectedShip;
        if (ship?.shipClass == null)
            return;

        var maskByTargetId = new Dictionary<string, bool>(StringComparer.Ordinal);
        bool IsMasked(ShipLog target)
        {
            if (target == null)
                return false;
            if (maskByTargetId.TryGetValue(target.objectId, out var masked))
                return masked;
            masked = NavalCombatServices.GetMaskCheckService().Check(ship, target)?.isMasked == true;
            maskByTargetId[target.objectId] = masked;
            return masked;
        }

        for (var batteryIndex = 0; batteryIndex < ship.batteryStatus.Count; batteryIndex++)
        {
            var status = ship.batteryStatus[batteryIndex];
            var record = status.GetBatteryRecord();
            if (record == null)
                continue;
            var totalBarrels = record.mountLocationRecords.Sum(location => location.barrels);
            var currentBarrels = status.mountStatus.Where(mount => mount.IsOperational()).Sum(mount => mount.barrels);
            var assignments = status.mountStatus
                .Select(mount =>
                {
                    var target = mount.GetFiringTarget();
                    var interval = GetBatteryFiringIntervalSeconds(ship, target, mount, record, IsMasked(target));
                    return new { Mount = mount, Target = target, Interval = interval, mount.ammunitionType };
                })
                .Where(assignment => assignment.Target != null)
                .ToList();
            var groups = assignments
                .GroupBy(assignment => new
                {
                    assignment.Target.objectId,
                    Process = Math.Round(assignment.Mount.processSeconds, 2),
                    Interval = Math.Round(assignment.Interval ?? -1d, 2),
                    assignment.ammunitionType
                })
                .Select(group =>
                {
                    var first = group.First();
                    var timing = first.Interval.HasValue
                        ? $"{first.Mount.processSeconds:0.#} / {first.Interval.Value:0.#} s"
                        : $"{first.Mount.processSeconds:0.#} / —";
                    return new WeaponFiringGroupViewModel(
                        NavalDisplayNames.Ship(first.Target),
                        NavalCombatStrings.Text("Ui.Shell.ValueMounts", group.Count()),
                        timing,
                        GetAmmunitionAcronym(first.ammunitionType),
                        GetBatteryFireControlState(status, first.Target));
                })
                .ToList();
            var ammunitionTypesInUse = status.mountStatus
                .Where(mount => mount.IsOperational() && mount.GetFiringTarget() != null)
                .Select(mount => mount.ammunitionType)
                .ToHashSet();
            BatteryWeapons.Add(new BatteryWeaponStatusViewModel(
                NavalDisplayNames.Global(record.name),
                $"{record.shellSizeInch:0.##}''",
                $"×({currentBarrels}/{totalBarrels})",
                BuildBatteryAmmunition(status.ammunition, ammunitionTypesInUse),
                groups));
        }

        for (var rapidIndex = 0; rapidIndex < ship.rapidFiringStatus.Count; rapidIndex++)
        {
            var status = ship.rapidFiringStatus[rapidIndex];
            var record = status.GetRapidFireBatteryRecord();
            if (record == null)
                continue;
            var portTotal = record.barrelsLevelPort.FirstOrDefault();
            var starboardTotal = record.barrelsLevelStarboard.FirstOrDefault();
            var portCurrent = status.GetAvailableBarrels(RapidFiringBatteryLocation.Port);
            var starboardCurrent = status.GetAvailableBarrels(RapidFiringBatteryLocation.Starboard);
            var groups = status.targettingRecords
                .Where(assignment => assignment?.GetTarget() != null && assignment.allocated > 0)
                .Select(assignment =>
                {
                    var target = assignment.GetTarget();
                    var interval = 120d / assignment.allocated * (IsMasked(target) ? 2d : 1d);
                    return new WeaponFiringGroupViewModel(
                        NavalDisplayNames.Ship(target),
                        NavalCombatStrings.Text("Ui.Shell.ValueGunsAllocated", assignment.allocated),
                        $"{assignment.processingSeconds:0.#} / {interval:0.#} s");
                })
                .ToList();
            RapidFiringWeapons.Add(new RapidFiringWeaponStatusViewModel(
                NavalDisplayNames.Global(record.name),
                $"RF{rapidIndex + 1}",
                $"×({portCurrent}/{portTotal}, {starboardCurrent}/{starboardTotal})",
                NavalCombatStrings.Text("Ui.Shell.AmmunitionValue", status.ammunition),
                groups));
        }

        var torpedoSector = ship.shipClass.torpedoSector;
        if (torpedoSector?.mountLocationRecords?.Count > 0)
        {
            var mounts = ship.torpedoSectorStatus.mountStatus;
            var totalTubes = torpedoSector.mountLocationRecords.Sum(location => location.barrels);
            var currentTubes = mounts
                .Where(mount => mount.IsOperational())
                .Sum(mount => Math.Min(mount.currentLoad, mount.barrels));
            var firingGroups = mounts
                .Where(mount => mount.IsOperational() && mount.GetFiringTarget() != null)
                .GroupBy(mount => mount.GetFiringTarget().objectId)
                .Select(group => new WeaponFiringGroupViewModel(
                    NavalDisplayNames.Ship(group.First().GetFiringTarget()),
                    NavalCombatStrings.Text("Ui.Shell.ValueTubesAllocated", group.Sum(mount => mount.barrels)),
                    string.Empty))
                .ToList();
            TorpedoWeapons.Add(new TorpedoWeaponStatusViewModel(
                NavalDisplayNames.Global(torpedoSector.name),
                NavalCombatStrings.Text("Ui.Shell.TorpedoTubeXValueValue", currentTubes, totalTubes),
                NavalCombatStrings.Text("Ui.Shell.TorpedoXvalue", ship.torpedoSectorStatus.GetAmmunitionMagazinePlusLoaded()),
                FormatTorpedoReloads(ship),
                firingGroups));
        }
    }

    void RebuildSelectedDamageStatus()
    {
        DamageAlerts.Clear();
        var ship = SelectedShip;
        if (ship == null)
            return;

        var states = ship.GetSubStatesDownward().ToList();
        var alerts = new List<string>();

        if (ship.shipClass != null)
        {
            var machinerySpaces = ship.shipClass.speedKnotsEngineRoomsLevels.Count
                + ship.shipClass.speedKnotsBoilerRooms.Count;
            var floodedMachinerySpaces = ship.dynamicStatus.engineRoomFloodingHits
                + ship.dynamicStatus.boilerRoomFloodingHits;
            var floodedPercent = (double)floodedMachinerySpaces / Math.Max(1, machinerySpaces);
            if (floodedPercent >= 0.8d)
                alerts.Add(NavalCombatStrings.Text("Ui.Shell.MachineryFlooding80SinkingRisk"));

            if (!ship.IsLandBattery() && ship.shipClass.speedKnots > 0d && ship.GetMaxSpeedKnots() <= 0d)
                alerts.Add(NavalCombatStrings.Text("Ui.Shell.ImmobilizedNoEffectivePropulsion"));
        }

        if (ship.operationalState == ShipOperationalState.FloodingObstruction)
            alerts.Add(NavalCombatStrings.Text("Ui.Shell.SinkingOrFloodingObstruction"));
        else if (ship.operationalState == ShipOperationalState.AbandonShip)
            alerts.Add(NavalCombatStrings.Text("Ui.Shell.ShipAbandoned"));

        if (states.OfType<SinkingState>().Any())
            alerts.Add(NavalCombatStrings.Text("Ui.Shell.SinkingProcessInProgress"));
        if (states.OfType<UncontrolledFlooding>().Any())
            alerts.Add(NavalCombatStrings.Text("Ui.Shell.UncontrolledFlooding"));

        var severeFloodingCount = states.OfType<SevereFloodingState>().Count();
        if (severeFloodingCount > 0)
            alerts.Add(NavalCombatStrings.Text("Ui.Shell.SevereFloodingValue", severeFloodingCount));
        if (states.OfType<ExcessiveFlooding>().Any())
            alerts.Add(NavalCombatStrings.Text("Ui.Shell.ExcessiveFlooding"));
        if (states.OfType<ShipSettleState>().Any())
            alerts.Add(NavalCombatStrings.Text("Ui.Shell.ShipSettling"));

        var fires = states.OfType<ShipboardFireState>().ToList();
        if (fires.Count > 0)
            alerts.Add(NavalCombatStrings.Text("Ui.Shell.ShipboardFireValueSeverityValue", fires.Count, fires.Sum(fire => fire.severity)));
        if (states.OfType<RiskingInMagazineExplosion>().Any())
            alerts.Add(NavalCombatStrings.Text("Ui.Shell.MagazineExplosionRisk"));
        if (states.OfType<FireInExplodePotentialCargoHold>().Any())
            alerts.Add(NavalCombatStrings.Text("Ui.Shell.CargoHoldExplosionRisk"));

        if (states.OfType<FeedwaterPumpDamaged>().Any(effect => effect.hasLoseAllPropulsion)
            || states.OfType<PropulsionDamaged>().Any(effect => effect.hasLoseAllPropulsion))
        {
            alerts.Add(NavalCombatStrings.Text("Ui.Shell.AllPropulsionLost"));
        }
        if (states.OfType<MainPowerplantOOA>().Any())
            alerts.Add(NavalCombatStrings.Text("Ui.Shell.MainPowerplantOutOfAction"));

        var damageControlModifiers = ship.GetSubStates<IDamageControlModifier>().ToList();
        if (damageControlModifiers.Any(modifier => modifier.IsDamageControlBlocked()))
            alerts.Add(NavalCombatStrings.Text("Ui.Shell.DamageControlBlocked"));
        else if (fires.Count > 0 && damageControlModifiers.Any(modifier => modifier.IsFightingFireBlocked()))
            alerts.Add(NavalCombatStrings.Text("Ui.Shell.FirefightingBlocked"));

        foreach (var alert in alerts.Distinct(StringComparer.Ordinal))
            DamageAlerts.Add(new DamageAlertViewModel(alert));
    }

    static string GetBatteryFireControlState(BatteryStatus battery, ShipLog target)
    {
        var states = battery.fireControlSystemStatusRecords
            .Where(record => record.IsOperational() && record.targetObjectId == target?.objectId)
            .Select(record => record.trackingState)
            .ToHashSet();
        if (states.Contains(TrackingSystemState.Hitting))
            return NavalCombatStrings.Text("Core.TrackingSystemState.Hitting");
        if (states.Contains(TrackingSystemState.BeginTracking))
            return NavalCombatStrings.Text("Core.TrackingSystemState.BeginTracking");
        if (states.Contains(TrackingSystemState.Tracking))
            return NavalCombatStrings.Text("Core.TrackingSystemState.Tracking");
        return string.Empty;
    }

    static double? GetBatteryFiringIntervalSeconds(
        ShipLog shooter,
        ShipLog target,
        MountStatusRecord mount,
        BatteryRecord battery,
        bool masked)
    {
        if (target == null || !mount.IsOperational())
            return null;
        var stats = MeasureStats.MeasureApproximation(shooter, target);
        var penetration = battery.penetrationTableRecords.FirstOrDefault(row => stats.distanceYards <= row.distanceYards);
        if (penetration == null)
            return null;
        var rate = penetration.rateOfFire
            * mount.GetSubStates<IRateOfFireModifier>()
                .Select(modifier => modifier.GetRateOfFireCoef())
                .DefaultIfEmpty(1d)
                .Min();
        if (rate <= 0d)
            return null;
        return 120d / rate * (masked ? 2d : 1d);
    }

    static IReadOnlyList<AmmunitionStockViewModel> BuildBatteryAmmunition(
        BatteryAmmunitionRecord ammunition,
        ISet<AmmunitionType> ammunitionTypesInUse)
    {
        var values = new[]
        {
            (AmmunitionType.ArmorPiercing, ammunition.ArmorPiercing),
            (AmmunitionType.SemiArmorPiercing, ammunition.semiArmorPiercing),
            (AmmunitionType.Common, ammunition.common),
            (AmmunitionType.HighExplosive, ammunition.highExplosive)
        };
        var visible = values
            .Where(value => value.Item2 > 0 || ammunitionTypesInUse.Contains(value.Item1))
            .ToList();
        if (visible.Count == 0)
            return [new AmmunitionStockViewModel(NavalCombatStrings.Text("Ui.Shell.AmmunitionEmpty"), false, true, false)];
        return visible
            .Select((value, index) => new AmmunitionStockViewModel(
                $"{GetAmmunitionAcronym(value.Item1)} ×{value.Item2}",
                ammunitionTypesInUse.Contains(value.Item1),
                value.Item2 <= 0,
                index > 0))
            .ToList();
    }

    static string GetAmmunitionAcronym(AmmunitionType ammunitionType) =>
        BatteryAmmunitionRecord.ammunitionTypeAcronymMap.GetValueOrDefault(
            ammunitionType,
            ammunitionType.ToString());

    static string FormatTorpedoReloads(ShipLog ship)
    {
        if (TorpedoMountStatusRecord.disableTorpedoReload)
            return NavalCombatStrings.Text("Ui.Shell.ReloadingDisabled");
        var reloads = new List<int>();
        foreach (var mount in ship.torpedoSectorStatus.mountStatus.Where(mount => mount.IsOperational()))
        {
            var record = mount.MountRecord;
            if (record == null)
                continue;
            var requested = Math.Max(0, mount.barrels - mount.currentLoad);
            var reloadLimit = record.reloadLimit == 0
                ? int.MaxValue
                : Math.Max(0, record.reloadLimit - mount.reloadedLoad);
            var loadingTubes = Math.Min(reloadLimit, Math.Min(requested, ship.torpedoSectorStatus.ammunition));
            var remaining = Math.Max(0, (int)Math.Ceiling(360d - mount.reloadingSeconds));
            for (var tube = 0; tube < loadingTubes; tube++)
                reloads.Add(remaining);
        }
        if (reloads.Count == 0)
            return NavalCombatStrings.Text("Ui.Shell.NoTubesReloading");
        return NavalCombatStrings.Text("Ui.Shell.Reloading") + string.Join(" · ", reloads
            .GroupBy(seconds => seconds)
            .OrderBy(group => group.Key)
            .Select(group => group.Count() > 1 ? $"{group.Key} s ×{group.Count()}" : $"{group.Key} s"));
    }


}
}
