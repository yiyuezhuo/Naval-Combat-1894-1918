using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using NavalCombat.Application;
using NavalCombatCore;

namespace NavalCombat.Application.Persistence;

public class FileScenarioDataSource : IScenarioDataSource, IScenarioCatalogSource
{
    public const string DefaultScenarioId = BattleSession.DefaultScenarioId;
    const string ScenarioSuffix = ".scen.xml";

    readonly string dataDirectory;
    protected virtual string Language => "en";
    protected virtual string UnnamedCommand => "Unnamed command";
    protected virtual string NoDescription => "No description supplied.";
    protected virtual string EmptyTemplate => "This scenario is an empty template and contains no commandable ships.";

    public FileScenarioDataSource(string dataDirectory) : this(dataDirectory,true) { }

    protected FileScenarioDataSource(string dataDirectory,bool fileSystem)
    {
        if (dataDirectory == null) throw new ArgumentNullException(nameof(dataDirectory));
        if (string.IsNullOrWhiteSpace(dataDirectory)) throw new ArgumentException("Value cannot be empty or whitespace.", nameof(dataDirectory));
        this.dataDirectory = fileSystem ? Path.GetFullPath(dataDirectory) : dataDirectory;
    }

    public Task<ScenarioXmlBundle> LoadDefaultAsync(CancellationToken cancellationToken = default) =>
        LoadScenarioAsync(DefaultScenarioId, cancellationToken);

    public async Task<ScenarioXmlBundle> LoadScenarioAsync(
        string scenarioId,
        CancellationToken cancellationToken = default)
    {
        var scenarioPath = ResolveScenarioPath(scenarioId);
        var scenarioXml = await ReadTextAsync(scenarioPath, cancellationToken);
        var references = ReadStreamingAssetReferences(scenarioXml);

        var leadersTask = ReadReferencedFileAsync(references.LeadersPath, cancellationToken);
        var classesTask = ReadReferencedFileAsync(references.ShipClassesPath, cancellationToken);
        var shipsTask = ReadReferencedFileAsync(references.NamedShipsPath, cancellationToken);
        await Task.WhenAll(leadersTask, classesTask, shipsTask);

        return new ScenarioXmlBundle(
            scenarioXml,
            await leadersTask,
            await classesTask,
            await shipsTask);
    }

