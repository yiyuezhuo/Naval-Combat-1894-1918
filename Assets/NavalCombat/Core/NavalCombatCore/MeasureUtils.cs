using System;
using GeographicLib;


namespace NavalCombatCore
{
    public class MeasureStats
    {
        public double distanceYards;
        public double observerToTargetTrueBearingRelativeToNorthDeg;
        public double targetToObserverTrueBearingRelativeToNorthDeg;
        public double observerToTargetBearingRelativeToBowDeg;
        public double targetToObserverBearingRelativeToBowDeg;
        public TargetAspect observerPresentAspectFromTarget => GetXPresentAspectFromY(observerToTargetTrueBearingRelativeToNorthDeg);
        public TargetAspect targetPresentAspectFromObserver => GetXPresentAspectFromY(targetToObserverBearingRelativeToBowDeg);

        public static MeasureStats Measure(IDF3Model observer, IDF3Model target)
        {
            var inverseLine = Geodesic.WGS84.InverseLine(
                observer.GetLatitudeDeg(), observer.GetLongitudeDeg(),
                target.GetLatitudeDeg(), target.GetLongitudeDeg()
            );
            var distM = (double)inverseLine.Distance;
            var distYards = distM * MeasureUtils.meterToYard;
            var observerToTargetTrueBearingRelativeToNorthDeg = MeasureUtils.NormalizeAngle((double)inverseLine.Azimuth);
            var targetToObserverTrueBearingRelativeToNorthDeg = MeasureUtils.NormalizeAngle(observerToTargetTrueBearingRelativeToNorthDeg + 180); // Though not very accurete if distance is extremely long.
            var observerToTargetBearingRelativeToBowDeg = MeasureUtils.NormalizeAngle(observerToTargetTrueBearingRelativeToNorthDeg - observer.GetHeadingDeg());
            var targetToObserverBearingRelativeToBowDeg = MeasureUtils.NormalizeAngle(targetToObserverTrueBearingRelativeToNorthDeg - target.GetHeadingDeg());

            return new()
            {
                distanceYards = distYards,
                observerToTargetTrueBearingRelativeToNorthDeg = observerToTargetTrueBearingRelativeToNorthDeg,
                targetToObserverTrueBearingRelativeToNorthDeg = targetToObserverTrueBearingRelativeToNorthDeg,
                observerToTargetBearingRelativeToBowDeg = observerToTargetBearingRelativeToBowDeg,
                targetToObserverBearingRelativeToBowDeg = targetToObserverBearingRelativeToBowDeg,
            };
        }

        public static MeasureStats MeasureApproximation(IDF3Model observer, IDF3Model target)
        {
            var (DistanceKm, InitialBearingDeg) = Approximation.CalculateDistanceKmAndBearingDeg(observer, target);
            var distYards = (double)DistanceKm * 1000 * MeasureUtils.meterToYard;
            var observerToTargetTrueBearingRelativeToNorthDeg = MeasureUtils.NormalizeAngle((double)InitialBearingDeg);
            var targetToObserverTrueBearingRelativeToNorthDeg = MeasureUtils.NormalizeAngle(observerToTargetTrueBearingRelativeToNorthDeg + 180); // Though not very accurete if distance is extremely long.
            var observerToTargetBearingRelativeToBowDeg = MeasureUtils.NormalizeAngle(observerToTargetTrueBearingRelativeToNorthDeg - observer.GetHeadingDeg());
            var targetToObserverBearingRelativeToBowDeg = MeasureUtils.NormalizeAngle(targetToObserverTrueBearingRelativeToNorthDeg - target.GetHeadingDeg());
            return new()
            {
                distanceYards = distYards,
                observerToTargetTrueBearingRelativeToNorthDeg = observerToTargetTrueBearingRelativeToNorthDeg,
                targetToObserverTrueBearingRelativeToNorthDeg = targetToObserverTrueBearingRelativeToNorthDeg,
                observerToTargetBearingRelativeToBowDeg = observerToTargetBearingRelativeToBowDeg,
                targetToObserverBearingRelativeToBowDeg = targetToObserverBearingRelativeToBowDeg,
            };
        }

