using NavalCombatCore;
using System.Xml.Serialization;

namespace NavalCombat.Tests;

public sealed class IndividualMountTests
{
    static string Xml<T>(T value)
    {
        using var writer = new StringWriter();
        new XmlSerializer(typeof(T)).Serialize(writer, value);
        return writer.ToString();
    }

    static T Copy<T>(T value) => (T)new XmlSerializer(typeof(T)).Deserialize(new StringReader(Xml(value)))!;

    [Fact]
    public void ExplicitInstallationEditsRetainSurvivorsAndDoNotCreateTorpedoAmmunition()
    {
        var game = ScenarioFixture.LoadSession().GameState;
        var ship = game.shipLogs.First(s => s.batteryStatus.Any(b => b.mountStatus.Count > 1)
            && s.torpedoSectorStatus.mountStatus.Count > 0);
        var battery = ship.batteryStatus.First(b => b.mountStatus.Count > 1);
        var retained = battery.mountStatus[0];
        var deleted = battery.mountStatus[1];
        retained.status = MountStatus.Disabled;
        retained.firingTargetObjectId = "retained-target";
        var before = Xml(retained);
        var records = battery.GetBatteryRecord().mountLocationRecords;
        records.Remove(deleted.MountRecord);
        records.Insert(0, new() { objectId = "added-gun", barrels = 2 });
        ship.shipClass.torpedoSector.mountLocationRecords.Add(new() { objectId = "added-tubes", barrels = 3 });
        var magazine = ship.torpedoSectorStatus.ammunition;
        var loaded = ship.torpedoSectorStatus.mountStatus.Sum(m => m.currentLoad);
        game.SynchronizeMountStates();
        Assert.Equal(before, Xml(retained));
        Assert.Contains(retained, battery.mountStatus);
        Assert.DoesNotContain(deleted, battery.mountStatus);
        Assert.Null(CoreUtils.EntityManager.Instance.Get<MountStatusRecord>(deleted.objectId));
        Assert.Equal(2, battery.mountStatus.Single(m => m.mountRecordId == "added-gun").barrels);
        var added = ship.torpedoSectorStatus.mountStatus.Single(m => m.mountRecordId == "added-tubes");
        Assert.Equal(0, added.currentLoad);
        Assert.Equal(magazine, ship.torpedoSectorStatus.ammunition);
        Assert.Equal(loaded, ship.torpedoSectorStatus.mountStatus.Sum(m => m.currentLoad));
        game.RebindMountRecords();
        Assert.NotNull(added.MountRecord);
    }

    [Fact]
    public void RebindingReplacesReferencesWithoutChangingAnySerializedCombatState()
    {
        var game = ScenarioFixture.LoadSession().GameState;
        var ship = game.shipLogs.First(s => s.batteryStatus.Any(b => b.mountStatus.Count > 1)
            && s.torpedoSectorStatus.mountStatus.Count > 0);
        var battery = ship.batteryStatus.First(b => b.mountStatus.Count > 1);
        var gun = battery.mountStatus[0];
        var tube = ship.torpedoSectorStatus.mountStatus[0];
        gun.status = MountStatus.Disabled;
        gun.firingTargetObjectId = "retained-target";
        gun.processSeconds = 7;
        tube.currentLoad = 0;
        tube.reloadedLoad = 3;
        tube.reloadingSeconds = 12;
        tube.firingTargetObjectId = "retained-torpedo-target";
        var before = Xml(ship);
        var oldGun = gun.MountRecord;
        var oldTube = tube.MountRecord;
        var guns = battery.GetBatteryRecord().mountLocationRecords;
        var tubes = ship.shipClass.torpedoSector.mountLocationRecords;
        var replacementGun = Copy(oldGun);
        var replacementTube = Copy(oldTube);
        guns[guns.IndexOf(oldGun)] = replacementGun;
        tubes[tubes.IndexOf(oldTube)] = replacementTube;
        game.RebindMountRecords();
        Assert.Same(replacementGun, gun.MountRecord);
        Assert.Same(replacementTube, tube.MountRecord);
        Assert.Equal(before, Xml(ship));
        Assert.DoesNotContain("<MountRecord>", before);
        replacementGun.barrels = 4;
        Assert.Equal(4, gun.MountRecord.barrels);
    }

