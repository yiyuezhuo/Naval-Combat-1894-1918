using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using CoreUtils;

namespace NavalCombatCore;

public partial class NavalGameState
{
    [XmlIgnore] public bool FogOfWarEnabled { get; private set; }
    readonly HashSet<(ShipLog observer, ShipLog target)> visualContacts = new();
    HashSet<(string side, ShipLog target)> sideContacts = new(), previousSideContacts = new();
    readonly Dictionary<(string side, ShipLog target), DateTime> contactSince = new();
    public DateTime ContactSince(string side, ShipLog target) =>
        contactSince.TryGetValue((side, target), out var time) ? time : DateTime.MaxValue;

    readonly HashSet<ShipLog> sightedDuringMinute = new();

    public bool CanSee(ShipLog observer, ShipLog target) =>
        !FogOfWarEnabled || (observer != null && target != null && visualContacts.Contains((observer, target)));

    public bool SideHasContact(string sideId, ShipLog target) =>
        !FogOfWarEnabled || (target != null && sideContacts.Contains((sideId, target)));

    public void SetFogOfWar(bool enabled)
    {
        if (FogOfWarEnabled == enabled) return;
        FogOfWarEnabled = enabled;
        hostileProximitySnapshot = null;
        sightedDuringMinute.Clear();
        contactSince.Clear();
        visualContacts.Clear();
        sideContacts.Clear();
        previousSideContacts.Clear();
        if (enabled) RefreshVisualContacts();
    }

    public void RefreshVisualContacts()
    {
        if (!FogOfWarEnabled) return;
        visualContacts.Clear();
        (previousSideContacts, sideContacts) = (sideContacts, previousSideContacts);
        sideContacts.Clear();
        var ships = shipLogsOnMapOrDestroyed.Where(s => s.shipClass != null).Select(s => new
        {
            ship = s, side = ((IShipGroupMember)s).GetRootParent().objectId,
            size = s.shipClass.targetSizeModifier, sun = scenarioState.GetSunPosition(s.position),
            civilian = !s.shipClass.IsCombatShip() && !s.IsLandBattery(), burning = NavalUtils.IsAfire(s)
        }).ToArray();
        foreach (var observer in ships)
        {
            if (!observer.ship.IsOnMap()) continue;
            var light = observer.sun.GetDayNightLevel();
            foreach (var target in ships)
            {
                if (observer.side == target.side) continue;
                var silhouette = target.burning;
                if (!silhouette && light == DayNightLevel.Twilight)
                {
                    var bearing = MeasureStats.Approximation.CalculateInitialBearing(observer.ship.position.LatDeg,
                        observer.ship.position.LonDeg, target.ship.position.LatDeg, target.ship.position.LonDeg);
                    silhouette = NavalUtils.GetDawnDuskFireControlOffset(target.sun, bearing) > 0;
                }
                var yards = RuleChart.GetVisibilityRangeYards(observer.size, target.size, scenarioState.visibility,
                    light, !scenarioState.hasMoonlight, silhouette, searchVesselIsNotAWarship: observer.civilian,
                    searchVesselSpeedKnots: observer.ship.speedKnots, targetSpeedKnots: target.ship.speedKnots);
                var a = observer.ship.position;
                var b = target.ship.position;
                if (Math.Abs(a.LonDeg - b.LonDeg) > 180)
                {
                    if (MeasureStats.Approximation.HaversineDistanceKm(a.LatDeg, a.LonDeg, b.LatDeg, b.LonDeg)
                        * 1000 * MeasureUtils.meterToYard > yards) continue;
                }
                else if (MeasureUtils.ApproximateDistanceSquaredYards(a, b) > yards * yards) continue;
                visualContacts.Add((observer.ship, target.ship));
                sideContacts.Add((observer.side, target.ship));
                if (scenarioState.surpriseAttack && target.ship.IsOnMap()) sightedDuringMinute.Add(observer.ship);
            }
        }
        if (!sideContacts.SetEquals(previousSideContacts))
        {
            hostileProximitySnapshot = null;
            foreach (var key in previousSideContacts)
                if (!sideContacts.Contains(key)) contactSince.Remove(key);
            foreach (var key in sideContacts)
                if (!previousSideContacts.Contains(key)) contactSince[key] = scenarioState.dateTime;
        }
        foreach (var observer in ships)
        {
            var ship = observer.ship;
            if (!ship.IsOnMap()) continue;
            bool Lost(string id) => !string.IsNullOrEmpty(id) && !CanSee(ship, EntityManager.Instance.GetOnMapShipLog(id));
            if (Lost(ship.shipLevelFiringTargetObjectId)) ship.shipLevelFiringTargetObjectId = null;
            foreach (var battery in ship.batteryStatus)
            {
                foreach (var mount in battery.mountStatus)
                    if (Lost(mount.firingTargetObjectId)) mount.SetFiringTarget(null);
                foreach (var director in battery.fireControlSystemStatusRecords)
                    if (Lost(director.targetObjectId)) director.SetTrackingTarget(null);
            }
            foreach (var battery in ship.rapidFiringStatus)
                battery.targettingRecords.RemoveAll(record => Lost(record.targetObjectId));
            foreach (var mount in ship.torpedoSectorStatus.mountStatus)
                if (Lost(mount.firingTargetObjectId)) mount.SetFiringTarget(null);
            if (ship.automaticOperationalRouteTargetObjectId != null &&
                !SideHasContact(observer.side, EntityManager.Instance.GetOnMapShipLog(ship.automaticOperationalRouteTargetObjectId)))
            {
                ship.ClearManualRoute();
                ship.ClearAutomaticOperationalRouteState();
            }
        }
    }
}
