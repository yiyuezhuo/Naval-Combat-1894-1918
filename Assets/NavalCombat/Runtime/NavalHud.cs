using NavalCombat.Avalonia.Services;
using NavalCombatCore;
using UnityEngine;
using UnityEngine.UIElements;

namespace NavalCombat.Unity
{
public sealed partial class NavalGame
{
    Toggle fleetDynamic,fleetOob;
    VisualElement fleetPanel,previewSquare;
    VisualElement hudRight,hudLog;
    Foldout shipSection,damageSection,armamentSection;
    Label armamentCompact;
    DropdownField previewMode;
    string previewShip;
    float nextPlanUpdate;
    static bool IsHudTarget(VisualElement target)=>HasHudParent(target);
    static bool HasHudParent(VisualElement target){for(var p=target;p!=null;p=p.parent)if(p.ClassListContains("hud-overlay"))return true;return false;}
    void SetupBattleHud()
    {
        var parked=new VisualElement{name="hudLegacyControls"};parked.style.display=DisplayStyle.None;root.Add(parked);
        parked.Add(Q<Label>("battleTitle"));parked.Add(Q<DropdownField>("language"));
        var mast=root.Q<VisualElement>(className:"masthead");
        var leftToolbar=new VisualElement{name="hudLeftToolbar"};mast.Insert(0,leftToolbar);
        var toolbarSurface=new VisualElement{name="hudToolbarSurface"};leftToolbar.Add(toolbarSurface);
        var actions=new VisualElement{name="hudActions"};actions.AddToClassList("hud-actions");toolbarSurface.Add(actions);
        leftToolbar.Add(Q<Label>("viewHint"));
        actions.Add(new Button(OpenSettingsDialog){name="settingsButton",text=L("Settings")});
        var timePanel=new VisualElement{name="hudTimePanel"};mast.Add(timePanel);
        var timeControls=new VisualElement{name="hudTimeControls"};timePanel.Add(timeControls);
        timeControls.Add(Q<Label>("clock"));
        var speedControls=new VisualElement{name="hudSpeedControls"};timeControls.Add(speedControls);
        foreach(var n in new[]{"pause","speed1","speed5","speed20","speed60","speed120"})speedControls.Add(Q<Button>(n));
        timePanel.Add(Q<Button>("menu"));
        fleetDynamic=new Toggle{name="fleetDynamic",text=""};fleetOob=new Toggle{name="fleetOob",text=""};
        void FleetSwitch(string name,Toggle toggle)
        {
            var group=new VisualElement();group.AddToClassList("fleet-switch");
            var label=new Label{name=name};group.Add(label);group.Add(toggle);actions.Add(group);
            label.RegisterCallback<ClickEvent>(_=>toggle.value=!toggle.value);
        }
        FleetSwitch("fleetDynamicLabel",fleetDynamic);FleetSwitch("fleetOobLabel",fleetOob);
        var bridgeTools=Q<VisualElement>("bridgeControls");
        toolbarSurface.Add(bridgeTools);
        foreach(var name in new[]{"bridgeLock","bridgeLabels"})
        {
            var toggle=Q<Toggle>(name);var group=new VisualElement();group.AddToClassList("fleet-switch");
            var label=new Label{name=name+"Label",tooltip=toggle.tooltip};
            int index=bridgeTools.IndexOf(toggle);bridgeTools.Insert(index,group);group.Add(label);group.Add(toggle);
            toggle.label=null;toggle.text="";toggle.tooltip=null;
            label.RegisterCallback<ClickEvent>(_=>toggle.value=!toggle.value);
        }
        var height=Q<FloatField>("bridgeHeight");height.label=null;
        var heightGroup=new VisualElement();heightGroup.AddToClassList("bridge-height");bridgeTools.Add(heightGroup);
        heightGroup.Add(new Label{name="bridgeHeightLabel"});heightGroup.Add(height);
        foreach(var n in new[]{"chart","threeD","bridge","report","viewTop","zoomIn","zoomOut","fit","resetView"})actions.Add(Q<Button>(n));
        fleetPanel=root.Q<VisualElement>(className:"oob");
        var dynamicUnits=Q<TreeView>("dynamicUnits");var units=Q<TreeView>("units");
        // Preserve controls that existing command/localization adapters still reference.
        foreach(var child in fleetPanel.Children().ToList())parked.Add(child);
        fleetPanel.Add(dynamicUnits);fleetPanel.Add(units);
        void Fleet(bool dynamic,bool enabled)
        {
            if(dynamic){fleetDynamic.SetValueWithoutNotify(enabled);if(enabled)fleetOob.SetValueWithoutNotify(false);}
            else{fleetOob.SetValueWithoutNotify(enabled);if(enabled)fleetDynamic.SetValueWithoutNotify(false);}
            dynamicTree=fleetDynamic.value;fleetPanel.style.display=fleetDynamic.value||fleetOob.value?DisplayStyle.Flex:DisplayStyle.None;
            if(enabled&&root.ClassListContains("touch-input")){touchActionsVisible=false;RefreshTouchControls();}
            dynamicUnits.style.display=fleetDynamic.value?DisplayStyle.Flex:DisplayStyle.None;units.style.display=fleetOob.value?DisplayStyle.Flex:DisplayStyle.None;
            if(Session?.GameState!=null)BuildUnits();
        }
        fleetDynamic.RegisterValueChangedCallback(e=>Fleet(true,e.newValue));fleetOob.RegisterValueChangedCallback(e=>Fleet(false,e.newValue));Fleet(true,false);
        var viewport=Q<VisualElement>("viewport");
        viewport.Add(fleetPanel);fleetPanel.AddToClassList("hud-overlay");
        SetupTouchControls();
        actions.Add(new Button(OpenTracks){name="tracksButton",text=TracksTitle()});
        var log=hudLog=root.Q<VisualElement>(className:"combat-log");Q<VisualElement>("battle").Add(log);log.AddToClassList("hud-overlay");
        log.Query<Label>(className:"section-title").ForEach(l=>l.style.display=DisplayStyle.None);
        log.Add(new Button(OpenLogDetails){name="logDetails",text=Message("Details","詳細","显示详情","顯示詳情")});
        var right=hudRight=root.Q<VisualElement>(className:"inspector-column");viewport.Add(right);right.AddToClassList("hud-overlay");
        var inspector=Q<ScrollView>("inspector");
        inspector.horizontalScrollerVisibility=ScrollerVisibility.Hidden;
        inspector.contentContainer.style.width=Length.Percent(100);
        shipSection=new Foldout{name="shipSection",value=true};damageSection=new Foldout{name="damageSection",value=true};armamentSection=new Foldout{name="armamentSection",value=true};
        foreach(var section in new[]{shipSection,damageSection,armamentSection})section.AddToClassList("hud-section");
        var selection=Q<Label>("selection");var stats=Q<Label>("shipStats");var maneuver=Q<VisualElement>("maneuverOrders");
        var damage=Q<ProgressBar>("damageBar");var table=Q<VisualElement>("damageTable");var alerts=Q<Label>("damageAlerts");var weapons=Q<VisualElement>("weaponReadout");
        foreach(var child in inspector.contentContainer.Children().ToList())parked.Add(child);
        shipSection.contentContainer.Add(stats);shipSection.Add(maneuver);inspector.Add(shipSection);
        // Persistent headers remain outside Foldout content, so collapsing never hides them.
        shipSection.Insert(0,selection);selection.style.display=DisplayStyle.None;
        inspector.Add(damageSection);damageSection.Add(table);damageSection.Add(alerts);
        var damageHeader=new VisualElement{name="damageHeader"};
        damageSection.hierarchy.Insert(1,damageHeader);damageHeader.Add(damage);
        inspector.Add(armamentSection);armamentCompact=new Label{name="armamentCompact"};armamentCompact.AddToClassList("armament-compact");armamentSection.hierarchy.Insert(1,armamentCompact);armamentSection.Add(weapons);
        armamentCompact.style.display=DisplayStyle.None;
        armamentSection.RegisterValueChangedCallback(e=>armamentCompact.style.display=e.newValue?DisplayStyle.None:DisplayStyle.Flex);
        previewMode=new DropdownField(new List<string>{"2D","3D",Message("Hidden","非表示","隐藏","隱藏")},1){name="previewMode"};
        var preview=Q<Image>("vesselPlan");previewSquare=new VisualElement{name="previewSquare"};previewSquare.AddToClassList("preview-square");
        right.Add(previewSquare);previewSquare.Add(preview);right.Add(previewMode);
        previewMode.RegisterValueChangedCallback(_=>{previewShip=null;UpdateHudPreview();});
        previewSquare.RegisterCallback<GeometryChangedEvent>(_=>UpdateHudPreview());
        LocalizeHud();
        var compassSpace=new VisualElement{name="mapCompass",pickingMode=PickingMode.Ignore};viewport.Add(compassSpace);
        var compass=new Label("N ↑"){name="mapNorth",pickingMode=PickingMode.Ignore};compassSpace.Add(compass);
        var scaleBar=new VisualElement{name="mapScale",pickingMode=PickingMode.Ignore};
        scaleBar.Add(new Label{name="mapScaleText",pickingMode=PickingMode.Ignore});viewport.Add(scaleBar);
        viewport.Add(new Label{name="cursorRange",pickingMode=PickingMode.Ignore});
    }
    void LocalizeHud()
    {
        if(fleetDynamic==null)return;
        LocalizeTouchControls();
        Q<Label>("fleetDynamicLabel").text=L("DynamicControl");Q<Label>("fleetOobLabel").text=L("PreBattleOob");
        fleetDynamic.tooltip=L("DynamicControl");fleetOob.tooltip=L("PreBattleOob");
        damageSection.text=L("DamageCondition");armamentSection.text=L("Armament");
        Q<Button>("settingsButton").text=L("Settings");
        Q<Button>("tracksButton").text=TracksTitle();
        Q<Button>("logDetails").text=Message("Details","詳細","显示详情","顯示詳情");
        Q<Label>("bridgeHeightLabel").text=Message("Height (m)","高さ (m)","观察高度（米）","觀察高度（米）");
        if(previewMode!=null){int mode=previewMode.index;previewMode.choices=new(){"2D","3D",Message("Hidden","非表示","隐藏","隱藏")};previewMode.SetValueWithoutNotify(previewMode.choices[Mathf.Clamp(mode,0,2)]);}
    }
    void RefreshHudLog()
    {
        var scroll=Q<ScrollView>("battleLog");scroll.Clear();
        foreach(var row in battleTableRows.Take(6))scroll.Add(Text($"{row[0]}  {row[1]}  {row[2]}","hud-log-line"));
        if(battleTableRows.Count==0)scroll.Add(Text(Message("No combat events","戦闘記録なし","暂无战斗记录","暫無戰鬥記錄"),"hud-log-line"));
        if(selection!=null)shipSection.text=selection.GetMemberName();
        if(SelectedShip!=null)
        {
            Q<ProgressBar>("damageBar").title=$"{DamagePercent:0.0}%";
            var guns=SelectedShip.shipClass.batteryRecords.Select(b=>(Caliber:b.shellSizeInch,Count:b.mountLocationRecords.Sum(m=>m.barrels)))
                .Concat(SelectedShip.shipClass.rapidFireBatteryRecords.Select(b=>(Caliber:b.GetShellSizeInch(),Count:b.barrelsLevelPort.FirstOrDefault()+b.barrelsLevelStarboard.FirstOrDefault())));
            armamentCompact.text=string.Join(" · ",guns.GroupBy(b=>b.Caliber).Select(g=>$"{g.Key:0.##}″ ×{g.Sum(b=>b.Count)}"));
            var tubes=SelectedShip.shipClass.torpedoSector.mountLocationRecords.Sum(m=>m.barrels);
            armamentCompact.text+=$" · {Message("Tubes","発射管","鱼雷管","魚雷管")} ×{tubes}";
        }
        else armamentCompact.text="";
    }
    void OpenLogDetails()
    {
        if(Session==null)return;
        var rows=Session.GameState.shipLogs.SelectMany(ship=>ship.logs.Select(log=>(ship,log))).OrderByDescending(x=>x.log.time)
            .Select(x=>$"{Session.ToScenarioTime(x.log.time):HH:mm:ss}  {x.ship.GetMemberName()}\n{x.log.SummaryContent()}").ToList();
        var list=new ListView{name="logDetailList",itemsSource=rows,virtualizationMethod=CollectionVirtualizationMethod.DynamicHeight,selectionType=SelectionType.None};
        list.makeItem=()=>Text("","log-detail-line");list.bindItem=(e,i)=>((Label)e).text=rows[i];list.AddToClassList("grow");
        PushDialog(L("CombatLog"),list,size:"large");
    }
    void UpdateHudPreview()
    {
        if(previewMode==null)return;
        RefreshTouchControls();
        UpdateMapReference();
        if(World.ViewMode=="Bridge")Q<Label>("viewHint").text=Message("Drag: look around · Wheel: zoom · Right click: ship menu · Space: pause","ドラッグ：見回す · ホイール：ズーム · 右クリック：艦メニュー · Space：一時停止","拖动：转动视角 · 滚轮：缩放 · 右键：舰只菜单 · 空格：暂停","拖動：轉動視角 · 滾輪：縮放 · 右鍵：艦隻選單 · 空格：暫停");
        else Q<Label>("viewHint").text=Message("Select: left click · Right click: ship menu / sea course · Pan: middle drag · Orbit: Shift+middle drag · Zoom: wheel · Pause: Space","左クリック：選択 · 右クリック：命令 · 中ドラッグ：移動 · Shift＋中ドラッグ：回転 · ホイール：ズーム · Space：一時停止","左键选择 · 右键舰只菜单／空海面下达航向 · 中键拖动 · Shift+中键旋转 · 滚轮缩放 · 空格暂停","左鍵選擇 · 右鍵艦隻選單／空海面下達航向 · 中鍵拖動 · Shift+中鍵旋轉 · 滾輪縮放 · 空格暫停");
        if(root.ClassListContains("touch-input"))Q<Label>("viewHint").text=World.ViewMode=="Bridge"
            ?Message("Drag: look around · Pinch: zoom · Hold: ship menu / sea course","ドラッグ：見回す · ピンチ：ズーム · 長押し：艦メニュー／針路","单指拖动环顾 · 双指捏合缩放 · 长按舰船菜单／海面设置航向","單指拖動環顧 · 雙指捏合縮放 · 長按艦船選單／海面設定航向")
            :Message("Tap: select · Drag: pan · Pinch: zoom · Two-finger drag: 3D orbit · Hold: ship menu / sea course","タップ：選択 · ドラッグ：移動 · ピンチ：ズーム · 2本指：3D回転 · 長押し：命令","轻点选择 · 单指拖动平移 · 双指捏合缩放／拖动环绕3D · 长按舰船菜单／海面设置航向","輕點選擇 · 單指拖動平移 · 雙指捏合縮放／拖動環繞3D · 長按艦船選單／海面設定航向");
        fleetPanel.BringToFront();hudLog.BringToFront();hudRight.BringToFront();
        touchActions.BringToFront();touchToggle.BringToFront();
        if(selection!=null)shipSection.text=selection.GetMemberName();
        bool show=SelectedShip!=null&&previewMode.index!=2;previewSquare.style.display=show?DisplayStyle.Flex:DisplayStyle.None;
        previewMode.style.bottom=show?Mathf.Min(280,Mathf.Max(120,root.layout.height*.32f))-24:0;
        if(!show){World.HideShipPreview();return;}
        float size=Mathf.Min(280,Mathf.Max(120,root.layout.height*.32f));previewSquare.style.width=size;previewSquare.style.height=size;
        var image=Q<Image>("vesselPlan");
        if(previewMode.index==1)
        {
            image.style.rotate=new Rotate(0);image.style.width=Length.Percent(100);image.style.height=Length.Percent(100);image.style.left=0;image.style.top=0;
            image.image=World.UpdateShipPreview(SelectedShip);return;
        }
        World.HideShipPreview();
        if(previewShip!=SelectedShip.objectId||Time.unscaledTime>=nextPlanUpdate)
        {
            var key=SelectedShip.objectId+"|"+ShipDetailRenderer.BuildDirectionSignature(SelectedShip)+"|"+SelectedShip.mapState;
            if(planSignature!=key||vesselPlan==null){planSignature=key;if(vesselPlan!=null)Destroy(vesselPlan);vesselPlan=ShipDetailRenderer.RenderBitmap(SelectedShip);}
            previewShip=SelectedShip.objectId;nextPlanUpdate=Time.unscaledTime+.5f;
        }
        image.image=vesselPlan;if(vesselPlan==null)return;
        float scale=(size-24)/Mathf.Sqrt(vesselPlan.width*vesselPlan.width+vesselPlan.height*vesselPlan.height);
        float width=vesselPlan.width*scale,height=vesselPlan.height*scale;
        image.style.width=width;image.style.height=height;image.style.left=(size-width)/2;image.style.top=(size-height)/2;
        image.style.rotate=new Rotate((float)SelectedShip.headingDeg-90);
    }

