using System.Numerics;
using MeijiPolitics.Rendering;
using NavalCombatCore;

namespace NavalCombat.Avalonia.Controls;

internal sealed class NavalMesh
{
    internal readonly List<float> Vertices = [];
    internal readonly List<float> PreparedVertices = [];
    internal static float[] Prepare(float[] vertices) => SketchMesh.Expand(vertices, vertices.Length / 9);
    internal void Instance(float[] local, Vector3 origin, Vector3 x, Vector3 y, Vector3 z, Vector3 color)
    {
        // Orthogonal basis with scale; inverse-transpose normals preserve crease and silhouette tests.
        Vector3 Normal(float a, float b, float c) =>
            x * (a / Math.Max(1e-12f, x.LengthSquared())) +
            y * (b / Math.Max(1e-12f, y.LengthSquared())) +
            z * (c / Math.Max(1e-12f, z.LengthSquared()));
        for (var i = 0; i < local.Length; i += SketchMesh.Stride)
        {
            var p = origin + x * local[i] + y * local[i+1] + z * local[i+2];
            PreparedVertices.AddRange([p.X,p.Y,p.Z,color.X,color.Y,color.Z]);
            for (var n = 6; n < SketchMesh.Stride; n += 3)
            {
                var normal = Normal(local[i+n],local[i+n+1],local[i+n+2]);
                if (normal.LengthSquared() > 1e-12f) normal = Vector3.Normalize(normal);
                PreparedVertices.AddRange([normal.X,normal.Y,normal.Z]);
            }
        }
    }
    internal void Triangle(Vector3 a, Vector3 b, Vector3 c, Vector3 color)
    {
        var cross = Vector3.Cross(b - a, c - a);
        var n = cross.LengthSquared() > 1e-12 ? Vector3.Normalize(cross) : Vector3.UnitZ;
        foreach (var p in new[] { a, b, c }) Vertices.AddRange([p.X, p.Y, p.Z, color.X, color.Y, color.Z, n.X, n.Y, n.Z]);
    }
    internal void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 color)
    { Triangle(a, b, c, color); Triangle(a, c, d, color); }
    internal void Box(Vector3 center, Vector3 size, Vector3 color)
    {
        var a = center - size / 2; var b = center + size / 2;
        Quad(new(a.X,a.Y,b.Z),new(b.X,a.Y,b.Z),b,new(a.X,b.Y,b.Z),color);
        Quad(a,new(b.X,a.Y,a.Z),new(b.X,a.Y,b.Z),new(a.X,a.Y,b.Z),color);
        Quad(new(b.X,a.Y,a.Z),new(b.X,b.Y,a.Z),b,new(b.X,a.Y,b.Z),color);
        Quad(new(b.X,b.Y,a.Z),new(a.X,b.Y,a.Z),new(a.X,b.Y,b.Z),b,color);
        Quad(new(a.X,b.Y,a.Z),a,new(a.X,a.Y,b.Z),new(a.X,b.Y,b.Z),color);
    }
    internal void Cylinder(Vector3 center, float radius, float height, Vector3 color, int sides = 10)
    {
        for (var i=0; i<sides; i++)
        {
            var a = center + new Vector3(MathF.Cos(i*(2 * MathF.PI)/sides)*radius,MathF.Sin(i*(2 * MathF.PI)/sides)*radius,0);
            var b = center + new Vector3(MathF.Cos((i+1)*(2 * MathF.PI)/sides)*radius,MathF.Sin((i+1)*(2 * MathF.PI)/sides)*radius,0);
            var z = new Vector3(0,0,height);
            Quad(a,b,b+z,a+z,color); Triangle(center+z,a+z,b+z,color);
        }
    }
}

