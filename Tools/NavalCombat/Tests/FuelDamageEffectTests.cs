using NavalCombatCore;

namespace NavalCombat.Tests;

public sealed class FuelDamageEffectTests
{
    [Theory]
    [InlineData(HitPenDetType.PenetrateWithDetonate, AmmunitionType.ArmorPiercing)]
    [InlineData(HitPenDetType.PassThrough, AmmunitionType.ArmorPiercing)]
    [InlineData(HitPenDetType.NoPenetration, AmmunitionType.ArmorPiercing)]
    [InlineData(HitPenDetType.PenetrateWithDetonate, AmmunitionType.HighExplosive)]
    [InlineData(HitPenDetType.PassThrough, AmmunitionType.HighExplosive)]
    [InlineData(HitPenDetType.NoPenetration, AmmunitionType.HighExplosive)]
    public void FeedPumpDamageReducesSpeedByTotalBoilerRoomCount(HitPenDetType hit, AmmunitionType ammunition)
    {
        var ship = LoadShip();

        Apply(ship, "119", hit, ammunition);

        Assert.Equal(27d, ship.GetMaxSpeedKnots());
        if (ammunition == AmmunitionType.HighExplosive)
            Assert.Equal(40d, Assert.Single(ship.GetSubStates<ShipboardFireState>()).severity);
        else
            Assert.Empty(ship.GetSubStates<ShipboardFireState>());
    }

    [Theory]
    [InlineData(0d, AmmunitionType.ArmorPiercing)]
    [InlineData(null, AmmunitionType.ArmorPiercing)]
    [InlineData(0d, AmmunitionType.HighExplosive)]
    [InlineData(null, AmmunitionType.HighExplosive)]
    public void CoalOnlyOrUnknownFuelAvoidsPumpSpeedLossButHeStillStartsFire(double? oil, AmmunitionType ammunition)
    {
        var ship = LoadShip();
        ship.shipClass.oilTons = oil;

        Apply(ship, "119", HitPenDetType.NoPenetration, ammunition);

        Assert.Equal(30d, ship.GetMaxSpeedKnots());
        if (ammunition == AmmunitionType.HighExplosive)
            Assert.Equal(40d, Assert.Single(ship.GetSubStates<ShipboardFireState>()).severity);
        else
            Assert.Empty(ship.GetSubStates<ShipboardFireState>());
    }

    [Fact]
    public void PumpLossStacksBeforeMachineryLimitsAndCannotMakeSpeedNegative()
    {
        var ship = LoadShip();
        ship.dynamicStatus.engineRoomHits = 1;
        Assert.Equal(24d, ship.GetMaxSpeedKnots());

        Apply(ship, "119");
        Assert.Equal(24d, ship.GetMaxSpeedKnots());
        Apply(ship, "119");
        Assert.Equal(24d, ship.GetMaxSpeedKnots());
        Apply(ship, "119");
        Assert.Equal(21d, ship.GetMaxSpeedKnots());
        for (var i = 0; i < 10; i++) Apply(ship, "119");

        Assert.Equal(0d, ship.GetMaxSpeedKnots());
    }

    [Fact]
    public void ExplicitDamageResetClearsPumpSpeedLoss()
    {
        var ship = LoadShip();
        Apply(ship, "119");

        ship.dynamicStatus.ResetDamageExpenditureState();

        Assert.Equal(30d, ship.GetMaxSpeedKnots());
    }

    [Theory]
    [InlineData(0.6499d, 1)]
    [InlineData(0.65d, 1)]
    [InlineData(0.6501d, 0)]
    public void BunkerDamageFloodingUsesInclusiveSixtyFivePercentThreshold(double roll, int floodedRooms)
    {
        var ship = LoadShip();

        WithRandom(new ConstantRandom(roll), () => Apply(ship, "154"));

        Assert.Equal(floodedRooms, ship.dynamicStatus.engineRoomFloodingHits);
        Assert.Equal(0, ship.dynamicStatus.engineRoomHits);
        Assert.Equal(20d, Assert.Single(ship.GetSubStates<ShipboardFireState>()).severity);
    }

    [Fact]
    public void RepeatedBunkerDamageStacksPermanentFloodingAndFires()
    {
        var ship = LoadShip();

        WithRandom(new ConstantRandom(0.4d), () =>
        {
            Apply(ship, "154");
            Apply(ship, "154", HitPenDetType.PassThrough);
        });

        Assert.Equal(2, ship.dynamicStatus.engineRoomFloodingHits);
        Assert.Equal(12d, ship.GetMaxSpeedKnots());
        Assert.Equal(new[] { 20d, 20d }, ship.GetSubStates<ShipboardFireState>().Select(fire => fire.severity));
    }

