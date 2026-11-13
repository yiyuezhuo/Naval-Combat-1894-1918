using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NavalCombat.Application;
using NavalCombat.Avalonia.Resources;
using NavalCombat.Avalonia.ViewModels;
using NavalCombat.Unity;
using NavalCombatCore;
using UnityEngine;
using UnityEngine.UIElements;

namespace NavalCombat.Unity.Editor
{
public static class NavalUiValidation
{
    public static async Task<string> RunNative()
    {
        var game=UnityEngine.Object.FindAnyObjectByType<NavalGame>();
        while(!game.Ready)await Task.Delay(50);
        await game.OpenScenarioAsync("RJH - Battle of Ulsan.scen.xml");game.StartParticipation(BattleParticipationMode.FleetCommand);game.Session.SetSimulationSpeed(SimulationSpeed.Paused);
        var root=game.GetComponent<UIDocument>().rootVisualElement;var results=new List<string>();
        void Check(bool ok,string message){if(!ok)throw new Exception(message);results.Add(message);}
        void Invoke(string name,params object[] args)=>typeof(NavalGame).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(game,args);
        var tabs=root.Q<TabView>("fleetTabs");tabs.selectedTabIndex=1;await Task.Delay(80);
        Check(root.Q<Tab>("dynamicTab").resolvedStyle.display==DisplayStyle.None&&root.Q<Tab>("oobTab").resolvedStyle.display==DisplayStyle.Flex,"Native TabView shows only its active page");
        var tree=root.Q<TreeView>("units");tree.ExpandAll();var item=tree.GetItemDataForIndex<IShipGroupMember>(1);tree.SetSelection(1);await Task.Delay(50);
        Check(game.Session.SelectedObjectId==item.objectId,"Native TreeView selection updates the battle selection");
        Check(root.Q<MultiColumnListView>("battleLog").columns.Count==3,"Battle log uses three native columns");
        Invoke("OpenPause");await Task.Delay(50);var dialog=root.Q<VisualElement>(className:"dialog");
        Check(dialog.layout.height<root.layout.height*.6f&&dialog.layout.width<root.layout.width*.6f,"Pause dialog fits its contents instead of filling the screen");Invoke("CloseDialog");
        var own=game.Session.GameState.shipLogsOnMap.First(s=>game.Session.IsPlayerShip(s));game.Select(own);
        game.OpenDossier(new(DossierKind.ShipClass,own.shipClass.objectId,""));await Task.Delay(50);
        Check(root.Q<VisualElement>("modalBody").Q<TabView>()!=null,"Dossier uses a native TabView");
        Check(root.Q<VisualElement>("modalBody").Query<MultiColumnListView>().ToList().Any(t=>t.columns.Count==3),"Armor uses a native multi-column table");
        root.Q<VisualElement>("modalBody").Q<TabView>().selectedTabIndex=1;await Task.Delay(80);
        var armor=root.Q<VisualElement>("modalBody").Q<MultiColumnListView>();var cells=armor.Q<VisualElement>(className:"unity-multi-column-view__row-container").Children().ToList();
        Check(cells.Count==3&&cells[1].layout.x>cells[0].layout.x&&Math.Abs(cells[0].layout.y-cells[2].layout.y)<1,"Multi-column cells align horizontally without overlapping rows");Invoke("CloseDialog");
        var markdown=new MarkdownRenderer{value="# Heading\n\n| Name | Value |\n| --- | --- |\n| **Armor** | 12 |\n\n- First\n- Second\n\n[Source](https://example.com)",selectable=true};root.Add(markdown);
        Check(markdown.Query<VisualElement>(className:"markdown-table-row").ToList().Count==2,"Markdig renders a real header and data row");
        Check(markdown.Query<Label>().ToList().Any(l=>l.text.Contains("<link=")),"Markdown links retain inline link tags");markdown.RemoveFromHierarchy();
        var labels=(Dictionary<ShipLog,Label>)typeof(NavalGame).GetField("mapLabels",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(game);float maxError=0;
        for(int i=0;i<8;i++)
        {
            game.World.Pan(new Vector2(5,2));await Task.Delay(35);var screen=game.World.ViewCamera.WorldToScreenPoint(game.World.Project(own.position.LatDeg,own.position.LonDeg));var panel=RuntimePanelUtils.ScreenToPanel(root.panel,new Vector2(screen.x,Screen.height-screen.y));var local=root.Q<VisualElement>("viewport").WorldToLocal(panel)+new Vector2(10,8);var actual=labels[own].resolvedStyle.translate;
            maxError=Math.Max(maxError,Vector2.Distance(local,new Vector2(actual.x,actual.y)));
        }
        Check(maxError<1,"Map labels track camera movement within one pixel between status refreshes");
        game.World.Fit();tabs.selectedTabIndex=0;
        var result="PASS\n"+string.Join("\n",results)+"\nMaximum label error: "+maxError;System.IO.File.WriteAllText(".artifacts/naval-native-ui-validation.txt",result);return result;
    }
    public static async Task<string> Run()
    {
        if(!UnityEditor.EditorApplication.isPlaying)throw new InvalidOperationException("Enter Play mode in the naval scene first.");
        var game=UnityEngine.Object.FindAnyObjectByType<NavalGame>();
        for(int i=0;i<200&&!game.Ready;i++)await Task.Delay(50);
        if(!game.Ready)throw new Exception("Archive did not initialize.");
        var results=new List<string>();
        var root=game.GetComponent<UIDocument>().rootVisualElement;
        void Check(bool value,string message){if(!value)throw new Exception(message);results.Add(message);}
        void Close()=>typeof(NavalGame).GetMethod("CloseDialog",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(game,null);
        void Click(Button button){if(button==null)throw new Exception("Missing button");button.Focus();using var e=NavigationSubmitEvent.GetPooled();button.SendEvent(e);}
        void Clear()=>typeof(NavalGame).GetMethod("ClearDialogs",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(game,new object[]{false});
        await game.OpenScenarioAsync("RJH - Battle of Ulsan.scen.xml");
        game.StartParticipation(BattleParticipationMode.FleetCommand);
        var own=game.Session.GameState.shipLogsOnMap.First(s=>game.Session.IsPlayerShip(s));
        var friend=game.Session.GameState.shipLogsOnMap.First(s=>s!=own&&game.Session.IsPlayerShip(s));
        var enemy=game.Session.GameState.shipLogsOnMap.First(s=>!game.Session.IsPlayerSideMember(s));
        game.Select(own);await Task.Delay(100);
        game.ShowTacticalMenu(own,new Vector2(400,200));
        Check(root.Q<VisualElement>("tacticalContextMenu").Query<Button>().ToList().Any(b=>b.text==NavalCombatStrings.Text("Ui.TorpedoAnalysis.Title")),"Selected-ship menu includes torpedo analysis and dossiers");
        game.ShowTacticalMenu(friend,new Vector2(400,200));
        Check(root.Q<VisualElement>("tacticalContextMenu").Query<Button>().ToList().Any(b=>b.text==NavalCombatStrings.Text("Ui.Shell.Relative")),"Friendly-ship menu includes relative station keeping");
        game.ShowTacticalMenu(enemy,new Vector2(400,200));
        Click(root.Q<VisualElement>("tacticalContextMenu").Q<Button>());
        Check(own.shipLevelFiringTargetObjectId==enemy.objectId,"Enemy-menu attack targets the clicked ship");
        game.Session.DetachShip(friend.objectId);
        game.Session.SetFollowTarget(own.objectId,friend.objectId);
        game.OpenFormation(own,"FollowParameters");
        var distance=root.Q<DoubleField>("orderDistance");distance.value=-1;
        var before=own.followDistanceYards;
        Click(root.Q<VisualElement>("modalActions").Query<Button>().ToList().Last());
        Check(own.followDistanceYards==before&&!string.IsNullOrEmpty(root.Q<Label>("orderValidation").text),"Invalid spacing keeps the dialog open and model unchanged");
        distance.value=725;Click(root.Q<VisualElement>("modalActions").Query<Button>().ToList().Last());
        Check(own.followDistanceYards==725,"Applying follow parameters reaches BattleSession");
        game.Session.SetRelativeTarget(own.objectId,friend.objectId);game.OpenFormation(own,"RelativeParameters");
        root.Q<DoubleField>("orderDistance").value=900;root.Q<DoubleField>("orderBearing").value=45;root.Q<Toggle>("orderAbsolute").value=true;
        Click(root.Q<VisualElement>("modalActions").Query<Button>().ToList().Last());
        Check(own.relativeToTargetDistanceYards==900&&own.relativeToTargetAzimuth==45&&own.relativeToAbsolute,"Relative parameters preserve distance, bearing and absolute mode");
        foreach(var kind in new[]{"FollowFormation","RelativeFormation"}){game.OpenFormation(own,kind);Check(root.Q<DoubleField>("orderDistance")!=null,kind+" opens its own parameter form");Close();}
        game.Session.SetSimulationSpeed(SimulationSpeed.Fast);
        game.OpenDossier(new(DossierKind.ShipState,own.objectId,""));
        Check(game.Session.Speed==SimulationSpeed.Paused,"Opening a dossier pauses simulation");
        Check(root.Q<VisualElement>("modalActions").Query<Button>().ToList().Count==1,"Dossier footer contains only close, no battle-management buttons");
        var refs=new List<DossierReference>{new(DossierKind.ShipClass,own.shipClass.objectId,""),new(DossierKind.NamedShip,own.namedShip.objectId,""),new(DossierKind.Torpedo,own.shipClass.objectId,"")};
        if(own.leader!=null)refs.Add(new(DossierKind.Leader,own.leader.objectId,""));
        if(own.shipClass.batteryRecords.Count>0)refs.Add(new(DossierKind.Battery,own.shipClass.batteryRecords[0].objectId,""));
        foreach(var reference in refs){game.OpenDossier(reference);await Task.Delay(30);Check(!root.Q<VisualElement>("modal").ClassListContains("hidden"),reference.Kind+" dossier renders");Close();}
        Check(game.Session.Speed==SimulationSpeed.Paused,"Closing a child dossier preserves its parent and paused state");Close();Check(game.Session.Speed==SimulationSpeed.Fast,"Closing the last dossier restores previous speed");game.Session.SetSimulationSpeed(SimulationSpeed.Paused);
        game.OpenTorpedoAnalysis(own);await Task.Delay(100);Check(root.Query<NavalTorpedoDiagram>().ToList().Count==1,"Torpedo analysis contains the interactive diagram");Close();
        game.Session.StartObserverBattle();game.Select(own);game.ShowTacticalMenu(own,new Vector2(400,200));
        Check(!root.Q<VisualElement>("tacticalContextMenu").Query<Button>().ToList().Any(b=>b.text==NavalCombatStrings.Text("Ui.Shell.Detach")),"Observer context menu offers inspections without commands");
        Check(!game.TryIssueMapCourse(35,130),"Observer sea click remains read-only");
        root.Q<DropdownField>("language").value="English";await Task.Delay(30);root.Q<DropdownField>("language").value="简体中文";await Task.Delay(30);
        Check(game.LastError==null,"Language refresh and all UI checks produce no application error");
        Clear();game.ReturnToArchive();
        var report="PASS\n"+string.Join("\n",results);System.IO.Directory.CreateDirectory(".artifacts");System.IO.File.WriteAllText(".artifacts/naval-ui-validation.txt",report);return report;
    }
}
}
