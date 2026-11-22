using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

public enum PlaceholderFunnelCountMode
{
    Auto,
    Zero,
    One,
    Two,
    Three
}

public enum PlaceholderShipType
{
    NotSpecified,
    Battleship,
    Battlecruiser,
    LightCruiser,
    ArmoredCruiser,
    Destroyer,
    PatrolGunboat,
    TorpedoBoat,
    ArmedMerchantCruiser,
    Transport,
    Repair,
    LandBattery
}

public enum PlaceholderMountLocation
{
    NotSpecified,
    PortForward,
    Forward,
    StarboardForward,
    PortMidship,
    Midship,
    StarboardMidship,
    PortAfter,
    After,
    StarboardAfter,
}

public sealed class ShipClassPlaceholderRenderInput
{
    public ShipGraphicParameters graphicParameters;
    public string name;
    public PlaceholderShipType type;
    public double displacementTons;
    public double lengthFoot;
    public double beamFoot;
    public double speedKnots;
    public List<PlaceholderBatteryRenderRecord> batteryRecords = new();
    public List<PlaceholderMountRenderRecord> torpedoMountRecords = new();
    public double torpedoDiameterIn;
    public List<PlaceholderRapidFireRenderRecord> rapidFireBatteryRecords = new();
}

public sealed class PlaceholderBatteryRenderRecord
{
    public string objectId;
    public double shellSizeInch;
    public double barrelLengthCalibers;
    public List<PlaceholderMountRenderRecord> mountLocationRecords = new();
}

public sealed class PlaceholderMountRenderRecord
{
    public string objectId;
    public bool casemate;
    public bool fixedMount;
    public PlaceholderMountLocation mountLocation;
    public int barrels;

    public List<PlaceholderMountArcRenderRecord> mountArcs = new();
    public bool trainable;
    public TorpedoGraphicParameters torpedoGraphic;
    public double mountDirectionDeg = double.NaN;
}

public sealed class PlaceholderMountArcRenderRecord
{
    public double startDeg;
    public double CoverageDeg;
    public bool isCrossDeckFire;
}

public sealed class PlaceholderRapidFireRenderRecord
{
    public List<int> barrelsLevelPort = new();
    public List<int> barrelsLevelStarboard = new();
}

public readonly struct RgbaImage
{
    public readonly int width;
    public readonly int height;
    public readonly byte[] pixels;

    public RgbaImage(int width, int height, byte[] pixels)
    {
        this.width = width;
        this.height = height;
        this.pixels = pixels ?? Array.Empty<byte>();
    }
}

public readonly struct RgbaColor
{
    public readonly byte r;
    public readonly byte g;
    public readonly byte b;
    public readonly byte a;

    public RgbaColor(byte r, byte g, byte b, byte a)
    {
        this.r = r;
        this.g = g;
        this.b = b;
        this.a = a;
    }
}

readonly struct PixelVector2
{
    public readonly double x;
    public readonly double y;

    public PixelVector2(double x, double y)
    {
        this.x = x;
        this.y = y;
    }

    public double sqrMagnitude => x * x + y * y;

    public PixelVector2 normalized
    {
        get
        {
            var magnitude = Math.Sqrt(sqrMagnitude);
            return magnitude <= 0.0001d ? zero : new PixelVector2(x / magnitude, y / magnitude);
        }
    }

    public static PixelVector2 zero => new(0d, 0d);
    public static PixelVector2 right => new(1d, 0d);
    public static PixelVector2 left => new(-1d, 0d);
    public static PixelVector2 up => new(0d, 1d);
    public static PixelVector2 down => new(0d, -1d);

    public static PixelVector2 operator +(PixelVector2 lhs, PixelVector2 rhs) => new(lhs.x + rhs.x, lhs.y + rhs.y);
    public static PixelVector2 operator -(PixelVector2 lhs, PixelVector2 rhs) => new(lhs.x - rhs.x, lhs.y - rhs.y);
    public static PixelVector2 operator *(PixelVector2 lhs, double rhs) => new(lhs.x * rhs, lhs.y * rhs);
    public static PixelVector2 operator *(double lhs, PixelVector2 rhs) => rhs * lhs;

    public static double Distance(PixelVector2 lhs, PixelVector2 rhs)
    {
        var delta = lhs - rhs;
        return Math.Sqrt(delta.sqrMagnitude);
    }

    public static PixelVector2 Lerp(PixelVector2 lhs, PixelVector2 rhs, double t)
    {
        t = PixelMath.Clamp01(t);
        return new PixelVector2(PixelMath.Lerp(lhs.x, rhs.x, t), PixelMath.Lerp(lhs.y, rhs.y, t));
    }
}

readonly struct PixelRect
{
    readonly double x;
    readonly double y;

    public readonly double width;
    public readonly double height;

    public PixelRect(double x, double y, double width, double height)
    {
        this.x = x;
        this.y = y;
        this.width = width;
        this.height = height;
    }

    public double xMin => x;
    public double xMax => x + width;
    public double yMin => y;
    public double yMax => y + height;
    public PixelVector2 center => new(x + width / 2d, y + height / 2d);
    public PixelVector2 size => new(width, height);

    public bool Overlaps(PixelRect other)
    {
        return other.xMax > xMin && other.xMin < xMax && other.yMax > yMin && other.yMin < yMax;
    }
}

readonly struct PixelRectInt
{
    readonly int x;
    readonly int y;
    readonly int width;
    readonly int height;

    public PixelRectInt(int x, int y, int width, int height)
    {
        this.x = x;
        this.y = y;
        this.width = width;
        this.height = height;
    }

    public int xMin => x;
    public int xMax => x + width;
    public int yMin => y;
    public int yMax => y + height;
}

static class PixelMath
{
    public const double PI = Math.PI;
    public const double Deg2Rad = Math.PI / 180d;
    public const double Rad2Deg = 180d / Math.PI;

    public static int Clamp(int value, int min, int max) => Math.Min(Math.Max(value, min), max);
    public static double Clamp(double value, double min, double max) => Math.Min(Math.Max(value, min), max);
    public static double Clamp01(double value) => Clamp(value, 0d, 1d);
    public static int Max(int lhs, int rhs) => Math.Max(lhs, rhs);
    public static double Max(double lhs, double rhs) => Math.Max(lhs, rhs);
    public static int Min(int lhs, int rhs) => Math.Min(lhs, rhs);
    public static double Min(double lhs, double rhs) => Math.Min(lhs, rhs);
    public static int FloorToInt(double value) => (int)Math.Floor(value);
    public static int CeilToInt(double value) => (int)Math.Ceiling(value);
    public static int RoundToInt(double value) => (int)Math.Round(value);
    public static double Sin(double value) => Math.Sin(value);
    public static double Cos(double value) => Math.Cos(value);
    public static double Atan2(double y, double x) => Math.Atan2(y, x);
    public static double Pow(double value, double power) => Math.Pow(value, power);
    public static double Abs(double value) => Math.Abs(value);
    public static double Sign(double value) => value >= 0d ? 1d : -1d;

    public static double Lerp(double lhs, double rhs, double t)
    {
        t = Clamp01(t);
        return lhs + (rhs - lhs) * t;
    }

    public static double InverseLerp(double lhs, double rhs, double value)
    {
        if (Math.Abs(rhs - lhs) <= 0.000001d)
            return 0d;
        return Clamp01((value - lhs) / (rhs - lhs));
    }
}

public struct ShipClassPlaceholderImageRenderSettings
{
    public int canvasWidth;
    public int canvasHeight;
    public int hullPadding;
    public int lineWidth;
    public int deckInsetAmount;
    public double superstructureHeightScale;
    public PlaceholderFunnelCountMode funnelCountMode;
    public double funnelSpacingBias;
    public double bowSharpness;
    public double sternFullness;
    public double weaponScale;
}

public static class ShipClassPlaceholderImageRendererCore
{
    const double TrainableTorpedoLauncherFootprintFeet = 6.12d;
    const double AdditionalTrainableTorpedoTubeFootprintFeet = 2d;

    public enum RenderVariant
    {
        Preview,
        TopJpg,
        IconPng,
    }

    [Flags]
    public enum RenderLayer
    {
        StaticShip = 1,
        WeaponsAndDetails = 2,
        All = StaticShip | WeaponsAndDetails,
    }

    sealed class StaticLayoutCache
    {
        public readonly Dictionary<ShipClassPlaceholderImageRenderSettings, Dictionary<PlaceholderMountRenderRecord, List<PixelVector2>>> deckTorpedoPositions = new();
    }

    static readonly ConditionalWeakTable<ShipClassPlaceholderRenderInput, StaticLayoutCache> staticLayoutCaches = new();

    struct RenderPalette
    {
        public RgbaColor background;
        public RgbaColor hullFill;
        public RgbaColor hullOutline;
        public RgbaColor interiorLine;
        public RgbaColor detailFill;
        public RgbaColor torpedoBaseFill;
        public RgbaColor mountFill;
        public RgbaColor rapidFire;
        public RgbaColor funnelFill;
        public RgbaColor funnelOutline;
        public RgbaColor mastLine;
    }

    sealed class PixelCanvas
    {
        readonly RgbaColor[] pixels;
        public ShipClassGraphicLayout Layout { get; }
        public PixelRect ShipRect { get; set; }
        public bool Capture(ShipGraphicPartKind kind, PixelVector2 pos, double width, double length,
            PixelVector2 direction = default, int barrels = 0)
        {
            if (Layout == null) return false;
            Layout.Parts.Add(new(kind, (pos.y - ShipRect.center.y) / ShipRect.height,
                (pos.x - ShipRect.center.x) / ShipRect.width, width / ShipRect.height, length / ShipRect.width,
                direction.y, direction.x, barrels));
            return true;
        }

        public int width { get; }
        public int height { get; }

        public PixelCanvas(int width, int height, RgbaColor background, ShipClassGraphicLayout layout = null)
        {
            this.width = width;
            this.height = height;
            Layout = layout;
            pixels = layout == null ? Enumerable.Repeat(background, width * height).ToArray() : Array.Empty<RgbaColor>();
        }

        public void SetPixel(int x, int y, RgbaColor color)
        {
            if (x < 0 || y < 0 || x >= width || y >= height)
                return;
            pixels[y * width + x] = Blend(pixels[y * width + x], color);
        }

        public void FillPixelRect(PixelRectInt rect, RgbaColor color)
        {
            var xMin = PixelMath.Max(0, rect.xMin);
            var xMax = PixelMath.Min(width, rect.xMax);
            var yMin = PixelMath.Max(0, rect.yMin);
            var yMax = PixelMath.Min(height, rect.yMax);
            for (var y = yMin; y < yMax; y++)
            {
                for (var x = xMin; x < xMax; x++)
                {
                    pixels[y * width + x] = Blend(pixels[y * width + x], color);
                }
            }
        }

        public void FillPolygon(PixelVector2[] points,RgbaColor color)
        {
            for(var y=Math.Max(0,(int)Math.Ceiling(points.Min(p=>p.y)));y<=Math.Min(height-1,(int)Math.Floor(points.Max(p=>p.y)));y++)
            {
                var crossings=new List<double>();
                for(var i=0;i<points.Length;i++)
                {
                    var a=points[i];var b=points[(i+1)%points.Length];
                    if((a.y<=y && b.y>y)||(b.y<=y && a.y>y))crossings.Add(a.x+(b.x-a.x)*(y-a.y)/(b.y-a.y));
                }
                if(crossings.Count>=2)for(var x=Math.Max(0,(int)Math.Ceiling(crossings.Min()));x<=Math.Min(width-1,(int)Math.Floor(crossings.Max()));x++)SetPixel(x,y,color);
            }
        }

        public void FillEllipse(PixelVector2 center, double radiusX, double radiusY, RgbaColor color)
        {
            var xMin = PixelMath.FloorToInt(center.x - radiusX);
            var xMax = PixelMath.CeilToInt(center.x + radiusX);
            var yMin = PixelMath.FloorToInt(center.y - radiusY);
            var yMax = PixelMath.CeilToInt(center.y + radiusY);
            var invRx = radiusX <= 0 ? 0 : 1d / radiusX;
            var invRy = radiusY <= 0 ? 0 : 1d / radiusY;
            for (var y = yMin; y <= yMax; y++)
            {
                for (var x = xMin; x <= xMax; x++)
                {
                    var dx = (x - center.x) * invRx;
                    var dy = (y - center.y) * invRy;
                    if (dx * dx + dy * dy <= 1d)
                    {
                        SetPixel(x, y, color);
                    }
                }
            }
        }

