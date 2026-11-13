using System;
using System.Collections.Generic;
using CoreUtils;
using GeographicLib;
using Vector2 = NavalCombatCore.DoubleVector2;

namespace NavalCombatCore
{
    public enum ObstacleAvoidanceMode
    {
        None,
        Weak,
        Strong
    }

    public sealed class ObstacleAvoidanceParameters
    {
        public double roiPreviewSeconds;
        public double roiHardClearancePixels;
        public double roiInfluenceDistancePixels;
        public double roiEarlyExitDistancePixels;

        public static readonly ObstacleAvoidanceParameters Strong = new()
        {
            roiPreviewSeconds = 75d,
            roiHardClearancePixels = 1.5d,
            roiInfluenceDistancePixels = 4d,
            roiEarlyExitDistancePixels = 15d
        };

        public static readonly ObstacleAvoidanceParameters Weak = new()
        {
            roiPreviewSeconds = 37.5d,
            roiHardClearancePixels = 0.75d,
            roiInfluenceDistancePixels = 2d,
            roiEarlyExitDistancePixels = 7.5d
        };
    }

    public class ObstacleAvoidChecker
    {
        const double ROIShoreGradientEpsilon = 1e-4d;
        const double LegacyStepDeg = 10d;
        const double LegacySimpleStepDeg = 20d;
        const double LegacyBoundDeg = 10d;
        static readonly double[] LegacyExtrapolateRange = { 30d, 60d, 120d, 240d };
        static readonly double[] LegacySimpleExtrapolateRange = { 60d };

        // Argments
        // public ShipLog shipLog;
        public LatLon latLon;
        public double initialDesiredHeadingDeg;
        public double speedMeterPerSecond;
        public bool simple;
        public ObstacleAvoidanceParameters parameters;
        // public IElevationProvider elevationProvider;

        // States
        Dictionary<double, bool> headingPassedMap = new();

        public static bool enableROIShoreFieldAvoidance = true;

        public static ObstacleAvoidChecker Extract(ShipLog shipLog, ObstacleAvoidanceParameters parameters, bool simple=false)
        {
            // var speedKnots = shipLog.GetSpeedKnots();
            // if(speedKnots < 0) // Assume it's move astern to resolve collision
            //     speedKnots = 4;
            var speedKnots = Math.Max(shipLog.GetSpeedKnots(), shipLog.GetMaxSpeedKnots() * 0.75d);
            
            var speedMeterPerSecond = speedKnots / 3600 * MeasureUtils.navalMileToMeter;

            var latLon = new LatLon(shipLog.GetLatitudeDeg(), shipLog.GetLongitudeDeg());

            return new()
            {
                latLon=latLon,
                initialDesiredHeadingDeg=shipLog.desiredHeadingDeg,
                speedMeterPerSecond=speedMeterPerSecond,
                simple=simple,
                parameters=parameters ?? ObstacleAvoidanceParameters.Strong
                // elevationProvider=ServiceLocator.Get<IElevationProvider>()
            };
        }

        double GetLegacyStepDeg() => simple ? LegacySimpleStepDeg : LegacyStepDeg;

        public double Check()
        {
            if (TryCheckROIShoreField(out var shoreFieldHeadingDeg))
                return shoreFieldHeadingDeg;

            return CheckLegacy();
        }

        double CheckLegacy()
        {
            if(speedMeterPerSecond <= 0)
                return initialDesiredHeadingDeg;

            for(var deltaHeadingDeg = 0d; deltaHeadingDeg < 180; deltaHeadingDeg += GetLegacyStepDeg())
            {
                var currentHeading = initialDesiredHeadingDeg + deltaHeadingDeg;
                if(Detect3(currentHeading))
                {
                    return currentHeading;
                }
                if(deltaHeadingDeg > 0)
                {
                    currentHeading = initialDesiredHeadingDeg - deltaHeadingDeg;
                    if(Detect3(currentHeading))
                    {
                        return currentHeading;
                    }
                }
            }

            return initialDesiredHeadingDeg;
        }

