using System.Globalization;
using System.Xml.Linq;
using CoreUtils;
using NavalCombat.Application.Persistence;
using NavalCombat.Avalonia.Services;
using NavalCombatCore;
using UnityEngine;
using YYZ;

namespace NavalCombat.Unity;

public sealed class NavalText : ILocalizeService
{
    public static readonly NavalText Instance = new();
    readonly Dictionary<string, string> english = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, string> current = new(StringComparer.OrdinalIgnoreCase);
    public static readonly string[] Cultures = { "en", "ja-JP", "zh-CN", "zh-TW" };
    public string Culture { get; private set; } = "en";
    static void Read(string suffix, Dictionary<string, string> target)
    {
        target.Clear();
        var asset = Resources.Load<TextAsset>("Naval/Strings/NavalCombatStrings" + suffix);
        if (asset == null) return;
        foreach (var node in XDocument.Parse(asset.text).Root.Elements("data"))
            target[(string)node.Attribute("name")] = node.Element("value")?.Value ?? "";
    }
    public void SetLanguage(string culture)
    {
        Culture = Cultures.Contains(culture) ? culture : "en";
        Read("", english);
        Read(Culture == "en" ? "" : "." + Culture, current);
        GlobalString.shortMode = GlobalString.mergeMode = Culture switch
        {
            "ja-JP" => LanguageType.Japanese, "zh-CN" => LanguageType.ChineseSimplified,
            "zh-TW" => LanguageType.ChineseTraditional, _ => LanguageType.English
        };
    }
    public string Text(string key, params object[] args)
    {
        var value = current.GetValueOrDefault(key) ?? english.GetValueOrDefault(key) ?? key;
        return args.Length == 0 ? value : string.Format(CultureInfo.GetCultureInfo(Culture), value, args);
    }
    public string Get(string key, params object[] args)
    {
        var resource = "Core." + key;
        return current.ContainsKey(resource) || english.ContainsKey(resource) ? Text(resource, args)
            : args.Length == 0 ? key : string.Format(key, args);
    }
    public string GetFor(object value) => Get(value?.ToString() ?? "");
    public string GetEnum<T>(T value) => Get(typeof(T).Name + "." + value);
}

public static class NavalHostServices
{
    public static void InitializeLanguage()
    {
        // Resolve the device language on every launch; manual changes are session-only.
        var defaultCulture = UnityEngine.Application.systemLanguage switch
        {
            SystemLanguage.Chinese or SystemLanguage.ChineseSimplified => "zh-CN",
            SystemLanguage.ChineseTraditional => "zh-TW",
            SystemLanguage.Japanese => "ja-JP",
            _ => "en"
        };
        NavalText.Instance.SetLanguage(defaultCulture);
    }
    public static void Configure()
    {
        ServiceLocator.Register<ILocalizeService>(NavalText.Instance, false);
        ServiceLocator.Register<ILoggerService>(new UnityLogger(), false);
        ServiceLocator.Register<IMaskCheckService>(new PlanarShipCollisionService(), false);
        CoreParameter.Instance.checkLandCollision = false;
        CoreParameter.Instance.checkShipCollision = true;
        OperationalRoutePlannerService.Instance.routePlanner = new StraightLineOperationalRoutePlanner();
    }
    sealed class UnityLogger : ILoggerService
    {
        public void Log(string message) => Debug.Log(message);
        public void LogWarning(string message) => Debug.LogWarning(message);
        public void LogError(string message) => Debug.LogError(message);
    }
}