    [Fact]
    public void MissingMountRejectsRebindingBeforePublishingAnyReplacement()
    {
        var game = ScenarioFixture.LoadSession().GameState;
        var battery = game.shipLogs.SelectMany(s => s.batteryStatus).First(b => b.mountStatus.Count > 1);
        var gun = battery.mountStatus[0];
        var other = battery.mountStatus[1];
        var old = gun.MountRecord;
        var records = battery.GetBatteryRecord().mountLocationRecords;
        records[records.IndexOf(old)] = Copy(old);
        records.Remove(other.MountRecord);
        Assert.Throws<InvalidOperationException>(game.RebindMountRecords);
        Assert.Same(old, gun.MountRecord);
        Assert.Throws<InvalidOperationException>(() => gun.Bind(new() { objectId = "wrong-id" }));
        Assert.Same(old, gun.MountRecord);
    }

    [Fact]
    public void GunAndTorpedoRecordsRoundTripAsDistinctSingleMountTypes()
    {
        var ship = new ShipClass
        {
            batteryRecords = [new() { mountLocationRecords = [new()
                { objectId = "gun", barrels = 2, mountLocation = MountLocation.Forward }] }],
            torpedoSector = new() { mountLocationRecords = [new()
                { objectId = "tube", barrels = 3, trainable = true, reloadLimit = 6 }] }
        };
        var serializer = new XmlSerializer(typeof(ShipClass));
        using var text = new StringWriter();
        serializer.Serialize(text, ship);
        var xml = text.ToString();
        Assert.Contains("<GunMountRecord>", xml);
        Assert.Contains("<TorpedoMountRecord>", xml);
        Assert.DoesNotContain("<MountLocationRecord>", xml);
        Assert.DoesNotContain("<mounts>", xml);
        Assert.Null(typeof(GunMountRecord).GetField("trainable"));
        Assert.Null(typeof(GunMountRecord).GetField("reloadLimit"));
        var copy = (ShipClass)serializer.Deserialize(new StringReader(xml))!;
        Assert.Equal(2, Assert.Single(copy.batteryRecords[0].mountLocationRecords).barrels);
        var launcher = Assert.Single(copy.torpedoSector.mountLocationRecords);
        Assert.Equal("tube", launcher.objectId);
        Assert.True(launcher.trainable);
        Assert.Equal(6, launcher.reloadLimit);
    }

    [Fact]
    public void RuntimeMountsResolveTheirIdentityAfterClassAndStatusReordering()
    {
        var session = ScenarioFixture.LoadSession();
        var ship = session.GameState.shipLogs.First(s => s.batteryStatus.Any(b => b.mountStatus.Count > 1));
        var battery = ship.batteryStatus.First(b => b.mountStatus.Count > 1);
        var states = battery.mountStatus.ToArray();
        var records = states.Select(m => m.GetMountRecordInfo().record).ToArray();
        battery.GetBatteryRecord().mountLocationRecords.Reverse();
        battery.mountStatus.Reverse();
        for (var i = 0; i < states.Length; i++)
        {
            Assert.Equal(records[i].objectId, states[i].mountRecordId);
            Assert.Same(records[i], states[i].GetMountRecordInfo().record);
            Assert.Same(records[i], states[i].GetFullContext().mountLocationRecord);
        }
        var torpedoShip = session.GameState.shipLogs.First(s => s.torpedoSectorStatus.mountStatus.Count > 1);
        var tubes = torpedoShip.torpedoSectorStatus.mountStatus.ToArray();
        var tubeRecords = tubes.Select(m => m.GetTorpedoMountRecordInfo().record).ToArray();
        torpedoShip.shipClass.torpedoSector.mountLocationRecords.Reverse();
        torpedoShip.torpedoSectorStatus.mountStatus.Reverse();
        for (var i = 0; i < tubes.Length; i++)
            Assert.Same(tubeRecords[i], tubes[i].GetTorpedoMountRecordInfo().record);
    }
}
