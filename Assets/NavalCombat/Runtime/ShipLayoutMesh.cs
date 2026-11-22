using System.Numerics;
using System.Runtime.CompilerServices;
using NavalCombat.Application;
using NavalCombatCore;
using NavalCombat.Avalonia.Services;

namespace NavalCombat.Avalonia.Controls;

/// <summary>Physical mesh generation independent of 2D glyph sizing.</summary>
internal static class ShipLayoutMesh
{
    static readonly ConditionalWeakTable<ShipClass, Model> models = new();
    static readonly ConditionalWeakTable<ShipLog, Pose> poses = new();
    internal sealed class Pose(Model model)
    {
        internal readonly Model Model=model;
        internal readonly double[] Angles=new double[model.weapons.Count];
        internal readonly Dictionary<string,int> Mounts=[];
        internal readonly AbstractMountStatusRecord[] States=new AbstractMountStatusRecord[model.weapons.Count];
        internal readonly float[][] Recoils=model.weapons.Select(w=>new float[w.Part.Barrels]).ToArray();
        internal void ApplyShots(ShipLog ship,GunneryVisualTracker visuals)
        {
            foreach(var recoil in Recoils) Array.Clear(recoil, 0, recoil.Length);
            if(visuals==null)return;
            foreach(var shell in visuals.BridgeShells)
            {
                if(shell.Shot.ShooterId!=ship.objectId || shell.Shot.WeaponKind!=GunneryWeaponKind.Battery
                    || !Mounts.TryGetValue(shell.Shot.WeaponId,out var index) || Recoils[index].Length==0)continue;
                var barrel=shell.BarrelSequence%Recoils[index].Length;
                var part=Model.weapons[index].Part;
                Recoils[index][barrel]=Math.Max(Recoils[index][barrel],(float)shell.RecoilFraction*(float)Math.Clamp(part.Caliber*2,.08,.65));
                // Freeze launch orientation once. Subsequent tracking must not bend an in-flight shot.
                shell.Muzzle ??= Model.Muzzle(index,barrel,Angles[index],ship.headingDeg);
            }
        }
        internal void Update(ShipLog ship) => Model.UpdatePose(ship,Angles,States);
    }
    internal static Pose UpdatePose(ShipLog ship)
    {
        var model=Cached(ship.shipClass);
        if(!poses.TryGetValue(ship,out var pose) || !ReferenceEquals(pose.Model,model))
        {
            poses.Remove(ship);poses.Add(ship,pose=new Pose(model));
            var states=ship.batteryStatus.SelectMany(b=>b.mountStatus.Cast<AbstractMountStatusRecord>())
                .Concat(ship.torpedoSectorStatus.mountStatus).Where(m=>!string.IsNullOrEmpty(m.mountRecordId))
                .ToDictionary(m=>m.mountRecordId);
            for(var i=0;i<model.weapons.Count;i++)
            {
                var part=model.weapons[i].Part;
                var state=states.GetValueOrDefault(part.MountRecordId);
                pose.States[i]=state;
                if(part.Kind==ShipGraphicPartKind.Gun && state!=null) pose.Mounts[state.objectId]=i;
            }
        }
        pose.Update(ship);return pose;
    }
    internal static bool CanGenerate(ShipClass ship) => ship != null && ship.type != ShipType.LandBattery
        && double.IsFinite(ship.lengthFoot) && ship.lengthFoot > 0 && double.IsFinite(ship.beamFoot) && ship.beamFoot > 0;
    internal static void Invalidate(ShipClass ship) => models.Remove(ship);
    internal static ShipClassGraphicLayout Layout(ShipClass ship) => ShipGraphicLayoutBuilder.Build(ShipClassPlaceholderRenderAdapter.CreateRenderInput(ship));
    internal static Model Cached(ShipClass ship)
    {
        if (models.TryGetValue(ship, out var model) && model.Revision != ship.RuntimeLayoutRevision)
            models.Remove(ship);
        return models.GetValue(ship, s => new Model(s));
    }
    internal static float[] Build(ShipClass ship,Func<ShipGraphicPart,bool> highlighted=null) => ship.type==ShipType.LandBattery ? NavalShipMesh.Build(ship,false,false) : new Model(ship).Raw(highlighted);
    internal static Vector3 Eye(ShipClass ship)
    {
        var eye=Cached(ship).Layout.Observer;
        return new((float)eye.X,(float)eye.Y,(float)eye.Z);
    }
    internal sealed class Model
    {
        internal readonly ShipClassGraphicLayout Layout;
        internal readonly int Revision;
        readonly ShipClassPlaceholderRenderInput input;
        readonly List<float> hullRaw = [], structureRaw = [];
        readonly List<(ShipGraphicPart Part,int Start,int Count)> structureRanges=[];
        readonly float[] staticPrepared, hullPrepared;
        internal readonly List<(ShipGraphicPart Part,Vector3 Origin,float[] Raw,float[] Prepared)> weapons = [];
        internal readonly List<(PlaceholderMountRenderRecord Record,bool CanTrain)> weaponBindings=[];
        readonly List<(float[] Housing,float[][] Barrels)> weaponPieces=[];
        internal float[] StaticGeometry => staticPrepared;
        internal float[] DestroyedGeometry => hullPrepared;
        internal IReadOnlyList<(float[] Housing,float[][] Barrels)> WeaponGeometry => weaponPieces;
        internal IEnumerable<(ShipGraphicPart Part,float[] Raw)> StructureGeometry => structureRanges.Where(r=>ShipGraphicLayoutBuilder.IsStructure(r.Part.Kind)).Select(r=>(r.Part,structureRaw.Skip(r.Start).Take(r.Count).ToArray()));
        internal NavalPickMesh PickMesh {get;private set;}
        internal NavalPickMesh DestroyedPickMesh {get;private set;}
        readonly float length, beam;
        internal Model(ShipClass ship)
        {
            Revision=ship.RuntimeLayoutRevision;
            input=ShipClassPlaceholderRenderAdapter.CreateRenderInput(ship);
            Layout=ShipGraphicLayoutBuilder.Build(input);
            (length,beam)=NavalShipMesh.Dimensions(ship);
            var hull=new NavalMesh(); var surface=new Vector3(.30f,.35f,.38f); var deck=new Vector3(.69f,.58f,.40f);
            var height=(float)Layout.Freeboard;
            static Vector3 HullBottom(Vector3 top) => new(
                top.X*(float)ShipGraphicLayoutBuilder.HullBottomBeamScale,
                top.Y*(float)ShipGraphicLayoutBuilder.HullBottomLengthScale,
                (float)ShipGraphicLayoutBuilder.HullBottomZ);
            for(var i=0;i<Layout.Hull.Count;i++)
            {
                var a=Layout.Hull[i]; var b=Layout.Hull[(i+1)%Layout.Hull.Count];
                if(a==b) continue; // The two hull sides share the stem; do not emit a degenerate face.
                var va=new Vector3((float)a.X*beam,(float)a.Y*length,height);
                var vb=new Vector3((float)b.X*beam,(float)b.Y*length,height);
                hull.Triangle(new(0,0,height),vb,va,deck);
                hull.Quad(va,vb,HullBottom(vb),HullBottom(va),surface);
            }
            hullRaw.AddRange(hull.Vertices);
            var structure=new NavalMesh();
            foreach(var p in Layout.Parts)
            {
                // This is a waterline model, not a full underwater hull. Submerged
                // fixed tubes remain in the semantic/2D layout, but have no exposed mesh.
                if(p.Kind is ShipGraphicPartKind.RapidFire or ShipGraphicPartKind.SubmergedTorpedo or ShipGraphicPartKind.UnspecifiedTorpedo) continue;
                var pos=new Vector3((float)p.X*beam,(float)p.Y*length,(float)p.Z);
                var w=(float)p.Width*beam; var l=(float)p.Length*length; var h=(float)p.Height;
                if(p.Kind is ShipGraphicPartKind.Gun or ShipGraphicPartKind.DeckTorpedo)
                {
                    if(p.Kind==ShipGraphicPartKind.Gun && p.Z>Layout.Freeboard+.1
                        && !Layout.Parts.Any(s=>s.Kind is ShipGraphicPartKind.Deck or ShipGraphicPartKind.Deckhouse
                            && Math.Abs(p.X-s.X)<s.Width/2 && Math.Abs(p.Y-s.Y)<s.Length/2 && s.Z+s.Height>=p.Z-.1))
                    {
                        // Barbette/sponson support must reach the hull; an elevated mount cannot float.
                        var support=(float)(p.Z-Layout.Freeboard);
                        var bottom=new Vector3(pos.X,pos.Y,(float)Layout.Freeboard);
                        if(p.Appearance==ShipGunAppearance.Casemate) structure.Box(bottom+new Vector3(0,0,support/2),new(w,l,support),surface);
                        else structure.Cylinder(bottom,p.BarbetteDiameter>0?(float)p.BarbetteDiameter/2:w*.48f,support,surface,24);
                    }
                    if(p.Kind==ShipGraphicPartKind.Gun && p.Appearance==ShipGunAppearance.Casemate)
                        CasemateHousing(structure,pos,w,l,h,surface);
                    if(p.Kind==ShipGraphicPartKind.Gun && (p.Appearance==ShipGunAppearance.FixedMount || (p.Appearance==ShipGunAppearance.Turret && p.FixedHousing)))
                    {
                        var housing=new NavalMesh();
                        if(p.Appearance==ShipGunAppearance.FixedMount)
                            housing.Box(new(0,0,h*.32f),new(w*.8f,l*.9f,h*.64f),surface);
                        else TurretHouse(housing,w,l,h,surface,p.HousingShape);
                        var a=(float)(p.RestAngle*Math.PI/180);var c=MathF.Cos(a);var sn=MathF.Sin(a);
                        for(var i=0;i<housing.Vertices.Count;i+=9)
                        {
                            var v=housing.Vertices;
                            structure.Vertices.AddRange([pos.X+c*v[i]+sn*v[i+1],pos.Y-sn*v[i]+c*v[i+1],pos.Z+v[i+2],
                                v[i+3],v[i+4],v[i+5],c*v[i+6]+sn*v[i+7],-sn*v[i+6]+c*v[i+7],v[i+8]]);
                        }
                    }
                    var raw=Weapon(p,w,l,h);
                    var pivot=pos+new Vector3((float)p.GunPivotOffset.X,(float)p.GunPivotOffset.Y,0);
                    weapons.Add((p,pivot,raw,NavalMesh.Prepare(raw)));
                    weaponPieces.Add(p.Kind==ShipGraphicPartKind.Gun
                        ? (NavalMesh.Prepare(Weapon(p,w,l,h,-2)),Enumerable.Range(0,p.Barrels).Select(i=>NavalMesh.Prepare(Weapon(p,w,l,h,i))).ToArray())
                        : (NavalMesh.Prepare(raw),Array.Empty<float[]>()));
                    var records=p.Kind==ShipGraphicPartKind.Gun?input.batteryRecords[p.BatteryIndex].mountLocationRecords:input.torpedoMountRecords;
                    var record=records[p.RecordIndex];
                    weaponBindings.Add((record,record.trainable &&
                        !(record.mountArcs.Count>0 && record.mountArcs.All(a=>a.CoverageDeg<=30))));
                    continue;
                }
                var structureStart=structure.Vertices.Count;
                switch(p.Kind)
                {
                    case ShipGraphicPartKind.HullTorpedoPort:
                        HullTorpedoPorts(structure,p);
                        break;
                    case ShipGraphicPartKind.Deck:
                        var polygon=Layout.Hull.ToList();
                        foreach(var boundary in new[]{(Axis:0,Limit:p.X-p.Width/2,Sign:1),(Axis:0,Limit:p.X+p.Width/2,Sign:-1),
                            (Axis:1,Limit:p.Y-p.Length/2,Sign:1),(Axis:1,Limit:p.Y+p.Length/2,Sign:-1)})
                        {
                            var clipped=new List<(double X,double Y)>();
                            for(var j=0;j<polygon.Count;j++)
                            {
                                var a=polygon[j];var b=polygon[(j+1)%polygon.Count];
                                var av=boundary.Axis==0?a.X:a.Y;var bv=boundary.Axis==0?b.X:b.Y;
                                var insideA=(av-boundary.Limit)*boundary.Sign>=0;var insideB=(bv-boundary.Limit)*boundary.Sign>=0;
                                if(insideA) clipped.Add(a);
                                if(insideA!=insideB) {var t=(boundary.Limit-av)/(bv-av);clipped.Add((a.X+(b.X-a.X)*t,a.Y+(b.Y-a.Y)*t));}
                            }
                            polygon=clipped;
                        }
                        if(polygon.Count<3) break;
                        var center=new Vector3((float)polygon.Average(v=>v.X)*beam,(float)polygon.Average(v=>v.Y)*length,pos.Z+h);
                        for(var j=0;j<polygon.Count;j++)
                        {
                            var a=polygon[j];var b=polygon[(j+1)%polygon.Count];
                            var va=new Vector3((float)a.X*beam,(float)a.Y*length,pos.Z+h);var vb=new Vector3((float)b.X*beam,(float)b.Y*length,pos.Z+h);
                            structure.Triangle(center,vb,va,deck);
                            structure.Quad(va,vb,vb-new Vector3(0,0,h),va-new Vector3(0,0,h),surface);
                        }
                        break;
                    case ShipGraphicPartKind.Mast:
                        structure.Cylinder(pos,Math.Max(.12f,w/2),h,new(.12f,.15f,.17f),8);
                        structure.Box(pos+new Vector3(0,0,h*.7f),new((float)p.CrossbarWidth*beam,.14f,.14f),new(.12f,.15f,.17f));
                        if(p.StructureStyle==ShipStructureStyle.TripodMast)
                            foreach(var side in new[]{-1,1})Rod(structure,pos+new Vector3(side*Math.Max(w*3,beam*.1f),-Math.Max(l,beam*.1f),0),pos+new Vector3(0,0,h*.8f),Math.Max(.10f,w*.3f),surface);
                        structure.Cylinder(pos+new Vector3(0,0,h*.53f),Math.Max(.35f,w*1.8f),.18f,surface,12);
                        break;
                    case ShipGraphicPartKind.Funnel:
                        var rake=-(float)Math.Tan(p.RakeDegrees*Math.PI/180);
                        Ellipse(structure,pos,w/2,l/2,h,new(.58f,.45f,.25f),rake);
                        Ellipse(structure,pos+new Vector3(0,rake*(h-.25f),h-.25f),w*.52f,l*.52f,.3f,new(.12f,.15f,.17f),rake);
                        break;
                    case ShipGraphicPartKind.Bridge:
                        // Start at the actual deck beneath the whole support, not at the
                        // bridge's authored elevation (which may be above empty space).
                        var platformBottom=pos.Z+h-.3f;
                        var supportBase=(float)Layout.Freeboard;
                        foreach(var deckPart in Layout.Parts.Where(s=>s.Kind is ShipGraphicPartKind.Deck or ShipGraphicPartKind.Deckhouse))
                            if(Math.Abs(p.X-deckPart.X)+p.Width*.21<=deckPart.Width/2
                                && Math.Abs(p.Y-deckPart.Y)+p.Length*.375<=deckPart.Length/2
                                && deckPart.Z+deckPart.Height<=platformBottom)
                                supportBase=Math.Max(supportBase,(float)(deckPart.Z+deckPart.Height));
                        var bridgeSupportHeight=platformBottom-supportBase;
                        if(bridgeSupportHeight>0)
                            structure.Box(new(pos.X,pos.Y,supportBase+bridgeSupportHeight/2),
                                new(w*.42f,l*.75f,bridgeSupportHeight),surface);
                        structure.Box(pos+new Vector3(0,0,h-.15f),new((float)p.WingWidth*beam,l,.3f),surface);
                        if(p.StructureStyle==ShipStructureStyle.ClosedBridge)
                        {
                            var cabinHeight=Math.Min(2.4f,h);
                            structure.Box(pos+new Vector3(0,0,h-cabinHeight/2),new(w*.65f,l,cabinHeight),surface);
                            structure.Box(pos+new Vector3(0,l*.505f,h-cabinHeight*.3f),new(w*.58f,.05f,cabinHeight*.25f),new(.12f,.20f,.24f));
                        }
                        // Short front screen makes the open command platform readable without railings.
                        structure.Box(pos+new Vector3(0,l*.47f,h+.25f),new(w,.12f,.5f),surface);
                        break;
                    case ShipGraphicPartKind.Deckhouse when p.StructureStyle==ShipStructureStyle.BeveledDeckhouse:
                        var outline=ShipGraphicLayoutBuilder.HousingOutline(ShipGunHousingShape.Beveled);
                        for(var i=0;i<outline.Length;i++)
                        {
                            var a=outline[i];var b=outline[(i+1)%outline.Length];
                            var bottomA=pos+new Vector3((float)a.X*w,(float)a.Y*l,0);var bottomB=pos+new Vector3((float)b.X*w,(float)b.Y*l,0);
                            var topA=bottomA+new Vector3(0,0,h);var topB=bottomB+new Vector3(0,0,h);
                            structure.Quad(bottomA,bottomB,topB,topA,surface);structure.Triangle(pos+new Vector3(0,0,h),topA,topB,surface);
                        }
                        break;
                    default:
                        structure.Box(pos+new Vector3(0,0,h/2),new(w,l,h),p.Kind==ShipGraphicPartKind.Deck?deck:surface);
                        break;
                }
                structureRanges.Add((p,structureStart,structure.Vertices.Count-structureStart));
            }
            structureRaw.AddRange(structure.Vertices);
            PickMesh=new NavalPickMesh(hullRaw.Concat(structureRaw).ToArray(),9);
            DestroyedPickMesh=new NavalPickMesh(hullRaw.ToArray(),9);
            hullPrepared=NavalMesh.Prepare(hullRaw.ToArray());
            staticPrepared=NavalMesh.Prepare(hullRaw.Concat(structureRaw).ToArray());
        }
        void HullTorpedoPorts(NavalMesh mesh,ShipGraphicPart part)
        {
            var broadside=part.DirectionX!=0;
            var sign=Math.Sign(broadside?part.DirectionX:part.DirectionY);
            var dark=new Vector3(.07f,.09f,.10f);
            var rim=new Vector3(.39f,.43f,.46f);
            for(var tube=0;tube<part.Barrels;tube++)
            {
                var center=(tube-(part.Barrels-1)*.5)*part.TubeSpacing;
                Vector3 Point(double radius,int segment)
                {
                    var angle=segment*Math.PI/8;
                    var offset=center+radius*part.Caliber*.5*Math.Cos(angle);
                    var along=broadside?part.Y+offset/length:part.X+offset/beam;
                    var z=part.Z+radius*part.Height*.5*Math.Sin(angle);
                    var (x,y)=Layout.HullPortPosition(broadside,sign,along,z);
                    return new((float)(x*beam+.025*part.DirectionX),(float)(y*length+.025*part.DirectionY),(float)z);
                }
                var hub=Point(0,0);
                var winding=broadside?sign:-sign;
                for(var i=0;i<16;i++)
                {
                    var a=i*winding; var b=(i+1)*winding;
                    mesh.Triangle(hub,Point(.82,a),Point(.82,b),dark);
                    mesh.Quad(Point(.82,a),Point(1,a),Point(1,b),Point(.82,b),rim);
                }
            }
        }
        internal static float[] Weapon(ShipGraphicPart p,float width,float length,float height,int barrelFilter=-1)
        {
            var m=new NavalMesh(); var grey=new Vector3(.30f,.35f,.38f); var dark=new Vector3(.12f,.15f,.17f);
            var gun=p.Kind==ShipGraphicPartKind.Gun;
            if(!gun)
            {
                var diameter=(float)p.Caliber;var axis=(float)p.Height;
                if(barrelFilter<0)
                {
                    if(p.Trainable)m.Cylinder(Vector3.Zero,Math.Max(.35f,width*.6f),.18f,grey,24);
                    m.Box(new(0,0,(axis-diameter/2)/2),new(Math.Max(width,.4f),.35f,Math.Max(.1f,axis-diameter/2)),grey);
                }
                if(barrelFilter!=-2)
                    for(var i=0;i<p.Barrels;i++)
                    {
                        if(barrelFilter>=0 && barrelFilter!=i)continue;
                        var offset=(i-(p.Barrels-1)/2f)*(float)p.TubeSpacing;
                        var angle=(i-(p.Barrels-1)/2f)*(float)p.TubeSplay*MathF.PI/180;
                        TorpedoTube(m,offset,angle,(float)p.BarrelStart,(float)p.BarrelLength,diameter,axis,grey,dark);
                    }
                return m.Vertices.ToArray();
            }
            if(gun && barrelFilter<0)
            {
                switch(p.Appearance)
                {
                    case ShipGunAppearance.Turret:
                        if(!p.FixedHousing) TurretHouse(m,width,length,height,grey,p.HousingShape); break;
                    case ShipGunAppearance.FixedMount:
                        break; // The carriage belongs to the static geometry.
                    case ShipGunAppearance.Casemate:
                        // The curved shield turns inside the fixed dark opening and stays put during recoil.
                        var socketRadius=Math.Min(width,length)*.15f;
                        for(var i=0;i<p.Barrels;i++)
                        {
                            var x=(i-(p.Barrels-1)/2f)*width/Math.Max(2,p.Barrels+1);
                            m.Cylinder(new(x,0,height*.39f),socketRadius,height*.53f,grey,16);
                        }
                        break;
                    case ShipGunAppearance.Shield:
                        m.Cylinder(Vector3.Zero,width*.25f,height*.65f,grey,10);
                        m.Box(new(0,length*.15f,height*.65f),new(width,.15f,height),grey); break;
                    default:
                        m.Cylinder(Vector3.Zero,width*.2f,height*.7f,grey,10); break;
                }
            }
            var barrel=(float)p.BarrelLength;
            var spacing=width/Math.Max(2,p.Barrels+1);
            for(var i=0;i<p.Barrels;i++)
            {
                if(barrelFilter==-2 || (barrelFilter>=0 && barrelFilter!=i))continue;
                var x=(i-(p.Barrels-1)/2f)*spacing;
                if(gun) GunBarrel(m,new(x,(float)p.BarrelStart,height*(p.Appearance==ShipGunAppearance.Turret?.5f:.7f)),barrel,(float)p.Caliber,dark,(float)(ShipGraphicLayoutBuilder.VisibleBarrelStart(p,length)-p.BarrelStart));
                else m.Box(new(x,barrel*.42f,.2f),new(.45f,barrel,.45f),dark);
            }
            return m.Vertices.ToArray();
        }
        static void CasemateHousing(NavalMesh mesh,Vector3 pos,float width,float length,float height,Vector3 armor)
        {
            // A recessed horizontal opening between a fixed sill and roof. The hull hides the inboard half.
            var recess=new Vector3(.09f,.12f,.14f);
            const int sides=24;
            (float Radius,float Z,Vector3 Wall)[] rings=[
                (.48f,0,armor),(.5f,.34f,armor),(.43f,.34f,recess),
                (.43f,.94f,recess),(.5f,.94f,armor),(.49f,1,armor)];
            Vector3 Rim(int i,int ring)
            {
                var a=(i%sides)*2*MathF.PI/sides;
                var (radius,z,_)=rings[ring];
                return pos+new Vector3(MathF.Cos(a)*width*radius,MathF.Sin(a)*length*radius,z*height);
            }
            for(var i=0;i<sides;i++)
            {
                var j=i+1;
                for(var r=0;r<rings.Length-1;r++)
                    mesh.Quad(Rim(i,r),Rim(j,r),Rim(j,r+1),Rim(i,r+1),rings[r].Wall);
                mesh.Triangle(pos+Vector3.UnitZ*height,Rim(i,rings.Length-1),Rim(j,rings.Length-1),armor);
                mesh.Triangle(pos,Rim(j,0),Rim(i,0),armor);
            }
        }
        static void TorpedoTube(NavalMesh mesh,float offset,float angle,float start,float length,float diameter,float axis,Vector3 color,Vector3 dark)
        {
            // Eight radial faces keep thin tubes legible in line art, as with gun barrels.
            const int sides=8;
            var c=MathF.Cos(angle);var s=MathF.Sin(angle);
            Vector3 Point(float y,float radius,int i)
            {
                var a=i*(2 * MathF.PI)/sides;var x=MathF.Cos(a)*radius;
                return new(offset+x*c+y*s,-x*s+y*c,axis+MathF.Sin(a)*radius);
            }
            var rings=new (float Y,float R)[]{(start,diameter*.5f),(start+.08f,diameter*.5f),
                (start+.08f,diameter*.54f),(start+.15f,diameter*.54f),(start+.15f,diameter*.5f),
                (start+length-.15f,diameter*.5f),(start+length-.15f,diameter*.54f),
                (start+length,diameter*.54f),(start+length,diameter*.42f),(start+length-.12f,diameter*.42f)};
            for(var i=0;i<sides;i++)
            {
                for(var r=0;r<rings.Length-1;r++)mesh.Quad(Point(rings[r].Y,rings[r].R,i),Point(rings[r+1].Y,rings[r+1].R,i),Point(rings[r+1].Y,rings[r+1].R,i+1),Point(rings[r].Y,rings[r].R,i+1),color);
                mesh.Triangle(Point(start,0,0),Point(start,diameter*.5f,i+1),Point(start,diameter*.5f,i),color);
                mesh.Triangle(Point(start+length-.12f,0,0),Point(start+length-.12f,diameter*.42f,i),Point(start+length-.12f,diameter*.42f,i+1),dark);
            }
        }
        // A low armoured gunhouse with a broad front, bevelled corners and sloped sides.
        // Its fixed circular barbette is generated in the static hull mesh.
        static void TurretHouse(NavalMesh mesh,float width,float length,float height,Vector3 color,ShipGunHousingShape shape)
        {
            var outline=ShipGraphicLayoutBuilder.HousingOutline(shape).Select(p=>new Vector2((float)p.X,(float)p.Y)).ToArray();
            var taper=shape==ShipGunHousingShape.Circular?.96f:.88f;
            for(var i=0;i<outline.Length;i++)
            {
                var a=outline[i];var b=outline[(i+1)%outline.Length];
                var bottomA=new Vector3(a.X*width,a.Y*length,0);
                var bottomB=new Vector3(b.X*width,b.Y*length,0);
                var topA=new Vector3(bottomA.X*taper,bottomA.Y*taper,height);
                var topB=new Vector3(bottomB.X*taper,bottomB.Y*taper,height);
                mesh.Quad(bottomA,bottomB,topB,topA,color);
                mesh.Triangle(new(0,0,height),topA,topB,color);
            }
        }
        // Axial rings share vertices so contour adjacency hides triangulation seams.
        // Exterior diameters are schematic multiples of bore, independent of mount count.
        static void GunBarrel(NavalMesh mesh,Vector3 origin,float length,float caliber,Vector3 color,float clip)
        {
            var original=new[]{(T:0f,R:1.5f),(T:.2f,R:1.4f),(T:.6f,R:.9f),(T:1f,R:.7f)};
            var cut=Math.Clamp(clip/length,0,.99f);
            var segment=Enumerable.Range(0,3).First(i=>cut<=original[i+1].T);
            var a=original[segment];var b=original[segment+1];
            var radius=a.R+(b.R-a.R)*(cut-a.T)/(b.T-a.T);
            var stations=new[]{(T:cut,R:radius)}.Concat(original.Where(p=>p.T>cut)).ToArray();
            // Eight facets retain readable edges for small barrels in line-art views.
            const int sides=8;
            Vector3 Point(int ring,int side)
            {
                var a=(side%sides)*(2 * MathF.PI)/sides;
                return origin+new Vector3(MathF.Cos(a)*caliber*stations[ring].R,length*stations[ring].T,
                    MathF.Sin(a)*caliber*stations[ring].R);
            }
            for(var i=0;i<sides;i++)
            {
                for(var r=0;r<stations.Length-1;r++)
                    mesh.Quad(Point(r,i),Point(r+1,i),Point(r+1,i+1),Point(r,i+1),color);
                mesh.Triangle(origin+new Vector3(0,length*cut,0),Point(0,i),Point(0,i+1),color);
                mesh.Triangle(origin+new Vector3(0,length,0),Point(stations.Length-1,i+1),Point(stations.Length-1,i),color);
            }
        }
        static void Ellipse(NavalMesh mesh,Vector3 position,float rx,float ry,float height,Vector3 color,float rake=0)
        {
            var m=new NavalMesh();m.Cylinder(Vector3.Zero,1,height,color,16);
            for(var i=0;i<m.Vertices.Count;i+=9)
            {
                var n=Vector3.Normalize(new Vector3(m.Vertices[i+6]/rx,m.Vertices[i+7]/ry,m.Vertices[i+8]-rake*m.Vertices[i+7]/ry));
                mesh.Vertices.AddRange([position.X+m.Vertices[i]*rx,position.Y+m.Vertices[i+1]*ry+rake*m.Vertices[i+2],position.Z+m.Vertices[i+2],color.X,color.Y,color.Z,n.X,n.Y,n.Z]);
            }
        }
        static void Rod(NavalMesh mesh,Vector3 start,Vector3 end,float radius,Vector3 color)
        {
            var forward=Vector3.Normalize(end-start);var right=Vector3.Normalize(Vector3.Cross(forward,Vector3.UnitY));var up=Vector3.Cross(right,forward);
            Vector3 Offset(int i)=>radius*(right*MathF.Cos(i*MathF.PI/4)+up*MathF.Sin(i*MathF.PI/4));
            for(var i=0;i<8;i++)mesh.Quad(start+Offset(i),start+Offset(i+1),end+Offset(i+1),end+Offset(i),color);
        }
        internal void UpdatePose(ShipLog ship,Span<double> angles,AbstractMountStatusRecord[] states)
        {
            for(var i=0;i<weapons.Count;i++)
            {
                var part=weapons[i].Part;var binding=weaponBindings[i];ShipLog target=null;
                if(part.Kind==ShipGraphicPartKind.Gun || binding.CanTrain) target=states[i]?.GetFiringTarget();
                angles[i]=ShipGraphicLayoutBuilder.ResolveDirection(part,binding.Record,ShipDetailRenderer.GetTargetDirectionRelativeToBow(ship,target));
            }
        }
        internal (Vector3 Offset,Vector3 Direction) Muzzle(int index,int barrel,double angle,double heading)
        {
            var w=weapons[index];var p=w.Part;
            var spacing=(float)p.Width*beam/Math.Max(2,p.Barrels+1);
            var tip=new Vector3((barrel-(p.Barrels-1)/2f)*spacing,(float)(p.BarrelStart+p.BarrelLength),
                (float)p.Height*(p.Appearance==ShipGunAppearance.Turret?.5f:.7f));
            var a=(float)((heading+angle)*Math.PI/180);var h=(float)(heading*Math.PI/180);
            var body=new NavalSceneInstance([],Vector3.Zero,MathF.Cos(h),MathF.Sin(h));
            var gun=new NavalSceneInstance([],body.Transform(w.Origin),MathF.Cos(a),MathF.Sin(a));
            return (gun.Transform(tip),new(MathF.Sin(a),MathF.Cos(a),0));
        }
        internal float[] Prepared(ShipClassPlaceholderRenderInput pose,bool destroyed,bool white)
        {
            var result=new List<float>(destroyed?hullPrepared:staticPrepared);
            if(!destroyed)
                foreach(var w in weapons) Append(result,w.Prepared,w.Origin,ShipGraphicLayoutBuilder.Direction(w.Part,pose),MeijiPolitics.Rendering.SketchMesh.Stride);
            if(white)
                for(var i=0;i<result.Count;i+=MeijiPolitics.Rendering.SketchMesh.Stride)
                    if(result[i+3]==.30f) {result[i+3]=.72f;result[i+4]=.74f;result[i+5]=.71f;}
            return result.ToArray();
        }
        internal float[] Raw(Func<ShipGraphicPart,bool> highlighted=null)
        {
            var result=hullRaw.Concat(structureRaw).ToList();
            foreach(var range in structureRanges.Where(r=>highlighted?.Invoke(r.Part)==true))
                for(var i=hullRaw.Count+range.Start;i<hullRaw.Count+range.Start+range.Count;i+=9){result[i+3]=.2f;result[i+4]=.95f;result[i+5]=.85f;}
            foreach(var w in weapons)
            {
                var start=result.Count;
                Append(result,w.Raw,w.Origin,ShipGraphicLayoutBuilder.Direction(w.Part,input),9);
                if(highlighted?.Invoke(w.Part)==true)
                    for(var i=start;i<result.Count;i+=9){result[i+3]=.2f;result[i+4]=.95f;result[i+5]=.85f;}
            }
            return result.ToArray();
        }
        static void Append(List<float> output,float[] source,Vector3 pos,double degrees,int stride)
        {
            var a=(float)(degrees*Math.PI/180);var c=MathF.Cos(a);var s=MathF.Sin(a);
            for(var i=0;i<source.Length;i+=stride)
            {
                output.AddRange([pos.X+source[i]*c+source[i+1]*s,pos.Y-source[i]*s+source[i+1]*c,pos.Z+source[i+2],source[i+3],source[i+4],source[i+5]]);
                for(var n=6;n<stride;n+=3) output.AddRange([source[i+n]*c+source[i+n+1]*s,-source[i+n]*s+source[i+n+1]*c,source[i+n+2]]);
            }
        }
    }
}
