using System.Globalization;
using NavalCombat.Application;
using UnityEngine;

namespace NavalCombat.Unity
{
public static class NavalScenarioCatalog
{
    [Serializable] sealed class Manifest { public Entry[] entries; }
    [Serializable] sealed class Names { public string english,japanese,chineseSimplified,chineseTraditional; }
    [Serializable] sealed class Entry { public string file,startUtc,recommendation;public bool historical;public Names names; }
    public static List<ScenarioCatalogEntry> Load()
    {
        var asset=Resources.Load<TextAsset>("Naval/ScenarioCatalog");
        if(asset==null)throw new InvalidDataException("Missing manual scenario catalog.");
        var manifest=JsonUtility.FromJson<Manifest>(asset.text);
        if(manifest?.entries==null)throw new InvalidDataException("Invalid manual scenario catalog.");
        var seen=new HashSet<string>(StringComparer.Ordinal);var result=new List<ScenarioCatalogEntry>();
        foreach(var e in manifest.entries)
        {
            if(string.IsNullOrWhiteSpace(e.file)||!e.file.EndsWith(".scen.xml",StringComparison.Ordinal)||e.file.IndexOfAny(new[]{'/', '\\', ':'})>=0||!seen.Add(e.file))throw new InvalidDataException("Invalid/duplicate catalog filename: "+e.file);
            var recommendation=e.recommendation switch {"recommended"=>ScenarioRecommendation.Recommended,"neutral"=>ScenarioRecommendation.Neutral,"discouraged"=>ScenarioRecommendation.Discouraged,_=>throw new InvalidDataException("Invalid recommendation: "+e.file)};
            result.Add(new(e.file,e.file,e.file[..^9]){LocalizedNames=e.names==null?null:new(e.names.english,e.names.japanese,e.names.chineseSimplified,e.names.chineseTraditional),IsListed=true,Historical=e.historical,StartTimeUtc=DateTimeOffset.Parse(e.startUtc,CultureInfo.InvariantCulture,DateTimeStyles.AssumeUniversal|DateTimeStyles.AdjustToUniversal),Recommendation=recommendation});
        }
        return result.OrderBy(e=>e.IsTutorial?0:e.Historical==true?1:2).ThenBy(e=>e.StartTimeUtc).ThenBy(e=>e.FileName,StringComparer.Ordinal).ToList();
    }
}
}