    void UpdateMapReference()
    {
        var range=Q<Label>("cursorRange");range.text="";
        if(mapHover.HasValue && !modal && SelectedShip!=null && World.SeaPoint(PanelToScreen(mapHover.Value),out var point))
        {
            GeographicLib.Geodesic.WGS84.Inverse(SelectedShip.position.LatDeg,SelectedShip.position.LonDeg,point.LatDeg,point.LonDeg,out var metersToCursor,out _,out _);
            double yards=metersToCursor/.9144;
            range.text=yards<=35000?$"↔ {yards:N0} yd":$"↔ {metersToCursor/1852:0.0} nmi";
        }
        var north=Q<Label>("mapNorth");var scale=Q<VisualElement>("mapScale");
        var camera=World.ViewCamera;
        north.style.rotate=new Rotate(-camera.transform.eulerAngles.y);
        var center=new Vector2(Screen.width*.5f,Screen.height*.5f);
        bool visible=World.ViewMode!="Bridge" && World.SeaPoint(center,out var a) && World.SeaPoint(center+Vector2.right*100,out var b);
        scale.style.display=visible?DisplayStyle.Flex:DisplayStyle.None;
        if(!visible)return;
        World.SeaPoint(center,out a);World.SeaPoint(center+Vector2.right*100,out b);
        float meters=Vector3.Distance(World.Project(a.LatDeg,a.LonDeg),World.Project(b.LatDeg,b.LonDeg));
        if(meters<=0)return;
        bool nautical=meters>=1852;
        float metersPerUnit=nautical?1852:.9144f;
        float units=meters/metersPerUnit;
        float unit=Mathf.Pow(10,Mathf.Floor(Mathf.Log10(units))),n=units/unit;
        float value=(n>=5?5:n>=2?2:1)*unit;
        float panelRatio=RuntimePanelUtils.ScreenToPanel(root.panel,new Vector2(100,0)).x-RuntimePanelUtils.ScreenToPanel(root.panel,Vector2.zero).x;
        scale.style.width=panelRatio*value*metersPerUnit/meters;
        Q<Label>("mapScaleText").text=nautical?$"{value:0.#} nmi":$"{value:0.#} yd";
    }
}
}
