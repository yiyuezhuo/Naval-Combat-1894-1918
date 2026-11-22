using CoreUtils;
using NavalCombatCore;
using System.Xml.Serialization;

namespace NavalCombat.Tests;

public sealed class LeaderEmbarkationTests
{
    [Theory]
    [InlineData("RJH - Battle of the Yellow Sea.scen.xml", "Peresviet", "Pavel Ukhtomsky", LeaderPost.DeputyCommander)]
    [InlineData("RJH - The Battle of Tsushima.scen.xml", "Mikasa", "Akiyama Saneyuki", LeaderPost.StaffOfficer)]
    [InlineData("RJH - The Battle of Tsushima.scen.xml", "Mikasa", "Katō Tomosaburō", LeaderPost.ChiefOfStaff)]
    [InlineData("SJH - Battle of Yalu River.scen.xml", "Saikyo Maru", "Kabayama Sukenori", LeaderPost.VisitingOfficial)]
    [InlineData("SJS - Battle of Yalu River (Beiyang with foreign ships).scen.xml", "Saikyo Maru", "Kabayama Sukenori", LeaderPost.VisitingOfficial)]
    public void ScenarioAppointmentsEmbarkWithTheirTitles(string scenario, string shipName, string leaderName, LeaderPost post)
    {
        var session = ScenarioFixture.LoadSession(scenario);
        session.StartObserverBattle();
        var state = session.GameState;
        var leader = state.leaders.Single(l => l.name.english.Trim() == leaderName);
        var ship = state.shipLogs.Single(s => s.namedShip.name.english == shipName);
        var entry = Assert.Single(ship.embarkedLeaders, e => e.leaderObjectId == leader.objectId);
        Assert.Equal(post, entry.post);
        Assert.False(string.IsNullOrWhiteSpace(entry.title.chineseSimplified));
        Assert.DoesNotContain(state.shipLogs.Where(s => s != ship).SelectMany(s => s.embarkedLeaders), e => e.leaderObjectId == leader.objectId);
        if (post == LeaderPost.DeputyCommander || post == LeaderPost.VisitingOfficial)
            Assert.Equal(leader.objectId, ship.embarkedLeaders.OrderBy(e => e.GetDisplayPriority()).ThenBy(e => e.GetCommandDepth()).First().leaderObjectId);
    }

    [Fact]
    public void PundoFangBoqianCommandsEscortGroupFromChiYuan()
    {
        var session = ScenarioFixture.LoadSession("SJH - Battle of Pundo.scen.xml");
        session.StartObserverBattle();
        var state = session.GameState;
        var fang = state.leaders.Single(l => l.name.english == "Fang Boqian");
        var escort = state.shipGroups.Single(g => g.name.english == "Escort Group");
        var chiYuan = state.shipLogs.Single(s => s.namedShip.name.english == "Chi Yuan");
        var aboard = chiYuan.embarkedLeaders.Where(e => e.leaderObjectId == fang.objectId).ToList();
        Assert.Equal(2, aboard.Count);
        Assert.Contains(aboard, e => e.post == LeaderPost.Commander && e.commandObjectId == escort.objectId);
        Assert.Contains(aboard, e => e.post == LeaderPost.Captain && e.commandObjectId == chiYuan.objectId);
        Assert.DoesNotContain(state.shipLogs.Where(s => s != chiYuan).SelectMany(s => s.embarkedLeaders),
            e => e.leaderObjectId == fang.objectId);
    }

    [Fact]
    public void StaffRankBelowCaptainsAndExplicitAssignmentsSurviveSerialization()
    {
        var state = ScenarioFixture.LoadSession().GameState;
        var group = state.shipGroups.First(g => g.leader != null);
        var ship = state.shipLogs.Last();
        state.leaderAppointments.Add(new LeaderAppointment
        {
            leaderObjectId = group.leader.objectId, commandObjectId = group.objectId,
            shipObjectId = ship.objectId, post = LeaderPost.StaffOfficer,
            title = new GlobalString { english = "Special staff" }, displayPriority = 55
        });
        state.InitializeLeaderEmbarkation();
        var entry = Assert.Single(state.shipLogs.SelectMany(s => s.embarkedLeaders), e => e.leaderObjectId == group.leader.objectId);
        Assert.Contains(entry, ship.embarkedLeaders);
        Assert.Equal(55, entry.GetDisplayPriority());
        Assert.True(new EmbarkedLeader { post = LeaderPost.Captain }.GetDisplayPriority() < new EmbarkedLeader { post = LeaderPost.StaffOfficer }.GetDisplayPriority());
        var serializer = new XmlSerializer(typeof(NavalGameState));
        using var writer = new StringWriter(); serializer.Serialize(writer, state);
        var restored = (NavalGameState)serializer.Deserialize(new StringReader(writer.ToString()));
        restored.InitializeLeaderEmbarkation();
        Assert.Single(restored.leaderAppointments);
        var restoredEntry = Assert.Single(restored.shipLogs.Single(s => s.objectId == ship.objectId).embarkedLeaders, e => e.leaderObjectId == group.leader.objectId);
        Assert.Equal("Special staff", restoredEntry.title.english);
        Assert.Equal(55, restoredEntry.displayPriority);
    }

