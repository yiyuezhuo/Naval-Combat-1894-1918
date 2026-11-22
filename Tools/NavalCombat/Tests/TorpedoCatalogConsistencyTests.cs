using NavalCombat.Application;

namespace NavalCombat.Tests;

public sealed class TorpedoCatalogConsistencyTests
{
    [Theory]
    [InlineData(TorpedoInstallation.Automatic,false,ShipGraphicPartKind.SubmergedTorpedo)]
    [InlineData(TorpedoInstallation.Automatic,true,ShipGraphicPartKind.DeckTorpedo)]
    [InlineData(TorpedoInstallation.Surface,false,ShipGraphicPartKind.DeckTorpedo)]
    [InlineData(TorpedoInstallation.Underwater,true,ShipGraphicPartKind.SubmergedTorpedo)]
    [InlineData(TorpedoInstallation.AboveWaterInHull,false,ShipGraphicPartKind.HullTorpedoPort)]
    [InlineData(TorpedoInstallation.AboveWaterUnspecified,true,ShipGraphicPartKind.UnspecifiedTorpedo)]
    public void InstallationControlsAppearanceIndependentlyOfTrainability(
        TorpedoInstallation installation,bool trainable,ShipGraphicPartKind expected)
    {
        var input=new ShipClassPlaceholderRenderInput { type=PlaceholderShipType.ArmoredCruiser };
        input.torpedoMountRecords.Add(new PlaceholderMountRenderRecord {
            objectId="tube",mountLocation=PlaceholderMountLocation.PortMidship,trainable=trainable,
            torpedoGraphic=installation==TorpedoInstallation.Automatic?null:new() {installation=installation}
        });
        var part=ShipGraphicLayoutBuilder.Build(input).Parts.Single(p=>p.MountRecordId=="tube");
        Assert.Equal(expected,part.Kind);
        Assert.Equal(trainable,part.Trainable);
    }

    [Fact]
    public void ChihYuanKeepsFourScenarioMountsWithOnlyTwoVisibleHullPorts()
    {
        var session=ScenarioFixture.LoadSession("SJS - Chih Yuan vs Naniwa.scen.xml");
        var ship=session.GameState.shipLogs.Single(s=>s.shipClass.name.english=="Chih Yuan");
        var input=ShipClassPlaceholderRenderAdapter.CreateRenderInput(ship.shipClass);
        var layout=ShipGraphicLayoutBuilder.Build(input);
        var parts=layout.Parts.Where(p=>p.RecordIndex>=0 && p.BatteryIndex<0).ToList();
        Assert.Equal(4,parts.Count);
        Assert.Equal(ship.torpedoSectorStatus.mountStatus.Select(s=>s.mountRecordId),parts.Select(p=>p.MountRecordId));
        Assert.Equal(2,parts.Count(p=>p.Kind==ShipGraphicPartKind.UnspecifiedTorpedo));
        var ports=parts.Where(p=>p.Kind==ShipGraphicPartKind.HullTorpedoPort).OrderBy(p=>p.X).ToList();
        Assert.Equal(2,ports.Count);
        Assert.True(ports[0].X<0 && ports[1].X>0);
        Assert.All(ports,p=>
        {
            Assert.True(p.Z>0 && p.Z<layout.Freeboard);
            Assert.True(p.Trainable);
            input.torpedoMountRecords[p.RecordIndex].mountDirectionDeg=45;
            Assert.Equal(p.RestAngle,ShipGraphicLayoutBuilder.Direction(p,input));
        });
    }

    [Fact]
    public void AuthoredScenariosHaveOneStatusPerPhysicalTorpedoMount()
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "TestData");
        foreach (var path in Directory.GetFiles(directory, "*.scen.xml"))
        {
            var session = ScenarioFixture.LoadSession(Path.GetFileName(path));
            foreach (var ship in session.GameState.shipLogs)
            {
                foreach (var battery in ship.batteryStatus)
                {
                    var records = battery.GetBatteryRecord().mountLocationRecords;
                    Assert.Equal(records.Count, battery.mountStatus.Count);
                    Assert.Equal(records.Select(m => m.objectId), battery.mountStatus.Select(m => m.mountRecordId));
                    Assert.All(battery.mountStatus, m => Assert.NotNull(m.GetMountRecordInfo()));
                }
                var expected = ship.shipClass.torpedoSector.mountLocationRecords
                    .Select(m => m.barrels).ToArray();
                var actual = ship.torpedoSectorStatus.mountStatus;
                Assert.Equal(ship.shipClass.torpedoSector.mountLocationRecords.Select(m => m.objectId),
                    actual.Select(m => m.mountRecordId));
                Assert.True(expected.SequenceEqual(actual.Select(m => m.barrels)),
                    $"{Path.GetFileName(path)}: {ship.namedShip.name.english}");
                Assert.All(actual, m => Assert.InRange(m.currentLoad, 0, m.barrels));
                Assert.True(ship.torpedoSectorStatus.ammunition >= 0);
                Assert.Equal(actual.Count, actual.Select(m => m.objectId).Distinct().Count());
            }
        }
    }
}