        public void DrawLine(PixelVector2 from, PixelVector2 to, int thickness, RgbaColor color)
        {
            var steps = PixelMath.CeilToInt(PixelVector2.Distance(from, to) * 1.5d);
            steps = PixelMath.Max(steps, 1);
            var radius = PixelMath.Max(1, thickness) / 2d;
            for (var i = 0; i <= steps; i++)
            {
                var p = PixelVector2.Lerp(from, to, i / (double)steps);
                FillEllipse(p, radius, radius, color);
            }
        }

        public void DrawPixelRectOutline(PixelRect rect, int thickness, RgbaColor color)
        {
            DrawLine(new PixelVector2(rect.xMin, rect.yMin), new PixelVector2(rect.xMax, rect.yMin), thickness, color);
            DrawLine(new PixelVector2(rect.xMax, rect.yMin), new PixelVector2(rect.xMax, rect.yMax), thickness, color);
            DrawLine(new PixelVector2(rect.xMax, rect.yMax), new PixelVector2(rect.xMin, rect.yMax), thickness, color);
            DrawLine(new PixelVector2(rect.xMin, rect.yMax), new PixelVector2(rect.xMin, rect.yMin), thickness, color);
        }

        public RgbaImage ToImage()
        {
            var bytes = new byte[pixels.Length * 4];
            for (var i = 0; i < pixels.Length; i++)
            {
                var offset = i * 4;
                bytes[offset] = pixels[i].r;
                bytes[offset + 1] = pixels[i].g;
                bytes[offset + 2] = pixels[i].b;
                bytes[offset + 3] = pixels[i].a;
            }

            return new RgbaImage(width, height, bytes);
        }

        static RgbaColor Blend(RgbaColor dst, RgbaColor src)
        {
            if (src.a == 255)
                return src;
            if (src.a == 0)
                return dst;

            var srcA = src.a / 255d;
            var dstA = dst.a / 255d;
            var outA = srcA + dstA * (1 - srcA);
            if (outA <= 0)
                return new RgbaColor(0, 0, 0, 0);

            byte BlendChannel(byte s, byte d)
            {
                var result = (s * srcA + d * dstA * (1 - srcA)) / outA;
                return (byte)PixelMath.Clamp(PixelMath.RoundToInt(result), 0, 255);
            }

            return new RgbaColor(
                BlendChannel(src.r, dst.r),
                BlendChannel(src.g, dst.g),
                BlendChannel(src.b, dst.b),
                (byte)PixelMath.Clamp(PixelMath.RoundToInt(outA * 255d), 0, 255)
            );
        }
    }

    sealed class HullProfile
    {
        public double bowSection;
        public double sternSection;
        public double maxBeamScale;
        public double sternTipScale;
        public double bridgeCenter;
        public double superstructureLength;
        public double deckhouseCenter;
    }

    readonly struct FunnelShape
    {
        public readonly PixelVector2 center;
        public readonly double radiusX;
        public readonly double radiusY;

        public FunnelShape(PixelVector2 center, double radiusX, double radiusY)
        {
            this.center = center;
            this.radiusX = radiusX;
            this.radiusY = radiusY;
        }

        public PixelRect Bounds => new(center.x - radiusX, center.y - radiusY, radiusX * 2d, radiusY * 2d);
    }

    static readonly Dictionary<PlaceholderShipType, HullProfile> HullProfiles = new()
    {
        { PlaceholderShipType.Battleship, new HullProfile { bowSection = 0.22d, sternSection = 0.70d, maxBeamScale = 1d, sternTipScale = 0.18d, bridgeCenter = 0.31d, superstructureLength = 0.19d, deckhouseCenter = 0.73d } },
        { PlaceholderShipType.Battlecruiser, new HullProfile { bowSection = 0.22d, sternSection = 0.70d, maxBeamScale = 1d, sternTipScale = 0.18d, bridgeCenter = 0.31d, superstructureLength = 0.19d, deckhouseCenter = 0.73d } }, // TODO: It's copied from battleship now, use a more specific value.
        { PlaceholderShipType.ArmoredCruiser, new HullProfile { bowSection = 0.23d, sternSection = 0.70d, maxBeamScale = 1d, sternTipScale = 0.16d, bridgeCenter = 0.32d, superstructureLength = 0.17d, deckhouseCenter = 0.72d } },
        // { PlaceholderShipType.Cruiser, new HullProfile { bowSection = 0.26d, sternSection = 0.68d, maxBeamScale = 1d, sternTipScale = 0.12d, bridgeCenter = 0.33d, superstructureLength = 0.15d, deckhouseCenter = 0.70d } },
        { PlaceholderShipType.LightCruiser, new HullProfile { bowSection = 0.27d, sternSection = 0.67d, maxBeamScale = 1d, sternTipScale = 0.10d, bridgeCenter = 0.34d, superstructureLength = 0.14d, deckhouseCenter = 0.70d } },
        { PlaceholderShipType.Destroyer, new HullProfile { bowSection = 0.30d, sternSection = 0.63d, maxBeamScale = 1d, sternTipScale = 0.08d, bridgeCenter = 0.36d, superstructureLength = 0.10d, deckhouseCenter = 0.67d } },
        { PlaceholderShipType.TorpedoBoat, new HullProfile { bowSection = 0.31d, sternSection = 0.62d, maxBeamScale = 1d, sternTipScale = 0.07d, bridgeCenter = 0.38d, superstructureLength = 0.08d, deckhouseCenter = 0.64d } },
        { PlaceholderShipType.PatrolGunboat, new HullProfile { bowSection = 0.24d, sternSection = 0.74d, maxBeamScale = 1d, sternTipScale = 0.18d, bridgeCenter = 0.34d, superstructureLength = 0.13d, deckhouseCenter = 0.72d } },
        { PlaceholderShipType.Transport, new HullProfile { bowSection = 0.22d, sternSection = 0.76d, maxBeamScale = 1d, sternTipScale = 0.24d, bridgeCenter = 0.30d, superstructureLength = 0.16d, deckhouseCenter = 0.74d } },
        { PlaceholderShipType.Repair, new HullProfile { bowSection = 0.22d, sternSection = 0.76d, maxBeamScale = 1d, sternTipScale = 0.24d, bridgeCenter = 0.32d, superstructureLength = 0.16d, deckhouseCenter = 0.74d } },
        { PlaceholderShipType.ArmedMerchantCruiser, new HullProfile { bowSection = 0.24d, sternSection = 0.76d, maxBeamScale = 1d, sternTipScale = 0.22d, bridgeCenter = 0.31d, superstructureLength = 0.15d, deckhouseCenter = 0.73d } },
        { PlaceholderShipType.NotSpecified, new HullProfile { bowSection = 0.25d, sternSection = 0.72d, maxBeamScale = 1d, sternTipScale = 0.14d, bridgeCenter = 0.33d, superstructureLength = 0.14d, deckhouseCenter = 0.71d } },
    };

    public static RgbaImage Render(ShipClassPlaceholderRenderInput shipClass, ShipClassPlaceholderImageRenderSettings settings, RenderVariant variant)
        => Render(shipClass, settings, variant, RenderLayer.All);

    public static RgbaImage Render(
        ShipClassPlaceholderRenderInput shipClass,
        ShipClassPlaceholderImageRenderSettings settings,
        RenderVariant variant,
        RenderLayer layers) => RenderCore(shipClass, settings, variant, layers, null);

    /// <summary>Runs the same part placement as the 2D image without allocating or rasterizing pixels.</summary>
    public static ShipClassGraphicLayout BuildLayout(ShipClassPlaceholderRenderInput shipClass, ShipClassPlaceholderImageRenderSettings settings)
    {
        return ShipGraphicLayoutBuilder.Build(shipClass);
    }

