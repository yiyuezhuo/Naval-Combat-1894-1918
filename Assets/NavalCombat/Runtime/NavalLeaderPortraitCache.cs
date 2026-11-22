using System.Collections;
using System.IO;
using CoreUtils;
using UnityEngine;
using UnityEngine.Networking;

namespace NavalCombat.Unity
{
public sealed class NavalLeaderPortraitCache : MonoBehaviour
{
    public sealed class Entry
    {
        public event Action Loaded;
        public void Complete() => Loaded?.Invoke();
        public Texture2D Texture;
    }
    readonly Dictionary<string,Entry> entries=new();
    readonly HashSet<UnityWebRequest> requests=new();

    public Entry Get(PictureReference reference)
    {
        string path=reference.path.Replace('\\','/');
        string key=$"{reference.isBuiltin}|{path}";
        if(entries.TryGetValue(key,out var entry))return entry;
        entry=new Entry();entries.Add(key,entry);
        StartCoroutine(Load(path,reference.isBuiltin,entry));
        return entry;
    }

    IEnumerator Load(string path,bool builtin,Entry entry)
    {
        string uri;
        try
        {
            string root = UnityEngine.Application.streamingAssetsPath;
            if (builtin && root.Contains("://"))
                uri = root.TrimEnd('/') + "/NavalCombat/" + string.Join("/", path.Split('/').Select(Uri.EscapeDataString));
            else
            {
                if (builtin) path = Path.Combine(root, "NavalCombat", path);
                uri = path.Contains("://") ? path : new Uri(Path.GetFullPath(path)).AbsoluteUri;
            }
        }
        catch(ArgumentException){entry.Complete();yield break;}
        using(var request=UnityWebRequestTexture.GetTexture(uri, nonReadable: true))
        {
            requests.Add(request);
            yield return request.SendWebRequest();
            if(request.result==UnityWebRequest.Result.Success)entry.Texture=DownloadHandlerTexture.GetContent(request);
            entry.Complete();
            requests.Remove(request);
        }
    }

    void OnDestroy()
    {
        foreach(var request in requests){request.Abort();request.Dispose();}
        requests.Clear();
        foreach(var entry in entries.Values)if(entry.Texture!=null)Destroy(entry.Texture);
        entries.Clear();
    }
}
}
