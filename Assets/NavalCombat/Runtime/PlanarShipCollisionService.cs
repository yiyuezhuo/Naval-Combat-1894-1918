using CoreUtils;
using NavalCombatCore;

namespace NavalCombat.Avalonia.Services;

/// <summary>
/// Replaces Unity's collider-backed masking, collision, and torpedo fire-lane
/// queries with deterministic two-dimensional geometry.
/// </summary>
public sealed class PlanarShipCollisionService : IMaskCheckService
{
    const double EarthMeanRadiusMeters = 6_371_008.8d;
    const double FeetToMeters = 0.3048d;
    const double YardsToMeters = 0.9144d;
    const double DegreesToRadians = Math.PI / 180d;
    const double RadiansToDegrees = 180d / Math.PI;
    const double ParallelEpsilon = 1e-12d;
    const double TorpedoThreatLengthYards = 400d;
    const double NearTargetMaskDistanceYards = 2_000d;

    readonly Dictionary<ShipLog, Orientation> orientationCache = new();
    NavalGameState orientationCacheState;

    public MaskCheckResult Check(LatLon src, LatLon dst)
    {
        if (!TryBuildSegment(src, dst, out var segment))
            return new MaskCheckResult();

        EnsureOrientationCacheState();
        ShipLog nearestBlocker = null;
        var nearestDistanceMeters = double.PositiveInfinity;
        foreach (var blocker in NavalGameState.Instance.shipLogsOnMap)
        {
            if (!TryGetPhysicalHalfExtents(blocker, out var halfLengthMeters, out var halfBeamMeters)
                || !TryIntersectSegmentWithShip(
                    segment,
                    blocker,
                    halfLengthMeters,
                    halfBeamMeters,
                    out var hitDistanceMeters,
                    out _,
                    out _)
                // Unity raycasts do not report a collider containing the ray origin.
                || hitDistanceMeters <= ParallelEpsilon
                || hitDistanceMeters >= nearestDistanceMeters)
            {
                continue;
            }

            nearestBlocker = blocker;
            nearestDistanceMeters = hitDistanceMeters;
        }

        return nearestBlocker == null
            ? new MaskCheckResult()
            : new MaskCheckResult
            {
                isMasked = true,
                maskedObject = nearestBlocker,
                message = $"Masked by {nearestBlocker.objectId}"
            };
    }

    public MaskCheckResult Check(ShipLog observer, ShipLog target)
    {
        if (observer?.shipClass == null
            || target == null
            || !TryBuildSegment(observer.position, target.position, out var segment))
        {
            return new MaskCheckResult();
        }

        EnsureOrientationCacheState();
        ShipLog nearestBlocker = null;
        var nearestDistanceMeters = double.PositiveInfinity;
        var nearestBlockerSize = 0;
        var nearestBlockerTargetDistanceYards = 0d;
        var observerSize = observer.shipClass.targetSizeModifier;

        foreach (var blocker in NavalGameState.Instance.shipLogsOnMap)
        {
            if (ReferenceEquals(blocker, observer)
                || ReferenceEquals(blocker, target)
                || !TryGetPhysicalHalfExtents(blocker, out var halfLengthMeters, out var halfBeamMeters)
                || !TryIntersectSegmentWithShip(
                    segment,
                    blocker,
                    halfLengthMeters,
                    halfBeamMeters,
                    out var hitDistanceMeters,
                    out _,
                    out _)
                || hitDistanceMeters >= nearestDistanceMeters)
            {
                continue;
            }

            var blockerSize = blocker.shipClass.targetSizeModifier;
            var blockerTargetDistanceYards =
                MeasureStats.Approximation.HaversineDistanceYards(blocker, target);
            if (blockerSize < observerSize
                && blockerTargetDistanceYards >= NearTargetMaskDistanceYards)
            {
                continue;
            }

            nearestBlocker = blocker;
            nearestDistanceMeters = hitDistanceMeters;
            nearestBlockerSize = blockerSize;
            nearestBlockerTargetDistanceYards = blockerTargetDistanceYards;
        }

        return nearestBlocker == null
            ? new MaskCheckResult()
            : new MaskCheckResult
            {
                isMasked = true,
                maskedObject = nearestBlocker,
                message = $"{observer.objectId} (size={observerSize}) is masked by "
                    + $"{nearestBlocker.objectId} (size={nearestBlockerSize}, "
                    + $"to tgt dist={nearestBlockerTargetDistanceYards})"
            };
    }

