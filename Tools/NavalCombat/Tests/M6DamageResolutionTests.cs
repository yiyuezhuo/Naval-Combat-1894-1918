using NavalCombatCore;

namespace NavalCombat.Tests;

public sealed class M6DamageResolutionTests
{
    [Fact]
    public void CapsizeCreatesSinkingProcessThatCanFinishOnTwentyFivePercentRoll()
    {
        var ship = LoadShip();
        var severeFlooding = new SevereFloodingState();

        WithRandom(new SequenceRandom(0.99d), () => severeFlooding.DoOnClockTick(ship, 120d));

        var sinking = Assert.Single(ship.subStates.OfType<SinkingState>());
        Assert.Equal(StateLifeCycle.DieRollPassed, sinking.lifeCycle);
        Assert.Equal(25d, sinking.dieRollThreshold);
        Assert.Equal(ShipOperationalState.FloodingObstruction, ship.operationalState);

        WithRandom(new SequenceRandom(0.24d), () => sinking.Step(ship, 120d));

        Assert.Equal(MapState.Destroyed, ship.mapState);
        Assert.DoesNotContain(sinking, ship.subStates);
    }

    [Fact]
    public void FeedwaterPumpAlternatesBetweenOperationalAndPropulsionLostWithoutRemovingDamage()
    {
        var pump = new FeedwaterPumpDamaged
        {
            lifeCycle = StateLifeCycle.Permanent,
            lostAllPropulsionPercentage = 30d,
            restorePropulsionPercentage = 15d,
        };
        var random = new SequenceRandom(0.20d, 0.20d, 0.10d);

        WithRandom(random, () =>
        {
            pump.DoOnClockTick(null, 120d);
            Assert.True(pump.hasLoseAllPropulsion);
            Assert.Equal(0d, pump.GetMaxSpeedKnotCoef());

            pump.DoOnClockTick(null, 120d);
            Assert.True(pump.hasLoseAllPropulsion);

            pump.DoOnClockTick(null, 120d);
            Assert.False(pump.hasLoseAllPropulsion);
            Assert.Equal(1d, pump.GetMaxSpeedKnotCoef());
        });

        Assert.Equal(3, random.NextDoubleCalls);
        Assert.Equal(StateLifeCycle.Permanent, pump.lifeCycle);
    }

    [Fact]
    public void MachineryFloodingResultUsesFloodingHitCounters()
    {
        var ship = LoadShip();
        var severeFlooding = new SevereFloodingState();

        WithRandom(new SequenceRandom(0.84d, 0.40d), () => severeFlooding.DoOnClockTick(ship, 120d));

        Assert.Equal(1, ship.dynamicStatus.engineRoomFloodingHits);
        Assert.Equal(0, ship.dynamicStatus.engineRoomHits);
        Assert.Equal(0, ship.dynamicStatus.boilerRoomFloodingHits);
        Assert.Equal(0, ship.dynamicStatus.boilerRoomHits);
    }

    [Fact]
    public void AmmunitionLossSamplesDistinctSectionsAndRemovesHalfOfAffectedShare()
    {
        var session = ScenarioFixture.LoadSession();
        var ship = session.GameState.shipLogs.Single(s => s.GetMemberName().Contains("Rurik", StringComparison.OrdinalIgnoreCase));
        Assert.True(ship.batteryStatus.Count >= 3);

        foreach (var battery in ship.batteryStatus)
        {
            battery.ammunition.ArmorPiercing = 100;
            battery.ammunition.semiArmorPiercing = 100;
            battery.ammunition.common = 100;
            battery.ammunition.highExplosive = 100;
        }

        var batteries = ship.batteryStatus.Take(2).ToList();
        var sections = batteries
            .SelectMany(b => b.mountStatus)
            .Select(m => m.GetMountLocation())
            .Distinct()
            .ToList();
        var selectedSection = sections[0];
        var expectedRemaining = batteries.Select(battery =>
        {
            var affected = battery.mountStatus.Count(m => m.GetMountLocation() == selectedSection);
            var lossFraction = 0.5d * affected / battery.mountStatus.Count;
            return 100 - (int)Math.Ceiling(100d * lossFraction);
        }).ToList();
        var random = new SequenceRandom(0.05d) { NextIndex = 0 };

        WithRandom(random, () => new SevereFloodingState().DoOnClockTick(ship, 120d));

        Assert.Equal(sections.Count, random.LastNextMaxValue);
        for (var i = 0; i < batteries.Count; i++)
        {
            Assert.Equal(expectedRemaining[i], batteries[i].ammunition.ArmorPiercing);
            Assert.Equal(expectedRemaining[i], batteries[i].ammunition.common);
        }
        Assert.Equal(100, ship.batteryStatus[2].ammunition.ArmorPiercing);
    }

