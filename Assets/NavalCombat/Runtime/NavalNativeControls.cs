using CoreUtils;
using NavalCombatCore;
using NavalCombat.Avalonia.ViewModels;
using UnityEngine;
using UnityEngine.UIElements;

namespace NavalCombat.Unity
{
public sealed partial class NavalGame
{
    readonly Dictionary<string,int> treeIds=new();
    readonly List<string[]> battleTableRows=new();
    bool syncingTree;
    readonly HashSet<TreeView> populatedTrees=new();
    int TreeId(IShipGroupMember member){if(!treeIds.TryGetValue(member.objectId,out int id))treeIds[member.objectId]=id=treeIds.Count+1;return id;}
    void SetupNativeControls()
    {
        Q<VisualElement>("scenarioPreview").Add(new MarkdownRenderer{name="scenarioMarkdown",selectable=true});
        foreach(var name in new[]{"units","dynamicUnits","posts"})
        {
            var tree=Q<TreeView>(name);tree.fixedItemHeight=32;tree.selectionType=SelectionType.Single;
            tree.makeItem=()=>{var row=new VisualElement();row.AddToClassList("fleet-row");row.Add(Text("","fleet-name"));row.Add(Text("","fleet-speed"));return row;};
            tree.bindItem=(e,i)=>{var m=tree.GetItemDataForIndex<IShipGroupMember>(i);((Label)e.ElementAt(0)).text=m.GetMemberName();((Label)e.ElementAt(1)).text=m is ShipLog s?$"{s.speedKnots:0.0} kn":"";};
            tree.selectionChanged+=items=>{if(syncingTree)return;var m=items.OfType<IShipGroupMember>().FirstOrDefault();if(m==null)return;if(name=="posts")selectedPost=m;else Select(m);};
        }
        SetupBattleHud();
    }
    void BuildPostTree(){if(Session==null)return;SetTree(Q<TreeView>("posts"),Session.RootGroups.Select(StaticNode).ToList(),selectedPost);}
    TreeViewItemData<IShipGroupMember> StaticNode(IShipGroupMember member)
    {
        var children=member is ShipGroup g?g.childrenObjectIds.Select(id=>EntityManager.Instance.Get<IShipGroupMember>(id)).Where(m=>m!=null).Select(StaticNode).ToList():new();
        return new(TreeId(member),member,children);
    }
    void SetTree(TreeView tree,List<TreeViewItemData<IShipGroupMember>> nodes,IShipGroupMember selected)
    {
        var expanded=populatedTrees.Contains(tree)?treeIds.Values.Where(id=>tree.IsExpanded(id)).ToArray():Array.Empty<int>();syncingTree=true;
        try{tree.SetRootItems(nodes);tree.Rebuild();populatedTrees.Add(tree);foreach(int id in expanded)tree.ExpandItem(id);if(selected!=null)tree.SetSelectionByIdWithoutNotify(new[]{TreeId(selected)});}
        finally{syncingTree=false;}
    }
    void BuildNativeTrees()
    {
        if(root==null||Session?.GameState==null)return;
        SetTree(Q<TreeView>("units"),Session.RootGroups.Select(StaticNode).ToList(),selection);
        var ships=Session.GameState.shipLogs.ToList();var visited=new HashSet<ShipLog>();
        TreeViewItemData<IShipGroupMember> Node(ShipLog s)
        {visited.Add(s);return new(TreeId(s),s,ships.Where(c=>c.GetControlPredecessor()==s&&!visited.Contains(c)).Select(Node).ToList());}
        var nodes=new List<TreeViewItemData<IShipGroupMember>>();
        foreach(var s in ships.Where(s=>s.GetControlPredecessor()==null))if(!visited.Contains(s))nodes.Add(Node(s));
        foreach(var s in ships)if(!visited.Contains(s))nodes.Add(Node(s));
        SetTree(Q<TreeView>("dynamicUnits"),nodes,selection);
    }
    void SyncTreeSelection(IShipGroupMember member)
    {
        if(member==null)return;syncingTree=true;
        try{foreach(var n in new[]{"units","dynamicUnits"})Q<TreeView>(n).SetSelectionByIdWithoutNotify(new[]{TreeId(member)});}finally{syncingTree=false;}
    }
    static void ConfigureTable(MultiColumnListView table,List<string[]> rows,string[] titles,float[] widths)
    {
        table.columns.Clear();table.fixedItemHeight=28;table.selectionType=SelectionType.Single;
        for(int i=0;i<titles.Length;i++){int col=i;table.columns.Add(new Column{name="column"+i,title=titles[i],width=widths[i],stretchable=i==titles.Length-1,makeCell=()=>new Label(),bindCell=(e,index)=>{var l=(Label)e;l.text=rows[index].ElementAtOrDefault(col)??"";l.tooltip=l.text;}});}
        table.itemsSource=rows;
    }
    void RefreshBattleTable()
    {
        battleTableRows.Clear();foreach(var (ship,log) in Session.GetBattleLogs())battleTableRows.Add(new[]{Session.ToScenarioTime(log.time).ToString("HH:mm:ss"),ship.GetMemberName(),log.SummaryContent()});RefreshHudLog();
        Q<Label>("battleTitle").text=Session.ScenarioTitle;
        foreach(var n in new[]{"units","dynamicUnits"})Q<TreeView>(n).RefreshItems();
    }
    void RefreshStatusRows()
    {
        var damage=Q<VisualElement>("damageTable");damage.Clear();var weapons=Q<VisualElement>("weaponReadout");weapons.Clear();if(SelectedShip==null)return;
        void Row(VisualElement parent,string left,string right,string css="status-row") {var row=new VisualElement();row.AddToClassList(css);row.Add(Text(left,"status-key"));row.Add(Text(right,"status-value"));parent.Add(row);}
        Row(damage,L("DamageCondition"),DamageTierSummary);Row(damage,U("Core.HitLocationMerchantVessel.Propulsion"),PropulsionDamageSummary);Row(damage,L("Machinery"),MachineryDamageSummary);Row(damage,L("MachineryFlooding"),MachineryFloodingSummary);Row(damage,L("DamageControl"),DamageControlSummary);
        void Groups(IEnumerable<WeaponFiringGroupViewModel> groups){foreach(var g in groups)Row(weapons,"→ "+g.TargetName,string.Join(" · ",new[]{g.Allocation,g.Timing,g.Ammunition,g.FireControlState}.Where(s=>!string.IsNullOrWhiteSpace(s))));}
        foreach(var b in BatteryWeapons){Row(weapons,b.Caliber+" "+b.Barrels,string.Join(" · ",b.Ammunition.Select(a=>a.Text)),"weapon-row");Groups(b.FiringGroups);}
        foreach(var b in RapidFiringWeapons){Row(weapons,b.Label+" "+b.Barrels,b.Ammunition,"weapon-row");Groups(b.FiringGroups);}
        foreach(var b in TorpedoWeapons){Row(weapons,b.Tubes,b.Ammunition+" · "+b.Reloads,"weapon-row");Groups(b.FiringGroups);}
    }
}
}
