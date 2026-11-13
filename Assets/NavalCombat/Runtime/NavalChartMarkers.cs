using NavalCombatCore;
using NavalCombat.Avalonia.Controls;
using UnityEngine;

namespace NavalCombat.Unity
{
public enum ChartRepresentation { Model, Symbol, Dot }

public sealed partial class NavalWorld
{
    public const float ChartModelThresholdPixels=72;
    public const float ChartDotThresholdPixels=2;
    readonly Dictionary<ShipLog,ChartRepresentation> chartRepresentations=new();
    readonly Dictionary<(bool dot,bool friendly),Mesh> markerMeshes=new();
    Material markerMaterial;
    float ChartMetersPerPixel=>2*ViewCamera.orthographicSize/Mathf.Max(1,ViewCamera.pixelHeight);
    public bool IsBlueSide(ShipLog ship)=>Session.IsObserverMode
        ? Session.GetSideId(ship)==Session.RootGroups.FirstOrDefault()?.objectId
        : Session.IsPlayerSideMember(ship);
    public float ShipLengthPixels(ShipLog ship)
    {
        float length=NavalShipMesh.Dimensions(ship.shipClass).Length*ShipScale(ship);
        if(ViewCamera.orthographic)return length/ChartMetersPerPixel;
        float depth=ViewCamera.WorldToScreenPoint(Project(ship.position.LatDeg,ship.position.LonDeg)).z;
        return depth>0?length*ViewCamera.pixelHeight/(2*depth*Mathf.Tan(ViewCamera.fieldOfView*Mathf.Deg2Rad*.5f)):0;
    }
    float ChartSelectionRadius(ShipLog ship)=>GetChartRepresentation(ship)==ChartRepresentation.Model
        ? Mathf.Max(NavalShipMesh.Dimensions(ship.shipClass).Length*DisplayScale*.65f,ChartMetersPerPixel*18)
        : ChartMetersPerPixel*(GetChartRepresentation(ship)==ChartRepresentation.Dot?9:18);
    public ChartRepresentation GetChartRepresentation(ShipLog ship)
        =>ViewMode!="Chart"?ChartRepresentation.Model:chartRepresentations.GetValueOrDefault(ship,ChartRepresentation.Model);
    ChartRepresentation ResolveRepresentation(ShipLog ship)
    {
        if(ViewMode!="Chart")return ChartRepresentation.Model;
        float pixels=NavalShipMesh.Dimensions(ship.shipClass).Length*DisplayScale/ChartMetersPerPixel;
        var next=pixels>=ChartModelThresholdPixels?ChartRepresentation.Model:pixels<=ChartDotThresholdPixels?ChartRepresentation.Dot:ChartRepresentation.Symbol;
        if(chartRepresentations.TryGetValue(ship,out var previous))
        {
            if(previous==ChartRepresentation.Model && pixels>=64)next=ChartRepresentation.Model;
            if(previous==ChartRepresentation.Symbol && pixels>1.7f && pixels<80)next=ChartRepresentation.Symbol;
            if(previous==ChartRepresentation.Dot && pixels<=2.4f)next=ChartRepresentation.Dot;
        }
        chartRepresentations[ship]=next;return next;
    }
    bool MarkerHit(ShipLog ship,Vector2 point,Vector3 center)
    {
        var delta=point-new Vector2(center.x,center.y);
        if(GetChartRepresentation(ship)==ChartRepresentation.Dot)return delta.sqrMagnitude<=8*8;
        var world=Project(ship.position.LatDeg,ship.position.LonDeg);
        var forwardPoint=ViewCamera.WorldToScreenPoint(world+Quaternion.Euler(0,(float)ship.headingDeg,0)*Vector3.forward*100);
        var forward=new Vector2(forwardPoint.x-center.x,forwardPoint.y-center.y).normalized;
        return Mathf.Abs(Vector2.Dot(delta,forward))<=15 && Mathf.Abs(Vector2.Dot(delta,new(-forward.y,forward.x)))<=7;
    }
    bool RenderChartMarker(ShipLog ship,Vector3 position)
    {
        var representation=ResolveRepresentation(ship);if(representation==ChartRepresentation.Model)return false;
        bool dot=representation==ChartRepresentation.Dot,friendly=IsBlueSide(ship);
        if(markerMaterial==null){markerMaterial=new Material(Resources.Load<Shader>("Naval/ShipVertexColor"));markerMaterial.SetFloat("_Style",4);}
        if(!markerMeshes.TryGetValue((dot,friendly),out var mesh))markerMeshes[(dot,friendly)]=mesh=MakeMarkerMesh(dot,friendly);
        var view=GetShipView(ship);
        if(view.Marker==null)
        {
            var obj=new GameObject("Chart marker",typeof(MeshFilter),typeof(MeshRenderer));obj.transform.SetParent(view.Root,false);obj.layer=ChartOverlayLayer;
            obj.GetComponent<MeshRenderer>().sharedMaterial=markerMaterial;view.Marker=obj.GetComponent<MeshFilter>();
        }
        var go=view.Marker.gameObject;go.SetActive(true);view.Marker.sharedMesh=mesh;
        go.name=$"{ship.GetMemberName()} — {representation}";
        go.transform.SetPositionAndRotation(position+Vector3.up*5,Quaternion.Euler(0,(float)ship.headingDeg,0));go.transform.localScale=Vector3.one*ChartMetersPerPixel;
        return true;
    }
    static Mesh MakeMarkerMesh(bool dot,bool friendly)
    {
        var outline=dot?Enumerable.Range(0,20).Select(i=>new Vector2(Mathf.Sin(i*Mathf.PI/10)*3,Mathf.Cos(i*Mathf.PI/10)*3)).ToArray():new[]{new Vector2(0,12),new Vector2(4,4),new Vector2(4,-10),new Vector2(-4,-10),new Vector2(-4,4)};
        var vertices=new List<Vector3>();var colors=new List<Color>();var triangles=new List<int>();
        void Fill(float shrink,float height,Color color)
        {
            int start=vertices.Count;vertices.Add(new(0,height,0));colors.Add(color);
            foreach(var p in outline){vertices.Add(new(p.x*shrink,height,p.y*shrink));colors.Add(color);}
            for(int i=0;i<outline.Length;i++){triangles.Add(start);triangles.Add(start+1+i);triangles.Add(start+1+(i+1)%outline.Length);}
        }
        // Chart-ink colors in sRGB. Vertex colors reach the unlit shader directly,
        // so convert for a linear project instead of displaying washed-out ink.
        Color color=friendly?new Color32(0,59,115,255):new Color32(217,0,0,255);
        var border=new Color(color.r*.45f,color.g*.45f,color.b*.45f,1);
        if(QualitySettings.activeColorSpace==ColorSpace.Linear){color=color.linear;border=border.linear;}
        Fill(1,0,border);Fill(dot?.65f:.76f,.02f,color);
        var mesh=new Mesh{name=dot?"Fixed 6px chart point":"Fixed 22x8px chart symbol"};mesh.SetVertices(vertices);mesh.SetColors(colors);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
    }
    void DisposeChartMarkers(){foreach(var mesh in markerMeshes.Values)Destroy(mesh);Destroy(markerMaterial);}
}
}
