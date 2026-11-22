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
    TreeView openFleetTree;
    readonly Dictionary<string,Leader> commandLeaders=new();

    void IndexCommanders()
    {
        treeIds.Clear();
        commandLeaders.Clear();
        foreach(var ship in Session.GameState.shipLogs)
        {
            foreach(var post in ship.embarkedLeaders)
            {
                if(post.post!=LeaderPost.Captain && post.post!=LeaderPost.Commander)continue;
                var commandId=string.IsNullOrEmpty(post.commandObjectId)?ship.objectId:post.commandObjectId;
                var leader=EntityManager.Instance.Get<Leader>(post.leaderObjectId);
                if(leader!=null)commandLeaders[commandId]=leader;
            }
        }
    }

    int TreeId(IShipGroupMember member)
    {
        if(!treeIds.TryGetValue(member.objectId,out int id))treeIds[member.objectId]=id=treeIds.Count+1;
        return id;
    }

    string FleetRowText(IShipGroupMember member)
    {
        var ship=member as ShipLog;
        if(!commandLeaders.TryGetValue(member.objectId,out var leader) && member is ShipGroup group)leader=group.leader;
        string text=member.GetMemberName();
        string commander=leader?.name?.GetMergedName();
        if(!string.IsNullOrWhiteSpace(commander))text+=" ["+commander+"]";
        if(ship==null)return text;
        if(ship.damagePoint>0 && ship.shipClass!=null)
        {
            double percent=ShipDamagePercent(ship);
            string value=percent<.1?"<0.1":percent.ToString("0.#");
            text+=" · "+Message("Damage","損傷","损伤","損傷")+" "+value+"%";
        }
        if(ship.mapState!=MapState.Deployed)text+=" · "+NavalText.Instance.GetEnum(ship.mapState);
        return text;
    }

    void ConfigureFleetTree(TreeView tree,bool dynamic=false)
    {
        tree.fixedItemHeight=32;
        tree.selectionType=SelectionType.Single;
        if(dynamic)
        {
            tree.makeItem=()=>
            {
                var row=new VisualElement();
                row.AddToClassList("fleet-row");
                row.Add(Text("","fleet-name"));
                row.Add(Text("","fleet-speed"));
                return row;
            };
            tree.bindItem=(row,index)=>
            {
                var ship=(ShipLog)tree.GetItemDataForIndex<IShipGroupMember>(index);
                ((Label)row[0]).text=ship.GetMemberName();
                ((Label)row[1]).text=$"{ship.speedKnots:0.0} kn";
            };
            return;
        }
        tree.makeItem=()=>Text("","fleet-name");
        tree.bindItem=(element,index)=>
        {
            var label=(Label)element;
            label.text=FleetRowText(tree.GetItemDataForIndex<IShipGroupMember>(index));
            label.tooltip=label.text;
        };
    }

    void SetupNativeControls()
    {
        Q<VisualElement>("scenarioPreview").Add(new MarkdownRenderer{name="scenarioMarkdown",selectable=true});
        var posts=Q<TreeView>("posts");
        ConfigureFleetTree(posts);
        posts.selectionChanged+=items=>
        {
            if(items.OfType<IShipGroupMember>().FirstOrDefault() is {} member)selectedPost=member;
        };
        SetupBattleHud();
    }

    void BuildPostTree()
    {
        if(Session==null)return;
        var tree=Q<TreeView>("posts");
        var post=selectedPost;
        tree.ClearSelection();
        tree.SetRootItems(Session.RootGroups.Select(StaticNode).ToList());
        selectedPost=post;
        if(post!=null)tree.SetSelectionByIdWithoutNotify(new[]{TreeId(post)});
    }

    TreeViewItemData<IShipGroupMember> StaticNode(IShipGroupMember member)
    {
        var children=member is ShipGroup g?g.childrenObjectIds.Select(id=>EntityManager.Instance.Get<IShipGroupMember>(id)).Where(m=>m!=null).Select(StaticNode).ToList():new();
        return new(TreeId(member),member,children);
    }

    List<TreeViewItemData<IShipGroupMember>> DynamicNodes()
    {
        var ships=Session.GameState.shipLogs.Where(Session.CanInspect).ToList();
        var children=ships.ToLookup(ship=>ship.GetControlPredecessor());
        var visited=new HashSet<ShipLog>();
        TreeViewItemData<IShipGroupMember> Node(ShipLog ship)
        {
            visited.Add(ship);
            return new(TreeId(ship),ship,children[ship].Where(child=>!visited.Contains(child)).Select(Node).ToList());
        }
        var nodes=children[null].Select(Node).ToList();
        foreach(var ship in ships)if(!visited.Contains(ship))nodes.Add(Node(ship));
        return nodes;
    }

    void OpenFleetDialog(bool dynamic,ShipLog reveal=null)
    {
        if(Session?.GameState==null)return;
        var tree=new TreeView{name=dynamic?"dynamicUnits":"units"};
        tree.AddToClassList("grow");
        ConfigureFleetTree(tree,dynamic);
        tree.SetRootItems(dynamic?DynamicNodes():Session.RootGroups.Where(Session.CanInspect).Select(StaticNode).ToList());
        openFleetTree=tree;
        var page=PushDialog(dynamic?L("DynamicControl"):U("Ui.Order of Battle"),tree,()=>openFleetTree=null,"medium");
        Button doctrine=null;
        if(!dynamic)
        {
            doctrine=ActionButton(page.Actions,Message("Set doctrine","ドクトリン設定","设置条令","設定條令"),()=>
            {
                if(tree.selectedItem is IShipGroupMember member)OpenDoctrineDialog(member);
            });
            doctrine.name="fleetDoctrine";
        }
        tree.selectionChanged+=items=>
        {
            if(openFleetTree!=tree)return;
            var member=items.OfType<IShipGroupMember>().FirstOrDefault();
            doctrine?.SetEnabled(member!=null);
            if(member!=null && selection!=member)Select(member);
        };
        if(reveal!=null)Select(reveal);
        SyncTreeSelection(selection);
        doctrine?.SetEnabled(tree.selectedItem!=null);
        if(tree.selectedItem!=null)
        {
            int id=TreeId((IShipGroupMember)tree.selectedItem);
            tree.schedule.Execute(()=>{if(tree.selectedItem is IShipGroupMember current && TreeId(current)==id)tree.ScrollToItemById(id);});
        }
    }

    void SyncTreeSelection(IShipGroupMember member)
    {
        var tree=openFleetTree;
        if(tree==null || tree.selectedItem==member)return;
        if(member==null || !Session.CanInspect(member) || (tree.name=="dynamicUnits" && member is ShipGroup)){tree.ClearSelection();return;}
        tree.SetSelectionByIdWithoutNotify(new[]{TreeId(member)});
    }
    static void ConfigureTable(MultiColumnListView table,List<string[]> rows,string[] titles,float[] widths)
    {
        table.columns.Clear();table.fixedItemHeight=28;table.selectionType=SelectionType.Single;
        for(int i=0;i<titles.Length;i++){int col=i;table.columns.Add(new Column{name="column"+i,title=titles[i],width=widths[i],stretchable=i==titles.Length-1,makeCell=()=>new Label(),bindCell=(e,index)=>{var l=(Label)e;l.text=rows[index].ElementAtOrDefault(col)??"";l.tooltip=l.text;}});}
        table.itemsSource=rows;
    }
    void RefreshBattleTable()
    {
        battleTableRows.Clear();foreach(var (ship,log) in Session.GetBattleLogs())battleTableRows.Add(new[]{Session.ToScenarioTime(log.time).ToString("HH:mm:ss"),ship.GetMemberName(),ShipHistoryBuilder.LogSummary(Session,log)});RefreshHudLog();
    }
    void RefreshStatusRows()
    {
        var damage=Q<VisualElement>("damageTable");damage.Clear();var weapons=Q<VisualElement>("weaponReadout");weapons.Clear();if(SelectedShip==null || IsSelectedContact)return;
        void Row(VisualElement parent,string left,string right,string css="status-row") {var row=new VisualElement();row.AddToClassList(css);row.Add(Text(left,"status-key"));row.Add(Text(right,"status-value"));parent.Add(row);}
        Row(damage,L("DamageCondition"),DamageTierSummary);Row(damage,U("Core.HitLocationMerchantVessel.Propulsion"),PropulsionDamageSummary);Row(damage,L("Machinery"),MachineryDamageSummary);Row(damage,L("MachineryFlooding"),MachineryFloodingSummary);Row(damage,L("DamageControl"),DamageControlSummary);
        void Groups(IEnumerable<WeaponFiringGroupViewModel> groups){foreach(var g in groups)Row(weapons,"→ "+g.TargetName,string.Join(" · ",new[]{g.Allocation,g.Timing,g.Ammunition,g.FireControlState}.Where(s=>!string.IsNullOrWhiteSpace(s))));}
        foreach(var b in BatteryWeapons){Row(weapons,b.Caliber+" "+b.Barrels,string.Join(" · ",b.Ammunition.Select(a=>a.Text)),"weapon-row");Groups(b.FiringGroups);}
        foreach(var b in RapidFiringWeapons){Row(weapons,b.Label+" "+b.Barrels,b.Ammunition,"weapon-row");Groups(b.FiringGroups);}
        foreach(var b in TorpedoWeapons){Row(weapons,b.Tubes,b.Ammunition+" · "+b.Reloads,"weapon-row");Groups(b.FiringGroups);}
    }
}
}
