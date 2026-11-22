using NavalCombat.Application;
using NavalCombat.Avalonia.Controls;
using NavalCombat.Avalonia.Services;
using NavalCombatCore;
using UnityEngine;
using UnityEngine.Rendering;
using NVector3 = System.Numerics.Vector3;

namespace NavalCombat.Unity
{

public sealed partial class NavalWorld : MonoBehaviour
{
    public Camera ViewCamera { get; private set; }
    public string ViewMode { get; private set; } = "Chart";
    public float DisplayScale = 1;
    public bool Follow;
    public bool BridgeLock,BridgeLabels=true;
    public float BridgeHeight;
    public string BridgeTargetId;
    public string BridgeObserverId;
    public int SceneStyle;
    public GunneryVisualMode ProjectileMode=GunneryVisualMode.ProjectilesWithTrails;
    public string SelectedId;
    public readonly GunneryVisualTracker Gunfire = new();
    public BattleSession Session;
    double originLat, originLon;
    Vector3 focus;
    float distance = 10000, yaw, pitch = 52, bridgeYaw, bridgePitch;
    readonly Dictionary<float[], Mesh> meshes = new();
    Material shipMaterial, seaMaterial, lineMaterial;
    GameObject sea;
    readonly List<LineRenderer> effects = new();
    int usedEffects;

