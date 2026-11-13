using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NavalCombatCore
{
    public enum NaabLikeDragFunction
    {
        G1,
        G2,
        G5,
        G6,
        G7,
        G8,
        G9,
        GS,
        GL
    }

    public sealed class NaabLikeProjectile
    {
        public string name = "";
        public double diameterInches = 5d;
        public double totalWeightPounds = 50d;
        public double bodyWeightPounds = 50d;
        public double windscreenWeightPounds;
        public double apCapWeightPounds;

        /// <summary>
        /// ProjInfo cap family selector:
        /// 0 - None
        /// 1 - Hard cap
        /// 2 - Medium cap
        /// 3 - Soft cap
        /// 4 - Hood
        /// </summary>
        public int hcwclcrCapType;
        
        // Former nation/shellClass selector for windscreen/AP-cap NBL addends: <=3in -> 1;
        // nation 1 -> 0.75; nation 2 class 16 -> 0.05; nation 3 class 13-16,
        // nation 4 class 8, and nation 5 class 7-12 -> 0.33; otherwise -> 1.
        public double windscreenNblAddendMultiplier = 1d;
        public double highObliquityWindscreenNblAddendMultiplier = 0.1d; // Former nation 6 class 8/9 branch above 45 deg.
        public double highObliquityThresholdDeg;

        public double muzzleVelocityFeetPerSecond = 3000d;
        public double maxRangeYards = 22600d;
        public double maxElevationDeg = 20d;
        public NaabLikeDragFunction dragFunction = NaabLikeDragFunction.G5;
        public double ballisticCoefficient = 1.9307d;
        public double dragCoefficientAdjust = 14d;
        public double effectiveShellQuality = 0.575d;

        public static NaabLikeProjectile CreateDefaultMetaProjectile()
        {
            return new NaabLikeProjectile
            {
                name = "Palliser Chilled Cast Iron Shot (GB) 6''",
                diameterInches = 6d,
                totalWeightPounds = 100d,
                bodyWeightPounds = 100d,
                windscreenWeightPounds = 0d,
                apCapWeightPounds = 0d,
                hcwclcrCapType = 0,
                windscreenNblAddendMultiplier = 0.75d,
                highObliquityWindscreenNblAddendMultiplier = 0.1d,
                highObliquityThresholdDeg = 0d,
                muzzleVelocityFeetPerSecond = 2230d,
                maxRangeYards = 14600d,
                maxElevationDeg = 20d,
                dragFunction = NaabLikeDragFunction.G1,
                ballisticCoefficient = 2.9727d,
                dragCoefficientAdjust = 0d,
                effectiveShellQuality = 0.575d
            };
        }

        public NaabLikeProjectile Clone()
        {
            return (NaabLikeProjectile)MemberwiseClone();
        }
    }

    public sealed class NaabLikeModelOptions
    {
        public bool enableNaabLikeAdaptation = true;
        public bool enableShellQualityPostScale = true;
        public bool enableExtremeObliquityScale = true;

        public NaabLikeModelOptions Clone()
        {
            return (NaabLikeModelOptions)MemberwiseClone();
        }
    }

    public sealed class NaabLikeArmorInput
    {
        public double quality = 0.95d;
        public double elongationPercent = 22d;
        public double bhn = 235d;
        public double inclinedDeg;
    }

    public sealed class NaabLikeTrajectoryPoint
    {
        public double rangeYards;
        public double timeSeconds;
        public double speedFeetPerSecond;
        public double angleOfFallDeg;
        public double heightFeet;
    }

    public sealed class NaabLikeBallisticsResult
    {
        public bool success;
        public string failureReason;
        public double elevationDeg;
        public double rangeYards;
        public double timeOfFlightSeconds;
        public double impactVelocityFeetPerSecond;
        public double angleOfFallDeg;
        public double horizontalPenetrationInches;
        public double verticalPenetrationInches;
        public readonly List<NaabLikeTrajectoryPoint> trajectory = new();
    }

    public sealed class NaabLikeBallisticsData
    {
        public readonly Dictionary<NaabLikeDragFunction, NaabLikeDragTable> dragTables = new();
        public NaabLikeTerminalTables terminalTables;

        public static NaabLikeBallisticsData LoadEmbedded()
        {
            var data = new NaabLikeBallisticsData();
            AddDragTable(data, NaabLikeDragFunction.G1, NaabLikeEmbeddedData.DragG1);
            AddDragTable(data, NaabLikeDragFunction.G2, NaabLikeEmbeddedData.DragG2);
            AddDragTable(data, NaabLikeDragFunction.G5, NaabLikeEmbeddedData.DragG5);
            AddDragTable(data, NaabLikeDragFunction.G6, NaabLikeEmbeddedData.DragG6);
            AddDragTable(data, NaabLikeDragFunction.G7, NaabLikeEmbeddedData.DragG7);
            AddDragTable(data, NaabLikeDragFunction.G8, NaabLikeEmbeddedData.DragG8);
            AddDragTable(data, NaabLikeDragFunction.G9, NaabLikeEmbeddedData.DragG9);
            AddDragTable(data, NaabLikeDragFunction.GS, NaabLikeEmbeddedData.DragGS);
            AddDragTable(data, NaabLikeDragFunction.GL, NaabLikeEmbeddedData.DragGL);
            data.terminalTables = new NaabLikeTerminalTables
            {
                tdLowerBounds = NaabLikeEmbeddedData.TdLowerBounds,
                tdUpperBounds = NaabLikeEmbeddedData.TdUpperBounds,
                windscreenMatrix = NaabLikeEmbeddedData.WindscreenMatrix,
                windscreenMatrixTail = NaabLikeEmbeddedData.WindscreenMatrixTail,
                obliquityReferenceVector = NaabLikeEmbeddedData.ObliquityReferenceVector,
                highObliquityReferenceMatrix = NaabLikeEmbeddedData.HighObliquityReferenceMatrix,
                tdDecisionBreaks = NaabLikeEmbeddedData.TdDecisionBreaks,
                tdBaseVelocityCoefficients = NaabLikeEmbeddedData.TdDecisionParamA,
                tdThicknessPowerExponents = NaabLikeEmbeddedData.TdDecisionParamB,
                tdShapeSineAmplitudes = NaabLikeEmbeddedData.TdDecisionParamC,
                tdShapeSineFrequencies = NaabLikeEmbeddedData.TdDecisionParamD,
                tdShapeSinePhaseDegs = NaabLikeEmbeddedData.TdDecisionParamE,
                modeSharedMidOb = NaabLikeEmbeddedData.ModeSharedMidOb,
                mode1LowOb = NaabLikeEmbeddedData.Mode1LowOb,
                mode2LowOb = NaabLikeEmbeddedData.Mode2LowOb,
                armorHardnessProfile = NaabLikeEmbeddedData.ArmorHardnessProfile
            };
            return data;
        }

        static void AddDragTable(NaabLikeBallisticsData data, NaabLikeDragFunction dragFunction, double[] coefficients)
        {
            data.dragTables[dragFunction] = new NaabLikeDragTable(
                dragFunction.ToString(),
                NaabLikeEmbeddedData.DragSpeedFeetPerSecond,
                NaabLikeEmbeddedData.DragMach,
                coefficients);
        }
    }

    public sealed class NaabLikeDragTable
    {
        public readonly string name;
        readonly double[] speedFeetPerSecond;
        readonly double[] mach;
        readonly double[] cd;

        public NaabLikeDragTable(string name, double[] speedFeetPerSecond, double[] mach, double[] cd)
        {
            this.name = name;
            this.speedFeetPerSecond = speedFeetPerSecond;
            this.mach = mach;
            this.cd = cd;
        }

        public double CoefficientFromMach(double value, bool useCurvedInterpolation = true)
        {
            if (mach.Length == 0)
                return 0d;
            if (value <= mach[0])
                return cd[0];
            if (value >= mach[^1])
                return cd[^1];

            var idx = Array.BinarySearch(mach, value);
            if (idx >= 0)
                return cd[idx];
            var lower = ~idx - 1;
            return useCurvedInterpolation
                ? CurvedInterpolate(mach, cd, lower, value)
                : LinearInterpolate(mach, cd, lower, value);
        }

        static double CurvedInterpolate(double[] xs, double[] ys, int lower, double x)
        {
            if (lower < 0)
                return ys[0];
            if (lower >= xs.Length - 1)
                return ys[^1];

            var linear = LinearInterpolate(xs, ys, lower, x);
            if (lower >= xs.Length - 2)
                return linear;

            var x0 = xs[lower];
            var x1 = xs[lower + 1];
            var x2 = xs[lower + 2];
            var y0 = ys[lower];
            var y1 = ys[lower + 1];
            var y2 = ys[lower + 2];
            var h0 = x1 - x0;
            var h1 = x2 - x1;
            if (Math.Abs(h0) < 1e-9d || Math.Abs(h1) < 1e-9d)
                return linear;

            var dy0 = y1 - y0;
            var dy1 = y2 - y1;
            var slope0 = dy0 / h0;
            var slope1 = dy1 / h1;
            if (Math.Abs(slope1) > Math.Abs(10d * slope0) || Math.Abs(slope0) > Math.Abs(10d * slope1))
                return linear;

            var t = (x - x0) / h0;
            var correction = t * (t - 1d) * ((dy1 * h0 / h1) - dy0) * 0.5d;
            var curved = linear + correction;
            if (curved < linear)
                curved = (curved + linear) * 0.5d;
            return curved;
        }

        static double LinearInterpolate(double[] xs, double[] ys, int lower, double x)
        {
            var x0 = xs[lower];
            var x1 = xs[lower + 1];
            var y0 = ys[lower];
            var y1 = ys[lower + 1];
            if (Math.Abs(x1 - x0) < 1e-9d)
                return y0;
            return ((x1 - x) * y0 + (x - x0) * y1) / (x1 - x0);
        }
    }

    public sealed class NaabLikeTerminalTables
    {
        public double[] tdLowerBounds;
        public double[] tdUpperBounds;
        public double[][] windscreenMatrix;
        public double[] windscreenMatrixTail;
        public double[] obliquityReferenceVector;
        public double[][] highObliquityReferenceMatrix;
        public double[] tdDecisionBreaks;
        public double[] tdBaseVelocityCoefficients;
        public double[] tdThicknessPowerExponents;
        public double[] tdShapeSineAmplitudes;
        public double[] tdShapeSineFrequencies;
        public double[] tdShapeSinePhaseDegs;
        public double[] modeSharedMidOb;
        public double[] mode1LowOb;
        public double[] mode2LowOb;
        public double[] armorHardnessProfile;
    }

    public sealed class NaabLikeExteriorBallisticsSolver
    {
        const double FeetToMeters = 0.3048d;
        const double MetersToFeet = 1d / FeetToMeters;
        const double Pre1962DragScaleFeet = 0.0002048757d;
        const double ExeDragAdjustRangeScaleFeet = 600000d;
        const double ExeMinBallisticCoefficient = 0.01d;
        const double Pre1962SoundScaleFeetPerSecond = 49.19d;
        const double Pre1962TempCoeff0 = -6.015e-06d;
        const double Pre1962TempCoeff1 = 0d;
        const double Pre1962DensityCoeff0 = -3.158e-05d;
        const double Pre1962DensityCoeff1 = 0d;
        const double Pre1962Epsilon = 1.0e-10d;
        const double EarthRadiusMeters = 6378135.0;
        const double EarthMu = 3.989411596224e14;

        readonly NaabLikeDragTable dragTable;
        readonly NaabLikeProjectile projectile;
        readonly NaabLikeModelOptions options;
        readonly double dxFeet;
        double dragAdjustMachLeOneRangeFeet;
        double dragAdjustMachGtOneRangeFeet;

        public NaabLikeExteriorBallisticsSolver(
            NaabLikeDragTable dragTable,
            NaabLikeProjectile projectile,
            double dxFeet = 3d,
            NaabLikeModelOptions options = null)
        {
            this.dragTable = dragTable;
            this.projectile = projectile;
            this.dxFeet = dxFeet;
            this.options = options?.Clone() ?? new NaabLikeModelOptions();
        }

        public NaabLikeBallisticsResult SolveToGround(double elevationDeg, double maxRangeYards, double sampleStepYards)
        {
            ResetDragAdjustState();
            var state = BuildInitialState(projectile.muzzleVelocityFeetPerSecond, elevationDeg);
            var result = new NaabLikeBallisticsResult
            {
                success = false,
                elevationDeg = elevationDeg
            };

            result.trajectory.Add(ToTrajectoryPoint(state));
            var nextSampleFeet = Math.Max(sampleStepYards, 1d) * 3d;
            var maxRangeFeet = Math.Max(maxRangeYards, 1d) * 3d;
            var previous = state;
            while (state.xFeet <= maxRangeFeet && state.timeSeconds <= 300d && state.vxFeetPerSecond > 1e-6d)
            {
                previous = state;
                state = Step(state);
                if (state.xFeet >= nextSampleFeet)
                {
                    result.trajectory.Add(ToTrajectoryPoint(state));
                    nextSampleFeet += Math.Max(sampleStepYards, 1d) * 3d;
                }

                if (state.yFeet <= 0d && state.xFeet > dxFeet)
                {
                    var impact = InterpolateState(previous, state, previous.yFeet / Math.Max(0.0001d, previous.yFeet - state.yFeet));
                    result.success = true;
                    result.rangeYards = impact.xFeet / 3d;
                    result.timeOfFlightSeconds = impact.timeSeconds;
                    result.impactVelocityFeetPerSecond = Speed(impact);
                    result.angleOfFallDeg = Math.Max(RadiansToDegrees(Math.Atan2(-impact.vyFeetPerSecond, impact.vxFeetPerSecond)), 0d);
                    if (result.trajectory.Count == 0 || Math.Abs(result.trajectory[^1].rangeYards - result.rangeYards) > 0.1d)
                        result.trajectory.Add(ToTrajectoryPoint(impact));
                    return result;
                }
            }

            result.failureReason = "Projectile did not reach ground before the simulation limit.";
            return result;
        }

        public NaabLikeBallisticsResult SolveForTargetRange(double targetRangeYards, double maxElevationDeg, double? angleHintDeg = null)
        {
            var hit = FiringSolutionForRange(projectile.muzzleVelocityFeetPerSecond, targetRangeYards, maxElevationDeg, angleHintDeg);
            if (hit == null)
            {
                return new NaabLikeBallisticsResult
                {
                    success = false,
                    failureReason = "No low-angle firing solution was found.",
                    rangeYards = targetRangeYards
                };
            }

            var trajectory = SolveTrajectoryToRange(hit.elevationDeg, targetRangeYards, Math.Max(targetRangeYards / 80d, 100d));
            return new NaabLikeBallisticsResult
            {
                success = true,
                elevationDeg = hit.elevationDeg,
                rangeYards = targetRangeYards,
                timeOfFlightSeconds = hit.timeSeconds,
                impactVelocityFeetPerSecond = hit.speedFeetPerSecond,
                angleOfFallDeg = hit.descentDeg,
                trajectory = { }
            }.WithTrajectory(trajectory);
        }

        public List<NaabLikeBallisticsResult> SolveForTargetRangesParallel(
            IReadOnlyList<double> targetRangeYards,
            double maxElevationDeg,
            int workerCount = 8,
            Action<int, int> progressCallback = null,
            CancellationToken cancellationToken = default)
        {
            if (targetRangeYards == null || targetRangeYards.Count == 0)
                return new List<NaabLikeBallisticsResult>();

            var maxTargetRange = targetRangeYards.Where(range => range > 0d).DefaultIfEmpty(projectile.maxRangeYards).Max();
            var simulationLimitYards = Math.Max(projectile.maxRangeYards, maxTargetRange * 1.35d + 1000d);
            var sampleStepYards = Math.Max(simulationLimitYards / 120d, 100d);
            var rangeToleranceYards = Math.Max(0.5d, maxTargetRange * 0.0001d);
            var maxElevation = Math.Clamp(maxElevationDeg, 0.1d, 89.9d);
            var workers = Math.Clamp(workerCount, 1, 64);
            var cache = new SharedElevationRangeCache();

            var seedCount = Math.Clamp(workers, 2, 16);
            var seedElevations = new List<double>(seedCount);
            for (int i = 0; i < seedCount; i++)
            {
                var t = seedCount == 1 ? 1d : (double)i / (seedCount - 1);
                seedElevations.Add(Lerp(Math.Min(0.5d, maxElevation), maxElevation, t));
            }

            Parallel.ForEach(seedElevations, new ParallelOptions
            {
                MaxDegreeOfParallelism = workers,
                CancellationToken = cancellationToken
            }, elevation =>
            {
                cache.GetOrAdd(elevation, angle => SolveGroundSample(angle, simulationLimitYards, sampleStepYards));
            });

            var results = new NaabLikeBallisticsResult[targetRangeYards.Count];
            var order = BuildRangeDistributedOrder(targetRangeYards, workers);
            var completed = 0;

            Parallel.ForEach(order, new ParallelOptions
            {
                MaxDegreeOfParallelism = workers,
                CancellationToken = cancellationToken
            }, index =>
            {
                var targetRange = targetRangeYards[index];
                results[index] = SolveTargetRangeFromSharedCache(
                    cache,
                    targetRange,
                    maxElevation,
                    simulationLimitYards,
                    sampleStepYards,
                    rangeToleranceYards,
                    cancellationToken);
                progressCallback?.Invoke(Interlocked.Increment(ref completed), targetRangeYards.Count);
            });

            return results.ToList();
        }

        NaabLikeBallisticsResult SolveTargetRangeFromSharedCache(
            SharedElevationRangeCache cache,
            double targetRangeYards,
            double maxElevationDeg,
            double simulationLimitYards,
            double sampleStepYards,
            double rangeToleranceYards,
            CancellationToken cancellationToken)
        {
            if (targetRangeYards <= 0d)
            {
                return new NaabLikeBallisticsResult
                {
                    success = false,
                    failureReason = "Target range must be greater than 0.",
                    rangeYards = targetRangeYards
                };
            }

            GroundSample bestSample = null;
            for (int iteration = 0; iteration < 48; iteration++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var lowBranch = cache.GetLowAngleBranch();
                if (lowBranch.Count == 0)
                {
                    cache.GetOrAdd(Math.Min(0.5d, maxElevationDeg), angle => SolveGroundSample(angle, simulationLimitYards, sampleStepYards));
                    continue;
                }

                bestSample = FindNearestRangeSample(lowBranch, targetRangeYards, bestSample);
                if (bestSample?.success == true && Math.Abs(bestSample.rangeYards - targetRangeYards) <= rangeToleranceYards)
                    return BuildTargetRangeResult(bestSample, targetRangeYards);

                if (TryFindRangeBracket(lowBranch, targetRangeYards, out var lo, out var hi))
                {
                    var span = hi.rangeYards - lo.rangeYards;
                    var t = Math.Abs(span) < 1e-6d ? 0.5d : (targetRangeYards - lo.rangeYards) / span;
                    var nextElevation = Lerp(lo.elevationDeg, hi.elevationDeg, Math.Clamp(t, 0.05d, 0.95d));
                    if (Math.Abs(hi.elevationDeg - lo.elevationDeg) <= 1e-5d)
                        break;
                    cache.GetOrAdd(nextElevation, angle => SolveGroundSample(angle, simulationLimitYards, sampleStepYards));
                    continue;
                }

                var first = lowBranch[0];
                var last = lowBranch[^1];
                if (targetRangeYards < first.rangeYards && first.elevationDeg > 1e-4d)
                {
                    cache.GetOrAdd(first.elevationDeg * 0.5d, angle => SolveGroundSample(angle, simulationLimitYards, sampleStepYards));
                    continue;
                }

                if (targetRangeYards > last.rangeYards && last.elevationDeg < maxElevationDeg - 1e-4d)
                {
                    var nextHigher = cache.GetNextHigherElevationSample(last.elevationDeg);
                    var upperElevation = nextHigher?.elevationDeg ?? maxElevationDeg;
                    if (upperElevation <= last.elevationDeg + 1e-4d)
                        break;
                    cache.GetOrAdd((last.elevationDeg + upperElevation) * 0.5d, angle => SolveGroundSample(angle, simulationLimitYards, sampleStepYards));
                    continue;
                }

                break;
            }

            var fallback = NewSolver().SolveForTargetRange(targetRangeYards, maxElevationDeg);
            if (fallback.success)
                return fallback;

            return bestSample?.success == true
                ? new NaabLikeBallisticsResult
                {
                    success = false,
                    failureReason = $"No low-angle firing solution was found. Nearest cached range: {bestSample.rangeYards:0} yd.",
                    rangeYards = targetRangeYards
                }
                : fallback;
        }

        GroundSample SolveGroundSample(double elevationDeg, double simulationLimitYards, double sampleStepYards)
        {
            var solver = NewSolver();
            var result = solver.SolveToGround(elevationDeg, simulationLimitYards, sampleStepYards);
            return new GroundSample
            {
                success = result.success,
                failureReason = result.failureReason,
                elevationDeg = elevationDeg,
                rangeYards = result.rangeYards,
                timeOfFlightSeconds = result.timeOfFlightSeconds,
                impactVelocityFeetPerSecond = result.impactVelocityFeetPerSecond,
                angleOfFallDeg = result.angleOfFallDeg
            };
        }

        NaabLikeBallisticsResult BuildTargetRangeResult(GroundSample sample, double targetRangeYards)
        {
            var trajectory = NewSolver().SolveTrajectoryToRange(sample.elevationDeg, targetRangeYards, Math.Max(targetRangeYards / 80d, 100d));
            return new NaabLikeBallisticsResult
            {
                success = true,
                elevationDeg = sample.elevationDeg,
                rangeYards = targetRangeYards,
                timeOfFlightSeconds = sample.timeOfFlightSeconds,
                impactVelocityFeetPerSecond = sample.impactVelocityFeetPerSecond,
                angleOfFallDeg = sample.angleOfFallDeg
            }.WithTrajectory(trajectory);
        }

        NaabLikeExteriorBallisticsSolver NewSolver()
        {
            return new NaabLikeExteriorBallisticsSolver(dragTable, projectile.Clone(), dxFeet, options);
        }

        static GroundSample FindNearestRangeSample(List<GroundSample> samples, double targetRangeYards, GroundSample currentBest)
        {
            var best = currentBest;
            var bestError = best == null ? double.PositiveInfinity : Math.Abs(best.rangeYards - targetRangeYards);
            foreach (var sample in samples)
            {
                var error = Math.Abs(sample.rangeYards - targetRangeYards);
                if (error < bestError)
                {
                    best = sample;
                    bestError = error;
                }
            }
            return best;
        }

        static bool TryFindRangeBracket(List<GroundSample> lowBranch, double targetRangeYards, out GroundSample lo, out GroundSample hi)
        {
            lo = null;
            hi = null;
            for (int i = 1; i < lowBranch.Count; i++)
            {
                var a = lowBranch[i - 1];
                var b = lowBranch[i];
                if (a.rangeYards <= targetRangeYards && targetRangeYards <= b.rangeYards)
                {
                    lo = a;
                    hi = b;
                    return true;
                }
            }
            return false;
        }

        static List<int> BuildRangeDistributedOrder(IReadOnlyList<double> targetRanges, int workerCount)
        {
            var sorted = Enumerable.Range(0, targetRanges.Count)
                .OrderBy(index => targetRanges[index])
                .ToList();
            var output = new List<int>(targetRanges.Count);
            var used = new HashSet<int>();
            var initialCount = Math.Min(workerCount, sorted.Count);
            for (int i = 0; i < initialCount; i++)
            {
                var t = initialCount == 1 ? 0d : (double)i / (initialCount - 1);
                var sortedIndex = Math.Clamp((int)Math.Round(t * (sorted.Count - 1)), 0, sorted.Count - 1);
                var index = sorted[sortedIndex];
                if (used.Add(index))
                    output.Add(index);
            }
            foreach (var index in sorted)
            {
                if (used.Add(index))
                    output.Add(index);
            }
            return output;
        }

        sealed class GroundSample
        {
            public bool success;
            public string failureReason;
            public double elevationDeg;
            public double rangeYards;
            public double timeOfFlightSeconds;
            public double impactVelocityFeetPerSecond;
            public double angleOfFallDeg;
        }

        sealed class SharedElevationRangeCache
        {
            readonly object gate = new();
            readonly List<GroundSample> samples = new();
            readonly HashSet<int> inFlightKeys = new();

            public GroundSample GetOrAdd(double elevationDeg, Func<double, GroundSample> factory)
            {
                var normalizedElevation = Math.Clamp(elevationDeg, 0d, 89.9d);
                var key = ElevationKey(normalizedElevation);
                lock (gate)
                {
                    while (inFlightKeys.Contains(key))
                        Monitor.Wait(gate, 10);

                    var existing = FindByKey(key);
                    if (existing != null)
                        return existing;

                    inFlightKeys.Add(key);
                }

                GroundSample created = null;
                try
                {
                    created = factory(normalizedElevation) ?? new GroundSample
                    {
                        success = false,
                        failureReason = "Sampling failed.",
                        elevationDeg = normalizedElevation
                    };
                }
                finally
                {
                    lock (gate)
                    {
                        inFlightKeys.Remove(key);
                        if (created != null && FindByKey(key) == null)
                        {
                            samples.Add(created);
                            samples.Sort((a, b) => a.elevationDeg.CompareTo(b.elevationDeg));
                        }
                        Monitor.PulseAll(gate);
                    }
                }

                return created;
            }

            public List<GroundSample> GetLowAngleBranch()
            {
                lock (gate)
                {
                    var output = new List<GroundSample>();
                    var previousRange = double.NegativeInfinity;
                    foreach (var sample in samples)
                    {
                        if (!sample.success)
                            continue;
                        if (sample.rangeYards <= previousRange + 0.01d)
                            break;
                        output.Add(sample);
                        previousRange = sample.rangeYards;
                    }
                    return output;
                }
            }

            public GroundSample GetNextHigherElevationSample(double elevationDeg)
            {
                lock (gate)
                {
                    foreach (var sample in samples)
                    {
                        if (sample.success && sample.elevationDeg > elevationDeg + 1e-4d)
                            return sample;
                    }
                    return null;
                }
            }

            GroundSample FindByKey(int key)
            {
                foreach (var sample in samples)
                {
                    if (ElevationKey(sample.elevationDeg) == key)
                        return sample;
                }
                return null;
            }

            static int ElevationKey(double elevationDeg) => (int)Math.Round(elevationDeg * 1000000d);
        }

        public List<NaabLikeTrajectoryPoint> SolveTrajectoryToRange(double elevationDeg, double targetRangeYards, double sampleStepYards)
        {
            ResetDragAdjustState();
            var state = BuildInitialState(projectile.muzzleVelocityFeetPerSecond, elevationDeg);
            var output = new List<NaabLikeTrajectoryPoint> { ToTrajectoryPoint(state) };
            var targetRangeFeet = Math.Max(targetRangeYards, 0d) * 3d;
            var nextSampleFeet = Math.Max(sampleStepYards, 1d) * 3d;
            while (state.xFeet <= targetRangeFeet && state.timeSeconds <= 300d && state.vxFeetPerSecond > 1e-6d)
            {
                state = Step(state);
                if (state.xFeet >= nextSampleFeet)
                {
                    output.Add(ToTrajectoryPoint(state));
                    nextSampleFeet += Math.Max(sampleStepYards, 1d) * 3d;
                }
            }
            if (output.Count == 0 || output[^1].rangeYards < targetRangeYards - 0.1d)
                output.Add(ToTrajectoryPoint(state));
            return output;
        }

        RangeImpactSolution FiringSolutionForRange(double muzzleVelocityFeetPerSecond, double targetRangeYards, double maxElevationDeg, double? angleHintDeg)
        {
            var cache = new Dictionary<double, RangeImpactSolution>();
            RangeImpactSolution Sample(double angle)
            {
                var key = Math.Round(angle * 1000000d) / 1000000d;
                if (!cache.TryGetValue(key, out var cached))
                {
                    cached = SampleAtRange(muzzleVelocityFeetPerSecond, key, targetRangeYards);
                    cache[key] = cached;
                }
                return cached;
            }

            RangeImpactSolution Refine(double lo, double hi)
            {
                var loSample = Sample(lo);
                var hiSample = Sample(hi);
                if (loSample == null || hiSample == null)
                    return null;
                var loHeight = loSample.heightFeet;
                var hiHeight = hiSample.heightFeet;
                for (int i = 0; i < 36; i++)
                {
                    var mid = (lo + hi) * 0.5d;
                    var midSample = Sample(mid);
                    if (midSample == null)
                        return null;
                    var midHeight = midSample.heightFeet;
                    if (Math.Abs(midHeight) <= 0.1d)
                        return midSample;
                    if ((loHeight < 0d && midHeight >= 0d) || (loHeight > 0d && midHeight <= 0d))
                    {
                        hi = mid;
                        hiHeight = midHeight;
                    }
                    else
                    {
                        lo = mid;
                        loHeight = midHeight;
                    }
                }
                return Sample((lo + hi) * 0.5d);
            }

            (double lo, double hi)? Scan(double start, double stop, double step)
            {
                double? previousAngle = null;
                double? previousHeight = null;
                for (var angle = start; angle <= stop + 1e-9d; angle += step)
                {
                    var hit = Sample(angle);
                    if (hit == null)
                        continue;
                    var height = hit.heightFeet;
                    if (Math.Abs(height) <= 0.25d)
                        return (angle, angle);
                    if (previousAngle.HasValue && previousHeight.HasValue &&
                        ((previousHeight.Value < 0d && height >= 0d) || (previousHeight.Value > 0d && height <= 0d)))
                        return (previousAngle.Value, angle);
                    previousAngle = angle;
                    previousHeight = height;
                }
                return null;
            }

            var windows = new List<(double start, double stop, double step)>();
            if (angleHintDeg.HasValue)
            {
                windows.Add((Math.Max(0d, angleHintDeg.Value - 2d), Math.Min(maxElevationDeg, angleHintDeg.Value + 4d), 0.5d));
                windows.Add((Math.Max(0d, angleHintDeg.Value - 6d), Math.Min(maxElevationDeg, angleHintDeg.Value + 12d), 1d));
            }
            windows.Add((0d, maxElevationDeg, 1d));

            foreach (var window in windows)
            {
                var bracket = Scan(window.start, window.stop, window.step);
                if (!bracket.HasValue)
                    continue;
                if (Math.Abs(bracket.Value.lo - bracket.Value.hi) < 1e-12d)
                    return Sample(bracket.Value.lo);

                var fineLo = Math.Max(0d, bracket.Value.lo - 1d);
                var fineHi = Math.Min(maxElevationDeg, bracket.Value.hi + 1d);
                var fine = Scan(fineLo, fineHi, 0.25d);
                if (fine.HasValue)
                    return Math.Abs(fine.Value.lo - fine.Value.hi) < 1e-12d ? Sample(fine.Value.lo) : Refine(fine.Value.lo, fine.Value.hi);
                break;
            }

            return null;
        }

        RangeImpactSolution SampleAtRange(double muzzleVelocityFeetPerSecond, double elevationDeg, double targetRangeYards)
        {
            ResetDragAdjustState();
            var targetRangeFeet = targetRangeYards * 3d;
            var state = BuildInitialState(muzzleVelocityFeetPerSecond, elevationDeg);
            while (state.xFeet <= targetRangeFeet && state.timeSeconds <= 300d && state.vxFeetPerSecond > 1e-6d)
            {
                var previous = state;
                state = Step(state);
                if (state.xFeet >= targetRangeFeet)
                {
                    if (state.xFeet <= previous.xFeet)
                        return null;
                    var t = (targetRangeFeet - previous.xFeet) / (state.xFeet - previous.xFeet);
                    var hit = InterpolateState(previous, state, t);
                    return new RangeImpactSolution
                    {
                        targetRangeYards = targetRangeYards,
                        elevationDeg = elevationDeg,
                        timeSeconds = hit.timeSeconds,
                        speedFeetPerSecond = Speed(hit),
                        descentDeg = Math.Max(RadiansToDegrees(Math.Atan2(-hit.vyFeetPerSecond, hit.vxFeetPerSecond)), 0d),
                        heightFeet = hit.yFeet
                    };
                }
            }
            return null;
        }

        BallisticState Step(BallisticState point)
        {
            var slope1 = Slopes(point);
            var euler = new BallisticState
            {
                xFeet = point.xFeet + dxFeet,
                yFeet = point.yFeet + dxFeet * slope1.dyDx,
                vxFeetPerSecond = point.vxFeetPerSecond + dxFeet * slope1.dvxDx,
                vyFeetPerSecond = point.vyFeetPerSecond + dxFeet * slope1.dvyDx,
                timeSeconds = point.timeSeconds + dxFeet * slope1.dtDx
            };
            var slope2 = Slopes(euler);
            return new BallisticState
            {
                xFeet = point.xFeet + dxFeet,
                yFeet = point.yFeet + 0.5d * dxFeet * (slope1.dyDx + slope2.dyDx),
                vxFeetPerSecond = point.vxFeetPerSecond + 0.5d * dxFeet * (slope1.dvxDx + slope2.dvxDx),
                vyFeetPerSecond = point.vyFeetPerSecond + 0.5d * dxFeet * (slope1.dvyDx + slope2.dvyDx),
                timeSeconds = point.timeSeconds + 0.5d * dxFeet * (slope1.dtDx + slope2.dtDx)
            };
        }

        (double dyDx, double dvxDx, double dvyDx, double dtDx) Slopes(BallisticState point)
        {
            var speed = Speed(point);
            if (speed < 1e-9d || Math.Abs(point.vxFeetPerSecond) < 1e-9d)
                return (0d, 0d, 0d, 0d);

            var atmosphere = AtmosphereAtHeight(point.yFeet);
            var mach = speed / atmosphere.soundFeetPerSecond;
            var cdRef = dragTable.CoefficientFromMach(mach, options.enableNaabLikeAdaptation);
            var bcEff = EffectiveBallisticCoefficient(point.xFeet, mach);
            var dragSlope = Pre1962DragScaleFeet * atmosphere.densityRatio * cdRef * speed / bcEff;
            var dvxDx = -dragSlope;
            var dvyDx = dvxDx * (point.vyFeetPerSecond / point.vxFeetPerSecond) - atmosphere.gravityFeetPerSecondSquared / point.vxFeetPerSecond;
            return (point.vyFeetPerSecond / point.vxFeetPerSecond, dvxDx, dvyDx, 1d / point.vxFeetPerSecond);
        }

        double EffectiveBallisticCoefficient(double rangeFeet, double mach)
        {
            var bc = projectile.ballisticCoefficient;
            if (!options.enableNaabLikeAdaptation)
                return bc;

            var adjust = projectile.dragCoefficientAdjust;
            if (Math.Abs(adjust) < 1e-9d)
                return bc;

            var effectiveRange = rangeFeet > 0d ? rangeFeet : dxFeet;
            var rangeTerm = 0d;
            if (mach <= 1d)
            {
                if (dragAdjustMachLeOneRangeFeet == 0d)
                    dragAdjustMachLeOneRangeFeet = effectiveRange;
            }
            else
            {
                if (dragAdjustMachLeOneRangeFeet > 0d && dragAdjustMachGtOneRangeFeet == 0d)
                    dragAdjustMachGtOneRangeFeet = effectiveRange;
                rangeTerm = Math.Abs(dragAdjustMachLeOneRangeFeet + dragAdjustMachGtOneRangeFeet - effectiveRange);
            }

            return Math.Max(ExeMinBallisticCoefficient, bc + adjust * rangeTerm / ExeDragAdjustRangeScaleFeet);
        }

        static (double soundFeetPerSecond, double densityRatio, double gravityFeetPerSecondSquared) AtmosphereAtHeight(double heightFeet)
        {
            var tempRankine = 518.67d * Math.Exp((Pre1962TempCoeff1 * heightFeet + Pre1962TempCoeff0) * heightFeet) + Pre1962Epsilon;
            var sound = Math.Sqrt(tempRankine) * Pre1962SoundScaleFeetPerSecond + Pre1962Epsilon;
            var density = Math.Exp((Pre1962DensityCoeff1 * heightFeet + Pre1962DensityCoeff0) * heightFeet);
            var radius = EarthRadiusMeters + heightFeet * FeetToMeters;
            var gravity = (double)(EarthMu / (radius * radius)) * MetersToFeet;
            return (sound, density, gravity);
        }

        static BallisticState BuildInitialState(double muzzleVelocityFeetPerSecond, double elevationDeg)
        {
            var angleRad = DegreesToRadians(elevationDeg);
            return new BallisticState
            {
                vxFeetPerSecond = muzzleVelocityFeetPerSecond * Math.Cos(angleRad),
                vyFeetPerSecond = muzzleVelocityFeetPerSecond * Math.Sin(angleRad)
            };
        }

        void ResetDragAdjustState()
        {
            dragAdjustMachLeOneRangeFeet = 0d;
            dragAdjustMachGtOneRangeFeet = 0d;
        }

        static NaabLikeTrajectoryPoint ToTrajectoryPoint(BallisticState state)
        {
            return new NaabLikeTrajectoryPoint
            {
                rangeYards = state.xFeet / 3d,
                timeSeconds = state.timeSeconds,
                speedFeetPerSecond = Speed(state),
                angleOfFallDeg = Math.Max(RadiansToDegrees(Math.Atan2(-state.vyFeetPerSecond, state.vxFeetPerSecond)), 0d),
                heightFeet = state.yFeet
            };
        }

        static BallisticState InterpolateState(BallisticState a, BallisticState b, double t)
        {
            t = Math.Clamp(t, 0d, 1d);
            return new BallisticState
            {
                xFeet = Lerp(a.xFeet, b.xFeet, t),
                yFeet = Lerp(a.yFeet, b.yFeet, t),
                vxFeetPerSecond = Lerp(a.vxFeetPerSecond, b.vxFeetPerSecond, t),
                vyFeetPerSecond = Lerp(a.vyFeetPerSecond, b.vyFeetPerSecond, t),
                timeSeconds = Lerp(a.timeSeconds, b.timeSeconds, t)
            };
        }

        static double Speed(BallisticState state) => Math.Sqrt(state.vxFeetPerSecond * state.vxFeetPerSecond + state.vyFeetPerSecond * state.vyFeetPerSecond);
        static double Lerp(double a, double b, double t) => a + (b - a) * t;
        static double DegreesToRadians(double degrees) => degrees * Math.PI / 180d;
        static double RadiansToDegrees(double radians) => radians * 180d / Math.PI;

        sealed class BallisticState
        {
            public double xFeet;
            public double yFeet;
            public double vxFeetPerSecond;
            public double vyFeetPerSecond;
            public double timeSeconds;
        }

        sealed class RangeImpactSolution
        {
            public double targetRangeYards;
            public double elevationDeg;
            public double timeSeconds;
            public double speedFeetPerSecond;
            public double descentDeg;
            public double heightFeet;
        }
    }

    public sealed class NaabLikeTerminalBallisticsSolver
    {
        const double MinScannedThicknessInches = 0.1d;
        const double ThicknessScanStepInches = 0.02d;
        const double MaxThicknessByDiameterMultiplier = 6d;
        const double MaxThicknessDiameterClearanceInches = 0.1d;
        const double MaxScannedThicknessInches = 199d;
        const double PartialPenetrationVelocityMarginFeetPerSecond = 60d;
        const double NoHolingVelocityMarginFeetPerSecond = 120d;
        const double HighObliquityStartDeg = 45d;
        const double ExtremeObliquityStartDeg = 80d;
        const double RightAngleObliquityDeg = 90d;
        const double LowObliquityReferenceStepDeg = 2.5d;
        const double HighObliquityReferenceStepDeg = 2.5d;
        const double ComponentObliquityTableStepDeg = 5d;
        const double SharedCapObliquityStartDeg = 40d;
        const double SharedCapObliquityEndDeg = 75d;
        const double SharedCapObliquityStepDeg = 2.5d;
        const double ApCapThresholdReferenceObliquityDeg = 45d;
        const double TdTableStep = 0.05d;
        const double HighObliquityTdSaturation = 0.9d;
        const int MaxTdDecisionSegmentIndex = 11;
        const int ComponentTdTailBucket = 16;
        const int HighObliquityTdSaturatedBucket = 17;

        readonly NaabLikeTerminalTables tables;
        readonly NaabLikeProjectile projectile;
        readonly NaabLikeArmorInput armor;
        readonly NaabLikeModelOptions options;

        public NaabLikeTerminalBallisticsSolver(
            NaabLikeTerminalTables tables,
            NaabLikeProjectile projectile,
            NaabLikeArmorInput armor,
            NaabLikeModelOptions options = null)
        {
            this.tables = tables;
            this.projectile = projectile;
            this.armor = armor;
            this.options = options?.Clone() ?? new NaabLikeModelOptions();
        }

        public double CompletePenetrationInches(double strikingVelocityFeetPerSecond, double obliquityDeg)
        {
            return ScanBallisticLimitThicknesses(strikingVelocityFeetPerSecond, obliquityDeg).full;
        }

        public (double full, double partial, double noHoling) ScanBallisticLimitThicknesses(double strikingVelocityFeetPerSecond, double obliquityDeg)
        {
            var upper = Math.Min(
                MaxThicknessByDiameterMultiplier * projectile.diameterInches - MaxThicknessDiameterClearanceInches,
                MaxScannedThicknessInches);
            var actual = Math.Max(strikingVelocityFeetPerSecond, 0d);
            var full = 0d;
            var partial = 0d;
            var noHoling = 0d;

            for (var thickness = MinScannedThicknessInches; thickness <= upper + 1e-6d; thickness += ThicknessScanStepInches)
            {
                var trueNbl = TrueNblVelocityFeetPerSecond(thickness, obliquityDeg);
                var actualRounded = Math.Round(actual);
                var trueRounded = Math.Round(trueNbl);
                if (full == 0d && actual <= trueNbl)
                    full = thickness;
                if (partial == 0d && actualRounded + PartialPenetrationVelocityMarginFeetPerSecond <= trueRounded)
                    partial = thickness;
                if (actualRounded + NoHolingVelocityMarginFeetPerSecond < trueRounded)
                {
                    noHoling = thickness;
                    break;
                }
            }

            var scale = PenetrationTripletPostScale(obliquityDeg);
            return (full * scale, partial * scale, noHoling * scale);
        }

        double TrueNblVelocityFeetPerSecond(double thicknessInches, double obliquityDeg)
        {
            var diameter = Math.Max(projectile.diameterInches, 0.1d);
            var td = ClampTd(thicknessInches / diameter);
            var (baseVelocityCoefficient, thicknessPowerExponent, _, _, _) = TdSegmentParameters(td);
            var weightOverD3 = Math.Max(projectile.totalWeightPounds / (diameter * diameter * diameter), 1e-9d);
            var plateQuality = options.enableNaabLikeAdaptation
                ? EffectivePlateQualityFactor()
                : armor.quality;
            var powerBase = Math.Max(plateQuality, 0.01d) * td;
            var baseNbl = baseVelocityCoefficient * Math.Pow(powerBase, thicknessPowerExponent);
            baseNbl *= TdShapeMultiplier(td);
            baseNbl *= ScaleFactor();
            baseNbl /= Math.Sqrt(weightOverD3);

            var ob = ClampObliquity(obliquityDeg);
            if (ob > 0.1d)
                baseNbl *= ObliquityMultiplier(ob, td);

            baseNbl *= ElongationFactor();
            if (options.enableNaabLikeAdaptation)
            {
                baseNbl *= 1d +
                    WindscreenSelectorAddend(td, ob) +
                    ApCapObliquityAddend(td, ob);
            }
            return baseNbl;
        }

        double EffectivePlateQualityFactor()
        {
            var baseline = ArmorHardnessProfile(235d);
            var current = ArmorHardnessProfile(armor.bhn);
            if (Math.Abs(current) < 1e-12d)
                return armor.quality;
            return Math.Clamp(armor.quality * baseline / current, 0.5d, 1.1d);
        }

        double ScaleFactor()
        {
            var diameter = Math.Max(projectile.diameterInches, 0.1d);
            return Math.Sqrt(Math.Max(1e-9d, 1d - 0.04d * Math.Log(diameter / 3d)));
        }

        double ElongationFactor()
        {
            var diameter = Math.Max(projectile.diameterInches, 8d);
            var pct = Math.Clamp(armor.elongationPercent, 0.1d, 25d);
            return 1d - (1d - Math.Sqrt(pct / 25d)) * (diameter - 8d) / 8d;
        }

        double ObliquityMultiplier(double obliquityDeg, double td)
        {
            var ob = ClampObliquity(obliquityDeg);
            if (ob < HighObliquityStartDeg)
                return LowObliquityMultiplier(ob);
            var reference = HighObReference(td, ob);
            return reference / Math.Max(Math.Cos(DegreesToRadians(ob)), 1e-9d);
        }

        double LowObliquityMultiplier(double obliquityDeg)
        {
            var values = tables.obliquityReferenceVector;
            if (values == null || values.Length == 0)
                return 1d;
            var step = LowObliquityReferenceStepDeg;
            var ob = Math.Clamp(obliquityDeg, 0d, (values.Length - 1) * step);
            var scaled = ob / step;
            var idx = Math.Min(TruncatePositiveIndex(scaled), values.Length - 2);
            var frac = scaled - idx;
            var reference = Lerp(values[idx], values[idx + 1], frac);
            return reference / Math.Max(Math.Cos(DegreesToRadians(ob)), 1e-9d);
        }

        double HighObReference(double td, double obliquityDeg)
        {
            var matrix = tables.highObliquityReferenceMatrix;
            var obScaled = Math.Max(0d, (obliquityDeg - HighObliquityStartDeg) / HighObliquityReferenceStepDeg);
            var obBucket = Math.Min(TruncatePositiveIndex(obScaled), matrix.Length - 2);
            var obFrac = (obliquityDeg - HighObliquityStartDeg - obBucket * HighObliquityReferenceStepDeg) / HighObliquityReferenceStepDeg;
            int tdBucket;
            double tdFrac;
            if (td >= HighObliquityTdSaturation)
            {
                tdBucket = HighObliquityTdSaturatedBucket;
                tdFrac = 1d;
            }
            else
            {
                var tdScaled = Math.Max(0d, td / TdTableStep);
                tdBucket = Math.Min(TruncatePositiveIndex(tdScaled), matrix[0].Length - 2);
                tdFrac = (td - tdBucket * TdTableStep) / TdTableStep;
            }

            var row0 = matrix[obBucket];
            var row1 = matrix[obBucket + 1];
            var ref0 = Lerp(row0[tdBucket], row0[tdBucket + 1], tdFrac);
            var ref1 = Lerp(row1[tdBucket], row1[tdBucket + 1], tdFrac);
            return Lerp(ref0, ref1, obFrac);
        }

        (double baseVelocityCoefficient, double thicknessPowerExponent, double shapeSineAmplitude, double shapeSineFrequency, double shapeSinePhaseDeg) TdSegmentParameters(double td)
        {
            var idx = 1;
            var breaks = tables.tdDecisionBreaks;
            while (idx < breaks.Length - 1 && breaks[idx] < td && idx < MaxTdDecisionSegmentIndex)
                idx++;
            return (
                tables.tdBaseVelocityCoefficients[idx],
                tables.tdThicknessPowerExponents[idx],
                tables.tdShapeSineAmplitudes[idx],
                tables.tdShapeSineFrequencies[idx],
                tables.tdShapeSinePhaseDegs[idx]);
        }

        double TdShapeMultiplier(double td)
        {
            var (_, _, shapeSineAmplitude, shapeSineFrequency, shapeSinePhaseDeg) = TdSegmentParameters(td);
            if (Math.Abs(shapeSineAmplitude) < 1e-12d)
                return 1d;
            var shaped = Math.Sin(DegreesToRadians(td * shapeSineFrequency - shapeSinePhaseDeg));
            return 1d + shapeSineAmplitude * Math.Max(shaped, 0d);
        }

        double ArmorHardnessProfile(double bhn)
        {
            var values = tables.armorHardnessProfile;
            var clamped = Math.Clamp(bhn, 200d, 200d + 5d * (values.Length - 1));
            var scaled = (clamped - 200d) / 5d;
            var idx = Math.Min(Math.Floor(scaled), values.Length - 2);
            var i = Math.Max(0, (int)idx);
            return Lerp(values[i], values[i + 1], scaled - i);
        }

        double WindscreenSelectorAddend(double td, double obliquityDeg)
        {
            var selectorMultiplier = WindscreenNblAddendMultiplier(obliquityDeg);
            var windscreenPercent = 100d * Math.Max(projectile.windscreenWeightPounds, 0d) / Math.Max(projectile.totalWeightPounds, 1e-9d);
            if (windscreenPercent <= 0.1d)
                return 0d;

            var clampedTd = ClampTd(td);
            if (SuppressProjectileAddendForThinPlate(clampedTd, selectorMultiplier))
                return 0d;

            var interp = clampedTd < 0.03d
                ? InterpObMatrixRow(tables.windscreenMatrix[1], obliquityDeg)
                : InterpMatrixComponent(tables.windscreenMatrix, tables.windscreenMatrixTail, clampedTd, obliquityDeg);
            return (windscreenPercent / 5.1d) * selectorMultiplier * interp;
        }

        double ApCapObliquityAddend(double td, double obliquityDeg)
        {
            var mode = projectile.hcwclcrCapType;
            if (mode is not (1 or 2))
                return 0d;

            var apCapPercent = 100d * Math.Max(projectile.apCapWeightPounds, 0d) / Math.Max(projectile.totalWeightPounds, 1e-9d);
            if (apCapPercent <= 0.1d)
                return 0d;

            var ob = ClampObliquity(obliquityDeg);
            var clampedTd = ClampTd(td);
            if (SuppressProjectileAddendForThinPlate(clampedTd, WindscreenNblAddendMultiplier(ob)))
                return 0d;

            var divisor = mode == 2 ? 10d : 20d;
            var cutoff = mode == 2 ? 65d : 50d;
            var capRatio = apCapPercent / divisor;
            var threshold = ApCapObliquityThreshold(ob);
            if (clampedTd > threshold)
            {
                if (ob >= cutoff)
                    return 0d;
                var tableValue = mode == 1
                    ? InterpVectorByStep(tables.mode1LowOb, ob, 0d, 5d)
                    : InterpVectorByStep(tables.mode2LowOb, ob, 0d, 5d);
                return tableValue * (1d + 0.6d * (capRatio - 1d));
            }

            if (ob > SharedCapObliquityStartDeg && ob < SharedCapObliquityEndDeg)
                return InterpVectorByStep(tables.modeSharedMidOb, ob, SharedCapObliquityStartDeg, SharedCapObliquityStepDeg) * capRatio;
            return 0d;
        }

        double WindscreenNblAddendMultiplier(double obliquityDeg)
        {
            return projectile.highObliquityThresholdDeg > 0d && obliquityDeg > projectile.highObliquityThresholdDeg
                ? projectile.highObliquityWindscreenNblAddendMultiplier
                : projectile.windscreenNblAddendMultiplier;
        }

        static bool SuppressProjectileAddendForThinPlate(double td, double selectorMultiplier)
        {
            return td < 0.03d && (selectorMultiplier >= 0.75d || (selectorMultiplier >= 0.4d && td < 0.015d));
        }

        static double ApCapObliquityThreshold(double obliquityDeg)
        {
            return Math.Round(PiecewiseObliquityThreshold(obliquityDeg) * 1000d) / 1000d;
        }

        static double PiecewiseObliquityThreshold(double obliquityDeg)
        {
            var ob = Math.Max(obliquityDeg, 0d);
            if (ob > 65d)
                return 0.42d;
            if (ob > 55d)
                return 0.44d - 0.002d * (ob - 55d);
            return 0.66d - 0.18d * (ob / ApCapThresholdReferenceObliquityDeg);
        }

        double InterpMatrixComponent(double[][] matrix, double[] tail, double td, double obliquityDeg)
        {
            var obBucket = ObBucket(obliquityDeg, out var obLerp);
            var tdBucket = TdBucket(td);
            var tdLerp = TdLerp(td, tdBucket);
            if (tdBucket == ComponentTdTailBucket)
                return Lerp(tail[0], tail[1], obLerp);

            var row0 = matrix[tdBucket];
            var row1 = matrix[Math.Min(tdBucket + 1, matrix.Length - 1)];
            var col0 = obBucket;
            var col1 = Math.Min(obBucket + 1, row0.Length - 1);
            var base0 = Lerp(row0[col0], row0[col1], obLerp);
            var base1 = Lerp(row1[col0], row1[col1], obLerp);
            return Lerp(base0, base1, tdLerp);
        }

        double InterpObMatrixRow(double[] row, double obliquityDeg)
        {
            var obBucket = ObBucket(obliquityDeg, out var obLerp);
            var col1 = Math.Min(obBucket + 1, row.Length - 1);
            return Lerp(row[obBucket], row[col1], obLerp);
        }

        int TdBucket(double td)
        {
            var bucket = 0;
            var upper = tables.tdUpperBounds;
            while (bucket < ComponentTdTailBucket && upper[bucket] < td)
                bucket++;
            return bucket;
        }

        double TdLerp(double td, int bucket)
        {
            if (bucket >= ComponentTdTailBucket)
                return 0d;
            var lo = tables.tdLowerBounds[bucket];
            var hi = tables.tdUpperBounds[bucket];
            return hi <= lo ? 0d : (td - lo) / (hi - lo);
        }

        static int ObBucket(double obliquityDeg, out double fraction)
        {
            var ob = ClampObliquity(obliquityDeg);
            var bucket = Math.Clamp((int)(ob / ComponentObliquityTableStepDeg), 0, ComponentTdTailBucket);
            fraction = bucket < ComponentTdTailBucket ? (ob - bucket * ComponentObliquityTableStepDeg) / ComponentObliquityTableStepDeg : 0d;
            return bucket;
        }

        double InterpVectorByStep(double[] values, double x, double start, double step)
        {
            if (values.Length == 0)
                return 0d;
            if (values.Length == 1)
                return values[0];
            var scaled = (x - start) / step;
            var idx = Math.Clamp((int)Math.Floor(scaled), 0, values.Length - 2);
            var frac = Math.Clamp(scaled - idx, 0d, 1d);
            return Lerp(values[idx], values[idx + 1], frac);
        }

        double PenetrationTripletPostScale(double obliquityDeg)
        {
            var extremeObliquityScale = 1d;
            var useExtremeObliquityScale = options.enableNaabLikeAdaptation || options.enableExtremeObliquityScale;
            if (useExtremeObliquityScale &&
                obliquityDeg >= ExtremeObliquityStartDeg &&
                obliquityDeg < RightAngleObliquityDeg)
            {
                var cosOb = Math.Max(Math.Cos(DegreesToRadians(obliquityDeg)), 0d);
                var cosRef = Math.Max(Math.Cos(DegreesToRadians(ExtremeObliquityStartDeg)), 1e-9d);
                extremeObliquityScale = Math.Pow(cosOb / cosRef, 1.1d);
            }

            var useShellQuality = options.enableNaabLikeAdaptation || options.enableShellQualityPostScale;
            var shellQualityScale = useShellQuality
                ? Math.Clamp(projectile.effectiveShellQuality, 0.2d, 1.2d)
                : 1d;
            return extremeObliquityScale * shellQualityScale;
        }

        static double ClampTd(double td) => Math.Clamp(td, 0.001d, 5.99999d);
        static double ClampObliquity(double ob) => Math.Clamp(ob, 0d, 79.9999d);
        static int TruncatePositiveIndex(double value) => Math.Max(0, (int)value);
        static double Lerp(double a, double b, double t) => a + (b - a) * t;
        static double DegreesToRadians(double degrees) => degrees * Math.PI / 180d;
    }

    static class NaabLikeBallisticsResultExtensions
    {
        public static NaabLikeBallisticsResult WithTrajectory(this NaabLikeBallisticsResult result, IEnumerable<NaabLikeTrajectoryPoint> trajectory)
        {
            result.trajectory.Clear();
            if (trajectory != null)
                result.trajectory.AddRange(trajectory);
            return result;
        }
    }
}
