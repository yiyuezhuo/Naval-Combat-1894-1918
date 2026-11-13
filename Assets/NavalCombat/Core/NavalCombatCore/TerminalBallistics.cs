using System;

namespace NavalCombatCore
{
    public enum TerminalBallisticsFormulaPreset
    {
        DeMarreNickelSteel,
        KruppAllPurpose,
        Custom
    }

    public sealed class TerminalBallisticsFormulaParameters
    {
        public TerminalBallisticsFormulaPreset preset = TerminalBallisticsFormulaPreset.DeMarreNickelSteel;
        public string name = "De Marre Nickel-Steel";
        public double numericalConstant = 0.00005021d;
        public double projectileDiameterExponent = 0.07144d;
        public double energyDensityExponent = 0.71429d;
        public double coefficient = 1d;
        public double obliquityCosineExponent = 3d;

        public TerminalBallisticsFormulaParameters Clone()
        {
            return new TerminalBallisticsFormulaParameters
            {
                preset = preset,
                name = name,
                numericalConstant = numericalConstant,
                projectileDiameterExponent = projectileDiameterExponent,
                energyDensityExponent = energyDensityExponent,
                coefficient = coefficient,
                obliquityCosineExponent = obliquityCosineExponent
            };
        }

        public static TerminalBallisticsFormulaParameters ForPreset(TerminalBallisticsFormulaPreset preset)
        {
            return preset switch
            {
                TerminalBallisticsFormulaPreset.KruppAllPurpose => new TerminalBallisticsFormulaParameters
                {
                    preset = preset,
                    name = "Krupp All-Purpose",
                    numericalConstant = 0.30386d,
                    projectileDiameterExponent = 0.25d,
                    energyDensityExponent = 0.625d,
                    coefficient = 655d,
                    obliquityCosineExponent = 0d
                },
                TerminalBallisticsFormulaPreset.Custom => new TerminalBallisticsFormulaParameters
                {
                    preset = preset,
                    name = "Custom",
                    numericalConstant = 0.00005021d,
                    projectileDiameterExponent = 0.07144d,
                    energyDensityExponent = 0.71429d,
                    coefficient = 1d,
                    obliquityCosineExponent = 3d
                },
                _ => new TerminalBallisticsFormulaParameters()
            };
        }
    }

    public sealed class TerminalBallisticsInput
    {
        public double projectileMassKg = 386d;
        public double projectileDiameterInches = 12d;
        public double impactVelocityMetersPerSecond = 500d;
        public double angleOfFallDeg = 10d;
        public double projectileQualityFactor = 1d;
        public TerminalBallisticsFormulaParameters formulaParameters = TerminalBallisticsFormulaParameters.ForPreset(TerminalBallisticsFormulaPreset.DeMarreNickelSteel);
    }

    public sealed class TerminalBallisticsResult
    {
        public bool success;
        public string failureReason;
        public double impactVelocityMetersPerSecond;
        public double angleOfFallDeg;
        public double verticalObliquityDeg;
        public double horizontalObliquityDeg;
        public double verticalPenetrationInches;
        public double horizontalPenetrationInches;
    }

    public static class TerminalBallisticsSolver
    {
        const double FeetPerMeter = 3.2808399d;
        const double PoundsPerKilogram = 2.20462262d;
        const double MillimetersPerInch = 25.4d;

        public static TerminalBallisticsResult Solve(TerminalBallisticsInput input)
        {
            var validationError = Validate(input);
            if (validationError != null)
            {
                return new TerminalBallisticsResult
                {
                    success = false,
                    failureReason = validationError,
                    impactVelocityMetersPerSecond = input?.impactVelocityMetersPerSecond ?? 0d,
                    angleOfFallDeg = input?.angleOfFallDeg ?? 0d
                };
            }

            var verticalObliquity = input.angleOfFallDeg;
            var horizontalObliquity = 90d - input.angleOfFallDeg;

            return new TerminalBallisticsResult
            {
                success = true,
                impactVelocityMetersPerSecond = input.impactVelocityMetersPerSecond,
                angleOfFallDeg = input.angleOfFallDeg,
                verticalObliquityDeg = verticalObliquity,
                horizontalObliquityDeg = horizontalObliquity,
                verticalPenetrationInches = CalculatePenetrationInches(input, verticalObliquity),
                horizontalPenetrationInches = CalculatePenetrationInches(input, horizontalObliquity)
            };
        }