        static TargetAspect GetXPresentAspectFromY(double XToYrelativeBearingToBowDeg)
        {
            return Math.Min(
                MeasureUtils.GetPositiveAngleDifference(XToYrelativeBearingToBowDeg, 90),
                MeasureUtils.GetPositiveAngleDifference(XToYrelativeBearingToBowDeg, 270)
            ) <= 60 ? TargetAspect.Broad : TargetAspect.Narrow;
        }

        public static class Approximation
        {
            private const double EarthRadius = 6371000; // m

            public static (double newLat, double newLon) CalculateNewPosition(double lat, double lon, double bearing, double distanceM)
            {
                double latRad = DegreesToRadians(lat);
                double lonRad = DegreesToRadians(lon);
                double bearingRad = DegreesToRadians(bearing);

                double newLatRad = Math.Asin(Math.Sin(latRad) * Math.Cos(distanceM / EarthRadius) +
                                    Math.Cos(latRad) * Math.Sin(distanceM / EarthRadius) * Math.Cos(bearingRad));

                double newLonRad = lonRad + Math.Atan2(Math.Sin(bearingRad) * Math.Sin(distanceM / EarthRadius) * Math.Cos(latRad),
                                                    Math.Cos(distanceM / EarthRadius) - Math.Sin(latRad) * Math.Sin(newLatRad));

                double newLat = RadiansToDegrees(newLatRad);
                double newLon = RadiansToDegrees(newLonRad);

                return (newLat, newLon);
            }

            private static double DegreesToRadians(double degrees)
            {
                return degrees * Math.PI / 180.0;
            }

            private static double RadiansToDegrees(double radians)
            {
                return radians * 180.0 / Math.PI;
            }

            public static string Test()
            {
                // Lat 40.7128，Lon-74.0060，45 deg（east-west），move 1000m
                var result = CalculateNewPosition(40.7128, -74.0060, 45, 1000);
                return $"New Latitude: {result.newLat}, New Longitude: {result.newLon}";
            }

            public static double CalculateInitialBearing(double lat1, double lon1, double lat2, double lon2)
            {
                // Convert latitude and longitude from degrees to radians
                double lat1Rad = DegreesToRadians(lat1);
                double lon1Rad = DegreesToRadians(lon1);
                double lat2Rad = DegreesToRadians(lat2);
                double lon2Rad = DegreesToRadians(lon2);

                // Calculate the difference in longitude
                double deltaLon = lon2Rad - lon1Rad;

                // Use the spherical trigonometry formula to calculate the initial bearing
                double y = Math.Sin(deltaLon) * Math.Cos(lat2Rad);
                double x = Math.Cos(lat1Rad) * Math.Sin(lat2Rad) - Math.Sin(lat1Rad) * Math.Cos(lat2Rad) * Math.Cos(deltaLon);
                double initialBearingRad = Math.Atan2(y, x);

                // Convert the bearing from radians to degrees
                double initialBearingDeg = RadiansToDegrees(initialBearingRad);

                // Normalize the bearing to the range [0, 360)
                initialBearingDeg = (initialBearingDeg + 360) % 360;

                return initialBearingDeg;
            }

            public static double CalculateInitialBearing(IDF3Model observer, IDF3Model target)
            {
                return CalculateInitialBearing(observer.GetLatitudeDeg(), observer.GetLongitudeDeg(), target.GetLatitudeDeg(), target.GetLongitudeDeg());
            }

            public static string Test2()
            {
                // Latitude and longitude of the starting point (in degrees)
                double lat1 = 34.0522; // Example: Latitude of Los Angeles
                double lon1 = -118.2437; // Example: Longitude of Los Angeles

                // Latitude and longitude of the destination (in degrees)
                double lat2 = 40.7128; // Example: Latitude of New York
                double lon2 = -74.0060; // Example: Longitude of New York

                // Calculate the initial bearing
                double initialBearing = CalculateInitialBearing(lat1, lon1, lat2, lon2);

                return $"Initial Bearing: {initialBearing} degrees";
            }

