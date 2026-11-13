using NavalCombatCore;

namespace NavalCombat.Tests;

public sealed class WarshipDamageTableTests
{
    [Theory]
    [InlineData(1878, "112")]
    [InlineData(1880, "112")]
    [InlineData(1905, "112")]
    [InlineData(1906, "124")]
    [InlineData(1922, "124")]
    [InlineData(1923, "140")]
    [InlineData(1945, "140")]
    [InlineData(1946, "140")]
    public void ReferenceYearSelectsTheChartIncludingEraBoundaries(int year, string expected)
    {
        // D100 13-14, Conning Tower: L1=112, L2=124, L3=140.
        WithRandom(0.13d, () =>
            Assert.Equal(expected, RuleChart.ResolveDamageEffectId(DamageEffectCause.ConningTower, year)));
    }

    [Fact]
    public void AllChartsHaveCompleteWeightedColumnsAndPreserveStarredGeneralResults()
    {
        foreach (var table in new[]
        {
            RuleChart.damageDeterminationTableWarships1880to1905,
            RuleChart.damageDeterminationTableWarships1906to1922,
            RuleChart.damageDeterminationTableWarships1923to1945,
        })
        {
            Assert.Equal(100d, table.rows.Sum());
            Assert.All(table.rows, weight => Assert.True(weight > 0d));
            Assert.Equal(Enum.GetValues<DamageEffectCause>().Length, table.cols.Length);
            Assert.Equal(table.rows.Length, table.cells.GetLength(0));
            Assert.Equal(table.cols.Length, table.cells.GetLength(1));
            Assert.All(table.cells.Cast<string>(), code => Assert.Matches(@"^\*?\d{3}$", code));
        }

        foreach (var year in new[] { 1905, 1906, 1923 })
            WithRandom(0d, () => Assert.Equal("*601", RuleChart.ResolveDamageEffectId(DamageEffectCause.General, year)));
    }

    [Theory]
    [InlineData(1905, 1930, 1945, "112")]
    [InlineData(1906, 1880, 1900, "124")]
    [InlineData(1922, 1945, 1940, "124")]
    [InlineData(1923, 1880, 1900, "140")]
    public void WarshipDamageUsesVictimClassReferenceInsteadOfAttackerOrBattleYear(
        int victimYear, int attackerYear, int battleYear, string expected)
    {
        var state = ScenarioFixture.LoadSession().GameState;
        var victim = state.shipLogs.First();
        var attacker = state.shipLogs.First(ship => !ReferenceEquals(ship.shipClass, victim.shipClass));
        victim.shipClass.referenceYear = victimYear;
        attacker.shipClass.referenceYear = attackerYear;
        state.scenarioState.dateTime = new DateTime(battleYear, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        string executed = null;
        var previous = new[] { "112", "124", "140" }.ToDictionary(id => id, id => DamageEffectChart.damageEffectMap[id]);
        try
        {
            foreach (var id in previous.Keys)
                DamageEffectChart.damageEffectMap[id] = context =>
                {
                    Assert.Same(victim, context.subject);
                    executed = id;
                };
            WithRandom(0.13d, () => Assert.Equal(expected, DamageEffectChart.AddNewDamageEffect(new()
            {
                subject = victim,
                source = attacker,
                damageSchema = DamageSchema.Warship,
                cause = DamageEffectCause.ConningTower,
            })));
        }
        finally
        {
            foreach (var (id, handler) in previous)
                DamageEffectChart.damageEffectMap[id] = handler;
        }

        Assert.Equal(expected, executed);
    }

    [Theory]
    [InlineData(DamageSchema.MerchantVessal, "905")]
    [InlineData(DamageSchema.LandBattery, "L101")]
    public void OtherDamageSchemasKeepTheirOwnChartsRegardlessOfReferenceYear(DamageSchema schema, string expected)
    {
        var ship = ScenarioFixture.LoadSession().GameState.shipLogs.First();
        var executions = 0;
        var previous = DamageEffectChart.damageEffectMap[expected];
        try
        {
            DamageEffectChart.damageEffectMap[expected] = _ => executions++;
            foreach (var year in new[] { 1905, 1923 })
            {
                ship.shipClass.referenceYear = year;
                WithRandom(0d, () => Assert.Equal(expected, DamageEffectChart.AddNewDamageEffect(new()
                {
                    subject = ship,
                    damageSchema = schema,
                    causeMerchantVessel = DamageEffectCauseMerchantVessel.Superstructure,
                    causeLandBattery = DamageEffectCauseLandBattery.Horizontal,
                })));
            }
        }
        finally
        {
            DamageEffectChart.damageEffectMap[expected] = previous;
        }

        Assert.Equal(2, executions);
    }

    static void WithRandom(double roll, Action action)
    {
        var previous = RandomUtils.rand;
        try
        {
            RandomUtils.rand = new FixedRandom(roll);
            action();
        }
        finally
        {
            RandomUtils.rand = previous;
        }
    }

    sealed class FixedRandom(double value) : Random
    {
        public override double NextDouble() => value;
    }
}
