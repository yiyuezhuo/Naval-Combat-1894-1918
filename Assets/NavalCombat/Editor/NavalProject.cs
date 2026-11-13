using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.TextCore.Text;
using NavalCombat.Unity;

namespace NavalCombat.Unity.Editor
{
    public static class NavalProject
    {
        public const string ScenePath = "Assets/NavalCombat/Scenes/NavalCombat.unity";
        [MenuItem("Naval Combat/Open Battle Scene")]
        public static void Open()
        {
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            if(!File.Exists(ScenePath))Create();
            else EditorSceneManager.OpenScene(ScenePath);
        }
        public static void Create()
        {
            if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Save the current scene before creating the naval scene.");
            Directory.CreateDirectory("Assets/NavalCombat/Scenes");
            Directory.CreateDirectory("Assets/NavalCombat/Settings");
            AssetDatabase.Refresh();
            var settings=AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/NavalCombat/Settings/NavalPanel.asset");
            if(settings==null)
            {
                settings=ScriptableObject.CreateInstance<PanelSettings>();
                settings.scaleMode=PanelScaleMode.ScaleWithScreenSize;
                settings.referenceResolution=new Vector2Int(1600,900);
                settings.screenMatchMode=PanelScreenMatchMode.MatchWidthOrHeight;settings.match=.5f;
                settings.themeStyleSheet=AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>("Assets/NavalCombat/Runtime/Resources/Naval/NavalTheme.tss");
                AssetDatabase.CreateAsset(settings,"Assets/NavalCombat/Settings/NavalPanel.asset");
            }
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var host=new GameObject("Naval Combat",typeof(UIDocument),typeof(NavalGame));
            var doc=host.GetComponent<UIDocument>();
            doc.panelSettings=AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/NavalCombat/Settings/NavalPanel.asset");
            doc.visualTreeAsset=AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/NavalCombat/Runtime/Resources/Naval/NavalScreen.uxml");
            EditorSceneManager.SaveScene(scene,ScenePath);
            AssetDatabase.SaveAssets();
        }
        [MenuItem("Naval Combat/Build WebGL")]
        public static void BuildWebGL()
        {
            Directory.CreateDirectory(".artifacts");
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes=new[]{ScenePath},locationPathName="Builds/NavalCombatWebGL",
                target=BuildTarget.WebGL,options=BuildOptions.None
            });
            File.WriteAllText(".artifacts/naval-webgl-build-result.txt",report.summary.result+"\n"+report.summary.totalErrors+" errors\n"+report.summary.totalSize+" bytes\nCompression: "+PlayerSettings.WebGL.compressionFormat);
            if(report.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new Exception("WebGL build failed: "+report.summary.result);
        }
        [MenuItem("Naval Combat/Build Windows")]
        public static void BuildWindows()
        {
            if(!File.Exists(ScenePath))throw new FileNotFoundException(ScenePath);
            Directory.CreateDirectory("Builds/NavalCombat");
            Directory.CreateDirectory(".artifacts");
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes=new[]{ScenePath},locationPathName="Builds/NavalCombat/NavalCombat.exe",
                target=BuildTarget.StandaloneWindows64,options=BuildOptions.None
            });
            File.WriteAllText(".artifacts/naval-build-result.txt",report.summary.result+"\n"+report.summary.totalErrors+" errors\n"+report.summary.totalSize+" bytes");
            if(report.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new Exception("Windows build failed: "+report.summary.result);
        }
    }
}
