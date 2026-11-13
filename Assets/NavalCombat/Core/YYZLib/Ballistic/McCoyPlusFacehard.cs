using System;
using System.Collections.Generic;

namespace YYZ.Ballistic
{
    public sealed class McCoyPlusFacehardInput
    {
        public McCoyPlusInput McCoy { get; set; } = McCoyPlus.DefaultInput();

        public FacehardBridgeInput Facehard { get; set; } = new FacehardBridgeInput();
    }

    public sealed class FacehardBridgeInput
    {
        public double ProjectileDiameter { get; set; } = 8;

        public double PlateThickness { get; set; } = 8;

        public double StrikingVelocity { get; set; } = 1500;

        public double Obliquity { get; set; }
    }

    public sealed class FacehardBridgeResult
    {
        public double NavyBl { get; set; }
    }

    public sealed class McCoyPlusFacehardRow : McCoyPlusRow
    {
        public double? PenetrationInches { get; set; }

        public double? HorizontalPenetrationInches { get; set; }

        public double? FacehardNavyBl { get; set; }

        public double FacehardObliquity { get; set; }
    }

    public sealed class McCoyPlusFacehardResult
    {
        public List<McCoyPlusFacehardRow> Rows { get; set; } = new List<McCoyPlusFacehardRow>();

        public List<McCoyPlusFacehardRow> ChartRows { get; set; } = new List<McCoyPlusFacehardRow>();

        public List<string> Warnings { get; set; } = new List<string>();
    }

    public sealed class McCoyPlusFacehardSolvedThickness
    {
        public double? PenetrationInches;
        public double? FacehardNavyBl;
        public double FacehardObliquity;
        public string Warning;
    }

    public static class McCoyPlusFacehard
    {
        const double MaxThicknessInches = 80;
        const double ThicknessToleranceFps = 0.5;
        const int MaxSearchIterations = 42;

        sealed class NavyBlResult
        {
            public double NavyBl;
            public double Obliquity;
        }

        public static McCoyPlusFacehardInput DefaultInput()
        {
            return new McCoyPlusFacehardInput();
        }

        public static McCoyPlusFacehardResult Calculate(McCoyPlusFacehardInput input)
        {
            return Calculate(input, null);
        }

        public static McCoyPlusFacehardResult Calculate(McCoyPlusFacehardInput input, IEnumerable<double> targetRanges, Func<FacehardBridgeInput, FacehardBridgeResult> calculator = null, CancellationToken cancellationToken = default, IProgress<BallisticProgress> progress = null)
        {
            calculator ??= FacehardBridge.Create(FacehardInput.CreateDefault());

            var source = input ?? new McCoyPlusFacehardInput();
            var trajectory = targetRanges == null
                ? McCoyPlus.CalculateParallel(source.McCoy, cancellationToken: cancellationToken, progress: progress)
                : McCoyPlus.CalculateTargetsParallel(source.McCoy, targetRanges, cancellationToken: cancellationToken, progress: progress);
            var warnings = new List<string>(trajectory.Warnings);
            var rows = new List<McCoyPlusFacehardRow>();
            progress?.Report(new(BallisticStage.Penetration, 0, trajectory.Rows.Count));
            foreach (var row in trajectory.Rows)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var solved = SolvePenetrationThicknessForRow(source.Facehard, row, row.FallAngleDegrees, calculator);
                var horizontalSolved = SolvePenetrationThicknessForRow(source.Facehard, row, 90 - row.FallAngleDegrees, calculator, true);
                if (solved.Warning != null)
                {
                    warnings.Add(solved.Warning);
                }

                rows.Add(new McCoyPlusFacehardRow
                {
                    Range = row.Range,
                    Time = row.Time,
                    ElevationDegrees = row.ElevationDegrees,
                    Velocity = row.Velocity,
                    FallAngleDegrees = row.FallAngleDegrees,
                    Trajectory = row.Trajectory,
                    PenetrationInches = solved.PenetrationInches,
                    HorizontalPenetrationInches = horizontalSolved.PenetrationInches,
                    FacehardNavyBl = solved.FacehardNavyBl,
                    FacehardObliquity = solved.FacehardObliquity,
                });
                progress?.Report(new(BallisticStage.Penetration, rows.Count, trajectory.Rows.Count));
            }

