using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>Deterministic physical layout; no canvas, glyph scale or per-ship target state.</summary>
public static class ShipGraphicLayoutBuilder
{
    // Lower ring of the waterline mesh, in metres (Z) and fractions of the upper ring (XY).
    public const double HullBottomZ = -.3, HullBottomBeamScale = .9, HullBottomLengthScale = .99;
    public static ShipGraphicParameters EditableParameters(ShipClassPlaceholderRenderInput input)
    {
        var layout=Build(input);
        return new ShipGraphicParameters
        {
            source=input.graphicParameters?.source??"",notes=input.graphicParameters?.notes??"",
            freeboard=layout.Freeboard,bowFullness=input.graphicParameters?.bowFullness??1,sternFullness=input.graphicParameters?.sternFullness??.65,
            replaceStructures=true,mainBridgeId=input.graphicParameters?.mainBridgeId??"",
            structures=layout.Parts.Where(p=>p.Kind is ShipGraphicPartKind.Deck or ShipGraphicPartKind.Deckhouse or ShipGraphicPartKind.Bridge or ShipGraphicPartKind.Mast or ShipGraphicPartKind.Funnel)
                .Select(p=>new ShipGraphicStructure {objectId=p.StructureId,kind=p.Kind,x=p.X,y=p.Y,z=p.Z,width=p.Width,length=p.Length,height=p.Height,style=p.StructureStyle,wingWidth=p.WingWidth,crossbarWidth=p.CrossbarWidth,rakeDegrees=p.RakeDegrees}).ToList(),
            mounts=layout.Parts.Where(p=>p.Kind==ShipGraphicPartKind.Gun)
                .Select(p=>new ShipGraphicMountOverride {batteryId=p.BatteryId,mountRecordId=p.MountRecordId,
                    x=p.X,y=p.Y,z=p.Z,width=p.Width,length=p.Length,height=p.Height,barbetteDiameter=p.BarbetteDiameter,barrelLength=0,exposedBarrelLength=p.ExposedBarrelLength,restAngle=p.RestAngle,appearance=p.Appearance,housingShape=p.HousingShape,fixedHousing=p.FixedHousing}).ToList(),
            customObserver=input.graphicParameters?.customObserver??false,observerX=input.graphicParameters?.observerX??0,
            observerY=input.graphicParameters?.observerY??0,observerZ=input.graphicParameters?.observerZ??0
        };
    }
    public static (double Length, double Beam) Dimensions(ShipClassPlaceholderRenderInput input)
    {
        var small = input.type is PlaceholderShipType.Destroyer or PlaceholderShipType.TorpedoBoat or PlaceholderShipType.PatrolGunboat;
        return (Positive(input.lengthFoot * .3048, small ? 60 : 110), Positive(input.beamFoot * .3048, small ? 7 : 18));
    }
    static double Positive(double value, double fallback) => double.IsFinite(value) && value > 0 ? value : fallback;
    static double Finite(double value, double fallback = 0) => double.IsFinite(value) ? value : fallback;

