using NavalCombatCore;

namespace NavalCombat.Tests;

public sealed class JutlandEngageScenarioTests
{
    const string Scenario = "WWH - Jutland - The Battle Fleets Engage.scen.xml";

    [Fact]
    public void HistoricalStartingDamageAndExpenditureSurviveLoadingAndStepping()
    {
        var game = ScenarioFixture.LoadSession(Scenario).GameState;
        var ships = game.shipLogs.ToDictionary(s => s.namedShip.name.english);
        Assert.Equal(new DateTime(1916, 5, 31, 18, 0, 0, DateTimeKind.Utc), game.scenarioState.dateTime);
        foreach (var sunk in new[] { "Queen Mary", "Indefatigable", "Nomad", "Nestor", "V27", "V29" })
            Assert.DoesNotContain(sunk, ships.Keys);
        foreach (var flagship in new[] { "Iron Duke", "Friedrich der Grosse", "Invincible", "Defence", "König" })
            Assert.Contains(flagship, ships.Keys);
        Assert.Equal(ships["Lion"].shipClass.damagePoint * .20, ships["Lion"].damagePoint, 3);
        Assert.Equal(MountStatus.Destroyed, ships["Lion"].batteryStatus[0].mountStatus[2].status);
        Assert.Equal(MountStatus.Disabled, ships["Princess Royal"].batteryStatus[0].mountStatus[0].status);
        Assert.All(ships["Von der Tann"].batteryStatus[0].mountStatus, m => Assert.NotEqual(MountStatus.Operational, m.status));
        Assert.Equal(10, ships["Chester"].batteryStatus[0].mountStatus.Sum(m => m.barrels));
        Assert.Equal(4, ships["Chester"].batteryStatus[0].mountStatus.Count(m => m.status == MountStatus.Destroyed));
        Assert.Equal(0, ships["Onslow"].torpedoSectorStatus.GetAmmunitionMagazinePlusLoaded());
        Assert.Equal(0, ships["Petard"].torpedoSectorStatus.GetAmmunitionMagazinePlusLoaded());
        Assert.Equal(1, ships["Nicator"].torpedoSectorStatus.GetAmmunitionMagazinePlusLoaded());
        Assert.Equal(2, ships["Shark"].torpedoSectorStatus.GetAmmunitionMagazinePlusLoaded());
        Assert.Equal(ShipType.Transport, ships["Engadine"].shipClass.type);
        Assert.All(ships["Wiesbaden"].batteryStatus.SelectMany(b => b.mountStatus), m => Assert.Equal(MountStatus.Destroyed, m.status));
        Assert.All(ships["Wiesbaden"].rapidFiringStatus, r => {
            Assert.True(r.portMountHits >= 3);
            Assert.True(r.starboardMountHits >= 3);
        });
        game.Step(1d);
        Assert.Equal(0, ships["Wiesbaden"].GetMaxSpeedKnots());
        Assert.Equal(0, ships["Shark"].GetMaxSpeedKnots());
        Assert.Equal(2, ships["Acasta"].GetMaxSpeedKnots());
        Assert.Equal(2, ships["Onslow"].GetMaxSpeedKnots());
    }
}
