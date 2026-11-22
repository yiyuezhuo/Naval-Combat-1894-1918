using NavalCombat.Avalonia.Resources;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using CoreUtils;
using Texture2D = UnityEngine.Texture2D;
using NavalCombat.Application;
using NavalCombat.Avalonia.Services;
using NavalCombatCore;

namespace NavalCombat.Avalonia.ViewModels;

public enum DossierKind
{
    ShipClass,
    Battery,
    Torpedo,
    NamedShip,
    ShipState,
    Leader,
}

public sealed record DossierReference(DossierKind Kind, string ObjectId, string Name);
public sealed record DossierFactSnapshot(string Label, string Value);
public sealed record DossierLinkSnapshot(string Label, string Value, DossierReference Reference, string UnavailableReason = null);
public sealed record DossierRowSnapshot(
    string Heading,
    string Summary = "",
    string Detail = "",
    DossierReference Reference = null,
    bool IsMarkdown = false);
public sealed record DossierArcSnapshot(double StartDeg, double CoverageDeg, bool IsCrossDeckFire);
public sealed record DossierMountSectorSnapshot(
    MountLocation Location,
    int Mounts,
    int Barrels,
    IReadOnlyList<DossierArcSnapshot> Arcs);
public sealed record DossierPenetrationPointSnapshot(
    double DistanceYards,
    double VerticalInches,
    double HorizontalInches);
public sealed record WeaponDiagramSnapshot(
    string Caption,
    string MountUnitLabel,
    IReadOnlyList<DossierMountSectorSnapshot> MountSectors,
    IReadOnlyList<DossierPenetrationPointSnapshot> PenetrationPoints,
    double MaximumRangeYards,
    double MainBeltEffectiveInches);
public sealed record DossierArmorRowSnapshot(string Location, double ActualInches, double EffectiveInches);
public sealed record DossierSectionSnapshot(
    string Title,
    IReadOnlyList<DossierFactSnapshot> Facts,
    IReadOnlyList<DossierRowSnapshot> Rows,
    string Note = "",
    bool UseVirtualizedRows = false,
    bool ShowShipPlan = false,
    IReadOnlyList<DossierArmorRowSnapshot> ArmorRows = null,
    HistoryChartSnapshot HistoryChart = null);
public sealed record DossierTabSnapshot(string Title, IReadOnlyList<DossierSectionSnapshot> Sections);

public sealed class DossierSnapshot : IDisposable
{
    public DossierKind Kind { get; init; }
    public string ObjectId { get; init; }
    public string Eyebrow { get; init; }
    public string Title { get; init; }
    public string Subtitle { get; init; } = "";
    public string Monogram { get; init; } = "";
    public string SnapshotTime { get; init; } = "";
    public double PlanHeadingDeg { get; init; }
    public Texture2D PlanBitmap { get; init; }
    public WeaponDiagramSnapshot WeaponDiagram { get; init; }
    public IReadOnlyList<DossierLinkSnapshot> Links { get; init; } = [];
    public IReadOnlyList<DossierTabSnapshot> Tabs { get; init; } = [];

    public void Dispose() => UnityEngine.Object.Destroy(PlanBitmap);
}

public static class DossierSnapshotFactory
{
    static readonly CultureInfo invariant = CultureInfo.InvariantCulture;

    public static DossierSnapshot Create(BattleSession session, DossierReference reference)
    {
        if (reference == null)
            return null;
        return reference.Kind switch
        {
            DossierKind.ShipClass => CreateShipClass(EntityManager.Instance.Get<ShipClass>(reference.ObjectId)),
            DossierKind.Battery => CreateBattery(EntityManager.Instance.Get<BatteryRecord>(reference.ObjectId)),
            DossierKind.Torpedo => CreateTorpedo(EntityManager.Instance.Get<ShipClass>(reference.ObjectId)),
            DossierKind.NamedShip => CreateNamedShip(EntityManager.Instance.Get<NamedShip>(reference.ObjectId)),
            DossierKind.Leader => CreateLeader(EntityManager.Instance.Get<Leader>(reference.ObjectId)),
            DossierKind.ShipState => CreateShipState(session, EntityManager.Instance.Get<ShipLog>(reference.ObjectId)),
            _ => null,
        };
    }

