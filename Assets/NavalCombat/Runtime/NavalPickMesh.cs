using System.Numerics;

namespace NavalCombat.Avalonia.Controls;

/// <summary>Object-local static picking surface, with a cheap bounding-box rejection.
/// Guns and their tessellated barrels are deliberately not a picking surface.</summary>
internal sealed class NavalPickMesh(float[] vertices,int stride)
{
    readonly Vector3 minimum=Bounds(vertices,stride,false), maximum=Bounds(vertices,stride,true);
    static Vector3 Bounds(float[] data,int stride,bool max)
    {
        var result=new Vector3(max?float.NegativeInfinity:float.PositiveInfinity);
        for(var i=0;i<data.Length;i+=stride)
        {var p=new Vector3(data[i],data[i+1],data[i+2]);result=max?Vector3.Max(result,p):Vector3.Min(result,p);}
        return result;
    }
    internal double? Hit(Vector3 origin,Vector3 ray,NavalSceneInstance instance)
    {
        Vector3 Inverse(Vector3 p)=>new(p.X*instance.Cos-p.Y*instance.Sin,p.X*instance.Sin+p.Y*instance.Cos,p.Z);
        var localOrigin=Inverse(origin-instance.Origin)/instance.Scale;
        // Keep ray parameter in world metres, including for exaggerated chart scale.
        var localRay=Inverse(ray)/instance.Scale;
        float near=0,far=float.PositiveInfinity;
        for(var axis=0;axis<3;axis++)
        {
            var o=(axis==0?localOrigin.X:axis==1?localOrigin.Y:localOrigin.Z);var d=(axis==0?localRay.X:axis==1?localRay.Y:localRay.Z);
            if(Math.Abs(d)<1e-10) {if(o<(axis==0?minimum.X:axis==1?minimum.Y:minimum.Z) || o>(axis==0?maximum.X:axis==1?maximum.Y:maximum.Z))return null;continue;}
            var a=((axis==0?minimum.X:axis==1?minimum.Y:minimum.Z)-o)/d;var b=((axis==0?maximum.X:axis==1?maximum.Y:maximum.Z)-o)/d;
            near=Math.Max(near,Math.Min(a,b));far=Math.Min(far,Math.Max(a,b));
            if(far<near)return null;
        }
        double? closest=null;
        for(var i=0;i+stride*2<vertices.Length;i+=stride*3)
        {
            Vector3 Read(int at)=>new(vertices[at],vertices[at+1],vertices[at+2]);
            if(IntersectTriangle(localOrigin,localRay,Read(i),Read(i+stride),Read(i+stride*2)) is {} distance
                && (closest==null || distance<closest.Value))closest=distance;
        }
        return closest;
    }
    static double? IntersectTriangle(Vector3 origin,Vector3 ray,Vector3 a,Vector3 b,Vector3 c)
    {
        var e=b-a;var f=c-a;var h=Vector3.Cross(ray,f);var det=Vector3.Dot(e,h);
        if(Math.Abs(det)<1e-7)return null;var s=origin-a;var u=Vector3.Dot(s,h)/det;if(u<0||u>1)return null;
        var q=Vector3.Cross(s,e);var v=Vector3.Dot(ray,q)/det;if(v<0||u+v>1)return null;
        var distance=Vector3.Dot(f,q)/det;return distance>=0?distance:null;
    }
}