    public virtual Task<IReadOnlyList<ScenarioCatalogEntry>> ScanScenariosAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!Directory.Exists(dataDirectory))
            throw new DirectoryNotFoundException($"The scenario data folder was not found: {dataDirectory}");

        IReadOnlyList<ScenarioCatalogEntry> entries = Directory
            .EnumerateFiles(dataDirectory, "*", SearchOption.TopDirectoryOnly)
            .Where(path => path.EndsWith(ScenarioSuffix, StringComparison.OrdinalIgnoreCase))
            .Select(path => Path.GetFileName(path))
            .OrderBy(fileName => fileName, StringComparer.OrdinalIgnoreCase)
            .Select(fileName => new ScenarioCatalogEntry(
                fileName,
                fileName,
                fileName[..^ScenarioSuffix.Length]))
            .ToList();

        return Task.FromResult(entries);
    }

    public async Task<ScenarioPreview> LoadPreviewAsync(
        string scenarioId,
        CancellationToken cancellationToken = default)
    {
        var scenarioPath = ResolveScenarioPath(scenarioId);
        var xml = await ReadTextAsync(scenarioPath,cancellationToken);
        using var text = new StringReader(xml);
        using var reader = XmlReader.Create(text,CreateReaderSettings(false));
        var document = XDocument.Load(reader);
        return CreatePreview(scenarioId, document);
    }

    async Task<string> ReadReferencedFileAsync(string relativePath, CancellationToken cancellationToken)
    {
        var fullPath = ResolveWithinDataDirectory(relativePath);
        return await ReadTextAsync(fullPath, cancellationToken);
    }

    protected virtual Task<string> ReadTextAsync(string path,CancellationToken cancellationToken) => File.ReadAllTextAsync(path,cancellationToken);

    string ResolveScenarioPath(string scenarioId)
    {
        if (scenarioId == null) throw new ArgumentNullException(nameof(scenarioId));
        if (string.IsNullOrWhiteSpace(scenarioId)) throw new ArgumentException("Value cannot be empty or whitespace.", nameof(scenarioId));
        if (!scenarioId.EndsWith(ScenarioSuffix, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(Path.GetFileName(scenarioId), scenarioId, StringComparison.Ordinal))
        {
            throw new ArgumentException("The scenario identifier must be a top-level .scen.xml file name.", nameof(scenarioId));
        }

        var fullPath = ResolveWithinDataDirectory(scenarioId);
        return fullPath;
    }

    protected virtual string ResolveWithinDataDirectory(string relativePath)
    {
        if (relativePath == null) throw new ArgumentNullException(nameof(relativePath));
        if (string.IsNullOrWhiteSpace(relativePath)) throw new ArgumentException("Value cannot be empty or whitespace.", nameof(relativePath));
        var normalizedPath = relativePath
            .Replace('\\', Path.DirectorySeparatorChar)
            .Replace('/', Path.DirectorySeparatorChar);
        if (Path.IsPathRooted(normalizedPath))
            throw new InvalidOperationException("Scenario data references must be relative paths.");

        var fullPath = Path.GetFullPath(Path.Combine(dataDirectory, normalizedPath));
        var relativeToRoot = Path.GetRelativePath(dataDirectory, fullPath);
        if (relativeToRoot == ".."
            || relativeToRoot.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
            || Path.IsPathRooted(relativeToRoot))
        {
            throw new InvalidOperationException("The scenario data path leaves the application data directory.");
        }
        return fullPath;
    }

    static StreamingReferences ReadStreamingAssetReferences(string scenarioXml)
    {
        using var textReader = new StringReader(scenarioXml);
        using var xmlReader = XmlReader.Create(textReader, CreateReaderSettings(async: false));
        var document = XDocument.Load(xmlReader, LoadOptions.None);
        var reference = RequiredChild(document.Root, "streamingAssetReference");
        return new StreamingReferences(
            ChildValue(reference, "leadersPath", "Leaders.xml"),
            ChildValue(reference, "shipClassesPath", "ShipClasses.xml"),
            ChildValue(reference, "namedShipsPath", "NamedShips.xml"));
    }

    static async Task<XDocument> LoadDocumentAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var reader = XmlReader.Create(stream, CreateReaderSettings(async: true));
        return await XDocument.LoadAsync(reader, LoadOptions.None, cancellationToken);
    }

    static XmlReaderSettings CreateReaderSettings(bool async) => new()
    {
        Async = async,
        DtdProcessing = DtdProcessing.Prohibit,
        XmlResolver = null,
        IgnoreComments = true,
        IgnoreWhitespace = true,
        CloseInput = false
    };

    ScenarioPreview CreatePreview(string scenarioId, XDocument document)
    {
        var root = document.Root ?? throw new InvalidDataException("The scenario XML has no root element.");
        var gameState = RequiredChild(root, "navalGameState");
        var scenarioState = RequiredChild(gameState, "scenarioState");
        var shipElements = Child(gameState, "shipLogs")?.Elements().ToList() ?? [];
        var groupElements = Child(gameState, "shipGroups")?.Elements().ToList() ?? [];

        var groupParents = groupElements
            .Select(group => new
            {
                Id = ChildValue(group, "objectId"),
                ParentId = ChildValue(group, "parentObjectId"),
                Name = LocalizedName(Child(group, "name"))
            })
            .Where(group => !string.IsNullOrWhiteSpace(group.Id))
            .ToDictionary(group => group.Id, StringComparer.Ordinal);

        var rootGroups = groupParents.Values
            .Where(group => string.IsNullOrWhiteSpace(group.ParentId))
            .ToList();
        var rootShipCounts = rootGroups.ToDictionary(group => group.Id, _ => 0, StringComparer.Ordinal);
        foreach (var ship in shipElements)
        {
            var currentId = ChildValue(ship, "parentObjectId");
            var visited = new HashSet<string>(StringComparer.Ordinal);
            while (!string.IsNullOrWhiteSpace(currentId)
                   && visited.Add(currentId)
                   && groupParents.TryGetValue(currentId, out var group))
            {
                if (string.IsNullOrWhiteSpace(group.ParentId))
                {
                    rootShipCounts[group.Id]++;
                    break;
                }
                currentId = group.ParentId;
            }
        }

        var sides = rootGroups
            .Select(group => new ScenarioSidePreview(
                string.IsNullOrWhiteSpace(group.Name) ? UnnamedCommand : group.Name,
                rootShipCounts[group.Id]))
            .ToList();

        var startTime = ParseDateTimeOffset(ChildValue(scenarioState, "dateTime"), "scenario start time");
        var hasEndTime = ParseBoolean(ChildValue(scenarioState, "hasEndDateTime"));
        var endTime = hasEndTime
            ? ParseDateTimeOffset(ChildValue(scenarioState, "endDateTime"), "scenario end time")
            : (DateTimeOffset?)null;
        var description = LocalizedName(Child(scenarioState, "globalDescription"));
        if (string.IsNullOrWhiteSpace(description)
            || string.Equals(description.Trim(), "none", StringComparison.OrdinalIgnoreCase))
        {
            description = NoDescription;
        }

        var canStart = shipElements.Count > 0 && rootShipCounts.Values.Any(count => count > 0);
        var unavailableReason = canStart
            ? null
            : EmptyTemplate;
        var displayName = scenarioId.EndsWith(ScenarioSuffix, StringComparison.OrdinalIgnoreCase)
            ? scenarioId[..^ScenarioSuffix.Length]
            : scenarioId;

        var rotationText = ChildValue(Child(root, "viewState"), "yRotation");
        var offset = ScenarioTimeZone.FromViewRotation(double.TryParse(rotationText, NumberStyles.Float, CultureInfo.InvariantCulture, out var rotation) ? rotation : null);

        return new ScenarioPreview(
            scenarioId,
            displayName,
            startTime.ToOffset(offset),
            endTime is null ? null : endTime.Value.ToOffset(offset),
            description,
            ChildValue(scenarioState, "visibility", "ExceptionallyClear"),
            ParseInteger(ChildValue(scenarioState, "seaStateBeaufort")),
            ParseBoolean(ChildValue(scenarioState, "hasMoonlight"), defaultValue: true),
            ParseBoolean(ChildValue(scenarioState, "surpriseAttack")),
            shipElements.Count,
            groupElements.Count,
            sides,
            canStart,
            unavailableReason);
    }

    static DateTimeOffset ParseDateTimeOffset(string value, string fieldName)
    {
        if (DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var result))
        {
            return result;
        }
        throw new InvalidDataException($"The {fieldName} is missing or invalid.");
    }

    static int ParseInteger(string value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) ? result : 0;

    static bool ParseBoolean(string value, bool defaultValue = false) =>
        bool.TryParse(value, out var result) ? result : defaultValue;

    string LocalizedName(XElement element) =>
        FirstNonEmpty(
            ChildValue(element, Language switch
            {
                "ja-JP" => "japanese",
                "zh-CN" => "chineseSimplified",
                "zh-TW" => "chineseTraditional",
                _ => "english"
            }),
            ChildValue(element, "english"),
            ChildValue(element, "chineseSimplified"),
            ChildValue(element, "chineseTraditional"),
            ChildValue(element, "japanese"));

    static string FirstNonEmpty(params string[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value) && value != "none");

    static XElement Child(XElement element, string localName) =>
        element?.Elements().FirstOrDefault(child => child.Name.LocalName == localName);

    static XElement RequiredChild(XElement element, string localName) =>
        Child(element, localName)
        ?? throw new InvalidDataException($"The scenario XML does not contain {localName}.");

    static string ChildValue(XElement element, string localName, string fallback = null) =>
        Child(element, localName)?.Value?.Trim() is { Length: > 0 } value ? value : fallback;

    sealed record StreamingReferences(string LeadersPath, string ShipClassesPath, string NamedShipsPath);
}