    public static DossierSnapshot CreateShipClass(ShipClass shipClass)
    {
        if (shipClass == null)
            return null;
        var title = Name(shipClass.name);
        var particulars = new List<DossierFactSnapshot>
        {
            Fact(NavalCombatStrings.Text("Ui.Type"), Friendly(shipClass.type)),
            Fact(NavalCombatStrings.Text("Ui.Country"), Friendly(shipClass.country)),
            Fact(NavalCombatStrings.Text("Ui.Shell.ReferenceYear"), shipClass.referenceYear.ToString(invariant)),
            Fact(NavalCombatStrings.Text("Ui.Shell.Displacement"), Tons(shipClass.displacementTons)),
            Fact(NavalCombatStrings.Text("Ui.Shell.Dimensions"), $"{shipClass.lengthFoot:0.#} × {shipClass.beamFoot:0.#} × {shipClass.draftFoot:0.#} ft"),
            Fact(NavalCombatStrings.Text("Ui.Shell.Complement"), NavalCombatStrings.Text("Ui.Shell.ValueMen", shipClass.complementMen)),
            Fact(NavalCombatStrings.Text("Ui.Shell.MaximumSpeed"), Knots(shipClass.speedKnots)),
            Fact(NavalCombatStrings.Text("Ui.Shell.TurnInTwoMinutes"), NavalCombatStrings.Text("Ui.Shell.ValueStandardValueEmergency", shipClass.standardTurnDegPer2Min, shipClass.emergencyTurnDegPer2Min)),
            Fact(NavalCombatStrings.Text("Ui.Shell.DamageCapacity"), $"{shipClass.damagePoint:0.#} DP"),
            Fact(NavalCombatStrings.Text("Ui.Shell.DamageControl"), shipClass.damageControlRatingUnmodified.ToString(invariant)),
            Fact(NavalCombatStrings.Text("Core.RamType.Ram"), Friendly(shipClass.ram)),
        };
        if (shipClass.extraShipType != ExtraShipType.NotSpecified)
            particulars.Insert(1, Fact(NavalCombatStrings.Text("Ui.Shell.DesignClassification"), Friendly(shipClass.extraShipType)));

        var armor = shipClass.armorRating;
        var armorFacts = armor == null ? [] : new List<DossierFactSnapshot>
        {
            Fact(NavalCombatStrings.Text("Ui.Shell.ArmorType"), Friendly(armor.armorType)),
            Fact(NavalCombatStrings.Text("Ui.Shell.EffectivenessFactor"), armor.armorTypeFactor.ToString("0.##", invariant)),
        };
        var armorRows = armor == null ? [] : new List<DossierArmorRowSnapshot>
        {
            Armor(NavalCombatStrings.Text("Core.ArmorLocation.Deck"), armor.deck), Armor(NavalCombatStrings.Text("Ui.Shell.MainBelt"), armor.mainBelt), Armor(NavalCombatStrings.Text("Ui.Shell.BeltEnds"), armor.beltEnd),
            Armor(NavalCombatStrings.Text("Ui.Shell.TurretRoof"), armor.turretHorizontal), Armor(NavalCombatStrings.Text("Ui.Shell.TurretFace"), armor.turretVertical),
            Armor(NavalCombatStrings.Text("Core.ArmorLocation.Barbette"), armor.barbette), Armor(NavalCombatStrings.Text("Ui.Shell.ConningTower"), armor.conningTower),
            Armor(NavalCombatStrings.Text("Ui.Shell.SuperstructureDeck"), armor.superStructureHorizontal),
            Armor(NavalCombatStrings.Text("Ui.Shell.SuperstructureSide"), armor.superStructureVertical),
        };

        var batteryRows = shipClass.batteryRecords.Select(record => new DossierRowSnapshot(
            Name(record.name),
            NavalCombatStrings.Text("Ui.Shell.ValueValueBarrelsValueRounds", Inches(record.shellSizeInch), record.mountLocationRecords.Sum(mount => mount.barrels), record.ammunitionCapacity),
            NavalCombatStrings.Text("Ui.Shell.ShellValueLbRangeValueRateValueMinDamageValueN", record.shellWeightPounds, Yards(record.rangeYards), record.maxRateOfFireShootPerMin, record.damageRating)
            + NavalCombatStrings.Text("Ui.Shell.FireControlValuePositionsFcsValueN", record.fireControlPositions, record.fireControlType?.code)
            + Mounts(record.mountLocationRecords, includeTrainability: false),
            Ref(DossierKind.Battery, record.objectId, NavalCombatStrings.Text("Ui.Shell.OpenDiagram")))).ToList();
        var rapidRows = shipClass.rapidFireBatteryRecords.Select((record, index) => new DossierRowSnapshot(
            Name(record.name, NavalCombatStrings.Text("Ui.Shell.RapidFiringBatteryValue", index + 1)),
            NavalCombatStrings.Text("Ui.Shell.ValueDamageValue", RapidFireCaliber(record), record.damageFactor),
            NavalCombatStrings.Text("Ui.Shell.RangeValueEffectiveValueMaximum", Yards(record.effectiveRangeYards), Yards(record.maxRangeYards))
            + NavalCombatStrings.Text("Ui.Shell.PortValueStarboardValueBarrels", record.barrelsLevelPort.FirstOrDefault(), record.barrelsLevelStarboard.FirstOrDefault())
            + RapidFireControl(record))).ToList();
        var torpedoRows = BuildClassTorpedoRows(shipClass);
        var noteRows = new List<DossierRowSnapshot>();
        AddTextRow(noteRows, NavalCombatStrings.Text("Ui.Shell.Notes"), Text(shipClass.remark));
        if (shipClass.isPoorlySupported)
            noteRows.Insert(0, new DossierRowSnapshot(NavalCombatStrings.Text("Ui.Shell.DataConfidence"), NavalCombatStrings.Text("Ui.Shell.LimitedSupportingData"), NavalCombatStrings.Text("Ui.Shell.ThisClassIsMarkedAsPoorlySupportedInTheSourceData")));

        return new DossierSnapshot
        {
            Kind = DossierKind.ShipClass,
            ObjectId = shipClass.objectId,
            Eyebrow = NavalCombatStrings.Text("Ui.Shell.ShipClassTechnicalRecord"),
            Title = title,
            Subtitle = $"{Friendly(shipClass.type)} · {Friendly(shipClass.country)} · {shipClass.referenceYear}",
            PlanBitmap = ShipDetailRenderer.RenderClassBitmap(shipClass),
            PlanHeadingDeg = 90d,
            Tabs =
            [
                Tab(NavalCombatStrings.Text("Ui.Shell.Particulars"), Section(NavalCombatStrings.Text("Ui.Shell.PrincipalParticulars"), particulars), Section(NavalCombatStrings.Text("Ui.Shell.ClassPlan"), showShipPlan: true)),
                Tab(NavalCombatStrings.Text("Ui.Shell.Protection"), Section(
                    NavalCombatStrings.Text("Ui.Shell.ArmorScheme"),
                    armorFacts,
                    armorTableRows: armorRows,
                    note: NavalCombatStrings.Text("Ui.Shell.EffInAndThePenetrationValuesUsedByTheGameAreNormalizedAgainstWwiiEraCla1717320"))),
                Tab(NavalCombatStrings.Text("Ui.Shell.Armament"), Section(NavalCombatStrings.Text("Ui.Shell.Batteries"), rows: batteryRows), Section(NavalCombatStrings.Text("Ui.Shell.RapidFiringBatteries"), rows: rapidRows), Section(NavalCombatStrings.Text("Ui.Shell.Torpedoes"), rows: torpedoRows)),
                Tab(NavalCombatStrings.Text("Ui.Shell.Notes"), Section(NavalCombatStrings.Text("Ui.Shell.RecordNotes"), rows: noteRows, note: noteRows.Count == 0 ? NavalCombatStrings.Text("Ui.Shell.NoAdditionalNotesAreRecorded") : "")),
            ],
        };
    }

