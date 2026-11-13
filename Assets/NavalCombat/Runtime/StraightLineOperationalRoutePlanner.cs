using NavalCombatCore;

namespace NavalCombat.Avalonia.Services;

/// <summary>
/// Builds a direct operational route for the unrestricted-ocean Avalonia host.
/// Shared route guidance resolves the geodesic course to the returned waypoint,
/// so no planar approximation or artificial intermediate points are required.
/// </summary>
public sealed class StraightLineOperationalRoutePlanner : IOperationalRoutePlanner
{
    public bool TryBuildOperationalRoute(
        ShipLog shipLog,
        LatLon targetPosition,
        out List<LatLon> routePoints)
    {
        routePoints = [];
        if (shipLog?.position is null || targetPosition is null)
            return false;

        routePoints.Add(targetPosition.Clone());
        return true;
    }
}