    public static ShipClassGraphicLayout Build(ShipClassPlaceholderRenderInput input)
    {
        var (length, beam) = Dimensions(input);
        var p = input.graphicParameters;
        var deck = Positive(p?.freeboard ?? 0, Math.Max(1.5, beam * .22));
        var result = new ShipClassGraphicLayout { Freeboard = deck };
        var bow = Math.Clamp(Positive(p?.bowFullness ?? 1, 1), .4, 2);
        var stern = Math.Clamp(Positive(p?.sternFullness ?? .65, .65), .2, 1.5);
        double Breadth(double y) => y > .15 ? .5 * Math.Pow(Math.Max(0, (.5-y)/.35), bow * .65)
            : y < -.28 ? .5 * Math.Pow(Math.Max(0,1-Math.Pow((-y-.28)/.22,2)),stern*.75) : .5;
        var stations=Enumerable.Range(0,17).Select(i=>-.5+.22*(1-Math.Cos(i*Math.PI/32)))
            .Concat(Enumerable.Range(1,40).Select(i=>-.28+.78*i/40)).ToArray();
        foreach(var y in stations) result.Hull.Add((-Breadth(y),y));
        foreach(var y in stations.Skip(1).SkipLast(1).Reverse()) result.Hull.Add((Breadth(y),y));
        int automaticIndex=0;
        void Structure(ShipGraphicPartKind kind,double x,double y,double z,double w,double l,double h)
        {
            result.Parts.Add(new(kind,x,y,w,l,0,1,0)
                { Z=z,Height=h,StructureId="auto-"+automaticIndex++,WingWidth=w,CrossbarWidth=.45 });
        }
        if (p?.replaceStructures != true)
        {
            Structure(ShipGraphicPartKind.Deckhouse,0,0,deck,.48,.38,beam*.13);
            Structure(ShipGraphicPartKind.Bridge,0,.235,deck+beam*.13,.68,.065,beam*.12);
            Structure(ShipGraphicPartKind.Mast,0,.21,deck,.025,.004,beam*1.5);
            if (input.type != PlaceholderShipType.TorpedoBoat) Structure(ShipGraphicPartKind.Mast,0,-.26,deck,.025,.004,beam*1.4);
            var count = input.type is PlaceholderShipType.Transport or PlaceholderShipType.Repair ? 1
                : input.type is PlaceholderShipType.ArmoredCruiser or PlaceholderShipType.Destroyer ? 3 : 2;
            for(var i=0;i<count;i++) Structure(ShipGraphicPartKind.Funnel,0,.06-i*.095,deck+beam*.13,.18,.035,beam*.55);
        }
        var authored=p?.structures??new();
        var resolved=new Dictionary<ShipGraphicStructure,ShipGraphicPart>();
        var visiting=new HashSet<ShipGraphicStructure>();
        ShipGraphicPart Resolve(ShipGraphicStructure s)
        {
            if(resolved.TryGetValue(s,out var found))return found;
            if(!visiting.Add(s)){result.Warnings.Add("Structure attachment cycle: "+s.objectId);return null;}
            var parent=string.IsNullOrEmpty(s.parentId)?null:authored.FirstOrDefault(v=>v.objectId==s.parentId);
            var support=parent!=null&&IsStructure(parent.kind)?Resolve(parent):null;
            if(!string.IsNullOrEmpty(s.parentId)&&support==null)result.Warnings.Add("Missing structure support: "+s.objectId);
            var x=Finite(s.x)+(support?.X??0);var y=Finite(s.y)+(support?.Y??0);
            var z=Finite(s.z)+(support!=null?support.Z+support.Height:s.attachedToDeck?deck:0);
            var w=Positive(s.width,.1);var l=Positive(s.length,.02);
            if(s.kind==ShipGraphicPartKind.Funnel&&s.style==ShipStructureStyle.RoundFunnel)l=w*beam/length;
            found=new(s.kind,x,y,w,l,0,1,0) { Z=z,Height=Positive(s.height,1),
                StructureId=string.IsNullOrEmpty(s.objectId)?"legacy-"+authored.IndexOf(s):s.objectId,
                StructureStyle=s.style,WingWidth=Positive(s.wingWidth,w),CrossbarWidth=Positive(s.crossbarWidth,.45),RakeDegrees=Math.Clamp(Finite(s.rakeDegrees),-45,45) };
            visiting.Remove(s);resolved[s]=found;return found;
        }
        foreach(var s in authored)
        {
            if(!IsStructure(s.kind)){result.Warnings.Add("Structure cannot create weapons: "+s.kind);continue;}
            result.Parts.Add(Resolve(s));
        }
        var entries = new List<(PlaceholderMountRenderRecord Record,int Battery,int RecordIndex,double Caliber,string BatteryId)>();
        for(var b=0;b<input.batteryRecords.Count;b++)
            for(var r=0;r<input.batteryRecords[b].mountLocationRecords.Count;r++)
                entries.Add((input.batteryRecords[b].mountLocationRecords[r],b,r,input.batteryRecords[b].shellSizeInch,input.batteryRecords[b].objectId ?? ""));
        var used = new HashSet<ShipGraphicMountOverride>();
        foreach(var group in entries.GroupBy(e=>e.Record.mountLocation))
        {
            var placements=PlaceGuns(group.Select(e=>
                new GunPlacement(e.Battery,e.RecordIndex,e.Record,e.Caliber,
                    p?.mounts.FirstOrDefault(o=>!o.automaticPosition && !string.IsNullOrEmpty(e.Record.objectId) && o.batteryId==e.BatteryId && o.mountRecordId==e.Record.objectId))),length,beam,result.Warnings);
            foreach(var e in group)
            {
                var (x,_)=Position(e.Record.mountLocation,0,1);
                var y=placements[(e.Battery,e.RecordIndex)];
                var caliber=Positive(e.Caliber,.1)*.0254;
                var style=DefaultAppearance(e.Caliber,e.Record);
                var size=GunSize(e.Caliber,e.Record.barrels,beam,style);
                var width=size.Width;
                var o=p?.mounts.FirstOrDefault(o=>!string.IsNullOrEmpty(e.Record.objectId) && o.batteryId==e.BatteryId && o.mountRecordId==e.Record.objectId);
                if(o!=null) used.Add(o);
                var rest=o==null ? DefaultAngle(e.Record) : Finite(o.restAngle);
                var angle=rest*Math.PI/180;
                var actualWidth=o==null?width:Positive(o.width,width/beam)*beam;
                var actualLength=o==null?size.Length:Positive(o.length,size.Length/length)*length;
                var appearance=o?.appearance??style;
                var baseDiameter=Positive(o?.barbetteDiameter??0,appearance==ShipGunAppearance.Turret?actualWidth*1.08:0);
                var height=Positive(o?.height??0,GunSize(e.Caliber,e.Record.barrels,beam,appearance).Height);
                var z=deck;
                if(appearance==ShipGunAppearance.Casemate && x!=0 && (o==null || o.automaticPosition))
                {
                    // One battery deck below the weather deck, with clearance above the waterline on low hulls.
                    z=deck-Math.Min(Math.Max(2.4,height+.25),deck*.65);
                    var depth=(deck-(z+height*.5))/(deck-HullBottomZ);
                    var station=y/(1-depth*(1-HullBottomLengthScale));
                    var side=Breadth(station)*(1-depth*(1-HullBottomBeamScale));
                    // The fixed housing spans X in ship axes. Bury most of it and leave its outer face exposed.
                    x=Math.Sign(x)*Math.Max(0,side-actualWidth*.25/beam);
                }
                else
                {
                    // At arbitrary training angles either footprint axis can face the hull edge.
                    if(x!=0) x=Math.Sign(x)*Math.Min(Math.Abs(x),Math.Max(0,Breadth(y)-Math.Max(baseDiameter,Math.Max(actualWidth,actualLength))/beam*.5));
                    foreach(var structure in result.Parts.Where(s=>s.Kind is ShipGraphicPartKind.Deck or ShipGraphicPartKind.Deckhouse))
                        if(Math.Abs(x-structure.X)<structure.Width/2+actualWidth/beam/2
                            && Math.Abs(y-structure.Y)<structure.Length/2+actualLength/length/2)
                            z=Math.Max(z,structure.Z+structure.Height);
                    if(appearance==ShipGunAppearance.Turret) z+=.65;
                }
                if(o!=null && !o.automaticPosition) { x=Finite(o.x); y=Finite(o.y); z=Finite(o.z,z); }
                (double X,double Y) pivot=(0,0);
                if(appearance==ShipGunAppearance.Casemate)
                    pivot=x!=0 ? (Math.Sign(x)*actualWidth*.38,0) : (0,Math.Sign(y)*actualLength*.38);
                var barrelTotal=Positive(o?.barrelLength??0,caliber*Positive(input.batteryRecords[e.Battery].barrelLengthCalibers,30));
                var start=-Math.Min(barrelTotal*.2,actualWidth*.35);
                var face=appearance switch {
                    ShipGunAppearance.Turret=>actualLength*.47,
                    ShipGunAppearance.Shield=>actualLength*.15+.075,
                    ShipGunAppearance.Casemate=>(x!=0?actualWidth:actualLength)*.5,
                    _=>0d
                };
                var exposed=Positive(o?.exposedBarrelLength??0,Math.Max(caliber,barrelTotal+start-face));
                barrelTotal=face+exposed-start;
                var pivotDepth=Math.Abs(pivot.X)+Math.Abs(pivot.Y);
                result.Parts.Add(new(ShipGraphicPartKind.Gun,x,y,actualWidth/beam,actualLength/length,Math.Sin(angle),Math.Cos(angle),Math.Max(1,e.Record.barrels))
                { Z=z,Height=height,BarbetteDiameter=baseDiameter,GunPivotOffset=pivot,BarrelLength=barrelTotal,Caliber=caliber,BarrelStart=start-pivotDepth,BarrelFace=face-pivotDepth,ExposedBarrelLength=exposed,
                    BatteryId=e.BatteryId,MountRecordId=e.Record.objectId??"",BatteryIndex=e.Battery,RecordIndex=e.RecordIndex,
                    RestAngle=rest,Appearance=o?.appearance??style,HousingShape=o?.housingShape??ShipGunHousingShape.Beveled,FixedHousing=o?.fixedHousing??false });
            }
        }
        foreach(var o in p?.mounts ?? new()) if(!used.Contains(o)) result.Warnings.Add($"Unmatched mount: {o.batteryId}/{o.mountRecordId}");
        for(var r=0;r<input.torpedoMountRecords.Count;r++)
        {
            var rec=input.torpedoMountRecords[r];
            {
                var index=input.torpedoMountRecords.Take(r).Count(t=>t.mountLocation==rec.mountLocation);
                var count=input.torpedoMountRecords.Count(t=>t.mountLocation==rec.mountLocation);
                var (x,y)=Position(rec.mountLocation,index,count);
                var g=rec.torpedoGraphic??new TorpedoGraphicParameters();
                var small=input.type is PlaceholderShipType.TorpedoBoat or PlaceholderShipType.Destroyer;
                var submerged=g.installation==TorpedoInstallation.Underwater ||
                    (g.installation==TorpedoInstallation.Automatic && !rec.trainable && !small && rec.mountLocation!=PlaceholderMountLocation.Midship);
                var diameter=Positive(g.tubeDiameter,Positive(input.torpedoDiameterIn,18)*.0254+.06);
                if(g.installation is TorpedoInstallation.AboveWaterInHull or TorpedoInstallation.AboveWaterUnspecified)
                {
                    var side=x;
                    if(rec.mountLocation==PlaceholderMountLocation.Midship && g.customPosition) side=Finite(g.x);
                    var broadside=side!=0;
                    var sign=(broadside?side:y)<0?-1:1;
                    var radius=Math.Min(diameter/2,deck*.4);
                    var axisZ=Math.Clamp(g.customPosition?Finite(g.z,deck*.55):deck*.55,radius,deck-radius);
                    var barrels=Math.Max(1,rec.barrels);
                    var portSpacing=Math.Max(diameter*1.15,Positive(g.tubeSpacing,diameter*1.4));
                    var along=broadside?y:(index-(count-1)*.5)*barrels*portSpacing/beam;
                    if(g.customPosition) along=Finite(broadside?g.y:g.x,along);
                    var known=g.installation==TorpedoInstallation.AboveWaterInHull;
                    if(known) (x,y)=result.HullPortPosition(broadside,sign,along,axisZ);
                    else if(g.customPosition) (x,y)=(Finite(g.x,x),Finite(g.y,y));
                    result.Parts.Add(new(known?ShipGraphicPartKind.HullTorpedoPort:ShipGraphicPartKind.UnspecifiedTorpedo,
                        x,y,diameter/beam,diameter/length,broadside?sign:0,broadside?0:sign,barrels)
                    { Z=axisZ,Height=radius*2,Caliber=diameter,TubeSpacing=portSpacing,Trainable=rec.trainable,
                        RecordIndex=r,MountRecordId=rec.objectId??"",RestAngle=broadside?(sign>0?90:270):(sign>0?0:180) });
                    continue;
                }
                var tubeLength=Positive(g.tubeLength,submerged?.7:input.torpedoDiameterIn>0 && input.torpedoDiameterIn<=14?4.5:5);
                var splay=Math.Clamp(Finite(g.tubeSplay),-45,45);
                var pivot=Math.Clamp(Finite(g.tubePivot,.5),0,1);
                var spacing=Math.Max(diameter*1.15+2*tubeLength*Math.Max(pivot,1-pivot)*Math.Sin(Math.Abs(splay)*Math.PI/360),Positive(g.tubeSpacing,diameter*1.4));
                var rest=rec.trainable?Normalize(Finite(g.stowedAngle)):DefaultAngle(rec); var a=rest*Math.PI/180;
                var z=deck;
                if(submerged) { if(x!=0) x=Math.Sign(x)*Breadth(y); else y=Math.Sign(y)*.48; z=-.6; }
                if(g.customPosition) { x=Finite(g.x);y=Finite(g.y);z=Finite(g.z,deck); }
                else if(!submerged)
                {
                    // Reserve the full longitudinal tube footprint; never bury a launcher in a deckhouse.
                    var half=tubeLength/length/2+.012;
                    bool Clear(double cy)=>!result.Parts.Any(s=>s.Kind is ShipGraphicPartKind.Deckhouse or ShipGraphicPartKind.Bridge or ShipGraphicPartKind.Funnel or ShipGraphicPartKind.Deck
                        && Math.Abs(x-s.X)<s.Width/2+diameter/beam && Math.Abs(cy-s.Y)<s.Length/2+half)
                        && !result.Parts.Any(s=>s.Kind==ShipGraphicPartKind.DeckTorpedo && Math.Abs(cy-s.Y)<s.Length/2+half);
                    var candidates=Enumerable.Range(0,81).Select(i=>-.44+i*.011).Where(cy=>Math.Abs(cy)+half<.49).OrderBy(cy=>Math.Abs(cy-y));
                    if(!Clear(y)) { var available=candidates.Where(Clear).ToArray();if(available.Length>0)y=available[0];else result.Warnings.Add("No clear torpedo deck position: "+rec.objectId); }
                }
                result.Parts.Add(new(submerged?ShipGraphicPartKind.SubmergedTorpedo:ShipGraphicPartKind.DeckTorpedo,x,y,
                    (diameter+spacing*(Math.Max(1,rec.barrels)-1))/beam,tubeLength/length,Math.Sin(a),Math.Cos(a),Math.Max(1,rec.barrels))
                { Z=z,Height=Positive(g.tubeAxisHeight,diameter/2+.4),BarrelLength=tubeLength,Caliber=diameter,
                    BarrelStart=-tubeLength*pivot,TubeSpacing=spacing,TubeSplay=splay,Trainable=rec.trainable,
                    RecordIndex=r,MountRecordId=rec.objectId??"",RestAngle=rest });
            }
        }
        var bridge=result.Parts.FirstOrDefault(s=>s.Kind==ShipGraphicPartKind.Bridge&&s.StructureId==p?.mainBridgeId)
            ??result.Parts.FirstOrDefault(s=>s.Kind==ShipGraphicPartKind.Bridge);
        result.Observer=bridge==null ? (beam*.1,length*.22,deck+beam*.25+1.7)
            : (bridge.X*beam+Math.Min(1,bridge.Width*beam*.2),(bridge.Y+bridge.Length*.3)*length,bridge.Z+bridge.Height+1.7);
        if(p?.customObserver==true) result.Observer=(Finite(p.observerX)*beam,Finite(p.observerY)*length,Finite(p.observerZ,result.Observer.Z));
        return result;
    }

