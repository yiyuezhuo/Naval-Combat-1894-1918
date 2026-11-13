using NavalCombat.Application;
using NavalCombat.Application.Persistence;
using UnityEngine;
using UnityEngine.Networking;

namespace NavalCombat.Unity
{
public sealed class UnityScenarioSource : FileScenarioDataSource
{
    readonly string root;
    readonly Dictionary<string,string> sharedXml=new();
    public UnityScenarioSource(string directory):base(directory,false)
    {
        root=directory.Replace('\\','/').TrimEnd('/')+"/";
        if(!root.Contains("://"))root=new Uri(Path.GetFullPath(directory)+Path.DirectorySeparatorChar).AbsoluteUri;
    }
    protected override string Language=>NavalText.Instance.Culture;
    protected override string ResolveWithinDataDirectory(string path)
    {
        if(string.IsNullOrWhiteSpace(path))throw new ArgumentException("Empty data reference.");
        var parts=path.Replace('\\','/').Split('/');
        if(parts.Any(p=>p==".."||p=="."||p.Length==0||p.Contains(':')))throw new ArgumentException("Data references must stay within the naval data directory.");
        return root+string.Join("/",parts.Select(Uri.EscapeDataString));
    }
    protected override async Task<string> ReadTextAsync(string address,CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        bool cache=!address.EndsWith(".scen.xml",StringComparison.OrdinalIgnoreCase);
        if(cache&&sharedXml.TryGetValue(address,out var cached))return cached;
        using var request=UnityWebRequest.Get(address);request.timeout=30;
        var operation=request.SendWebRequest();
        while(!operation.isDone)
        {
            if(cancellationToken.IsCancellationRequested){request.Abort();cancellationToken.ThrowIfCancellationRequested();}
            await UnityEngine.Awaitable.NextFrameAsync();
        }
        cancellationToken.ThrowIfCancellationRequested();
        if(request.result!=UnityWebRequest.Result.Success)throw new IOException($"Cannot load {address}: {request.error}");
        var xml=request.downloadHandler.text;if(cache)sharedXml[address]=xml;return xml;
    }
    public override Task<IReadOnlyList<ScenarioCatalogEntry>> ScanScenariosAsync(CancellationToken cancellationToken=default)
    {
        cancellationToken.ThrowIfCancellationRequested();sharedXml.Clear();
        var entries=NavalScenarioCatalog.Load();
#if UNITY_EDITOR || UNITY_STANDALONE
        var directory=Path.Combine(UnityEngine.Application.streamingAssetsPath,"NavalCombat");
        var known=entries.Select(e=>e.FileName).ToHashSet(StringComparer.Ordinal);
        var extra=Directory.EnumerateFiles(directory,"*",SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName).Where(f=>f.EndsWith(".scen.xml",StringComparison.OrdinalIgnoreCase)&&!known.Contains(f))
            .Select(f=>new ScenarioCatalogEntry(f,f,f[..^9]){Recommendation=f.StartsWith("TT - ",StringComparison.Ordinal)?ScenarioRecommendation.Discouraged:ScenarioRecommendation.Neutral}).ToList();
        entries.InsertRange(0,extra);
#endif
        return Task.FromResult<IReadOnlyList<ScenarioCatalogEntry>>(entries);
    }
}
}