    void Awake()
    {
        SetupWorldHierarchy();
        ViewCamera = new GameObject("Naval camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
        ViewCamera.transform.SetParent(EnvironmentRoot,false);
        ViewCamera.clearFlags = CameraClearFlags.SolidColor;
        ViewCamera.backgroundColor = new Color(.69f, .75f, .77f);
        ViewCamera.nearClipPlane = .15f;
        ViewCamera.farClipPlane = 250000;
        shipMaterial = new Material(Resources.Load<Shader>("Naval/ShipVertexColor"));
        seaMaterial = new Material(Resources.Load<Shader>("Naval/Ocean"));
        lineMaterial = new Material(Resources.Load<Shader>("Naval/EffectLine"));
        sea = GameObject.CreatePrimitive(PrimitiveType.Plane);
        sea.name = "Sea"; sea.transform.SetParent(EnvironmentRoot,false); sea.transform.localScale = Vector3.one * 40000;
        sea.GetComponent<Renderer>().sharedMaterial = seaMaterial;
        Destroy(sea.GetComponent<Collider>());
        SetupSky(ViewCamera);
    }
    public Vector3 Project(double lat, double lon) => new((float)(Math.IEEERemainder(lon-originLon,360)*111320*Math.Cos(originLat*Math.PI/180)), 0, (float)((lat-originLat)*111320));
    public LatLon Unproject(Vector3 p) => new(originLat + p.z/111320d, originLon+p.x/(111320d*Math.Cos(originLat*Math.PI/180)));
    public void Attach(BattleSession session)
    {
        chartRepresentations.Clear();
        ResetGunneryEffects();
        ResetWakes();
        ResetShipViews();
        ResetShipPreview();
        foreach(var mesh in meshes.Values)Destroy(mesh);meshes.Clear();
        Session = session; Gunfire.Reset();
        ResetTracks();
        ResetRangeOverlays();
        var ships=session.GameState.shipLogsOnMap.ToList();
        originLat=ships.Count>0?ships.Average(s=>s.position.LatDeg):0;
        originLon=ships.Count>0?ships[0].position.LonDeg:0;
        SelectedId=null; SetMode("Chart"); Fit();
    }
    public void Fit()
    {
        var points=Session?.GameState?.shipLogsOnMap.Where(Session.CanDisplay).Select(s=>Project(s.position.LatDeg,s.position.LonDeg)).ToList();
        if(points==null || points.Count==0)return;
        var bounds=new Bounds(points[0],Vector3.zero); foreach(var p in points)bounds.Encapsulate(p);
        focus=bounds.center; distance=Mathf.Clamp(Mathf.Max(bounds.size.x/ViewCamera.aspect,bounds.size.z)*1.3f+1500,500,150000);
    }
    public void OnVisibilityChanged()
    {
        Gunfire.Reset();ResetGunneryEffects();
        if(ViewMode=="Bridge" && Session.HasPlayerFog && !Session.IsPlayerSideMember(Session.GameState.shipLogs.FirstOrDefault(s=>s.objectId==BridgeObserverId)))SetMode("Chart");
        BridgeTargetId=null;
    }
    public void SetMode(string mode)
    {
        if(mode=="Bridge")
        {
            var ship=Session?.GameState?.shipLogs.FirstOrDefault(s=>s.objectId==SelectedId);
            if(Session?.HasPlayerFog==true && !Session.CanInspect(ship))return;
            BridgeObserverId=SelectedId;
        }
        ViewMode=mode;bridgeYaw=bridgePitch=0;
    }
    public void BridgeAhead(){bridgeYaw=bridgePitch=0;}
    public void TopDown(){SetMode("3D");pitch=89;yaw=0;}
    public void ResetView()
    {
        if(Session?.InitialMapView is {} view){focus=Project(view.CenterLatitudeDeg,view.CenterLongitudeDeg);distance=Mathf.Clamp((float)view.OrthographicSize*2,80,150000);yaw=0;pitch=52;}
        else Fit();
    }
    public void Pan(Vector2 delta)
    {
        if(ViewMode=="Bridge") {bridgeYaw+=delta.x*.2f; bridgePitch=Mathf.Clamp(bridgePitch-delta.y*.15f,-75,75);return;}
        var right=ViewCamera.transform.right; var forward=Vector3.ProjectOnPlane(ViewCamera.transform.up,Vector3.up).normalized;
        focus-= (right*delta.x + forward*delta.y)*distance/Math.Max(100,Screen.height); Follow=false;
    }
    public void Orbit(Vector2 delta) { yaw+=delta.x*.2f; pitch=Mathf.Clamp(pitch-delta.y*.2f,12,89); }
    public void Zoom(float delta)
    {
        if(ViewMode=="Bridge")ViewCamera.fieldOfView=Mathf.Clamp(ViewCamera.fieldOfView*Mathf.Exp(-delta*.12f),8,75);
        else distance=Mathf.Clamp(distance*Mathf.Exp(-delta*.12f),80,150000);
    }
    public ShipLog Pick(Vector2 screen,float minimumScreenRadius=0)
    {
        ShipLog best=null;float nearest=float.PositiveInfinity,nearestDepth=float.PositiveInfinity;
        foreach(var ship in Session?.GameState?.shipLogsOnMap ?? Enumerable.Empty<ShipLog>())
        {
            if(!Session.CanDisplay(ship))continue;
            var center=Project(ship.position.LatDeg,ship.position.LonDeg)+Vector3.up*3;
            var projected=ViewCamera.WorldToScreenPoint(center);
            if(projected.z<=ViewCamera.nearClipPlane)continue;
            float radius=SelectionRadius(ship);
            bool inside=false;
            var previous=ViewCamera.WorldToScreenPoint(center+Vector3.forward*radius);
            for(int i=1;i<=32;i++)
            {
                float angle=i*Mathf.PI/16;
                var current=ViewCamera.WorldToScreenPoint(center+new Vector3(Mathf.Sin(angle),0,Mathf.Cos(angle))*radius);
                if(current.z>0 && previous.z>0 && (current.y>screen.y)!=(previous.y>screen.y) &&
                    screen.x<(previous.x-current.x)*(screen.y-current.y)/(previous.y-current.y)+current.x)inside=!inside;
                previous=current;
            }
            float screenDistance=(screen-new Vector2(projected.x,projected.y)).sqrMagnitude;
            if(!inside&&(minimumScreenRadius<=0||screenDistance>minimumScreenRadius*minimumScreenRadius))continue;
            if(screenDistance<nearest || Mathf.Approximately(screenDistance,nearest)&&projected.z<nearestDepth)
            {best=ship;nearest=screenDistance;nearestDepth=projected.z;}
        }
        return best;
    }
    float SelectionRadius(ShipLog ship)=>ViewMode=="Chart"?ChartSelectionRadius(ship):Mathf.Max(80,distance*.012f);
    float ShipScale(ShipLog ship)=>ViewMode=="Bridge"?1:DisplayScale;
    public bool SeaPoint(Vector2 screen,out LatLon point)
    {
        var ray=ViewCamera.ScreenPointToRay(screen);var plane=new Plane(Vector3.up,Vector3.zero);
        if(plane.Raycast(ray,out var enter)){point=Unproject(ray.GetPoint(enter));return true;} point=null;return false;
    }
    Mesh GetMesh(float[] raw)
    {
        if(meshes.TryGetValue(raw,out var found))return found;
        int actualStride=MeijiPolitics.Rendering.SketchMesh.Stride;
        var count=raw.Length/actualStride; var vertices=new Vector3[count];var colors=new Color[count];var triangles=new int[count];var bary=new List<Vector3>();
        for(int i=0;i<count;i++)bary.Add(i%3==0?Vector3.right:i%3==1?Vector3.up:Vector3.forward);
        for(int i=0;i<count;i++) {int j=i*actualStride;vertices[i]=new(raw[j],raw[j+2],raw[j+1]);colors[i]=new(raw[j+3],raw[j+4],raw[j+5]);triangles[i]=i;}
        for(int i=0;i+2<count;i+=3)(triangles[i+1],triangles[i+2])=(triangles[i+2],triangles[i+1]);
        var mesh=new Mesh {name="Historical ship geometry",indexFormat=IndexFormat.UInt32};mesh.vertices=vertices;mesh.colors=colors;mesh.SetUVs(0,bary);mesh.triangles=triangles;mesh.RecalculateNormals();mesh.RecalculateBounds();
        meshes.Add(raw,mesh);return mesh;
    }
    void Line(Vector3 a,Vector3 b,Color color,float width,int layer=0)
    {
        if(usedEffects==effects.Count){var go=new GameObject($"Line slot {effects.Count:000}",typeof(LineRenderer));go.transform.SetParent(lineRoot,false);var l=go.GetComponent<LineRenderer>();l.sharedMaterial=lineMaterial;l.positionCount=2;effects.Add(l);}
        var line=effects[usedEffects++];line.gameObject.SetActive(true);line.gameObject.layer=layer;line.startColor=line.endColor=color;
        line.startWidth=line.endWidth=width;line.SetPosition(0,a);line.SetPosition(1,b);
    }
    void LateUpdate()
    {
        if(Session?.GameState==null)return;
        UpdateOcean();
        shipMaterial.SetFloat("_Style",SceneStyle);
        var selected=Session.GameState.shipLogs.FirstOrDefault(s=>s.objectId==(ViewMode=="Bridge"?BridgeObserverId:SelectedId));
        if(selected!=null && !Session.CanDisplay(selected))selected=null;
        if(Follow && selected!=null)focus=Project(selected.position.LatDeg,selected.position.LonDeg);
        if(ViewMode=="Bridge" && selected==null)ViewMode="Chart";
        ViewCamera.orthographic=ViewMode=="Chart";
        if(ViewMode=="Bridge")
        {
            var eye=NavalShipMesh.BridgeEye(selected.shipClass);
            var rotation=Quaternion.Euler(0,(float)selected.headingDeg,0);
            ViewCamera.transform.position=Project(selected.position.LatDeg,selected.position.LonDeg)+rotation*new Vector3(eye.X,eye.Z+BridgeHeight,eye.Y);
            ViewCamera.transform.rotation=Quaternion.Euler(bridgePitch,(float)selected.headingDeg+bridgeYaw,0);
            var target=Session.GameState.shipLogsOnMap.FirstOrDefault(s=>s.objectId==BridgeTargetId);
            if(target!=null && !Session.CanDisplay(target)){BridgeTargetId=null;target=null;}
            if(BridgeLock&&target!=null)ViewCamera.transform.LookAt(Project(target.position.LatDeg,target.position.LonDeg)+Vector3.up*8);
        }
        else
        {
            ViewCamera.fieldOfView=55; ViewCamera.orthographicSize=distance*.5f;
            ViewCamera.transform.rotation=Quaternion.Euler(ViewMode=="Chart"?90:pitch,yaw,0);
            ViewCamera.transform.position=focus-ViewCamera.transform.forward*distance;
        }
        UpdateShipViews();
        UpdateWakes();
        UpdateTracks();
        UpdateRangeOverlay(selected);
        usedEffects=0;
        var lineWidth=Mathf.Max(1,distance/1600);
        if(ViewMode=="Chart")
            foreach(var ship in Session.GameState.shipLogsOnMap)
                if(ship.GetEffectiveControlMode()==ControlMode.Independent && (Session.IsObserverMode || Session.IsPlayerSideMember(ship)))
                    DrawCourseOrder(ship,Project(ship.position.LatDeg,ship.position.LonDeg)+Vector3.up*3);
        if(selected!=null)
        {
            var p=Project(selected.position.LatDeg,selected.position.LonDeg)+Vector3.up*3;
            if(ViewMode=="Chart" && Session.CanInspect(selected))DrawSelectedFireLines(selected,p);
            if(ViewMode!="Bridge")
            {
                var radius=SelectionRadius(selected);
                for(int i=0;i<32;i++){float a=i*Mathf.PI/16,b=(i+1)*Mathf.PI/16;Line(p+new Vector3(Mathf.Sin(a),0,Mathf.Cos(a))*radius,p+new Vector3(Mathf.Sin(b),0,Mathf.Cos(b))*radius,new Color(.9f,.79f,.4f),lineWidth,ChartOverlayLayer);}
            }
            if(Session.CanInspect(selected))foreach(var waypoint in selected.manualRoute){var q=Project(waypoint.LatDeg,waypoint.LonDeg)+Vector3.up*3;Line(p,q,Color.white,lineWidth,ChartOverlayLayer);p=q;}
        }
        if(Session.Speed!=SimulationSpeed.Paused)Gunfire.Advance(Time.unscaledDeltaTime);
        UpdateGunneryEffects();
        foreach(var shot in Gunfire.ActiveShells.Take(ProjectileMode==GunneryVisualMode.None?0:300))
        {
            if(!Session.CanPresentShot(shot.Shot))continue;
            var a=Project(shot.Shot.ShooterLatitudeDeg,shot.Shot.ShooterLongitudeDeg)+Vector3.up*10;
            var b=Project(shot.Shot.TargetLatitudeDeg,shot.Shot.TargetLongitudeDeg)+Vector3.up*10;
            if(shotEffects.TryGetValue(shot,out var effect)){a=effect.Start;b=effect.End;}
            RenderShell(a,b,(float)shot.Progress);
        }
        foreach(var torpedo in Session.GameState.launchedTorpedosOnMap)
        {
            if(Session.HasPlayerFog && !Session.IsPlayerSideMember(torpedo.GetShooter()))continue;
            var p=Project(torpedo.position.LatDeg,torpedo.position.LonDeg)+Vector3.up*2;
            var direction=Quaternion.Euler(0,(float)torpedo.headingDeg,0)*Vector3.forward;
            Line(p,p-direction*Mathf.Max(30,distance*.004f),new Color(.6f,.9f,.9f),Mathf.Max(1,lineWidth*.7f));
        }
        for(int i=usedEffects;i<effects.Count;i++)effects[i].gameObject.SetActive(false);
    }
    static Vector3 ShellArc(Vector3 start,Vector3 end,float progress)
        =>Vector3.Lerp(start,end,progress)+Vector3.up*Mathf.Sin(progress*Mathf.PI)*Mathf.Min(200,(end-start).magnitude*.04f);

    void DrawCourseOrder(ShipLog ship,Vector3 origin)
    {
        float pixel=ChartMetersPerPixel;
        var direction=Quaternion.Euler(0,(float)ship.desiredHeadingDeg,0)*Vector3.forward;
        float length=ChartSelectionRadius(ship)*2;
        var tip=origin+direction*length;
        var courseColor=new Color(1,.78f,.16f);
        Line(origin,tip,courseColor,1.5f*pixel,ChartOverlayLayer);
        var side=Vector3.Cross(Vector3.up,direction);
        Line(tip,tip-direction*9*pixel+side*4*pixel,courseColor,1.5f*pixel,ChartOverlayLayer);
        Line(tip,tip-direction*9*pixel-side*4*pixel,courseColor,1.5f*pixel,ChartOverlayLayer);
    }

    void DrawSelectedFireLines(ShipLog ship,Vector3 origin)
    {
        float pixel=ChartMetersPerPixel;
        foreach(var target in ship.GetFiringToTargets())
        {
            if(!target.IsOnMap() || !Session.CanDisplay(target))continue;
            var end=Project(target.position.LatDeg,target.position.LonDeg)+Vector3.up*3;
            var delta=end-origin;float distance=delta.magnitude;
            var step=delta.normalized;
            float dash=Mathf.Max(8*pixel,distance/200);
            for(float offset=0;offset<distance;offset+=dash*1.6f)
                Line(origin+step*offset,origin+step*Mathf.Min(distance,offset+dash),new Color(1,.32f,.2f),pixel,ChartOverlayLayer);
        }
    }

    void RenderShell(Vector3 start,Vector3 end,float progress)
    {
        RenderShellForCamera(start,end,progress,ViewCamera,ChartOverlayLayer);
        if(previewCamera!=null && previewCamera.enabled)
            RenderShellForCamera(start,end,progress,previewCamera,PreviewLayer);
    }

    void RenderShellForCamera(Vector3 start,Vector3 end,float progress,Camera camera,int layer)
    {
        var head=ShellArc(start,end,progress);
        float depth=camera.WorldToViewportPoint(head).z;
        if(depth<=camera.nearClipPlane || depth>camera.farClipPlane)return;
        float metersPerPixel=(camera.orthographic?2*camera.orthographicSize:
            2*depth*Mathf.Tan(camera.fieldOfView*Mathf.Deg2Rad*.5f))/Mathf.Max(1,camera.pixelHeight);
        if(ProjectileMode==GunneryVisualMode.ProjectilesWithTrails && progress>0)
        {
            // Both ends must lie on the same arc. The old tail omitted altitude,
            // drawing a long rod down toward the sea instead of a flight trail.
            float step=Mathf.Min(progress,.001f);
            float tangentLength=Vector3.Distance(head,ShellArc(start,end,progress-step))/step;
            float tailStep=Mathf.Min(.1f,Mathf.Min(175,120*metersPerPixel)/Mathf.Max(.001f,tangentLength));
            var tail=ShellArc(start,end,Mathf.Max(0,progress-tailStep));
            Line(tail,head,new Color(1,.88f,.55f),metersPerPixel,layer);
            var trail=effects[usedEffects-1];
            trail.startColor=new Color(1,.88f,.55f,0);trail.endColor=new Color(1,.88f,.55f,.75f);
            trail.startWidth=0;trail.endWidth=metersPerPixel;
        }
        // A separate camera-facing bright point remains visible with trails disabled.
        var half=camera.transform.up*metersPerPixel;
        Line(head-half,head+half,new Color(1,1,.85f),2*metersPerPixel,layer);
    }
    void OnDestroy(){Destroy(skyMaterial);Destroy(particleMaterial);DisposeShipPreview();ResetWakes();ResetShipViews();Destroy(wakeMaterial);DisposeChartMarkers();foreach(var mesh in meshes.Values)Destroy(mesh);Destroy(shipMaterial);Destroy(seaMaterial);Destroy(lineMaterial);}
}

}
