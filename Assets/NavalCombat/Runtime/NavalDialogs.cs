using CoreUtils;
using NavalCombat.Application;
using NavalCombat.Avalonia.Resources;
using NavalCombat.Avalonia.ViewModels;
using NavalCombatCore;
using UnityEngine;
using UnityEngine.UIElements;

namespace NavalCombat.Unity
{
public sealed partial class NavalGame
{
    sealed class DialogPage
    {
        public string Title;
        public string Size;
        public VisualElement Body;
        public VisualElement Actions=new();
        public Action Dispose;
    }
    readonly Stack<DialogPage> pages=new();
    VisualElement contextMenu;
    static string U(string key,params object[] args)=>NavalCombatStrings.Text(key,args);
    static Label Text(string value,string css="prose") {var label=new Label(value);label.AddToClassList(css);return label;}
    Button ActionButton(VisualElement parent,string label,Action action,bool enabled=true)
    {
        var button=new Button(()=>Execute(action)){text=label};button.SetEnabled(enabled);parent.Add(button);return button;
    }
    DialogPage PushDialog(string title,VisualElement body,Action dispose=null,string size="compact")
    {
        CloseContext();
        if(!modal){modalSpeed=Session?.Speed??SimulationSpeed.Paused;Session?.SetSimulationSpeed(SimulationSpeed.Paused);}
        CancelMapGestures();
        modal=true;
        audioPlayer.SetPresentationState(CurrentScreen=="battle",true,World);
        var page=new DialogPage{Title=title,Body=body,Dispose=dispose,Size=size};page.Actions.AddToClassList("toolbar");pages.Push(page);
        ActionButton(page.Actions,L("Close"),CloseDialog);RenderDialog();return page;
    }
    void RenderDialog()
    {
        if(pages.Count==0)return;
        var page=pages.Peek();
        var frame=root.Q<VisualElement>(className:"dialog");foreach(var size in new[]{"compact","medium","large"})frame.EnableInClassList("dialog-"+size,page.Size==size);
        Q<Label>("modalTitle").text=page.Title;
        Q<VisualElement>("modalBody").Clear();Q<VisualElement>("modalBody").Add(page.Body);
        Q<VisualElement>("modalActions").Clear();Q<VisualElement>("modalActions").Add(page.Actions);
        Q<VisualElement>("modal").RemoveFromClassList("hidden");
        page.Actions.Q<Button>()?.Focus();
    }
    void CloseDialog()
    {
        if(pages.Count>0)pages.Pop().Dispose?.Invoke();
        if(pages.Count>0){RenderDialog();return;}
        modal=false;Q<VisualElement>("modal").AddToClassList("hidden");
        audioPlayer.SetPresentationState(CurrentScreen=="battle",false,World);
        if(Session?.Phase==BattlePhase.Running)Session.SetSimulationSpeed(modalSpeed);
    }
    void ClearDialogs(bool resume=false)
    {
        while(pages.Count>0)pages.Pop().Dispose?.Invoke();modal=false;Q<VisualElement>("modal").AddToClassList("hidden");CloseContext();
        audioPlayer.SetPresentationState(CurrentScreen=="battle",false,World);
        if(resume && Session?.Phase==BattlePhase.Running)Session.SetSimulationSpeed(modalSpeed);
    }
    void Confirm(string title,Action action)
    {
        var page=PushDialog(title,Text(U("Ui.Pause.DiscardConfirm")));
        page.Actions.Clear();ActionButton(page.Actions,U("Ui.Pause.Cancel"),CloseDialog);
        ActionButton(page.Actions,U("Ui.Pause.Confirm"),()=>{ClearDialogs();action();});
    }
    void OpenPause()
    {
        var body=new VisualElement();body.AddToClassList("menu-body");body.Add(Text(Session?.ScenarioTitle??L("ScenarioArchiveNavalCombat")));
        PushDialog(U("Ui.Pause.Paused"),body).Actions.Clear();
        ActionButton(body,U("Ui.Pause.Resume"),CloseDialog);
        ActionButton(body,U("Ui.Pause.Settings"),OpenSettingsDialog);
        if(Session!=null)
        {
            ActionButton(body,U("Ui.Pause.Restart"),()=>Confirm(U("Ui.Pause.Restart"),LoadChosen));
            ActionButton(body,L("ActionReport"),OpenReport);
            ActionButton(body,Message("Conclude battle","戦闘終了","结束战斗","結束戰鬥"),()=>Confirm(L("AfterActionReport"),()=>{Session.ConcludeBattle("Action concluded by player.");OpenReport();}),Session.Phase==BattlePhase.Running);
        }
        ActionButton(body,U("Ui.Pause.Scenarios"),()=>Confirm(U("Ui.Pause.Scenarios"),ReturnToArchive));
    }
    void OpenReport()
    {
        if(Session==null)return;
        if(Session.Phase==BattlePhase.Ended)lastPhase=Session.Phase;
        var page=PushDialog(L("AfterActionReport"),BuildVictoryReport(),size:"large");
        if(Session.Phase==BattlePhase.Ended)
        {
            page.Actions.Clear();ActionButton(page.Actions,L("ViewFinalSituation"),()=>ClearDialogs());
            ActionButton(page.Actions,U("Ui.Pause.Restart"),()=>Confirm(U("Ui.Pause.Restart"),LoadChosen));
            ActionButton(page.Actions,U("Ui.Pause.Scenarios"),()=>{ClearDialogs();ReturnToArchive();});
        }
    }
    void OpenFault(string message,string details)
    {
        LastError=details;
        var body=new VisualElement();body.AddToClassList("grow");body.Add(Text(message));
        var technical=new TextField(L("TechnicalDetailsSelectable")){value=details??message,multiline=true,isReadOnly=true};technical.AddToClassList("fault-details");body.Add(technical);
        var page=PushDialog(L("TheRequestedTacticalOrderIsNotValid"),body);
        ActionButton(page.Actions,L("CopyErrorDetails"),()=>GUIUtility.systemCopyBuffer=technical.value);
        ActionButton(page.Actions,U("Ui.Pause.Restart"),()=>{ClearDialogs();LoadChosen();});
        ActionButton(page.Actions,U("Ui.Pause.Scenarios"),()=>{ClearDialogs();ReturnToArchive();});
    }
    void OpenCommandError(string message)=>PushDialog(L("TheRequestedTacticalOrderIsNotValid"),Text(message));
    void Tactical(Action action)
    {
        try{action();RefreshSelection(true);BuildUnits();}
        catch(InvalidOperationException e){OpenCommandError(e.Message);}
        catch(ArgumentException e){OpenCommandError(e.Message);}
    }
    void CloseContext(){contextMenu?.RemoveFromHierarchy();contextMenu=null;}
    public void ShowTacticalMenu(ShipLog hit,Vector2 panelPosition)
    {
        CloseContext();if(hit==null || Session==null)return;
        if(World.ViewMode=="Bridge" && EntityManager.Instance.Get<ShipLog>(World.BridgeObserverId) is {} observer)Select(observer);
        var actor=selection as ShipLog;
        if(actor==null){Select(hit);actor=hit;}
        contextMenu=new VisualElement{name="tacticalContextMenu"};contextMenu.AddToClassList("tactical-context");root.Add(contextMenu);
        void Item(string label,Action action,bool enabled=true){ActionButton(contextMenu,label,()=>{CloseContext();Tactical(action);},enabled).AddToClassList("context-item");}
        bool can=Session.Phase==BattlePhase.Running&&Session.IsPlayerShip(actor)&&actor.mapState==MapState.Deployed&&actor.operationalState==ShipOperationalState.Operational;
        if(hit==actor)
        {
            if(can&&!actor.IsLandBattery())
            {
                Item(L("Detach"),()=>Session.DetachShip(actor.objectId));
                Item(U("Ui.Reverse Control Chain"),()=>Session.ReverseControlChain(actor.objectId));
                Item(U("Ui.Arrange formation in relative bearing"),()=>OpenFormation(actor,"RelativeFormation"));
                Item(U("Ui.Arrange formation in follow chain"),()=>OpenFormation(actor,"FollowFormation"));
            }
            Item(U("Ui.TorpedoAnalysis.Title"),()=>OpenTorpedoAnalysis(hit),hit.shipClass.torpedoSector.torpedoSettings.Count>0&&hit.torpedoSectorStatus.mountStatus.Count>0);
            Item(L("ShowShipClass"),()=>OpenDossier(new(DossierKind.ShipClass,hit.shipClass.objectId,"")));
            Item(L("ShowNamedShip"),()=>OpenDossier(new(DossierKind.NamedShip,hit.namedShip.objectId,"")),hit.namedShip!=null);
            Item(L("ShowShipState"),()=>OpenDossier(new(DossierKind.ShipState,hit.objectId,"")));
            Item(L("ShowLeader"),()=>OpenDossier(new(DossierKind.Leader,hit.leader.objectId,"")),hit.leader!=null);
        }
        else if(can && Session.GetSideId(actor)==Session.GetSideId(hit)&&!actor.IsLandBattery())
        {
            Item(L("Follow"),()=>Session.SetFollowTarget(actor.objectId,hit.objectId));
            Item(L("Relative"),()=>Session.SetRelativeTarget(actor.objectId,hit.objectId));
            if(actor.controlMode==ControlMode.FollowTarget)Item(L("EditFollowParameter"),()=>OpenFormation(actor,"FollowParameters"));
            if(actor.controlMode==ControlMode.RelativeToTarget)Item(L("EditRelativeParameter"),()=>OpenFormation(actor,"RelativeParameters"));
        }
        else if(can && Session.GetSideId(actor)!=Session.GetSideId(hit))
        {
            Item(U("Core.TheaterPosture.Attack"),()=>Session.SetAttackConstraint(actor.objectId,hit.objectId));
            if(!string.IsNullOrWhiteSpace(actor.shipLevelFiringTargetObjectId))Item(L("CancelAttack"),()=>Session.CancelAttackConstraint(actor.objectId));
        }
        if(contextMenu.childCount==0){CloseContext();return;}
        var menu=contextMenu;
        menu.transform.position=new Vector3(panelPosition.x,panelPosition.y);
        menu.schedule.Execute(()=>{if(menu.panel!=null)menu.transform.position=new Vector3(Mathf.Clamp(panelPosition.x,0,Mathf.Max(0,root.layout.width-menu.layout.width)),Mathf.Clamp(panelPosition.y,0,Mathf.Max(0,root.layout.height-menu.layout.height)));});
        menu.Q<Button>()?.Focus();
    }
    public void OpenFormation(ShipLog ship,string kind)
    {
        bool formation=kind.EndsWith("Formation"),relative=kind.StartsWith("Relative");
        var body=new ScrollView();body.AddToClassList("grow");
        var distance=new DoubleField(L("DistanceYards")){name="orderDistance",value=formation?(relative?250:500):relative?ship.relativeToTargetDistanceYards:ship.followDistanceYards};body.Add(distance);
        var bearing=new DoubleField(L("BearingDegrees")){name="orderBearing",value=formation?90:ship.relativeToTargetAzimuth};
        var absolute=new Toggle(L("AbsoluteBearingFromTrueNorth")){name="orderAbsolute",value=!formation&&ship.relativeToAbsolute};
        var symmetric=new Toggle(L("SymmetricFormation")){name="orderSymmetric"};
        var modes=Enum.GetValues(typeof(RelativeFormationMode)).Cast<RelativeFormationMode>().ToList();
        var mode=new DropdownField(L("FormationPattern"),modes.Select(m=>NavalText.Instance.GetEnum(m)).ToList(),0){name="formationPattern"};
        if(relative){if(formation)body.Add(mode);body.Add(bearing);body.Add(absolute);if(formation)body.Add(symmetric);}
        if(relative&&formation)
        {
            void EnablePattern(){bool editable=modes[mode.index]!=RelativeFormationMode.KeepCurrentPosition;distance.SetEnabled(editable);bearing.SetEnabled(editable);symmetric.SetEnabled(editable);}
            mode.RegisterValueChangedCallback(_=>EnablePattern());EnablePattern();body.Add(Text(L("KeepCurrentPositionRecordsEachShipSPresentBearingAndDistanceOtherPatte7b22c0e4")));
        }
        else if(relative)body.Add(Text(L("ClearTheOptionToMeasureTheBearingFromTheReferenceShipSHeading")));
        var error=Text("","validation-error");error.name="orderValidation";body.Add(error);
        var page=PushDialog(relative?(formation?U("Ui.Arrange formation in relative bearing"):L("EditRelativeParameter")):(formation?U("Ui.Arrange formation in follow chain"):L("EditFollowParameter")),body);
        ActionButton(page.Actions,L("Apply"),()=>
        {
            try
            {
                if(!double.IsFinite(distance.value)||distance.value<=0||!double.IsFinite(bearing.value))throw new ArgumentException(Message("Enter a positive distance and a finite bearing.","正の距離と有効な方位を入力してください。","请输入正数间距和有效方位角。","請輸入正數間距和有效方位角。"));
                if(kind=="FollowParameters")Session.SetFollowParameters(ship.objectId,distance.value);
                else if(kind=="RelativeParameters")Session.SetRelativeParameters(ship.objectId,distance.value,bearing.value,absolute.value);
                else if(kind=="FollowFormation")Session.ArrangeFollowFormation(ship.objectId,distance.value);
                else Session.ArrangeRelativeFormation(ship.objectId,new RelativeFormationRequest{mode=modes[mode.index],distanceYards=distance.value,angleDeg=bearing.value,absolute=absolute.value,isSymmetric=symmetric.value});
                CloseDialog();RefreshSelection(true);BuildUnits();
            }
            catch(Exception e) when(e is ArgumentException or InvalidOperationException){error.text=e.Message;}
        });
    }
    public void OpenDossier(DossierReference reference)
    {
        var snapshot=DossierSnapshotFactory.Create(Session,reference);if(snapshot==null)return;
        var body=new VisualElement();body.AddToClassList("grow");
        body.Add(Text(snapshot.Eyebrow+" · "+snapshot.Subtitle));body.Add(Text(snapshot.SnapshotTime));
        var links=new VisualElement();links.AddToClassList("toolbar");body.Add(links);
        foreach(var link in snapshot.Links)ActionButton(links,link.Label+": "+link.Value,()=>OpenDossier(link.Reference),link.Reference!=null).tooltip=link.UnavailableReason;
        var tabs=new TabView();tabs.AddToClassList("grow");body.Add(tabs);
        void Show(DossierTabSnapshot tab)
        {
            var page=new Tab(tab.Title);tabs.Add(page);var content=new ScrollView();content.AddToClassList("grow");page.Add(content);
            foreach(var section in tab.Sections)
            {
                content.Add(Text(section.Title,"section-title"));
                if(section.ShowShipPlan&&snapshot.PlanBitmap!=null){var plan=new Image{image=snapshot.PlanBitmap,scaleMode=ScaleMode.ScaleToFit};plan.AddToClassList("dossier-plan");content.Add(plan);}
                var facts=new VisualElement();facts.AddToClassList("dossier-facts");content.Add(facts);
                foreach(var fact in section.Facts??Array.Empty<DossierFactSnapshot>())
                {
                    var cell=new VisualElement();cell.AddToClassList("dossier-fact");cell.Add(Text(fact.Label,"fact-label"));cell.Add(Text(fact.Value,"fact-value"));facts.Add(cell);
                }
                if(section.ArmorRows!=null && section.ArmorRows.Count>0)
                {
                    var rows=section.ArmorRows.Select(a=>new[]{a.Location,a.ActualInches.ToString("0.##"),a.EffectiveInches.ToString("0.##")}).ToList();
                    var table=new MultiColumnListView();ConfigureTable(table,rows,new[]{L("Location"),L("ActualIn"),L("EffIn")},new[]{250f,105f,105f});table.AddToClassList("armor-table");content.Add(table);
                }
                if(section.HistoryChart!=null)content.Add(new NavalHistoryChart(section.HistoryChart));
                void AddRow(VisualElement host,DossierRowSnapshot row)
                {
                    host.Add(Text(row.Heading,"section-title"));
                    if(row.IsMarkdown)AddHistoricalText(host,row.Summary+"\n"+row.Detail);
                    else host.Add(Text(row.Summary+"\n"+row.Detail));
                    if(row.Reference!=null)ActionButton(host,row.Reference.Name,()=>OpenDossier(row.Reference));
                }
                if(section.UseVirtualizedRows && section.Rows?.Count>0)
                {
                    var rows=section.Rows.Select(r=>new[]{r.Heading,r.Summary+" "+r.Detail}).ToList();
                    var list=new MultiColumnListView();ConfigureTable(list,rows,new[]{L("Time"),L("CombatLog")},new[]{150f,650f});
                    list.AddToClassList("dossier-log");content.Add(list);
                }
                else foreach(var row in section.Rows??Array.Empty<DossierRowSnapshot>())AddRow(content,row);
                if(!string.IsNullOrWhiteSpace(section.Note))content.Add(Text(section.Note));
            }
            if(snapshot.WeaponDiagram!=null)content.Add(new NavalWeaponDiagram(snapshot.WeaponDiagram));
        }
        foreach(var tab in snapshot.Tabs)Show(tab);
        PushDialog(snapshot.Title,body,snapshot.Dispose,"large");
    }
    public void OpenTorpedoAnalysis(ShipLog ship)
    {
        var snapshot=TorpedoAnalysisSnapshot.Create(Session,ship);
        string T(string key,params object[] args)=>U("Ui.TorpedoAnalysis."+key,args);
        string Reasons(TorpedoFireBlock blocks)=>blocks==0?T("Ready"):string.Join(" · ",Enum.GetValues(typeof(TorpedoFireBlock)).Cast<TorpedoFireBlock>().Where(b=>b!=0&&blocks.HasFlag(b)).Select(b=>T(b.ToString())));
        var body=new VisualElement();body.AddToClassList("grow");body.Add(Text(T("Summary",snapshot.Time.ToString("yyyy-MM-dd HH:mm:ss 'UTC'"),snapshot.Remaining,snapshot.SalvoShots,snapshot.WaitSeconds)));
        var columns=new VisualElement();columns.AddToClassList("columns");body.Add(columns);
        var list=new ScrollView();list.AddToClassList("analysis-targets");columns.Add(list);
        var right=new VisualElement();right.AddToClassList("analysis-details");columns.Add(right);
        var options=new DropdownField();right.Add(options);var details=Text("");right.Add(details);
        var diagram=new NavalTorpedoDiagram();right.Add(diagram);
        var toolbar=new VisualElement();toolbar.AddToClassList("toolbar");right.Add(toolbar);
        ActionButton(toolbar,"−",()=>diagram.Zoom(.8f));ActionButton(toolbar,"+",()=>diagram.Zoom(1.25f));ActionButton(toolbar,T("Fit"),diagram.ResetView);
        TorpedoTargetSnapshot selected=null;
        void ChooseOption()
        {
            if(selected==null||options.index<0||options.index>=selected.Options.Count)return;
            var option=selected.Options[options.index];var e=option.Evaluation;
            details.text=T("Factors",e.TimeFactor,e.RangeFactor,e.Score,e.Threshold)+"\n"+Reasons(e.Blocks)+(e.HasSolution?"\n"+T("Geometry",e.CurrentDistanceYards,e.InterceptionYards,e.ArrivalSeconds):"");
            diagram.SetSelection(snapshot.Heading,selected,option);
        }
        void Choose(TorpedoTargetSnapshot target)
        {
            selected=target;options.choices=target.Options.Select(o=>T("Option",o.MountNumber,o.Loaded,o.Evaluation.SpeedKnots,o.Evaluation.RangeYards)).ToList();
            if(target.Options.Count>0)options.SetValueWithoutNotify(options.choices[Math.Max(0,target.Options.ToList().IndexOf(target.Best))]);ChooseOption();
        }
        options.RegisterValueChangedCallback(_=>ChooseOption());
        foreach(var target in snapshot.Targets)
        {
            var e=target.Best?.Evaluation;
            ActionButton(list,target.Name+(target.Assigned?" · "+T("Assigned"):"")+"\n"+(e==null?T("NoWeapon"):T("Row",e.CurrentDistanceYards,e.SpeedKnots,e.RangeYards,e.Score,e.Threshold)+"\n"+Reasons(e.Blocks)),()=>Choose(target));
        }
        if(snapshot.Targets.Count>0)Choose(snapshot.Targets[0]);else list.Add(Text(T("NoTargets")));
        body.Add(Text(T("Notice")));PushDialog(T("Title")+" — "+snapshot.ShooterName,body,size:"large");
    }
    void OpenSettingsDialog()
    {
        var body=new ScrollView();body.AddToClassList("grow");
        var languageSource=Q<DropdownField>("language");var languageSetting=new DropdownField("Language",languageSource.choices,languageSource.index){name="settingsLanguage"};body.Add(languageSetting);
        var follow=new Toggle(Message("Camera follows selection","選択艦を追尾","相机跟随选中舰艇","相機跟隨選中艦艇")){value=World.Follow};body.Add(follow);
        var scale=new Slider(Message("Ship scale","艦船倍率","舰船显示比例","艦船顯示比例"),1,12){value=Q<Slider>("scale").value};body.Add(scale);
        var coast=new DropdownField(L("MapDisplay"),Q<DropdownField>("coastline").choices,Q<DropdownField>("coastline").index);body.Add(coast);
        var music=new Toggle(Message("Music","音楽","音乐","音樂")){value=audioPlayer.MusicEnabled};music.style.display=DisplayStyle.None;body.Add(music);
        var sound=new Toggle(Message("Sound effects","効果音","音效","音效")){value=audioPlayer.EffectsEnabled};body.Add(sound);
        var musicVolume=new Slider(Message("Music volume","音楽音量","音乐音量","音樂音量"),0,1){value=audioPlayer.MusicVolume};musicVolume.style.display=DisplayStyle.None;body.Add(musicVolume);
        var soundVolume=new Slider(Message("Effects volume","効果音量","音效音量","音效音量"),0,1){value=audioPlayer.EffectsVolume};body.Add(soundVolume);
        var noPenetrationDamage=new Slider(U("Ui.No Penetration Damage Coef"),0,1){name="settingsNoPenetrationDamage",showInputField=true,value=(float)CoreParameter.Instance.noPenetrationDamageCoef};body.Add(noPenetrationDamage);
        var projectiles=new DropdownField(L("GunneryProjectileVisuals"),new(){NavalText.Instance.Get("None"),Message("Projectiles","砲弾","弹丸","彈丸"),Message("Projectiles and trails","砲弾と曳跡","弹丸与轨迹","彈丸與軌跡")},(int)World.ProjectileMode);body.Add(projectiles);
        var style=new DropdownField(U("Ui.Scene.Style"),new(){U("Ui.Scene.Color"),U("Ui.Scene.Wireframe"),U("Ui.Scene.Sketch"),U("Ui.Scene.LineArt")},World.SceneStyle);body.Add(style);
        var prefix=new DropdownField(U("Ui.MapShipName.Prefix"),new(){U("Ui.MapShipName.None"),U("Ui.MapShipName.Classic"),U("Ui.MapShipName.Extended")},(int)shipNamePrefix);body.Add(prefix);
        var trim=new Toggle(U("Ui.MapShipName.Trim")){value=trimShipSuffix};body.Add(trim);
        var labels=new DropdownField(Message("Ship labels","艦名表示","舰名显示模式","艦名顯示模式"),new(){Message("Hide overlaps","重複を非表示","隐藏重叠","隱藏重疊"),Message("Always show","常に表示","总是显示","總是顯示"),Message("Local avoidance","近距離回避","简单避让","簡單避讓"),Message("Full avoidance","全域回避","完全避让","完全避讓")},shipLabelMode);body.Add(labels);
        var hideSmall=new Toggle(Message("Hide labels below pixel threshold","小さい艦の名前を非表示","按舰长像素阈值隐藏舰名","按艦長像素閾值隱藏艦名")){value=hideSmallShipLabels};body.Add(hideSmall);
        var minPixels=new FloatField(Message("Minimum ship length (px)","最小艦長 (px)","最小舰长（像素）","最小艦長（像素）")){value=shipLabelMinPixels};body.Add(minPixels);minPixels.SetEnabled(hideSmall.value);hideSmall.RegisterValueChangedCallback(e=>minPixels.SetEnabled(e.newValue));
        var page=PushDialog(L("Settings"),body,size:"medium");ActionButton(page.Actions,L("Apply"),()=>
        {
            Q<Toggle>("follow").value=follow.value;Q<Slider>("scale").value=scale.value;
            Q<DropdownField>("coastline").value=coast.value;Q<Toggle>("music").value=music.value;Q<Toggle>("sound").value=sound.value;
            audioPlayer.MusicVolume=musicVolume.value;audioPlayer.EffectsVolume=soundVolume.value;
            if(float.IsFinite(noPenetrationDamage.value))CoreParameter.Instance.noPenetrationDamageCoef=Mathf.Clamp01(noPenetrationDamage.value);
            World.ProjectileMode=(NavalCombat.Avalonia.Services.GunneryVisualMode)projectiles.index;World.SceneStyle=style.index;
            shipLabelMode=labels.index;hideSmallShipLabels=hideSmall.value;shipLabelMinPixels=float.IsFinite(minPixels.value)?Mathf.Max(0,minPixels.value):1.7f;
            shipNamePrefix=(ShipNamePrefixMode)prefix.index;trimShipSuffix=trim.value;SaveDisplaySettings();CloseDialog();languageSource.index=languageSetting.index;LocalizeHud();
        });
    }
}
}
