using System.Collections.Generic;

public enum ShipGraphicPartKind { Deckhouse, Funnel, Mast, Gun, RapidFire, DeckTorpedo, SubmergedTorpedo, Bridge, Deck }
public enum ShipGunAppearance { Open, Shield, Turret, Casemate }
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
    public List<ShipGraphicMountOverride> mounts = new();
    public bool customObserver;
    public double observerX, observerY, observerZ;
}

public sealed class ShipGraphicStructure
{
    public ShipGraphicPartKind kind = ShipGraphicPartKind.Deckhouse;
    public double x, y, z = 4, width = .4, length = .1, height = 2;
}

public sealed class ShipGraphicMountOverride
{
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
    public double BarbetteDiameter { get; init; }
    public double BarrelLength { get; init; }
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
