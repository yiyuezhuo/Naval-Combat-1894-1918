using System.Collections;
using NavalCombat.Application;
using UnityEngine;
using UnityEngine.UIElements;

namespace NavalCombat.Unity
{
    // Opt-in integration check for the actual Windows player, not normal gameplay.
    public sealed class NavalPlayerSmoke : MonoBehaviour
    {
        readonly List<string> results = new();
        string output;
        bool failed;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Initialize()
        {
            var args=Environment.GetCommandLineArgs();var index=Array.IndexOf(args,"--naval-smoke");
            if(index<0)return;
            var game=UnityEngine.Object.FindAnyObjectByType<NavalGame>();
            if(game==null)return;
            var probe=game.gameObject.AddComponent<NavalPlayerSmoke>();
            probe.output=index+1<args.Length?Path.GetFullPath(args[index+1]):UnityEngine.Application.persistentDataPath;
        }
        void OnEnable()=>UnityEngine.Application.logMessageReceived+=OnLog;
        void OnDisable()=>UnityEngine.Application.logMessageReceived-=OnLog;
        void OnLog(string message,string stack,LogType type)
        {
            if(type is LogType.Error or LogType.Exception or LogType.Assert){failed=true;results.Add(type+": "+message+"\n"+stack);}
        }
        IEnumerator Start()
        {
            UnityEngine.Application.runInBackground=true;
            Directory.CreateDirectory(output);
            var game=GetComponent<NavalGame>();
            var deadline=Time.realtimeSinceStartup+60;
            while(!game.Ready && Time.realtimeSinceStartup<deadline)yield return null;
            if(!game.Ready){Finish("FAIL: archive did not load");yield break;}
            foreach(var id in new[]{"TT - Tutorial 1 - Single Ship.scen.xml","RJH - Battle of Ulsan.scen.xml","WWH - Jutland - The Battle Fleets Engage.scen.xml"})
            {
                var load=game.OpenScenarioAsync(id);
                while(!load.IsCompleted)yield return null;
                if(load.IsFaulted || game.LastError!=null){Finish("FAIL: "+load.Exception+game.LastError);yield break;}
                game.StartParticipation(BattleParticipationMode.Observer);
                game.Session.SetSimulationSpeed(SimulationSpeed.Paused);
                yield return null;
                foreach(var control in new[]{"maneuver","fireDoctrine","searchlight"})
                    if(GetComponent<UIDocument>().rootVisualElement.Q<DropdownField>(control).enabledInHierarchy){Finish("FAIL: observer command enabled: "+control);yield break;}
                if(game.TryIssueMapCourse(0,0)){Finish("FAIL: observer course order accepted");yield break;}
                var start=game.Session.GameState.scenarioState.dateTime;
                game.Session.SetSimulationSpeed(SimulationSpeed.Realtime);
                for(int i=0;i<30;i++)game.Session.AdvanceWallClock(1);
                game.Session.SetSimulationSpeed(SimulationSpeed.Paused);
                if(game.Session.Phase==BattlePhase.Faulted || game.Session.GameState.scenarioState.dateTime<=start){Finish("FAIL: "+game.Session.ErrorDetails);yield break;}
                var frames=0;var before=Time.realtimeSinceStartup;
                while(Time.realtimeSinceStartup-before<3){frames++;yield return null;}
                results.Add(id+": "+game.Session.GameState.shipLogs.Count+" ships; simulation advanced; observer read-only; "+(frames/(Time.realtimeSinceStartup-before)).ToString("0.0")+" rendered fps");
                yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot(Path.Combine(output,game.Session.GameState.shipLogs.Count+"-ships.png"));
                yield return null;
                game.World.SetMode("3D");yield return new WaitForSecondsRealtime(.5f);
                game.World.SetMode("Bridge");yield return new WaitForSecondsRealtime(.5f);
                game.ReturnToArchive();
            }
            Finish(failed?"FAIL: see runtime errors":"PASS");
        }
        void Finish(string status)
        {
            results.Insert(0,status);File.WriteAllLines(Path.Combine(output,"player-smoke.txt"),results);
            UnityEngine.Application.Quit(status=="PASS"?0:1);
        }
    }
}