    public bool IsSafeToFireTorpedoAt(ShipLog shooter, ShipLog target)
    {
        if (shooter == null
            || target == null
            || !TryBuildSegment(shooter.position, target.position, out var segment))
        {
            return true;
        }

        EnsureOrientationCacheState();
        var shooterRoot = ((IShipGroupMember)shooter).GetRootParent();
        foreach (var potentialFriendly in NavalGameState.Instance.shipLogsOnMap)
        {
            if (ReferenceEquals(potentialFriendly, shooter)
                || potentialFriendly?.shipClass == null
                || ((IShipGroupMember)potentialFriendly).GetRootParent() != shooterRoot)
            {
                continue;
            }

            // Preserve Unity's deliberately generous torpedo-threat collider:
            // 400 yards long, with the ship beam's numeric foot value treated as yards.
            var halfLengthMeters = TorpedoThreatLengthYards * YardsToMeters / 2d;
            var halfBeamMeters = potentialFriendly.GetBeamFoot() * YardsToMeters / 2d;
            if (halfBeamMeters <= 0d)
                continue;

            if (TryIntersectSegmentWithShip(
                segment,
                potentialFriendly,
                halfLengthMeters,
                halfBeamMeters,
                out _,
                out _,
                out _))
            {
                return false;
            }
        }

        return true;
    }

    public CollideCheckResult CollideCheck(IObjectIdLabeled observer, double testDistanceYards)
    {
        if (observer is not ICollider movingCollider
            || !double.IsFinite(testDistanceYards)
            || testDistanceYards <= 0d)
        {
            return null;
        }

        var origin = movingCollider.GetPosition();
        if (origin == null
            || !double.IsFinite(origin.LatDeg)
            || !double.IsFinite(origin.LonDeg))
        {
            return null;
        }

        var maxDistanceMeters = testDistanceYards * YardsToMeters;
        var observerHeadingRadians = movingCollider.GetHeadingDeg() * DegreesToRadians;
        var segment = new Segment(
            origin,
            Math.Sin(observerHeadingRadians),
            Math.Cos(observerHeadingRadians),
            maxDistanceMeters,
            EarthMeanRadiusMeters * Math.Cos(origin.LatDeg * DegreesToRadians));

        EnsureOrientationCacheState();
        var gameState = NavalGameState.Instance;

        ShipLog nearestShip = null;
        double nearestDistanceMeters = double.PositiveInfinity;
        double nearestLocalForwardMeters = 0d;
        double nearestLocalRightMeters = 0d;
        double nearestHalfLengthMeters = 0d;

        foreach (var target in gameState.shipLogsOnMap)
        {
            if (ReferenceEquals(target, observer)
                || !TryGetPhysicalHalfExtents(target, out var halfLengthMeters, out var halfBeamMeters)
                || !TryIntersectSegmentWithShip(
                    segment,
                    target,
                    halfLengthMeters,
                    halfBeamMeters,
                    out var hitDistanceMeters,
                    out var hitLocalForwardMeters,
                    out var hitLocalRightMeters)
                || hitDistanceMeters >= nearestDistanceMeters)
            {
                continue;
            }

            nearestShip = target;
            nearestDistanceMeters = hitDistanceMeters;
            nearestLocalForwardMeters = hitLocalForwardMeters;
            nearestLocalRightMeters = hitLocalRightMeters;
            nearestHalfLengthMeters = halfLengthMeters;
        }

        if (nearestShip == null)
            return null;

        return new CollideCheckResult
        {
            collided = nearestShip,
            collideLocation = Math.Abs(nearestLocalForwardMeters) > nearestHalfLengthMeters * 0.7d
                ? ArmorLocation.BeltEnd
                : ArmorLocation.MainBelt,
            // Matches the local-point calculation in Unity's LOSLine.CollideCheck.
            impactAngleDeg = Math.Abs(Math.Atan2(
                nearestLocalForwardMeters,
                nearestLocalRightMeters)) * RadiansToDegrees
        };
    }