    public static DossierSnapshot CreateBattery(BatteryRecord battery)
    {
        if (battery == null)
            return null;
        var shipClass = EntityManager.Instance.GetParent<ShipClass>(battery);
        var mountSectors = BuildMountSectors(battery.mountLocationRecords);
        var penetration = battery.penetrationTableRecords
            .OrderBy(record => record.distanceYards)
            .Select(record => new DossierPenetrationPointSnapshot(
                record.distanceYards,
                record.verticalPenetrationInchs,
                record.horizontalPenetrationInchs))
            .ToList();
        var facts = new List<DossierFactSnapshot>
        {
            Fact(NavalCombatStrings.Text("Ui.Shell.Caliber"), Inches(battery.shellSizeInch)),
            Fact(NavalCombatStrings.Text("Ui.Shell.ShellWeight"), $"{battery.shellWeightPounds:0.#} lb"),
            Fact(NavalCombatStrings.Text("Ui.Shell.MaximumRange"), Yards(battery.rangeYards)),
            Fact(NavalCombatStrings.Text("Ui.Shell.MaximumRateOfFire"), NavalCombatStrings.Text("Ui.Shell.ValueRoundsMin", battery.maxRateOfFireShootPerMin)),
            Fact(NavalCombatStrings.Text("Ui.Shell.PenetrationBasis"), Friendly(battery.penetrationTableBaseType)),
            Fact(NavalCombatStrings.Text("Ui.Shell.FireControl"), NavalCombatStrings.Text("Ui.Shell.ValuePositionsFcsValue", battery.fireControlPositions, battery.fireControlType?.code)),
        };
        var mountRows = battery.mountLocationRecords.Select(record => new DossierRowSnapshot(
            Friendly(record.mountLocation),
            NavalCombatStrings.Text("Ui.Shell.ValueMountsValueBarrelsEach", 1, record.barrels),
            NavalCombatStrings.Text("Ui.Shell.FiringArcsValue", record.SummaryArcs()))).ToList();
        return new DossierSnapshot
        {
            Kind = DossierKind.Battery,
            ObjectId = battery.objectId,
            Eyebrow = NavalCombatStrings.Text("Ui.Shell.BatteryFiringAndPenetrationRecord"),
            Title = Name(battery.name),
            Subtitle = shipClass == null ? NavalCombatStrings.Text("Ui.Shell.UnassociatedBattery") : NavalCombatStrings.Text("Ui.Shell.ValueClass", Name(shipClass.name)),
            WeaponDiagram = new WeaponDiagramSnapshot(
                NavalCombatStrings.Text("Ui.Shell.MountSectorsBearingFromShipSBow"),
                NavalCombatStrings.Text("Ui.Shell.Barrels"),
                mountSectors,
                penetration,
                battery.rangeYards,
                shipClass?.armorRating?.mainBelt?.effectInch ?? 0d),
            Links =
            [
                Link(NavalCombatStrings.Text("Ui.Shell.ShipClass"), shipClass == null ? null : Ref(DossierKind.ShipClass, shipClass.objectId, Name(shipClass.name))),
            ],
            Tabs =
            [
                Tab(NavalCombatStrings.Text("Core.Battery"), Section(NavalCombatStrings.Text("Ui.Shell.BatteryParticulars"), facts), Section(NavalCombatStrings.Text("Ui.Shell.MountArrangement"), rows: mountRows)),
            ],
        };
    }

    public static DossierSnapshot CreateTorpedo(ShipClass shipClass)
    {
        var sector = shipClass?.torpedoSector;
        if (sector?.mountLocationRecords == null || sector.mountLocationRecords.Count == 0)
            return null;
        var settings = sector.torpedoSettings
            .Select((setting, index) => new DossierRowSnapshot(
                NavalCombatStrings.Text("Ui.Shell.SettingValue", index + 1),
                $"{setting.speedKnots:0.#} kn · {Yards(setting.rangeYards)}"))
            .ToList();
        var facts = new List<DossierFactSnapshot>
        {
            Fact(NavalCombatStrings.Text("Ui.Shell.Tubes"), sector.mountLocationRecords.Sum(mount => mount.barrels).ToString(invariant)),
            Fact(NavalCombatStrings.Text("Ui.Shell.TorpedoesCarried"), sector.ammunitionCapacity.ToString(invariant)),
            Fact(NavalCombatStrings.Text("Ui.Shell.DamageClass"), Friendly(sector.damageClass)),
            Fact(NavalCombatStrings.Text("Ui.Pistol"), Friendly(sector.pistolType)),
            Fact(NavalCombatStrings.Text("Ui.Shell.DudProbability"), sector.dudProbability.ToString("P0", invariant)),
        };
        if (sector.metaInfo != null)
            facts.Add(Fact(NavalCombatStrings.Text("Ui.Shell.Warhead"), $"{sector.metaInfo.warheadLb:0.#} lb"));
        var mountRows = sector.mountLocationRecords.Select(record => new DossierRowSnapshot(
            Friendly(record.mountLocation),
            NavalCombatStrings.Text("Ui.Shell.ValueMountsValueTubesEachValue", 1, record.barrels, ((record is TorpedoMountRecord { trainable: true }) ? "trainable" : "fixed")),
            NavalCombatStrings.Text("Ui.Shell.FiringArcsValue", record.SummaryArcs())))
            .ToList();
        return new DossierSnapshot
        {
            Kind = DossierKind.Torpedo,
            ObjectId = shipClass.objectId,
            Eyebrow = NavalCombatStrings.Text("Ui.Shell.TorpedoBatteryFiringRecord"),
            Title = Name(sector.name, NavalCombatStrings.Text("Ui.Shell.TorpedoBattery")),
            Subtitle = NavalCombatStrings.Text("Ui.Shell.ValueClass", Name(shipClass.name)),
            WeaponDiagram = new WeaponDiagramSnapshot(
                NavalCombatStrings.Text("Ui.Shell.TorpedoTubeSectorsBearingFromShipSBow"),
                NavalCombatStrings.Text("Ui.Shell.Tubes7e37c8"),
                BuildMountSectors(sector.mountLocationRecords),
                [],
                0d,
                0d),
            Links =
            [
                Link(NavalCombatStrings.Text("Ui.Shell.ShipClass"), Ref(DossierKind.ShipClass, shipClass.objectId, Name(shipClass.name))),
            ],
            Tabs =
            [
                Tab(NavalCombatStrings.Text("Ui.Shell.TorpedoBattery"),
                    Section(NavalCombatStrings.Text("Ui.Shell.TorpedoParticulars"), facts),
                    Section(NavalCombatStrings.Text("Ui.Shell.SpeedAndRangeSettings"), rows: settings),
                    Section(NavalCombatStrings.Text("Ui.Shell.TubeArrangement"), rows: mountRows)),
            ],
        };
    }

