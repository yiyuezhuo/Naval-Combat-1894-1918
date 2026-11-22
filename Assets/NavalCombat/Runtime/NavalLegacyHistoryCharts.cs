using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using NavalCombatCore;
namespace NavalCombat.Unity {
public class HistoryPieSlice
{
    public string label;
    public float value;
    public Color color;
    public int hitCount;
    public List<float> hitValues = new();
}

class HistoryBarSegment
{
    public string label;
    public int count;
    public Color color;
}

class HistoryBarItem
{
    public string label;
    public List<HistoryBarSegment> segments = new();
    public int TotalCount => segments.Sum(segment => Mathf.Max(0, segment.count));
}

public partial class HistoryPieChart : VisualElement
{
    List<HistoryPieSlice> slices = new();

    public HistoryPieChart()
    {
        style.flexGrow = 1;
        generateVisualContent += OnGenerateVisualContent;
    }

    public void SetSlices(IEnumerable<HistoryPieSlice> newSlices)
    {
        slices = newSlices?.Where(s => s != null && s.value > 0).ToList() ?? new();
        MarkDirtyRepaint();
    }

    void OnGenerateVisualContent(MeshGenerationContext context)
    {
        var painter = context.painter2D;
        var width = contentRect.width;
        var height = contentRect.height;
        var radius = Mathf.Max(0, Mathf.Min(width, height) * 0.5f - 6f);
        var center = new Vector2(width * 0.5f, height * 0.5f);

        painter.lineWidth = 1f;
        painter.lineCap = LineCap.Butt;

        var total = slices.Sum(s => Mathf.Max(0, s.value));
        if (radius <= 0)
            return;

        if (total <= 0.0001f)
        {
            DrawFullCircle(painter, center, radius, new Color(0.85f, 0.85f, 0.85f, 1f));
            return;
        }

        if (slices.Count == 1)
        {
            DrawFullCircle(painter, center, radius, slices[0].color);
            return;
        }

        var startAngle = -90f;
        foreach (var slice in slices)
        {
            var sweep = 360f * slice.value / total;
            if (sweep <= 0.01f)
                continue;

            if (sweep >= 359.99f)
            {
                DrawFullCircle(painter, center, radius, slice.color);
                return;
            }

            painter.fillColor = slice.color;
            painter.strokeColor = Color.black;
            painter.BeginPath();
            painter.MoveTo(center);
            painter.Arc(center, radius, startAngle, startAngle + sweep);
            painter.ClosePath();
            painter.Fill();
            painter.Stroke();
            startAngle += sweep;
        }
    }

    void DrawFullCircle(Painter2D painter, Vector2 center, float radius, Color fillColor)
    {
        painter.fillColor = fillColor;
        painter.strokeColor = Color.black;
        painter.BeginPath();
        painter.Arc(center, radius, 0f, 360f);
        painter.ClosePath();
        painter.Fill();
        painter.Stroke();
    }
}


public partial class HistoryStackedBarChart : VisualElement
{
    const float TopPadding = 8f;
    const float SidePadding = 8f;
    const float BottomPadding = 66f;
    const float BarGap = 18f;
    const float SegmentLabelPreferredHeight = 16f;

    readonly VisualElement labelLayer = new();
    List<HistoryBarItem> bars = new();

    public HistoryStackedBarChart()
    {
        style.flexGrow = 1;
        labelLayer.style.position = Position.Absolute;
        labelLayer.style.left = 0;
        labelLayer.style.top = 0;
        labelLayer.style.right = 0;
        labelLayer.style.bottom = 0;
        labelLayer.pickingMode = PickingMode.Ignore;
        Add(labelLayer);
        generateVisualContent += OnGenerateVisualContent;
        RegisterCallback<GeometryChangedEvent>(_ => RebuildLabels());
    }

    internal void SetBars(IEnumerable<HistoryBarItem> newBars)
    {
        bars = newBars?.Where(bar => bar != null).ToList() ?? new();
        RebuildLabels();
        MarkDirtyRepaint();
    }

    void OnGenerateVisualContent(MeshGenerationContext context)
    {
        var painter = context.painter2D;
        var chartRect = GetChartRect();
        if (chartRect.width <= 0 || chartRect.height <= 0 || bars.Count == 0)
            return;

        painter.lineWidth = 1f;
        painter.lineCap = LineCap.Butt;
        painter.strokeColor = Color.black;

        var maxTotal = Mathf.Max(1, bars.Max(bar => bar.TotalCount));
        var barRects = GetBarRects(chartRect);
        var baselineY = chartRect.yMax;
        painter.BeginPath();
        painter.MoveTo(new Vector2(chartRect.xMin, baselineY));
        painter.LineTo(new Vector2(chartRect.xMax, baselineY));
        painter.Stroke();

        for (int barIdx = 0; barIdx < bars.Count; barIdx++)
        {
            var bar = bars[barIdx];
            var barRect = barRects[barIdx];
            var currentTop = chartRect.yMax;

            foreach (var segment in bar.segments.Where(segment => segment.count > 0))
            {
                var segmentHeight = chartRect.height * segment.count / maxTotal;
                currentTop -= segmentHeight;
                var segmentRect = new Rect(barRect.x, currentTop, barRect.width, segmentHeight);
                DrawRect(painter, segmentRect, segment.color);
            }
        }
    }

