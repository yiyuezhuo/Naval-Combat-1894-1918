using NavalCombat.Avalonia.Controls;
using NavalCombatCore;
using UnityEngine;

namespace NavalCombat.Unity
{
public sealed partial class NavalWorld
{
    // Stable identity/ownership; the container stays at the world origin so historical
    // wake samples do not move with the vessel. Only Model follows the vessel pose.
    sealed class ShipView
    {
        public Transform Root,Model,Weapons;
        public ShipLayoutMesh.Model Geometry;
        public MeshFilter Hull,Marker;
        public Transform[] Mounts;
        public Transform[][] Barrels;
        public Transform[] ModelParts;
        public int ModelLayer=-1;
    }
    readonly Dictionary<ShipLog,ShipView> shipViews=new();
    readonly HashSet<ShipLog> visibleShips=new();
    static Transform Child(string name,Transform parent)
    {
        var t=new GameObject(name).transform;t.SetParent(parent,false);return t;
    }
    ShipView GetShipView(ShipLog ship)
    {
        if(!shipViews.TryGetValue(ship,out var view))
        {
            view=new ShipView{Root=Child($"{ship.GetMemberName()} [{ship.objectId}]",shipRenderRoot)};
            shipViews.Add(ship,view);
        }
        return view;
    }
    MeshFilter ShipPart(string name,Transform parent,float[] geometry)
    {
        var t=Child(name,parent);var filter=t.gameObject.AddComponent<MeshFilter>();
        filter.sharedMesh=GetMesh(geometry);t.gameObject.AddComponent<MeshRenderer>().sharedMaterial=shipMaterial;return filter;
    }
    void BuildShipView(ShipView view,ShipLayoutMesh.Model model)
    {
        if(view.Model!=null){view.Model.gameObject.SetActive(false);Destroy(view.Model.gameObject);}
        view.Geometry=model;view.Model=Child("Model",view.Root);
        view.Hull=ShipPart("Hull and superstructure",view.Model,model.StaticGeometry);
        view.Weapons=Child("Weapons",view.Model);
        view.Mounts=new Transform[model.weapons.Count];view.Barrels=new Transform[model.weapons.Count][];
        for(int i=0;i<model.weapons.Count;i++)
        {
            var weapon=model.weapons[i];var pieces=model.WeaponGeometry[i];
            var mount=view.Mounts[i]=Child($"{weapon.Part.Kind} {i+1} [{weapon.Part.MountRecordId}]",view.Weapons);
            mount.localPosition=new Vector3(weapon.Origin.X,weapon.Origin.Z,weapon.Origin.Y);
            if(pieces.Housing.Length>0)ShipPart("Housing",mount,pieces.Housing);
            view.Barrels[i]=new Transform[pieces.Barrels.Length];
            for(int j=0;j<pieces.Barrels.Length;j++)view.Barrels[i][j]=ShipPart($"Barrel {j+1}",mount,pieces.Barrels[j]).transform;
        }
        view.ModelParts=view.Model.GetComponentsInChildren<Transform>(true);view.ModelLayer=-1;
    }
    void UpdateShipViews()
    {
        visibleShips.Clear();
        foreach(var ship in Session.GameState.shipLogsOnMapOrDestroyed)
        {
            if(!Session.CanDisplay(ship))continue;
            visibleShips.Add(ship);var view=GetShipView(ship);view.Root.gameObject.SetActive(true);
            var position=Project(ship.position.LatDeg,ship.position.LonDeg);
            bool marker=RenderChartMarker(ship,position);
            if(view.Marker!=null)view.Marker.gameObject.SetActive(marker);
            bool closeUp=NearPreview(position);
            if(marker&&!closeUp){if(view.Model!=null)view.Model.gameObject.SetActive(false);continue;}
            var pose=ShipLayoutMesh.UpdatePose(ship);pose.ApplyShots(ship,Gunfire);
            if(!ReferenceEquals(view.Geometry,pose.Model))BuildShipView(view,pose.Model);
            int layer=marker?PreviewLayer:0;
            if(view.ModelLayer!=layer){foreach(var part in view.ModelParts)part.gameObject.layer=layer;view.ModelLayer=layer;}
            view.Model.gameObject.SetActive(true);
            view.Model.SetPositionAndRotation(position,Quaternion.Euler(0,(float)ship.headingDeg,0));
            view.Model.localScale=Vector3.one*ShipScale(ship);
            bool destroyed=ship.mapState==MapState.Destroyed;
            view.Hull.sharedMesh=GetMesh(destroyed?pose.Model.DestroyedGeometry:pose.Model.StaticGeometry);
            view.Weapons.gameObject.SetActive(!destroyed&&(distance<=35000||closeUp));
            if(destroyed||(distance>35000&&!closeUp))continue;
            for(int i=0;i<view.Mounts.Length;i++)
            {
                view.Mounts[i].localRotation=Quaternion.Euler(0,(float)pose.Angles[i],0);
                for(int j=0;j<view.Barrels[i].Length;j++)view.Barrels[i][j].localPosition=new Vector3(0,0,-pose.Recoils[i][j]);
            }
        }
        foreach(var pair in shipViews)if(!visibleShips.Contains(pair.Key))pair.Value.Root.gameObject.SetActive(false);
    }
    void ResetShipViews()
    {
        foreach(var view in shipViews.Values){view.Root.gameObject.SetActive(false);Destroy(view.Root.gameObject);}
        shipViews.Clear();visibleShips.Clear();
    }
}
}
