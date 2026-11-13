
using System;

namespace NavalCombatCore
{
    public partial class CoreParameter
    {
        static CoreParameter instance = new CoreParameter();
        public static CoreParameter Instance => instance;

        public bool checkLandCollision = true;
        public bool relaxedLandCollision = true;
        public bool checkShipCollision = true;
        public bool checkFriendlyShipCollision = false;
        public bool enableLeaderRuleVariant = true;

        public double angleStepDeg = 18; // 360 / 18 = 20 test angles => (0, 18, 36, 54, ...)
        public double attackCoef = 1d;
        // public double defenceCoef = 1d;
        public double defenceCoef = 0.1d;
        public double distanceCoef = 1;
        public double extrapolateSeconds=360;
        public double globalHitCoef = 1d;
        public double noPenetrationDamageCoef = 0.1d;
        public bool batteryDetailShowNonActiveModifier = false;

        public double automaticTorpedoFiringRangeRelaxedCoef = 2.5d;

        public int referenceTimeZoneOffset = +8; // Though it should be a view parameter instead of Core?
                                                 // public double automaticTorpedoFiringRelaxedAngle = 60; // Or dynamic resolved using standard or emergency turn?

        /// <summary>
        /// Convert UTC DateTime to Reference TimeZone DateTimeOffset
        /// </summary>
        /// <param name="time"> UTC DateTime</param>
        /// <returns></returns>
        public DateTimeOffset GetReferenceTimeZoneDateTimeOffset(DateTime time)
        {
            // $"{CoreParameter.Instance.GetReferenceTimeZoneDateTimeOffset(time).ToString("yyyy-MM-dd HH:mm:ss z")}: {SummaryContent()}";
            var dateTimeOffset = new DateTimeOffset(time);
            return dateTimeOffset.ToOffset(TimeSpan.FromHours(referenceTimeZoneOffset));
        }

        public string GetReferenceTimeZoneDateTimeOffsetString(DateTime time) => GetReferenceTimeZoneDateTimeOffset(time).ToString("yyyy-MM-dd HH:mm:ss z");
    }
}