    public static bool IsStructure(ShipGraphicPartKind kind) => kind is ShipGraphicPartKind.Deck or ShipGraphicPartKind.Deckhouse or ShipGraphicPartKind.Bridge or ShipGraphicPartKind.Mast or ShipGraphicPartKind.Funnel;

    // IDs associate authored overrides only. Physical size and armament determine placement.
    sealed record GunPlacement(int Battery,int RecordIndex,PlaceholderMountRenderRecord Record,double Caliber,ShipGraphicMountOverride Override);
    internal static ShipGunAppearance DefaultAppearance(double caliber,PlaceholderMountRenderRecord record) =>
        record.fixedMount?ShipGunAppearance.FixedMount:record.casemate?ShipGunAppearance.Casemate:caliber>=7 || record.barrels>1?ShipGunAppearance.Turret:ShipGunAppearance.Shield;

    // Envelope fit to the plan comparisons in docs/SHIP_GUN_APPEARANCE.md, not measured hardware.
    // Crew/breech room is additive; shield/open footprints must not inherit enclosed-turret bulk.
    internal static (double Width,double Length,double Height) GunSize(double caliber,int barrels,double beam,ShipGunAppearance appearance)
    {
        var d=Positive(caliber,.1)*.0254;var extra=Math.Max(0,barrels-1);
        var width=appearance switch {
            ShipGunAppearance.Turret=>1.5+d*(14+7*extra),
            ShipGunAppearance.Casemate=>.8+d*(9+4*extra),
            ShipGunAppearance.Shield=>.7+d*(10+4*extra),
            _=>.55+d*(7+4*extra)
        };
        width=Math.Clamp(width,.65,Math.Max(.65,beam*.75));
        var length=appearance==ShipGunAppearance.Turret?width*1.05:width*.9;
        var height=appearance==ShipGunAppearance.Turret?Math.Clamp(.7+4*d,1,2.4):Math.Clamp(.55+3*d,.65,1.7);
        return (width,length,height);
    }