    [Fact]
    public void InvalidAppointmentDoesNotPartiallyInitializeRoster()
    {
        var state = ScenarioFixture.LoadSession().GameState;
        state.leaderAppointments.Add(new LeaderAppointment { leaderObjectId = state.leaders[0].objectId, shipObjectId = "missing" });
        Assert.Throws<InvalidOperationException>(() => state.InitializeLeaderEmbarkation());
        Assert.False(state.leaderEmbarkationInitialized);
        Assert.All(state.shipLogs, ship => Assert.Empty(ship.embarkedLeaders));
    }

    [Fact]
    public void TsushimaRelativeEscortsDoNotPreventDivisionCommandersMovingToTail()
    {
        var session = ScenarioFixture.LoadSession("RJH - The Battle of Tsushima.scen.xml");
        session.StartObserverBattle();
        var state = session.GameState;
        var combined = state.shipGroups.Single(g => g.name.english == "Combined Fleet");
        var first = state.shipGroups.Single(g => g.name.english == "First Squadron" && g.parentObjectId == combined.objectId);
        var second = state.shipGroups.Single(g => g.name.english == "Second Squadron" && g.parentObjectId == combined.objectId);
        var firstDivision = state.shipGroups.Single(g => g.name.english == "First Division" && g.parentObjectId == first.objectId);
        var secondDivision = state.shipGroups.Single(g => g.name.english == "Second Division" && g.parentObjectId == second.objectId);
        void Aboard(ShipGroup command, string shipName)
        {
            var ship = Assert.Single(state.shipLogs, s => s.embarkedLeaders.Any(e =>
                e.post == LeaderPost.Commander && e.commandObjectId == command.objectId));
            Assert.Equal(shipName, ship.namedShip.name.english);
        }
        Aboard(combined, "Mikasa");
        Aboard(first, "Mikasa");
        Aboard(firstDivision, "Nisshin");
        Aboard(second, "Idzumo");
        Aboard(secondDivision, "Iwate");
    }

    [Fact]
    public void PortArthurFleetCommandersStayAtHeadAndDivisionCommandersMoveToTail()
    {
        var session=ScenarioFixture.LoadSession("RJH - Battle of Port Arthur.scen.xml");
        session.StartObserverBattle();
        var state=session.GameState;
        ShipGroup Group(string name)=>state.shipGroups.Single(g=>g.name.english==name);
        ShipLog Ship(string name)=>state.shipLogs.Single(s=>s.namedShip.name.english==name);
        void Aboard(string group,string ship)=>Assert.Contains(Ship(ship).embarkedLeaders,
            e=>e.post==LeaderPost.Commander&&e.commandObjectId==Group(group).objectId);
        Assert.Equal(Group("Combined Fleet").objectId,Group("First Fleet").parentObjectId);
        Assert.Equal(Group("First Fleet").objectId,Group("First Division").parentObjectId);
        Assert.Equal(Group("First Fleet").objectId,Group("Third Division").parentObjectId);
        Assert.Equal(Group("Second Fleet").objectId,Group("Second Division").parentObjectId);
        Assert.Equal("Kamimura Hikonojō",Group("Second Fleet").leader.name.english);
        Aboard("Combined Fleet","Mikasa");Aboard("First Fleet","Mikasa");
        Aboard("First Division","Hatsuse");Aboard("Second Fleet","Idzumo");Aboard("Second Division","Iwate");
        Assert.DoesNotContain(Ship("Mikasa").embarkedLeaders,e=>e.commandObjectId==Group("First Division").objectId);
        Assert.DoesNotContain(Ship("Idzumo").embarkedLeaders,e=>e.commandObjectId==Group("Second Division").objectId);
        Assert.All(state.shipLogs.Where(s=>s.leader!=null),s=>Assert.Contains(s.embarkedLeaders,e=>e.post==LeaderPost.Captain));
        Aboard("Russian Forces","Peresviet");Aboard("Battle Squadron","Peresviet");
        Aboard("1st Sub-Division","Petropavlovsk");
    }

