using System.Collections.Generic;

public enum ShipGraphicPartKind { Deckhouse, Funnel, Mast, Gun, RapidFire, DeckTorpedo, SubmergedTorpedo, Bridge, Deck }
public enum ShipGunAppearance { Open, Shield, Turret, Casemate, FixedMount }
public enum TorpedoInstallation { Automatic, Surface, Underwater }

/// <summary>Appearance only. Positions use beam/length fractions, dimensions use metres.
/// Zero dimensions infer defaults; tubePivot is the fraction of tube length behind its pivot.</summary>
public sealed class TorpedoGraphicParameters
{
    public TorpedoInstallation installation;
    public bool customPosition;
    public double x, y, z;
    public double tubeLength, tubeDiameter, tubeSpacing, tubeAxisHeight;
    public double tubePivot = .5;
    public double tubeSplay;
    public double stowedAngle;
}

public enum ShipGunHousingShape { Beveled, Circular }
public enum ShipStructureStyle { Standard, ClosedBridge, RoundFunnel, TripodMast, BeveledDeckhouse }

/// <summary>Optional archival appearance overrides. XY and footprint use beam/length fractions;
/// vertical coordinates and barrel lengths are metres above the waterline.</summary>
public sealed class ShipGraphicParameters
{
    public string source = "";
    public string notes = "";
    public double freeboard = 4;
    public double bowFullness = 1;
    public double sternFullness = .65;
    public bool replaceStructures;
    public List<ShipGraphicStructure> structures = new();
    public string mainBridgeId = "";
    public List<ShipGraphicMountOverride> mounts = new();
    public bool customObserver;
    public double observerX, observerY, observerZ;
}

/// <summary>XY are offsets from the parent centre (or hull centre), in beam/length fractions.
/// Z is metres above the parent roof, main deck when attachedToDeck, or waterline for legacy absolute placement.</summary>
public sealed class ShipGraphicStructure
{
    public string objectId = "", parentId = "", mirrorId = "";
    public CoreUtils.GlobalString name = new(){english=""};
    public bool attachedToDeck;
    public ShipGraphicPartKind kind = ShipGraphicPartKind.Deckhouse;
    public double x, y, z = 4, width = .4, length = .1, height = 2;
    public ShipStructureStyle style;
    public double wingWidth, crossbarWidth, rakeDegrees;
}

public sealed class ShipGraphicMountOverride
{
    public bool automaticPosition;
    public string batteryId = "", mountRecordId = "";
    public double x, y, z = 4, width = .12, length = .035;
    // Metres; zero infers height/base diameter from the appearance. Base is distinct from gunhouse.
    public double height, barbetteDiameter;
    // Zero uses Battery calibres (30 if unknown); a positive exposed length takes precedence.
    public double barrelLength, exposedBarrelLength, restAngle;
    public ShipGunAppearance appearance = ShipGunAppearance.Shield;
    public ShipGunHousingShape housingShape;
    public bool fixedHousing;
}

/// <summary>Plan coordinates: X is starboard / beam, Y is forward / length, origin at hull centre.
/// Sizes are fractions of beam and length respectively. Heights are deliberately not historical data.</summary>
public sealed record ShipGraphicPart(ShipGraphicPartKind Kind, double X, double Y,
    double Width, double Length, double DirectionX, double DirectionY, int Barrels)
{
    public double Z { get; init; }
    public double Height { get; init; }
    public string StructureId { get; init; } = "";
    public ShipStructureStyle StructureStyle { get; init; }
    public double WingWidth { get; init; }
    public double CrossbarWidth { get; init; }
    public double RakeDegrees { get; init; }
    public double BarbetteDiameter { get; init; }
    public double BarrelLength { get; init; }
    // Ship-space displacement in metres from the fixed housing centre to the visual swivel.
    public (double X,double Y) GunPivotOffset { get; init; }
    public double Caliber { get; init; }
    public double BarrelStart { get; init; }
    public double BarrelFace { get; init; }
    public double ExposedBarrelLength { get; init; }
    public string BatteryId { get; init; } = "";
    public string MountRecordId { get; init; } = "";
    public int BatteryIndex { get; init; } = -1;
    public int RecordIndex { get; init; } = -1;
    public double RestAngle { get; init; }
    public ShipGunAppearance Appearance { get; init; }
    public ShipGunHousingShape HousingShape { get; init; }
    public bool FixedHousing { get; init; }
    public double TubeSpacing { get; init; }
    public double TubeSplay { get; init; }
    public bool Trainable { get; init; }
}

public sealed class ShipClassGraphicLayout
{
    public List<(double X, double Y)> Hull { get; } = new();
    public List<ShipGraphicPart> Parts { get; } = new();
    public double Freeboard { get; set; }
    public (double X, double Y, double Z) Observer { get; set; }
    public List<string> Warnings { get; } = new();
}
