using NavalCombatCore;

namespace NavalCombat.Application;

public static class ShipClassPlaceholderRenderAdapter
{
    public static ShipClassPlaceholderRenderInput CreateRenderInput(ShipClass shipClass)
    {
        if (shipClass == null)
            return null;

        return new ShipClassPlaceholderRenderInput
        {
            graphicParameters = shipClass.graphicParameters,
            name = shipClass.name?.GetMergedName() ?? shipClass.name?.english ?? "ShipClass",
            type = ToPlaceholderShipType(shipClass.type),
            displacementTons = shipClass.displacementTons,
            lengthFoot = shipClass.lengthFoot,
            beamFoot = shipClass.beamFoot,
            speedKnots = shipClass.speedKnots,
            batteryRecords = (shipClass.batteryRecords ?? [])
                .Select(battery => new PlaceholderBatteryRenderRecord
                {
                    objectId = battery.objectId,
                    shellSizeInch = battery.shellSizeInch,
                    barrelLengthCalibers = battery.barrelLengthCalibers,
                    mountLocationRecords = (battery.mountLocationRecords ?? [])
                        .Select(CreateMountRecord)
                        .ToList()
                })
                .ToList(),
            torpedoDiameterIn = shipClass.torpedoSector?.metaInfo?.diameterIn ?? 0,
            torpedoMountRecords = (shipClass.torpedoSector?.mountLocationRecords ?? [])
                .Select(CreateMountRecord)
                .ToList(),
            rapidFireBatteryRecords = (shipClass.rapidFireBatteryRecords ?? [])
                .Select(record => new PlaceholderRapidFireRenderRecord
                {
                    barrelsLevelPort = new List<int>(record.barrelsLevelPort ?? []),
                    barrelsLevelStarboard = new List<int>(record.barrelsLevelStarboard ?? [])
                })
                .ToList()
        };
    }

    static PlaceholderMountRenderRecord CreateMountRecord(MountRecord record)
    {
        if (record == null)
            return new PlaceholderMountRenderRecord();

        return new PlaceholderMountRenderRecord
        {
            objectId = record.objectId,
            casemate = record.mountArcsPattern == MountArcsPattern.Casemate,
            fixedMount = record is GunMountRecord { mountType: GunMountType.FixedMlrBlr },
            mountLocation = ToPlaceholderMountLocation(record.mountLocation),
            barrels = record.barrels,

            trainable = record is TorpedoMountRecord { trainable: true },
            torpedoGraphic = (record as TorpedoMountRecord)?.torpedoGraphic,
            mountArcs = (record.mountArcs ?? [])
                .Select(arc => new PlaceholderMountArcRenderRecord
                {
                    startDeg = arc.startDeg,
                    CoverageDeg = arc.CoverageDeg,
                    isCrossDeckFire = arc.isCrossDeckFire
                })
                .ToList()
        };
    }

    static PlaceholderShipType ToPlaceholderShipType(ShipType shipType) =>
        Enum.TryParse(shipType.ToString(), out PlaceholderShipType placeholderType)
            ? placeholderType
            : PlaceholderShipType.NotSpecified;

    static PlaceholderMountLocation ToPlaceholderMountLocation(MountLocation mountLocation) =>
        Enum.TryParse(mountLocation.ToString(), out PlaceholderMountLocation placeholderLocation)
            ? placeholderLocation
            : PlaceholderMountLocation.NotSpecified;
}
