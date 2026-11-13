namespace NavalCombat.Application;

public sealed record ScenarioXmlBundle(
    string ScenarioXml,
    string LeadersXml,
    string ShipClassesXml,
    string NamedShipsXml);

public enum ScenarioRecommendation { Neutral, Recommended, Discouraged }

public sealed record ScenarioLocalizedNames(string English, string Japanese, string ChineseSimplified, string ChineseTraditional);

public sealed record ScenarioCatalogEntry(
    string Id,
    string FileName,
    string DisplayName)
{
    public ScenarioLocalizedNames LocalizedNames { get; init; }
    public string GetDisplayName(string culture)
    {
        var name = culture switch
        {
            "ja-JP" => LocalizedNames?.Japanese,
            "zh-CN" => LocalizedNames?.ChineseSimplified,
            "zh-TW" => LocalizedNames?.ChineseTraditional,
            _ => LocalizedNames?.English
        };
        return string.IsNullOrWhiteSpace(name) ? DisplayName : name;
    }
    public bool IsListed { get; init; }
    public bool? Historical { get; init; }
    public DateTimeOffset? StartTimeUtc { get; init; }
    public ScenarioRecommendation Recommendation { get; init; }
    public string Prefix => FileName.Split(" - ", 2, StringSplitOptions.None)[0].ToUpperInvariant();
    public bool IsTutorial => Prefix == "TT";
    public string WarCode => Prefix.Length == 3 && Prefix[..2] is "SJ" or "RJ" or "BR" or "WW" ? Prefix[..2] : "Other";
    public string HistoryCode => Historical.HasValue ? (Historical.Value ? "H" : "S") : Prefix.Length==3 && Prefix[2] is 'H' or 'S' ? Prefix[2].ToString() : "Other";
}

public sealed record ScenarioSidePreview(
    string Name,
    int ShipCount);

public sealed record ScenarioPreview(
    string Id,
    string DisplayName,
    DateTimeOffset StartTime,
    DateTimeOffset? EndTime,
    string Description,
    string Visibility,
    int SeaStateBeaufort,
    bool HasMoonlight,
    bool SurpriseAttack,
    int ShipCount,
    int ShipGroupCount,
    IReadOnlyList<ScenarioSidePreview> Sides,
    bool CanStart,
    string UnavailableReason);

public interface IScenarioDataSource
{
    Task<ScenarioXmlBundle> LoadDefaultAsync(CancellationToken cancellationToken = default);

    Task<ScenarioXmlBundle> LoadScenarioAsync(
        string scenarioId,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This data source does not expose selectable scenarios.");
}

public interface IScenarioCatalogSource
{
    Task<IReadOnlyList<ScenarioCatalogEntry>> ScanScenariosAsync(
        CancellationToken cancellationToken = default);

    Task<ScenarioPreview> LoadPreviewAsync(
        string scenarioId,
        CancellationToken cancellationToken = default);
}
