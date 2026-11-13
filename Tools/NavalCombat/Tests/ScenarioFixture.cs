using NavalCombat.Application;
using System.Xml.Linq;

namespace NavalCombat.Tests;

internal static class ScenarioFixture
{
    public static BattleSession LoadSession(string scenarioId = "RJH - Battle of Ulsan.scen.xml")
    {
        var session = new BattleSession(new TestScenarioDataSource());
        session.LoadFromXml(
            Read(scenarioId),
            Read("Leaders.xml"),
            Read("ShipClasses.xml"),
            Read("NamedShips.xml"));
        return session;
    }

    static string Read(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "TestData", name));

    internal static int CatalogRecordCount(string name) =>
        XDocument.Parse(Read(name)).Root!.Elements().Count();

    internal static ScenarioXmlBundle Bundle() => new(
        Read("RJH - Battle of Ulsan.scen.xml"),
        Read("Leaders.xml"),
        Read("ShipClasses.xml"),
        Read("NamedShips.xml"));

    sealed class TestScenarioDataSource : IScenarioDataSource
    {
        public Task<ScenarioXmlBundle> LoadDefaultAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Bundle());
    }
}