        static string Validate(TerminalBallisticsInput input)
        {
            if (input == null)
                return "Input is missing.";
            if (!IsFinitePositive(input.projectileMassKg))
                return "Projectile mass must be greater than 0.";
            if (!IsFinitePositive(input.projectileDiameterInches))
                return "Projectile diameter must be greater than 0.";
            if (!IsFinitePositive(input.impactVelocityMetersPerSecond))
                return "Impact velocity must be greater than 0.";
            if (!double.IsFinite(input.angleOfFallDeg) || input.angleOfFallDeg < 0d || input.angleOfFallDeg > 90d)
                return "Angle of fall must be between 0 and 90 degrees.";
            if (!IsFinitePositive(input.projectileQualityFactor))
                return "Projectile quality factor must be greater than 0.";

            var parameters = input.formulaParameters;
            if (parameters == null)
                return "Formula parameters are missing.";
            if (!IsFinitePositive(parameters.numericalConstant))
                return "Formula constant must be greater than 0.";
            if (!double.IsFinite(parameters.projectileDiameterExponent))
                return "Projectile diameter exponent must be finite.";
            if (!IsFinitePositive(parameters.energyDensityExponent))
                return "Energy density exponent must be greater than 0.";
            if (!IsFinitePositive(parameters.coefficient))
                return "Formula coefficient must be greater than 0.";
            if (!double.IsFinite(parameters.obliquityCosineExponent) || parameters.obliquityCosineExponent < 0d)
                return "Obliquity cosine exponent must be 0 or greater.";
            return null;
        }

        static double CalculatePenetrationInches(TerminalBallisticsInput input, double obliquityDeg)
        {
            var parameters = input.formulaParameters;
            var diameterInches = input.projectileDiameterInches;
            var weightPounds = KilogramsToPounds(input.projectileMassKg);
            var velocityFeetPerSecond = MetersPerSecondToFeetPerSecond(input.impactVelocityMetersPerSecond);

            var energyDensity = weightPounds / Math.Pow(diameterInches, 3d);
            var velocityRatio = velocityFeetPerSecond / parameters.coefficient;
            var obliquityMultiplier = GetObliquityMultiplier(obliquityDeg, parameters.obliquityCosineExponent);
            var innerTerm = energyDensity * velocityRatio * velocityRatio * obliquityMultiplier * input.projectileQualityFactor;
            if (innerTerm <= 0d)
                return 0d;

            var thicknessOverDiameter =
                parameters.numericalConstant *
                Math.Pow(diameterInches, parameters.projectileDiameterExponent) *
                Math.Pow(innerTerm, parameters.energyDensityExponent);
            return (double)(thicknessOverDiameter * diameterInches);
        }

        static double GetObliquityMultiplier(double obliquityDeg, double cosineExponent)
        {
            if (cosineExponent <= 0d)
                return 1d;

            var radians = Math.Clamp(obliquityDeg, 0d, 90d) * Math.PI / 180d;
            var cosine = Math.Max(0d, Math.Cos(radians));
            return Math.Pow(cosine, cosineExponent);
        }

        static bool IsFinitePositive(double value) => !double.IsNaN(value) && !double.IsInfinity(value) && value > 0d;

        public static double MetersPerSecondToFeetPerSecond(double metersPerSecond) => metersPerSecond * FeetPerMeter;
        public static double FeetPerSecondToMetersPerSecond(double feetPerSecond) => feetPerSecond / FeetPerMeter;
        public static double KilogramsToPounds(double kilograms) => kilograms * PoundsPerKilogram;
        public static double PoundsToKilograms(double pounds) => pounds / PoundsPerKilogram;
        public static double InchesToMillimeters(double inches) => inches * MillimetersPerInch;
        public static double MillimetersToInches(double millimeters) => millimeters / MillimetersPerInch;
    }
}