    static RgbaImage RenderCore(ShipClassPlaceholderRenderInput shipClass, ShipClassPlaceholderImageRenderSettings settings,
        RenderVariant variant, RenderLayer layers, ShipClassGraphicLayout layout)
    {
        var palette = GetPalette(settings, variant);
        var background = layers == RenderLayer.WeaponsAndDetails
            ? new RgbaColor(0, 0, 0, 0)
            : palette.background;
        var canvas = new PixelCanvas(settings.canvasWidth, settings.canvasHeight, background, layout);

        var shipPixelRect = CalculateShipPixelRect(shipClass, settings);
        canvas.ShipRect = shipPixelRect;
        var centerY = shipPixelRect.center.y;
        var topEdge = new double[settings.canvasWidth];
        var bottomEdge = new double[settings.canvasWidth];
        for (var i = 0; i < topEdge.Length; i++)
        {
            topEdge[i] = double.NaN;
            bottomEdge[i] = double.NaN;
        }
        CalculateHullEdges(shipClass, shipPixelRect, settings, topEdge, bottomEdge);

        var physical = ShipGraphicLayoutBuilder.Build(shipClass);
        // Rasterize the semantic hull into scanline edges shared by deck and detail drawing.
        for (var px=0;px<topEdge.Length;px++)
        {
            var y=(px-shipPixelRect.xMin)/shipPixelRect.width-.5;
            var crossings=new List<double>();
            for(var j=0;j<physical.Hull.Count;j++)
            {
                var v=physical.Hull[j]; var w=physical.Hull[(j+1)%physical.Hull.Count];
                if(Math.Abs(v.Y-w.Y)<1e-9 || y<Math.Min(v.Y,w.Y) || y>Math.Max(v.Y,w.Y)) continue;
                crossings.Add(centerY+(v.X+(w.X-v.X)*(y-v.Y)/(w.Y-v.Y))*shipPixelRect.height);
            }
            if(crossings.Count>=2) { topEdge[px]=crossings.Min(); bottomEdge[px]=crossings.Max(); }
            else { topEdge[px]=double.NaN; bottomEdge[px]=double.NaN; }
        }
        if ((layers & RenderLayer.StaticShip) != 0)
        {
            for(var px=0;px<topEdge.Length;px++)
            {
                if(double.IsNaN(topEdge[px])) continue;
                canvas.DrawLine(new(px,topEdge[px]),new(px,bottomEdge[px]),1,palette.hullFill);
                if(px==0 || double.IsNaN(topEdge[px-1]))
                    canvas.DrawLine(new(px,topEdge[px]),new(px,bottomEdge[px]),settings.lineWidth,palette.hullOutline);
                if(px>0 && !double.IsNaN(topEdge[px-1]))
                {
                    canvas.DrawLine(new(px-1,topEdge[px-1]),new(px,topEdge[px]),settings.lineWidth,palette.hullOutline);
                    canvas.DrawLine(new(px-1,bottomEdge[px-1]),new(px,bottomEdge[px]),settings.lineWidth,palette.hullOutline);
                }
            }
        }
        foreach(var part in physical.Parts.OrderBy(p=>p.Kind==ShipGraphicPartKind.Mast?double.MaxValue:p.Z))
        {
            if(part.Kind==ShipGraphicPartKind.UnspecifiedTorpedo) continue;
            var weapon=part.Kind is ShipGraphicPartKind.Gun or ShipGraphicPartKind.DeckTorpedo or ShipGraphicPartKind.SubmergedTorpedo or ShipGraphicPartKind.HullTorpedoPort;
            if((layers & (weapon?RenderLayer.WeaponsAndDetails:RenderLayer.StaticShip))==0) continue;
            var pos=new PixelVector2(shipPixelRect.xMin+(part.Y+.5)*shipPixelRect.width,centerY+part.X*shipPixelRect.height);
            var w=part.Width*shipPixelRect.height; var l=part.Length*shipPixelRect.width;
            var angle=ShipGraphicLayoutBuilder.Direction(part,shipClass)*Math.PI/180;
            var direction=new PixelVector2(Math.Cos(angle),Math.Sin(angle));
            if(part.Kind==ShipGraphicPartKind.Gun)
                DrawPhysicalGunMount(canvas,pos,direction,part,w,l,shipPixelRect.width/ShipGraphicLayoutBuilder.Dimensions(shipClass).Length,
                    settings.weaponScale,palette.detailFill,palette.hullOutline);
            else if(part.Kind==ShipGraphicPartKind.HullTorpedoPort)
            {
                var broadside=part.DirectionX!=0;
                var scale=shipPixelRect.width/ShipGraphicLayoutBuilder.Dimensions(shipClass).Length;
                for(var i=0;i<part.Barrels;i++)
                {
                    var offset=(i-(part.Barrels-1)*.5)*part.TubeSpacing*scale;
                    canvas.FillEllipse(new(pos.x+(broadside?offset:0),pos.y+(broadside?0:offset)),
                        broadside?Math.Max(1,l/2):1,broadside?1:Math.Max(1,w/2),palette.hullOutline);
                }
            }
            else if(part.Kind==ShipGraphicPartKind.SubmergedTorpedo)
                DrawSubmergedTorpedoMount(canvas,pos,direction,part.Barrels,8*settings.weaponScale,palette.hullOutline,palette.detailFill);
            else if(part.Kind==ShipGraphicPartKind.DeckTorpedo)
                DrawPhysicalTorpedoMount(canvas,pos,direction,part,shipPixelRect.width/ShipGraphicLayoutBuilder.Dimensions(shipClass).Length,settings.weaponScale,palette.hullOutline,palette.detailFill,palette.torpedoBaseFill);
            else if(part.Kind==ShipGraphicPartKind.Mast)
            {
                canvas.DrawLine(new(pos.x,pos.y-shipPixelRect.height*part.CrossbarWidth/2),new(pos.x,pos.y+shipPixelRect.height*part.CrossbarWidth/2),Math.Max(2,settings.lineWidth),palette.hullOutline);
                canvas.FillEllipse(pos,Math.Max(2,l/2),Math.Max(2,w/2),palette.hullOutline);
                if(part.StructureStyle==ShipStructureStyle.TripodMast)
                    foreach(var side in new[]{-1,1})canvas.DrawLine(pos,new(pos.x-Math.Max(part.Length,.1*ShipGraphicLayoutBuilder.Dimensions(shipClass).Beam/ShipGraphicLayoutBuilder.Dimensions(shipClass).Length)*shipPixelRect.width,pos.y+side*Math.Max(part.Width*3,.1)*shipPixelRect.height),settings.lineWidth,palette.hullOutline);
            }
            else if(part.Kind==ShipGraphicPartKind.Funnel)
            {
                var offset=-Math.Tan(part.RakeDegrees*Math.PI/180)*part.Height/ShipGraphicLayoutBuilder.Dimensions(shipClass).Length*shipPixelRect.width;
                for(int i=0;i<=Math.Ceiling(Math.Abs(offset));i++)
                    canvas.FillEllipse(new(pos.x+Math.Sign(offset)*i,pos.y),l/2,w/2,palette.funnelOutline);
                for(int i=0;i<=Math.Ceiling(Math.Abs(offset));i++)
                    canvas.FillEllipse(new(pos.x+Math.Sign(offset)*i,pos.y),Math.Max(1,l/2-settings.lineWidth),Math.Max(1,w/2-settings.lineWidth),palette.funnelFill);
                var top=new PixelVector2(pos.x+offset,pos.y);
                canvas.FillEllipse(top,l/2,w/2,palette.funnelOutline);
                canvas.FillEllipse(top,Math.Max(1,l/2-settings.lineWidth),Math.Max(1,w/2-settings.lineWidth),palette.funnelFill);
            }
            else if(part.Kind==ShipGraphicPartKind.Deck)
            {
                for(var px=Math.Max(0,(int)(pos.x-l/2));px<Math.Min(canvas.width,pos.x+l/2);px++)
                    if(!double.IsNaN(topEdge[px])) canvas.DrawLine(new(px,Math.Max(topEdge[px],pos.y-w/2)),new(px,Math.Min(bottomEdge[px],pos.y+w/2)),1,palette.detailFill);
            }
            else if(part.Kind==ShipGraphicPartKind.Bridge)
            {
                var wings=part.WingWidth*shipPixelRect.height;
                DrawDeckBlock(canvas,new PixelRect(pos.x-l/2,pos.y-wings/2,l,wings),palette.detailFill,palette.hullOutline,settings.lineWidth);
                if(part.StructureStyle==ShipStructureStyle.ClosedBridge)DrawDeckBlock(canvas,new PixelRect(pos.x-l/2,pos.y-w*.325,l,w*.65),palette.detailFill,palette.hullOutline,settings.lineWidth);
            }
            else if(part.StructureStyle==ShipStructureStyle.BeveledDeckhouse)
            {
                var polygon=ShipGraphicLayoutBuilder.HousingOutline(ShipGunHousingShape.Beveled).Select(v=>new PixelVector2(pos.x+v.Y*l,pos.y+v.X*w)).ToArray();
                canvas.FillPolygon(polygon,palette.detailFill);
                for(int i=0;i<polygon.Length;i++)canvas.DrawLine(polygon[i],polygon[(i+1)%polygon.Length],settings.lineWidth,palette.hullOutline);
            }
            else DrawDeckBlock(canvas,new PixelRect(pos.x-l/2,pos.y-w/2,l,w),palette.detailFill,palette.hullOutline,settings.lineWidth);
        }
        if((layers & RenderLayer.WeaponsAndDetails)!=0)
            DrawRapidFireDetails(canvas,shipClass,shipPixelRect,centerY,settings,palette,topEdge,bottomEdge);

        return layout == null ? canvas.ToImage() : default;
    }

    public static int CalculateRecommendedCanvasWidth(ShipClassPlaceholderRenderInput shipClass, ShipClassPlaceholderImageRenderSettings settings)
    {
        var ratio = PixelMath.Max(1.5d, shipClass.lengthFoot / PixelMath.Max(1d, shipClass.beamFoot));
        var visualPadding = EstimateVisualPadding(shipClass, settings);
        var availableHeight = PixelMath.Max(1d, settings.canvasHeight - visualPadding * 2d);
        var shipWidth = availableHeight * ratio;
        var canvasWidth = shipWidth + visualPadding * 2d;
        return PixelMath.CeilToInt(canvasWidth);
    }

    static PixelRect CalculateShipPixelRect(ShipClassPlaceholderRenderInput shipClass, ShipClassPlaceholderImageRenderSettings settings)
    {
        var visualPadding = EstimateVisualPadding(shipClass, settings);
        var availableWidth = settings.canvasWidth - visualPadding * 2d;
        var availableHeight = settings.canvasHeight - visualPadding * 2d;
        var ratio = PixelMath.Max(1.5d, shipClass.lengthFoot / PixelMath.Max(1d, shipClass.beamFoot));

        var shipWidth = availableWidth;
        var shipHeight = shipWidth / ratio;
        if (shipHeight > availableHeight)
        {
            shipHeight = availableHeight;
            shipWidth = shipHeight * ratio;
        }

        var x = (settings.canvasWidth - shipWidth) / 2d;
        var y = (settings.canvasHeight - shipHeight) / 2d;
        return new PixelRect(x, y, shipWidth, shipHeight);
    }

    static void CalculateHullEdges(ShipClassPlaceholderRenderInput shipClass, PixelRect shipPixelRect, ShipClassPlaceholderImageRenderSettings settings, double[] topEdge, double[] bottomEdge)
    {
        var profile = GetHullProfile(shipClass.type);
        for (var ix = PixelMath.FloorToInt(shipPixelRect.xMin); ix <= PixelMath.CeilToInt(shipPixelRect.xMax); ix++)
        {
            var t = 1d - PixelMath.InverseLerp(shipPixelRect.xMin, shipPixelRect.xMax, ix);
            var halfBreadth = EvaluateHalfBreadth(t, profile, settings) * shipPixelRect.height * 0.5d;
            var yTop = shipPixelRect.center.y - halfBreadth;
            var yBottom = shipPixelRect.center.y + halfBreadth;

            topEdge[PixelMath.Clamp(ix, 0, topEdge.Length - 1)] = yTop;
            bottomEdge[PixelMath.Clamp(ix, 0, bottomEdge.Length - 1)] = yBottom;
        }
    }

    static void DrawHull(PixelCanvas canvas, PixelRect shipPixelRect, ShipClassPlaceholderImageRenderSettings settings, RenderPalette palette, double[] topEdge, double[] bottomEdge)
    {
        if (canvas.Layout != null) return;
        PixelVector2? prevTop = null;
        PixelVector2? prevBottom = null;
        for (var ix = PixelMath.FloorToInt(shipPixelRect.xMin); ix <= PixelMath.CeilToInt(shipPixelRect.xMax); ix++)
        {
            var clampedX = PixelMath.Clamp(ix, 0, topEdge.Length - 1);
            var yTop = topEdge[clampedX];
            var yBottom = bottomEdge[clampedX];
            if (double.IsNaN(yTop) || double.IsNaN(yBottom))
                continue;

            for (var y = PixelMath.CeilToInt(yTop); y <= PixelMath.FloorToInt(yBottom); y++)
            {
                canvas.SetPixel(ix, y, palette.hullFill);
            }

            var top = new PixelVector2(ix, yTop);
            var bottom = new PixelVector2(ix, yBottom);
            if (prevTop.HasValue)
            {
                canvas.DrawLine(prevTop.Value, top, settings.lineWidth, palette.hullOutline);
                canvas.DrawLine(prevBottom.Value, bottom, settings.lineWidth, palette.hullOutline);
            }
            prevTop = top;
            prevBottom = bottom;
        }

        canvas.DrawLine(new PixelVector2(shipPixelRect.xMin, shipPixelRect.center.y), new PixelVector2(shipPixelRect.xMax, shipPixelRect.center.y), PixelMath.Max(1, settings.lineWidth - 1), palette.interiorLine);
    }

    static void DrawDeckLines(PixelCanvas canvas, PixelRect shipPixelRect, ShipClassPlaceholderImageRenderSettings settings, RenderPalette palette, double[] topEdge, double[] bottomEdge)
    {
        if (canvas.Layout != null) return;
        PixelVector2? prevInnerTop = null;
        PixelVector2? prevInnerBottom = null;
        var deckInset = settings.deckInsetAmount;
        for (var ix = PixelMath.FloorToInt(shipPixelRect.xMin); ix <= PixelMath.CeilToInt(shipPixelRect.xMax); ix++)
        {
            var clampedX = PixelMath.Clamp(ix, 0, topEdge.Length - 1);
            if (double.IsNaN(topEdge[clampedX]))
                continue;

            var yTop = topEdge[clampedX] + deckInset;
            var yBottom = bottomEdge[clampedX] - deckInset;
            if (yBottom <= yTop)
                continue;

            var innerTop = new PixelVector2(ix, yTop);
            var innerBottom = new PixelVector2(ix, yBottom);
            if (prevInnerTop.HasValue)
            {
                canvas.DrawLine(prevInnerTop.Value, innerTop, 1, palette.interiorLine);
                canvas.DrawLine(prevInnerBottom.Value, innerBottom, 1, palette.interiorLine);
            }
            prevInnerTop = innerTop;
            prevInnerBottom = innerBottom;
        }
    }

    static void DrawSuperstructure(PixelCanvas canvas, ShipClassPlaceholderRenderInput shipClass, PixelRect shipPixelRect, double centerY, ShipClassPlaceholderImageRenderSettings settings, RenderPalette palette, double[] topEdge, double[] bottomEdge)
    {
        var profile = GetHullProfile(shipClass.type);
        var mainLength = shipPixelRect.width * profile.superstructureLength * settings.superstructureHeightScale;
        var mainWidth = shipPixelRect.height * 0.20d * settings.superstructureHeightScale;
        var bridge = BuildCenteredPixelRect(shipPixelRect, NormalizeForeAftX(profile.bridgeCenter), mainLength, mainWidth, centerY);
        DrawDeckBlock(canvas, bridge, palette.detailFill, palette.hullOutline, settings.lineWidth);

        var deckhouse = BuildCenteredPixelRect(shipPixelRect, NormalizeForeAftX(profile.deckhouseCenter), mainLength * 0.55d, mainWidth * 0.7d, centerY);
        DrawDeckBlock(canvas, deckhouse, palette.detailFill, palette.hullOutline, PixelMath.Max(1, settings.lineWidth - 1));

        foreach (var funnel in BuildFunnelShapes(shipClass, shipPixelRect, centerY, settings, topEdge, bottomEdge))
        {
            DrawFunnel(canvas, funnel, palette);
        }
    }

