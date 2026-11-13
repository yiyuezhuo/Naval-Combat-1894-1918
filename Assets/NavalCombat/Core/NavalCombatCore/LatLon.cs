namespace NavalCombatCore
{

    public class LatLon
    {
        public double LatDeg;
        public double LonDeg;

        public LatLon()
        {

        }

        public LatLon(double latDeg, double lonDeg)
        {
            LatDeg = latDeg;
            LonDeg = lonDeg;
        }

        public override string ToString()
        {
            return $"LatLon({LatDeg}, {LonDeg})";
        }

        public LatLon Clone() => new LatLon(LatDeg, LonDeg);
    }

}