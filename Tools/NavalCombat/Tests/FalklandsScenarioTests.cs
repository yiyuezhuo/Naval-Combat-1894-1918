using NavalCombat.Application;
using NavalCombatCore;

namespace NavalCombat.Tests;

public sealed class FalklandsScenarioTests
{
    [Theory]
    [InlineData("WWH - Battle of the Falklands.scen.xml",14,true)]
    [InlineData("WWH - Battle of the Falklands - 1050 Reconstruction.scen.xml",14,false)]
    public void VariantsLoadWithCorrectFlagshipsAndAdvance(string file,int utcHour,bool explicitAppointment)
    {
        var session=ScenarioFixture.LoadSession(file);
        session.StartObserverBattle();
        var game=session.GameState;
        var start=new DateTime(1914,12,8,utcHour,50,0,DateTimeKind.Utc);
        Assert.Equal(start,game.scenarioState.dateTime);
        Assert.Equal(start,game.scenarioState.beginDateTime);
        Assert.Equal(12,game.shipLogs.Count);
        Assert.Equal(explicitAppointment?1:0,game.leaderAppointments.Count);
        Assert.Equal("Invincible",Assert.Single(game.shipLogs.Where(s=>s.embarkedLeaders.Any(e=>e.leaderObjectId=="819c8f80-c43a-5cc0-93d8-8f44fa9ea3ae"))).namedShip.name.english);
        Assert.Equal("Scharnhorst",Assert.Single(game.shipLogs.Where(s=>s.embarkedLeaders.Any(e=>e.leaderObjectId=="3a531ece-ac90-53c7-abb2-c8c50d9a63e2"))).namedShip.name.english);
        if(!explicitAppointment)
        {
            var ships=game.shipLogs.ToDictionary(s=>s.namedShip.name.english);
            var yardsPerNmi=1852/.9144;
            Assert.InRange(MeasureUtils.ApproximateDistanceYards(ships["Glasgow"].position,ships["Invincible"].position)/yardsPerNmi,2.97,3.03);
            Assert.InRange(MeasureUtils.ApproximateDistanceYards(ships["Glasgow"].position,ships["Leipzig"].position)/yardsPerNmi,11.9,12.1);
            Assert.Equal(ControlMode.Independent,ships["Invincible"].controlMode);
            Assert.Equal(ControlMode.Independent,ships["Scharnhorst"].controlMode);
        }
        session.SetSimulationSpeed(SimulationSpeed.Realtime);
        for(int i=0;i<30;i++)session.AdvanceWallClock(1);
        Assert.NotEqual(BattlePhase.Faulted,session.Phase);
        Assert.Equal(start.AddSeconds(30),game.scenarioState.dateTime);
        Assert.All(game.shipLogs,s=>Assert.True(double.IsFinite(s.position.LatDeg)&&double.IsFinite(s.position.LonDeg)));
    }
}