            public static double HaversineDistanceKm(double lat1, double lon1, double lat2, double lon2)
            {
                const double R = 6371;

                double dLat = DegreesToRadians(lat2 - lat1);
                double dLon = DegreesToRadians(lon2 - lon1);

                double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                        Math.Cos(DegreesToRadians(lat1)) * Math.Cos(DegreesToRadians(lat2)) *
                        Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

                double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

                double distance = R * c;

                return distance;
            }

            public static double HaversineDistanceYards(IDF3Model observer, IDF3Model target)
            {
                var distanceKm = HaversineDistanceKm(observer.GetLatitudeDeg(), observer.GetLongitudeDeg(), target.GetLatitudeDeg(), target.GetLongitudeDeg());
                return distanceKm * 1000 * MeasureUtils.meterToYard;
            }

            public static double HaversineDistanceYards(LatLon latLon1, LatLon latLon2)
            {
                var distanceKm = HaversineDistanceKm(latLon1.LatDeg, latLon1.LonDeg, latLon2.LatDeg, latLon2.LonDeg);
                return distanceKm * 1000 * MeasureUtils.meterToYard;
            }

            public static (double DistanceKm, double InitialBearingDeg) CalculateDistanceKmAndBearingDeg(double lat1, double lon1, double lat2, double lon2)
            {
                const double R = 6371; // Earth's radius in km

                // Convert latitude and longitude from degrees to radians
                double lat1Rad = DegreesToRadians(lat1);
                double lon1Rad = DegreesToRadians(lon1);
                double lat2Rad = DegreesToRadians(lat2);
                double lon2Rad = DegreesToRadians(lon2);

                // Differences in coordinates
                double dLat = lat2Rad - lat1Rad;
                double dLon = lon2Rad - lon1Rad;

                // Haversine distance calculation
                double sinDLat2 = Math.Sin(dLat / 2);
                double sinDLon2 = Math.Sin(dLon / 2);
                double a = sinDLat2 * sinDLat2 +
                        Math.Cos(lat1Rad) * Math.Cos(lat2Rad) *
                        sinDLon2 * sinDLon2;
                double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
                double distance = R * c;

                // Initial bearing calculation
                double y = Math.Sin(dLon) * Math.Cos(lat2Rad);
                double x = Math.Cos(lat1Rad) * Math.Sin(lat2Rad) -
                        Math.Sin(lat1Rad) * Math.Cos(lat2Rad) * Math.Cos(dLon);
                double initialBearingRad = Math.Atan2(y, x);
                double initialBearingDeg = RadiansToDegrees(initialBearingRad);
                initialBearingDeg = (initialBearingDeg + 360) % 360; // Normalize to [0, 360)

                return (distance, initialBearingDeg);
            }

            public static (double DistanceKm, double InitialBearingDeg) CalculateDistanceKmAndBearingDeg(IDF3Model observer, IDF3Model target)
            {
                return CalculateDistanceKmAndBearingDeg(observer.GetLatitudeDeg(), observer.GetLongitudeDeg(), target.GetLatitudeDeg(), target.GetLongitudeDeg());
            }
        }
    }

    public static class MeasureUtils
    {
        public const double YardsPerDegree = 2025.37183d * 60d;

        public static double yardToMeter = 0.9144d;
        public static double meterToYard = 1.09361d;
        public static double navalMileToMeter = 1852d;
        public static double yardToFoot = 3;
        public static double footToYard = 1d / 3;
        public static double meterToFoot = meterToYard * yardToFoot;
        public static double navalMileToKilometer = 1.852d;
        public static double kilometerToNavalMile = 1d / navalMileToKilometer;
        public static double kilometerToYard = meterToYard * 1000;
        public static double navalMileToYard = 2025.37183d;

        public readonly struct LocalProjection
        {
            public readonly double centerLatDeg;
            public readonly double centerLonDeg;
            public readonly double lonScaleYards;

            public LocalProjection(double centerLatDeg, double centerLonDeg)
            {
                this.centerLatDeg = centerLatDeg;
                this.centerLonDeg = centerLonDeg;
                lonScaleYards = Math.Max(0.01d, Math.Abs((double)Math.Cos(centerLatDeg * Math.PI / 180d))) * YardsPerDegree;
            }

            public double LatitudeToY(double latitudeDeg)
            {
                return (latitudeDeg - centerLatDeg) * YardsPerDegree;
            }

            public double LongitudeToX(double longitudeDeg)
            {
                return (longitudeDeg - centerLonDeg) * lonScaleYards;
            }
        }