    static void DrawBatteryMounts(PixelCanvas canvas, ShipClassPlaceholderRenderInput shipClass, PixelRect shipPixelRect, double centerY, ShipClassPlaceholderImageRenderSettings settings, RenderPalette palette, double[] topEdge, double[] bottomEdge)
    {
        var batteryVisualScale = EvaluateBatteryGlyphVisualScale(shipPixelRect);
        var groupedRecords = (shipClass.batteryRecords ?? new List<PlaceholderBatteryRenderRecord>())
            .SelectMany(battery => (battery.mountLocationRecords ?? new List<PlaceholderMountRenderRecord>())
                .Select(record => new
                {
                    battery,
                    record,
                    glyphSize = EvaluateBatteryGlyphSize(battery.shellSizeInch) * settings.weaponScale * 2.2d * batteryVisualScale,
                }))
            .GroupBy(entry => entry.record.mountLocation);

        foreach (var group in groupedRecords)
        {
            var totalMounts = group.Sum(entry => 1);
            var layoutGlyphSize = group.Max(entry => entry.glyphSize);
            var allPositions = ResolveMountPositions(group.Key, totalMounts, layoutGlyphSize, shipPixelRect, centerY, topEdge, bottomEdge);
            var positionIndex = 0;
            var orderedEntries = group
                .OrderBy(entry => ResolveMountDirection(entry.record).x)
                .ThenBy(entry => ResolveMountDirection(entry.record).y)
                .ToList();

            foreach (var entry in orderedEntries)
            {
                var mountCount = 1;
                var positions = allPositions.Skip(positionIndex).Take(mountCount);
                var positionList = positions.ToList();
                for (var mountIndex = 0; mountIndex < positionList.Count; mountIndex++)
                {
                    var direction = ResolveMountDirection(entry.record);
                    DrawGunMount(canvas, positionList[mountIndex], direction, entry.record.barrels, entry.glyphSize, settings.lineWidth, palette.mountFill, palette.hullOutline, palette.detailFill);
                }

                positionIndex += mountCount;
            }
        }
    }

    static void DrawTorpedoMounts(PixelCanvas canvas, ShipClassPlaceholderRenderInput shipClass, PixelRect shipPixelRect, double centerY, ShipClassPlaceholderImageRenderSettings settings, RenderPalette palette, double[] topEdge, double[] bottomEdge)
    {
        var torpedoVisualScale = EvaluateTorpedoGlyphVisualScale(shipPixelRect);
        var deckObstacles = BuildSuperstructureObstacles(shipClass, shipPixelRect, centerY, settings, topEdge, bottomEdge);

        var torpedoEntries = (shipClass.torpedoMountRecords ?? new List<PlaceholderMountRenderRecord>())
            .Select((record, index) =>
            {
                var submerged = IsSubmergedTorpedoRecord(record);
                var baseSize = PixelMath.Clamp(5d + record.barrels * 0.7d, 5d, 11d) * settings.weaponScale * 2d * torpedoVisualScale;
                return new
                {
                    record,
                    index,
                    submerged,
                    size = ResolveTorpedoSymbolSize(shipClass, record, baseSize, shipPixelRect, settings, submerged),
                    direction = submerged
                        ? GetSubmergedMountOutwardDirection(record.mountLocation, ResolveMountDirection(record))
                        : ResolveMountDirection(record),
                };
            })
            .ToList();

        foreach (var entry in torpedoEntries.Where(entry => entry.submerged))
        {
            var positions = ResolveSubmergedTorpedoPositions(entry.record, entry.size, shipPixelRect, centerY, topEdge, bottomEdge, entry.direction);
            foreach (var pos in positions)
            {
                DrawSubmergedTorpedoMount(canvas, pos, entry.direction, entry.record.barrels, entry.size, palette.mountFill, palette.hullOutline);
            }
        }

        foreach (var group in torpedoEntries.Where(entry => !entry.submerged).GroupBy(entry => entry.record.mountLocation))
        {
            var orderedEntries = group.OrderBy(entry => entry.index).ToList();
            List<List<PixelVector2>> positionsByEntry;
            var layoutCache = staticLayoutCaches.GetOrCreateValue(shipClass);
            lock (layoutCache)
            {
                if (!layoutCache.deckTorpedoPositions.TryGetValue(settings, out var positionsByRecord))
                {
                    positionsByRecord = new Dictionary<PlaceholderMountRenderRecord, List<PixelVector2>>();
                    layoutCache.deckTorpedoPositions.Add(settings, positionsByRecord);
                }

                if (!orderedEntries.All(entry => positionsByRecord.ContainsKey(entry.record)))
                {
                    var totalMounts = orderedEntries.Sum(entry => 1);
                    var layoutSize = orderedEntries.Max(entry => entry.size);
                    var keepSideMountsAligned = orderedEntries.Any(entry => entry.record.trainable);
                    var positions = ResolveDeckTorpedoPositions(
                        shipClass.type,
                        group.Key,
                        keepSideMountsAligned,
                        totalMounts,
                        layoutSize,
                        shipPixelRect,
                        centerY,
                        topEdge,
                        bottomEdge,
                        deckObstacles);
                    var positionIndex = 0;
                    foreach (var entry in orderedEntries)
                    {
                        var mountCount = 1;
                        positionsByRecord[entry.record] = positions.Skip(positionIndex).Take(mountCount).ToList();
                        positionIndex += mountCount;
                    }
                }

                positionsByEntry = orderedEntries.Select(entry => positionsByRecord[entry.record]).ToList();
            }

            for (var entryIndex = 0; entryIndex < orderedEntries.Count; entryIndex++)
            {
                var entry = orderedEntries[entryIndex];
                var mountPositions = positionsByEntry[entryIndex];
                for (var mountIndex = 0; mountIndex < mountPositions.Count; mountIndex++)
                {
                    var direction = ResolveMountDirection(entry.record);
                    DrawDeckTorpedoMount(canvas, mountPositions[mountIndex], direction, entry.record.barrels, entry.size, entry.record.trainable, palette.hullOutline, palette.detailFill, palette.torpedoBaseFill);
                }
            }
        }
    }

    static void DrawRapidFireDetails(PixelCanvas canvas, ShipClassPlaceholderRenderInput shipClass, PixelRect shipPixelRect, double centerY, ShipClassPlaceholderImageRenderSettings settings, RenderPalette palette, double[] topEdge, double[] bottomEdge)
    {
        var rapidFireRecords = shipClass.rapidFireBatteryRecords ?? new List<PlaceholderRapidFireRenderRecord>();
        var portBarrels = rapidFireRecords.Sum(r => (r.barrelsLevelPort ?? new List<int>()).FirstOrDefault());
        var starboardBarrels = rapidFireRecords.Sum(r => (r.barrelsLevelStarboard ?? new List<int>()).FirstOrDefault());
        DrawRapidFireSide(canvas, shipClass, shipPixelRect, centerY, topEdge, bottomEdge, portBarrels, false, palette.rapidFire, settings.weaponScale);
        DrawRapidFireSide(canvas, shipClass, shipPixelRect, centerY, topEdge, bottomEdge, starboardBarrels, true, palette.rapidFire, settings.weaponScale);
    }

    static void DrawRapidFireSide(PixelCanvas canvas, ShipClassPlaceholderRenderInput shipClass, PixelRect shipPixelRect, double centerY, double[] topEdge, double[] bottomEdge, int count, bool starboard, RgbaColor color, double weaponScale)
    {
        if (count <= 0)
            return;

        var range = ResolveRapidFireDistributionRange(shipClass);
        var inboardRatio = ResolveRapidFireInboardRatio(shipClass);
        for (var i = 0; i < count; i++)
        {
            var t = count == 1 ? 0.5d : i / (double)(count - 1);
            var x = shipPixelRect.xMin + shipPixelRect.width * PixelMath.Lerp(range.x, range.y, t);
            var halfBreadth = SampleHullHalfBreadth(topEdge, bottomEdge, PixelMath.RoundToInt(x));
            var sideSign = starboard ? 1d : -1d;
            var y = centerY + sideSign * halfBreadth * inboardRatio;
            DrawRapidFireGun(canvas, new PixelVector2(x, y), sideSign, color, weaponScale);
        }
    }

    static PixelVector2 ResolveRapidFireDistributionRange(ShipClassPlaceholderRenderInput shipClass)
    {
        var isSmallCombatant = shipClass.type == PlaceholderShipType.Destroyer || shipClass.type == PlaceholderShipType.TorpedoBoat;
        if (isSmallCombatant)
            return BuildCenteredRange(PixelMath.Clamp(shipClass.lengthFoot / 620d, 0.28d, 0.38d));

        if (shipClass.type == PlaceholderShipType.Transport || shipClass.type == PlaceholderShipType.Repair || shipClass.type == PlaceholderShipType.ArmedMerchantCruiser)
            return BuildCenteredRange(PixelMath.Clamp(shipClass.lengthFoot / 760d, 0.44d, 0.56d));

        return BuildCenteredRange(PixelMath.Clamp(shipClass.lengthFoot / 600d, 0.50d, 0.68d));
    }

    static PixelVector2 BuildCenteredRange(double span)
    {
        var halfSpan = span * 0.5d;
        return new PixelVector2(0.5d - halfSpan, 0.5d + halfSpan);
    }

    static double ResolveRapidFireInboardRatio(ShipClassPlaceholderRenderInput shipClass)
    {
        return shipClass.type == PlaceholderShipType.Destroyer || shipClass.type == PlaceholderShipType.TorpedoBoat
            ? 0.58d
            : 0.78d;
    }

    static void DrawRapidFireGun(PixelCanvas canvas, PixelVector2 pos, double sideSign, RgbaColor color, double weaponScale)
    {
        if (canvas.Capture(ShipGraphicPartKind.RapidFire, pos, 4.8 * weaponScale, 8.4 * weaponScale, new PixelVector2(0, sideSign), 1)) return;
        var mountRadius = PixelMath.Clamp(2.4d * weaponScale, 2d, 4.4d);
        var barrelLength = PixelMath.Clamp(8.4d * weaponScale, 6d, 13d);
        var barrelStart = pos;
        var barrelEnd = pos + new PixelVector2(0d, sideSign * barrelLength);
        canvas.FillEllipse(pos, mountRadius, mountRadius, color);
        canvas.DrawLine(barrelStart, barrelEnd, 1, color);
    }

    static void DrawMasts(PixelCanvas canvas, ShipClassPlaceholderRenderInput shipClass, PixelRect shipPixelRect, double centerY, ShipClassPlaceholderImageRenderSettings settings, RenderPalette palette, double[] topEdge, double[] bottomEdge)
    {
        var mastXs = new List<double> { shipPixelRect.xMin + shipPixelRect.width * NormalizeForeAftX(0.22d) };
        if (shipClass.type != PlaceholderShipType.TorpedoBoat)
        {
            mastXs.Add(shipPixelRect.xMin + shipPixelRect.width * NormalizeForeAftX(0.82d));
        }

        foreach (var x in mastXs)
        {
            if (canvas.Capture(ShipGraphicPartKind.Mast, new PixelVector2(x, centerY), 2.4, 2.4)) continue;
            var halfBreadth = SampleHullHalfBreadth(topEdge, bottomEdge, PixelMath.RoundToInt(x));
            var height = PixelMath.Clamp(halfBreadth * 0.34d, 6d, 18d);
            var halfTick = PixelMath.Clamp(shipPixelRect.width * 0.004d, 2d, 5d);
            canvas.DrawLine(new PixelVector2(x, centerY - height / 2d), new PixelVector2(x, centerY + height / 2d), 1, palette.mastLine);
            canvas.DrawLine(new PixelVector2(x - halfTick, centerY), new PixelVector2(x + halfTick, centerY), 1, palette.mastLine);
            canvas.FillEllipse(new PixelVector2(x, centerY), 1.2d, 1.2d, palette.mastLine);
        }
    }

