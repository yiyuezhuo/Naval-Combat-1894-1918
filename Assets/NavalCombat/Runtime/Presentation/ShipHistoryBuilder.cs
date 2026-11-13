using CoreUtils;
using NavalCombat.Application;
using NavalCombat.Avalonia.Resources;
using NavalCombat.Avalonia.Services;
using NavalCombatCore;

namespace NavalCombat.Avalonia.ViewModels;

public sealed record HistoryValue(string Label, double Value, int Count = 0, IReadOnlyList<double> Samples = null);
public sealed record HistoryBar(string Label, IReadOnlyList<HistoryValue> Segments);
public sealed record HistoryChartSnapshot(string Title, IReadOnlyList<HistoryValue> Slices, IReadOnlyList<HistoryBar> Bars, string EmptyMessage, bool Detailed = false, bool IsBarChart = false);

/// <summary>Detached aggregates with the same counting and hit matching rules as Unity ShipLogEditor.</summary>
public static class ShipHistoryBuilder
{
    sealed record Shot(string Shooter, string Weapon, string Target, DateTime Time, double Damage, bool Hit,
        bool Rapid, DamageSchema Schema = default, HitPenDetType Result = default, string WeaponId = "");
    static string Name(string id) => EntityManager.Instance.Get<ShipLog>(id) is { } ship ? NavalDisplayNames.Ship(ship) : NavalCombatStrings.GetCore("Unknown");
    static string Weapon(string name, string fallback) => string.IsNullOrWhiteSpace(name) ? NavalCombatStrings.GetCore(fallback) : name;
    static string EnumName<T>(T value) => NavalCombat.Unity.NavalText.Instance.GetEnum(value);
    static IEnumerable<Shot> Shots(ShipLog ship)
    {
        for (int i = 0; i < ship.batteryStatus.Count; i++)
        {
            var name = Weapon(ship.shipClass?.batteryRecords.ElementAtOrDefault(i)?.name?.GetShortName(), NavalCombatStrings.GetCore("Battery {0}", i + 1));
            foreach (var log in ship.batteryStatus[i].mountStatus.SelectMany(m => m.logs))
                yield return new(ship.objectId, name, log.firingTargetObjectId, log.firingTime, log.ShellDamageResult?.damagePoint ?? 0, log.hit, false, log.DamageSchema, log.HitPenDetType, $"battery:{i}");
        }
        for (int i = 0; i < ship.rapidFiringStatus.Count; i++)
        {
            var name = Weapon(ship.shipClass?.rapidFireBatteryRecords.ElementAtOrDefault(i)?.name?.GetShortName(), NavalCombatStrings.GetCore("Rapid Battery {0}", i + 1));
            foreach (var log in ship.rapidFiringStatus[i].logs)
                yield return new(ship.objectId, name, log.firingTargetObjectId, log.firingTime, log.damagePoint, log.hit, true, WeaponId: $"rapid:{i}");
        }
    }
    public static IReadOnlyList<HistoryChartSnapshot> Build(BattleSession session, ShipLog ship)
    {
        var byShooter = new List<HistoryValue>(); var byIncomingWeapon = new List<HistoryValue>();
        var outgoing = Shots(ship).ToList();
        var shooterIds = ship.logs.OfType<ShipLogBatteryHitLog>().Select(l => l.shooterId)
            .Concat(ship.logs.OfType<ShipLogRapidFiringGunHitLog>().Select(l => l.shooterId)).Distinct();
        var candidates = shooterIds.Select(id => EntityManager.Instance.Get<ShipLog>(id)).Where(s => s != null)
            .SelectMany(Shots).Where(s => s.Hit && s.Target == ship.objectId).ToList();
        foreach (var log in ship.logs.OrderBy(l => l.time))
        {
            string id; double damage; string weapon;
            switch (log)
            {
                case ShipLogBatteryHitLog hit:
                    id = hit.shooterId; damage = hit.damagePoint;
                    weapon = Match(id, log.time, damage, false, hit.damageSchema, hit.hitPenDetType); break;
                case ShipLogRapidFiringGunHitLog hit:
                    id = hit.shooterId; damage = hit.damagePoint;
                    weapon = Match(id, log.time, damage, true); break;
                case ShipLogTorpedoHitLog hit:
                    var torpedo = hit.GetTorpedo(); id = torpedo?.shooterId; damage = hit.damagePoint;
                    weapon = Weapon(torpedo?.sourceName?.GetShortName(), "Torpedo"); break;
                default: continue;
            }
            byShooter.Add(new(Name(id), damage, 1));
            byIncomingWeapon.Add(new($"{Name(id)} · {weapon}", damage, 1));
        }
        string Match(string shooter, DateTime time, double damage, bool rapid, DamageSchema schema = default, HitPenDetType result = default)
        {
            var match = candidates.FirstOrDefault(c => c.Shooter == shooter && c.Rapid == rapid
                && Math.Abs((c.Time - time).TotalSeconds) < .01 && Math.Abs(c.Damage - damage) < .01
                && (rapid || c.Schema == schema && c.Result == result));
            if (match == null) return NavalCombatStrings.GetCore(rapid ? "Rapid Battery" : "Battery");
            candidates.Remove(match); return match.Weapon;
        }
        double other = Math.Max(0, ship.damagePoint + ship.pendingDamagePoint - byShooter.Sum(v => v.Value));
        if (other > .001) byShooter.Add(new(NavalCombatStrings.GetCore("Other DP Loss"), other));
        foreach (var torpedo in session.GameState.launchedTorpedos.Where(t => t.shooterId == ship.objectId))
            outgoing.Add(new(ship.objectId, Weapon(torpedo.sourceName?.GetShortName(), "Torpedo"), torpedo.hitTargetObjectId,
                default, torpedo.inflictDamagePoint, torpedo.endgameType == LaunchedTorpedoEndgameType.Hit, false, WeaponId: "torpedo:" + torpedo.sourceName?.GetShortName()));
        var hits = outgoing.Where(s => s.Hit).ToArray();
        var locations = ship.logs.OfType<ShipLogBatteryHitLog>().Where(l => l.damageSchema is DamageSchema.Warship or DamageSchema.LandBattery)
            .GroupBy(l => l.ArmorLocation).OrderBy(g => g.Key).Select(g => new HistoryBar(EnumName(g.Key),
                new[] { HitPenDetType.PenetrateWithDetonate, HitPenDetType.PassThrough, HitPenDetType.NoPenetration }
                    .Select(result => new HistoryValue(EnumName(result), g.Count(l => l.hitPenDetType == result))).ToArray())).ToArray();
        var fire = outgoing.GroupBy(s => s.WeaponId).Select(g => new HistoryBar(g.First().Weapon,
            [new(NavalCombatStrings.GetCore("Hit"), g.Count(s => s.Hit)), new(NavalCombatStrings.GetCore("miss"), g.Count(s => !s.Hit))])).ToArray();
        HistoryChartSnapshot Pie(string title, IEnumerable<HistoryValue> values, string emptyMessage) => new(NavalCombatStrings.GetUi(title),
            values.GroupBy(v => v.Label).Select(g => new HistoryValue(g.Key, g.Sum(v => Math.Max(0, v.Value)), g.Sum(v => v.Count), g.Where(v=>v.Count>0).Select(v=>v.Value).ToArray()))
                .Where(v => v.Value > 0).OrderByDescending(v => v.Value).ToArray(), [], NavalCombatStrings.GetCore(emptyMessage), title is "Incoming DP By Weapon Source" or "Outgoing DP By Weapon And Target");
        return [Pie("Current DP Loss Sources", byShooter, "No current DP loss."), Pie("Incoming DP By Weapon Source", byIncomingWeapon, "No incoming DP records."),
            Pie("Outgoing DP By Target", hits.Select(s => new HistoryValue(Name(s.Target), s.Damage, 1)), "No outgoing DP."),
            Pie("Outgoing DP By Weapon And Target", hits.Select(s => new HistoryValue($"{s.Weapon} → {Name(s.Target)}", s.Damage, 1)), "No outgoing weapon DP records."),
            new(NavalCombatStrings.GetUi("Battery Armor Location Results"), [], locations, NavalCombatStrings.GetUi("No incoming battery hits."), IsBarChart: true),
            new(NavalCombatStrings.GetUi("Outgoing Weapon Fire Results"), [], fire, NavalCombatStrings.GetUi("No outgoing weapon fire records."), IsBarChart: true)];
    }
}
