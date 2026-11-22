using CoreUtils;
using NavalCombatCore;
using UnityEngine;
using UnityEngine.UIElements;

namespace NavalCombat.Unity
{
public sealed partial class NavalWorld
{
    internal sealed class RangeOptions
    {
        public int? TargetSize;
        public readonly HashSet<int> GunBatteries = new();
        public readonly HashSet<int> RapidBatteries = new();
        public bool Torpedoes;
        public int Revision;
    }

    internal RangeOptions RangeDisplay { get; } = new();
    readonly List<LineRenderer> rangeLines = new();
    Transform rangeRoot;
    internal ShipLog SelectedRangeShip;
    ShipLog drawnRangeShip;
    int drawnRevision;
    double drawnVisibility;

    void ClearRangeLines()
    {
        if (rangeRoot != null)
        {
            rangeRoot.gameObject.SetActive(false);
            Destroy(rangeRoot.gameObject);
            rangeRoot = null;
        }
        rangeLines.Clear();
        drawnRangeShip = null;
    }

    void ResetRangeOverlays()
    {
        ClearRangeLines();
        SelectedRangeShip = null;
    }

    void UpdateRangeOverlay(ShipLog ship)
    {
        var options = RangeDisplay;
        if (ViewMode != "Chart" || ship != SelectedRangeShip || ship?.IsOnMap() != true || !Session.CanInspect(ship)
            || (!options.TargetSize.HasValue && options.GunBatteries.Count == 0 && options.RapidBatteries.Count == 0 && !options.Torpedoes))
        {
            if (rangeRoot != null) ClearRangeLines();
            return;
        }
        var design = ship.shipClass;
        var scenario = Session.GameState.scenarioState;
        double visibility = options.TargetSize is int targetSize
            ? RuleChart.GetVisibilityRangeYards(design.targetSizeModifier, targetSize, scenario.visibility,
                scenario.GetSunPosition(ship.position).GetDayNightLevel(), !scenario.hasMoonlight,
                searchVesselIsNotAWarship: !design.IsCombatShip() && !ship.IsLandBattery(),
                searchVesselSpeedKnots: ship.speedKnots)
            : 0;
        if (ship != drawnRangeShip || options.Revision != drawnRevision || drawnVisibility != visibility)
        {
            ClearRangeLines();
            rangeRoot = Child("Ship range overlays", lineRoot);
            drawnRangeShip = ship;
            drawnRevision = options.Revision;
            drawnVisibility = visibility;
            AddRangeSector(visibility, 0, 360, new Color(.13f,.60f,.68f));
            foreach (int slot in options.GunBatteries)
            {
                if (slot >= design.batteryRecords.Count) continue;
                var battery = design.batteryRecords[slot];
                AddMountRanges(battery.mountLocationRecords, battery.rangeYards, new Color(.84f,.40f,.14f));
            }
            foreach (int slot in options.RapidBatteries)
            {
                if (slot >= design.rapidFireBatteryRecords.Count) continue;
                var rapid = design.rapidFireBatteryRecords[slot];
                var color = new Color(.64f,.49f,.10f);
                if (rapid.barrelsLevelStarboard.FirstOrDefault() > 0) AddRangeSector(rapid.maxRangeYards, 0, 180, color);
                if (rapid.barrelsLevelPort.FirstOrDefault() > 0) AddRangeSector(rapid.maxRangeYards, 180, 180, color);
            }
            var torpedoes = design.torpedoSector;
            if (options.Torpedoes)
                AddMountRanges(torpedoes.mountLocationRecords, torpedoes.torpedoSettings.Select(s=>s.rangeYards).DefaultIfEmpty().Max(), new Color(.53f,.32f,.70f));
        }
        rangeRoot.SetPositionAndRotation(Project(ship.position.LatDeg, ship.position.LonDeg) + Vector3.up * 2,
            Quaternion.Euler(0, (float)ship.headingDeg, 0));
        foreach (var line in rangeLines) line.startWidth = line.endWidth = ChartMetersPerPixel * 1.5f;
    }

