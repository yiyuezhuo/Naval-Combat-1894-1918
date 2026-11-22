using CoreUtils;
using NavalCombatCore;
using UnityEngine;
using UnityEngine.UIElements;

namespace NavalCombat.Unity
{
public sealed partial class NavalGame
{
    VisualElement leaderPreview;
    NavalLeaderPortraitView hudLeaderPortrait;
    string previewLeaderId,previewLeaderShipId;

    List<Leader> LeadersAboard(ShipLog ship) => ship?.embarkedLeaders?
        .OrderBy(entry=>entry.GetDisplayPriority()).ThenBy(entry=>entry.GetCommandDepth())
        .Select(entry=>EntityManager.Instance.Get<Leader>(entry.leaderObjectId))
        .Where(leader=>leader!=null).GroupBy(leader=>leader.objectId).Select(group=>group.First()).ToList()??new();

    string LeaderPosition(ShipLog ship,Leader leader)
    {
        if(ship==null||leader==null)return "";
        return string.Join(" · ",ship.embarkedLeaders.Where(entry=>entry.leaderObjectId==leader.objectId).OrderBy(entry=>entry.GetDisplayPriority()).ThenBy(entry=>entry.GetCommandDepth()).Select(entry=>
        {
            if(entry.title!=null)return entry.title.GetMergedName();
            var command=EntityManager.Instance.Get<IShipGroupMember>(entry.commandObjectId);
            var title=entry.post switch
            {
                LeaderPost.Captain=>Message("Captain","艦長","舰长","艦長"),
                LeaderPost.DeputyCommander=>Message("Deputy Commander","副司令官","副司令","副司令"),
                LeaderPost.ChiefOfStaff=>Message("Chief of Staff","参謀長","参谋长","參謀長"),
                LeaderPost.StaffOfficer=>Message("Staff Officer","参謀","参谋","參謀"),
                LeaderPost.VisitingOfficial=>Message("Visiting Official","来訪高官","来访官员","來訪官員"),
                _=>Message("Commander","司令官","司令","司令")
            };
            return (command?.GetMemberName()??ship.GetMemberName())+" "+title;
        }).Distinct());
    }

    void LocalizePortrait(NavalLeaderPortraitView view) => view.Localize(
        Message("Photo","写真","照片","照片"),Message("Hidden","非表示","隐藏","隱藏"),Message("Commander portrait","指揮官の肖像","人物预览","人物預覽"),
        Message("Previous leader","前の指揮官","上一位人物","上一位人物"),Message("Next leader","次の指揮官","下一位人物","下一位人物"));

    void SetupLeaderPreview()
    {
        hudLeaderPortrait=new NavalLeaderPortraitView(this,()=>CycleHudLeader(-1),()=>CycleHudLeader(1));
        leaderPreview=hudLeaderPortrait.Root;leaderPreview.name="leaderPreview";leaderPreview.AddToClassList("hud-overlay");
        hudLeaderPortrait.Mode.name="leaderPreviewMode";
        Q<VisualElement>("viewport").Add(leaderPreview);
    }

    void CycleHudLeader(int direction)
    {
        var leaders=LeadersAboard(SelectedShip);
        if(leaders.Count==0)return;
        int index=Mathf.Max(0,leaders.FindIndex(leader=>leader.objectId==previewLeaderId));
        previewLeaderId=leaders[(index+direction+leaders.Count)%leaders.Count].objectId;
        UpdateHudPreview();
    }

    void LocalizeLeaderPreview(){if(hudLeaderPortrait!=null)LocalizePortrait(hudLeaderPortrait);}

    void UpdateLeaderPreview(bool shipVisible)
    {
        if(hudLeaderPortrait==null)return;
        var ship=IsSelectedContact?null:SelectedShip;
        var leaders=LeadersAboard(ship);
        if(previewLeaderShipId!=ship?.objectId){previewLeaderShipId=ship?.objectId;previewLeaderId=null;}
        var leader=leaders.FirstOrDefault(value=>value.objectId==previewLeaderId)??leaders.FirstOrDefault();
        previewLeaderId=leader?.objectId;
        hudLeaderPortrait.SetLeader(leader,LeaderPosition(ship,leader),leaders.Count>1);
        bool visible=hudLeaderPortrait.ReserveSpace;
        leaderPreview.style.display=leader!=null?DisplayStyle.Flex:DisplayStyle.None;
        float size=Mathf.Min(280,Mathf.Max(120,root.layout.height*.32f));
        float width=size*.8f;
        var viewport=Q<VisualElement>("viewport");
        float right=shipVisible?Mathf.Max(8,viewport.worldBound.xMax-previewSquare.worldBound.xMin)+4:8;
        if(float.IsNaN(right))right=size+12;
        float actualWidth=visible?width:leaders.Count>1?170:110;
        leaderPreview.style.right=right;leaderPreview.style.width=actualWidth;leaderPreview.style.height=visible?size:22;
        leaderPreview.BringToFront();
        float previewLeft=leader!=null?viewport.worldBound.xMax-right-actualWidth:
            shipVisible?previewSquare.worldBound.xMin:viewport.worldBound.xMax-8;
        float logRight=Q<VisualElement>("battle").worldBound.xMax-previewLeft+8;
        if(!float.IsNaN(logRight))hudLog.style.right=Mathf.Max(8,logRight);
    }

    void DisposeLeaderPreview()=>hudLeaderPortrait?.Dispose();
}
}