    [Fact]
    public void ClassCBunkerDamageStartsFireWithoutFloodingOrAdditionalRolls()
    {
        var ship = LoadShip();

        WithRandom(new SequenceRandom(), () => Apply(ship, "154", HitPenDetType.NoPenetration,
            additionalProbability: 1d));

        Assert.Equal(0, ship.dynamicStatus.engineRoomFloodingHits);
        Assert.Equal(20d, Assert.Single(ship.GetSubStates<ShipboardFireState>()).severity);
    }

    [Theory]
    [InlineData(HitPenDetType.PenetrateWithDetonate)]
    [InlineData(HitPenDetType.PassThrough)]
    [InlineData(HitPenDetType.NoPenetration)]
    public void HeBunkerDamageStartsBothBaseAndAdditionalFires(HitPenDetType hit)
    {
        var ship = LoadShip();
        ship.shipClass.oilTons = 0d;

        WithRandom(new ConstantRandom(0.99d), () => Apply(ship, "154", hit, AmmunitionType.HighExplosive));

        Assert.Equal(new[] { 20d, 40d }, ship.GetSubStates<ShipboardFireState>().Select(fire => fire.severity));
    }

    [Theory]
    [InlineData(0.05d, "173")]
    [InlineData(0.20d, "183")]
    [InlineData(0.35d, "167")]
    [InlineData(0.60d, "169")]
    [InlineData(0.85d, "122")]
    public void BunkerAdditionalRollDispatchesThePrintedEffect(double tableRoll, string expectedId)
    {
        var ship = LoadShip();
        DamageEffectContext dispatched = null;
        var previousHandler = DamageEffectChart.damageEffectMap[expectedId];
        DamageEffectChart.damageEffectMap[expectedId] = context => dispatched = context;
        try
        {
            WithRandom(new SequenceRandom(0.99d, 0d, tableRoll), () =>
                Apply(ship, "154", additionalProbability: 1d));
        }
        finally
        {
            DamageEffectChart.damageEffectMap[expectedId] = previousHandler;
        }

        Assert.NotNull(dispatched);
        Assert.Same(ship, dispatched.subject);
        Assert.Equal(0d, dispatched.addtionalDamageEffectProbility);
    }

    [Fact]
    public void NonpenetratingHeBunkerHitStillRollsClassAFloodingAndAdditionalEffect()
    {
        var ship = LoadShip();
        var dispatched = false;
        var previousHandler = DamageEffectChart.damageEffectMap["122"];
        DamageEffectChart.damageEffectMap["122"] = _ => dispatched = true;
        try
        {
            WithRandom(new SequenceRandom(0.4d, 0d, 0.99d), () =>
                Apply(ship, "154", HitPenDetType.NoPenetration, AmmunitionType.HighExplosive, 1d));
        }
        finally
        {
            DamageEffectChart.damageEffectMap["122"] = previousHandler;
        }

        Assert.True(dispatched);
        Assert.Equal(1, ship.dynamicStatus.engineRoomFloodingHits);
        Assert.Equal(new[] { 20d, 40d }, ship.GetSubStates<ShipboardFireState>().Select(fire => fire.severity));
    }

    static ShipLog LoadShip()
    {
        var ship = ScenarioFixture.LoadSession().GameState.shipLogs.First();
        ship.operationalState = ShipOperationalState.Operational;
        ship.subStates.Clear();
        ship.dynamicStatus = new DynamicStatus();
        ship.shipClass.speedKnots = 30d;
        ship.shipClass.coalTons = 500d;
        ship.shipClass.oilTons = 100d;
        ship.shipClass.speedKnotsEngineRoomsLevels = new() { 30d, 24d, 12d };
        ship.shipClass.speedKnotsPropulsionShaftLevels = new() { 30d, 20d };
        ship.shipClass.speedKnotsBoilerRooms = new() { 30d, 26d, 22d };
        return ship;
    }

    static void Apply(ShipLog ship, string id, HitPenDetType hit = HitPenDetType.PenetrateWithDetonate,
        AmmunitionType ammunition = AmmunitionType.ArmorPiercing, double additionalProbability = 0d) =>
        DamageEffectChart.AddNewDamageEffect(new DamageEffectContext
        {
            subject = ship,
            damageSchema = DamageSchema.Warship,
            hitPenDetType = hit,
            ammunitionType = ammunition,
            addtionalDamageEffectProbility = additionalProbability,
        }, id);

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

    sealed class ConstantRandom(double value) : Random
    {
        public override double NextDouble() => value;
    }

    sealed class SequenceRandom(params double[] values) : Random
    {
        readonly Queue<double> remaining = new(values);
        public override double NextDouble() => remaining.Dequeue();
    }
}