    public static (double X, double Y)[] HousingOutline(ShipGunHousingShape shape) => shape == ShipGunHousingShape.Circular
        ? Enumerable.Range(0,48).Select(i => (Math.Cos(i*Math.PI/24)*.5, Math.Sin(i*Math.PI/24)*.5)).ToArray()
        : new (double,double)[] {(-.5,-.25),(-.32,-.5),(.32,-.5),(.5,-.25),(.5,.28),(.32,.5),(-.32,.5),(-.5,.28)};

    // Clip only invisible gunhouse internals. Nominal length, muzzle and taper stations stay unchanged.
    public static double VisibleBarrelStart(ShipGraphicPart part,double mountLength) =>
        part.Appearance switch {
            ShipGunAppearance.Casemate => Math.Max(0,part.BarrelStart),
            ShipGunAppearance.Turret => Math.Max(part.BarrelStart,-mountLength*.30+Math.Clamp(part.Caliber*2,.08,.65)),
            _ => part.BarrelStart
        };

    static Dictionary<(int,int),double> PlaceGuns(IEnumerable<GunPlacement> source,double length,double beam,List<string> warnings)
    {
        var guns=source.ToArray();var positions=new Dictionary<(int,int),double>();
        if(guns.Length==0)return positions;
        var location=guns[0].Record.mountLocation;
        var (_,center)=Position(location,0,1);
        var radius=center==0?.22:.095;
        var occupied=new List<(double Y,double Half)>();
        double Half(GunPlacement g)
        {
            var size=GunSize(g.Caliber,g.Record.barrels,beam,g.Override?.appearance??DefaultAppearance(g.Caliber,g.Record));
            var w=Positive(g.Override?.width??0,size.Width/beam)*beam;
            var l=Positive(g.Override?.length??0,size.Length/length)*length;
            var appearance=g.Override?.appearance??DefaultAppearance(g.Caliber,g.Record);
            var baseDiameter=Positive(g.Override?.barbetteDiameter??0,appearance==ShipGunAppearance.Turret?w*1.08:0);
            return Math.Max(baseDiameter,Math.Max(w,l))/length/2;
        }
        // Authored mounts reserve space before automatic mounts are assigned.
        foreach(var g in guns.Where(g=>g.Override!=null))
        {
            var y=Finite(g.Override.y);positions[(g.Battery,g.RecordIndex)]=y;
            occupied.Add((y,Half(g)));
        }
        var crowded=false;
        foreach(var family in guns.Where(g=>g.Override==null)
            .GroupBy(g=>(Caliber:Positive(g.Caliber,.1),g.Record.barrels,g.Record.casemate))
            .OrderByDescending(g=>g.Key.Caliber).ThenByDescending(g=>g.Key.barrels).ThenBy(g=>g.Key.casemate))
        {
            var members=family.OrderBy(g=>DefaultAngle(g.Record)).ToArray();
            var half=members.Max(Half);
            var clearance=Math.Max(.005,1/length);
            var available=Math.Max(0,radius-half);
            var pitch=Math.Max(.055,half*2+clearance);
            for(var i=0;i<members.Length;i++)
            {
                var preferred=center+Math.Clamp(((members.Length-1)*.5-i)*pitch,-available,available);
                double Overlap(double y)=>occupied.Sum(o=>Math.Max(0,half+o.Half+clearance-Math.Abs(y-o.Y)));
                // Bounded deterministic search: pack near the preferred station, expanding only as needed.
                var candidates=Enumerable.Range(0,401).Select(n=>center-available+available*2*n/400).Append(preferred);
                var y=candidates.OrderBy(v=>Math.Round(Overlap(v),8)).ThenBy(v=>Math.Abs(v-preferred)).ThenByDescending(v=>v).First();
                if(Overlap(y)>1e-8)crowded=true;
                var g=members[i];positions[(g.Battery,g.RecordIndex)]=y;
                occupied.Add((y,Half(g)));
            }
        }
        if(crowded)warnings.Add("Crowded automatic mount layout: "+location);
        return positions;
    }

