using System;
using System.Collections.Generic;

namespace NavalCombatCore
{
    public enum ExternalBallisticsDragModel
    {
        G1,
        G7
    }

    public enum ExternalBallisticsDragInputMode
    {
        GModelBallisticCoefficient,
        PhysicalCd
    }

    public sealed class ExternalBallisticsInput
    {
        public double muzzleVelocityMetersPerSecond = 730d;
        public double elevationAngleDeg = 15d;
        public ExternalBallisticsDragInputMode dragInputMode = ExternalBallisticsDragInputMode.GModelBallisticCoefficient;
        public double projectileDiameterMeters = 0.3048d;
        public double projectileMassKg = 386d;
        public double ballisticCoefficient = 0.5d;
        public ExternalBallisticsDragModel dragModel = ExternalBallisticsDragModel.G1;
        public double constantDragCoefficient = 0.3d;
        public double airDensityKgPerCubicMeter = 1.225d;
        public double speedOfSoundMetersPerSecond = 340.29d;
        public double timeStepSeconds = 0.02d;
        public double gravityMetersPerSecondSquared = 9.80665d;
        public double maxSimulationSeconds = 240d;
    }

    public sealed class ExternalBallisticsTrajectoryPoint
    {
        public double timeSeconds;
        public double xMeters;
        public double yMeters;
        public double velocityXMetersPerSecond;
        public double velocityYMetersPerSecond;

        public double speedMetersPerSecond => Math.Sqrt(
            velocityXMetersPerSecond * velocityXMetersPerSecond +
            velocityYMetersPerSecond * velocityYMetersPerSecond);
    }

    public sealed class ExternalBallisticsResult
    {
        public bool success;
        public string failureReason;
        public double elevationAngleDeg;
        public double rangeMeters;
        public double timeOfFlightSeconds;
        public double impactVelocityMetersPerSecond;
        public double angleOfFallDeg;
        public List<ExternalBallisticsTrajectoryPoint> trajectory = new();
    }

    public static class ExternalBallisticsSolver
    {
        const double InchesPerMeter = 39.3700787d;
        const double FeetPerMeter = 3.2808399d;
        const double PoundsPerKilogram = 2.20462262d;

        public static ExternalBallisticsResult Solve(ExternalBallisticsInput input)
        {
            var validationError = Validate(input);
            if (validationError != null)
            {
                return new ExternalBallisticsResult
                {
                    success = false,
                    failureReason = validationError,
                    elevationAngleDeg = input?.elevationAngleDeg ?? 0d
                };
            }

            var result = new ExternalBallisticsResult
            {
                success = true,
                elevationAngleDeg = input.elevationAngleDeg
            };

            var angleRad = input.elevationAngleDeg * Math.PI / 180d;
            var current = new ExternalBallisticsTrajectoryPoint
            {
                timeSeconds = 0d,
                xMeters = 0d,
                yMeters = 0d,
                velocityXMetersPerSecond = input.muzzleVelocityMetersPerSecond * Math.Cos(angleRad),
                velocityYMetersPerSecond = input.muzzleVelocityMetersPerSecond * Math.Sin(angleRad)
            };

            result.trajectory.Add(ClonePoint(current));

            var stepCount = Math.Max(1, (int)Math.Ceiling(input.maxSimulationSeconds / input.timeStepSeconds));
            for (int stepIndex = 0; stepIndex < stepCount; stepIndex++)
            {
                var previous = current;
                current = Step(previous, input);

                if (current.yMeters <= 0d && current.timeSeconds > 0d)
                {
                    var impact = InterpolateImpact(previous, current);
                    result.trajectory.Add(impact);
                    result.rangeMeters = impact.xMeters;
                    result.timeOfFlightSeconds = impact.timeSeconds;
                    result.impactVelocityMetersPerSecond = impact.speedMetersPerSecond;
                    result.angleOfFallDeg = Math.Atan2(
                        Math.Max(0d, -impact.velocityYMetersPerSecond),
                        Math.Max(0.0001d, impact.velocityXMetersPerSecond)) * 180d / Math.PI;
                    return result;
                }

                result.trajectory.Add(current);
            }

            result.success = false;
            result.failureReason = "Projectile did not reach ground before the simulation time limit.";
            return result;
        }

