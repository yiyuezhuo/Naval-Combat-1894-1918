using System.Xml.Serialization;
using NavalCombatCore;
using YYZ;

namespace NavalCombat.Application;

[XmlRoot("FullState")]
public sealed class FullStateEnvelope
{
    public StreamingAssetReference streamingAssetReference = new();
    public NavalGameState navalGameState = new();
    public ScenarioViewState viewState;
}

public sealed class ScenarioViewState
{
    public double xRotation;
    public double yRotation;
    public double orthographicSize;

    public InitialMapView ToInitialMapView()
    {
        if (!double.IsFinite(xRotation)
            || !double.IsFinite(yRotation)
            || !double.IsFinite(orthographicSize)
            || xRotation is < -90d or > 90d
            || orthographicSize <= 0d)
        {
            return null;
        }

        var longitude = (-yRotation) % 360d;
        if (longitude >= 180d)
            longitude -= 360d;
        if (longitude < -180d)
            longitude += 360d;
        return new InitialMapView(xRotation, longitude, orthographicSize);
    }
}

public sealed record InitialMapView(
    double CenterLatitudeDeg,
    double CenterLongitudeDeg,
    double OrthographicSize);

public sealed record LoadedScenarioPackage(
    NavalGameState GameState,
    InitialMapView InitialMapView)
{
    public TimeSpan TimeZoneOffset { get; init; }
}

public static class ScenarioTimeZone
{
    // Match the original selector: signed view-center longitude, rounded to 15-degree zones.
    // Old/custom scenarios without a usable saved longitude display UTC.
    public static TimeSpan FromViewRotation(double? yRotation)
    {
        if (yRotation is not double rotation || !double.IsFinite(rotation)) return TimeSpan.Zero;
        var longitude = (-rotation) % 360d;
        if (longitude < 0d) longitude += 360d;
        if (longitude > 180d) longitude -= 360d;
        return TimeSpan.FromHours(Math.Round(longitude / 15d));
    }
}

public sealed class StreamingAssetReference
{
    public string leadersPath = "Leaders.xml";
    public string shipClassesPath = "ShipClasses.xml";
    public string namedShipsPath = "NamedShips.xml";
}

public static class ScenarioPackage
{
    public static NavalGameState Load(
        string scenarioXml,
        string leadersXml,
        string shipClassesXml,
        string namedShipsXml) => LoadPackage(
            scenarioXml,
            leadersXml,
            shipClassesXml,
            namedShipsXml).GameState;

    public static LoadedScenarioPackage LoadPackage(
        string scenarioXml,
        string leadersXml,
        string shipClassesXml,
        string namedShipsXml)
    {
        if (scenarioXml == null) throw new ArgumentNullException(nameof(scenarioXml));
        if (string.IsNullOrWhiteSpace(scenarioXml)) throw new ArgumentException("Value cannot be empty or whitespace.", nameof(scenarioXml));
        if (leadersXml == null) throw new ArgumentNullException(nameof(leadersXml));
        if (string.IsNullOrWhiteSpace(leadersXml)) throw new ArgumentException("Value cannot be empty or whitespace.", nameof(leadersXml));
        if (shipClassesXml == null) throw new ArgumentNullException(nameof(shipClassesXml));
        if (string.IsNullOrWhiteSpace(shipClassesXml)) throw new ArgumentException("Value cannot be empty or whitespace.", nameof(shipClassesXml));
        if (namedShipsXml == null) throw new ArgumentNullException(nameof(namedShipsXml));
        if (string.IsNullOrWhiteSpace(namedShipsXml)) throw new ArgumentException("Value cannot be empty or whitespace.", nameof(namedShipsXml));

        var envelope = XmlUtils.FromXML<FullStateEnvelope>(scenarioXml)
            ?? throw new InvalidOperationException("The scenario package could not be deserialized.");
        var gameState = envelope.navalGameState
            ?? throw new InvalidOperationException("The scenario does not contain navalGameState.");

        gameState.scenarioState ??= new ScenarioState();
        gameState.scenarioState.FillBeginDateTimeIfMissing();

        if (!gameState.leadersBuiltin && (gameState.leaders == null || gameState.leaders.Count == 0))
            gameState.LeadersFromXML(leadersXml);
        if (!gameState.shipClassesBuiltin && (gameState.shipClasses == null || gameState.shipClasses.Count == 0))
            gameState.ShipClassesFromXML(shipClassesXml);
        if (!gameState.namedShipsBuiltin && (gameState.namedShips == null || gameState.namedShips.Count == 0))
            gameState.NamedShipsFromXML(namedShipsXml);

        NavalGameState.UpdateInstance(gameState);
        VictoryStatus.CaptureInitialBaselineIfMissing(gameState);
        return new LoadedScenarioPackage(gameState, envelope.viewState?.ToInitialMapView())
        {
            TimeZoneOffset = ScenarioTimeZone.FromViewRotation(envelope.viewState?.yRotation)
        };
    }
}
