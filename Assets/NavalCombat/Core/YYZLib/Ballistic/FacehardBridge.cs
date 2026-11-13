using System;

namespace YYZ.Ballistic;

/// <summary>Captures a private projectile/armor template for a single calculation.</summary>
public static class FacehardBridge
{
    public static Func<FacehardBridgeInput, FacehardBridgeResult> Create(FacehardInput template)
    {
        if (template == null) throw new ArgumentNullException(nameof(template));
        var snapshot = Copy(template);
        return impact =>
        {
            var input = Copy(snapshot);
            input.ProjectileDiameter = impact.ProjectileDiameter;
            input.PlateThickness = impact.PlateThickness;
            input.StrikingVelocity = impact.StrikingVelocity;
            input.Obliquity = impact.Obliquity;
            return new FacehardBridgeResult { NavyBl = FacehardCalculator.CalculateFacehardNavyBl(input) };
        };
    }

    private static FacehardInput Copy(FacehardInput source) => new()
    {
        ArmorId = source.ArmorId, ProjectilePresetId = source.ProjectilePresetId,
        PlateThickness = source.PlateThickness, ProjectileDiameter = source.ProjectileDiameter,
        ProjectileWeight = source.ProjectileWeight, ProjectileBodyWeight = source.ProjectileBodyWeight,
        StrikingVelocity = source.StrikingVelocity, Obliquity = source.Obliquity,
        ProjectileLimitQuality = source.ProjectileLimitQuality, ProjectileDamageQuality = source.ProjectileDamageQuality,
        CapType = source.CapType, ShatterResistance = source.ShatterResistance,
        BreakUnderNbl = source.BreakUnderNbl, LightCase = source.LightCase, CurvedPlate = source.CurvedPlate,
        WoodBackingThickness = source.WoodBackingThickness, CementBackingThickness = source.CementBackingThickness,
        MetalBackingThickness = source.MetalBackingThickness, BackingQuality = source.BackingQuality,
        BackingPlates = source.BackingPlates, NoseSchema = source.NoseSchema,
        JapaneseCapHead = source.JapaneseCapHead, NoseCondition = source.NoseCondition,
        WindscreenWeight = source.WindscreenWeight, WindscreenCapHeadWeight = source.WindscreenCapHeadWeight
    };
}