/// <summary>Generic period silhouettes, not reconstructions of individual classes. Local bow is +Y.</summary>
internal static class NavalShipMesh
{
    // Standing on the generic open bridge; this is a model parameter, not a historical measurement.
    internal static Vector3 BridgeEye(ShipClass ship)
    {
        if(ShipLayoutMesh.CanGenerate(ship)) return ShipLayoutMesh.Eye(ship);
        var (length,beam)=Dimensions(ship);
        return new(beam*.12f,length*.18f,Math.Max(2,beam*.24f)+beam*.475f+1.7f);
    }
    internal static (float Length, float Beam) Dimensions(ShipClass ship)
    {
        var small = ship?.type is ShipType.Destroyer or ShipType.TorpedoBoat or ShipType.PatrolGunboat;
        var length = ship?.lengthFoot * .3048 ?? 0;
        var beam = ship?.beamFoot * .3048 ?? 0;
        return ((float)(double.IsFinite(length) && length > 0 ? length : small ? 60 : 110),
            (float)(double.IsFinite(beam) && beam > 0 ? beam : small ? 7 : 18));
    }
    internal static float[] Build(ShipClass ship, bool white, bool destroyed)
    {
        var m = new NavalMesh(); var (l,w) = Dimensions(ship);
        var hull = destroyed ? new Vector3(.25f,.27f,.27f) : white ? new(.72f,.74f,.71f) : new(.25f,.29f,.31f);
        var deck = new Vector3(.65f,.54f,.36f); var dark = new Vector3(.13f,.15f,.16f);
        if (ship?.type == ShipType.LandBattery)
        {
            m.Box(new(0,0,3),new(w,l,6),new(.5f,.48f,.42f));
            for(var i=-1;i<=1;i++) { m.Cylinder(new(0,i*l*.25f,6),w*.16f,3,hull); m.Box(new(0,i*l*.25f+w*.2f,9),new(w*.08f,w*.5f,1),dark); }
            return m.Vertices.ToArray();
        }
        var height = Math.Max(2, w*.24f);
        Vector2[] outline = [new(0,l*.5f),new(-w*.37f,l*.36f),new(-w*.5f,l*.12f),new(-w*.45f,-l*.4f),new(-w*.28f,-l*.5f),new(w*.28f,-l*.5f),new(w*.45f,-l*.4f),new(w*.5f,l*.12f),new(w*.37f,l*.36f)];
        for(var i=0;i<outline.Length;i++)
        {
            var a=new Vector3(outline[i],height); var b=new Vector3(outline[(i+1)%outline.Length],height);
            m.Triangle(new(0,0,height),a,b,deck);
            m.Quad(new(a.X*.85f,a.Y*.98f,0),new(b.X*.85f,b.Y*.98f,0),b,a,hull);
        }
        if (destroyed) return m.Vertices.ToArray();
        var small = ship?.type is ShipType.Destroyer or ShipType.TorpedoBoat or ShipType.PatrolGunboat;
        var merchant = ship?.type is ShipType.Transport or ShipType.Repair or ShipType.ArmedMerchantCruiser;
        var capital = ship?.type is ShipType.Battleship or ShipType.Battlecruiser;
        m.Box(new(0,l*.03f,height+w*.12f),new(w*.55f,l*.4f,w*.24f),hull);
        m.Box(new(0,l*.18f,height+w*.35f),new(w*.7f,l*.075f,w*.25f),hull);
        var funnels = merchant ? 1 : capital ? 2 : 3;
        for(var i=0;i<funnels;i++)
        {
            var y=l*(.05f-i*.1f);
            m.Cylinder(new(0,y,height+w*.24f),w*.12f,w*.48f,new(.58f,.45f,.25f));
            m.Cylinder(new(0,y,height+w*.72f),w*.125f,w*.09f,dark);
        }
        foreach(var y in new[]{l*.26f,-l*.26f})
        {
            m.Cylinder(new(0,y,height),Math.Max(.25f,w*.022f),w*(small?1.1f:1.6f),dark,6);
            m.Box(new(0,y,height+w),new(w*.7f,w*.025f,w*.025f),dark);
        }
        if (!merchant)
            foreach(var y in new[]{l*.34f,-l*.35f})
            {
                var radius=w*(capital?.28f:.18f); var z=height+w*.15f;
                m.Cylinder(new(0,y,height),radius,w*.17f,hull);
                var barrels=capital?2:1;
                for(var i=0;i<barrels;i++) m.Box(new((i-(barrels-1)/2f)*w*.12f,y+Math.Sign(y)*w*.3f,z),new(w*.045f,w*.55f,w*.045f),dark);
            }
        return m.Vertices.ToArray();
    }
}
