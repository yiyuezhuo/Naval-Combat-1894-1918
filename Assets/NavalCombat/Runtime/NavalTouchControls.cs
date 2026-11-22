using NavalCombat.Application;
using UnityEngine;
using UnityEngine.UIElements;

namespace NavalCombat.Unity
{
public sealed partial class NavalGame
{
    ScrollView touchActions;
    Button touchToggle,touchPause;
    bool touchActionsVisible=true;
    const string HelpSeenKey="Naval.HelpSeen.v1";
    string HelpTitle()=>Message("Help","ヘルプ","帮助","說明");

    void SetupTouchControls()
    {
        var viewport=Q<VisualElement>("viewport");
        touchActions=new ScrollView(ScrollViewMode.Vertical){name="touchActions",horizontalScrollerVisibility=ScrollerVisibility.Hidden};
        touchActions.AddToClassList("hud-overlay");viewport.Add(touchActions);
        Button Add(string name,Action action)
        {
            var button=ActionButton(touchActions,"",action);button.name=name;button.AddToClassList("touch-action");return button;
        }
        touchPause=Add("touchPause",()=>{Session?.TogglePause();RefreshTouchControls();});
        Add("touchSpeed",OpenTouchSpeed);
        Add("touchView",OpenTouchView);
        Add("touchReport",OpenReport);
        Add("touchSettings",OpenSettingsDialog);
        Add("touchMenu",OpenPause);
        Add("touchHelp",()=>OpenHelp());
        Add("touchMap",OpenTouchMap);
        touchToggle=new Button(()=>
        {
            touchActionsVisible=!touchActionsVisible;
            RefreshTouchControls();
        }){name="touchToggle"};
        touchToggle.AddToClassList("hud-overlay");touchToggle.AddToClassList("touch-action");viewport.Add(touchToggle);
        Q<VisualElement>("hudActions").Add(new Button(()=>OpenHelp()){name="helpButton"});
        LocalizeTouchControls();
    }
    void LocalizeTouchControls()
    {
        if(touchActions==null)return;
        Q<Button>("helpButton").text=HelpTitle();
        Q<Button>("touchSpeed").text=Message("Time speed","時間倍率","推进速率","推進速率");
        Q<Button>("touchView").text=Message("View","視点","视角","視角");
        Q<Button>("touchReport").text=L("ActionReport");
        Q<Button>("touchSettings").text=L("Settings");
        Q<Button>("touchMenu").text=Message("Menu","メニュー","菜单","選單");
        Q<Button>("touchHelp").text=HelpTitle();
        Q<Button>("touchMap").text=Message("Map controls","マップ操作","地图操作","地圖操作");
        RefreshTouchControls();
    }
    void RefreshTouchControls()
    {
        if(touchActions==null)return;
        bool visible=CurrentScreen=="battle"&&root.ClassListContains("touch-input");
        touchToggle.style.display=visible?DisplayStyle.Flex:DisplayStyle.None;
        touchActions.style.display=visible&&touchActionsVisible?DisplayStyle.Flex:DisplayStyle.None;
        touchToggle.text=touchActionsVisible
            ?Message("Hide touch controls","タッチ操作を隠す","隐藏触屏按钮","隱藏觸控按鈕")
            :Message("Show touch controls","タッチ操作を表示","显示触屏按钮","顯示觸控按鈕");
        touchPause.text=Session?.Speed==SimulationSpeed.Paused
            ?Message("Play","再生","播放","播放") : Message("Pause","一時停止","暂停","暫停");
        touchPause.SetEnabled(Session?.Phase==BattlePhase.Running);
        Q<Button>("touchSpeed").SetEnabled(Session?.Phase==BattlePhase.Running);
    }
    ScrollView TouchChoices()
    {
        var body=new ScrollView(ScrollViewMode.Vertical){horizontalScrollerVisibility=ScrollerVisibility.Hidden};
        body.AddToClassList("grow");body.AddToClassList("touch-choices");return body;
    }
    void TouchChoice(VisualElement body,string label,Action action,bool enabled=true)
        =>ActionButton(body,label,action,enabled).AddToClassList("touch-action");
    void OpenTouchSpeed()
    {
        var body=TouchChoices();PushDialog(Message("Time speed","時間倍率","推进速率","推進速率"),body,size:"medium");
        foreach(int rate in new[]{1,5,20,60,120})
        {
            var speed=(SimulationSpeed)rate;
            TouchChoice(body,rate+"×",()=>{modalSpeed=speed;CloseDialog();});
        }
    }
    void OpenTouchView()
    {
        var body=TouchChoices();PushDialog(Message("View","視点","视角","視角"),body,size:"medium");
        TouchChoice(body,"2D",()=>{World.SetMode("Chart");CloseDialog();});
        TouchChoice(body,"3D",()=>{World.SetMode("3D");CloseDialog();});
        TouchChoice(body,U("Ui.Bridge.Enter"),()=>{World.SetMode("Bridge");CloseDialog();},SelectedShip!=null && !IsSelectedContact);
        if(SelectedShip==null)body.Add(Text(Message("Select a ship to enter bridge view.","艦橋視点に切り替えるには艦を選択してください。","请先选中舰船，再进入舰桥视角。","請先選中艦船，再進入艦橋視角。")));
    }
    void OpenTouchMap()
    {
        var body=TouchChoices();PushDialog(Message("Map controls","マップ操作","地图操作","地圖操作"),body,size:"medium");
        void Choice(string label,Action action)=>TouchChoice(body,label,()=>{action();CloseDialog();});
        Choice(U("Ui.Scene.Top"),World.TopDown);
        Choice("+  "+Message("Zoom in","拡大","放大","放大"),()=>World.Zoom(1));
        Choice("-  "+Message("Zoom out","縮小","缩小","縮小"),()=>World.Zoom(-1));
        Choice(L("FitAllCurrentShipPositions"),World.Fit);
        Choice(L("Reset"),World.ResetView);
    }
    void OpenHelp(bool firstVisit=false)
    {
        var body=new ScrollView(ScrollViewMode.Vertical){horizontalScrollerVisibility=ScrollerVisibility.Hidden};body.AddToClassList("grow");
        body.AddToClassList("battle-help");
        bool touch=root.ClassListContains("touch-input");
        var tips=new List<string>();
        void Tip(string en,string ja,string cn,string tw)=>tips.Add("- "+Message(en,ja,cn,tw));
        Tip("**Time**: play / pause, with adjustable speed.","**時間**：再生／一時停止。倍率を調整できます。","**时间**：播放／暂停，可调整推进速率。","**時間**：播放／暫停，可調整推進速率。");
        if(touch)Tip("**View**: drag with one finger to pan; pinch to zoom.","**視点**：1本指でドラッグして移動。ピンチで拡大縮小。","**视角**：单指拖动平移；双指捏合缩放。","**視角**：單指拖動平移；雙指捏合縮放。");
        else Tip("**View**: middle-mouse drag to pan; scroll to zoom.","**視点**：中ボタンでドラッグして移動。ホイールで拡大縮小。","**视角**：中键拖动平移；滚轮缩放。","**視角**：中鍵拖動平移；滾輪縮放。");
        if(touch)Tip("**Select**: tap a unit.","**選択**：タップ。","**选择**：轻点单位。","**選擇**：輕點單位。");
        else Tip("**Select**: left-click a unit.","**選択**：左クリック。","**选择**：左键点击单位。","**選擇**：左鍵點擊單位。");
        if(touch)Tip("**Course**: hold on empty sea.","**針路**：何もない海面を長押し。","**航向**：长按空海面设置航向。","**航向**：長按空海面設定航向。");
        else Tip("**Course**: right-click on empty sea.","**針路**：何もない海面を右クリック。","**航向**：右键点击空海面设置航向。","**航向**：右鍵點擊空海面設定航向。");
        Tip("**Orders**: adjust speed and course in the right sidebar.","**命令**：右側パネルで速力と針路を調整。","**下令**：在右侧栏调整速度与航向。","**下令**：在右側欄調整速度與航向。");
        if(touch)Tip("**Details**: hold on the selected ship for more information.","**詳細**：選択艦を長押しして情報を表示。","**详情**：长按已选舰船，查看更多信息。","**詳情**：長按已選艦船，查看更多資訊。");
        else Tip("**Details**: right-click on the selected ship for more information.","**詳細**：選択艦を右クリックして情報を表示。","**详情**：右键点击已选舰船，查看更多信息。","**詳情**：右鍵點擊已選艦船，查看更多資訊。");
        if(touch)Tip("**Follow**: select your ship, then hold on another friendly ship to set follow and other relationships.","**追従**：自艦を選び、別の友軍艦を長押しして追従などの関係を設定。","**跟随**：选中己方舰船后，长按其他友舰，设置跟随等关系。","**跟隨**：選中己方艦船後，長按其他友艦，設定跟隨等關係。");
        else Tip("**Follow**: select your ship, then right-click on another friendly ship to set follow and other relationships.","**追従**：自艦を選び、別の友軍艦を右クリックして追従などの関係を設定。","**跟随**：选中己方舰船后，右键点击其他友舰，设置跟随等关系。","**跟隨**：選中己方艦船後，右鍵點擊其他友艦，設定跟隨等關係。");
        body.Add(new MarkdownRenderer{name="helpMarkdown",value=string.Join("\n",tips)});
        PushDialog(HelpTitle(),body,firstVisit?()=>{PlayerPrefs.SetInt(HelpSeenKey,1);PlayerPrefs.Save();}:null,"medium");
    }
}
}
