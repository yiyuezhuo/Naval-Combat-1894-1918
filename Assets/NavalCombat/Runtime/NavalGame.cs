using System.Text;
using CoreUtils;
using NavalCombat.Application;
using NavalCombat.Avalonia.Resources;
using NavalCombatCore;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using FontAsset = UnityEngine.TextCore.Text.FontAsset;

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
    PanelSettings panelSettings;
    PanelTextSettings textSettings, previousTextSettings;
    readonly Dictionary<string, FontAsset> fontAssets = new();
    UnityScenarioSource source;
    readonly CancellationTokenSource lifetime = new();
    List<ScenarioCatalogEntry> catalog = new(), filtered = new();
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

    FontAsset GetFontAsset(string name, bool systemFont = false)
    {
        if (fontAssets.TryGetValue(name, out var fontAsset))
            return fontAsset;

        if (systemFont)
            fontAsset = FontAsset.CreateFontAsset(name, "Regular", 32);
        else if (Resources.Load<Font>("Naval/Fonts/" + name) is Font sourceFont)
            fontAsset = FontAsset.CreateFontAsset(sourceFont);

        fontAssets.Add(name, fontAsset);
        return fontAsset;
    }

    void ApplyLanguageFont()
    {
        var english = GetFontAsset("NotoSans-Regular");
        var japanese = GetFontAsset("NotoSansJP-Regular");
        var simplifiedChinese = GetFontAsset("NotoSansSC-Regular");
        var traditionalChinese = GetFontAsset("NotoSansTC-Regular");
        var primary = NavalText.Instance.Culture switch
        {
            "ja-JP" => japanese,
            "zh-CN" => simplifiedChinese,
            "zh-TW" => traditionalChinese,
            _ => english
        };
        primary = primary ?? simplifiedChinese ?? GetFontAsset("Microsoft JhengHei", systemFont: true)
            ?? GetFontAsset("Arial", systemFont: true);

        var fallbacks = new List<FontAsset>();
        foreach (var font in new[] { english, japanese, simplifiedChinese, traditionalChinese })
            if (font != null && font != primary)
                fallbacks.Add(font);
        if (simplifiedChinese == null && japanese == null && GetFontAsset("Yu Gothic", systemFont: true) is FontAsset systemJapanese)
            fallbacks.Add(systemJapanese);

        textSettings.fallbackFontAssets = fallbacks;
        textSettings.defaultFontAsset = primary;
        root.style.unityFontDefinition = primary != null ? FontDefinition.FromSDFFont(primary) : StyleKeyword.Null;
    }

    async void Start()
    {
        NavalHostServices.InitializeLanguage();
        var document = GetComponent<UIDocument>();
        root = document.rootVisualElement;
        panelSettings = document.panelSettings;
        previousTextSettings = panelSettings.textSettings;
        textSettings = ScriptableObject.CreateInstance<PanelTextSettings>();
        ApplyLanguageFont();
        panelSettings.textSettings = textSettings;
        NavalHostServices.Configure();
        source=new UnityScenarioSource(Path.Combine(UnityEngine.Application.streamingAssetsPath,"NavalCombat"));
        World=gameObject.AddComponent<NavalWorld>();
        var coastObject=new GameObject("Coastlines");coastObject.transform.SetParent(World.EnvironmentRoot,false);coastline=coastObject.AddComponent<NavalCoastline>();
        audioPlayer=gameObject.AddComponent<NavalAudio>();
        var coast=Q<DropdownField>("coastline");coast.choices=new(){"None","50m","10m"};coast.SetValueWithoutNotify("None");
        coast.RegisterValueChangedCallback(e=> {if(e.target==coast && e.newValue==coast.value && Session?.GameState!=null)coastline.Load(World,e.newValue);});
        Q<Toggle>("music").style.display=DisplayStyle.None;
        Q<Toggle>("sound").RegisterValueChangedCallback(e=>audioPlayer.EffectsEnabled=e.newValue);
        var language=Q<DropdownField>("language");language.choices=new(){"English","日本語","简体中文","繁體中文"};
        language.SetValueWithoutNotify(language.choices[Array.IndexOf(NavalText.Cultures,NavalText.Instance.Culture)]);
        language.RegisterValueChangedCallback(_ =>
        {
            NavalText.Instance.SetLanguage(NavalText.Cultures[language.index]);
            ApplyLanguageFont();
            Localize();
            RefreshSelection(true);
            if(CurrentScreen=="briefing")BuildPosts();
            if (chosen != null) Preview(chosen);
        });
        var list=Q<ListView>("scenarios");list.fixedItemHeight=28;
        list.makeItem=()=> {var label=new Label();label.AddToClassList("scenario-row");return label;};
        list.bindItem=(e,i)=>{((Label)e).text=CatalogLabel(filtered[i]);e.tooltip=CatalogLabel(filtered[i]);};
        list.selectionChanged += items=> {var entry=items.OfType<ScenarioCatalogEntry>().FirstOrDefault();if(entry!=null)Preview(entry);};
        list.itemsChosen += _=>LoadChosen();
        Q<TextField>("search").RegisterValueChangedCallback(_=>Filter());
        Click("load",LoadChosen);Click("back",ReturnToArchive);
        Click("archiveSettings",OpenSettingsDialog);
#if UNITY_EDITOR
        Click("quitGame",()=>UnityEditor.EditorApplication.isPlaying=false);
#elif UNITY_WEBGL
        Q<Button>("quitGame").AddToClassList("hidden");
#else
        Click("quitGame",()=>UnityEngine.Application.Quit());
#endif
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
        Q<Button>("quitGame").text=T("Exit Game","ゲームを終了","退出游戏","離開遊戲");
        Q<TextField>("search").label=T("Search","検索","搜索","搜尋");
        Q<Slider>("headingSlider").label=T("Course °","針路 °","航向 °","航向 °");Q<FloatField>("heading").label="";
        Q<Slider>("speedSlider").label=T("Speed kn","速力 kn","航速 kn","航速 kn");Q<FloatField>("speed").label="";
        Q<Slider>("scale").label=T("Scale","縮尺","比例","比例");
        Q<DropdownField>("coastline").label=T("Coast","海岸","海岸","海岸");
        Q<Toggle>("music").label=T("Music","音楽","音乐","音樂");
        Q<Toggle>("sound").label=T("Sound","効果音","音效","音效");
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
        ClearDialogs();
        previewCancellation?.Cancel();previewRevision++;
        busy=true;Q<Button>("load").SetEnabled(false);Q<Label>("status").text="…";
        try
        {
            if(Session!=null){Session.SetSimulationSpeed(SimulationSpeed.Paused);Session.GunneryShotsResolved-=OnShots;}
            NavalHostServices.Configure();Session=new BattleSession(source);
            var loaded=await Session.LoadScenarioAsync(chosen.Id,lifetime.Token);if(this==null)return;
            if(!loaded)throw new InvalidOperationException(Session.ErrorMessage+"\n"+Session.ErrorDetails);
            Session.GunneryShotsResolved += OnShots;
            Session.GameState.InitializeLeaderEmbarkation();
            IndexCommanders();
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
        Session.GameState.SetFogOfWar(fogOfWar);
        var initialShip=Session.GetInitialSelectedShip(selectedPost?.objectId);
        ShowScreen("battle");Select(initialShip);
        if(!PlayerPrefs.HasKey(HelpSeenKey))OpenHelp(true);
        World.Fit();lastPhase=Session.Phase;
    }
    public void Select(IShipGroupMember member)
    {
        if((member is ShipLog ship && !Session.CanDisplay(ship)) || (member is ShipGroup && !Session.CanInspect(member)))
            member=null;
        selection=member;Session.SelectedObjectId=member?.objectId;
        World.SelectedRangeShip=member as ShipLog;
        World.SelectedId=member is ShipLog?member.objectId:(member as ShipGroup)?.Walk<ShipLog>().FirstOrDefault()?.objectId;
        hudRight.style.display=member==null?DisplayStyle.None:DisplayStyle.Flex;
        RefreshSelection(true);
        RefreshContactPresentation();
        SyncTreeSelection(member);
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
    void RefreshSelection(bool rebuild)
    {
        if(root==null)return;
        Q<Button>("bridge").SetEnabled(selection is ShipLog s && s.IsOnMap() && !IsSelectedContact);
        if(selection==null)return;
        refreshing=true;
        if(selection is ShipLog ship)
        {
            var shipClass=ship.shipClass;
            var classLabel=L("ValueClass",NavalDisplayNames.Global(shipClass.name));
            Q<Label>("shipStats").text=$"{NavalMapShipNames.FormatShipType(shipClass,shipNamePrefix)} · {shipClass.displacementTons:N0} t · {classLabel}";
            if(rebuild && !IsSelectedContact){Q<FloatField>("heading").SetValueWithoutNotify((float)ship.desiredHeadingDeg);Q<FloatField>("speed").SetValueWithoutNotify((float)ship.desiredSpeedKnots);}
        }
        else
        {
            var ships=((ShipGroup)selection).Walk<ShipLog>().ToList();
            Q<Label>("shipStats").text=L("ValueShips",ships.Count)+"\n"+string.Join("\n",ships.Select(s=>$"{s.GetMemberName()} · {s.speedKnots:0.0} kn · {s.mapState}"));
            if(rebuild && ships.Count>0){Q<FloatField>("heading").SetValueWithoutNotify((float)ships[0].desiredHeadingDeg);Q<FloatField>("speed").SetValueWithoutNotify((float)ships[0].desiredSpeedKnots);}
        }
        refreshing=false;
    }
    void ShowScreen(string name)
    {
        battleClockTooltip.AddToClassList("hidden");
        if(name=="battle")RefreshBattleClock();
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
        if(keyboard?.escapeKey.wasPressedThisFrame==true)
        {
            if(contextMenu!=null)CloseContext();
            else if(modal)CloseModal();
            else if(CurrentScreen=="battle")
            {
                if(selection!=null)Select(null);
                else OpenModal(false);
            }
        }
        if(busy || Session?.GameState==null || CurrentScreen!="battle")return;
        var focused=root.focusController?.focusedElement as VisualElement;
        bool editing=focused!=null && (focused is TextField || focused.GetFirstAncestorOfType<TextField>()!=null || focused.GetFirstAncestorOfType<FloatField>()!=null);
        if(!modal && contextMenu==null && !editing && keyboard?.spaceKey.wasPressedThisFrame==true)Session.TogglePause();
        var beforeAdvance=Session.GameState.scenarioState.dateTime;
        if(!modal)Session.AdvanceWallClock(Time.unscaledDeltaTime);
        if(Session.HasPlayerFog)
        {
            if(IsSelectedContact && (!Session.CanDisplay(SelectedShip) ||
                Session.GameState.ContactSince(Session.PlayerRootGroupId,SelectedShip)>beforeAdvance))Select(null);
            if(contextMenu?.userData is ShipLog hit && !Session.CanDisplay(hit))CloseContext();
        }
        if(Session.Phase!=lastPhase){lastPhase=Session.Phase;if(Session.Phase==BattlePhase.Ended)ShowReport();if(Session.Phase==BattlePhase.Faulted)OpenFault(Session.ErrorMessage,Session.ErrorDetails);}
        if(Time.unscaledTime<nextRefresh)return;nextRefresh=Time.unscaledTime+.25f;
        RefreshBattleClock();
        Q<Button>("pause").EnableInClassList("speed-selected",(int)Session.Speed==0);
        foreach(var speed in new[]{1,5,20,60,120})Q<Button>("speed"+speed).EnableInClassList("speed-selected",(int)Session.Speed==speed);
        Q<Label>("status").text=Time.unscaledTime<noticeUntil?notice:$"{Session.ScenarioTitle} · {Session.ParticipationMode} · {Session.Phase}";
        RefreshSelection(false);
        if(IsSelectedContact)RefreshContactPresentation();

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
        foreach(var panel in new VisualElement[]{hudRight,hudLog,leaderPreview,touchActions,touchToggle})
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
            if(!Session.CanDisplay(ship)){label.AddToClassList("hidden");continue;}
            label.text=NavalMapShipNames.Format(ship,shipNamePrefix,trimShipSuffix);
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
    void OnDestroy()
    {
        previewCancellation?.Cancel();
        previewCancellation?.Dispose();
        World?.HideShipPreview();
        DisposeLeaderPreview();
        if (vesselPlan != null)
            Destroy(vesselPlan);
        while (pages.Count > 0)
            pages.Pop().Dispose?.Invoke();
        lifetime.Cancel();
        lifetime.Dispose();
        if (Session != null)
        {
            Session.SetSimulationSpeed(SimulationSpeed.Paused);
            Session.GunneryShotsResolved -= OnShots;
        }

        if (panelSettings != null && panelSettings.textSettings == textSettings)
            panelSettings.textSettings = previousTextSettings;
        if (textSettings != null)
            Destroy(textSettings);
        foreach (var fontAsset in fontAssets.Values)
            if (fontAsset != null)
                Destroy(fontAsset);
    }
}

}