    public static DossierSnapshot CreateNamedShip(NamedShip namedShip)
    {
        if (namedShip == null)
            return null;
        var shipClass = namedShip.shipClass;
        var leader = namedShip.defaultLeader;
        var service = new List<DossierFactSnapshot>
        {
            Fact(NavalCombatStrings.Text("Ui.Shell.CrewRating"), Signed(namedShip.crewRating)),
            Fact(NavalCombatStrings.Text("Ui.Shell.SpeedModifier"), namedShip.speedModifier.ToString("+0.#%;-0.#%;0%", invariant)),
        };
        var noteRows = new List<DossierRowSnapshot>();
        AddTextRow(noteRows, NavalCombatStrings.Text("Ui.Shell.Notes"), Text(namedShip.remark));
        return new DossierSnapshot
        {
            Kind = DossierKind.NamedShip,
            ObjectId = namedShip.objectId,
            Eyebrow = NavalCombatStrings.Text("Ui.Shell.NamedShipHistoricalRecord"),
            Title = Name(namedShip.name),
            Subtitle = AlternateNames(namedShip.name),
            PlanBitmap = ShipDetailRenderer.RenderClassBitmap(shipClass),
            PlanHeadingDeg = 0d,
            Links =
            [
                Link(NavalCombatStrings.Text("Ui.Shell.ShipClass"), shipClass == null ? null : Ref(DossierKind.ShipClass, shipClass.objectId, Name(shipClass.name))),
                Link(NavalCombatStrings.Text("Ui.Shell.CommandingOfficer"), leader == null ? null : Ref(DossierKind.Leader, leader.objectId, Name(leader.name))),
            ],
            Tabs =
            [
                Tab(NavalCombatStrings.Text("Ui.Shell.Record"),
                    Section(NavalCombatStrings.Text("Ui.Shell.ServiceModifiers"), service),
                    Section(NavalCombatStrings.Text("Ui.Shell.Notes"), rows: noteRows, note: noteRows.Count == 0 ? NavalCombatStrings.Text("Ui.Shell.NoAdditionalNotesAreRecorded") : "")),
            ],
        };
    }

    public static DossierSnapshot CreateLeader(Leader leader)
    {
        if (leader == null)
            return null;
        var skills = new List<DossierFactSnapshot>
        {
            Fact(NavalCombatStrings.Text("Ui.Shell.NavalStrategic"), Skill(leader.navalStrategic)),
            Fact(NavalCombatStrings.Text("Ui.Shell.NavalOperational"), Skill(leader.navalOperational)),
            Fact(NavalCombatStrings.Text("Ui.Shell.NavalTactical"), Skill(leader.navalTactical)),
        };
        var traits = leader.traits
            .Where(trait => trait != LeaderTrait.NotSpecified)
            .Select(trait => new DossierRowSnapshot(Friendly(trait)))
            .ToList();
        var noteRows = new List<DossierRowSnapshot>();
        AddTextRow(noteRows, NavalCombatStrings.Text("Ui.Shell.Notes"), Text(leader.remark));
        return new DossierSnapshot
        {
            Kind = DossierKind.Leader,
            ObjectId = leader.objectId,
            Eyebrow = NavalCombatStrings.Text("Ui.Shell.CommandingOfficerServiceRecord"),
            Title = Name(leader.name),
            Subtitle = AlternateNames(leader.name),
            Monogram = Monogram(Name(leader.name)),
            Tabs =
            [
                Tab(NavalCombatStrings.Text("Ui.Shell.Record"),
                    Section(NavalCombatStrings.Text("Ui.Shell.NavalCommandAbility"), skills),
                    Section(NavalCombatStrings.Text("Ui.Shell.RecordedTraits"), rows: traits, note: traits.Count == 0 ? NavalCombatStrings.Text("Ui.Shell.NoTraitsAreRecorded") : ""),
                    Section(NavalCombatStrings.Text("Ui.Shell.BiographicalNotes"), rows: noteRows, note: noteRows.Count == 0 ? NavalCombatStrings.Text("Ui.Shell.NoAdditionalNotesAreRecorded") : "")),
            ],
        };
    }

