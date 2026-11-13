namespace NavalCombat.Tests;

public sealed class TorpedoCatalogConsistencyTests
{
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
