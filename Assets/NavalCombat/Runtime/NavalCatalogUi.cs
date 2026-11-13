using NavalCombat.Application;
using UnityEngine.UIElements;

namespace NavalCombat.Unity
{
public sealed partial class NavalGame
{
    void SetupCatalogFilter()
    {
        var filter=Q<DropdownField>("recommendationFilter");
        filter.choices=new(){"Recommended only","Exclude not recommended","Include not recommended"};filter.index=1;
        filter.RegisterValueChangedCallback(_=>Filter());
    }
    void LocalizeCatalog()
    {
        void Relabel(string name,List<string> choices)
        {
            var field=Q<DropdownField>(name);int selected=Math.Clamp(field.index,0,choices.Count-1);
            field.choices=choices;field.SetValueWithoutNotify(choices[selected]);
        }
        Relabel("warFilter",new(){U("Ui.Archive.AllWars"),U("Ui.Archive.SJ"),U("Ui.Archive.RJ"),U("Ui.Archive.BR"),Message("First World War","第一次世界大戦","第一次世界大战","第一次世界大戰")});
        Relabel("historyFilter",new(){U("Ui.Archive.AllTypes"),U("Ui.Archive.Historical"),U("Ui.Archive.NonHistorical")});
        Q<Toggle>("tutorialFilter").label=Message("Debug","デバッグ","调试","除錯");
        Q<Toggle>("tutorialFilter").tooltip=Message("Show hidden tutorials regardless of recommendation","推奨設定に関係なくチュートリアルを表示","显示默认隐藏的教程，不受推荐度筛选限制","顯示預設隱藏的教學，不受推薦度篩選限制");
        var filter=Q<DropdownField>("recommendationFilter");int index=Math.Clamp(filter.index,0,2);
        filter.choices=new(){Message("Recommended only","推奨のみ","仅推荐","僅推薦"),Message("Exclude not recommended","非推奨を除く","非不推荐","非不推薦"),Message("Include not recommended","非推奨を含む","包含不推荐","包含不推薦")};
        filter.SetValueWithoutNotify(filter.choices[index]);Q<ListView>("scenarios").RefreshItems();
    }
    bool MatchesRecommendation(ScenarioCatalogEntry entry)=>Q<DropdownField>("recommendationFilter").index switch
    {
        0=>entry.Recommendation==ScenarioRecommendation.Recommended,
        1=>entry.Recommendation!=ScenarioRecommendation.Discouraged,
        _=>true
    };
    string CatalogLabel(ScenarioCatalogEntry entry)
    {
        string mark=entry.Recommendation switch {ScenarioRecommendation.Recommended=>" ★",ScenarioRecommendation.Discouraged=>" △",_=>""};
        return entry.GetDisplayName(NavalText.Instance.Culture)+mark+(entry.IsListed?"":" ["+Message("Unlisted","リスト外","非清单","非清單")+"]");
    }
    async Task RefreshCatalogAsync()
    {
        try
        {
            var entries=await source.ScanScenariosAsync(lifetime.Token);
            if(this==null||CurrentScreen!="archive")return;
            catalog=entries.ToList();Filter();
        }
        catch(Exception e){if(this!=null&&e is not OperationCanceledException)Error(e);}
    }
}
}