    [Fact]
    public void SevereFloodingUsesBaseDamageControlBlockingRules()
    {
        var ship = LoadShip();
        var severeFlooding = new SevereFloodingState
        {
            lifeCycle = StateLifeCycle.GivenTime,
        };
        severeFlooding.BeginAt(ship);
        Assert.True(severeFlooding.damageControllable);

        var blocker = new DamageControlModifier
        {
            lifeCycle = StateLifeCycle.GivenTime,
            isDamageControlBlocked = true,
        };
        blocker.BeginAt(ship);

        Assert.False(severeFlooding.damageControllable);
    }

    [Fact]
    public void DamageControlledRollAboveNinetyEightHasNoEffectEvenWithPersistentOffset()
    {
        var ship = LoadShip();
        ship.subStates.Add(new SevereFloodingRollModifier
        {
            severeFloodingRollOffset = 10d,
        });
        var severeFlooding = new SevereFloodingState
        {
            damageControlApplied = true,
        };
        var initialSubStateCount = ship.subStates.Count;

        WithRandom(new SequenceRandom(0.89d), () => severeFlooding.DoOnClockTick(ship, 120d));

        Assert.Equal(ShipOperationalState.Operational, ship.operationalState);
        Assert.Equal(initialSubStateCount, ship.subStates.Count);
    }

    [Fact]
    public void CalmSeaDoesNotAddTenToDe609ActivationRoll()
    {
        var session = ScenarioFixture.LoadSession();
        var ship = session.GameState.shipLogs.First();
        ship.damagePoint = 0d;
        session.GameState.scenarioState.seaStateBeaufort = 3;
        var random = new SequenceRandom(0.65d, 0.99d);

        WithRandom(random, () => new DE609Effect().DoOnClockTick(ship, 120d));

        Assert.Equal(1, random.NextDoubleCalls);
        Assert.Equal(ShipOperationalState.Operational, ship.operationalState);
    }

    [Fact]
    public void FireControlModifierDefaultsToOffsetOnlyAndPreservesExplicitHalfCoefficient()
    {
        Assert.Equal(1d, new FireControlValueModifier().GetFireControlValueCoef());
        Assert.Equal(0.5d, new FireControlValueModifier { fireControlValueCoef = 0.5d }.GetFireControlValueCoef());
    }

    [Fact]
    public void PresetFunnelDamageScenarioUsesOffsetWithoutUnintendedHalfCoefficient()
    {
        var session = ScenarioFixture.LoadSession("SJH - Torpedo Attack Day 2, Weihaiwei.scen.xml");
        var modifiers = session.GameState.shipLogs
            .SelectMany(ship => ship.GetSubStatesDownward().OfType<FireControlValueModifier>())
            .ToList();

        var funnelDamage = Assert.Single(modifiers, modifier => modifier.cause?.Contains("DE 148", StringComparison.Ordinal) == true);
        var shockDamage = Assert.Single(modifiers, modifier => modifier.cause?.Contains("DE 112", StringComparison.Ordinal) == true);
        Assert.Equal(1d, funnelDamage.fireControlValueCoef);
        Assert.Equal(-1d, funnelDamage.fireControlValueOffset);
        Assert.Equal(0.5d, shockDamage.fireControlValueCoef);
    }

    static ShipLog LoadShip()
    {
        var ship = ScenarioFixture.LoadSession().GameState.shipLogs.First();
        ship.mapState = MapState.Deployed;
        ship.operationalState = ShipOperationalState.Operational;
        ship.subStates.Clear();
        ship.dynamicStatus = new DynamicStatus();
        return ship;
    }

    static void WithRandom(Random random, Action action)
    {
        var previous = RandomUtils.rand;
        try
        {
            RandomUtils.rand = random;
            action();
        }
        finally
        {
            RandomUtils.rand = previous;
        }
    }

    sealed class SequenceRandom(params double[] values) : Random
    {
        readonly Queue<double> values = new(values);

        public int NextDoubleCalls { get; private set; }
        public int NextIndex { get; init; }
        public int LastNextMaxValue { get; private set; }

        public override double NextDouble()
        {
            NextDoubleCalls++;
            return values.Dequeue();
        }

        public override int Next(int maxValue)
        {
            LastNextMaxValue = maxValue;
            return Math.Clamp(NextIndex, 0, maxValue - 1);
        }
    }
}