            return new McCoyPlusFacehardResult
            {
                Rows = rows,
                ChartRows = McCoyPlus.SelectChartRows(rows),
                Warnings = BallisticCollections.DistinctStrings(warnings),
            };
        }

        static NavyBlResult FacehardNavyBlAtThickness(FacehardBridgeInput baseInput, McCoyPlusRow row, double thickness, double obliquity, Func<FacehardBridgeInput, FacehardBridgeResult> calculator)
        {
            var result = calculator(new FacehardBridgeInput
            {
                ProjectileDiameter = baseInput.ProjectileDiameter,
                PlateThickness = thickness,
                StrikingVelocity = row.Velocity,
                Obliquity = obliquity,
            });
            return new NavyBlResult { NavyBl = result.NavyBl, Obliquity = obliquity };
        }

        public static McCoyPlusFacehardSolvedThickness SolvePenetrationThicknessForRow(FacehardBridgeInput baseInput, McCoyPlusRow row, double impactObliquity, Func<FacehardBridgeInput, FacehardBridgeResult> calculator, bool rejectAboveLimit = false)
        {
            if (rejectAboveLimit && impactObliquity > 80)
            {
                return new McCoyPlusFacehardSolvedThickness
                {
                    PenetrationInches = null,
                    FacehardNavyBl = null,
                    FacehardObliquity = impactObliquity,
                    Warning = null,
                };
            }

            var obliquity = Math.Min(Math.Max(impactObliquity, 0), 80);
            var low = 0.1;
            var high = Math.Max(Math.Max(baseInput.ProjectileDiameter * 2, baseInput.PlateThickness), 1);
            var highResult = FacehardNavyBlAtThickness(baseInput, row, high, obliquity, calculator);

            while (highResult.NavyBl < row.Velocity && high < MaxThicknessInches)
            {
                low = high;
                high = Math.Min(high * 1.5, MaxThicknessInches);
                highResult = FacehardNavyBlAtThickness(baseInput, row, high, obliquity, calculator);
            }

            if (highResult.NavyBl < row.Velocity)
            {
                return new McCoyPlusFacehardSolvedThickness
                {
                    PenetrationInches = null,
                    FacehardNavyBl = highResult.NavyBl,
                    FacehardObliquity = highResult.Obliquity,
                    Warning = $"No Facehard thickness up to {BallisticText.ToJsString(MaxThicknessInches)} in matched {BallisticText.Fixed(row.Velocity, 0)} ft/s at {BallisticText.ToJsString(row.Range)}.",
                };
            }

            var bestThickness = high;
            var bestNavyBl = highResult.NavyBl;
            var bestObliquity = highResult.Obliquity;

            for (var index = 0; index < MaxSearchIterations; index += 1)
            {
                var mid = (low + high) / 2;
                var midResult = FacehardNavyBlAtThickness(baseInput, row, mid, obliquity, calculator);
                bestThickness = mid;
                bestNavyBl = midResult.NavyBl;
                bestObliquity = midResult.Obliquity;

                if (Math.Abs(midResult.NavyBl - row.Velocity) <= ThicknessToleranceFps)
                {
                    break;
                }

                if (midResult.NavyBl < row.Velocity)
                {
                    low = mid;
                }
                else
                {
                    high = mid;
                }
            }

            return new McCoyPlusFacehardSolvedThickness
            {
                PenetrationInches = bestThickness,
                FacehardNavyBl = bestNavyBl,
                FacehardObliquity = bestObliquity,
                Warning = null,
            };
        }
    }
}