        public static double ApproximateDistanceSquaredYards(LatLon a, LatLon b)
        {
            var averageLatRad = (a.LatDeg + b.LatDeg) * 0.5d * (double)Math.PI / 180d;
            var dx = (b.LonDeg - a.LonDeg) * (double)Math.Cos(averageLatRad) * YardsPerDegree;
            var dy = (b.LatDeg - a.LatDeg) * YardsPerDegree;
            return dx * dx + dy * dy;
        }

        public static double ApproximateDistanceYards(LatLon a, LatLon b)
        {
            return (double)Math.Sqrt(ApproximateDistanceSquaredYards(a, b));
        }

        public static double MoveAngleTowards(double current, double target, double step)
        {
            // Normalize angles to 0-360 range
            current = NormalizeAngle(current);
            target = NormalizeAngle(target);

            // Calculate both possible directional differences (clockwise and counter-clockwise)
            double diff = target - current;
            double absDiff = Math.Abs(diff);

            // If direct difference is greater than 180°, choose the other direction
            if (absDiff > 180d)
            {
                if (diff > 0)
                {
                    diff -= 360d; // Take the shorter path clockwise
                }
                else
                {
                    diff += 360d; // Take the shorter path counter-clockwise
                }
            }

            // Calculate actual movement (not exceeding step size)
            double move = Math.Sign(diff) * Math.Min(Math.Abs(diff), step);

            // Apply movement and normalize result
            double result = current + move;
            return NormalizeAngle(result);
        }

        // Helper function: Normalize angle to 0-360 range
        public static double NormalizeAngle(double angle)
        {
            angle %= 360d; // Reduce to base value
            if (angle < 0)
            {
                angle += 360d; // Convert negative angles to positive equivalents
            }
            return angle;
        }

        public static double GetPositiveAngleDifference(double angle1, double angle2)
        {
            // Normalize angles to 0-360 range
            angle1 = NormalizeAngle(angle1);
            angle2 = NormalizeAngle(angle2);

            // Calculate both possible directional differences
            double diff = Math.Abs(angle1 - angle2);

            // If difference is greater than 180°, take the shorter path around the circle
            if (diff > 180d)
            {
                diff = 360d - diff;
            }

            // Return the smallest positive difference
            return diff;
        }

        /// <summary>
        /// Checks if an angle is within a given arc range
        /// </summary>
        /// <param name="angle">The angle to check (in degrees)</param>
        /// <param name="startAngle">The starting angle of the arc (in degrees)</param>
        /// <param name="sweepAngle">The sweep angle of the arc (in degrees, positive for clockwise direction)</param>
        /// <returns>True if the angle is within the arc range, false otherwise</returns>
        public static bool IsAngleInArc(double angle, double startAngle, double sweepAngle)
        {
            // Normalize all angles to 0-360 range
            angle = NormalizeAngle(angle);
            startAngle = NormalizeAngle(startAngle);

            // Calculate the end angle of the arc
            double endAngle = NormalizeAngle(startAngle + sweepAngle);

            // Handle arcs that don't cross 0°
            if (sweepAngle > 0 && startAngle < endAngle)
            {
                return angle >= startAngle && angle <= endAngle;
            }
            else if (sweepAngle < 0 && startAngle > endAngle)
            {
                return angle <= startAngle && angle >= endAngle;
            }
            // Handle arcs that cross 0°
            else
            {
                if (sweepAngle > 0) // Clockwise arc crossing 0°
                {
                    return angle >= startAngle || angle <= endAngle;
                }
                else // Counter-clockwise arc crossing 0°
                {
                    return angle <= startAngle || angle >= endAngle;
                }
            }
        }

        public static bool IsAngleInArcRelaxed(double angle, double startAngle, double sweepAngle, double relaxAngle)
        {
            return IsAngleInArc(angle, startAngle - relaxAngle, sweepAngle + 2 * relaxAngle);
        }