    static double ResolveTorpedoSymbolSize(ShipClassPlaceholderRenderInput shipClass, PlaceholderMountRenderRecord record, double baseSize, PixelRect shipPixelRect, ShipClassPlaceholderImageRenderSettings settings, bool submerged)
    {
        if (submerged)
            return baseSize;

        if (!record.trainable)
            return baseSize * 1.55d;

        var pixelsPerFoot = EstimatePixelsPerFoot(shipClass, shipPixelRect);
        var footprintFeet = TrainableTorpedoLauncherFootprintFeet + PixelMath.Max(0, record.barrels - 1) * AdditionalTrainableTorpedoTubeFootprintFeet;
        var physicalSize = footprintFeet * pixelsPerFoot * settings.weaponScale;
        return PixelMath.Max(baseSize * 1.7d, physicalSize);
    }

    static double EstimatePixelsPerFoot(ShipClassPlaceholderRenderInput shipClass, PixelRect shipPixelRect)
    {
        var lengthScale = shipClass.lengthFoot > 0d ? shipPixelRect.width / shipClass.lengthFoot : 0d;
        var beamScale = shipClass.beamFoot > 0d ? shipPixelRect.height / shipClass.beamFoot : 0d;
        if (lengthScale > 0d && beamScale > 0d)
            return PixelMath.Min(lengthScale, beamScale);
        if (lengthScale > 0d)
            return lengthScale;
        if (beamScale > 0d)
            return beamScale;
        return 1d;
    }

    static void DrawFunnel(PixelCanvas canvas, FunnelShape funnel, RenderPalette palette)
    {
        if (canvas.Capture(ShipGraphicPartKind.Funnel, funnel.center, funnel.radiusY * 2, funnel.radiusX * 2)) return;
        canvas.FillEllipse(funnel.center, funnel.radiusX, funnel.radiusY, palette.funnelOutline);
        canvas.FillEllipse(
            funnel.center,
            PixelMath.Max(1d, funnel.radiusX - 1.5d),
            PixelMath.Max(1d, funnel.radiusY - 1.5d),
            palette.funnelFill);
        canvas.DrawLine(
            new PixelVector2(funnel.center.x, funnel.center.y - funnel.radiusY * 0.55d),
            new PixelVector2(funnel.center.x, funnel.center.y + funnel.radiusY * 0.55d),
            1,
            palette.funnelOutline);
    }

    static void DrawDeckBlock(PixelCanvas canvas, PixelRect rect, RgbaColor fill, RgbaColor outline, int lineWidth)
    {
        if (canvas.Capture(ShipGraphicPartKind.Deckhouse, rect.center, rect.height, rect.width)) return;
        canvas.FillPixelRect(new PixelRectInt(PixelMath.RoundToInt(rect.xMin), PixelMath.RoundToInt(rect.yMin), PixelMath.RoundToInt(rect.width), PixelMath.RoundToInt(rect.height)), fill);
        canvas.DrawPixelRectOutline(rect, PixelMath.Max(1, lineWidth), outline);
    }

    static PixelRect BuildCenteredPixelRect(PixelRect shipPixelRect, double normalizedX, double length, double width, double centerY)
    {
        var centerX = shipPixelRect.xMin + shipPixelRect.width * normalizedX;
        return new PixelRect(centerX - length / 2d, centerY - width / 2d, length, width);
    }

    static List<PixelVector2> ResolveMountPositions(PlaceholderMountLocation location, int mountCount, double symbolSize, PixelRect shipPixelRect, double centerY, double[] topEdge, double[] bottomEdge)
    {
        var anchors = GetAnchor(location);
        var isSideLocation =
            location == PlaceholderMountLocation.PortForward ||
            location == PlaceholderMountLocation.StarboardForward ||
            location == PlaceholderMountLocation.PortMidship ||
            location == PlaceholderMountLocation.StarboardMidship ||
            location == PlaceholderMountLocation.PortAfter ||
            location == PlaceholderMountLocation.StarboardAfter;

        var baseSpread = isSideLocation
            ? PixelMath.Max(symbolSize * 2.4d, shipPixelRect.width * 0.06d)
            : PixelMath.Max(symbolSize * 1.35d, shipPixelRect.width * 0.032d);
        var spread = mountCount <= 1 ? 0d : PixelMath.Min(baseSpread, shipPixelRect.width * (isSideLocation ? 0.13d : 0.07d));
        var positions = new List<PixelVector2>();
        for (var i = 0; i < PixelMath.Max(1, mountCount); i++)
        {
            var offsetT = mountCount <= 1 ? 0d : (i - (mountCount - 1) / 2d);
            var x = shipPixelRect.xMin + shipPixelRect.width * anchors.x + offsetT * spread;
            var halfBreadth = SampleHullHalfBreadth(topEdge, bottomEdge, PixelMath.RoundToInt(x));
            var sideCompression = isSideLocation ? 0.76d : 1d;
            var y = centerY + halfBreadth * anchors.y * sideCompression;

            if (isSideLocation && mountCount > 1)
            {
                var stagger = PixelMath.Abs(offsetT) < 0.1d ? 0d : PixelMath.Sign(offsetT) * PixelMath.Min(symbolSize * 0.18d, halfBreadth * 0.08d);
                y += PixelMath.Sign(anchors.y) * stagger;
            }

            positions.Add(new PixelVector2(x, y));
        }
        return positions;
    }

    static List<PixelVector2> ResolveDeckTorpedoPositions(PlaceholderShipType shipType, PlaceholderMountLocation location, bool keepSideMountsAligned, int mountCount, double symbolSize, PixelRect shipPixelRect, double centerY, double[] topEdge, double[] bottomEdge, List<PixelRect> obstacles)
    {
        var positions = ResolveDeckTorpedoBasePositions(shipType, location, keepSideMountsAligned, mountCount, symbolSize, shipPixelRect, centerY, topEdge, bottomEdge);
        if (obstacles == null || obstacles.Count == 0)
            return positions;

        var adjusted = new List<PixelVector2>(positions.Count);
        var reservedObstacles = new List<PixelRect>(obstacles);
        foreach (var pos in positions)
        {
            var adjustedPos = ResolveDeckMountAvoidance(pos, symbolSize, shipPixelRect, reservedObstacles);
            adjusted.Add(adjustedPos);
            reservedObstacles.Add(ExpandPixelRect(GetDeckMountBounds(adjustedPos, symbolSize), 4d));
        }

        return adjusted;
    }

    static List<PixelVector2> ResolveDeckTorpedoBasePositions(PlaceholderShipType shipType, PlaceholderMountLocation location, bool keepSideMountsAligned, int mountCount, double symbolSize, PixelRect shipPixelRect, double centerY, double[] topEdge, double[] bottomEdge)
    {
        var anchors = GetAnchor(location);
        var isSideLocation =
            location == PlaceholderMountLocation.PortForward ||
            location == PlaceholderMountLocation.StarboardForward ||
            location == PlaceholderMountLocation.PortMidship ||
            location == PlaceholderMountLocation.StarboardMidship ||
            location == PlaceholderMountLocation.PortAfter ||
            location == PlaceholderMountLocation.StarboardAfter;
        var count = PixelMath.Max(1, mountCount);
        var baseSpread = isSideLocation
            ? PixelMath.Max(symbolSize * 2.4d, shipPixelRect.width * 0.06d)
            : PixelMath.Max(symbolSize * 2.45d, shipPixelRect.width * 0.06d);
        var spread = count <= 1 ? 0d : PixelMath.Min(baseSpread, shipPixelRect.width * (isSideLocation ? 0.16d : 0.18d));
        var positions = new List<PixelVector2>(count);
        for (var i = 0; i < count; i++)
        {
            var offsetT = count <= 1 ? 0d : i - (count - 1) / 2d;
            var x = ResolveDeckTorpedoBaseX(shipType, location, anchors.x, offsetT, spread, symbolSize, shipPixelRect);
            var halfBreadth = SampleHullHalfBreadth(topEdge, bottomEdge, PixelMath.RoundToInt(x));
            var y = ResolveDeckTorpedoBaseY(location, anchors.y, halfBreadth, symbolSize, centerY, topEdge, bottomEdge, x);

            if (isSideLocation && count > 1 && !keepSideMountsAligned)
            {
                var stagger = PixelMath.Abs(offsetT) < 0.1d ? 0d : PixelMath.Sign(offsetT) * PixelMath.Min(symbolSize * 0.18d, halfBreadth * 0.08d);
                y += GetDeckTorpedoSideSign(location) * stagger;
            }

            positions.Add(new PixelVector2(x, y));
        }

        return positions;
    }

    static double ResolveDeckTorpedoBaseX(PlaceholderShipType shipType, PlaceholderMountLocation location, double anchorX, double offsetT, double spread, double symbolSize, PixelRect shipPixelRect)
    {
        var endSign = GetDeckTorpedoEndSign(location);
        if (endSign != 0d)
        {
            if (UsesInboardEndDeckTorpedoPlacement(shipType))
                return shipPixelRect.xMin + shipPixelRect.width * anchorX + offsetT * spread;

            var inset = PixelMath.Clamp(symbolSize * 2.8d, shipPixelRect.width * 0.075d, shipPixelRect.width * 0.16d);
            var endX = endSign > 0d ? shipPixelRect.xMax - inset : shipPixelRect.xMin + inset;
            return endX - endSign * offsetT * spread;
        }

        return shipPixelRect.xMin + shipPixelRect.width * anchorX + offsetT * spread;
    }

    static double ResolveDeckTorpedoBaseY(PlaceholderMountLocation location, double anchorY, double halfBreadth, double symbolSize, double centerY, double[] topEdge, double[] bottomEdge, double x)
    {
        var sideSign = GetDeckTorpedoSideSign(location);
        if (sideSign == 0d)
            return centerY + halfBreadth * anchorY;

        var clampedX = PixelMath.Clamp(PixelMath.RoundToInt(x), 0, topEdge.Length - 1);
        var edgeY = sideSign < 0d ? topEdge[clampedX] : bottomEdge[clampedX];
        if (double.IsNaN(edgeY))
            return centerY + halfBreadth * anchorY;

        var inset = PixelMath.Min(PixelMath.Max(symbolSize * 0.55d, 4d), PixelMath.Max(1d, halfBreadth * 0.7d));
        return edgeY - sideSign * inset;
    }

    static double GetDeckTorpedoSideSign(PlaceholderMountLocation location)
    {
        return location switch
        {
            PlaceholderMountLocation.PortForward or PlaceholderMountLocation.PortMidship or PlaceholderMountLocation.PortAfter => -1d,
            PlaceholderMountLocation.StarboardForward or PlaceholderMountLocation.StarboardMidship or PlaceholderMountLocation.StarboardAfter => 1d,
            _ => 0d
        };
    }

    static double GetDeckTorpedoEndSign(PlaceholderMountLocation location)
    {
        return location switch
        {
            PlaceholderMountLocation.Forward => 1d,
            PlaceholderMountLocation.After => -1d,
            _ => 0d
        };
    }

    static bool UsesInboardEndDeckTorpedoPlacement(PlaceholderShipType shipType)
    {
        return shipType == PlaceholderShipType.Destroyer || shipType == PlaceholderShipType.TorpedoBoat;
    }

    static bool IsSubmergedTorpedoRecord(PlaceholderMountRenderRecord record)
    {
        if (record.mountArcs == null || record.mountArcs.Count == 0)
            return false;

        return record.mountArcs.All(arc => arc.CoverageDeg <= 30d);
    }

