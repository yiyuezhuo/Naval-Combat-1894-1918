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
        var legend=new VisualElement();legend.AddToClassList("history-legend");
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
            bool fire=value.Bars.Count>0&&value.Bars[0].Segments.Count==2;
            Color32[] colors=fire?new Color32[]{new(50,118,64,255),new(112,112,112,255)}:new Color32[]{new(154,68,30,255),new(142,112,28,255),new(63,88,118,255)};
            var chart=new HistoryStackedBarChart();chart.AddToClassList("history-bars");
            chart.SetBars(value.Bars.Select(b=>new HistoryBarItem{label=b.Label,segments=b.Segments.Select((v,i)=>new HistoryBarSegment{label=v.Label,count=(int)v.Value,color=colors[i%colors.Length]}).ToList()}));Add(chart);
            double total=value.Bars.Sum(b=>b.Segments.Sum(v=>v.Value));
            if(total<=0)legend.Add(new Label(value.EmptyMessage));
            else for(int i=0;i<value.Bars[0].Segments.Count;i++)
            {
                double count=value.Bars.Sum(b=>b.Segments[i].Value);
                Entry(colors[i%colors.Length],$"{value.Bars[0].Segments[i].Label}: {count:0} ({count/total:P1})");
            }
        }
        Add(legend);
    }
}
public sealed class NavalWeaponDiagram : NavalDiagram
{
    readonly WeaponDiagramSnapshot snapshot;
    public NavalWeaponDiagram(WeaponDiagramSnapshot value)
    {
        snapshot=value;AddToClassList("weapon-diagram");generateVisualContent+=Draw;
        var data=new VisualElement();data.AddToClassList("weapon-data");Add(data);data.Add(new Label(value.Caption));
        foreach(var sector in value.MountSectors)data.Add(new Label($"{NavalText.Instance.GetEnum(sector.Location)}: {sector.Mounts} × {sector.Barrels} · "+string.Join(" / ",sector.Arcs.Select(a=>$"{a.StartDeg:0}° + {a.CoverageDeg:0}°"))));
        foreach(var point in value.PenetrationPoints)data.Add(new Label($"{point.DistanceYards:0} yd: {point.VerticalInches:0.##} / {point.HorizontalInches:0.##} in"));
    }
    void Draw(MeshGenerationContext c)
    {
        var p=c.painter2D;var at=new Vector2(110,115);
        foreach(var sector in snapshot.MountSectors)foreach(var arc in sector.Arcs)Wedge(p,at,85,arc.StartDeg,arc.CoverageDeg,new Color(.2f,.4f,.7f,.15f));
        Arrow(p,at,at+Vector2.up*-75,Ink);Caption(c,"0°",new(105,15));
        if(snapshot.PenetrationPoints.Count>0)
        {
            float x0=250,w=Mathf.Max(20,contentRect.width-x0-30),maxY=(float)Math.Max(1,snapshot.PenetrationPoints.Max(v=>Math.Max(v.VerticalInches,v.HorizontalInches)));
            double maxX=Math.Max(1,snapshot.PenetrationPoints.Max(v=>v.DistanceYards));
            Vector2 Point(DossierPenetrationPointSnapshot v,bool vertical)=>new(x0+(float)(v.DistanceYards/maxX)*w,190-(float)(vertical?v.VerticalInches:v.HorizontalInches)/maxY*150);
            Line(p,new(x0,30),new(x0,190),Ink);Line(p,new(x0,190),new(x0+w,190),Ink);
            for(int i=1;i<snapshot.PenetrationPoints.Count;i++){Line(p,Point(snapshot.PenetrationPoints[i-1],true),Point(snapshot.PenetrationPoints[i],true),Blue,2);Line(p,Point(snapshot.PenetrationPoints[i-1],false),Point(snapshot.PenetrationPoints[i],false),Orange,2);}
            Caption(c,"Vertical / Horizontal (in)",new(x0,10));Caption(c,$"0 — {maxX:0} yd",new(x0,200));
        }
    }
}
}