    void EnsureOrientationCacheState()
    {
        var gameState = NavalGameState.Instance;
        if (ReferenceEquals(gameState, orientationCacheState))
            return;

        orientationCache.Clear();
        orientationCacheState = gameState;
    }

    bool TryIntersectSegmentWithShip(
        Segment segment,
        ShipLog ship,
        double halfLengthMeters,
        double halfBeamMeters,
        out double hitDistanceMeters,
        out double hitLocalForwardMeters,
        out double hitLocalRightMeters)
    {
        hitDistanceMeters = 0d;
        hitLocalForwardMeters = 0d;
        hitLocalRightMeters = 0d;
        if (!IsValidShipGeometry(ship) || halfLengthMeters <= 0d || halfBeamMeters <= 0d)
            return false;

        var deltaLatitudeRadians =
            (ship.position.LatDeg - segment.Origin.LatDeg) * DegreesToRadians;
        var deltaLongitudeRadians = NormalizeLongitudeRadians(
            (ship.position.LonDeg - segment.Origin.LonDeg) * DegreesToRadians);
        var targetNorthMeters = deltaLatitudeRadians * EarthMeanRadiusMeters;
        var targetEastMeters = deltaLongitudeRadians * segment.MetersPerLongitudeRadian;

        var targetRadiusMeters = Math.Sqrt(
            halfLengthMeters * halfLengthMeters + halfBeamMeters * halfBeamMeters);
        var broadPhaseDistanceMeters = segment.LengthMeters + targetRadiusMeters;
        if (targetEastMeters * targetEastMeters + targetNorthMeters * targetNorthMeters
            > broadPhaseDistanceMeters * broadPhaseDistanceMeters)
        {
            return false;
        }

        var orientation = GetOrientation(ship);
        var originRelativeEast = -targetEastMeters;
        var originRelativeNorth = -targetNorthMeters;
        var originLocalForward = originRelativeEast * orientation.ForwardEast
            + originRelativeNorth * orientation.ForwardNorth;
        var originLocalRight = originRelativeEast * orientation.RightEast
            + originRelativeNorth * orientation.RightNorth;
        var rayLocalForward = segment.DirectionEast * orientation.ForwardEast
            + segment.DirectionNorth * orientation.ForwardNorth;
        var rayLocalRight = segment.DirectionEast * orientation.RightEast
            + segment.DirectionNorth * orientation.RightNorth;

        return TryIntersectRayWithBox(
            originLocalForward,
            originLocalRight,
            rayLocalForward,
            rayLocalRight,
            halfLengthMeters,
            halfBeamMeters,
            segment.LengthMeters,
            out hitDistanceMeters,
            out hitLocalForwardMeters,
            out hitLocalRightMeters);
    }

    static bool TryGetPhysicalHalfExtents(
        ShipLog ship,
        out double halfLengthMeters,
        out double halfBeamMeters)
    {
        halfLengthMeters = 0d;
        halfBeamMeters = 0d;
        if (!IsValidShipGeometry(ship))
            return false;

        halfLengthMeters = ship.GetLengthFoot() * FeetToMeters / 2d;
        halfBeamMeters = ship.GetBeamFoot() * FeetToMeters / 2d;
        return halfLengthMeters > 0d && halfBeamMeters > 0d;
    }