    public static DossierSnapshot CreateShipState(BattleSession session, ShipLog ship)
    {
        if (ship == null || session?.CanInspect(ship)==false)
            return null;
        var shipClass = ship.shipClass;
        var namedShip = ship.namedShip;
        var leader = ship.leader;
        var parent = ((IShipGroupMember)ship).GetParentGroup();
        var root = ((IShipGroupMember)ship).GetRootParent() as ShipGroup;
        var maxDamage = Math.Max(1d, shipClass?.damagePoint ?? 1d);
        var damagePercent = ship.damagePoint / maxDamage;
        var situation = new List<DossierFactSnapshot>
        {
            Fact(NavalCombatStrings.Text("Ui.Side"), NavalDisplayNames.Group(root)),
            Fact(NavalCombatStrings.Text("Core.UnitLabelDisplayMode.Formation"), NavalDisplayNames.Group(parent)),
            Fact(NavalCombatStrings.Text("Ui.Shell.Deployment"), Friendly(ship.mapState)),
            Fact(NavalCombatStrings.Text("Ui.Shell.OperationalState"), Friendly(ship.operationalState)),
            Fact(NavalCombatStrings.Text("Ui.Shell.Position"), $"{ship.position.LatDeg:0.000000}°, {ship.position.LonDeg:0.000000}°"),
            Fact(NavalCombatStrings.Text("Ui.Shell.Course"), NavalCombatStrings.Text("Ui.Shell.ValueCurrentValueOrdered", ship.headingDeg, ship.desiredHeadingDeg)),
            Fact(NavalCombatStrings.Text("Ui.Speed"), NavalCombatStrings.Text("Ui.Shell.ValueKnCurrentValueKnOrdered", ship.speedKnots, ship.desiredSpeedKnots)),
            Fact(NavalCombatStrings.Text("Ui.Shell.SpeedLimits"), NavalCombatStrings.Text("Ui.Shell.ValueKnAheadValueKnAstern", ship.GetMaxSpeedKnots(), ship.GetMinSpeedKnots())),
            Fact(NavalCombatStrings.Text("Ui.Shell.HullDamage"), $"{ship.damagePoint:0.#} / {shipClass?.damagePoint ?? 0:0.#} DP · {damagePercent:P1}"),
            Fact(NavalCombatStrings.Text("Ui.Shell.DamageTier"), ship.GetDamageTier().ToString(invariant)),
        };

        var commandFacts = new List<DossierFactSnapshot>
        {
            Fact(NavalCombatStrings.Text("Ui.Shell.ControlMode"), Friendly(ship.GetEffectiveControlMode())),
            Fact(NavalCombatStrings.Text("Ui.Shell.ManeuverDoctrine"), Doctrine(ship.doctrine.maneuverAutomaticType, ship.doctrine.GetManeuverAutomaticType())),
            Fact(NavalCombatStrings.Text("Ui.Shell.FireDoctrine"), Doctrine(ship.doctrine.fireAutomaticType, ship.doctrine.GetFireAutomaticType())),
            Fact(NavalCombatStrings.Text("Ui.Shell.SearchlightDoctrine"), Doctrine(ship.doctrine.searchlightAutomaticType, ship.doctrine.GetSearchlightAutomaticType())),
        };
        var commandRows = new List<DossierRowSnapshot>();
        var controlTarget = ship.GetEffectiveControlMode() switch
        {
            ControlMode.FollowTarget => ship.followedTarget,
            ControlMode.RelativeToTarget => ship.relativeToTarget,
            _ => null,
        };
        if (ship.GetEffectiveControlMode() == ControlMode.FollowTarget)
            commandRows.Add(ShipReferenceRow(NavalCombatStrings.Text("Ui.Shell.Following"), controlTarget, NavalCombatStrings.Text("Ui.Shell.DistanceValue", Yards(ship.followDistanceYards))));
        else if (ship.GetEffectiveControlMode() == ControlMode.RelativeToTarget)
            commandRows.Add(ShipReferenceRow(NavalCombatStrings.Text("Ui.Shell.RelativeTo"), controlTarget,
                NavalCombatStrings.Text("Ui.Shell.DistanceValueBearingValue", Yards(ship.relativeToTargetDistanceYards), ship.relativeToTargetAzimuth)
                + (ship.relativeToAbsolute ? NavalCombatStrings.Text("Ui.Shell.Absolute") : NavalCombatStrings.Text("Ui.Shell.RelativeToReferenceHeading"))));
        else
            commandRows.Add(new DossierRowSnapshot(NavalCombatStrings.Text("Ui.Shell.IndependentNavigation"), NavalCombatStrings.Text("Ui.Shell.ValueRouteWaypoints", ship.GetManualRouteWaypointCount()),
                ship.HasManualRoute() ? NavalCombatStrings.Text("Ui.Shell.ValueYdRemaining", ship.GetManualRouteRemainingDistanceMeters() * MeasureUtils.meterToYard) : NavalCombatStrings.Text("Ui.Shell.NoManualRoute")));
        var constrained = EntityManager.Instance.Get<ShipLog>(ship.shipLevelFiringTargetObjectId);
        commandRows.Add(constrained == null
            ? new DossierRowSnapshot(NavalCombatStrings.Text("Ui.Shell.AttackConstraint"), NavalCombatStrings.Text("Core.DirectorControlType.None"))
            : ShipReferenceRow(NavalCombatStrings.Text("Ui.Shell.AttackConstraint"), constrained, NavalCombatStrings.Text("Ui.Shell.ThisShipIsRestrictedToThisTarget")));
        commandRows.AddRange(BuildControlledRows(session, ship));

        var machinery = ship.dynamicStatus;
        var machineryFacts = new List<DossierFactSnapshot>
        {
            Fact(NavalCombatStrings.Text("Ui.Shell.EngineRooms"), NavalCombatStrings.Text("Ui.Shell.ValueHitsValueFloodingHits", machinery.engineRoomHits, machinery.engineRoomFloodingHits)),
            Fact(NavalCombatStrings.Text("Ui.Shell.PropulsionShafts"), NavalCombatStrings.Text("Ui.Shell.ValueHits", machinery.propulsionShaftHits)),
            Fact(NavalCombatStrings.Text("Ui.Shell.BoilerRooms"), NavalCombatStrings.Text("Ui.Shell.ValueHitsValueFloodingHits", machinery.boilerRoomHits, machinery.boilerRoomFloodingHits)),
            Fact(NavalCombatStrings.Text("Ui.Shell.MaximumSpeedOffset"), $"{machinery.maxSpeedKnotsOffset:+0.0;-0.0;0.0} kn"),
            Fact(NavalCombatStrings.Text("Ui.Shell.DamageControl"), NavalCombatStrings.Text("Ui.Shell.ValueEffectiveValueHits", ship.GetDamageControlRating(), ship.damageControlRatingHits)),
            Fact(NavalCombatStrings.Text("Ui.Shell.SmokeGenerator"), ship.smokeGeneratorDisabled ? NavalCombatStrings.Text("Core.Disabled") : NavalCombatStrings.Text("Ui.Shell.Available")),
            Fact(NavalCombatStrings.Text("Ui.Shell.PortSearchlight"), Searchlight(ship.searchLightHits.portEnabled, ship.searchLightHits.portHit, ship.searchLightHits.portDirectionDeg)),
            Fact(NavalCombatStrings.Text("Ui.Shell.StarboardSearchlight"), Searchlight(ship.searchLightHits.starboardEnabled, ship.searchLightHits.starboardHit, ship.searchLightHits.starboardDirectionDeg)),
        };
        var effectRows = ship.GetSubStatesDownward()
            .Where(state => state != null)
            .Select(state => new DossierRowSnapshot(
                Friendly(state.GetType().Name),
                state.DescribeLiftCycle(),
                string.Join(" · ", new[]
                {
                    Clean(state.cause),
                    state.damageControlApplied ? NavalCombatStrings.Text("Ui.Shell.DamageControlAssigned") : state.damageControllable ? NavalCombatStrings.Text("Ui.Shell.DamageControllable") : NavalCombatStrings.Text("Ui.Shell.NotDamageControllable")
                }.Where(value => !string.IsNullOrWhiteSpace(value)))))
            .ToList();

        var armamentRows = BuildShipArmamentRows(ship);
        var logRows = ship.logs
            .Where(log => log != null)
            .OrderByDescending(log => log.time)
            .Select(log => new DossierRowSnapshot(
                session.ToScenarioTime(log.time).ToString("yyyy-MM-dd HH:mm:ss", invariant),
                SafeLogSummary(session, log)))
            .ToList();
        var snapshotTime = session.GameState?.scenarioState == null ? NavalCombatStrings.Text("Ui.Shell.UnknownBattleTime") :
            session.ToScenarioTime(session.GameState.scenarioState.dateTime)
                .ToString("yyyy-MM-dd HH:mm:ss 'UTC'zzz", invariant);
        return new DossierSnapshot
        {
            Kind = DossierKind.ShipState,
            ObjectId = ship.objectId,
            Eyebrow = NavalCombatStrings.Text("Ui.Shell.ShipStateBattleSnapshot"),
            Title = NavalDisplayNames.Ship(ship),
            Subtitle = shipClass == null ? NavalCombatStrings.Text("Ui.Shell.UnknownClass") : NavalCombatStrings.Text("Ui.Shell.ValueClassValue", Name(shipClass.name), Friendly(shipClass.type)),
            SnapshotTime = NavalCombatStrings.Text("Ui.Shell.SnapshotTakenAtValue", snapshotTime),
            PlanBitmap = ShipDetailRenderer.RenderBitmap(ship),
            PlanHeadingDeg = ship.headingDeg,
            Links =
            [
                Link(NavalCombatStrings.Text("Ui.Shell.NamedShip"), namedShip == null ? null : Ref(DossierKind.NamedShip, namedShip.objectId, Name(namedShip.name))),
                Link(NavalCombatStrings.Text("Ui.Shell.ShipClass"), shipClass == null ? null : Ref(DossierKind.ShipClass, shipClass.objectId, Name(shipClass.name))),
                Link(NavalCombatStrings.Text("Ui.Shell.CommandingOfficer"), leader == null ? null : Ref(DossierKind.Leader, leader.objectId, Name(leader.name))),
            ],
            Tabs =
            [
                Tab(NavalCombatStrings.Text("Ui.Shell.Situation"), Section(NavalCombatStrings.Text("Ui.Shell.PresentSituation"), situation)),
                Tab(NavalCombatStrings.Text("Ui.Command"), Section(NavalCombatStrings.Text("Ui.Shell.OrdersAndDoctrine"), commandFacts, commandRows)),
                Tab(NavalCombatStrings.Text("Ui.Shell.DamageMachinery"), Section(NavalCombatStrings.Text("Ui.Shell.MachineryAndDamageControl"), machineryFacts), Section(NavalCombatStrings.Text("Ui.Shell.ActiveEffects"), rows: effectRows, note: effectRows.Count == 0 ? NavalCombatStrings.Text("Ui.Shell.NoActiveDamageEffects") : "")),
                Tab(NavalCombatStrings.Text("Ui.Shell.Armament"), Section(NavalCombatStrings.Text("Ui.Shell.WeaponsAndExpenditure"), rows: armamentRows, note: armamentRows.Count == 0 ? NavalCombatStrings.Text("Ui.Shell.NoArmamentIsRecorded") : "")),
                new DossierTabSnapshot(Resources.NavalCombatStrings.GetUi("History"), ShipHistoryBuilder.Build(session, ship)
                    .Select(chart => new DossierSectionSnapshot(chart.Title, [], [], HistoryChart: chart)).ToArray()),
                Tab(NavalCombatStrings.Text("Ui.Shell.ShipLog"), Section(NavalCombatStrings.Text("Ui.Shell.ShipSpecificActionRecord"), rows: logRows, note: logRows.Count == 0 ? NavalCombatStrings.Text("Ui.Shell.NoShipLogEntriesHaveBeenRecorded") : "", virtualized: true)),
            ],
        };
    }