        public static double AngleDifferenceFromArc(double angle, double startAngle, double sweepAngle)
        {
            // Normalize all angles to 0-360 range
            angle = NormalizeAngle(angle);
            startAngle = NormalizeAngle(startAngle);
            
            // If the angle is inside the arc, return 0 difference
            if (IsAngleInArc(angle, startAngle, sweepAngle))
            {
                return 0d;
            }
            
            // Calculate the end angle of the arc
            double endAngle = NormalizeAngle(startAngle + sweepAngle);
            
            // Calculate the difference to both start and end angles
            double diffToStart = Math.Min(Math.Abs(angle - startAngle), 360 - Math.Abs(angle - startAngle));
            double diffToEnd = Math.Min(Math.Abs(angle - endAngle), 360 - Math.Abs(angle - endAngle));
            
            // Return the smaller difference
            return Math.Min(diffToStart, diffToEnd);
        }
    }

    public class InterceptionPointSolver
    {
        const double epsilon = 0.0001d;

        public class Result
        {
            public bool success;
            public double azimuth;
            public double arrivalSeconds;
            public double distanceYards;
        }

        public static Result Calcualte(IDF4Model shooter, IDF4Model target, double speedKnots)
        {
            if (speedKnots <= epsilon)
                return new();

            var projection = new MeasureUtils.LocalProjection(shooter.GetLatitudeDeg(), shooter.GetLongitudeDeg());
            var relativeTargetPosition = (
                x: projection.LongitudeToX(target.GetLongitudeDeg()),
                y: projection.LatitudeToY(target.GetLatitudeDeg())
            );

            var targetSpeedYardsPerSecond = target.GetSpeedKnots() * MeasureUtils.navalMileToYard / 3600d;
            var targetHeadingRad = target.GetHeadingDeg() * Math.PI / 180.0;
            var targetVelocity = (
                x: (double)(Math.Sin(targetHeadingRad) * targetSpeedYardsPerSecond),
                y: (double)(Math.Cos(targetHeadingRad) * targetSpeedYardsPerSecond)
            );

            var projectileSpeedYardsPerSecond = speedKnots * MeasureUtils.navalMileToYard / 3600d;
            var arrivalTimeSeconds = SolveArrivalSeconds(relativeTargetPosition, targetVelocity, projectileSpeedYardsPerSecond);
            if (arrivalTimeSeconds <= epsilon || double.IsNaN(arrivalTimeSeconds) || double.IsInfinity(arrivalTimeSeconds))
                return new();

            var interceptPosition = (
                x: relativeTargetPosition.x + targetVelocity.x * arrivalTimeSeconds,
                y: relativeTargetPosition.y + targetVelocity.y * arrivalTimeSeconds
            );
            var interceptDistanceYards = (double)Math.Sqrt(interceptPosition.x * interceptPosition.x + interceptPosition.y * interceptPosition.y);
            if (interceptDistanceYards <= epsilon)
                return new();

            var azimuthDeg = MeasureUtils.NormalizeAngle((double)(Math.Atan2(interceptPosition.x, interceptPosition.y) * 180.0 / Math.PI));
            return new()
            {
                success = true,
                azimuth = azimuthDeg,
                arrivalSeconds = arrivalTimeSeconds,
                distanceYards = interceptDistanceYards
            };
        }

