using NavalCombatCore;
using NavalCombat.Avalonia.Resources;
namespace NavalCombat.Unity
{
public sealed partial class NavalGame
{
    ShipLog SelectedShip=>selection as ShipLog;
    double DamagePercent=>SelectedShip==null?0:SelectedShip.damagePoint/System.Math.Max(1,SelectedShip.shipClass.damagePoint)*100;
    IEnumerable<ShipLog> GetControlledFormationShips()
    {
        if(SelectedShip==null)yield break;
        var queue=new Queue<ShipLog>();var seen=new HashSet<ShipLog>();queue.Enqueue(SelectedShip);
        while(queue.Count>0){var ship=queue.Dequeue();if(!seen.Add(ship))continue;yield return ship;foreach(var child in Session.GameState.shipLogsOnMap.Where(s=>s.GetControlPredecessor()==ship))queue.Enqueue(child);}
    }
    public string DamageTierSummary
    {
        get
        {
            var ship = SelectedShip;
            if (ship == null)
                return "—";
            if (ship.mapState == MapState.Destroyed)
                return NavalCombatStrings.Text("Ui.Sunk");

            var level = DamagePercent switch
            {
                <= 0d => NavalCombatStrings.Text("Core.DirectorControlType.None"),
                < 25d => NavalCombatStrings.Text("Ui.Light"),
                < 70d => NavalCombatStrings.Text("Core.RangeBand.Medium"),
                _ => NavalCombatStrings.Text("Ui.Heavy")
            };
            return NavalCombatStrings.Text("Ui.Shell.TierValueValue", ship.GetDamageTier(), level);
        }
    }
    public string PropulsionDamageSummary
    {
        get
        {
            var ship = SelectedShip;
            if (ship?.shipClass == null)
                return "—";

            var nominal = Math.Max(0d, ship.shipClass.speedKnots);
            var effective = ship.GetMaxSpeedKnots();
            var percent = nominal <= 0d ? 0d : effective / nominal * 100d;
            return $"{effective:0.0}/{nominal:0.0} kn · {Math.Clamp(percent, 0d, 100d):0}%";
        }
    }
    public string MachineryDamageSummary
    {
        get
        {
            var ship = SelectedShip;
            if (ship?.shipClass == null)
                return "—";

            var status = ship.dynamicStatus;
            var engineHits = status.engineRoomHits + status.engineRoomFloodingHits
                + ship.GetSubStates<IEngineRoomHitModifier>().Sum(modifier => modifier.GetEngineRoomHitOffset());
            var boilerHits = status.boilerRoomHits + status.boilerRoomFloodingHits
                + ship.GetSubStates<IBoilerRoomHitModifier>().Sum(modifier => modifier.GetBoilerRoomHitOffset());
            return NavalCombatStrings.Text("Ui.Shell.EngineValueValue", engineHits, ship.shipClass.speedKnotsEngineRoomsLevels.Count)
                + NavalCombatStrings.Text("Ui.Shell.BoilerValueValue", boilerHits, ship.shipClass.speedKnotsBoilerRooms.Count)
                + NavalCombatStrings.Text("Ui.Shell.ShaftValueValue", status.propulsionShaftHits, ship.shipClass.speedKnotsPropulsionShaftLevels.Count);
        }
    }
    public string MachineryFloodingSummary
    {
        get
        {
            var ship = SelectedShip;
            if (ship?.shipClass == null)
                return "—";

            var flooded = ship.dynamicStatus.engineRoomFloodingHits + ship.dynamicStatus.boilerRoomFloodingHits;
            var spaces = ship.shipClass.speedKnotsEngineRoomsLevels.Count + ship.shipClass.speedKnotsBoilerRooms.Count;
            var percent = (double)flooded / Math.Max(1, spaces) * 100d;
            var danger = percent >= 80d ? NavalCombatStrings.Text("Ui.Shell.Danger") : string.Empty;
            return NavalCombatStrings.Text("Ui.Shell.ValueValueSpacesValueValue", flooded, spaces, percent, danger);
        }
    }
    public string DamageControlSummary
    {
        get
        {
            var ship = SelectedShip;
            if (ship?.shipClass == null)
                return "—";

            var states = ship.GetSubStatesDownward().ToList();
            var controllable = states.Count(state => state.damageControllable);
            var assigned = states.Count(state => state.damageControllable && state.damageControlApplied);
            return $"{ship.GetDamageControlRating()}/{ship.shipClass.damageControlRatingUnmodified} DCR"
                + NavalCombatStrings.Text("Ui.Shell.ValueValueAssigned", assigned, controllable);
        }
    }
    public double CurrentCourseValue => SelectedShip?.headingDeg ?? 0d;
    public double CurrentSpeedValue => SelectedShip?.speedKnots ?? 0d;
    public double MinimumOrderSpeed => SelectedShip?.GetMinSpeedKnots() ?? 0;
    public double MaximumOrderSpeed => SelectedShip?.GetMaxSpeedKnots() ?? 1;
    public double ControlledFormationMaximumSpeed => GetControlledFormationShips()
        .Select(ship => ship.GetMaxSpeedKnots())
        .DefaultIfEmpty(SelectedShip?.GetMaxSpeedKnots() ?? 0d)
        .Min();
    public string CurrentManeuverSummary => SelectedShip == null
        ? string.Empty
        : NavalCombatStrings.Text("Ui.Shell.CurrentCourseValue", SelectedShip.headingDeg)
          + NavalCombatStrings.Text("Ui.Shell.SpeedValueValueKn", SelectedShip.speedKnots, SelectedShip.GetMaxSpeedKnots())
          + NavalCombatStrings.Text("Ui.Shell.GroupValueKn", ControlledFormationMaximumSpeed);

}
}