    static IReadOnlyList<DossierRowSnapshot> BuildClassTorpedoRows(ShipClass shipClass)
    {
        var sector = shipClass?.torpedoSector;
        if (sector?.mountLocationRecords == null || sector.mountLocationRecords.Count == 0)
            return [];
        var meta = sector.metaInfo;
        var settings = string.Join(" · ", sector.torpedoSettings.Select(setting => $"{setting.speedKnots:0.#} kn / {Yards(setting.rangeYards)}"));
        var torpedoDetails = new List<string>();
        if (meta != null)
            torpedoDetails.Add(NavalCombatStrings.Text("Ui.Shell.WarheadValueLb", meta.warheadLb));
        torpedoDetails.Add(NavalCombatStrings.Text("Ui.Shell.DamageClassValue", sector.damageClass));
        torpedoDetails.Add(NavalCombatStrings.Text("Ui.Shell.ValuePistol", Friendly(sector.pistolType)));
        torpedoDetails.Add(NavalCombatStrings.Text("Ui.Shell.DudProbabilityValue", sector.dudProbability));
        return
        [
            new DossierRowSnapshot(
                Name(sector.name, NavalCombatStrings.Text("Ui.Shell.TorpedoBattery")),
                NavalCombatStrings.Text("Ui.Shell.ValueTubesValueTorpedoes", sector.mountLocationRecords.Sum(mount => mount.barrels), sector.ammunitionCapacity),
                $"{string.Join(" · ", torpedoDetails)}\n"
                + NavalCombatStrings.Text("Ui.Shell.SettingsValueNvalue", (string.IsNullOrWhiteSpace(settings) ? NavalCombatStrings.Text("Core.DirectorControlType.None") : settings), Mounts(sector.mountLocationRecords, includeTrainability: true)),
                Ref(DossierKind.Torpedo, shipClass.objectId, NavalCombatStrings.Text("Ui.Shell.OpenDiagram")))
        ];
    }

