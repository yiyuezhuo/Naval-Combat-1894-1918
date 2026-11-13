using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using PointerType = UnityEngine.UIElements.PointerType;

namespace NavalCombat.Unity
{
public sealed partial class NavalGame
{
    sealed class MapTouch
    {
        public Vector2 Start,Position;
        public float Began;
        public bool Moved,Blocked,Fired;
    }
    readonly Dictionary<int,MapTouch> mapTouches=new();
    readonly HashSet<int> activeTouchPointers=new();
    VisualElement mapInput;
    Vector2? mapHover;
    const float LongPressSeconds=.5f,DragThresholdPanelUnits=12,TouchPickRadiusPanelUnits=24;
    Vector2 PanelToScreen(Vector2 point)
    {
        var zero=RuntimePanelUtils.ScreenToPanel(root.panel,Vector2.zero);
        var end=RuntimePanelUtils.ScreenToPanel(root.panel,new Vector2(Screen.width,Screen.height));
        return new Vector2((point.x-zero.x)/(end.x-zero.x)*Screen.width,Screen.height-(point.y-zero.y)/(end.y-zero.y)*Screen.height);
    }
    float TouchPickRadius(Vector2 panel)=>Vector2.Distance(PanelToScreen(panel),PanelToScreen(panel+Vector2.right*TouchPickRadiusPanelUnits));
    void MapSelect(Vector2 panel,bool touch=false)
    {
        var screen=PanelToScreen(panel);var radius=touch?TouchPickRadius(panel):0;
        var ship=World.Pick(screen,radius);
        if(ship==null)return;
        if(World.ViewMode=="Bridge")World.BridgeTargetId=ship.objectId;else Select(ship);
    }
    void MapContext(Vector2 panel,bool touch=false)=>Execute(()=>
    {
        var screen=PanelToScreen(panel);var hit=World.Pick(screen,touch?TouchPickRadius(panel):0);
        if(hit!=null)ShowTacticalMenu(hit,root.WorldToLocal(panel));
        else{CloseContext();if(World.SeaPoint(screen,out var point))TryIssueMapCourse(point.LatDeg,point.LonDeg);}
    });
    void SetupMapInput()
    {
        mapInput=Q<VisualElement>("viewport");
        mapInput.RegisterCallback<PointerLeaveEvent>(_=>mapHover=null);
        root.RegisterCallback<PointerDownEvent>(e=>
        {
            if(e.pointerType!=PointerType.touch)return;
            activeTouchPointers.Add(e.pointerId);
            if(activeTouchPointers.Count>1){foreach(var touch in mapTouches.Values)touch.Blocked=true;}
            root.AddToClassList("touch-input");
            var target=e.target as VisualElement;
            if(modal||target==null||(!mapInput.Contains(target)&&target!=mapInput)||IsHudTarget(target))CancelMapGestures();
        },TrickleDown.TrickleDown);
        root.RegisterCallback<PointerUpEvent>(e=>{activeTouchPointers.Remove(e.pointerId);},TrickleDown.TrickleDown);
        root.RegisterCallback<PointerCancelEvent>(e=>{activeTouchPointers.Remove(e.pointerId);CancelMapGestures();},TrickleDown.TrickleDown);
        mapInput.RegisterCallback<PointerDownEvent>(e=>
        {
            if(modal||busy||CurrentScreen!="battle"||IsHudTarget(e.target as VisualElement)){return;}
            Vector2 point=e.position;
            if(e.pointerType==PointerType.touch)
            {
                // Captured pointers can deliver another down without ending the gesture.
                // Keep its original start time, position and multi-touch suppression.
                if(mapTouches.ContainsKey(e.pointerId))
                {
                    e.StopPropagation();return;
                }
                activeTouchPointers.Add(e.pointerId);
                CloseContext();mapTouches[e.pointerId]=new MapTouch{Start=point,Position=point,Began=Time.unscaledTime};
                if(activeTouchPointers.Count>1)foreach(var t in mapTouches.Values)t.Blocked=true;
                mapInput.CapturePointer(e.pointerId);e.StopPropagation();return;
            }
            if(e.pointerType!=PointerType.mouse)return;
            if(e.button==0)MapSelect(point);
            if(e.button==1){MapContext(point);e.StopPropagation();}
            if(e.button==2||World.ViewMode=="Bridge"&&e.button==0){dragging=true;mapInput.CapturePointer(e.pointerId);}
        });
        mapInput.RegisterCallback<PointerMoveEvent>(e=>
        {
            if(e.pointerType==PointerType.mouse)mapHover=IsHudTarget(e.target as VisualElement)?null:(Vector2)e.position;
            if(modal||busy){CancelMapGestures();return;}
            if(e.pointerType==PointerType.touch)
            {
                if(!mapTouches.TryGetValue(e.pointerId,out var touch))return;
                Vector2 point=e.position,previous=touch.Position;
                if(mapTouches.Count==2)
                {
                    var other=mapTouches.First(p=>p.Key!=e.pointerId).Value;
                    float before=Vector2.Distance(previous,other.Position),after=Vector2.Distance(point,other.Position);
                    touch.Position=point;
                    if(before>10&&after>10)World.Zoom(Mathf.Log(after/before)/.12f);
                    var delta=(point-previous)*.5f;delta.y=-delta.y;
                    if(World.ViewMode=="3D")World.Orbit(delta);
                    else World.Pan(World.ViewMode=="Bridge"?-delta:delta);
                }
                else
                {
                    touch.Position=point;
                    if(touch.Blocked||touch.Fired||mapTouches.Count!=1)return;
                    if(Vector2.Distance(point,touch.Start)>DragThresholdPanelUnits)
                    {
                        touch.Moved=true;
                    }
                    if(touch.Moved){var delta=point-previous;delta.y=-delta.y;World.Pan(World.ViewMode=="Bridge"?-delta:delta);}
                }
                e.StopPropagation();return;
            }
            if(!dragging||e.pointerType!=PointerType.mouse)return;
            var mouseDelta=new Vector2(e.deltaPosition.x,-e.deltaPosition.y);
            if(Keyboard.current?.shiftKey.isPressed==true&&World.ViewMode!="Bridge")World.Orbit(mouseDelta);
            else World.Pan(World.ViewMode=="Bridge"&&(e.pressedButtons&4)!=0?-mouseDelta:mouseDelta);
        });
        mapInput.RegisterCallback<PointerUpEvent>(e=>
        {
            // Captured up events do not always reach the root callback.
            // Remove here too; HashSet.Remove is safe if the root already handled it.
            if(e.pointerType==PointerType.touch)activeTouchPointers.Remove(e.pointerId);
            if(e.pointerType==PointerType.touch&&mapTouches.Remove(e.pointerId,out var touch))
            {
                if(!modal&&!busy&&!touch.Moved&&!touch.Blocked&&!touch.Fired)MapSelect(touch.Position,true);
                foreach(var remaining in mapTouches.Values)remaining.Blocked=true;
                e.StopPropagation();
            }
            else if(e.pointerType==PointerType.mouse)dragging=false;
            mapInput.ReleasePointer(e.pointerId);
        });
        mapInput.RegisterCallback<PointerCancelEvent>(e=>{activeTouchPointers.Remove(e.pointerId);CancelMapGestures();});
        mapInput.RegisterCallback<PointerCaptureOutEvent>(e=>{if(mapTouches.ContainsKey(e.pointerId))CancelMapGestures();else if(e.pointerId==PointerId.mousePointerId)dragging=false;});
        mapInput.RegisterCallback<WheelEvent>(e=>{if(modal||IsHudTarget(e.target as VisualElement))return;World.Zoom(-e.delta.y*.2f);e.StopPropagation();});
    }
    void UpdateMapTouch()
    {
        if(modal||busy||CurrentScreen!="battle"){CancelMapGestures();return;}
        if(mapTouches.Count!=1)return;
        var touch=mapTouches.Values.First();
        if(!touch.Moved&&!touch.Blocked&&!touch.Fired&&Time.unscaledTime-touch.Began>=LongPressSeconds)
        {touch.Fired=true;MapContext(touch.Start,true);}
    }
    void CancelMapGestures()
    {
        dragging=false;
        if(mapInput!=null&&mapInput.HasPointerCapture(PointerId.mousePointerId))mapInput.ReleasePointer(PointerId.mousePointerId);
        if(mapTouches.Count==0)return;
        var ids=mapTouches.Keys.ToArray();mapTouches.Clear();
        foreach(var id in ids){activeTouchPointers.Remove(id);mapInput?.ReleasePointer(id);}
    }
    void OnApplicationFocus(bool focused){if(!focused){CancelMapGestures();activeTouchPointers.Clear();}}
}
}