        // Legacy fixed-point iteration solver kept for reference and comparison.
        public static Result CalcualteFixedPointLegacy(IDF4Model shooter, IDF4Model target, double speedKnots)
        {
            var shooterLat = shooter.GetLatitudeDeg();
            var shooterLon = shooter.GetLongitudeDeg();
            var targetLat = target.GetLatitudeDeg();
            var targetLon = target.GetLongitudeDeg();

            var prevArrivalTimeSeconds = 0d;

            for (int i = 0; i < 20; i++)
            {
                var (distanceKm, azi1) = MeasureStats.Approximation.CalculateDistanceKmAndBearingDeg(shooterLat, shooterLon, targetLat, targetLon);
                var distanceNm = distanceKm * MeasureUtils.kilometerToNavalMile;
                var arrivalTimeSeconds = distanceNm / speedKnots * 3600;

                var diffSeconds = arrivalTimeSeconds - prevArrivalTimeSeconds;
                if (Math.Abs(diffSeconds) < 0.1d)
                {
                    return new()
                    {
                        success = true,
                        azimuth = (double)azi1,
                        arrivalSeconds = (double)arrivalTimeSeconds,
                        distanceYards = (double)distanceKm * 1000 * MeasureUtils.meterToYard
                    };
                }

                prevArrivalTimeSeconds = (double)arrivalTimeSeconds;
                var movedM = diffSeconds / 3600 * target.GetSpeedKnots() * MeasureUtils.navalMileToMeter;
                var (_targetLat, _targetLon) = MeasureStats.Approximation.CalculateNewPosition(targetLat, targetLon, target.GetHeadingDeg(), movedM);
                targetLat = (double)_targetLat;
                targetLon = (double)_targetLon;
            }

            return new();
        }

        static double SolveArrivalSeconds((double x, double y) relativeTargetPosition, (double x, double y) targetVelocity, double projectileSpeedYardsPerSecond)
        {
            var px = relativeTargetPosition.x;
            var py = relativeTargetPosition.y;
            var vx = targetVelocity.x;
            var vy = targetVelocity.y;
            var speedSquared = projectileSpeedYardsPerSecond * projectileSpeedYardsPerSecond;

            var a = vx * vx + vy * vy - speedSquared;
            var b = 2d * (px * vx + py * vy);
            var c = px * px + py * py;

            if (Math.Abs(a) <= epsilon)
            {
                if (Math.Abs(b) <= epsilon)
                    return -1d;

                var linearRoot = -c / b;
                return linearRoot > epsilon ? linearRoot : -1d;
            }

            var discriminant = b * b - 4d * a * c;
            if (discriminant < 0d)
                return -1d;

            var sqrtDiscriminant = (double)Math.Sqrt(discriminant);
            var root1 = (-b - sqrtDiscriminant) / (2d * a);
            var root2 = (-b + sqrtDiscriminant) / (2d * a);

            var bestRoot = double.PositiveInfinity;
            if (root1 > epsilon)
                bestRoot = root1;
            if (root2 > epsilon && root2 < bestRoot)
                bestRoot = root2;

            return double.IsPositiveInfinity(bestRoot) ? -1d : bestRoot;
        }
    }

    public interface ICollider
    {
        public double GetLengthFoot();
        public double GetBeamFoot();
        public double GetHeadingDeg();
        public LatLon GetPosition();
    }

    public static class CollideUtils
    {
        public static bool IsCollided(LatLon newPosition, ICollider self, ICollider other)
        {
            var otherPos = other.GetPosition();

            // Exact Method
            // Geodesic.WGS84.Inverse(newPosition.LatDeg, newPosition.LonDeg, otherPos.LatDeg, otherPos.LonDeg, out var distanceM, out var azi1, out var azi2);
            // Approximation Method
            var (distanceKm, azi1) = MeasureStats.Approximation.CalculateDistanceKmAndBearingDeg(newPosition.LatDeg, newPosition.LonDeg, otherPos.LatDeg, otherPos.LonDeg);
            var distanceM = distanceKm * 1000;
            var azi2 = azi1;

            if (MeasureUtils.GetPositiveAngleDifference(self.GetHeadingDeg(), (double)azi1) > 90)
                return false;

            var distanceFoot = distanceM * MeasureUtils.meterToFoot;
            var lengthFoot = self.GetLengthFoot();
            var otherLengthFoot = other.GetLengthFoot();
            if (distanceFoot < lengthFoot / 2 + otherLengthFoot / 2)
            {
                var diff = MeasureUtils.GetPositiveAngleDifference(other.GetHeadingDeg(), (double)azi2);
                var coef = Math.Abs(diff - 90) / 90;
                var otherMix = otherLengthFoot * coef + other.GetBeamFoot() * (1 - coef); // not an exact oval
                if (distanceFoot < lengthFoot / 2 + otherMix / 2)
                {
                    return true;
                }
            }
            return false;
        }


    }
}