    static IReadOnlyList<DossierRowSnapshot> BuildShipArmamentRows(ShipLog ship)
    {
        var rows = new List<DossierRowSnapshot>();
        foreach (var status in ship.batteryStatus)
        {
            var record = status.GetBatteryRecord();
            if (record == null)
                continue;
            var total = record.mountLocationRecords.Sum(mount => mount.barrels);
            var available = status.mountStatus.Where(mount => mount.IsOperational()).Sum(mount => mount.barrels);
            var ammo = $"AP {status.ammunition.ArmorPiercing} · SAP {status.ammunition.semiArmorPiercing} · COM {status.ammunition.common} · HE {status.ammunition.highExplosive}";
            rows.Add(new DossierRowSnapshot(Name(record.name), NavalCombatStrings.Text("Ui.Shell.ValueValueValueBarrels", Inches(record.shellSizeInch), available, total), ammo));
            foreach (var group in status.mountStatus.Where(mount => mount.GetFiringTarget() != null).GroupBy(mount => mount.GetFiringTarget().objectId))
            {
                var first = group.First();
                var target = first.GetFiringTarget();
                rows.Add(ShipReferenceRow(
                    NavalCombatStrings.Text("Ui.Shell.CurrentTarget"),
                    target,
                    NavalCombatStrings.Text("Ui.Shell.ValueMountsValueSProcessedValueValue", group.Count(), first.processSeconds, Friendly(first.ammunitionType), FireControl(status, target))));
            }
        }
        foreach (var (status, index) in ship.rapidFiringStatus.Select((status, index) => (status, index)))
        {
            var record = status.GetRapidFireBatteryRecord();
            if (record == null)
                continue;
            var portTotal = record.barrelsLevelPort.FirstOrDefault();
            var starboardTotal = record.barrelsLevelStarboard.FirstOrDefault();
            rows.Add(new DossierRowSnapshot(
                $"RF{index + 1} · {Name(record.name)}",
                NavalCombatStrings.Text("Ui.Shell.PortValueValueStarboardValueValue", status.GetAvailableBarrels(RapidFiringBatteryLocation.Port), portTotal, status.GetAvailableBarrels(RapidFiringBatteryLocation.Starboard), starboardTotal),
                NavalCombatStrings.Text("Ui.Shell.AmmunitionValueFireControlHitsValue", status.ammunition, status.fireControlHits)));
            foreach (var targetting in status.targettingRecords.Where(targetting => targetting?.GetTarget() != null && targetting.allocated > 0))
            {
                rows.Add(ShipReferenceRow(
                    NavalCombatStrings.Text("Ui.Shell.CurrentTarget"),
                    targetting.GetTarget(),
                    NavalCombatStrings.Text("Ui.Shell.ValueGunsValueSProcessedValue", targetting.allocated, targetting.processingSeconds, Friendly(targetting.location))));
            }
        }
        var torpedo = ship.shipClass?.torpedoSector;
        if (torpedo?.mountLocationRecords?.Count > 0)
        {
            var total = torpedo.mountLocationRecords.Sum(mount => mount.barrels);
            var loaded = ship.torpedoSectorStatus.mountStatus.Where(mount => mount.IsOperational()).Sum(mount => mount.currentLoad);
            rows.Add(new DossierRowSnapshot(
                Name(torpedo.name, NavalCombatStrings.Text("Ui.Shell.TorpedoBattery")),
                NavalCombatStrings.Text("Ui.Shell.ValueValueTubesLoadedValueTorpedoesAvailable", loaded, total, ship.torpedoSectorStatus.GetAmmunitionMagazinePlusLoaded()),
                TorpedoReloads(ship)));
            foreach (var group in ship.torpedoSectorStatus.mountStatus.Where(mount => mount.GetFiringTarget() != null).GroupBy(mount => mount.GetFiringTarget().objectId))
            {
                var target = group.First().GetFiringTarget();
                rows.Add(ShipReferenceRow(NavalCombatStrings.Text("Ui.Shell.CurrentTarget"), target, NavalCombatStrings.Text("Ui.Shell.ValueTubesAllocated", group.Sum(mount => mount.barrels))));
            }
        }
        return rows;
    }

