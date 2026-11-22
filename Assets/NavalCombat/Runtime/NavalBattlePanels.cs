using CoreUtils;
using NavalCombat.Application;
using NavalCombat.Avalonia.Services;
using NavalCombat.Avalonia.Resources;
using NavalCombatCore;
using UnityEngine;
using UnityEngine.UIElements;

namespace NavalCombat.Unity
{
public sealed partial class NavalGame
{
    bool debugMode;
    bool fogOfWar;
    string planSignature;
    Texture2D vesselPlan;
    ShipNamePrefixMode shipNamePrefix;
    bool trimShipSuffix;
    int shipLabelMode;
    bool hideSmallShipLabels;
    float shipLabelMinPixels=1.7f;
    float nextPanelRefresh;
    void SetupPanels()
    {
        shipNamePrefix=(ShipNamePrefixMode)PlayerPrefs.GetInt("Naval.NamePrefix",(int)ShipNamePrefixMode.Extended);trimShipSuffix=PlayerPrefs.GetInt("Naval.TrimName",1)!=0;
        shipLabelMode=Mathf.Clamp(PlayerPrefs.GetInt("Naval.LabelMode",0),0,3);
        hideSmallShipLabels=PlayerPrefs.GetInt("Naval.HideSmallLabels",0)!=0;
        shipLabelMinPixels=Mathf.Max(0,PlayerPrefs.GetFloat("Naval.LabelMinPixels",1.7f));
        World.SceneStyle=PlayerPrefs.GetInt("Naval.SceneStyle",0);World.ProjectileMode=(NavalCombat.Avalonia.Services.GunneryVisualMode)PlayerPrefs.GetInt("Naval.Projectiles",2);
        audioPlayer.MusicVolume=PlayerPrefs.GetFloat("Naval.MusicVolume",.14f);audioPlayer.EffectsVolume=PlayerPrefs.GetFloat("Naval.EffectsVolume",.3f);
        SetupNativeControls();
        SetupCatalogFilter();
        var wars=Q<DropdownField>("warFilter");wars.choices=WarChoices();wars.SetValueWithoutNotify(wars.choices[0]);wars.RegisterValueChangedCallback(_=>Filter());
        var history=Q<DropdownField>("historyFilter");history.choices=new(){U("Ui.Archive.AllTypes"),U("Ui.Archive.Historical"),U("Ui.Archive.NonHistorical")};history.SetValueWithoutNotify(history.choices[0]);history.RegisterValueChangedCallback(_=>Filter());
        fogOfWar=PlayerPrefs.GetInt("Naval.FogOfWar",0)!=0;
        debugMode=PlayerPrefs.GetInt("Naval.DebugMode",0)!=0;
        Q<Label>("archiveSystemLanguage").text=$"Application.systemLanguage: {UnityEngine.Application.systemLanguage}";
        Q<Label>("archiveSystemLanguage").EnableInClassList("hidden",!debugMode);
        var heading=Q<Slider>("headingSlider");var speed=Q<Slider>("speedSlider");
        heading.RegisterValueChangedCallback(e=>{if(!refreshing&&CanManeuver())Tactical(()=>Session.SetDesiredHeading(SelectedShip.objectId,e.newValue));});
        speed.RegisterValueChangedCallback(e=>{if(!refreshing&&CanManeuver())Tactical(()=>Session.SetDesiredSpeed(SelectedShip.objectId,e.newValue));});
        Q<FloatField>("heading").isDelayed=true;Q<FloatField>("speed").isDelayed=true;
        Q<FloatField>("heading").RegisterValueChangedCallback(e=>{if(!refreshing&&CanManeuver())Tactical(()=>Session.SetDesiredHeading(SelectedShip.objectId,e.newValue));});
        Q<FloatField>("speed").RegisterValueChangedCallback(e=>{if(!refreshing&&CanManeuver())Tactical(()=>Session.SetDesiredSpeed(SelectedShip.objectId,e.newValue));});
        Click("bridgeExit",()=>World.SetMode("Chart"));Click("bridgeAhead",World.BridgeAhead);
        Q<Toggle>("bridgeLock").RegisterValueChangedCallback(e=>World.BridgeLock=e.newValue);
        Q<Toggle>("bridgeLabels").RegisterValueChangedCallback(e=>World.BridgeLabels=e.newValue);
        Q<FloatField>("bridgeHeight").RegisterValueChangedCallback(e=>World.BridgeHeight=Mathf.Clamp(e.newValue,0,30));
        Click("viewTop",World.TopDown);Click("zoomIn",()=>World.Zoom(1));Click("zoomOut",()=>World.Zoom(-1));Click("resetView",World.ResetView);
    }
    void SaveDisplaySettings()
    {
        PlayerPrefs.SetInt("Naval.FogOfWar",fogOfWar?1:0);
        PlayerPrefs.SetInt("Naval.DebugMode",debugMode?1:0);
        PlayerPrefs.SetInt("Naval.LabelMode",shipLabelMode);PlayerPrefs.SetInt("Naval.HideSmallLabels",hideSmallShipLabels?1:0);PlayerPrefs.SetFloat("Naval.LabelMinPixels",shipLabelMinPixels);
        PlayerPrefs.SetInt("Naval.NamePrefix",(int)shipNamePrefix);PlayerPrefs.SetInt("Naval.TrimName",trimShipSuffix?1:0);
        PlayerPrefs.SetInt("Naval.SceneStyle",World.SceneStyle);PlayerPrefs.SetInt("Naval.Projectiles",(int)World.ProjectileMode);
        PlayerPrefs.SetFloat("Naval.MusicVolume",audioPlayer.MusicVolume);PlayerPrefs.SetFloat("Naval.EffectsVolume",audioPlayer.EffectsVolume);PlayerPrefs.Save();
    }
    bool CanManeuver()=>SelectedShip!=null&&Session?.Phase==BattlePhase.Running&&Session.IsPlayerShip(SelectedShip)&&SelectedShip.mapState==MapState.Deployed&&SelectedShip.operationalState==ShipOperationalState.Operational&&!SelectedShip.IsLandBattery();
    bool MatchesFilters(ScenarioCatalogEntry s)
    {
        int war=Q<DropdownField>("warFilter").index,history=Q<DropdownField>("historyFilter").index;
        return (s.IsTutorial?debugMode:MatchesRecommendation(s)) && (war<=0||s.WarCode==new[]{"","SJ","RJ","BR","WW","SA"}[war])&&(history<=0||s.HistoryCode==new[]{"","H","S"}[history]);
    }
    void RefreshPanels()
    {
        if(Session==null)return;
        Q<VisualElement>("bridgeControls").EnableInClassList("hidden",World.ViewMode!="Bridge");
        if(SelectedShip!=null && !IsSelectedContact)
        {
            Q<VisualElement>("maneuverOrders").SetEnabled(CanManeuver());
            var heading=Q<Slider>("headingSlider");heading.SetValueWithoutNotify((float)SelectedShip.desiredHeadingDeg);
            var speed=Q<Slider>("speedSlider");speed.lowValue=(float)MinimumOrderSpeed;speed.highValue=(float)Math.Max(MinimumOrderSpeed+.1,MaximumOrderSpeed);speed.SetValueWithoutNotify((float)SelectedShip.desiredSpeedKnots);
            Q<Label>("currentManeuver").text=CurrentManeuverSummary;
            Q<ProgressBar>("damageBar").value=(float)DamagePercent;
        }
        Q<VisualElement>("maneuverOrders").EnableInClassList("hidden",SelectedShip==null || IsSelectedContact);
        if(Time.unscaledTime<nextPanelRefresh)return;nextPanelRefresh=Time.unscaledTime+1;
        if(!IsSelectedContact){RebuildSelectedDamageStatus();RebuildSelectedWeaponStatuses();}
        Q<Label>("damageAlerts").text=DamageAlerts.Count>0?string.Join("\n",DamageAlerts.Select(a=>a.Text)):L("NoImmediateCriticalCondition");
        RefreshBattleTable();
        RefreshStatusRows();

    }
}
}
