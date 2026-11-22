using NavalCombat.Application;
using NavalCombatCore;

namespace NavalCombat.Tests;

public sealed class JutlandEmbarkationTests
{
    const string South="WWH - Jutland - The Run to the South.scen.xml";
    const string Engage="WWH - Jutland - The Battle Fleets Engage.scen.xml";

    [Theory]
    [InlineData(South,"David Beatty","Lion")]
    [InlineData(South,"Hugh Evan-Thomas","Barham")]
    [InlineData(South,"William Goodenough","Southampton")]
    [InlineData(South,"Friedrich Bödicker","Frankfurt")]
    [InlineData(Engage,"John Jellicoe","Iron Duke")]
    [InlineData(Engage,"Reinhard Scheer","Friedrich der Grosse")]
    [InlineData(Engage,"Alexander Duff","Superb")]
    [InlineData(Engage,"Hugh Evan-Thomas","Barham")]
    [InlineData(Engage,"Franz von Hipper","Lützow")]
    [InlineData(Engage,"William Pakenham","New Zealand")]
    public void HistoricalCommandersEmbarkOnlyOnTheirFlagship(string file,string leaderName,string shipName)
    {
        var session=ScenarioFixture.LoadSession(file);session.StartObserverBattle();
        var leader=session.GameState.leaders.Single(l=>l.name.english.Trim()==leaderName);
        var ship=Assert.Single(session.GameState.shipLogs,s=>s.embarkedLeaders.Any(e=>e.leaderObjectId==leader.objectId));
        Assert.Equal(shipName,ship.namedShip.name.english);
        if(leaderName=="Reinhard Scheer")Assert.Equal(2,ship.embarkedLeaders.Count(e=>e.leaderObjectId==leader.objectId));
    }

    [Fact]
    public void CombinedBattlecruiserSquadronsHaveOneCommanderAndRetainPakenhamsSquadronTitle()
    {
        var session = ScenarioFixture.LoadSession(Engage);
        var state = session.GameState;
        var group = state.shipGroups.Single(g => g.name.english == "British 1st and 2nd Battlecruiser Squadrons");
        session.StartRolePlayingBattle(group.objectId);

        var commander = Assert.Single(state.shipLogs.SelectMany(ship => ship.embarkedLeaders),
            entry => entry.commandObjectId == group.objectId && entry.post == LeaderPost.Commander);
        Assert.Equal("David Beatty", group.leader.name.english);
        Assert.Equal(group.leader.objectId, commander.leaderObjectId);
        var flagship = session.GetInitialSelectedShip();
        Assert.Equal("Lion", flagship.namedShip.name.english);
        Assert.Contains(commander, flagship.embarkedLeaders);

        var newZealand = state.shipLogs.Single(s => s.namedShip.name.english == "New Zealand");
        Assert.Equal(group.objectId, newZealand.parentObjectId);
        var deputy = Assert.Single(newZealand.embarkedLeaders,
            entry => entry.commandObjectId == group.objectId && entry.post == LeaderPost.DeputyCommander);
        Assert.Equal("William Pakenham", state.leaders.Single(l => l.objectId == deputy.leaderObjectId).name.english);
        Assert.Equal("第2战列巡洋舰分舰队司令", deputy.title.chineseSimplified);
    }

    [Theory]
    [InlineData("Grand Fleet","Iron Duke",false)]
    [InlineData("High Seas Fleet","Friedrich der Grosse",false)]
    [InlineData("British 3rd Division","Superb",true)]
    [InlineData("British 5th Battle Squadron","Barham",true)]
    public void InitialSelectionUsesTheSelectedCommandsFlagship(string groupName,string shipName,bool role)
    {
        var session=ScenarioFixture.LoadSession(Engage);
        var group=session.GameState.shipGroups.Single(g=>g.name.english==groupName);
        if(role)session.StartRolePlayingBattle(group.objectId);else session.StartBattle(group.objectId);
        Assert.Equal(shipName,session.GetInitialSelectedShip().namedShip.name.english);
    }

    [Fact]
    public void MissingCommanderUsesPreviousFallbackAndNotSubordinateCommander()
    {
        var session=ScenarioFixture.LoadSession(Engage);var group=session.RootGroups.First();
        session.StartBattle(group.objectId);
        foreach(var ship in session.GameState.shipLogs)ship.embarkedLeaders.RemoveAll(e=>e.commandObjectId==group.objectId);
        var expected=session.GameState.shipLogs.Where(session.IsPlayerShip)
            .OrderByDescending(s=>s.GetEffectiveControlMode()==ControlMode.Independent)
            .ThenByDescending(s=>s.shipClass.displacementTons).First();
        Assert.Same(expected,session.GetInitialSelectedShip());
    }

    [Fact]
    public void ObserverUsesChosenCommandAndFallsBackWhenNoneChosen()
    {
        var session=ScenarioFixture.LoadSession(Engage);session.StartObserverBattle();
        var group=session.RootGroups.Single(g=>g.name.english=="High Seas Fleet");
        Assert.Equal("Friedrich der Grosse",session.GetInitialSelectedShip(group.objectId).namedShip.name.english);
        Assert.NotNull(session.GetInitialSelectedShip());
    }
}
