using NavalCombat.Avalonia.Resources;
using NavalCombat.Avalonia.ViewModels;
using UnityEngine;
using UnityEngine.UIElements;

namespace NavalCombat.Unity
{
public abstract class NavalDiagram : VisualElement
{
    protected static readonly Color Ink=new(.12f,.16f,.2f),Blue=new(.1f,.3f,.65f),Orange=new(.8f,.35f,.08f);
    protected static Vector2 Direction(double angle)=>new((float)Math.Sin(angle*Math.PI/180),-(float)Math.Cos(angle*Math.PI/180));
    protected static void Line(Painter2D p,Vector2 a,Vector2 b,Color color,float width=1)
    {p.strokeColor=color;p.lineWidth=width;p.BeginPath();p.MoveTo(a);p.LineTo(b);p.Stroke();}
    protected static void Arrow(Painter2D p,Vector2 a,Vector2 b,Color color)
    {Line(p,a,b,color,2);var d=(b-a).normalized;var n=new Vector2(-d.y,d.x);Line(p,b,b-d*9+n*4,color,2);Line(p,b,b-d*9-n*4,color,2);}
    protected void Caption(MeshGenerationContext c,string text,Vector2 pos,Color? color=null)=>c.DrawText(text,pos,12,color??Ink,resolvedStyle.unityFontDefinition.fontAsset);
    protected static void Dot(Painter2D p,Vector2 at,Color color,float radius=4)
    {p.fillColor=color;p.BeginPath();p.Arc(at,radius,0,360);p.Fill();}
    protected static void Wedge(Painter2D p,Vector2 at,float radius,double start,double coverage,Color color)
    {p.fillColor=color;p.BeginPath();p.MoveTo(at);int n=Math.Max(2,(int)Math.Ceiling(coverage/5));for(int i=0;i<=n;i++)p.LineTo(at+Direction(start+coverage*i/n)*radius);p.ClosePath();p.Fill();}
}
public sealed class NavalTorpedoDiagram : NavalDiagram
{
    TorpedoTargetSnapshot target;TorpedoMountOption option;double heading;float zoom=1;Vector2 pan;bool dragging;
    public NavalTorpedoDiagram()
    {
        AddToClassList("torpedo-diagram");generateVisualContent+=Draw;
        RegisterCallback<WheelEvent>(e=>{Zoom(e.delta.y<0?1.15f:1/1.15f);e.StopPropagation();});
        RegisterCallback<PointerDownEvent>(e=>{if(e.button==0){dragging=true;this.CapturePointer(e.pointerId);e.StopPropagation();}});
        RegisterCallback<PointerMoveEvent>(e=>{if(dragging){pan+=new Vector2(e.deltaPosition.x,e.deltaPosition.y);MarkDirtyRepaint();}});
        RegisterCallback<PointerUpEvent>(e=>{dragging=false;this.ReleasePointer(e.pointerId);});
        RegisterCallback<PointerCaptureOutEvent>(_=>dragging=false);
    }
    public void SetSelection(double heading,TorpedoTargetSnapshot target,TorpedoMountOption option){this.heading=heading;this.target=target;this.option=option;ResetView();}
    public void Zoom(float value){zoom=Mathf.Clamp(zoom*value,.25f,8);MarkDirtyRepaint();}
    public void ResetView(){zoom=1;pan=Vector2.zero;MarkDirtyRepaint();}
    void Draw(MeshGenerationContext c)
    {
        if(target==null||option==null||contentRect.width<1)return;
        var p=c.painter2D;var e=option.Evaluation;
        var current=new Vector2((float)target.X,-(float)target.Y);var intercept=Direction(e.Azimuth)*(float)e.InterceptionYards;
        var min=Vector2.Min(Vector2.zero,current);var max=Vector2.Max(Vector2.zero,current);
        if(e.HasSolution){min=Vector2.Min(min,intercept);max=Vector2.Max(max,intercept);}
        var center=(min+max)/2;float scale=Mathf.Max(.001f,Mathf.Min(Mathf.Max(1,contentRect.width-160)/Mathf.Max(200,max.x-min.x),Mathf.Max(1,contentRect.height-150)/Mathf.Max(200,max.y-min.y)))*zoom;
        Vector2 Project(Vector2 v)=>contentRect.center+(v-center)*scale+pan;
        var a=Project(Vector2.zero);var b=Project(current);var hit=Project(intercept);
        foreach(var arc in option.Arcs)Wedge(p,a,60,heading+arc.Start,arc.Coverage,new Color(.2f,.4f,.8f,.15f));
        Line(p,a,b,Color.gray);Caption(c,$"{e.CurrentDistanceYards:0} yd",(a+b)/2);
        if(e.HasSolution)
        {
            Arrow(p,a,hit,Blue);Arrow(p,b,hit,Orange);Line(p,hit-Vector2.one*5,hit+Vector2.one*5,Blue);Line(p,hit+new Vector2(-5,5),hit+new Vector2(5,-5),Blue);
            Caption(c,$"{e.InterceptionYards:0} yd / {e.ArrivalSeconds:0} s",(a+hit)/2+new Vector2(8,12),Blue);
            Caption(c,NavalCombatStrings.Text("Ui.TorpedoAnalysis.Intercept"),hit+Vector2.one*8);
        }
        Dot(p,a,Ink);Dot(p,b,Color.red);Arrow(p,a,a+Direction(heading)*25,Ink);Arrow(p,b,b+Direction(target.Heading)*25,Color.red);
        Caption(c,NavalCombatStrings.Text("Ui.TorpedoAnalysis.Shooter"),a+new Vector2(8,-18));Caption(c,target.Name,b+new Vector2(8,-18));
        Caption(c,"N ↑",new Vector2(10,10));
        Caption(c,NavalCombatStrings.Text("Ui.TorpedoAnalysis.SightLine"),new Vector2(10,contentRect.height-55),Color.gray);
        Caption(c,NavalCombatStrings.Text("Ui.TorpedoAnalysis.TorpedoLine"),new Vector2(10,contentRect.height-38),Blue);
        Caption(c,NavalCombatStrings.Text("Ui.TorpedoAnalysis.TargetLine"),new Vector2(10,contentRect.height-21),Orange);
    }
}
public sealed class NavalHistoryChart : VisualElement
{
    static readonly Color32[] palette={new(51,102,153,255),new(191,87,0,255),new(46,125,50,255),new(123,31,162,255),new(194,24,91,255),new(0,121,107,255),new(97,97,97,255),new(255,179,0,255)};
    public NavalHistoryChart(HistoryChartSnapshot value)
    {
        AddToClassList("history-card");
        bool fire=value.Bars.Count>0&&value.Bars[0].Segments.Count==2;
        Color32[] barColors=fire?new Color32[]{new(50,118,64,255),new(112,112,112,255)}:new Color32[]{new(154,68,30,255),new(142,112,28,255),new(63,88,118,255)};
        var legend=new VisualElement();legend.AddToClassList("history-legend");
        VisualElement barColumn=null;
        HistoryStackedBarChart barChart=null;
        void Entry(Color color,string text)
        {
            var row=new VisualElement();row.AddToClassList("history-legend-row");
            var swatch=new VisualElement();swatch.AddToClassList("history-swatch");swatch.style.backgroundColor=color;row.Add(swatch);
            var label=new Label(text);label.style.whiteSpace=WhiteSpace.Normal;label.style.flexShrink=1;row.Add(label);legend.Add(row);
        }
        if(!value.IsBarChart)
        {
            var chart=new HistoryPieChart();chart.style.flexGrow=0;chart.AddToClassList("history-pie");
            chart.SetSlices(value.Slices.Select((v,i)=>new HistoryPieSlice{label=v.Label,value=(float)v.Value,color=palette[i%palette.Length]}));Add(chart);
            double total=value.Slices.Sum(v=>v.Value);
            if(total<=0)legend.Add(new Label(value.EmptyMessage));
            for(int i=0;i<value.Slices.Count;i++)
            {
                var v=value.Slices[i];string text=$"{v.Label}: {v.Value:0.##} ({v.Value/total:P1})";
                if(value.Detailed)
                {
                    double avg=v.Count>0?v.Value/v.Count:0;
                    text=NavalText.Instance.Get("{0}: {1} DP ({2}), {3} hits, avg {4} DP",v.Label,v.Value.ToString("0.##"),(v.Value/total).ToString("P1"),v.Count,avg.ToString("0.##"));
                    if(v.Samples?.Count>=2)
                    {
                        double std=Math.Sqrt(v.Samples.Sum(x=>(x-avg)*(x-avg))/v.Samples.Count);
                        text=NavalText.Instance.Get("{0}: {1} DP ({2}), {3} hits, avg {4} DP, std dev {5} DP",v.Label,v.Value.ToString("0.##"),(v.Value/total).ToString("P1"),v.Count,avg.ToString("0.##"),std.ToString("0.##"));
                    }
                }
                Entry(palette[i%palette.Length],text);
            }
        }
        else
        {
            legend.AddToClassList("history-bar-legend");
            var colors=barColors;
            var chart=new HistoryStackedBarChart();chart.AddToClassList("history-bars");
            chart.SetBars(value.Bars.Select(b=>new HistoryBarItem{label=b.Label,segments=b.Segments.Select((v,i)=>new HistoryBarSegment{label=v.Label,count=(int)v.Value,color=colors[i%colors.Length]}).ToList()}));
            barChart=chart;
            barColumn=new VisualElement();barColumn.AddToClassList("history-bar-column");
            barColumn.Add(chart);Add(barColumn);
            double total=value.Bars.Sum(b=>b.Segments.Sum(v=>v.Value));
            if(total<=0)legend.Add(new Label(value.EmptyMessage));
            else for(int i=0;i<value.Bars[0].Segments.Count;i++)
            {
                double count=value.Bars.Sum(b=>b.Segments[i].Value);
                Entry(colors[i%colors.Length],$"{value.Bars[0].Segments[i].Label}: {count:0} ({count/total:P1})");
            }
        }
        Add(legend);
        if(value.IsBarChart)
        {
            var colors=barColors;
            var details=new VisualElement();details.AddToClassList("history-bar-details");
            foreach(var bar in value.Bars)
            {
                var column=new VisualElement();column.AddToClassList("history-bar-detail");
                var title=new Label(bar.Label);title.AddToClassList("history-bar-detail-title");column.Add(title);
                double total=bar.Segments.Sum(s=>s.Value);
                for(int i=0;i<bar.Segments.Count;i++)
                {
                    var segment=bar.Segments[i];
                    var text=$"{segment.Label}: {segment.Value:0} ({(total>0?segment.Value/total:0):P0})";
                    var row=new VisualElement();row.AddToClassList("history-legend-row");
                    var swatch=new VisualElement();swatch.AddToClassList("history-swatch");swatch.style.backgroundColor=(Color)colors[i%colors.Length];row.Add(swatch);
                    var label=new Label(text){tooltip=text};label.style.whiteSpace=WhiteSpace.Normal;label.style.flexShrink=1;row.Add(label);
                    column.Add(row);
                }
                details.Add(column);
                column.RegisterCallback<GeometryChangedEvent>(_=>
                    details.style.height=details.Children().Max(c=>c.layout.height)+8f);
            }
            barColumn.Add(details);
            void AlignDetails()
            {
                var rects=barChart.GetColumnRects();
                for(int i=0;i<details.childCount&&i<rects.Count;i++)
                {
                    var column=details[i];
                    column.style.width=rects[i].width;
                    column.style.left=rects[i].x;
                }
            }
            barChart.RegisterCallback<GeometryChangedEvent>(_=>AlignDetails());
            AlignDetails();
        }
    }
}
public sealed class NavalWeaponDiagram : VisualElement
{
    public NavalWeaponDiagram(WeaponDiagramSnapshot value)
    {
        AddToClassList("weapon-diagram");
        Add(new WeaponPlot(value,false));
        if(value.PenetrationPoints.Count>0)Add(new WeaponPlot(value,true));
    }

