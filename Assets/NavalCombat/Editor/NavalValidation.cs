using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NavalCombat.Application;
using NavalCombat.Application.Persistence;
using NavalCombat.Unity;
using NavalCombatCore;
using UnityEngine;

namespace NavalCombat.Unity.Editor
{
    public static class NavalValidation
    {
        public static string Smoke()
        {
            if(UnityEditor.EditorApplication.isPlaying)throw new InvalidOperationException("Run core validation outside Play mode.");
            NavalHostServices.Configure();
            var dir=Path.Combine(UnityEngine.Application.streamingAssetsPath,"NavalCombat");
            var reports=new List<string>();
            foreach(var scenario in new[]{"TT - Tutorial 1 - Single Ship.scen.xml","RJH - Battle of Ulsan.scen.xml","WWH - Jutland - The Battle Fleets Engage.scen.xml"})
            {
                var session=new BattleSession(new FileScenarioDataSource(dir));
                var watch=Stopwatch.StartNew();
                session.LoadFromXml(File.ReadAllText(Path.Combine(dir,scenario)),File.ReadAllText(Path.Combine(dir,"Leaders.xml")),File.ReadAllText(Path.Combine(dir,"ShipClasses.xml")),File.ReadAllText(Path.Combine(dir,"NamedShips.xml")));
                session.StartObserverBattle();var start=session.GameState.scenarioState.dateTime;
                session.SetSimulationSpeed(SimulationSpeed.Realtime);
                for(int i=0;i<30;i++)session.AdvanceWallClock(1);
                if(session.Phase==BattlePhase.Faulted)throw new Exception(session.ErrorDetails);
                if(session.GameState.scenarioState.dateTime<=start)throw new Exception("Simulation did not advance: "+scenario);
                if(session.GameState.shipLogs.Any(s=>!double.IsFinite(s.position.LatDeg)||!double.IsFinite(s.position.LonDeg)))throw new Exception("Non-finite ship position");
                session.SetSimulationSpeed(SimulationSpeed.Paused);var paused=session.GameState.scenarioState.dateTime;session.AdvanceWallClock(5);
                if(session.GameState.scenarioState.dateTime!=paused)throw new Exception("Paused session advanced.");
                reports.Add(scenario+": "+session.GameState.shipLogs.Count+" ships, 30 simulation seconds, "+watch.ElapsedMilliseconds+" ms; pause verified");
            }
            Directory.CreateDirectory(".artifacts");var result=string.Join("\n",reports);File.WriteAllText(".artifacts/naval-unity-smoke.txt",result);return result;
        }
    }
}