    void RebuildLabels()
    {
        labelLayer.Clear();
        var chartRect = GetChartRect();
        if (chartRect.width <= 0 || chartRect.height <= 0 || bars.Count == 0)
            return;

        var maxTotal = Mathf.Max(1, bars.Max(bar => bar.TotalCount));
        var barRects = GetBarRects(chartRect);

        for (int barIdx = 0; barIdx < bars.Count; barIdx++)
        {
            var bar = bars[barIdx];
            var barRect = barRects[barIdx];
            var currentTop = chartRect.yMax;

            foreach (var segment in bar.segments.Where(segment => segment.count > 0))
            {
                var segmentHeight = chartRect.height * segment.count / maxTotal;
                currentTop -= segmentHeight;
                var text = BuildSegmentText(segment.count, bar.TotalCount, barRect.width);
                var requiredHeight = text.Contains('\n') ? SegmentLabelPreferredHeight * 2 : SegmentLabelPreferredHeight;
                if (segmentHeight < requiredHeight + 4f || barRect.width < 36f)
                    continue;
                var labelHeight = segmentHeight;
                var labelTop = currentTop + segmentHeight * 0.5f - labelHeight * 0.5f;

                var label = BuildOverlayLabel(
                    text,
                    barRect.x,
                    labelTop,
                    barRect.width,
                    labelHeight);
                label.style.unityFontStyleAndWeight = FontStyle.Bold;
                label.style.fontSize = 11;
                label.style.whiteSpace = WhiteSpace.Normal;
                label.style.color = ResolveSegmentLabelTextColor(segment.color);
                labelLayer.Add(label);
            }

            var xLabelWidth = Mathf.Max(barRect.width + 18f, 68f);
            var xLabel = BuildOverlayLabel(
                bar.label,
                barRect.x + barRect.width * 0.5f - xLabelWidth * 0.5f,
                chartRect.yMax + 6f,
                xLabelWidth,
                BottomPadding - 8f);
            xLabel.style.whiteSpace = WhiteSpace.Normal;
            xLabel.style.fontSize = 11;
            xLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            xLabel.style.unityTextAlign = TextAnchor.UpperCenter;
            labelLayer.Add(xLabel);
        }
    }

    Rect GetChartRect()
    {
        var width = Mathf.Max(0, contentRect.width - SidePadding * 2f);
        var height = Mathf.Max(0, contentRect.height - TopPadding - BottomPadding);
        return new Rect(SidePadding, TopPadding, width, height);
    }

    internal List<Rect> GetColumnRects() => GetBarRects(GetChartRect());

    List<Rect> GetBarRects(Rect chartRect)
    {
        if (bars.Count == 0)
            return new();

        var totalGap = BarGap * Mathf.Max(0, bars.Count - 1);
        var rawBarWidth = (chartRect.width - totalGap) / Mathf.Max(1, bars.Count);
        var barWidth = Mathf.Max(14f, rawBarWidth);
        if (barWidth * bars.Count + totalGap > chartRect.width)
        {
            barWidth = Mathf.Max(10f, (chartRect.width - totalGap) / Mathf.Max(1, bars.Count));
        }

        var totalBarsWidth = barWidth * bars.Count + totalGap;
        var startX = chartRect.xMin + Mathf.Max(0, (chartRect.width - totalBarsWidth) * 0.5f);
        var rects = new List<Rect>(bars.Count);
        for (int idx = 0; idx < bars.Count; idx++)
        {
            rects.Add(new Rect(startX + idx * (barWidth + BarGap), chartRect.yMin, barWidth, chartRect.height));
        }
        return rects;
    }

    static Label BuildOverlayLabel(string text, float x, float y, float width, float height, Color? color = null)
    {
        var label = new Label(text);
        label.pickingMode = PickingMode.Ignore;
        label.style.position = Position.Absolute;
        label.style.left = x;
        label.style.top = y;
        label.style.width = width;
        label.style.height = height;
        label.style.marginLeft = label.style.marginRight = label.style.marginTop = label.style.marginBottom = 0;
        label.style.paddingLeft = label.style.paddingRight = label.style.paddingTop = label.style.paddingBottom = 0;
        if (color.HasValue)
        {
            label.style.color = color.Value;
        }
        label.style.unityTextAlign = TextAnchor.MiddleCenter;
        label.style.whiteSpace = WhiteSpace.NoWrap;
        label.style.fontSize = 11;
        label.style.overflow = Overflow.Visible;
        return label;
    }

    static void DrawRect(Painter2D painter, Rect rect, Color fillColor)
    {
        if (rect.width <= 0 || rect.height <= 0)
            return;

        painter.fillColor = fillColor;
        painter.BeginPath();
        painter.MoveTo(new Vector2(rect.xMin, rect.yMin));
        painter.LineTo(new Vector2(rect.xMax, rect.yMin));
        painter.LineTo(new Vector2(rect.xMax, rect.yMax));
        painter.LineTo(new Vector2(rect.xMin, rect.yMax));
        painter.ClosePath();
        painter.Fill();
        painter.Stroke();
    }

    static string BuildSegmentText(int count, int totalCount, float availableWidth)
    {
        var ratio = totalCount <= 0 ? 0f : (float)count / totalCount;
        var ratioText = $"({Mathf.RoundToInt(ratio * 100f)}%)";
        var singleLineText = $"{count} {ratioText}";
        return availableWidth >= EstimateSingleLineWidth(singleLineText)
            ? singleLineText
            : $"{count}\n{ratioText}";
    }

    static float EstimateSingleLineWidth(string text)
    {
        if (string.IsNullOrEmpty(text))
            return 0f;

        return text.Length * 6.6f + 10f;
    }

    static Color ResolveSegmentLabelTextColor(Color background)
    {
        return new Color(0.95f, 0.91f, 0.82f, 1f);
    }
}

}