    static bool IsValidShipGeometry(ShipLog ship) => ship?.shipClass != null
        && ship.position != null
        && double.IsFinite(ship.position.LatDeg)
        && double.IsFinite(ship.position.LonDeg)
        && double.IsFinite(ship.headingDeg);

    static bool TryBuildSegment(LatLon src, LatLon dst, out Segment segment)
    {
        segment = default;
        if (src == null
            || dst == null
            || !double.IsFinite(src.LatDeg)
            || !double.IsFinite(src.LonDeg)
            || !double.IsFinite(dst.LatDeg)
            || !double.IsFinite(dst.LonDeg))
        {
            return false;
        }

        var metersPerLongitudeRadian =
            EarthMeanRadiusMeters * Math.Cos(src.LatDeg * DegreesToRadians);
        var northMeters = (dst.LatDeg - src.LatDeg) * DegreesToRadians * EarthMeanRadiusMeters;
        var eastMeters = NormalizeLongitudeRadians(
            (dst.LonDeg - src.LonDeg) * DegreesToRadians) * metersPerLongitudeRadian;
        var lengthMeters = Math.Sqrt(eastMeters * eastMeters + northMeters * northMeters);
        if (!double.IsFinite(lengthMeters) || lengthMeters <= ParallelEpsilon)
            return false;

        segment = new Segment(
            src,
            eastMeters / lengthMeters,
            northMeters / lengthMeters,
            lengthMeters,
            metersPerLongitudeRadian);
        return true;
    }

    Orientation GetOrientation(ShipLog ship)
    {
        if (orientationCache.TryGetValue(ship, out var cached)
            && cached.HeadingDeg == ship.headingDeg)
        {
            return cached;
        }

        var headingRadians = ship.headingDeg * DegreesToRadians;
        var orientation = new Orientation(
            ship.headingDeg,
            Math.Sin(headingRadians),
            Math.Cos(headingRadians),
            Math.Cos(headingRadians),
            -Math.Sin(headingRadians));
        orientationCache[ship] = orientation;
        return orientation;
    }

    static bool TryIntersectRayWithBox(
        double originForward,
        double originRight,
        double directionForward,
        double directionRight,
        double halfLength,
        double halfBeam,
        double maxDistance,
        out double hitDistance,
        out double hitForward,
        out double hitRight)
    {
        var enter = 0d;
        var exit = maxDistance;
        if (!ClipSlab(originForward, directionForward, halfLength, ref enter, ref exit)
            || !ClipSlab(originRight, directionRight, halfBeam, ref enter, ref exit)
            || enter > maxDistance)
        {
            hitDistance = 0d;
            hitForward = 0d;
            hitRight = 0d;
            return false;
        }

        hitDistance = enter;
        hitForward = originForward + directionForward * enter;
        hitRight = originRight + directionRight * enter;
        return true;
    }

    static bool ClipSlab(
        double origin,
        double direction,
        double halfExtent,
        ref double enter,
        ref double exit)
    {
        if (Math.Abs(direction) <= ParallelEpsilon)
            return origin >= -halfExtent && origin <= halfExtent;

        var first = (-halfExtent - origin) / direction;
        var second = (halfExtent - origin) / direction;
        if (first > second)
            (first, second) = (second, first);

        enter = Math.Max(enter, first);
        exit = Math.Min(exit, second);
        return enter <= exit && exit >= 0d;
    }

    static double NormalizeLongitudeRadians(double longitudeRadians)
    {
        var normalized = (longitudeRadians + Math.PI) % (2d * Math.PI);
        if (normalized < 0d)
            normalized += 2d * Math.PI;
        return normalized - Math.PI;
    }

    readonly record struct Orientation(
        double HeadingDeg,
        double ForwardEast,
        double ForwardNorth,
        double RightEast,
        double RightNorth);

    readonly record struct Segment(
        LatLon Origin,
        double DirectionEast,
        double DirectionNorth,
        double LengthMeters,
        double MetersPerLongitudeRadian);
}
