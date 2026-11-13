using UnityEngine;




using NavalCombat.Application;
using NavalCombatCore;

namespace NavalCombat.Avalonia.Services;

public static class ShipDetailRenderer
{
    public const int CanvasHeight = 250;

    public static ShipClassPlaceholderRenderInput BuildRenderInput(ShipLog ship)
    {
        var shipClass = ship?.shipClass;
        if (shipClass == null)
            return null;

        var input = ShipClassPlaceholderRenderAdapter.CreateRenderInput(shipClass);
        foreach (var mount in input.batteryRecords.SelectMany(battery => battery.mountLocationRecords))
            mount.mountDirectionDeg = double.NaN;
        foreach (var mount in input.torpedoMountRecords)
            mount.mountDirectionDeg = double.NaN;

        for (var batteryIndex = 0; batteryIndex < shipClass.batteryRecords.Count; batteryIndex++)
        {
            var status = batteryIndex < ship.batteryStatus.Count ? ship.batteryStatus[batteryIndex] : null;
            var states = status?.mountStatus.Where(m => !string.IsNullOrEmpty(m.mountRecordId))
                .ToDictionary(m => m.mountRecordId);
            foreach (var record in input.batteryRecords[batteryIndex].mountLocationRecords)
            {
                var mount = states?.GetValueOrDefault(record.objectId ?? "");
                record.mountDirectionDeg = GetTargetDirectionRelativeToBow(ship, mount?.GetFiringTarget());
            }
        }
        var torpedoStates = ship.torpedoSectorStatus.mountStatus.Where(m => !string.IsNullOrEmpty(m.mountRecordId))
            .ToDictionary(m => m.mountRecordId);
        foreach (var record in input.torpedoMountRecords)
        {
            var mount = torpedoStates.GetValueOrDefault(record.objectId ?? "");
            var canTrain = record.trainable && !(record.mountArcs.Count > 0 && record.mountArcs.All(a => a.CoverageDeg <= 30));
            record.mountDirectionDeg = canTrain ? GetTargetDirectionRelativeToBow(ship, mount?.GetFiringTarget()) : double.NaN;
        }
        return input;
    }

    public static Texture2D RenderBitmap(ShipLog ship)
        => RenderBitmap(ship, ShipClassPlaceholderImageRendererCore.RenderLayer.All);

    public static Texture2D RenderClassBitmapFresh(ShipClass shipClass)
    {
        NavalCombat.Avalonia.Controls.ShipLayoutMesh.Invalidate(shipClass);
        return RenderClassBitmap(shipClass);
    }

    public static Texture2D RenderClassBitmap(ShipClass shipClass)
    {
        if (shipClass == null)
            return null;

        var input = ShipClassPlaceholderRenderAdapter.CreateRenderInput(shipClass);
        foreach (var mount in input.batteryRecords.SelectMany(battery => battery.mountLocationRecords))
            mount.mountDirectionDeg = double.NaN;
        foreach (var mount in input.torpedoMountRecords)
            mount.mountDirectionDeg = double.NaN;

        var settings = CreateSettings(input);
        var image = ShipClassPlaceholderImageRendererCore.Render(
            input,
            settings,
            ShipClassPlaceholderImageRendererCore.RenderVariant.Preview,
            ShipClassPlaceholderImageRendererCore.RenderLayer.All);
        return ToBitmap(image);
    }

    public static Texture2D RenderStaticBitmap(ShipLog ship)
        => RenderBitmap(ship, ShipClassPlaceholderImageRendererCore.RenderLayer.StaticShip);

    public static Texture2D RenderWeaponsBitmap(ShipLog ship)
        => RenderBitmap(ship, ShipClassPlaceholderImageRendererCore.RenderLayer.WeaponsAndDetails);

    static Texture2D RenderBitmap(ShipLog ship, ShipClassPlaceholderImageRendererCore.RenderLayer layers)
    {
        var input = BuildRenderInput(ship);
        if (input == null)
            return null;

        var settings = CreateSettings(input);
        var image = ShipClassPlaceholderImageRendererCore.Render(
            input,
            settings,
            ShipClassPlaceholderImageRendererCore.RenderVariant.Preview,
            layers);
        return ToBitmap(image);
    }

    internal static ShipClassPlaceholderImageRenderSettings CreateSettings(ShipClassPlaceholderRenderInput input)
    {
        var settings = new ShipClassPlaceholderImageRenderSettings
        {
            canvasWidth = 1200,
            canvasHeight = CanvasHeight,
            hullPadding = 18,
            lineWidth = 3,
            deckInsetAmount = 10,
            superstructureHeightScale = 1d,
            funnelCountMode = PlaceholderFunnelCountMode.Auto,
            funnelSpacingBias = 0d,
            bowSharpness = 1.15d,
            sternFullness = 1.05d,
            weaponScale = 1d
        };
        settings.canvasWidth = Math.Clamp(
            ShipClassPlaceholderImageRendererCore.CalculateRecommendedCanvasWidth(input, settings),
            320,
            4096);
        return settings;
    }

    public static string BuildDirectionSignature(ShipLog ship)
    {
        var input = BuildRenderInput(ship);
        if (input == null)
            return string.Empty;
        static string Directions(IEnumerable<PlaceholderMountRenderRecord> records) => string.Join(
            ',',
            records.Select(record => record.mountDirectionDeg)
                .Select(direction => double.IsFinite(direction) ? Math.Round(direction).ToString("0") : "-"));
        return $"{ship.shipClass?.objectId}|{Directions(input.batteryRecords.SelectMany(record => record.mountLocationRecords))}|{Directions(input.torpedoMountRecords)}";
    }

    static Texture2D ToBitmap(RgbaImage image)
    {
        var bitmap = new Texture2D(image.width,image.height,TextureFormat.RGBA32,false);
        var flipped=new byte[image.pixels.Length];
        for(int row=0;row<image.height;row++)System.Array.Copy(image.pixels,row*image.width*4,flipped,(image.height-1-row)*image.width*4,image.width*4);
        bitmap.LoadRawTextureData(flipped);bitmap.Apply();
        return bitmap;
    }

    internal static double GetTargetDirectionRelativeToBow(ShipLog ship, ShipLog target)
    {
        if (target == null)
            return double.NaN;
        var (_, bearingDeg) = MeasureStats.Approximation.CalculateDistanceKmAndBearingDeg(ship, target);
        return MeasureUtils.NormalizeAngle(bearingDeg - ship.headingDeg);
    }

    static bool IsSubmerged(TorpedoMountRecord record) =>
        record.mountArcs != null
        && record.mountArcs.Count > 0
        && record.mountArcs.All(arc => arc.CoverageDeg <= 30d);

}