    static IEnumerable<DossierRowSnapshot> BuildControlledRows(BattleSession session, ShipLog controller)
    {
        var ships = session.GameState?.shipLogsOnMap ?? [];
        var visited = new HashSet<string>(StringComparer.Ordinal) { controller.objectId };
        var queue = new Queue<(ShipLog Ship, int Depth)>();
        queue.Enqueue((controller, 0));
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var child in ships.Where(candidate => candidate.GetControlPredecessorOnMap() == current.Ship))
            {
                if (!visited.Add(child.objectId))
                    continue;
                queue.Enqueue((child, current.Depth + 1));
                yield return ShipReferenceRow(
                    current.Depth == 0 ? NavalCombatStrings.Text("Ui.Shell.DirectlyControlledShip") : NavalCombatStrings.Text("Ui.Shell.IndirectlyControlledShip"),
                    child,
                    Friendly(child.GetEffectiveControlMode()));
            }
        }
    }

    static DossierRowSnapshot ShipReferenceRow(string heading, ShipLog ship, string detail) => ship == null
        ? new DossierRowSnapshot(heading, NavalCombatStrings.Text("Ui.Shell.NotAssigned"), detail)
        : new DossierRowSnapshot(heading, NavalDisplayNames.Ship(ship), detail, Ref(DossierKind.ShipState, ship.objectId, NavalDisplayNames.Ship(ship)));

    static string FireControl(BatteryStatus battery, ShipLog target)
    {
        var states = battery.fireControlSystemStatusRecords
            .Where(record => record.IsOperational() && record.targetObjectId == target?.objectId)
            .Select(record => record.trackingState)
            .ToHashSet();
        if (states.Contains(TrackingSystemState.Hitting)) return NavalCombatStrings.Text("Core.TrackingSystemState.Hitting");
        if (states.Contains(TrackingSystemState.BeginTracking)) return NavalCombatStrings.Text("Ui.Shell.BeginTracking");
        if (states.Contains(TrackingSystemState.Tracking)) return NavalCombatStrings.Text("Core.TrackingSystemState.Tracking");
        return NavalCombatStrings.Text("Ui.Shell.NoFireControlSolution");
    }

    static string TorpedoReloads(ShipLog ship)
    {
        if (TorpedoMountStatusRecord.disableTorpedoReload)
            return NavalCombatStrings.Text("Ui.Shell.ReloadingDisabled");
        var values = ship.torpedoSectorStatus.mountStatus
            .Where(mount => mount.IsOperational() && mount.currentLoad < mount.barrels && mount.reloadingSeconds > 0)
            .Select(mount => Math.Max(0, (int)Math.Ceiling(360d - mount.reloadingSeconds)))
            .GroupBy(seconds => seconds)
            .OrderBy(group => group.Key)
            .Select(group => group.Count() == 1 ? $"{group.Key} s" : $"{group.Key} s ×{group.Count()}")
            .ToList();
        return values.Count == 0 ? NavalCombatStrings.Text("Ui.Shell.NoTubesReloading") : NavalCombatStrings.Text("Ui.Shell.ReloadingValue", string.Join(" · ", values));
    }

    static string SafeLogSummary(BattleSession session, ShipLogLog log)
    {
        try { return ShipHistoryBuilder.LogSummary(session, log); }
        catch { return Friendly(log.GetType().Name); }
    }

    static string Searchlight(bool enabled, int hits, double direction) => hits > 0
        ? NavalCombatStrings.Text("Ui.Shell.DisabledValueHits", hits)
        : enabled ? NavalCombatStrings.Text("Ui.Shell.EnabledValue", direction) : NavalCombatStrings.Text("Ui.Shell.Stowed");

    static string Doctrine(Inheriable<AutomaticType> setting, AutomaticType effective) => setting.isInherited
        ? NavalCombatStrings.Text("Ui.Shell.InheritedValue", Friendly(effective))
        : NavalCombatStrings.Text("Ui.Shell.ExplicitValue", Friendly(setting.value));

    static DossierReference Ref(DossierKind kind, string id, string name) => new(kind, id, name);
    static DossierLinkSnapshot Link(string label, DossierReference reference) => reference == null
        ? new DossierLinkSnapshot(label, NavalCombatStrings.Text("Ui.Shell.NotAssigned"), null, NavalCombatStrings.Text("Ui.Shell.NoAssociatedRecordIsAvailable"))
        : new DossierLinkSnapshot(label, reference.Name, reference);
    static DossierFactSnapshot Fact(string label, string value) => new(label, string.IsNullOrWhiteSpace(value) ? NavalCombatStrings.Text("Ui.Shell.NotRecorded") : value);
    static DossierArmorRowSnapshot Armor(string label, ArmorRatingReocrd armor) => new(
        label,
        armor?.actualInch ?? 0d,
        armor?.effectInch ?? 0d);
    static DossierTabSnapshot Tab(string title, params DossierSectionSnapshot[] sections) => new(title, sections);
    static DossierSectionSnapshot Section(
        string title,
        IReadOnlyList<DossierFactSnapshot> facts = null,
        IReadOnlyList<DossierRowSnapshot> rows = null,
        string note = "",
        bool virtualized = false,
        bool showShipPlan = false,
        IReadOnlyList<DossierArmorRowSnapshot> armorTableRows = null)
        => new(title, facts ?? [], rows ?? [], note, virtualized, showShipPlan, armorTableRows ?? []);

    static IReadOnlyList<DossierMountSectorSnapshot> BuildMountSectors(IEnumerable<MountRecord> source)
    {
        var allRecords = source?.ToList() ?? [];
        return Enum.GetValues(typeof(MountLocation)).Cast<MountLocation>()
            .Where(location => location != MountLocation.NotSpecified)
            .Select(location =>
            {
                var records = allRecords.Where(record => record.mountLocation == location).ToList();
                return new DossierMountSectorSnapshot(
                    location,
                    records.Count(),
                    records.Sum(record => record.barrels),
                    records.SelectMany(record => record.mountArcs)
                        .Select(arc => new DossierArcSnapshot(arc.startDeg, arc.CoverageDeg, arc.isCrossDeckFire))
                        .ToList());
            })
            .ToList();
    }

    static void AddFact(List<DossierFactSnapshot> facts, string label, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            facts.Add(Fact(label, value));
    }

    static void AddTextRow(List<DossierRowSnapshot> rows, string heading, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            rows.Add(new DossierRowSnapshot(heading, value, IsMarkdown: true));
    }

    static string Name(GlobalString value, string fallback = null) =>
        NavalDisplayNames.Text(value) ?? fallback ?? NavalCombatStrings.Text("Ui.Shell.Unnamed");

    static string Text(GlobalString value) => NavalDisplayNames.Text(value);

    static string AlternateNames(GlobalString value)
    {
        var primary = Name(value);
        var alternatives = new[]
        {
            (NavalCombatStrings.Text("Ui.Japanese"), value?.japanese),
            (NavalCombatStrings.Text("Ui.Shell.SimplifiedChinese"), value?.chineseSimplified),
            (NavalCombatStrings.Text("Ui.Shell.TraditionalChinese"), value?.chineseTraditional),
        }.Select(item => (item.Item1, Value: Meaningful(item.Item2)))
            .Where(item => item.Value != null && !string.Equals(item.Value, primary, StringComparison.OrdinalIgnoreCase))
            .Select(item => $"{item.Item1}: {item.Value}")
            .ToList();
        return string.Join(" · ", alternatives);
    }

    static string Meaningful(string value)
    {
        value = value?.Trim();
        return string.IsNullOrWhiteSpace(value) || string.Equals(value, "none", StringComparison.OrdinalIgnoreCase)
            ? null
            : value;
    }

    static string Clean(string value) => Meaningful(value);
    static string Signed(int value) => value.ToString("+0;-0;0", invariant);
    static string Tons(double value) => NavalCombatStrings.Text("Ui.Shell.ValueTons", value);
    static string Knots(double value) => $"{value:0.0} kn";
    static string Yards(double value) => $"{value:N0} yd";
    static string Inches(double value) => $"{value:0.##} in";
    static string Friendly(object value) => value == null ? NavalCombatStrings.Text("Ui.Shell.NotRecorded")
        : value is Enum item ? NavalCombatStrings.EnumLabel(item) : SplitPascalCase(value.ToString());

    static string SplitPascalCase(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return NavalCombatStrings.Text("Ui.Shell.NotRecorded");
        return string.Concat(value.Select((character, index) =>
            index > 0 && char.IsUpper(character) && !char.IsUpper(value[index - 1])
                ? $" {character}"
                : character.ToString()));
    }

    static string Mounts(IEnumerable<MountRecord> records, bool includeTrainability) => string.Join(" · ", records.Select(record =>
        NavalCombatStrings.Text("Ui.Shell.ValueValueAtValue", 1, record.barrels, Friendly(record.mountLocation))
        + (includeTrainability ? $" · {((record is TorpedoMountRecord { trainable: true }) ? NavalCombatStrings.Text("Ui.Shell.TrainableTube") : NavalCombatStrings.Text("Ui.Shell.FixedTube"))}" : "")
        + NavalCombatStrings.Text("Ui.Shell.ArcsValue", record.SummaryArcs())));

    static string RapidFireCaliber(RapidFireBatteryRecord record) => record.metaInfo == null
        ? NavalCombatStrings.Text("Ui.Shell.ValueModelDefault", Inches(record.GetShellSizeInch()))
        : Inches(record.GetShellSizeInch());

    static string RapidFireControl(RapidFireBatteryRecord record)
    {
        if (record.metaInfo != null)
            return NavalCombatStrings.Text("Ui.Shell.FireControlTierValue", record.metaInfo.fireControlTier);
        return record.fireControlRecords.Count > 0
            ? NavalCombatStrings.Text("Ui.Shell.FireControlTableValueRangeLevelsTierNotRecorded", record.fireControlRecords.Count)
            : NavalCombatStrings.Text("Ui.Shell.FireControlDataNotRecorded");
    }

    static string Skill(LeaderSkillLevel value)
    {
        if (value == LeaderSkillLevel.Unknown)
            return NavalCombatStrings.Text("Core.LeaderSkillLevel.Unknown");
        var level = Math.Clamp((int)value, 1, 5);
        return $"{new string('■', level)}{new string('□', 5 - level)}  {Friendly(value)}";
    }

    static string Monogram(string name)
    {
        var words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return string.Concat(words.Take(2).Select(word => char.ToUpperInvariant(word[0])));
    }
}