    static List<PixelVector2> ResolveSubmergedTorpedoPositions(PlaceholderMountRenderRecord record, double symbolSize, PixelRect shipPixelRect, double centerY, double[] topEdge, double[] bottomEdge, PixelVector2 direction)
    {
        var outward = GetSubmergedMountOutwardDirection(record.mountLocation, direction);
        var positions = new List<PixelVector2>();
        var count = 1;
        var alongHull = new PixelVector2(-outward.y, outward.x);
        var spacing = symbolSize * 0.9d;
        var exposure = symbolSize * 0.58d;
        var anchor = GetAnchor(record.mountLocation);

        for (var i = 0; i < count; i++)
        {
            var offset = count <= 1 ? 0d : (i - (count - 1) / 2d) * spacing;
            double x;
            double y;

            if (PixelMath.Abs(outward.x) > 0.5d)
            {
                x = outward.x > 0 ? shipPixelRect.xMax + exposure : shipPixelRect.xMin - exposure;
                y = centerY;
            }
            else
            {
                x = shipPixelRect.xMin + shipPixelRect.width * anchor.x;
                var hullY = outward.y < 0
                    ? topEdge[PixelMath.Clamp(PixelMath.RoundToInt(x), 0, topEdge.Length - 1)]
                    : bottomEdge[PixelMath.Clamp(PixelMath.RoundToInt(x), 0, bottomEdge.Length - 1)];
                y = hullY + outward.y * exposure;
            }

            var pos = new PixelVector2(x, y) + alongHull * offset;
            positions.Add(pos);
        }

        return positions;
    }

    static PixelVector2 ResolveDeckMountAvoidance(PixelVector2 originalPos, double symbolSize, PixelRect shipPixelRect, List<PixelRect> obstacles)
    {
        var candidate = originalPos;
        if (!IntersectsObstacle(candidate, symbolSize, obstacles))
            return candidate;

        var halfWidth = GetDeckMountBounds(symbolSize).width * 0.5d;
        var margin = PixelMath.Max(4d, symbolSize * 0.35d);
        for (var attempt = 0; attempt < 8; attempt++)
        {
            var bounds = GetDeckMountBounds(candidate, symbolSize);
            var overlapping = obstacles.Where(obstacle => bounds.Overlaps(obstacle)).ToList();
            if (overlapping.Count == 0)
                return candidate;

            var bestCandidate = candidate;
            var bestDistance = double.MaxValue;
            foreach (var obstacle in overlapping)
            {
                var aftX = PixelMath.Max(shipPixelRect.xMin + halfWidth, obstacle.xMin - halfWidth - margin);
                var aftCandidate = new PixelVector2(aftX, originalPos.y);
                if (!IntersectsObstacle(aftCandidate, symbolSize, obstacles))
                {
                    var aftDistance = PixelMath.Abs(aftCandidate.x - originalPos.x);
                    if (aftDistance < bestDistance)
                    {
                        bestDistance = aftDistance;
                        bestCandidate = aftCandidate;
                    }
                }

                var foreX = PixelMath.Min(shipPixelRect.xMax - halfWidth, obstacle.xMax + halfWidth + margin);
                var foreCandidate = new PixelVector2(foreX, originalPos.y);
                if (!IntersectsObstacle(foreCandidate, symbolSize, obstacles))
                {
                    var foreDistance = PixelMath.Abs(foreCandidate.x - originalPos.x);
                    if (foreDistance < bestDistance)
                    {
                        bestDistance = foreDistance;
                        bestCandidate = foreCandidate;
                    }
                }
            }

            if (bestDistance < double.MaxValue)
                return bestCandidate;

            candidate = new PixelVector2(PixelMath.Clamp(candidate.x - margin, shipPixelRect.xMin + halfWidth, shipPixelRect.xMax - halfWidth), candidate.y);
        }

        return candidate;
    }

    static bool IntersectsObstacle(PixelVector2 pos, double symbolSize, List<PixelRect> obstacles)
    {
        var bounds = GetDeckMountBounds(pos, symbolSize);
        foreach (var obstacle in obstacles)
        {
            if (bounds.Overlaps(obstacle))
                return true;
        }

        return false;
    }

    static PixelRect GetDeckMountBounds(double symbolSize)
    {
        return new PixelRect(0d, 0d, symbolSize * 2.6d, symbolSize * 1.9d);
    }

    static PixelRect GetDeckMountBounds(PixelVector2 pos, double symbolSize)
    {
        var size = GetDeckMountBounds(symbolSize).size;
        return new PixelRect(pos.x - size.x / 2d, pos.y - size.y / 2d, size.x, size.y);
    }

    static double EvaluateBatteryGlyphSize(double shellSizeInch)
    {
        if (shellSizeInch <= 0)
            return 4.5d;

        if (shellSizeInch <= 4.7d)
            return PixelMath.Clamp(3.1d + shellSizeInch * 0.48d, 4d, 6.2d);

        var largeGunBoost = PixelMath.Pow(shellSizeInch - 4.7d, 0.98d) * 1.55d;
        return PixelMath.Clamp(5.8d + largeGunBoost, 5.8d, 23d);
    }

    static double EvaluateBatteryGlyphVisualScale(PixelRect shipPixelRect)
    {
        var visualBeam = PixelMath.Max(1d, shipPixelRect.height);
        var scale = PixelMath.Pow(72d / visualBeam, 0.55d);
        return PixelMath.Clamp(scale, 0.82d, 1.45d);
    }

    static double EvaluateTorpedoGlyphVisualScale(PixelRect shipPixelRect)
    {
        var visualBeam = PixelMath.Max(1d, shipPixelRect.height);
        var scale = PixelMath.Pow(82d / visualBeam, 0.72d);
        return PixelMath.Clamp(scale, 0.95d, 1.85d);
    }

    static PixelVector2 GetAnchor(PlaceholderMountLocation location)
    {
        return location switch
        {
            PlaceholderMountLocation.PortForward => new PixelVector2(NormalizeForeAftX(0.23d), -0.52d),
            PlaceholderMountLocation.Forward => new PixelVector2(NormalizeForeAftX(0.18d), 0d),
            PlaceholderMountLocation.StarboardForward => new PixelVector2(NormalizeForeAftX(0.23d), 0.52d),
            PlaceholderMountLocation.PortMidship => new PixelVector2(0.50d, -0.58d),
            PlaceholderMountLocation.Midship => new PixelVector2(0.50d, 0d),
            PlaceholderMountLocation.StarboardMidship => new PixelVector2(0.50d, 0.58d),
            PlaceholderMountLocation.PortAfter => new PixelVector2(NormalizeForeAftX(0.77d), -0.52d),
            PlaceholderMountLocation.After => new PixelVector2(NormalizeForeAftX(0.82d), 0d),
            PlaceholderMountLocation.StarboardAfter => new PixelVector2(NormalizeForeAftX(0.77d), 0.52d),
            _ => new PixelVector2(0.50d, 0d)
        };
    }

    static List<PixelRect> BuildSuperstructureObstacles(ShipClassPlaceholderRenderInput shipClass, PixelRect shipPixelRect, double centerY, ShipClassPlaceholderImageRenderSettings settings, double[] topEdge, double[] bottomEdge)
    {
        var obstacles = new List<PixelRect>();
        var profile = GetHullProfile(shipClass.type);
        var mainLength = shipPixelRect.width * profile.superstructureLength * settings.superstructureHeightScale;
        var mainWidth = shipPixelRect.height * 0.20d * settings.superstructureHeightScale;
        obstacles.Add(ExpandPixelRect(BuildCenteredPixelRect(shipPixelRect, NormalizeForeAftX(profile.bridgeCenter), mainLength, mainWidth, centerY), 3d));
        obstacles.Add(ExpandPixelRect(BuildCenteredPixelRect(shipPixelRect, NormalizeForeAftX(profile.deckhouseCenter), mainLength * 0.55d, mainWidth * 0.7d, centerY), 3d));

        foreach (var funnel in BuildFunnelShapes(shipClass, shipPixelRect, centerY, settings, topEdge, bottomEdge))
        {
            obstacles.Add(ExpandPixelRect(funnel.Bounds, 4d));
        }

        return obstacles;
    }

    static List<FunnelShape> BuildFunnelShapes(ShipClassPlaceholderRenderInput shipClass, PixelRect shipPixelRect, double centerY, ShipClassPlaceholderImageRenderSettings settings, double[] topEdge, double[] bottomEdge)
    {
        var funnelCount = ResolveFunnelCount(shipClass, settings);
        if (funnelCount <= 0)
            return new List<FunnelShape>();

        var baseXMin = shipPixelRect.xMin + shipPixelRect.width * NormalizeForeAftX(0.62d + settings.funnelSpacingBias * 0.2d);
        var baseXMax = shipPixelRect.xMin + shipPixelRect.width * NormalizeForeAftX(0.40d + settings.funnelSpacingBias * 0.2d);
        if (funnelCount == 1)
            baseXMin = baseXMax = shipPixelRect.xMin + shipPixelRect.width * NormalizeForeAftX(0.48d + settings.funnelSpacingBias * 0.2d);

        var shapes = new List<FunnelShape>(funnelCount);
        for (var i = 0; i < funnelCount; i++)
        {
            var t = funnelCount == 1 ? 0.5d : i / (double)(funnelCount - 1);
            var x = PixelMath.Lerp(baseXMin, baseXMax, t);
            var halfBreadth = SampleHullHalfBreadth(topEdge, bottomEdge, PixelMath.RoundToInt(x));
            var radiusX = PixelMath.Clamp(shipPixelRect.width * 0.012d, 4.5d, 9d) * settings.superstructureHeightScale;
            var radiusY = PixelMath.Clamp(halfBreadth * 0.20d, 4d, 9d) * settings.superstructureHeightScale;
            shapes.Add(new FunnelShape(new PixelVector2(x, centerY), radiusX, radiusY));
        }

        return shapes;
    }

    static PixelRect ExpandPixelRect(PixelRect rect, double amount)
    {
        return new PixelRect(rect.xMin - amount, rect.yMin - amount, rect.width + amount * 2d, rect.height + amount * 2d);
    }

    static PixelVector2 GetSubmergedMountOutwardDirection(PlaceholderMountLocation location, PixelVector2 fallbackDirection)
    {
        return location switch
        {
            PlaceholderMountLocation.Forward => PixelVector2.right,
            PlaceholderMountLocation.After => PixelVector2.left,
            PlaceholderMountLocation.PortForward => PixelVector2.up,
            PlaceholderMountLocation.PortMidship => PixelVector2.up,
            PlaceholderMountLocation.PortAfter => PixelVector2.up,
            PlaceholderMountLocation.StarboardForward => PixelVector2.down,
            PlaceholderMountLocation.StarboardMidship => PixelVector2.down,
            PlaceholderMountLocation.StarboardAfter => PixelVector2.down,
            _ => PixelMath.Abs(fallbackDirection.x) >= PixelMath.Abs(fallbackDirection.y)
                ? new PixelVector2(PixelMath.Sign(fallbackDirection.x), 0d)
                : new PixelVector2(0d, PixelMath.Sign(fallbackDirection.y))
        };
    }

