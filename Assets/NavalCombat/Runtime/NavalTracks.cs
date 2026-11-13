using CoreUtils;
using NavalCombatCore;
using UnityEngine;
using UnityEngine.UIElements;

namespace NavalCombat.Unity
{
public sealed partial class NavalWorld
{
    readonly Dictionary<string,LineRenderer> tracks=new();
    readonly HashSet<string> staticTrackLeaders=new();
    public bool HasTrack(string id)=>tracks.ContainsKey(id);
    public bool IsStaticTrackLeader(string id)=>staticTrackLeaders.Contains(id);
    public void ClearTracks()
    {
        foreach(var line in tracks.Values){line.gameObject.SetActive(false);Destroy(line.gameObject);}
        tracks.Clear();
    }
    void ResetTracks()
    {
        ClearTracks();staticTrackLeaders.Clear();
        foreach(var ship in Session.GameState.shipLogs)
        {
            var parent=((IShipGroupMember)ship).GetParentGroup();
            if(parent?.Walk<ShipLog>().FirstOrDefault()==ship)staticTrackLeaders.Add(ship.objectId);
        }
    }
    public void SetTrack(ShipLog ship,bool enabled)
    {
        if(!enabled)
        {
            if(tracks.Remove(ship.objectId,out var old)){old.gameObject.SetActive(false);Destroy(old.gameObject);}
            return;
        }
        if(tracks.ContainsKey(ship.objectId))return;
        var go=new GameObject("Track "+ship.objectId,typeof(LineRenderer));go.transform.SetParent(lineRoot,false);go.layer=ChartOverlayLayer;
        var line=go.GetComponent<LineRenderer>();line.sharedMaterial=lineMaterial;line.useWorldSpace=true;
        var color=IsBlueSide(ship)?new Color(.35f,.75f,1f):new Color(1f,.45f,.3f);
        line.startColor=line.endColor=color;line.positionCount=0;tracks.Add(ship.objectId,line);
    }
    void UpdateTracks()
    {
        foreach(var pair in tracks)
        {
            var ship=EntityManager.Instance.Get<ShipLog>(pair.Key);var line=pair.Value;
            if(ship==null){line.gameObject.SetActive(false);continue;}
            int count=ship.timeLocLogs.Count;bool append=ship.IsOnMap();
            line.gameObject.SetActive(ViewMode!="Bridge"&&count+(append?1:0)>1);
            line.startWidth=line.endWidth=Mathf.Max(.5f,ChartMetersPerPixel*1.5f);
            int pointCount=count+(append?1:0);
            if(line.positionCount!=pointCount)
            {
                line.positionCount=pointCount;
                for(int i=0;i<count;i++)
                {
                    var point=ship.timeLocLogs[i];line.SetPosition(i,Project(point.latDeg,point.lonDeg)+Vector3.up*3);
                }
            }
            if(append)line.SetPosition(count,Project(ship.position.LatDeg,ship.position.LonDeg)+Vector3.up*3);
        }
    }
}
public sealed partial class NavalGame
{
    float trackMinTons;
    bool trackDynamicLeaders,trackStaticLeaders;
    string TracksTitle()=>Message("Tracks","航跡図","航迹图","航跡圖");
    void OpenTracks()
    {
        if(Session?.GameState==null)return;
        var body=new VisualElement();body.AddToClassList("grow");
        var tons=new FloatField(Message("Minimum tonnage (≥)","最低排水量 (≥)","最低吨位（≥）","最低噸位（≥）")){value=trackMinTons};body.Add(tons);
        var dynamicLeaders=new Toggle(Message("Dynamic formation leaders (Independent)","動的編成の先頭艦（独立）","动态编制领舰（Independent）","動態編制領艦（Independent）")){value=trackDynamicLeaders};body.Add(dynamicLeaders);
        var staticLeaders=new Toggle(Message("Pre-battle OOB leaders","戦闘前編成の先頭艦","静态编制领舰（战前 OOB）","靜態編制領艦（戰前 OOB）")){value=trackStaticLeaders};body.Add(staticLeaders);
        var controls=new VisualElement();controls.AddToClassList("toolbar");body.Add(controls);
        var ships=Session.GameState.shipLogs.ToList();
        var list=new ListView{itemsSource=ships,virtualizationMethod=CollectionVirtualizationMethod.DynamicHeight,selectionType=SelectionType.None};list.AddToClassList("grow");
        list.makeItem=()=>
        {
            var toggle=new Toggle();toggle.style.minHeight=40;toggle.RegisterValueChangedCallback(e=>{if(toggle.userData is ShipLog ship)World.SetTrack(ship,e.newValue);});return toggle;
        };
        list.bindItem=(element,i)=>
        {
            var ship=ships[i];var toggle=(Toggle)element;toggle.userData=ship;
            toggle.label=$"{ship.GetMemberName()} · {ship.shipClass.displacementTons:0} t";
            toggle.SetValueWithoutNotify(World.HasTrack(ship.objectId));
        };
        ActionButton(controls,Message("Draw matching tracks","条件に合う航跡を描画","批量绘制航迹","批次繪製航跡"),()=>
        {
            trackMinTons=float.IsFinite(tons.value)?Mathf.Max(0,tons.value):0;tons.SetValueWithoutNotify(trackMinTons);
            trackDynamicLeaders=dynamicLeaders.value;trackStaticLeaders=staticLeaders.value;
            foreach(var ship in ships)
                if(ship.shipClass.displacementTons>=trackMinTons&&(!trackDynamicLeaders||ship.GetEffectiveControlMode()==ControlMode.Independent)&&(!trackStaticLeaders||World.IsStaticTrackLeader(ship.objectId)))World.SetTrack(ship,true);
            list.RefreshItems();
        });
        ActionButton(controls,Message("Clear all tracks","全航跡を消去","清除所有航迹","清除所有航跡"),()=>{World.ClearTracks();list.RefreshItems();});
        body.Add(Text(Message("Checked ships have tracks enabled. Batch filters are combined; existing tracks remain.","チェックした艦の航跡を表示。条件はすべて適用され、既存の航跡は残ります。","勾选舰艇以绘制航迹，取消勾选以移除。批量筛选同时满足所选条件，保留已有航迹。","勾選艦艇以繪製航跡，取消勾選以移除。批次篩選同時滿足所選條件，保留已有航跡。")));
        body.Add(list);PushDialog(TracksTitle(),body,size:"large");
    }
}
}