        static string Validate(ExternalBallisticsInput input)
        {
            if (input == null)
                return "Input is missing.";
            if (!IsFinitePositive(input.muzzleVelocityMetersPerSecond))
                return "Muzzle velocity must be greater than 0.";
            if (!IsFinitePositive(input.projectileDiameterMeters))
                return "Projectile diameter must be greater than 0.";
            if (!IsFinitePositive(input.projectileMassKg))
                return "Projectile mass must be greater than 0.";
            if (input.dragInputMode == ExternalBallisticsDragInputMode.GModelBallisticCoefficient && !IsFinitePositive(input.ballisticCoefficient))
                return "Ballistic coefficient must be greater than 0.";
            if (input.dragInputMode == ExternalBallisticsDragInputMode.PhysicalCd && !IsFinitePositive(input.constantDragCoefficient))
                return "Drag coefficient must be greater than 0.";
            if (!IsFinitePositive(input.airDensityKgPerCubicMeter))
                return "Air density must be greater than 0.";
            if (!IsFinitePositive(input.speedOfSoundMetersPerSecond))
                return "Speed of sound must be greater than 0.";
            if (!IsFinitePositive(input.timeStepSeconds))
                return "Time step must be greater than 0.";
            if (!IsFinitePositive(input.gravityMetersPerSecondSquared))
                return "Gravity must be greater than 0.";
            if (!double.IsFinite(input.elevationAngleDeg) || input.elevationAngleDeg <= 0d || input.elevationAngleDeg >= 90d)
                return "Elevation angle must be greater than 0 and less than 90 degrees.";
            return null;
        }

        static bool IsFinitePositive(double value) => !double.IsNaN(value) && !double.IsInfinity(value) && value > 0d;

        static ExternalBallisticsTrajectoryPoint Step(ExternalBallisticsTrajectoryPoint point, ExternalBallisticsInput input)
        {
            var speed = Math.Max(0.0001d, point.speedMetersPerSecond);
            var dragAcceleration = GetDragAccelerationMetersPerSecondSquared(speed, input);
            var dragX = dragAcceleration * point.velocityXMetersPerSecond / speed;
            var dragY = dragAcceleration * point.velocityYMetersPerSecond / speed;

            var nextVelocityX = point.velocityXMetersPerSecond - dragX * input.timeStepSeconds;
            var nextVelocityY = point.velocityYMetersPerSecond - (input.gravityMetersPerSecondSquared + dragY) * input.timeStepSeconds;

            return new ExternalBallisticsTrajectoryPoint
            {
                timeSeconds = point.timeSeconds + input.timeStepSeconds,
                xMeters = point.xMeters + (point.velocityXMetersPerSecond + nextVelocityX) * 0.5d * input.timeStepSeconds,
                yMeters = point.yMeters + (point.velocityYMetersPerSecond + nextVelocityY) * 0.5d * input.timeStepSeconds,
                velocityXMetersPerSecond = nextVelocityX,
                velocityYMetersPerSecond = nextVelocityY
            };
        }

        static double GetDragAccelerationMetersPerSecondSquared(double speedMetersPerSecond, ExternalBallisticsInput input)
        {
            if (input.dragInputMode == ExternalBallisticsDragInputMode.PhysicalCd)
            {
                var projectileArea = Math.PI * input.projectileDiameterMeters * input.projectileDiameterMeters * 0.25d;
                return 0.5d * input.airDensityKgPerCubicMeter * speedMetersPerSecond * speedMetersPerSecond *
                    input.constantDragCoefficient * projectileArea / input.projectileMassKg;
            }

            var mach = speedMetersPerSecond / input.speedOfSoundMetersPerSecond;
            var dragCoefficient = GetReferenceDragCoefficient(input.dragModel, mach);
            var sectionalDensity = KilogramsToPounds(input.projectileMassKg) /
                (MetersToInches(input.projectileDiameterMeters) * MetersToInches(input.projectileDiameterMeters));
            var formFactor = sectionalDensity / input.ballisticCoefficient;
            var effectiveCoefficient = dragCoefficient * formFactor;
            var gModelProjectileArea = Math.PI * input.projectileDiameterMeters * input.projectileDiameterMeters * 0.25d;
            return 0.5d * input.airDensityKgPerCubicMeter * speedMetersPerSecond * speedMetersPerSecond *
                effectiveCoefficient * gModelProjectileArea / input.projectileMassKg;
        }