    static void DrawPhysicalGunMount(PixelCanvas canvas,PixelVector2 pos,PixelVector2 direction,ShipGraphicPart part,
        double width,double length,double pixelsPerMeter,double weaponScale,RgbaColor fill,RgbaColor outline)
    {
        width*=weaponScale;length*=weaponScale;pixelsPerMeter*=weaponScale;
        if(canvas.Capture(ShipGraphicPartKind.Gun,pos,width,length,direction,part.Barrels))return;
        // Fixed base is a separate footprint, not an enlarged rotating gunhouse.
        if(part.BarbetteDiameter>0)
        {
            var radius=part.BarbetteDiameter*pixelsPerMeter/2;
            canvas.FillEllipse(pos,radius,radius,outline);
            canvas.FillEllipse(pos,Math.Max(0,radius-1),Math.Max(0,radius-1),new RgbaColor(fill.r,fill.g,fill.b,255));
        }
        var side=new PixelVector2(-direction.y,direction.x);
        var spacing=width/Math.Max(2,part.Barrels+1);
        var pivot=pos+new PixelVector2(part.GunPivotOffset.Y,part.GunPivotOffset.X)*pixelsPerMeter;
        for(var i=0;i<part.Barrels;i++)
        {
            var at=pivot+side*((i-(part.Barrels-1)/2d)*spacing);
            var start=part.BarrelStart*pixelsPerMeter;var tube=part.BarrelLength*pixelsPerMeter;
            var clip=ShipGraphicLayoutBuilder.VisibleBarrelStart(part,length/pixelsPerMeter)*pixelsPerMeter;
            var stations=new[]{(T:0d,D:3d),(T:.2,D:2.8),(T:.6,D:1.8),(T:1d,D:1.4)};
            for(var j=0;j<stations.Length-1;j++)
            {
                var a=stations[j];var b=stations[j+1];
                var t=Math.Max(a.T,(clip-start)/tube);if(t>=b.T)continue;
                var d=a.D+(b.D-a.D)*(t-a.T)/(b.T-a.T);
                var ra=Math.Max(1,part.Caliber*pixelsPerMeter*d/2);
                var rb=Math.Max(1,part.Caliber*pixelsPerMeter*b.D/2);
                var from=at+direction*(start+tube*t);var to=at+direction*(start+tube*b.T);
                // Flat end polygons avoid the old round stroke cap protruding behind the house.
                Polygon([from-side*ra,to-side*rb,to+side*rb,from+side*ra],outline);
            }
        }
        void Polygon(PixelVector2[] points,RgbaColor? color=null)
        {
            // Fill a small convex gunhouse in screen space, then outline its actual footprint.
            for(var y=Math.Max(0,(int)Math.Floor(points.Min(p=>p.y)));y<=Math.Min(canvas.height-1,(int)Math.Ceiling(points.Max(p=>p.y)));y++)
            {
                var crossings=new List<double>();
                for(var i=0;i<points.Length;i++)
                {
                    var a=points[i];var b=points[(i+1)%points.Length];
                    if((a.y<=y && b.y>y)||(b.y<=y && a.y>y))crossings.Add(a.x+(b.x-a.x)*(y-a.y)/(b.y-a.y));
                }
                if(crossings.Count>=2)
                    for(var x=Math.Max(0,(int)Math.Ceiling(crossings.Min()));x<=Math.Min(canvas.width-1,(int)Math.Floor(crossings.Max()));x++)
                        canvas.SetPixel(x,y,color??new RgbaColor(fill.r,fill.g,fill.b,255));
            }
            for(var i=0;i<points.Length;i++)canvas.DrawLine(points[i],points[(i+1)%points.Length],1,outline);
        }
        if(part.Appearance==ShipGunAppearance.Turret)
        {
            var forward=direction;
            if(part.FixedHousing) { var a=part.RestAngle*Math.PI/180;forward=new PixelVector2(Math.Cos(a),Math.Sin(a)); }
            var across=new PixelVector2(-forward.y,forward.x);
            Polygon(ShipGraphicLayoutBuilder.HousingOutline(part.HousingShape)
                .Select(v=>pos+across*(v.X*width)+forward*(v.Y*length)).ToArray());
        }
        else if(part.Appearance==ShipGunAppearance.FixedMount)
        {
            var a=part.RestAngle*Math.PI/180;
            var forward=new PixelVector2(Math.Cos(a),Math.Sin(a));
            var across=new PixelVector2(-forward.y,forward.x);
            Polygon([pos-across*(width*.4)-forward*(length*.45),pos+across*(width*.4)-forward*(length*.45),
                pos+across*(width*.4)+forward*(length*.45),pos-across*(width*.4)+forward*(length*.45)]);
        }
        else if(part.Appearance==ShipGunAppearance.Casemate)
        {
            canvas.FillEllipse(pos,length/2,width/2,outline);
            if(length>2 && width>2)
                canvas.FillEllipse(pos,length/2-1,width/2-1,new RgbaColor(fill.r,fill.g,fill.b,255));
        }
        else
        {
            var radius=Math.Max(2,width*(part.Appearance==ShipGunAppearance.Shield?.25:.2));
            canvas.FillEllipse(pos,radius,radius,outline);
            if(part.Appearance==ShipGunAppearance.Shield)
                canvas.DrawLine(pos+direction*(length*.15)-side*(width/2),pos+direction*(length*.15)+side*(width/2),Math.Max(1,(int)Math.Round(.15*pixelsPerMeter)),outline);
        }
    }

    static void DrawGunMount(PixelCanvas canvas, PixelVector2 pos, PixelVector2 direction, int barrels, double size, int lineWidth, RgbaColor fill, RgbaColor outline, RgbaColor detailFill)
    {
        if (canvas.Capture(ShipGraphicPartKind.Gun, pos, size, size * 1.5, direction, barrels)) return;
        var lines = PixelMath.Clamp(barrels, 1, 4);
        var footprintSize = size * (1d + 0.3d * (lines - 1));
        var bodyLength = footprintSize * 1.25d;
        var bodyWidth = PixelMath.Max(4d, footprintSize * 0.85d);
        var perpendicular = new PixelVector2(-direction.y, direction.x);
        var front = pos + direction * (bodyLength * 0.42d);
        var rear = pos - direction * (bodyLength * 0.36d);

        canvas.FillEllipse(pos, bodyLength * 0.58d, bodyWidth * 0.72d, detailFill);
        canvas.DrawPixelRectOutline(new PixelRect(pos.x - bodyLength * 0.58d, pos.y - bodyWidth * 0.72d, bodyLength * 1.16d, bodyWidth * 1.44d), 1, outline);
        canvas.FillEllipse(front, bodyWidth * 0.48d, bodyWidth * 0.48d, fill);
        canvas.FillEllipse(rear, bodyWidth * 0.38d, bodyWidth * 0.32d, detailFill);

        var barrelSpacing = PixelMath.Min(bodyWidth * 0.28d, footprintSize * 0.24d + 0.45d);
        var barrelThickness = PixelMath.Max(PixelMath.Max(2, lineWidth + 1), PixelMath.RoundToInt(size * 0.18d));
        for (var i = 0; i < lines; i++)
        {
            var spread = lines == 1 ? 0d : (i - (lines - 1) / 2d) * barrelSpacing;
            var from = front + perpendicular * spread;
            var to = from + direction * (size + 6d);
            canvas.DrawLine(from, to, barrelThickness, outline);
        }

        canvas.DrawLine(rear, front, 1, outline);
    }

    static void DrawSubmergedTorpedoMount(PixelCanvas canvas, PixelVector2 pos, PixelVector2 direction, int barrels, double size, RgbaColor fill, RgbaColor outline)
    {
        if (canvas.Capture(ShipGraphicPartKind.SubmergedTorpedo, pos, size * .44, size * 1.7, direction, barrels)) return;
        var along = direction.sqrMagnitude < 0.0001d ? PixelVector2.right : direction.normalized;
        var perp = new PixelVector2(-along.y, along.x);
        var halfLength = size * 0.85d;
        var halfWidth = PixelMath.Max(1.8d, size * 0.22d);
        var bodyStart = pos - along * halfLength;
        var bodyEnd = pos + along * halfLength;
        var outlineThickness = PixelMath.Max(2, PixelMath.RoundToInt(halfWidth * 2d + 1d));
        var fillThickness = PixelMath.Max(1, PixelMath.RoundToInt(halfWidth * 2d - 1d));

        canvas.DrawLine(bodyStart, bodyEnd, outlineThickness, outline);
        canvas.DrawLine(bodyStart, bodyEnd, fillThickness, fill);

        var tubes = PixelMath.Clamp(barrels, 1, 4);
        for (var i = 0; i < tubes; i++)
        {
            var spread = tubes == 1 ? 0d : PixelMath.Lerp(-halfWidth, halfWidth, i / (double)(tubes - 1));
            var start = pos + perp * spread - along * halfLength * 0.7d;
            var end = pos + perp * spread + along * halfLength * 0.7d;
            canvas.DrawLine(start, end, 1, outline);
        }
    }

    static void DrawPhysicalTorpedoMount(PixelCanvas canvas,PixelVector2 pos,PixelVector2 direction,ShipGraphicPart part,double scale,double weaponScale,RgbaColor outline,RgbaColor fill,RgbaColor baseFill)
    {
        var across=new PixelVector2(-direction.y,direction.x);
        var thickness=Math.Max(1,part.Caliber*scale);
        if(part.Trainable)
        {
            var radius=Math.Max(.35,(part.Caliber+part.TubeSpacing*(part.Barrels-1))*.6)*scale;
            canvas.FillEllipse(pos,radius+1,radius+1,outline);
            canvas.FillEllipse(pos,radius,radius,baseFill);
        }
        for(var i=0;i<part.Barrels;i++)
        {
            var offset=i-(part.Barrels-1)/2d;
            var a=offset*part.TubeSplay*Math.PI/180;
            var along=direction*Math.Cos(a)+across*Math.Sin(a);
            var perp=new PixelVector2(-along.y,along.x);
            var center=pos+across*(offset*part.TubeSpacing*scale);
            var start=center+along*(part.BarrelStart*scale);
            var end=start+along*(part.BarrelLength*scale);
            // Symbol scale only thickens outlines; physical tube length and pivot never move.
            var stroke=Math.Max(1,weaponScale);
            var polygon=new[]{start-perp*(thickness/2),end-perp*(thickness/2),end+perp*(thickness/2),start+perp*(thickness/2)};
            canvas.FillPolygon(polygon,fill);
            for(var k=0;k<4;k++)canvas.DrawLine(polygon[k],polygon[(k+1)%4],Math.Max(1,PixelMath.RoundToInt(stroke)),outline);
            foreach(var t in new[]{.08,.88})
            {
                var band=start+(end-start)*t;
                canvas.DrawLine(band-perp*(thickness/2),band+perp*(thickness/2),1,outline);
            }
        }
    }

    static void DrawDeckTorpedoMount(PixelCanvas canvas, PixelVector2 pos, PixelVector2 direction, int barrels, double size, bool trainable, RgbaColor outline, RgbaColor detailFill, RgbaColor torpedoBaseFill)
    {
        if (canvas.Capture(ShipGraphicPartKind.DeckTorpedo, pos, size, size * 1.9, direction, barrels)) return;
        var along = direction.sqrMagnitude < 0.0001d ? PixelVector2.right : direction.normalized;
        var perp = new PixelVector2(-along.y, along.x);
        var tubes = PixelMath.Clamp(barrels, 1, 4);
        var tubeLength = size * (trainable ? 1.85d : 1.95d);
        var tubeThickness = trainable
            ? PixelMath.Clamp(size * 0.18d, 4d, 11d)
            : PixelMath.Clamp(size * 0.27d, 3.5d, 7.5d);
        var tubeSpacing = tubes == 1 ? 0d : PixelMath.Max(tubeThickness * 1.2d, size * 0.26d);
        var bankWidth = tubeThickness + tubeSpacing * (tubes - 1);

        if (trainable)
        {
            var pivotRadius = PixelMath.Max(bankWidth * 0.72d, size * 0.50d);
            canvas.FillEllipse(pos, pivotRadius + 1.2d, pivotRadius + 1.2d, outline);
            canvas.FillEllipse(pos, pivotRadius, pivotRadius, torpedoBaseFill);
            canvas.DrawLine(pos - perp * pivotRadius, pos + perp * pivotRadius, 1, outline);
        }

        for (var i = 0; i < tubes; i++)
        {
            var spread = tubes == 1 ? 0d : (i - (tubes - 1) / 2d) * tubeSpacing;
            var center = pos + perp * spread;
            var start = center - along * (tubeLength * 0.5d);
            var end = center + along * (tubeLength * 0.5d);
            var outlineThickness = PixelMath.Max(3, PixelMath.RoundToInt(tubeThickness + 2d));
            var fillThickness = PixelMath.Max(1, PixelMath.RoundToInt(tubeThickness));
            canvas.DrawLine(start, end, outlineThickness, outline);
            canvas.DrawLine(start, end, fillThickness, detailFill);
            canvas.DrawLine(start + along * (tubeLength * 0.12d), end - along * (tubeLength * 0.12d), 1, outline);
            canvas.FillEllipse(end, tubeThickness * 0.55d, tubeThickness * 0.55d, outline);
            canvas.FillEllipse(end, tubeThickness * 0.28d, tubeThickness * 0.28d, detailFill);
        }

        if (trainable)
        {
            canvas.FillEllipse(pos, PixelMath.Max(1.6d, tubeThickness * 0.42d), PixelMath.Max(1.6d, tubeThickness * 0.42d), outline);
            canvas.DrawLine(pos, pos + along * (tubeLength * 0.62d), 1, outline);
        }
    }

