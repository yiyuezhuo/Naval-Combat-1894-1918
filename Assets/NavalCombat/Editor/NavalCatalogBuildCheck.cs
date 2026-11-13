using System;
using System.IO;
using System.Linq;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using NavalCombat.Unity;

public sealed class NavalCatalogBuildCheck : IPreprocessBuildWithReport
{
    public int callbackOrder=>0;
    public void OnPreprocessBuild(BuildReport report)=>Check();
    public static string[] Check()
    {
        var known=NavalScenarioCatalog.Load().Select(e=>e.FileName).ToHashSet(StringComparer.Ordinal);
        var directory=Path.Combine(Application.streamingAssetsPath,"NavalCombat");
        var extra=Directory.EnumerateFiles(directory,"*",SearchOption.TopDirectoryOnly).Select(Path.GetFileName)
            .Where(f=>f.EndsWith(".scen.xml",StringComparison.OrdinalIgnoreCase)&&!known.Contains(f)).ToArray();
        foreach(var file in extra)Debug.LogWarning("Naval scenario is not in the manual catalog: "+file);
        foreach(var file in known)if(!File.Exists(Path.Combine(directory,file)))Debug.LogWarning("Naval catalog references a missing scenario: "+file);
        return extra;
    }
}