    sealed class WeaponPlot : NavalDiagram
    {
        readonly WeaponDiagramSnapshot snapshot;
        readonly bool penetration;
        static readonly Color Grid=new(.85f,.85f,.85f),Fill=new(.86f,.86f,.86f);
        public WeaponPlot(WeaponDiagramSnapshot value,bool penetration)
        {
            snapshot=value;this.penetration=penetration;
            AddToClassList(penetration?"weapon-penetration":"weapon-sectors");
            generateVisualContent+=Draw;
        }
        void TextAt(MeshGenerationContext c,string text,Vector2 at,float size=12,Color? color=null)
            =>c.DrawText(text,at,size,color??Color.black,resolvedStyle.unityFontDefinition.fontAsset);
        static string T(string key,params object[] args)=>NavalCombatStrings.Text("Ui.Shell."+key,args);
        static void Dashed(Painter2D p,Vector2 a,Vector2 b,Color color,float dash=5,float width=1)
        {
            float length=Vector2.Distance(a,b);if(length<=0)return;
            var direction=(b-a)/length;
            for(float offset=0;offset<length;offset+=dash*1.6f)
                Line(p,a+direction*offset,a+direction*Mathf.Min(length,offset+dash),color,width);
        }
        void Draw(MeshGenerationContext c)
        {
            if(contentRect.width<100||contentRect.height<100)return;
            if(penetration)DrawPenetration(c);else DrawSectors(c);
        }
        void DrawSectors(MeshGenerationContext c)
        {
            var p=c.painter2D;
            TextAt(c,snapshot.Caption,new(6,4),14);
            float cw=(contentRect.width-12)/3,ch=(contentRect.height-32)/3;
            foreach(var sector in snapshot.MountSectors)
            {
                var (row,column)=GridPosition(sector.Location);
                var rect=new Rect(6+column*cw+4,28+row*ch+4,cw-8,ch-8);
                Line(p,rect.min,new(rect.xMax,rect.yMin),Grid);Line(p,new(rect.xMax,rect.yMin),rect.max,Grid);
                Line(p,rect.max,new(rect.xMin,rect.yMax),Grid);Line(p,new(rect.xMin,rect.yMax),rect.min,Grid);
                float radius=Mathf.Max(8,Mathf.Min(rect.width,rect.height-18)*.33f);
                var center=new Vector2(rect.center.x,rect.y+radius+9);
                foreach(var arc in sector.Arcs)
                {
                    if(arc.CoverageDeg<=0)continue;
                    Wedge(p,center,radius,arc.StartDeg,arc.CoverageDeg,Fill);
                    var first=center+Direction(arc.StartDeg)*radius;
                    var last=center+Direction(arc.StartDeg+arc.CoverageDeg)*radius;
                    if(arc.IsCrossDeckFire){Dashed(p,center,first,Color.black,2);Dashed(p,center,last,Color.black,2);}
                    else{Line(p,center,first,Color.black);Line(p,center,last,Color.black);}
                }
                p.strokeColor=Color.black;p.lineWidth=1;p.BeginPath();p.Arc(center,radius,0,360);p.Stroke();
                Line(p,center,center+Vector2.down*(radius+4),Color.black);
                if(sector.Barrels>0)TextAt(c,$"{sector.Mounts}× / {sector.Barrels} {snapshot.MountUnitLabel}",new(rect.x+3,rect.yMax-17),12);
            }
        }
        void DrawPenetration(MeshGenerationContext c)
        {
            var p=c.painter2D;var points=snapshot.PenetrationPoints;
            TextAt(c,T("PenetrationByRange"),new(42,4),14);
            var bounds=new Rect(42,70,contentRect.width-64,contentRect.height-112);
            double maxX=Math.Max(1,Math.Max(snapshot.MaximumRangeYards,points.Max(v=>v.DistanceYards)));
            double rawMax=Math.Max(1,Math.Max(snapshot.MainBeltEffectiveInches,points.Max(v=>Math.Max(v.VerticalInches,v.HorizontalInches))))*1.1;
            double magnitude=Math.Pow(10,Math.Floor(Math.Log10(rawMax))),normalized=rawMax/magnitude;
            double maxY=(normalized<=1?1:normalized<=2?2:normalized<=5?5:10)*magnitude;
            Vector2 Plot(double x,double y)=>new(bounds.x+(float)(x/maxX)*bounds.width,bounds.yMax-(float)(y/maxY)*bounds.height);
            for(int i=0;i<=5;i++)
            {
                float fraction=i/5f,x=bounds.x+bounds.width*fraction,y=bounds.yMax-bounds.height*fraction;
                if(i>0){Line(p,new(x,bounds.y),new(x,bounds.yMax),Grid);Line(p,new(bounds.x,y),new(bounds.xMax,y),Grid);}
                Line(p,new(x,bounds.yMax),new(x,bounds.yMax+4),Color.black);
                Line(p,new(bounds.x-4,y),new(bounds.x,y),Color.black);
                double distance=maxX*fraction;
                TextAt(c,distance>=1000?$"{distance/1000:0.#}k":$"{distance:0}",new(x-9,bounds.yMax+7),12);
                if(i>0)TextAt(c,$"{maxY*fraction:0.#}",new(5,y-7),12);
            }
            Line(p,new(bounds.x,bounds.y),new(bounds.x,bounds.yMax),Color.black);
            Line(p,new(bounds.x,bounds.yMax),bounds.max,Color.black);
            for(int i=1;i<points.Count;i++)
            {
                var a=points[i-1];var b=points[i];
                Line(p,Plot(a.DistanceYards,a.VerticalInches),Plot(b.DistanceYards,b.VerticalInches),Color.black,2);
                Dashed(p,Plot(a.DistanceYards,a.HorizontalInches),Plot(b.DistanceYards,b.HorizontalInches),Color.gray,6,1.5f);
            }
            if(snapshot.MainBeltEffectiveInches>0)
            {
                var a=Plot(0,snapshot.MainBeltEffectiveInches);var b=Plot(maxX,snapshot.MainBeltEffectiveInches);
                Dashed(p,a,b,Color.black,2);
                TextAt(c,T("MainBeltEffValueIn",snapshot.MainBeltEffectiveInches),a+new Vector2(4,-17),12);
            }
            TextAt(c,T("PenetrationIn"),new(bounds.x,23),12);
            Line(p,new(bounds.x,51),new(bounds.x+24,51),Color.black,2);
            TextAt(c,T("VerticalPenetration"),new(bounds.x+30,43),12);
            float legendX=bounds.x+bounds.width*.52f;
            Dashed(p,new(legendX,51),new(legendX+24,51),Color.gray);
            TextAt(c,T("HorizontalPenetration"),new(legendX+30,43),12,Color.gray);
            TextAt(c,T("DistanceYd"),new(bounds.center.x-35,bounds.yMax+25),12);
        }
        static (int Row,int Column) GridPosition(NavalCombatCore.MountLocation location)=>location switch
        {
            NavalCombatCore.MountLocation.PortForward=>(0,0),
            NavalCombatCore.MountLocation.Forward=>(0,1),
            NavalCombatCore.MountLocation.StarboardForward=>(0,2),
            NavalCombatCore.MountLocation.PortMidship=>(1,0),
            NavalCombatCore.MountLocation.Midship=>(1,1),
            NavalCombatCore.MountLocation.StarboardMidship=>(1,2),
            NavalCombatCore.MountLocation.PortAfter=>(2,0),
            NavalCombatCore.MountLocation.After=>(2,1),
            NavalCombatCore.MountLocation.StarboardAfter=>(2,2),
            _=>(1,1)
        };
    }
}
}