        bool TryCheckROIShoreField(out double newHeadingDeg)
        {
            newHeadingDeg = initialDesiredHeadingDeg;

            if (speedMeterPerSecond <= 0 || !enableROIShoreFieldAvoidance)
                return false;

            var shoreFieldProvider = ElevationService.Instance.shoreFieldProvider;
            if (shoreFieldProvider == null || !shoreFieldProvider.HasValidROIShoreField())
                return false;

            if (!shoreFieldProvider.TrySampleROIShoreField(latLon, out var currentSample))
                return false;

            if (currentSample.distancePixels >= parameters.roiEarlyExitDistancePixels)
            {
                newHeadingDeg = initialDesiredHeadingDeg;
                return true;
            }

            var previewDistanceM = speedMeterPerSecond * parameters.roiPreviewSeconds;
            Geodesic.WGS84.Direct(latLon.LatDeg, latLon.LonDeg, initialDesiredHeadingDeg, previewDistanceM, out double lat2, out double lon2);
            var previewLatLon = new LatLon((double)lat2, (double)lon2);
            if (!shoreFieldProvider.TrySampleROIShoreField(previewLatLon, out var previewSample))
                return false;

            var goal = HeadingDegToVector(initialDesiredHeadingDeg);
            var away = ResolveAwayVector(currentSample, previewSample);
            var previewDistancePixels = previewSample.distancePixels;
            var avoidWeight = previewDistancePixels <= parameters.roiHardClearancePixels
                ? 1d
                : Clamp01((parameters.roiInfluenceDistancePixels - previewDistancePixels) / (parameters.roiInfluenceDistancePixels - parameters.roiHardClearancePixels));

            if (avoidWeight <= 0d)
            {
                newHeadingDeg = initialDesiredHeadingDeg;
                return true;
            }

            if (away.LengthSquared() <= ROIShoreGradientEpsilon)
                return false;

            away = Vector2.Normalize(away);
            var tangentLeft = new Vector2(-away.Y, away.X);
            var tangentRight = -tangentLeft;
            var tangent = Vector2.Dot(goal, tangentLeft) >= Vector2.Dot(goal, tangentRight) ? tangentLeft : tangentRight;

            Vector2 steer = previewDistancePixels <= parameters.roiHardClearancePixels
                ? tangent * 0.55d + away * 1.0d
                : goal * (1d - avoidWeight) + tangent * avoidWeight * 0.7d + away * avoidWeight * 0.9d;

            if (steer.LengthSquared() <= ROIShoreGradientEpsilon)
                return false;

            steer = Vector2.Normalize(steer);
            newHeadingDeg = VectorToHeadingDeg(steer);
            return true;
        }

        bool IsUseLegacyBound() => true;

        public bool Detect3(double headingDeg)
        {
            if(IsUseLegacyBound())
            {
                return Detect(headingDeg - LegacyBoundDeg)
                    && Detect(headingDeg)
                    && Detect(headingDeg + LegacyBoundDeg);
            }
            return Detect(headingDeg);
        }

        IReadOnlyList<double> GetLegacyExtrapolateRange() => simple ? LegacySimpleExtrapolateRange : LegacyExtrapolateRange;

        public bool Detect(double headingDeg)
        {
            if(headingPassedMap.TryGetValue(headingDeg, out var passed))
                return passed;

            var ret = true;
            // for(var extrapolateSeconds = extrapolateSecondsLow; extrapolateSeconds <= extrapolateMinHigh; extrapolateSeconds += extrapolateMinStep)
            foreach(var extrapolateSeconds in GetLegacyExtrapolateRange())
            {
                var distM = speedMeterPerSecond * extrapolateSeconds;
                double arcLength = Geodesic.WGS84.Direct(latLon.LatDeg, latLon.LonDeg, headingDeg, distM, out double lat2, out double lon2);
                var latLon2 = new LatLon((double)lat2, (double)lon2);
                // if(elevationProvider.GetElevation(latLon2) >= 0)
                if(ElevationService.Instance.GetElevation(latLon2) > 0)
                {
                    ret = false;
                    break;
                }
            }

            headingPassedMap[headingDeg] = ret;
            return ret;
        }

        static Vector2 HeadingDegToVector(double headingDeg)
        {
            var rad = headingDeg * Math.PI / 180d;
            return new Vector2(Math.Sin(rad), Math.Cos(rad));
        }

        static double VectorToHeadingDeg(Vector2 direction)
        {
            return MeasureUtils.NormalizeAngle(Math.Atan2(direction.X, direction.Y) * 180d / Math.PI);
        }

        static Vector2 ResolveAwayVector(ShoreFieldSample currentSample, ShoreFieldSample previewSample)
        {
            var previewGradient = previewSample.gradient;
            var currentGradient = currentSample.gradient;

            if (previewGradient.LengthSquared() > ROIShoreGradientEpsilon && currentGradient.LengthSquared() > ROIShoreGradientEpsilon)
                return Vector2.Normalize(previewGradient) * 0.8d + Vector2.Normalize(currentGradient) * 0.2d;

            if (previewGradient.LengthSquared() > ROIShoreGradientEpsilon)
                return previewGradient;

            return currentGradient;
        }

        static double Clamp01(double value) => Math.Clamp(value, 0d, 1d);

    }
}