    static (double X,double Y) Position(PlaceholderMountLocation location,int index,int count)
    {
        var name=location.ToString(); var side=name.StartsWith("Port")?-1:name.StartsWith("Starboard")?1:0;
        var y=name.Contains("Forward")?.32:name.Contains("After")?-.34:0;
        var span=y==0?.44:.14;
        if(count>1) y+=(.5-index/(double)(count-1))*span;
        return (side*.43,y);
    }
    public static double DefaultAngle(PlaceholderMountRenderRecord record)
    {
        var name=record.mountLocation.ToString();
        var angle=name.StartsWith("Port")?270:name.StartsWith("Starboard")?90:name.Contains("After")?180:0;
        return Constrain(record,angle);
    }
    public static double Normalize(double a) => (a%360+360)%360;
    public static double Constrain(PlaceholderMountRenderRecord record,double angle)
    {
        angle=Normalize(angle);
        var hasArc=false;var nearest=angle;var best=double.PositiveInfinity;
        foreach(var arc in record.mountArcs)
        {
            if(!double.IsFinite(arc.startDeg)||!double.IsFinite(arc.CoverageDeg)||arc.CoverageDeg<=0) continue;
            hasArc=true;
            if(arc.CoverageDeg>=360 || Normalize(angle-arc.startDeg)<=arc.CoverageDeg) return angle;
            var start=Normalize(arc.startDeg);var end=Normalize(arc.startDeg+arc.CoverageDeg);
            var distance=Math.Abs(Normalize(start-angle+180)-180);
            if(distance<best) {best=distance;nearest=start;}
            distance=Math.Abs(Normalize(end-angle+180)-180);
            if(distance<best) {best=distance;nearest=end;}
        }
        return hasArc?nearest:angle;
    }
    public static double ResolveDirection(ShipGraphicPart part,PlaceholderMountRenderRecord record,double targetAngle)
        => part.Kind!=ShipGraphicPartKind.Gun && (!record.trainable || !double.IsFinite(targetAngle))
            ? part.RestAngle : Constrain(record,double.IsFinite(targetAngle)?targetAngle:part.RestAngle);

    public static double Direction(ShipGraphicPart part,ShipClassPlaceholderRenderInput input)
    {
        PlaceholderMountRenderRecord record;
        if(part.Kind==ShipGraphicPartKind.Gun) record=input.batteryRecords[part.BatteryIndex].mountLocationRecords[part.RecordIndex];
        else if(part.Kind is ShipGraphicPartKind.DeckTorpedo or ShipGraphicPartKind.SubmergedTorpedo) record=input.torpedoMountRecords[part.RecordIndex];
        else return part.RestAngle;
        return ResolveDirection(part,record,record.mountDirectionDeg);
    }
}
