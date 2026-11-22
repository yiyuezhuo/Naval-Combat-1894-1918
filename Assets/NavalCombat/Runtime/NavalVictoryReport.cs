using NavalCombatCore;
using UnityEngine;
using UnityEngine.UIElements;

namespace NavalCombat.Unity
{
public sealed partial class NavalGame
{
    VisualElement BuildVictoryReport()
    {
        var status=VictoryStatus.Generate(Session.GameState);var body=new VisualElement{name="victoryReport"};
        body.AddToClassList("grow");var tabs=new TabView();body.Add(tabs);
        var summary=new Tab(Message("Summary","概要","汇总","彙總"));var detail=new Tab(Message("Detail","詳細","逐舰详情","逐艦詳情"));tabs.Add(summary);tabs.Add(detail);
        var summaryScroll=new ScrollView();summaryScroll.AddToClassList("grow");summary.Add(summaryScroll);
        var detailScroll=new ScrollView();detailScroll.AddToClassList("grow");detail.Add(detailScroll);
        foreach(var side in status.sideVictoryStatuses)
        {
            bool restricted=Session.HasPlayerFog && Session.Phase!=NavalCombat.Application.BattlePhase.Ended;
            if(restricted && side.sideObjectId!=Session.PlayerRootGroupId)continue;
            var card=new VisualElement();card.AddToClassList("victory-card");summaryScroll.Add(card);
            card.Add(Text(restricted?side.name:side.name+" · "+NavalText.Instance.GetEnum(side.victoryLevel),"title"));
            if(!restricted)card.Add(Text($"{Message("Committed VP","投入戦力","投入胜利点","投入勝利點")}: {side.initialCommitVictoryPoint:0.0}   VP: {side.victoryPoint:0.0}   {Message("Loss VP","損失戦力","损失胜利点","損失勝利點")}: {side.lossVictoryPoint:0.0}\n{Message("Self loss ratio","自軍損失率","己方损失率","己方損失率")}: {side.selfLossRatio:P1}   {Message("Enemy/self loss ratio","敵味方損失比","敌我损失比","敵我損失比")}: {side.otherOverSelfLossRatio:0.00}"));
            var rows=side.shipTypeLossItems.Select(s=>new[]{NavalText.Instance.GetEnum(s.shipType),s.undamaged.ToString(),s.light.ToString(),s.medium.ToString(),s.heavy.ToString(),s.sunk.ToString(),s.lossVictoryPoint.ToString("0.0")}).ToList();
            var table=new MultiColumnListView();table.style.height=40+28*Mathf.Min(10,rows.Count);
            ConfigureTable(table,rows,new[]{Message("Type","艦種","舰种","艦種"),Message("None","無傷","完好","完好"),Message("Light","軽微","轻伤","輕傷"),Message("Medium","中破","中伤","中傷"),Message("Heavy","大破","重伤","重傷"),Message("Sunk","沈没","沉没","沉沒"),"Loss VP"},new[]{170f,75f,75f,75f,75f,75f,100f});card.Add(table);
            var details=new VisualElement();details.AddToClassList("victory-card");detailScroll.Add(details);details.Add(Text(side.name,"title"));
            details.Add(Text(Message("Yellow: remaining · Red: damage in this battle · Empty: initial damage","黄：残存 · 赤：今回損害 · 空：初期損害","黄色：剩余 · 红色：本场新增损伤 · 空白：初始损伤","黃色：剩餘 · 紅色：本場新增損傷 · 空白：初始損傷")));
            foreach(var ship in side.shipDetailItems)
            {
                var row=new VisualElement();row.AddToClassList("victory-ship");details.Add(row);
                row.Add(Text(ship.name+(ship.isSunk?" · "+Message("Sunk","沈没","沉没","沉沒"):""),"section-title"));
                var bar=new VisualElement();bar.AddToClassList("victory-damage");row.Add(bar);
                void Segment(double ratio,Color color){var segment=new VisualElement();segment.style.width=Length.Percent((float)(ratio*100));segment.style.backgroundColor=color;bar.Add(segment);}
                Segment(ship.yellowRatio,new Color(.82f,.7f,.22f));Segment(ship.redRatio,new Color(.75f,.24f,.2f));Segment(ship.transparentRatio,Color.clear);
                if(restricted)row.Add(Text($"{Message("Damage","損害","损伤","損傷")} · {ship.initialDamageRatio:P1} → {ship.currentDamageRatio:P1}"));
                else row.Add(Text($"{Message("Damage","損害","损伤","損傷")}: {ship.initialDamageRatio:P1} → {ship.currentDamageRatio:P1}   {Message("Shots / hits / received","発射 / 命中 / 被弾","发射 / 命中 / 被命中","發射 / 命中 / 被命中")}: {ship.shotsFiredCount} / {ship.hitsLandedCount} / {ship.hitsTakenCount}\nDP −{ship.damagePointLost:0.0}   {Message("Inflicted DP","与損害 DP","造成 DP 损失","造成 DP 損失")}: {ship.damagePointInflicted:0.0}"));
            }
        }
        return body;
    }
}
}
