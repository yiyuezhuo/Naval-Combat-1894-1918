using UnityEngine;
using UnityEngine.Rendering;

namespace NavalCombat.Unity
{
    public sealed class NavalCoastline : MonoBehaviour
    {
        NavalWorld world;
        Mesh mesh;
        Material material;
        int revision;
        public async void Load(NavalWorld target,string resolution)
        {
            world=target;int request=++revision;
            if(mesh!=null){Destroy(mesh);mesh=null;}
            if(resolution=="None")return;
            var path=Path.Combine(UnityEngine.Application.streamingAssetsPath,"NavalCombat/Coastlines/ne_"+resolution+"_coastline.shp");
            try
            {
                var lines=await Task.Run(()=>Read(path));
                if(this==null || request!=revision)return;
                var vertices=new List<Vector3>();var indices=new List<int>();
                foreach(var line in lines)
                {
                    for(int i=1;i<line.Length/2;i++)
                    {
                        var a=world.Project(line[(i-1)*2+1],line[(i-1)*2]);var b=world.Project(line[i*2+1],line[i*2]);
                        if(a.sqrMagnitude>800000f*800000f || b.sqrMagnitude>800000f*800000f || (a-b).sqrMagnitude>200000f*200000f)continue;
                        a.y=b.y=2;indices.Add(vertices.Count);vertices.Add(a);indices.Add(vertices.Count);vertices.Add(b);
                    }
                }
                mesh=new Mesh {name="Natural Earth coastline",indexFormat=IndexFormat.UInt32};mesh.SetVertices(vertices);mesh.SetIndices(indices,MeshTopology.Lines,0);mesh.RecalculateBounds();
                var filter=GetComponent<MeshFilter>();if(filter==null)filter=gameObject.AddComponent<MeshFilter>();filter.sharedMesh=mesh;
                var renderer=GetComponent<MeshRenderer>();if(renderer==null)renderer=gameObject.AddComponent<MeshRenderer>();
                if(material==null){material=new Material(Shader.Find("Universal Render Pipeline/Unlit"));material.color=new Color(.75f,.79f,.71f);}
                renderer.sharedMaterial=material;
            }
            catch(Exception e){if(this!=null)Debug.LogError("Coastline: "+e.Message);}
        }
        static List<double[]> Read(string path)
        {
            var lines=new List<double[]>();using var r=new BinaryReader(File.OpenRead(path));r.BaseStream.Position=100;
            while(r.BaseStream.Position<r.BaseStream.Length)
            {
                r.ReadInt32();var bytes=r.ReadBytes(4);int size=(bytes[0]<<24|bytes[1]<<16|bytes[2]<<8|bytes[3])*2;
                var end=r.BaseStream.Position+size;int type=r.ReadInt32();
                if(type==3 || type==5)
                {
                    for(int i=0;i<4;i++)r.ReadDouble();int parts=r.ReadInt32(),points=r.ReadInt32();
                    var offsets=new int[parts+1];for(int i=0;i<parts;i++)offsets[i]=r.ReadInt32();offsets[parts]=points;
                    var values=new double[points*2];for(int i=0;i<values.Length;i++)values[i]=r.ReadDouble();
                    for(int i=0;i<parts;i++){var part=new double[(offsets[i+1]-offsets[i])*2];Array.Copy(values,offsets[i]*2,part,0,part.Length);lines.Add(part);}
                }
                r.BaseStream.Position=end;
            }
            return lines;
        }
        void OnDestroy(){revision++;if(mesh!=null)Destroy(mesh);if(material!=null)Destroy(material);}
    }
}
