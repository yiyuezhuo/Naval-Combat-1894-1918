using YYZ.Ballistic;

namespace NavalCombat.Tests;

public sealed class BallisticKernelTests
{
    // Captured from Ballistic_McCoy_Okun's TypeScript calculators on 2026-09-12.
    // These are port-regression values, not independently verified BASIC ground truth.
    [Fact]
    public void DefaultModelsMatchTypeScriptReference()
    {
        Assert.Equal(1583.0580177432612, M79.Calculate(M79.DefaultInput()).NavyBallisticLimit, 8);
        var mc = McCoy.Calculate(McCoy.DefaultInput()).Points.Last();
        Assert.Equal(-283.59282309543187, mc.HeightInches, 7);
        Assert.Equal(1602.0512958262984, mc.Velocity, 7);
        Assert.Equal(1.4151383709934533, mc.Time, 9);
        Assert.Equal(0.12582469084689862, Jbm.CalculateMcDrag(new()).Rows[0].Cd0, 10);
        Assert.Equal(1.9960649549145941, Jbm.CalculateMcGyro(new()).Rows[0].StabilityFactor, 10);
        Assert.Equal(1.4153803994635092, Jbm.CalculateIntLift(new()).Rows[0].Cla, 10);
        var facehard = FacehardCalculator.CalculateFacehard(FacehardInput.CreateDefault());
        Assert.Equal(1546, facehard.NavyBl);
        Assert.Equal(1406, facehard.HolingBl);
        Assert.Equal(1500, facehard.EffectiveBl);
    }

    [Fact]
    public void CachedSearchAgreesWithMatchedRangeOnLowAngleSamples()
    {
        var input = new McCoyPlusInput { MaxRange = 3000, BallisticCoefficient = 2.489, MuzzleVelocity = 2827 };
        var cached = McCoyPlus.CalculateTargetsParallel(input, [1000, 2000, 3000]);
        input.ElevationSearchMode = McCoyPlusElevationSearchMode.MatchedRange;
        var matched = McCoyPlus.CalculateTargetsParallel(input, [1000, 2000, 3000], 1);
        Assert.Equal(3, cached.Rows.Count);
        Assert.Equal(matched.Rows.Count, cached.Rows.Count);
        foreach (var (a, b) in cached.Rows.Zip(matched.Rows))
        {
            Assert.Equal(a.Range, b.Range);
            Assert.InRange(Math.Abs(a.Velocity - b.Velocity), 0, 1);
            Assert.InRange(Math.Abs(a.ElevationDegrees - b.ElevationDegrees), 0, 0.03);
            Assert.InRange(Math.Abs(a.Time - b.Time), 0, 0.01);
        }
    }

    [Fact]
    public async Task FacehardRequestsCaptureIndependentTemplates()
    {
        var template = FacehardInput.CreateDefault();
        var original = FacehardBridge.Create(template);
        var impact = new FacehardBridgeInput { ProjectileDiameter = 14, PlateThickness = 12, StrikingVelocity = 1800, Obliquity = 30 };
        var expected = original(impact).NavyBl;
        template.ArmorId = "gruson";
        var other = FacehardBridge.Create(template);
        var otherExpected = other(impact).NavyBl;
        Assert.NotEqual(expected, otherExpected);
        var results = await Task.WhenAll(Enumerable.Range(0, 12).Select(i => Task.Run(() => (i % 2 == 0 ? original : other)(impact).NavyBl)));
        for (var i = 0; i < results.Length; i++) Assert.Equal(i % 2 == 0 ? expected : otherExpected, results[i]);
    }

    [Fact]
    public void CombinedModelsHonorThePerRequestDelegateAndAngleLimits()
    {
        var input = new McCoyPlusFacehardInput { McCoy = new() { MaxRange = 1000 } };
        var a = McCoyPlusFacehard.Calculate(input, [1000], impact => new() { NavyBl = impact.PlateThickness * 500 });
        var b = McCoyPlusFacehard.Calculate(input, [1000], impact => new() { NavyBl = impact.PlateThickness * 1000 });
        Assert.Single(a.Rows);
        Assert.InRange(Math.Abs(a.Rows[0].PenetrationInches!.Value / b.Rows[0].PenetrationInches!.Value - 2), 0, 0.002);
        Assert.Null(a.Rows[0].HorizontalPenetrationInches);
    }

    [Fact]
    public async Task CancellationInterruptsTrajectoryIntegration()
    {
        using var cts = new CancellationTokenSource();
        var input = new McCoyInput { MaxRange = 100000, PrintInterval = 0.001 };
        var task = Task.Run(() => McCoy.Calculate(input, cts.Token));
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await task);
        var plus = new McCoyPlusInput();
        Assert.ThrowsAny<OperationCanceledException>(() => McCoyPlus.CalculateParallel(plus, cancellationToken: cts.Token));
    }

    [Fact]
    public void ProgressReportsCompletedWorkWithinEachPhase()
    {
        var progress = new ProgressLog();
        var input = new McCoyPlusFacehardM79Input
        {
            McCoy = new() { MaxRange = 3000, BallisticCoefficient = 2.489, MuzzleVelocity = 2827 }
        };
        var result = McCoyPlusFacehardM79.Calculate(input, [1000, 3000], progress: progress);
        Assert.Equal(2, result.Rows.Count);
        Assert.Contains(new BallisticProgress(BallisticStage.Seeding, 8, 8), progress.Items);
        Assert.Contains(new BallisticProgress(BallisticStage.Trajectory, 2, 2), progress.Items);
        Assert.Contains(new BallisticProgress(BallisticStage.Penetration, 2, 2), progress.Items);
        Assert.All(progress.Items, item => Assert.InRange(item.Completed, 0, item.Total));
        Assert.Null(typeof(McCoyInput).GetProperty("CancellationToken"));
        Assert.Null(typeof(McCoyPlusInput).GetProperty("CancellationToken"));
    }

    private sealed class ProgressLog : IProgress<BallisticProgress>
    {
        public List<BallisticProgress> Items { get; } = [];
        public void Report(BallisticProgress value) => Items.Add(value);
    }
}
