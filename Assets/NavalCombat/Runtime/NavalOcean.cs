using NavalCombat.Application;
using NavalCombat.Avalonia.Controls;
using NavalCombatCore;
using UnityEngine;
using UnityEngine.Rendering;

namespace NavalCombat.Unity
{
public sealed partial class NavalWorld
{
    public Transform EnvironmentRoot {get;private set;}
    Transform shipRenderRoot,lineRoot;
    float surfaceClock;
    Material wakeMaterial;
    const float WakeLifetime=90;
    readonly Dictionary<ShipLog,WakeTrail> wakes=new();
    readonly struct WakeSample
    {
        public readonly Vector3 Position,Forward;
        public readonly float Time,Strength;
        public WakeSample(Vector3 p,Vector3 f,float t,float strength){Position=p;Forward=f;Time=t;Strength=strength;}
    }
    sealed class WakeTrail
    {
        public GameObject Object;
        public Mesh Mesh;
        public readonly List<WakeSample> Samples=new();
        public readonly List<Vector3> Vertices=new();
        public readonly List<Vector2> UV=new();
        public readonly List<Color> Colors=new();
        public readonly List<int> Indices=new();
    }
    void SetupWorldHierarchy()
    {
        Transform Group(string name,Transform parent){var g=new GameObject(name);g.transform.SetParent(parent,false);return g.transform;}
        var world=Group("World (runtime)",transform);
        EnvironmentRoot=Group("Environment",world);
        shipRenderRoot=Group("Ships",world);
        var effectsRoot=Group("Effects",world);
        lineRoot=Group("Selection, routes and projectiles (pool)",effectsRoot);
    }
    void UpdateOcean()
    {
        if(Session.Phase==BattlePhase.Running)surfaceClock+=Time.unscaledDeltaTime*(int)Session.Speed;
        seaMaterial.SetFloat("_WaveTime",surfaceClock);
        seaMaterial.SetFloat("_SeaState",Session.GameState.scenarioState.seaStateBeaufort);
        if(wakeMaterial!=null)wakeMaterial.SetFloat("_WaveTime",surfaceClock);
    }
    void ResetWakes()
    {
        foreach(var trail in wakes.Values){Destroy(trail.Mesh);Destroy(trail.Object);}wakes.Clear();surfaceClock=0;
    }
    void UpdateWakes()
    {
        foreach(var trail in wakes.Values)trail.Object.SetActive(false);
        foreach(var ship in Session.GameState.shipLogsOnMap)
        {
            if(ship.IsLandBattery())continue;
            float speed=Mathf.Abs((float)ship.speedKnots),strength=Mathf.Clamp01(speed/12);
            if(!wakes.TryGetValue(ship,out var trail))
            {
                if(speed<.3f)continue;
                if(wakeMaterial==null)wakeMaterial=new Material(Resources.Load<Shader>("Naval/Wake"));
                var go=new GameObject($"{ship.GetMemberName()} — wake",typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(GetShipView(ship).Root,false);
                var mesh=new Mesh{name="Ship wake ribbon"};mesh.MarkDynamic();go.GetComponent<MeshFilter>().sharedMesh=mesh;
                var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=wakeMaterial;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
                wakes[ship]=trail=new WakeTrail{Object=go,Mesh=mesh};
                var p=Project(ship.position.LatDeg,ship.position.LonDeg);var f=Quaternion.Euler(0,(float)ship.headingDeg,0)*Vector3.forward*Mathf.Sign((float)ship.speedKnots);
                // A steady-motion visual wake at scenario entry, replaced by sampled motion as play advances.
                for(int i=0;i<18;i++)trail.Samples.Add(new(p-f*speed*.514444f*i*5,f,surfaceClock-i*5,strength));
            }
            var position=Project(ship.position.LatDeg,ship.position.LonDeg);
            var forward=Quaternion.Euler(0,(float)ship.headingDeg,0)*Vector3.forward*((float)ship.speedKnots<0?-1:1);
            if(speed>.3f&&(trail.Samples.Count==0||(position-trail.Samples[0].Position).sqrMagnitude>1))trail.Samples.Insert(0,new(position,forward,surfaceClock,strength));
            trail.Samples.RemoveAll(s=>surfaceClock-s.Time>WakeLifetime);
            if(trail.Samples.Count>128)trail.Samples.RemoveRange(128,trail.Samples.Count-128);
            if(trail.Samples.Count<2)continue;
            bool mainVisible=GetChartRepresentation(ship)!=ChartRepresentation.Dot;
            var projected=ViewCamera.WorldToViewportPoint(position);
            mainVisible &= projected.z>=0&&projected.x>=-.25f&&projected.x<=1.25f&&projected.y>=-.25f&&projected.y<=1.25f;
            if(!mainVisible&&!NearPreview(position))continue;
            trail.Object.layer=mainVisible?0:PreviewLayer;
            trail.Object.SetActive(true);BuildWakeMesh(trail,ship);
        }
    }
    void BuildWakeMesh(WakeTrail trail,ShipLog ship)
    {
        var (length,beam)=NavalShipMesh.Dimensions(ship.shipClass);float scale=ShipScale(ship),along=0;
        trail.Vertices.Clear();trail.UV.Clear();trail.Colors.Clear();trail.Indices.Clear();
        for(int i=0;i<trail.Samples.Count;i++)
        {
            var sample=trail.Samples[i];float age=Mathf.Max(0,surfaceClock-sample.Time);
            var center=sample.Position-sample.Forward*length*.46f*scale+Vector3.up*.35f;
            var tangent=i+1<trail.Samples.Count?(sample.Position-trail.Samples[i+1].Position).normalized:sample.Forward;
            if(tangent.sqrMagnitude<.1f)tangent=sample.Forward;
            var side=new Vector3(tangent.z,0,-tangent.x);
            float width=(beam*.45f+age*.4f)*scale;
            float fade=Mathf.Pow(1-Mathf.Clamp01(age/WakeLifetime),1.6f)*sample.Strength*.8f;
            if(i==trail.Samples.Count-1)fade=0;
            if(i>0)along+=Vector3.Distance(sample.Position,trail.Samples[i-1].Position);
            trail.Vertices.Add(center-side*width);trail.Vertices.Add(center+side*width);
            trail.UV.Add(new(0,along));trail.UV.Add(new(1,along));trail.Colors.Add(new(1,1,1,fade));trail.Colors.Add(new(1,1,1,fade));
            if(i>0){int a=(i-1)*2,b=i*2;trail.Indices.Add(a);trail.Indices.Add(b);trail.Indices.Add(a+1);trail.Indices.Add(a+1);trail.Indices.Add(b);trail.Indices.Add(b+1);}
        }
        trail.Mesh.Clear();trail.Mesh.SetVertices(trail.Vertices);trail.Mesh.SetUVs(0,trail.UV);trail.Mesh.SetColors(trail.Colors);trail.Mesh.SetTriangles(trail.Indices,0);trail.Mesh.RecalculateBounds();
    }
}
}