    static PixelVector2 GetBarrelDirection(PlaceholderMountLocation location)
    {
        return location switch
        {
            PlaceholderMountLocation.PortForward => new PixelVector2(0.8d, -0.55d).normalized,
            PlaceholderMountLocation.Forward => PixelVector2.right,
            PlaceholderMountLocation.StarboardForward => new PixelVector2(0.8d, 0.55d).normalized,
            PlaceholderMountLocation.PortMidship => PixelVector2.up,
            PlaceholderMountLocation.Midship => PixelVector2.right,
            PlaceholderMountLocation.StarboardMidship => PixelVector2.down,
            PlaceholderMountLocation.PortAfter => new PixelVector2(-0.8d, -0.55d).normalized,
            PlaceholderMountLocation.After => PixelVector2.left,
            PlaceholderMountLocation.StarboardAfter => new PixelVector2(-0.8d, 0.55d).normalized,
            _ => PixelVector2.right
        };
    }

    static PixelVector2 ResolveMountDirection(PlaceholderMountRenderRecord record)
    {
        if (double.IsFinite(record.mountDirectionDeg))
        {
            return AngleDegToScreenVector(record.mountDirectionDeg);
        }

        var avgAngle = ResolvePreferredArcAngle(record);
        if (avgAngle.HasValue)
        {
            return AngleDegToScreenVector(avgAngle.Value);
        }

        return GetBarrelDirection(record.mountLocation);
    }

    static double? ResolvePreferredArcAngle(PlaceholderMountRenderRecord record)
    {
        if (record.mountArcs == null || record.mountArcs.Count == 0)
            return null;

        var vectors = new List<PixelVector2>();
        foreach (var arc in record.mountArcs)
        {
            var midDeg = NormalizeAngle(arc.startDeg + arc.CoverageDeg / 2d);
            vectors.Add(AngleDegToScreenVector(midDeg));
        }

        if (vectors.Count == 0)
            return null;

        var sum = PixelVector2.zero;
        foreach (var v in vectors)
        {
            sum += v;
        }

        if (sum.sqrMagnitude < 0.0001d)
            return null;

        return ScreenVectorToAngleDeg(sum.normalized);
    }

    static PixelVector2 AngleDegToScreenVector(double angleDeg)
    {
        var rad = angleDeg * PixelMath.Deg2Rad;
        return new PixelVector2(PixelMath.Cos(rad), PixelMath.Sin(rad)).normalized;
    }

    static double ScreenVectorToAngleDeg(PixelVector2 vector)
    {
        var rad = PixelMath.Atan2(vector.y, vector.x);
        return NormalizeAngle(rad * PixelMath.Rad2Deg);
    }

    static double NormalizeAngle(double angleDeg)
    {
        var normalized = angleDeg % 360d;
        return normalized < 0d ? normalized + 360d : normalized;
    }

    static double NormalizeForeAftX(double normalizedX) => 1d - normalizedX;

    static double EstimateVisualPadding(ShipClassPlaceholderRenderInput shipClass, ShipClassPlaceholderImageRenderSettings settings)
    {
        var maxBatteryGlyph = (shipClass.batteryRecords ?? new List<PlaceholderBatteryRenderRecord>())
            .Select(b => EvaluateBatteryGlyphSize(b.shellSizeInch))
            .DefaultIfEmpty(6d)
            .Max() * settings.weaponScale;
        var maxTorpedoGlyph = (shipClass.torpedoMountRecords ?? new List<PlaceholderMountRenderRecord>())
            .Select(r => PixelMath.Clamp(5d + r.barrels * 0.7d, 5d, 11d))
            .DefaultIfEmpty(5d)
            .Max() * settings.weaponScale;

        var weaponPadding = PixelMath.Max(maxBatteryGlyph * 1.2d, maxTorpedoGlyph * 1.15d);
        var padding=PixelMath.Max(settings.hullPadding + weaponPadding, settings.hullPadding + settings.lineWidth * 2d + 6d);
        // Semantic mount widths can exceed the old fixed glyph estimate. Reserve space for
        // outward-facing barrels at any training angle without changing physical positions.
        var (_,beam)=ShipGraphicLayoutBuilder.Dimensions(shipClass);
        var extent=.5;
        foreach(var battery in shipClass.batteryRecords)
        foreach(var mount in battery.mountLocationRecords)
        {
            var name=mount.mountLocation.ToString();
            var side=name.StartsWith("Port") || name.StartsWith("Starboard");
            var factor=settings.weaponScale*(1+.525*(1+.3*(Math.Clamp(mount.barrels,1,4)-1)));
            extent=Math.Max(extent,(side?.43:0)+ShipGraphicLayoutBuilder.GunSize(battery.shellSizeInch,mount.barrels,beam,ShipGraphicLayoutBuilder.DefaultAppearance(battery.shellSizeInch,mount)).Width/beam*factor);
            var calibers=double.IsFinite(battery.barrelLengthCalibers) && battery.barrelLengthCalibers>0?battery.barrelLengthCalibers:30;
            var nominal=Math.Max(.1,battery.shellSizeInch)*.0254*calibers;
            // Reserve the full nominal barrel reach at any bearing, including known long guns.
            extent=Math.Max(extent,(side?.43:0)+nominal/beam*settings.weaponScale);
            foreach(var o in shipClass.graphicParameters?.mounts ?? new())
                if(o.batteryId==battery.objectId && o.mountRecordId==mount.objectId)
                {
                    extent=Math.Max(extent,Math.Abs(o.x)+Math.Max(0,o.width)*factor);
                    var (shipLength,_)=ShipGraphicLayoutBuilder.Dimensions(shipClass);
                    extent=Math.Max(extent,Math.Abs(o.x)+Math.Max(0,o.barbetteDiameter)/beam*.5*settings.weaponScale);
                    var reach=o.barrelLength>0 && double.IsFinite(o.barrelLength)?o.barrelLength:nominal;
                    if(o.exposedBarrelLength>0 && double.IsFinite(o.exposedBarrelLength))
                        reach=o.exposedBarrelLength+Math.Max(0,o.length)*shipLength*.5;
                    extent=Math.Max(extent,Math.Abs(o.x)+reach/beam*settings.weaponScale);
                }
        }
        var fittedHeight=Math.Max(1,settings.canvasHeight-2*(6+settings.lineWidth))/Math.Max(1,2*extent);
        return Math.Max(padding,(settings.canvasHeight-fittedHeight)/2);
    }

    static double SampleHullHalfBreadth(double[] topEdge, double[] bottomEdge, int x)
    {
        x = PixelMath.Clamp(x, 0, topEdge.Length - 1);
        if (double.IsNaN(topEdge[x]))
            return 0;
        return (bottomEdge[x] - topEdge[x]) / 2d;
    }

    static double EvaluateHalfBreadth(double t, HullProfile profile, ShipClassPlaceholderImageRenderSettings settings)
    {
        t = PixelMath.Clamp01(t);
        // Use a simple 30/40/30 placeholder hull: sinusoidal bow entry, parallel midbody,
        // sinusoidal stern run. This keeps zero width at both ends and a flat max beam amidships.
        const double endSection = 0.3d;
        const double middleEnd = 0.7d;

        if (t < endSection)
        {
            var localT = t / endSection;
            return profile.maxBeamScale * PixelMath.Sin(localT * PixelMath.PI * 0.5d);
        }

        if (t <= middleEnd)
            return profile.maxBeamScale;

        var sternLocalT = (1d - t) / endSection;
        return profile.maxBeamScale * PixelMath.Sin(sternLocalT * PixelMath.PI * 0.5d);
    }

    static double SmootherStep01(double t)
    {
        t = PixelMath.Clamp01(t);
        return t * t * t * (t * (t * 6d - 15d) + 10d);
    }

    static HullProfile GetHullProfile(PlaceholderShipType shipType)
    {
        if (HullProfiles.TryGetValue(shipType, out var profile))
            return profile;
        return HullProfiles[PlaceholderShipType.NotSpecified];
    }

    static int ResolveFunnelCount(ShipClassPlaceholderRenderInput shipClass, ShipClassPlaceholderImageRenderSettings settings)
    {
        return settings.funnelCountMode switch
        {
            PlaceholderFunnelCountMode.Zero => 0,
            PlaceholderFunnelCountMode.One => 1,
            PlaceholderFunnelCountMode.Two => 2,
            PlaceholderFunnelCountMode.Three => 3,
            _ => InferFunnelCount(shipClass)
        };
    }

    static int InferFunnelCount(ShipClassPlaceholderRenderInput shipClass)
    {
        if (shipClass.type == PlaceholderShipType.Transport || shipClass.type == PlaceholderShipType.Repair)
            return shipClass.displacementTons > 7000 ? 2 : 1;
        if (shipClass.type == PlaceholderShipType.TorpedoBoat)
            return shipClass.speedKnots >= 24 ? 2 : 1;
        if (shipClass.type == PlaceholderShipType.Destroyer)
            return shipClass.speedKnots >= 27 ? 3 : 2;
        if (shipClass.type == PlaceholderShipType.Battleship || shipClass.type == PlaceholderShipType.ArmoredCruiser)
            return shipClass.displacementTons >= 9000 ? 2 : 1;
        // if (shipClass.type == PlaceholderShipType.Cruiser || shipClass.type == PlaceholderShipType.LightCruiser || shipClass.type == PlaceholderShipType.ArmedMerchantCruiser)
        if (shipClass.type == PlaceholderShipType.LightCruiser || shipClass.type == PlaceholderShipType.ArmedMerchantCruiser)
            return shipClass.speedKnots >= 21 ? 2 : 1;
        return shipClass.displacementTons >= 6000 ? 2 : 1;
    }

    static RenderPalette GetPalette(ShipClassPlaceholderImageRenderSettings settings, RenderVariant variant)
    {
        return variant switch
        {
            RenderVariant.TopJpg => new RenderPalette
            {
                background = new RgbaColor(255, 255, 255, 255),
                hullFill = new RgbaColor(255, 255, 255, 255),
                hullOutline = new RgbaColor(28, 28, 28, 255),
                interiorLine = new RgbaColor(0, 0, 0, 255),
                detailFill = new RgbaColor(255, 255, 255, 255),
                torpedoBaseFill = new RgbaColor(210, 210, 210, 255),
                mountFill = new RgbaColor(36, 36, 36, 255),
                rapidFire = new RgbaColor(0, 0, 0, 255),
                funnelFill = new RgbaColor(255, 255, 255, 255),
                funnelOutline = new RgbaColor(52, 52, 52, 255),
                mastLine = new RgbaColor(44, 44, 44, 230),
            },
            RenderVariant.IconPng => new RenderPalette
            {
                background = new RgbaColor(0, 0, 0, 0),
                hullFill = new RgbaColor(255, 255, 255, 255),
                hullOutline = new RgbaColor(0, 0, 0, 255),
                interiorLine = new RgbaColor(0, 0, 0, 255),
                detailFill = new RgbaColor(255, 255, 255, 255),
                torpedoBaseFill = new RgbaColor(220, 220, 220, 255),
                mountFill = new RgbaColor(0, 0, 0, 255),
                rapidFire = new RgbaColor(0, 0, 0, 255),
                funnelFill = new RgbaColor(255, 255, 255, 255),
                funnelOutline = new RgbaColor(0, 0, 0, 235),
                mastLine = new RgbaColor(0, 0, 0, 220),
            },
            _ => new RenderPalette
            {
                background = new RgbaColor(0, 0, 0, 0),
                hullFill = new RgbaColor(255, 255, 255, 255),
                hullOutline = new RgbaColor(0, 0, 0, 255),
                interiorLine = new RgbaColor(0, 0, 0, 230),
                detailFill = new RgbaColor(255, 255, 255, 220),
                torpedoBaseFill = new RgbaColor(210, 210, 210, 230),
                mountFill = new RgbaColor(0, 0, 0, 255),
                rapidFire = new RgbaColor(0, 0, 0, 220),
                funnelFill = new RgbaColor(255, 255, 255, 230),
                funnelOutline = new RgbaColor(0, 0, 0, 220),
                mastLine = new RgbaColor(0, 0, 0, 190),
            }
        };
    }
}