        static double GetReferenceDragCoefficient(ExternalBallisticsDragModel dragModel, double mach)
        {
            return dragModel switch
            {
                ExternalBallisticsDragModel.G7 => InterpolateDragCoefficient(mach, G7DragTable),
                _ => InterpolateDragCoefficient(mach, G1DragTable)
            };
        }

        static double InterpolateDragCoefficient(double mach, (double mach, double cd)[] table)
        {
            if (mach <= table[0].mach)
                return table[0].cd;

            for (int i = 1; i < table.Length; i++)
            {
                if (mach <= table[i].mach)
                {
                    var prev = table[i - 1];
                    var next = table[i];
                    var ratio = (mach - prev.mach) / (next.mach - prev.mach);
                    return prev.cd + (next.cd - prev.cd) * ratio;
                }
            }

            return table[^1].cd;
        }

        static ExternalBallisticsTrajectoryPoint InterpolateImpact(
            ExternalBallisticsTrajectoryPoint previous,
            ExternalBallisticsTrajectoryPoint current)
        {
            var ratio = previous.yMeters / Math.Max(0.0001d, previous.yMeters - current.yMeters);
            ratio = Math.Clamp(ratio, 0d, 1d);

            return new ExternalBallisticsTrajectoryPoint
            {
                timeSeconds = Lerp(previous.timeSeconds, current.timeSeconds, ratio),
                xMeters = Lerp(previous.xMeters, current.xMeters, ratio),
                yMeters = 0d,
                velocityXMetersPerSecond = Lerp(previous.velocityXMetersPerSecond, current.velocityXMetersPerSecond, ratio),
                velocityYMetersPerSecond = Lerp(previous.velocityYMetersPerSecond, current.velocityYMetersPerSecond, ratio)
            };
        }

        static ExternalBallisticsTrajectoryPoint ClonePoint(ExternalBallisticsTrajectoryPoint point)
        {
            return new ExternalBallisticsTrajectoryPoint
            {
                timeSeconds = point.timeSeconds,
                xMeters = point.xMeters,
                yMeters = point.yMeters,
                velocityXMetersPerSecond = point.velocityXMetersPerSecond,
                velocityYMetersPerSecond = point.velocityYMetersPerSecond
            };
        }

        static double Lerp(double a, double b, double ratio) => a + (b - a) * ratio;

        public static double MetersToYards(double meters) => meters * 1.0936133d;
        public static double YardsToMeters(double yards) => yards / 1.0936133d;
        public static double InchesToMeters(double inches) => inches / InchesPerMeter;
        public static double MetersToInches(double meters) => meters * InchesPerMeter;
        public static double MetersPerSecondToFeetPerSecond(double metersPerSecond) => metersPerSecond * FeetPerMeter;
        public static double FeetPerSecondToMetersPerSecond(double feetPerSecond) => feetPerSecond / FeetPerMeter;
        public static double KilogramsToPounds(double kilograms) => kilograms * PoundsPerKilogram;
        public static double PoundsToKilograms(double pounds) => pounds / PoundsPerKilogram;

        static readonly (double mach, double cd)[] G1DragTable =
        {
            (0.00d, 0.262d),
            (0.50d, 0.255d),
            (0.70d, 0.269d),
            (0.85d, 0.319d),
            (0.95d, 0.480d),
            (1.05d, 0.620d),
            (1.20d, 0.590d),
            (1.50d, 0.500d),
            (2.00d, 0.410d),
            (2.50d, 0.360d),
            (3.00d, 0.330d),
            (4.00d, 0.300d)
        };

        static readonly (double mach, double cd)[] G7DragTable =
        {
            (0.00d, 0.120d),
            (0.50d, 0.119d),
            (0.70d, 0.125d),
            (0.85d, 0.150d),
            (0.95d, 0.290d),
            (1.05d, 0.380d),
            (1.20d, 0.360d),
            (1.50d, 0.300d),
            (2.00d, 0.245d),
            (2.50d, 0.215d),
            (3.00d, 0.195d),
            (4.00d, 0.180d)
        };
    }
}
