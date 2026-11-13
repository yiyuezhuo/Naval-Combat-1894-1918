using System.Text;
using CoreUtils;
using NavalCombat.Application;
using NavalCombatCore;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace NavalCombat.Unity
{

[DefaultExecutionOrder(100)]
[RequireComponent(typeof(UIDocument))]
public sealed partial class NavalGame : MonoBehaviour
{
    public BattleSession Session { get; private set; }
    public NavalWorld World { get; private set; }
    public bool Ready { get; private set; }
    public string LastError { get; private set; }
    VisualElement root;
    UnityScenarioSource source;
    readonly CancellationTokenSource lifetime = new();
    List<ScenarioCatalogEntry> catalog = new(), filtered = new();
    List<ShipLog> targets = new();
    ScenarioCatalogEntry chosen;
    IShipGroupMember selectedPost, selection;
    int previewRevision;
    CancellationTokenSource previewCancellation;
    bool busy, modal, refreshing, dragging;
    float nextRefresh;
    float noticeUntil;
    string notice;
    SimulationSpeed modalSpeed;
    BattlePhase lastPhase;
    NavalCoastline coastline;
    NavalAudio audioPlayer;
    readonly Dictionary<ShipLog,Label> mapLabels = new();
    public string CurrentScreen { get; private set; } = "archive";
    T Q<T>(string name) where T:VisualElement => root.Q<T>(name);
    static string L(string key, params object[] args) => NavalText.Instance.Text("Ui.Shell."+key,args);
    void Click(string name, Action action) => Q<Button>(name).clicked += () => Execute(action);
    void Execute(Action action) { try { action(); } catch(Exception e) when(e is ArgumentException or InvalidOperationException) { OpenCommandError(e.Message); } catch(Exception e) { Error(e); } }
    void Error(Exception e) { LastError=e.ToString();Q<Label>("status").text=e.Message;Debug.LogException(e); }

    async void Start()
    {
        var textSettings=ScriptableObject.CreateInstance<PanelTextSettings>();
        var embeddedFont=Resources.Load<Font>("Naval/Fonts/NotoSansSC-Regular");
        textSettings.defaultFontAsset=embeddedFont!=null
            ? UnityEngine.TextCore.Text.FontAsset.CreateFontAsset(embeddedFont)
            : UnityEngine.TextCore.Text.FontAsset.CreateFontAsset("Microsoft JhengHei","Regular",32);
        if(textSettings.defaultFontAsset==null)textSettings.defaultFontAsset=UnityEngine.TextCore.Text.FontAsset.CreateFontAsset("Arial","Regular",32);
        textSettings.fallbackFontAssets=new();
        var japanese=embeddedFont==null?UnityEngine.TextCore.Text.FontAsset.CreateFontAsset("Yu Gothic","Regular",32):null;
        if(japanese!=null)textSettings.fallbackFontAssets.Add(japanese);
        GetComponent<UIDocument>().panelSettings.textSettings=textSettings;
        root=GetComponent<UIDocument>().rootVisualElement;
        NavalHostServices.InitializeLanguage();
        NavalHostServices.Configure();
        source=new UnityScenarioSource(Path.Combine(UnityEngine.Application.streamingAssetsPath,"NavalCombat"));
        World=gameObject.AddComponent<NavalWorld>();
        var coastObject=new GameObject("Coastlines");coastObject.transform.SetParent(World.EnvironmentRoot,false);coastline=coastObject.AddComponent<NavalCoastline>();
        audioPlayer=gameObject.AddComponent<NavalAudio>();
        var coast=Q<DropdownField>("coastline");coast.choices=new(){"None","50m","10m"};coast.SetValueWithoutNotify("None");
        coast.RegisterValueChangedCallback(e=> {if(e.target==coast && e.newValue==coast.value && Session?.GameState!=null)coastline.Load(World,e.newValue);});
        Q<Toggle>("music").style.display=DisplayStyle.None;
        Q<Toggle>("sound").RegisterValueChangedCallback(e=>audioPlayer.EffectsEnabled=e.newValue);
        Click("dossier",()=> {if(selection is ShipLog ship)OpenDossier(new(NavalCombat.Avalonia.ViewModels.DossierKind.ShipState,ship.objectId,""));});
        var language=Q<DropdownField>("language");language.choices=new(){"English","日本語","简体中文","繁體中文"};
        language.SetValueWithoutNotify(language.choices[Array.IndexOf(NavalText.Cultures,NavalText.Instance.Culture)]);
        language.RegisterValueChangedCallback(e=> {NavalText.Instance.SetLanguage(NavalText.Cultures[language.index]);Localize();RefreshSelection(true);BuildUnits();if(chosen!=null)Preview(chosen);});
        var list=Q<ListView>("scenarios");list.fixedItemHeight=28;
        list.makeItem=()=> {var label=new Label();label.AddToClassList("scenario-row");return label;};
        list.bindItem=(e,i)=>{((Label)e).text=CatalogLabel(filtered[i]);e.tooltip=CatalogLabel(filtered[i]);};
        list.selectionChanged += items=> {var entry=items.OfType<ScenarioCatalogEntry>().FirstOrDefault();if(entry!=null)Preview(entry);};
        list.itemsChosen += _=>LoadChosen();
        Q<TextField>("search").RegisterValueChangedCallback(_=>Filter());
        Click("load",LoadChosen);Click("back",ReturnToArchive);
        Click("archiveSettings",OpenSettingsDialog);
        Click("fleet",()=>StartParticipation(BattleParticipationMode.FleetCommand));
        Click("role",()=>StartParticipation(BattleParticipationMode.RolePlaying));
        Click("observer",()=>StartParticipation(BattleParticipationMode.Observer));
        Click("pause",()=>Session?.TogglePause());
        foreach(var speed in new[]{1,5,20,60,120}) {var value=speed;Click("speed"+speed,()=>Session?.SetSimulationSpeed((SimulationSpeed)value));}
        Click("chart",()=>World.SetMode("Chart"));Click("threeD",()=>World.SetMode("3D"));
        Click("bridge",()=> {if(selection is ShipLog)World.SetMode("Bridge");});
        Click("fit",()=>World.Fit());
        Q<Toggle>("follow").RegisterValueChangedCallback(e=>World.Follow=e.newValue);
        Q<Slider>("scale").RegisterValueChangedCallback(e=>World.DisplayScale=e.newValue);
        Click("menu",()=>{if(CurrentScreen=="battle")OpenPause();else OpenSettingsDialog();});
        Click("report",OpenReport);
        Click("applyOrder",()=> {var ship=CommandShip();var heading=Q<FloatField>("heading").value;var speed=Q<FloatField>("speed").value;if(!float.IsFinite(heading)||!float.IsFinite(speed)||speed<0)throw new ArgumentException("Enter a finite course and a non-negative speed.");Session.IssueIndependentOrder(ship.objectId,heading,speed);});
        foreach(var pair in new[]{("maneuver",DoctrineDomain.Maneuver),("fireDoctrine",DoctrineDomain.Fire),("searchlight",DoctrineDomain.Searchlight)})
        {
            var domain=pair.Item2;var field=Q<DropdownField>(pair.Item1);field.choices=new(){"Inherited","Manual","Automatic"};
            field.RegisterValueChangedCallback(e=>
            {
                // Choice relabeling can queue ChangeEvents until after Localize returns.
                if(refreshing || e.newValue!=field.value || field.index<0 || !field.enabledInHierarchy
                    || Session?.Phase!=BattlePhase.Running || selection==null || !Session.IsPlayerMember(selection))return;
                Execute(()=>SetDoctrine(domain,(DoctrineSetting)field.index));
            });
        }
        Click("attack",()=>Session.SetAttackConstraint(CommandShip().objectId,Target().objectId));
        Click("cancelAttack",()=>Session.CancelAttackConstraint(CommandShip().objectId));
        Click("followTarget",()=>Session.SetFollowTarget(CommandShip().objectId,Target().objectId));
        Click("detach",()=>Session.DetachShip(CommandShip().objectId));
        Click("reverse",()=>Session.ReverseControlChain(CommandShip().objectId));
        Click("lineAhead",()=>OpenFormation(CommandShip(),"FollowFormation"));
        Click("lineAbreast",()=>OpenFormation(CommandShip(),"RelativeFormation"));
        SetupMapInput();
        root.RegisterCallback<PointerDownEvent>(e=>{if(contextMenu!=null && e.target is VisualElement target && !contextMenu.Contains(target))CloseContext();},TrickleDown.TrickleDown);
        SetupPanels();Localize();ShowScreen("archive");
        try {catalog=(await source.ScanScenariosAsync(lifetime.Token)).ToList();Filter();Ready=true;Q<Label>("status").text=L("ValueScenariosFound",catalog.Count);}
        catch(Exception e){if(this!=null)Error(e);}
    }
    void Localize()
    {
        var wasRefreshing=refreshing;
        refreshing=true;
        try
        {
        root.Query<VisualElement>().ForEach(e=> {if(e.tooltip?.StartsWith("Ui.")==true){var text=NavalText.Instance.Text(e.tooltip);if(e is TextElement t)t.text=text;else if(e is Toggle toggle)toggle.label=text;}});
        Q<Label>("archiveVersion").text=UnityEngine.Application.version;
        int lang=Array.IndexOf(NavalText.Cultures,NavalText.Instance.Culture);
        string T(string en,string ja,string cn,string tw)=>new[]{en,ja,cn,tw}[lang];
        Q<Button>("menu").text=T("Menu","メニュー","菜单","選單");LocalizeHud();LocalizeCatalog();
        Q<Button>("reverse").text=T("Reverse chain","隊列反転","反转队列","反轉隊列");
        Q<TextField>("search").label=T("Search","検索","搜索","搜尋");
        Q<Slider>("headingSlider").label=T("Course °","針路 °","航向 °","航向 °");Q<FloatField>("heading").label="";
        Q<Slider>("speedSlider").label=T("Speed kn","速力 kn","航速 kn","航速 kn");Q<FloatField>("speed").label="";
        Q<FloatField>("spacing").label=T("Spacing yd","間隔 yd","间距 yd","間距 yd");
        Q<Slider>("scale").label=T("Scale","縮尺","比例","比例");
        Q<DropdownField>("coastline").label=T("Coast","海岸","海岸","海岸");
        Q<Toggle>("music").label=T("Music","音楽","音乐","音樂");
        Q<Toggle>("sound").label=T("Sound","効果音","音效","音效");
        Q<DropdownField>("target").label=T("Target","目標","目标","目標");
        Q<DropdownField>("maneuver").label=T("Maneuver","操艦","操舰","操艦");
        Q<DropdownField>("fireDoctrine").label=T("Fire","射撃","射击","射擊");
        Q<DropdownField>("searchlight").label=T("Searchlight","探照灯","探照灯","探照燈");
        foreach(var name in new[]{"maneuver","fireDoctrine","searchlight"})
        {
            var field=Q<DropdownField>(name);var index=Math.Max(0,field.index);
            var choices=new List<string>{T("Inherited","継承","继承","繼承"),T("Manual","手動","手动","手動"),T("Automatic","自動","自动","自動")};
            field.SetValueWithoutNotify(choices[index]);
            field.choices=choices;
        }
        Q<Tab>("dynamicTab").label=L("DynamicControl");Q<Tab>("oobTab").label=L("PreBattleOob");
        Q<Label>("viewHint").text=NavalText.Instance.Culture.StartsWith("zh")?"左键选择 · 右键舰只菜单／空海面下达航向 · 中键拖动 · Shift+中键旋转 · 滚轮缩放 · 空格暂停":"Select: left click · Right click: ship menu / sea course · Pan: middle drag · Orbit: Shift+middle drag · Zoom: wheel · Pause: Space";
        }
        finally {refreshing=wasRefreshing;}
    }
    void Filter()
    {
        var query=Q<TextField>("search").value??"";
        filtered=catalog.Where(s=>MatchesFilters(s) && (s.GetDisplayName(NavalText.Instance.Culture).IndexOf(query,StringComparison.OrdinalIgnoreCase)>=0 || s.DisplayName.IndexOf(query,StringComparison.OrdinalIgnoreCase)>=0)).ToList();
        Q<ListView>("scenarios").itemsSource=filtered;Q<ListView>("scenarios").Rebuild();
        if(filtered.Count>0){Q<ListView>("scenarios").SetSelectionWithoutNotify(new[]{0});Preview(filtered[0]);}
        else {chosen=null;previewCancellation?.Cancel();previewRevision++;Q<Button>("load").SetEnabled(false);}
    }
    async void Preview(ScenarioCatalogEntry entry)
    {
        previewCancellation?.Cancel();previewCancellation?.Dispose();previewCancellation=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        var previewToken=previewCancellation.Token;
        chosen=entry;int revision=++previewRevision;Q<Label>("scenarioTitle").text=CatalogLabel(entry);Q<Button>("load").SetEnabled(false);
        try
        {
            var p=await source.LoadPreviewAsync(entry.Id,previewToken);if(this==null || revision!=previewRevision)return;
            Q<MarkdownRenderer>("scenarioMarkdown").value=$"{p.StartTime:yyyy-MM-dd HH:mm zzz}\n{NavalText.Instance.Get("VisibilityDescription."+p.Visibility)} · {NavalText.Instance.Get("Sea State (Beaufort): {0}",p.SeaStateBeaufort)}\n{L("ValueShips",p.ShipCount)}\n\n"+string.Join("\n",p.Sides.Select(s=>$"{s.Name} · {s.ShipCount}"))+"\n\n"+p.Description+(p.CanStart?"":"\n"+p.UnavailableReason);
            Q<Button>("load").SetEnabled(p.CanStart&&!busy);
        }
        catch(Exception e){if(this!=null && revision==previewRevision && e is not OperationCanceledException)Error(e);}
    }
    void LoadChosen() { _ = LoadSelectedAsync(); }
    public Task OpenScenarioAsync(string scenarioId)
    {
        if(busy)throw new InvalidOperationException("A scenario is already loading.");
        chosen=catalog.FirstOrDefault(s=>s.Id==scenarioId) ?? throw new ArgumentException("Unknown scenario",nameof(scenarioId));
        return LoadSelectedAsync();
    }
    async Task LoadSelectedAsync()
    {
        if(chosen==null || busy)return;
        previewCancellation?.Cancel();previewRevision++;
        busy=true;Q<Button>("load").SetEnabled(false);Q<Label>("status").text="…";
        try
        {
            if(Session!=null){Session.SetSimulationSpeed(SimulationSpeed.Paused);Session.GunneryShotsResolved-=OnShots;}
            NavalHostServices.Configure();Session=new BattleSession(source);
            var loaded=await Session.LoadScenarioAsync(chosen.Id,lifetime.Token);if(this==null)return;
            if(!loaded)throw new InvalidOperationException(Session.ErrorMessage+"\n"+Session.ErrorDetails);
            Session.GunneryShotsResolved += OnShots;
            World.Attach(Session);selection=null;selectedPost=Session.RootGroups.FirstOrDefault();
            foreach(var label in mapLabels.Values)label.RemoveFromHierarchy();mapLabels.Clear();
            foreach(var ship in Session.GameState.shipLogs)
            {
                var label=new Label(ship.GetMemberName()){pickingMode=PickingMode.Ignore};label.AddToClassList("ship-label");
                Q<VisualElement>("viewport").Add(label);mapLabels.Add(ship,label);
            }
            coastline.Load(World,Q<DropdownField>("coastline").value);audioPlayer.Attach(Session);
            Q<Label>("briefTitle").text=chosen.GetDisplayName(NavalText.Instance.Culture);BuildPosts();ShowScreen("briefing");
        }
        catch(Exception e){if(this!=null && e is not OperationCanceledException)OpenFault(e.Message,e.ToString());}
        finally {busy=false;}
    }
    void OnShots(IReadOnlyList<GunneryShotResolvedEvent> shots,SimulationSpeed speed)=>World.Gunfire.Enqueue(shots,speed);
    void BuildPosts()=>BuildPostTree();
    public void StartParticipation(BattleParticipationMode mode)
    {
        if(mode==BattleParticipationMode.Observer)Session.StartObserverBattle();
        else if(mode==BattleParticipationMode.RolePlaying)Session.StartRolePlayingBattle(selectedPost.objectId);
        else Session.StartBattle(selectedPost.GetRootParent().objectId);
        var candidates=Session.GameState.shipLogs.Where(s=>Session.IsPlayerShip(s)).ToList();
        if(candidates.Count==0)candidates=Session.GameState.shipLogsOnMap.ToList();
        var initialShip=candidates.OrderByDescending(s=>s.GetEffectiveControlMode()==ControlMode.Independent)
            .ThenByDescending(s=>s.shipClass.displacementTons).FirstOrDefault();
        BuildUnits();ShowScreen("battle");Select(initialShip);
        if(!PlayerPrefs.HasKey(HelpSeenKey))OpenHelp(true);
        World.Fit();lastPhase=Session.Phase;
    }
    void BuildUnits()=>BuildNativeTrees();
    public void Select(IShipGroupMember member)
    {
        selection=member;Session.SelectedObjectId=member?.objectId;
        World.SelectedId=member is ShipLog?member.objectId:(member as ShipGroup)?.Walk<ShipLog>().FirstOrDefault()?.objectId;
        RefreshSelection(true);
        SyncTreeSelection(member);
    }
    ShipLog CommandShip()
    {
        if(Session?.Phase!=BattlePhase.Running || selection==null || !Session.IsPlayerMember(selection))throw new InvalidOperationException(L("EnemyShipInformationOnly"));
        var ship=selection as ShipLog;
        return ship ?? throw new InvalidOperationException(L("SelectAShipOrGroup"));
    }
    static string Message(string en,string ja,string cn,string tw)=>NavalText.Instance.Culture switch {"ja-JP"=>ja,"zh-CN"=>cn,"zh-TW"=>tw,_=>en};
    void NotifyUser(string message){notice=message;noticeUntil=Time.unscaledTime+5;Q<Label>("status").text=message;}
    public bool TryIssueMapCourse(double latitude,double longitude)
    {
        string reason=null;
        if(Session?.Phase!=BattlePhase.Running)
            reason=Message("No battle is running.","戦闘は進行していません。","当前没有正在进行的战斗。","目前沒有正在進行的戰鬥。");
        else if(Session.ParticipationMode==BattleParticipationMode.Observer)
            reason=Message("Observer mode is read-only.","観戦モードでは命令できません。","观察者模式只能查看，不能下令。","觀察者模式只能查看，不能下令。");
        else if(selection==null)reason=L("SelectAShipOrGroup");
        else if(!Session.IsPlayerMember(selection))
            reason=Session.IsPlayerSideMember(selection)?Message("This friendly unit is outside your command.","この友軍は指揮下にありません。","该友军不在你的指挥权限内。","該友軍不在你的指揮權限內。"):L("EnemyShipInformationOnly");
        var ship=selection as ShipLog;
        if(reason==null && (ship==null || ship.mapState!=MapState.Deployed || ship.operationalState!=ShipOperationalState.Operational || ship.IsLandBattery()))
            reason=Message("Select an operational ship at sea to order a course.","航行可能な艦を選択してください。","请选择在图上可航行的舰船下达航向。","請選擇在圖上可航行的艦船下達航向。");
        if(reason!=null){NotifyUser(reason);return false;}
        Session.IssueIndependentCourseOrder(ship.objectId,latitude,longitude);
        return true;
    }
    ShipLog Target()=>Q<DropdownField>("target").index>=0 && Q<DropdownField>("target").index<targets.Count?targets[Q<DropdownField>("target").index]:throw new InvalidOperationException(L("UnknownTarget"));
    void SetDoctrine(DoctrineDomain domain,DoctrineSetting setting)
    {
        if(Session?.Phase!=BattlePhase.Running||selection==null||!Session.IsPlayerMember(selection))return;
        if(selection is ShipGroup group){Session.SetGroupDoctrine(group.objectId,domain,setting);return;}
        var d=domain switch {DoctrineDomain.Maneuver=>selection.doctrine.maneuverAutomaticType,DoctrineDomain.Fire=>selection.doctrine.fireAutomaticType,_=>selection.doctrine.searchlightAutomaticType};
        d.isInherited=setting==DoctrineSetting.Inherited;d.value=setting==DoctrineSetting.Automatic?AutomaticType.Automatic:AutomaticType.Manual;
    }
    void RefreshSelection(bool rebuild)
    {
        if(root==null || selection==null)return;
        refreshing=true;
        Q<Label>("selection").text=selection.GetMemberName();
        bool can=Session.Phase==BattlePhase.Running && Session.IsPlayerMember(selection);
        Q<VisualElement>("orders").SetEnabled(true);
        foreach(var field in new[]{"maneuver","fireDoctrine","searchlight"})Q<DropdownField>(field).SetEnabled(can);Q<Button>("bridge").SetEnabled(selection is ShipLog s && s.IsOnMap());
        if(selection is ShipLog ship)
        {
            Q<Label>("shipStats").text=NavalText.Instance.GetEnum(ship.shipClass.type)+$" · {ship.shipClass.displacementTons:N0} t";
            Q<Label>("logs").text=string.Join("\n",ship.logs.TakeLast(20).Reverse().Select(l=>l.Summary()));
            if(rebuild){Q<FloatField>("heading").SetValueWithoutNotify((float)ship.desiredHeadingDeg);Q<FloatField>("speed").SetValueWithoutNotify((float)ship.desiredSpeedKnots);BuildWeapons(ship,can);}
        }
        else
        {
            var ships=((ShipGroup)selection).Walk<ShipLog>().ToList();
            Q<Label>("shipStats").text=L("ValueShips",ships.Count)+"\n"+string.Join("\n",ships.Select(s=>$"{s.GetMemberName()} · {s.speedKnots:0.0} kn · {s.mapState}"));
            Q<Label>("logs").text="";if(rebuild)Q<VisualElement>("weapons").Clear();
            if(rebuild && ships.Count>0){Q<FloatField>("heading").SetValueWithoutNotify((float)ships[0].desiredHeadingDeg);Q<FloatField>("speed").SetValueWithoutNotify((float)ships[0].desiredSpeedKnots);}
        }
        if(rebuild)
        {
            var field=Q<DropdownField>("target");var previousTarget=field.index>=0&&field.index<targets.Count?targets[field.index].objectId:null;
            targets=Session.GameState.shipLogsOnMap.Where(s=>s!=selection).ToList();field.choices=targets.Select(s=>s.GetMemberName()).ToList();
            if(targets.Count>0)field.SetValueWithoutNotify(field.choices[Math.Max(0,targets.FindIndex(s=>s.objectId==previousTarget))]);
            SetField("maneuver",selection.doctrine.maneuverAutomaticType);SetField("fireDoctrine",selection.doctrine.fireAutomaticType);SetField("searchlight",selection.doctrine.searchlightAutomaticType);
        }
        refreshing=false;
        void SetField(string name,Inheriable<AutomaticType> value)=>Q<DropdownField>(name).SetValueWithoutNotify(Q<DropdownField>(name).choices[value.isInherited?0:value.value==AutomaticType.Manual?1:2]);
    }
    void BuildWeapons(ShipLog ship,bool can)
    {
        var panel=Q<VisualElement>("weapons");panel.Clear();
        foreach(var battery in ship.batteryStatus)
        {
            var record=battery.GetBatteryRecord();var label=new Label($"{record?.name?.GetMergedName()} · {battery.ammunition.Summary()}");label.AddToClassList("prose");panel.Add(label);
            foreach(var mount in battery.mountStatus)
            {
                var row=new VisualElement();panel.Add(row);
                var name=new Label($"{mount.barrels} × {record?.shellSizeInch:0.##} in · {mount.status}");row.Add(name);
                var ammo=new EnumField(mount.ammunitionType);row.Add(ammo);ammo.SetEnabled(can);
                ammo.RegisterValueChangedCallback(e=>Execute(()=> {CommandShip();mount.ammunitionType=(AmmunitionType)e.newValue;}));
                var fire=new Button(()=>Execute(()=> {CommandShip();var target=Target();Session.SetAttackConstraint(ship.objectId,target.objectId);ship.doctrine.fireAutomaticType.isInherited=false;ship.doctrine.fireAutomaticType.value=AutomaticType.Manual;mount.SetFiringTarget(target);})){text=L("CurrentTarget")};fire.SetEnabled(can);row.Add(fire);
                var details=new Button(()=>Execute(()=> {PushDialog(L("FireControl"),Text(mount.DescribeFireControlDetail()));})){text=L("FireControl")};row.Add(details);
            }
        }
        foreach(var tube in ship.torpedoSectorStatus.mountStatus)
        {
            var b=new Button(()=>Execute(()=> {CommandShip();var target=Target();Session.SetAttackConstraint(ship.objectId,target.objectId);tube.SetFiringTarget(target);})){text=$"{L("Torpedoes")} · {tube.currentLoad}/{tube.barrels} · {tube.status}"};b.SetEnabled(can);panel.Add(b);
        }
    }
    void ShowScreen(string name)
    {
        audioPlayer.SetPresentationState(name=="battle",modal,World);
        CancelMapGestures();
        CurrentScreen=name;foreach(var screen in new[]{"archive","briefing","battle"})Q<VisualElement>(screen).EnableInClassList("hidden",screen!=name);
        World.ViewCamera.enabled=true;World.ViewCamera.cullingMask=name=="battle"?~(1<<NavalWorld.PreviewLayer):0;
        root.EnableInClassList("archive-screen",name=="archive");
        root.EnableInClassList("briefing-screen",name=="briefing");
        root.EnableInClassList("battle-hud",name=="battle");if(name!="battle")World.HideShipPreview();
        World.enabled=name=="battle";root.schedule.Execute(()=> {if(CurrentScreen=="battle")World.Fit();});
    }
    void OpenModal(bool report){if(report)OpenReport();else OpenPause();}
    void ShowReport()=>OpenReport();
    string ReportText()
    {
        var text=new StringBuilder(Session.EndReason??Session.ScenarioTitle);
        if(Session.Victory!=null)foreach(var side in Session.Victory.sideVictoryStatuses)
        {
            text.AppendLine().AppendLine($"{side.name} · {NavalText.Instance.GetEnum(side.victoryLevel)} · {L("VictoryPointsFormat",side.victoryPoint.ToString("0.0"))} · {L("LossPointsFormat",side.lossVictoryPoint.ToString("0.0"))} · {side.selfLossRatio*100:0.##}%");
            foreach(var loss in side.shipTypeLossItems.Where(i=>i.sunk+i.heavy+i.medium>0))text.AppendLine(L("ValueValueSunkValueHeavilyDamaged",NavalText.Instance.GetEnum(loss.shipType),loss.sunk,loss.heavy));
        }
        foreach(var group in Session.RootGroups)
        {
            text.AppendLine().AppendLine().AppendLine(group.GetMemberName());
            foreach(var ship in group.Walk<ShipLog>())text.AppendLine($"{ship.GetMemberName()} · {ship.mapState} · {ship.damagePoint:0.0}/{ship.shipClass.damagePoint:0.0}");
        }
        return text.ToString();
    }
    void CloseModal()=>CloseDialog();
    public void ReturnToArchive()
    {
        if(busy)return;
        if(Session!=null){Session.SetSimulationSpeed(SimulationSpeed.Paused);Session.GunneryShotsResolved-=OnShots;Session.ReturnToScenarioSelection();}
        ClearDialogs();selection=null;World.Gunfire.Reset();ShowScreen("archive");_ = RefreshCatalogAsync();
    }
    void Update()
    {
        if(root==null)return;
        audioPlayer.SetPresentationState(CurrentScreen=="battle",modal,World);
        UpdateMapTouch();
        var keyboard=Keyboard.current;
        if(keyboard?.escapeKey.wasPressedThisFrame==true){if(contextMenu!=null)CloseContext();else if(modal)CloseModal();else if(CurrentScreen=="battle")OpenModal(false);}
        if(busy || Session?.GameState==null || CurrentScreen!="battle")return;
        var focused=root.focusController?.focusedElement as VisualElement;
        bool editing=focused!=null && (focused is TextField || focused.GetFirstAncestorOfType<TextField>()!=null || focused.GetFirstAncestorOfType<FloatField>()!=null);
        if(!modal && contextMenu==null && !editing && keyboard?.spaceKey.wasPressedThisFrame==true)Session.TogglePause();
        if(!modal)Session.AdvanceWallClock(Time.unscaledDeltaTime);
        if(Session.Phase!=lastPhase){lastPhase=Session.Phase;if(Session.Phase==BattlePhase.Ended)ShowReport();if(Session.Phase==BattlePhase.Faulted)OpenFault(Session.ErrorMessage,Session.ErrorDetails);}
        if(Time.unscaledTime<nextRefresh)return;nextRefresh=Time.unscaledTime+.25f;
        Q<Label>("clock").text=$"{Session.ToScenarioTime(Session.GameState.scenarioState.dateTime):yyyy-MM-dd HH:mm:ss} ({Session.TimeZoneOffset.TotalHours:+0.##;-0.##;0})";
        Q<Button>("pause").EnableInClassList("speed-selected",(int)Session.Speed==0);
        foreach(var speed in new[]{1,5,20,60,120})Q<Button>("speed"+speed).EnableInClassList("speed-selected",(int)Session.Speed==speed);
        Q<Label>("status").text=Time.unscaledTime<noticeUntil?notice:$"{Session.ScenarioTitle} · {Session.ParticipationMode} · {Session.Phase}";
        RefreshSelection(false);

        var wasRefreshing=refreshing;refreshing=true;try{RefreshPanels();}finally{refreshing=wasRefreshing;}
    }
    void LateUpdate()
    {
        if(root?.panel!=null && Session!=null && CurrentScreen=="battle"){UpdateMapLabels();UpdateHudPreview();}
    }
    readonly List<VisualElement> labelLeaders=new();
    void UpdateMapLabels()
    {
        var viewport=Q<VisualElement>("viewport");
        var occupied=new List<Rect>();
        var panels=new List<Rect>();
        foreach(var panel in new VisualElement[]{fleetPanel,hudRight,hudLog,touchActions,touchToggle})
            if(panel!=null && panel.resolvedStyle.display!=DisplayStyle.None)
            {
                var bounds=panel.worldBound;
                panels.Add(new Rect(viewport.WorldToLocal(bounds.position),bounds.size));
            }
        int usedLeaders=0;
        var area=new Rect(4,25,Mathf.Max(0,viewport.layout.width-8),Mathf.Max(0,viewport.layout.height-29));
        foreach(var pair in mapLabels.OrderByDescending(p=>p.Key.objectId==World.SelectedId))
        {
            var ship=pair.Key;var label=pair.Value;
            label.text=NavalCombat.Avalonia.Resources.NavalMapShipNames.Format(ship,shipNamePrefix,trimShipSuffix);
            var p=World.ViewCamera.WorldToScreenPoint(World.Project(ship.position.LatDeg,ship.position.LonDeg));
            var panel=RuntimePanelUtils.ScreenToPanel(root.panel,new Vector2(p.x,Screen.height-p.y));
            var local=viewport.WorldToLocal(panel);
            bool visible=(World.ViewMode!="Bridge"||World.BridgeLabels) && ship.IsOnMap() && p.z>0 && area.Contains(local)
                && (!hideSmallShipLabels || World.ShipLengthPixels(ship)>=shipLabelMinPixels);
            var measured=label.MeasureTextSize(label.text,0,VisualElement.MeasureMode.Undefined,0,VisualElement.MeasureMode.Undefined);
            float width=Mathf.Min(150,Mathf.Max(30,measured.x+8)),height=Mathf.Max(20,measured.y+4);
            var bounds=new Rect(local.x+10,local.y+8,width,height);
            bool Fits(Rect candidate)=>candidate.xMin>=area.xMin && candidate.xMax<=area.xMax && candidate.yMin>=area.yMin && candidate.yMax<=area.yMax
                && !occupied.Any(r=>r.Overlaps(candidate)) && !panels.Any(r=>r.Overlaps(candidate));
            if(visible && shipLabelMode==0)visible=Fits(bounds);
            if(visible && shipLabelMode>=2 && !Fits(bounds))
            {
                bool found=false;
                for(int ring=1;ring<=2&&!found;ring++)
                    for(int i=0;i<8&&!found;i++)
                    {
                        float angle=i*Mathf.PI/4;
                        var candidate=new Rect(local.x+Mathf.Cos(angle)*(width*.5f+12*ring)-width*.5f,
                            local.y+Mathf.Sin(angle)*(height*.5f+14*ring)-height*.5f,width,height);
                        if(Fits(candidate)){bounds=candidate;found=true;}
                    }
                if(!found && shipLabelMode==3)
                {
                    float nearest=float.PositiveInfinity;
                    for(float y=area.yMin;y+height<=area.yMax;y+=height+3)
                        for(float x=area.xMin;x+width<=area.xMax;x+=16)
                        {
                            var candidate=new Rect(x,y,width,height);
                            float score=(candidate.center-local).sqrMagnitude;
                            if(score<nearest && Fits(candidate)){nearest=score;bounds=candidate;found=true;}
                        }
                }
                visible=found;
            }
            if(visible && panels.Any(r=>r.Overlaps(bounds)))visible=false;
            if(visible)
            {
                occupied.Add(new Rect(bounds.x-2,bounds.y-1,bounds.width+4,bounds.height+2));
                if(shipLabelMode==3)
                {
                    if(usedLeaders==labelLeaders.Count)
                    {
                        var line=new VisualElement{pickingMode=PickingMode.Ignore};line.AddToClassList("ship-label-leader");
                        viewport.Insert(0,line);labelLeaders.Add(line);
                    }
                    var leader=labelLeaders[usedLeaders++];leader.style.display=DisplayStyle.Flex;
                    var end=new Vector2(Mathf.Clamp(local.x,bounds.xMin,bounds.xMax),Mathf.Clamp(local.y,bounds.yMin,bounds.yMax));
                    var delta=end-local;
                    leader.style.left=local.x;leader.style.top=local.y;leader.style.width=delta.magnitude;
                    leader.style.rotate=new Rotate(Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg);
                }
            }
            label.EnableInClassList("hidden",!visible);
            label.EnableInClassList("friendly",World.IsBlueSide(ship));
            label.transform.position=new Vector3(bounds.x,bounds.y,0);
        }
        for(int i=usedLeaders;i<labelLeaders.Count;i++)labelLeaders[i].style.display=DisplayStyle.None;
    }
    void OnDestroy(){previewCancellation?.Cancel();previewCancellation?.Dispose();World?.HideShipPreview();if(vesselPlan!=null)Destroy(vesselPlan);while(pages.Count>0)pages.Pop().Dispose?.Invoke();lifetime.Cancel();lifetime.Dispose();if(Session!=null){Session.SetSimulationSpeed(SimulationSpeed.Paused);Session.GunneryShotsResolved-=OnShots;}}
}

}