    [Fact]
    public void CoronelSpeeIsAlreadyAssignedToScharnhorst()
    {
        var session=ScenarioFixture.LoadSession("WWH - Battle of Coronel.scen.xml");
        session.StartObserverBattle();
        var state=session.GameState;
        var spee=state.leaders.Single(l=>l.name.english=="Maximilian von Spee");
        var ship=state.shipLogs.Single(s=>s.namedShip.name.english=="Scharnhorst");
        Assert.Contains(ship.embarkedLeaders,e=>e.leaderObjectId==spee.objectId&&e.post==LeaderPost.Commander);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UnrelatedCommandersAndAmbiguousTailsDoNotMove(bool related)
    {
        var state=ScenarioFixture.LoadSession().GameState;
        var head=state.shipLogs.First(s=>s.controlMode==ControlMode.Independent);
        var followers=state.shipLogs.Where(s=>s!=head).Take(2).ToList();
        foreach(var ship in followers){ship.controlMode=ControlMode.FollowTarget;ship.followedTargetObjectId=head.objectId;}
        var root=new ShipGroup{objectId="test-root",leaderReference=new(){referenceObjectId=state.leaders[0].objectId}};
        var child=new ShipGroup{objectId="test-child",parentObjectId=related?root.objectId:null,
            leaderReference=new(){referenceObjectId=state.leaders[1].objectId},childrenObjectIds=new(){head.objectId,followers[0].objectId,followers[1].objectId}};
        root.childrenObjectIds.Add(related?child.objectId:head.objectId);
        state.shipGroups.Clear();state.shipGroups.AddRange(new[]{root,child});state.ResetAndRegisterAll();
        state.InitializeLeaderEmbarkation();
        Assert.Contains(head.embarkedLeaders,e=>e.commandObjectId==root.objectId);
        Assert.Contains(head.embarkedLeaders,e=>e.commandObjectId==child.objectId);
    }

    [Fact]
    public void CaptainsAndRecursiveCommandersEmbarkOnEligibleLargestShipsOnce()
    {
        var state=ScenarioFixture.LoadSession().GameState;
        state.shipLogs.Clear();state.shipGroups.Clear();state.namedShips.Clear();state.shipClasses.Clear();state.leaders.Clear();
        var captain=new Leader{objectId="captain"};
        var commander=new Leader{objectId="commander"};
        state.leaders.AddRange(new[]{captain,commander});
        ShipLog AddShip(string id,double tons,ControlMode mode)
        {
            state.shipClasses.Add(new ShipClass{objectId=id+"-class",displacementTons=tons});
            state.namedShips.Add(new NamedShip{objectId=id+"-named",shipClassObjectId=id+"-class",defaultLeaderReference=new(){referenceObjectId=captain.objectId}});
            var ship=new ShipLog{objectId=id,namedShipObjectId=id+"-named",controlMode=mode};state.shipLogs.Add(ship);return ship;
        }
        var follower=AddShip("follower",30000,ControlMode.FollowTarget);
        var small=AddShip("small",1000,ControlMode.Independent);
        var large=AddShip("large",10000,ControlMode.Independent);
        var tied=AddShip("z-tie",10000,ControlMode.Independent);
        var nested=new ShipGroup{objectId="nested",childrenObjectIds=new(){tied.objectId,large.objectId}};
        var root=new ShipGroup{objectId="root",leaderReference=new(){referenceObjectId=commander.objectId},childrenObjectIds=new(){small.objectId,nested.objectId,follower.objectId}};
        state.shipGroups.AddRange(new[]{root,nested});state.ResetAndRegisterAll();
        state.InitializeLeaderEmbarkation();
        Assert.All(state.shipLogs,ship=>Assert.Contains(ship.embarkedLeaders,e=>e.post==LeaderPost.Captain&&e.commandObjectId==ship.objectId));
        Assert.Contains(large.embarkedLeaders,e=>e.leaderObjectId==commander.objectId&&e.commandObjectId==root.objectId&&e.post==LeaderPost.Commander);
        Assert.DoesNotContain(follower.embarkedLeaders,e=>e.post==LeaderPost.Commander);
        Assert.DoesNotContain(tied.embarkedLeaders,e=>e.post==LeaderPost.Commander);
        large.controlMode=ControlMode.FollowTarget;
        state.InitializeLeaderEmbarkation();
        Assert.Equal(2,large.embarkedLeaders.Count);
        var serializer=new XmlSerializer(typeof(NavalGameState));
        using var writer=new StringWriter();serializer.Serialize(writer,state);
        var restored=(NavalGameState)serializer.Deserialize(new StringReader(writer.ToString()));
        Assert.True(restored.leaderEmbarkationInitialized);
        restored.InitializeLeaderEmbarkation();
        Assert.Equal(2,restored.shipLogs.Single(ship=>ship.objectId==large.objectId).embarkedLeaders.Count);
    }

    [Fact]
    public void CommanderWithoutIndependentDescendantsIsNotAssigned()
    {
        var state=ScenarioFixture.LoadSession().GameState;
        foreach(var ship in state.shipLogs)ship.controlMode=ControlMode.RelativeToTarget;
        state.InitializeLeaderEmbarkation();
        Assert.DoesNotContain(state.shipLogs.SelectMany(ship=>ship.embarkedLeaders),e=>e.post==LeaderPost.Commander);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void EveryParticipationModeInitializesEmbarkation(int mode)
    {
        var session=ScenarioFixture.LoadSession();
        Assert.False(session.GameState.leaderEmbarkationInitialized);
        if(mode==0)session.StartBattle(session.RootGroups[0].objectId);
        else if(mode==1)session.StartRolePlayingBattle(session.RootGroups[0].objectId);
        else session.StartObserverBattle();
        Assert.True(session.GameState.leaderEmbarkationInitialized);
        Assert.All(session.GameState.shipLogs.Where(ship=>ship.leader!=null),ship=>Assert.Contains(ship.embarkedLeaders,e=>e.leaderObjectId==ship.leader.objectId));
    }
}
