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
        void Click(Button button){button.Focus();using var evt=NavigationSubmitEvent.GetPooled();button.SendEvent(evt);}
        Invoke("ClearDialogs",false);
        Click(root.Q<Button>("fleetDynamic"));await Task.Delay(80);
        var dynamicTree=root.Q<TreeView>("dynamicUnits");
        Check(dynamicTree?.selectedItem is ShipLog,"Dynamic command dialog selects the current ship");
        Check(root.Q<Button>("fleetDoctrine")==null,"Dynamic command dialog has no doctrine action");
        dynamicTree.ExpandAll();
        var shipIndex=Enumerable.Range(0,dynamicTree.viewController.GetItemsCount()).First(i=>
            dynamicTree.GetItemDataForIndex<IShipGroupMember>(i) is ShipLog s && game.Session.IsPlayerShip(s) && s!=dynamicTree.selectedItem);
        dynamicTree.SetSelection(shipIndex);dynamicTree.ScrollToItem(shipIndex);await Task.Delay(80);
        var selectedShip=(ShipLog)dynamicTree.selectedItem;
        var dynamicRow=dynamicTree.GetRootElementForIndex(shipIndex);
        Check(game.Session.SelectedObjectId==selectedShip.objectId && dynamicRow.Q<Label>(className:"fleet-name").text==selectedShip.GetMemberName() &&
            dynamicRow.Q<Label>(className:"fleet-speed").text==$"{selectedShip.speedKnots:0.0} kn","Dynamic rows select ships and display name and speed in separate columns");
        Invoke("CloseDialog");
        Click(root.Q<Button>("fleetOob"));await Task.Delay(80);
        var tree=root.Q<TreeView>("units");
        Check(tree.selectedItem==selectedShip,"Order of battle opens on the current ship");
        Click(root.Q<Button>("fleetDoctrine"));
        var editor=root.Q<ScrollView>("doctrineEditor");
        Check(editor!=null && root.Q<Label>("modalTitle").text.Contains(selectedShip.GetMemberName()),"Doctrine dialog identifies the selected ship");
        var maneuver=editor.Q<DropdownField>();maneuver.index=2;
        Click(root.Q<Button>("applyDoctrine"));
        Check(selectedShip.doctrine.GetManeuverAutomaticType()==AutomaticType.Automatic,"Apply delegates maneuver to AI");
        tree.ExpandAll();var item=tree.GetItemDataForIndex<IShipGroupMember>(0);tree.SetSelection(0);await Task.Delay(50);
        Check(game.Session.SelectedObjectId==item.objectId,"Native TreeView selection updates the battle selection");
        Click(root.Q<Button>("fleetDoctrine"));
        Check(item is ShipGroup && root.Q<Button>("applyDoctrine")!=null,"Fleet command can edit a formation's doctrine");
        Invoke("CloseDialog");Invoke("CloseDialog");
        var target=game.Session.GameState.shipLogs.Last(s=>!game.Session.IsPlayerShip(s));
        game.ShowTacticalMenu(target,new Vector2(200,200));
        Click(root.Q<VisualElement>("tacticalContextMenu").Query<Button>().ToList().Last());
        await Task.Delay(100);
        tree=root.Q<TreeView>("units");
        var row=tree.GetRootElementForIndex(tree.selectedIndex);
        Check(tree.selectedItem==target && game.Session.SelectedObjectId==target.objectId &&
            row!=null && tree.Q<ScrollView>().contentViewport.worldBound.Overlaps(row.worldBound),"Context menu reveals and selects the target in the order of battle");
        Click(root.Q<Button>("fleetDoctrine"));
        Check(!root.Q<ScrollView>("doctrineEditor").Q<DropdownField>().enabledInHierarchy && root.Q<Button>("applyDoctrine")==null,"Enemy doctrine is read-only");
        Invoke("CloseDialog");Invoke("CloseDialog");
        Check(root.Q<ScrollView>("battleLog").contentContainer.childCount>0,"Combat log shows its HUD summary");
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
        game.World.Fit();
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
        void CheckPage(string pageName)
        {
            var page=root.Q<VisualElement>(pageName);
            Check(game.CurrentScreen==pageName && page.resolvedStyle.display==DisplayStyle.Flex
                && Mathf.Abs(page.worldBound.yMax-root.Q<VisualElement>("root").worldBound.yMax)<2,
                pageName+" fills the screen down to its bottom edge");
            Check(root.Q<VisualElement>("shipTactical")==null && root.Q<DropdownField>("target")==null,
                pageName+" contains no obsolete tactical target controls");
        }
        game.ReturnToArchive();
        await Task.Delay(100);
        CheckPage("archive");
        await game.OpenScenarioAsync("RJH - Battle of Ulsan.scen.xml");
        await Task.Delay(100);
        CheckPage("briefing");
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
