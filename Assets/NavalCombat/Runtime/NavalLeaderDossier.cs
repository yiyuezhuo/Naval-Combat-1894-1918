using CoreUtils;
using NavalCombat.Avalonia.ViewModels;
using UnityEngine;
using UnityEngine.UIElements;

namespace NavalCombat.Unity
{
public sealed partial class NavalGame
{
    void OpenLeaderDossier(DossierReference reference)
    {
        var initial=EntityManager.Instance.Get<Leader>(reference.ObjectId);
        if(initial==null)return;
        var ship=Session?.GameState?.shipLogs.FirstOrDefault(candidate=>candidate.embarkedLeaders.Any(entry=>entry.leaderObjectId==initial.objectId));
        var leaders=LeadersAboard(ship);
        if(leaders.Count==0)leaders.Add(initial);
        int index=Mathf.Max(0,leaders.FindIndex(leader=>leader.objectId==initial.objectId));
        var layout=new VisualElement{name="leaderDossier"};layout.AddToClassList("leader-dossier");
        var details=new VisualElement{name="leaderDossierDetails"};details.AddToClassList("leader-dossier-details");
        NavalLeaderPortraitView portrait=null;
        DossierSnapshot snapshot=null;
        void Refresh()
        {
            var leader=leaders[index];
            portrait.SetLeader(leader,LeaderPosition(ship,leader),leaders.Count>1);
        }
        void Cycle(int delta){index=(index+delta+leaders.Count)%leaders.Count;Refresh();}
        portrait=new NavalLeaderPortraitView(this,()=>Cycle(-1),()=>Cycle(1));
        portrait.DisplayedLeaderChanged+=leader=>
        {
            snapshot?.Dispose();snapshot=DossierSnapshotFactory.CreateLeader(leader);
            details.Clear();
            if(snapshot!=null){details.Add(Text(snapshot.Title,"title"));details.Add(BuildDossierBody(snapshot));}
        };
        portrait.Root.name="leaderDossierPortrait";portrait.Root.AddToClassList("leader-dossier-portrait");
        LocalizePortrait(portrait);layout.Add(portrait.Root);layout.Add(details);Refresh();
        PushDialog(Message("Leader record","人物記録","人物档案","人物檔案"),layout,()=>{portrait.Dispose();snapshot?.Dispose();},"large");
    }
}
}