    void AddMountRanges(IEnumerable<MountRecord> mounts, double yards, Color color)
    {
        var arcs = mounts.Where(m=>m.barrels > 0).SelectMany(m=>m.mountArcs).Select(a=>(a.startDeg,a.CoverageDeg)).Distinct();
        foreach (var arc in arcs) AddRangeSector(yards, arc.startDeg, arc.CoverageDeg, color);
    }

    void AddRangeSector(double yards, double start, double coverage, Color color)
    {
        if (yards <= 0 || coverage <= 0) return;
        bool circle = coverage >= 360;
        coverage = Math.Min(coverage, 360);
        int steps = Mathf.CeilToInt((float)coverage / 3);
        var line = Child("Range outline", rangeRoot).gameObject.AddComponent<LineRenderer>();
        line.gameObject.layer = ChartOverlayLayer;
        line.sharedMaterial = lineMaterial;
        line.useWorldSpace = false;
        line.loop = true;
        line.startColor = line.endColor = color;
        line.positionCount = circle ? steps : steps + 2;
        int index = 0;
        if (!circle) line.SetPosition(index++, Vector3.zero);
        for (int i = 0; index < line.positionCount; i++)
        {
            float bearing = (float)(start + coverage * i / steps) * Mathf.Deg2Rad;
            line.SetPosition(index++, new Vector3(Mathf.Sin(bearing), 0, Mathf.Cos(bearing)) * (float)(yards * .9144));
        }
        rangeLines.Add(line);
    }
}

public sealed partial class NavalGame
{
    static string RangeTitle() => Message("Ship ranges","射程・視認範囲","射程与可见范围","射程與可見範圍");

    void OpenRangeDialog()
    {
        var options = World.RangeDisplay;
        var body = new ScrollView();
        var sizes = RuleChart.visibilityDaylightExceptionallyClear.cols;
        var choices = new List<string> { Message("Hidden","非表示","不显示","不顯示") };
        choices.AddRange(sizes.Select(size=>size.ToString()));
        int selected = options.TargetSize.HasValue ? Array.IndexOf(sizes, options.TargetSize.Value) + 1 : 0;
        var field = new DropdownField(Message("Visibility · target size","視認範囲 · 目標サイズ","可见范围 · 目标尺寸","可見範圍 · 目標尺寸"), choices, selected);
        field.RegisterValueChangedCallback(_ => { options.TargetSize = field.index > 0 ? sizes[field.index-1] : (int?)null; options.Revision++; });
        field.tooltip = Message("Reference target at 18 kn, without silhouette bonus","基準目標は18ノット、シルエット補正なし","按18节参考目标估算，不含剪影加成","按18節參考目標估算，不含剪影加成");
        body.Add(field);
        void AddToggle(string label, bool enabled, Action<bool> change)
        {
            var toggle = new Toggle(label) { value = enabled };
            toggle.RegisterValueChangedCallback(e => { change(e.newValue); options.Revision++; });
            body.Add(toggle);
        }

        var designs = Session.GameState.shipLogs.Where(s => Session.CanInspect(s) && s.shipClass != null)
            .Select(s => s.shipClass).Distinct().ToList();
        void AddBatteries(string label, HashSet<int> enabledSlots, int count)
        {
            // Keep previously enabled slots editable when switching scenarios.
            count = Math.Max(count, enabledSlots.DefaultIfEmpty(-1).Max() + 1);
            for (int i = 0; i < count; i++)
            {
                int slot = i;
                AddToggle($"{label} {slot + 1}", enabledSlots.Contains(slot), show =>
                {
                    if (show) enabledSlots.Add(slot);
                    else enabledSlots.Remove(slot);
                });
            }
        }
        AddBatteries(Message("Gun battery","砲群","炮组","砲組"), options.GunBatteries,
            designs.Select(d => d.batteryRecords.Count).DefaultIfEmpty().Max());
        AddBatteries(Message("Rapid-fire battery","速射砲群","速射炮组","速射砲組"), options.RapidBatteries,
            designs.Select(d => d.rapidFireBatteryRecords.Count).DefaultIfEmpty().Max());
        AddToggle(Message("Torpedoes · maximum range","魚雷 · 最大射程","鱼雷 · 最大射程","魚雷 · 最大射程"),
            options.Torpedoes, show => options.Torpedoes = show);
        PushDialog(RangeTitle(), body, size:"medium");
    }
}
}
