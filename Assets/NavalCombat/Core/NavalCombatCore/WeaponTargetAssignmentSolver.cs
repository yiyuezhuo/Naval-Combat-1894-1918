using System.Collections.Generic;
using System.Linq;
using System.Collections;

namespace NavalCombatCore
{
    public interface IWTAObject : IDF4Model
    {
        // double EvaluateArmorScore();
        // double EvaluateArmorScore(TargetAspect targetAspect, RangeBand rangeBand);
        double EvaluateSurvivability();
        // double EvaluateBatteryFirepowerScore();
        // double EvaluateBatteryFirepowerScore(double distanceYards, TargetAspect targetAspect, double targetSpeedKnots, double bearingRelativeToBowDeg);
        // double EvaluateTorpedoThreatScore();
        // double EvaluateRapidFiringFirepowerScore();
        double EvaluateFirepowerScore();
        // double EvaluateGeneralScore();
        IWTAObject GetManualFireTarget();

        IEnumerable<IWTABattery> GetBatteries();
    }

    public interface IWTABattery
    {
        double EvaluateFirepowerScore(double distanceYards, TargetAspect targetAspect, double targetSpeedKnots, double bearingRelativeToBowDeg);
        IWTAObject GetCurrentFiringTarget();
        void SetFiringTarget(IWTAObject target);
        void ResetFiringTarget();
        int GetOverConcentrationCoef();
        bool IsChangeTargetBlocked();
    }

    public class WeaponTargetAssignmentSolver // WTA Problem Solver
    {
        // The primary goal is to reduce hostile potential firepower effectiveness. Thus:
        // 1. Fire Suppression: Deliver minimal firepower to enemy to create "under-fire" debuff to decrease their current fire projection.
        // 2. Mission Kill: Prior to attack low-survivability platform with high firepower.
        // 3. Prevent Over-concentration: it is possible that some available batteries are not used.
        // 4. Firepower stickiness: Firing platforms tend to remain engaged with the same target to avoid target-switching debuffs and to preserve visual coherence.

        // Though global optimal solution seems too "rational" for a era that gunnery officer make decision independently, the algorithm self is greedy and cannot be very closer to the global optimal solution.

        public double underfireCoef = 0.1d;
        public double overconcentrateCoef = 0.2d;
        public double changeTargetCoef = 0d; // Legacy flat stickiness is disabled in favor of state-based retention bonuses.
        public static double currentTargetTrackingEffectivenessBonus = 0.2d; // Bonus when keeping fire on a target already under Tracking/Hitting.
        public double longTermThreatWeight = 0.025d;
        public double shortTermThreatWeight = 0.975d;
        public static double shortThreatMinDistanceFactor = 0.33d;
        public static double shortThreatMaxDistanceFactor = 3d;

        // Urgency boost for knife-fight distances where enemy torpedo danger is assumed to be imminent.
        public static double torpedoThreatRangeYards = 1500d;
        public static double torpedoThreatTargetUrgencyFactor = 2.0d;
        public static double subjectiveCloseRangePreferenceMinFactor = 0.5d;

        public class ShooterRecord
        {
            public IWTAObject original;
            // Frozen Values
            public List<BatteryRecord> batteries = new();
            public Dictionary<TargetRecord, MeasureStats> measurements = new();
            public TargetRecord manualFireTarget;
            // Solver States

            public override string ToString()
            {
                return $"ShooterRecord({original})";
            }
        }

        public class TargetRecord
        {
            public IWTAObject original;
            // Frozen Values
            public double survivability;
            public double selfFirepowerScore;
            public double shortTermThreatScore;
            public double blendedThreatScore;
            public double speedKnots;
            // Solver states
            public double underFirepower;
            public int overConcentrationScore;

            public override string ToString()
            {
                return $"TargetRecord({original})";
            }
        }

        public class BatteryRecord
        {
            public IWTABattery original;
            // Frozen Values
            public TargetRecord currentTarget;
            public double currentTargetFireEffectivenessFactor = 1d;
            // Solver States
            public Dictionary<TargetRecord, double> firepowerScoreMap = new();
            public TargetRecord assignedTarget;
            public int overConcentrationCoef = 1; // regular corrected fire: +1, barrage fire: +2, RF Batteries: +0 (DoB) or +2 (Literally)?
                                                  // TODO: Switch to double
            public bool isChangeTargetBlocked;

            public override string ToString()
            {
                return $"BatteryRecord({original})";
            }
        }

        public class DecisionRecord
        {
            public ShooterRecord shooter;
            public BatteryRecord battery;
            public TargetRecord target;
            public double gain;
            public double firepowerScore;

            public override string ToString()
            {
                return $"DecisionRecord({shooter}, {battery} -> {target}, {gain}, {firepowerScore})";
            }
        }

        readonly struct CandidateEvaluation
        {
            public readonly MeasureStats stats;
            public readonly double threatDistanceFactor;
            public readonly double targetUrgencyFactor;
            public readonly double tryAddedFirepowerScoreBase;
            public readonly double tryAddedFirepowerScoreEffective;
            public readonly int tryAddedOverconcentrationScore;
            public readonly double rawGainBeforeStickiness;
            public readonly double changeTargetMultiplier;
            public readonly double finalGain;
            public readonly bool isCurrentTarget;

            public CandidateEvaluation(
                MeasureStats stats,
                double threatDistanceFactor,
                double targetUrgencyFactor,
                double tryAddedFirepowerScoreBase,
                double tryAddedFirepowerScoreEffective,
                int tryAddedOverconcentrationScore,
                double rawGainBeforeStickiness,
                double changeTargetMultiplier,
                double finalGain,
                bool isCurrentTarget)
            {
                this.stats = stats;
                this.threatDistanceFactor = threatDistanceFactor;
                this.targetUrgencyFactor = targetUrgencyFactor;
                this.tryAddedFirepowerScoreBase = tryAddedFirepowerScoreBase;
                this.tryAddedFirepowerScoreEffective = tryAddedFirepowerScoreEffective;
                this.tryAddedOverconcentrationScore = tryAddedOverconcentrationScore;
                this.rawGainBeforeStickiness = rawGainBeforeStickiness;
                this.changeTargetMultiplier = changeTargetMultiplier;
                this.finalGain = finalGain;
                this.isCurrentTarget = isCurrentTarget;
            }
        }

        public void Solve(IEnumerable<IWTAObject> shooterObjects, IEnumerable<IWTAObject> targetObjects)
        {
            var state = InitializeSolverState(shooterObjects, targetObjects);
            ApplyNonChangeableAssignments(state);

            // Pick a local optimal in every step until decision space become a empty set. 
            while (true)
            {
                var hasDecision = TryFindBestDecision(state, out var bestBattery, out var bestTarget, out var bestGain, out var bestFirepowerScore);

                if (!hasDecision)
                    break;
                if (bestGain <= 0)
                    break;

                // DEBUG
                // if (bestDecisionRecord.battery.currentTarget != bestDecisionRecord.target)
                // {
                //     var r = bestDecisionRecord;
                //     UnityEngine.Debug.LogWarning($"Retarget: ({r.shooter}, {r.battery}) {r.battery} -> {r.target}");
                // }

                ApplyDecision(bestBattery, bestTarget, bestFirepowerScore);
            }

            // Apply result
            ApplyAssignmentsToOriginal(state);
        }

        public WeaponTargetAssignmentInspectionSession CreateInspectionSession(IEnumerable<IWTAObject> shooterObjects, IEnumerable<IWTAObject> targetObjects)
        {
            var state = InitializeSolverState(shooterObjects, targetObjects);
            ApplyNonChangeableAssignments(state);
            return new WeaponTargetAssignmentInspectionSession(this, state);
        }

        internal WeaponTargetAssignmentSolverState InitializeSolverState(IEnumerable<IWTAObject> shooterObjects, IEnumerable<IWTAObject> targetObjects)
        {
            var shooters = shooterObjects.Select(s => new ShooterRecord()
            {
                original = s,
                batteries = s.GetBatteries().Select(b => new BatteryRecord()
                {
                    original = b,
                    overConcentrationCoef = b.GetOverConcentrationCoef()
                }).ToList()
            }).ToList();

            var targets = targetObjects.Select(t => new TargetRecord()
            {
                original = t,
                survivability = t.EvaluateSurvivability(),
                selfFirepowerScore = t.EvaluateFirepowerScore(),
                speedKnots = t.GetSpeedKnots(),
            }).ToList();

            var oriToTarget = targets.ToDictionary(t => t.original, t => t);

            foreach (var shooter in shooters)
            {
                var manualFireTarget = shooter.original.GetManualFireTarget();
                if (manualFireTarget != null)
                {
                    shooter.manualFireTarget = oriToTarget.GetValueOrDefault(manualFireTarget);
                }

                foreach (var battery in shooter.batteries)
                {
                    var currentTargetObject = battery.original.GetCurrentFiringTarget();
                    if (currentTargetObject != null)
                    {
                        battery.currentTarget = oriToTarget.GetValueOrDefault(currentTargetObject);
                        battery.isChangeTargetBlocked = battery.original.IsChangeTargetBlocked();
                        if (battery.currentTarget != null)
                        {
                            battery.currentTargetFireEffectivenessFactor = GetCurrentTargetFireEffectivenessFactor(battery);
                        }
                    }
                }

                foreach (var target in targets)
                {
                    var stats = shooter.measurements[target] = MeasureStats.Measure(shooter.original, target.original);
                    var pairShortTermThreat = target.selfFirepowerScore * GetThreatDistanceFactor(stats.distanceYards);
                    if (pairShortTermThreat > target.shortTermThreatScore)
                    {
                        target.shortTermThreatScore = pairShortTermThreat;
                    }

                    foreach (var battery in shooter.batteries)
                    {
                        var firepowerScore = battery.original.EvaluateFirepowerScore(
                                stats.distanceYards,
                                stats.targetPresentAspectFromObserver,
                                target.speedKnots,
                                stats.observerToTargetBearingRelativeToBowDeg)
                            * GetSubjectiveCloseRangePreferenceFactor(stats.distanceYards);
                        battery.firepowerScoreMap[target] = firepowerScore;
                    }
                }
            }

            foreach (var target in targets)
            {
                target.blendedThreatScore =
                    target.selfFirepowerScore * longTermThreatWeight
                    + target.shortTermThreatScore * shortTermThreatWeight;
            }

            return new WeaponTargetAssignmentSolverState(shooters, targets);
        }

        internal void ApplyNonChangeableAssignments(WeaponTargetAssignmentSolverState state)
        {
            foreach (var shooter in state.shooters)
            {
                foreach (var battery in shooter.batteries)
                {
                    if (battery.assignedTarget != null)
                        continue;

                    if (battery.currentTarget != null && battery.isChangeTargetBlocked)
                    {
                        battery.assignedTarget = battery.currentTarget; // TODO: Is it too harsh to a battery which is capable to shoot multiply targets?
                        battery.currentTarget.underFirepower += battery.firepowerScoreMap[battery.currentTarget] * battery.currentTargetFireEffectivenessFactor;
                        battery.currentTarget.overConcentrationScore += battery.overConcentrationCoef;
                    }
                }
            }
        }

        internal bool TryFindBestDecision(WeaponTargetAssignmentSolverState state, out BatteryRecord bestBattery, out TargetRecord bestTarget, out double bestGain, out double bestFirepowerScore)
        {
            bool hasDecision = false;
            bestBattery = null;
            bestTarget = null;
            bestGain = 0d;
            bestFirepowerScore = 0d;

            foreach (var shooter in state.shooters)
            {
                foreach (var battery in shooter.batteries)
                {
                    if (battery.assignedTarget != null)
                        continue;

                    foreach (var target in state.targets)
                    {
                        if (shooter.manualFireTarget != null && shooter.manualFireTarget != target)
                            continue;

                        var evaluation = EvaluateCandidate(shooter, battery, target);
                        if (!hasDecision || evaluation.finalGain > bestGain)
                        {
                            hasDecision = true;
                            bestBattery = battery;
                            bestTarget = target;
                            bestGain = evaluation.finalGain;
                            bestFirepowerScore = evaluation.tryAddedFirepowerScoreEffective;
                        }
                    }
                }
            }

            return hasDecision;
        }

        internal List<WeaponTargetAssignmentGainRow> BuildGainRows(WeaponTargetAssignmentSolverState state, out WeaponTargetAssignmentGainRow bestRow)
        {
            var rows = new List<WeaponTargetAssignmentGainRow>();
            bestRow = null;
            var bestGain = 0d;
            var hasDecision = false;
            var scanOrder = 0;

            foreach (var shooter in state.shooters)
            {
                foreach (var battery in shooter.batteries)
                {
                    if (battery.assignedTarget != null)
                        continue;

                    foreach (var target in state.targets)
                    {
                        if (shooter.manualFireTarget != null && shooter.manualFireTarget != target)
                            continue;

                        scanOrder++;
                        var evaluation = EvaluateCandidate(shooter, battery, target);
                        var row = new WeaponTargetAssignmentGainRow
                        {
                            scanOrder = scanOrder,
                            shooterName = ResolveObjectShortName(shooter.original),
                            batteryName = ResolveBatteryShortName(battery.original),
                            targetName = ResolveObjectShortName(target.original),
                            finalGain = evaluation.finalGain,
                            rawGainBeforeStickiness = evaluation.rawGainBeforeStickiness,
                            distanceYards = evaluation.stats.distanceYards,
                            targetSelfFirepower = target.blendedThreatScore,
                            targetLongTermThreat = target.selfFirepowerScore,
                            targetShortTermThreat = target.shortTermThreatScore,
                            threatDistanceFactor = evaluation.threatDistanceFactor,
                            targetSurvivability = target.survivability,
                            targetUrgencyFactor = evaluation.targetUrgencyFactor,
                            currentUnderFirepower = target.underFirepower,
                            currentOverConcentrationScore = target.overConcentrationScore,
                            tryAddedFirepowerScoreBase = evaluation.tryAddedFirepowerScoreBase,
                            tryAddedFirepowerScoreEffective = evaluation.tryAddedFirepowerScoreEffective,
                            tryAddedOverconcentrationScore = evaluation.tryAddedOverconcentrationScore,
                            isCurrentTarget = evaluation.isCurrentTarget,
                            currentTargetFireEffectivenessFactor = battery.currentTargetFireEffectivenessFactor,
                            changeTargetMultiplier = evaluation.changeTargetMultiplier,
                            batteryRecord = battery,
                            targetRecord = target,
                        };
                        rows.Add(row);

                        if (!hasDecision || row.finalGain > bestGain)
                        {
                            hasDecision = true;
                            bestGain = row.finalGain;
                            bestRow = row;
                        }
                    }
                }
            }

            return rows;
        }

        CandidateEvaluation EvaluateCandidate(ShooterRecord shooter, BatteryRecord battery, TargetRecord target)
        {
            var stats = shooter.measurements[target];
            var threatDistanceFactor = GetThreatDistanceFactor(stats.distanceYards);
            var targetUrgencyFactor = GetTargetUrgencyFactor(stats.distanceYards);
            var tryAddedFirepowerScoreBase = battery.firepowerScoreMap[target];
            var isCurrentTarget = battery.currentTarget == target;
            var tryAddedFirepowerScoreEffective = tryAddedFirepowerScoreBase;
            if (isCurrentTarget)
            {
                tryAddedFirepowerScoreEffective *= battery.currentTargetFireEffectivenessFactor;
            }

            var tryAddedOverconcentrationScore = battery.overConcentrationCoef;
            var rawGainBeforeStickiness = GetTargettingScoreGain(
                target.blendedThreatScore,
                target.survivability,
                targetUrgencyFactor,
                target.underFirepower,
                target.overConcentrationScore,
                tryAddedFirepowerScoreEffective,
                tryAddedOverconcentrationScore);
            var changeTargetMultiplier = isCurrentTarget ? 1 + changeTargetCoef : 1d;
            var finalGain = rawGainBeforeStickiness * changeTargetMultiplier;

            return new CandidateEvaluation(
                stats,
                threatDistanceFactor,
                targetUrgencyFactor,
                tryAddedFirepowerScoreBase,
                tryAddedFirepowerScoreEffective,
                tryAddedOverconcentrationScore,
                rawGainBeforeStickiness,
                changeTargetMultiplier,
                finalGain,
                isCurrentTarget);
        }

        internal void ApplyDecision(BatteryRecord bestBattery, TargetRecord bestTarget, double bestFirepowerScore)
        {
            bestBattery.assignedTarget = bestTarget; // TODO: Too harsh to battery which is capable to shoot multiply targets?
            bestTarget.underFirepower += bestFirepowerScore;
            bestTarget.overConcentrationScore += bestBattery.overConcentrationCoef;
        }

        internal void ApplyAssignmentsToOriginal(WeaponTargetAssignmentSolverState state)
        {
            foreach (var shooter in state.shooters)
            {
                foreach (var battery in shooter.batteries)
                {
                    battery.original.SetFiringTarget(battery.assignedTarget?.original);
                }
            }
        }

        static string ResolveObjectShortName(IWTAObject obj)
        {
            if (obj is ShipLog shipLog)
            {
                return shipLog.namedShip?.name?.GetShortName()
                    ?? shipLog.namedShip?.name?.GetMergedName()
                    ?? shipLog.objectId
                    ?? "[Invalid]";
            }

            return obj?.ToString() ?? "[Invalid]";
        }

        static string ResolveBatteryShortName(IWTABattery battery)
        {
            if (battery is BatteryStatus gunBattery)
            {
                return gunBattery.GetBatteryRecord()?.name?.GetShortName()
                    ?? "Battery";
            }

            if (battery is RapidFiringBatteryStatusOneSide rapidBattery)
            {
                var baseName = rapidBattery.original.GetRapidFireBatteryRecord()?.name?.GetShortName()
                    ?? "Rapid Firing";
                return $"{baseName} ({rapidBattery.side})";
            }

            if (battery is TorpedoBattery torpedoBattery)
            {
                return torpedoBattery.original?.shipClass?.torpedoSector?.name?.GetShortName()
                    ?? "Torpedo";
            }

            return battery?.ToString() ?? "[Invalid]";
        }

        public double GetTargettingScore(double targetSelfFirepower, double targetSurvivability, double targetUrgencyFactor, double targetUnderFirepower, int overConcentrationScore)
        {
            // var score = targetSelfFirepower / (1 + targetSurvivability) * targetUnderFirepower;
            var targetValue = (1 + targetSelfFirepower) / (1 + targetSurvivability) * targetUrgencyFactor;
            var score = targetValue * targetUnderFirepower;
            if (overConcentrationScore == 1)
            {
                score *= 1 + underfireCoef;
            }
            else if (overConcentrationScore >= 2)
            {
                score *= 1 - overconcentrateCoef * (overConcentrationScore - 1);
            }
            return score;
        }

        public double GetTargettingScoreGain(double targetSelfFirepower, double targetSurvivability, double targetUrgencyFactor,
            double currentTargetUnderFirepower, int currentOverConcentrationScore,
            double newBatteryFirepower, int tryAddedOverconcentrationScore)
        {
            var currentScore = GetTargettingScore(targetSelfFirepower, targetSurvivability, targetUrgencyFactor,
                currentTargetUnderFirepower, currentOverConcentrationScore);
            var newScore = GetTargettingScore(targetSelfFirepower, targetSurvivability, targetUrgencyFactor,
                currentTargetUnderFirepower + newBatteryFirepower, currentOverConcentrationScore + tryAddedOverconcentrationScore);
            return newScore - currentScore;
        }

        static double GetTargetUrgencyFactor(double distanceYards)
        {
            return distanceYards < torpedoThreatRangeYards ? torpedoThreatTargetUrgencyFactor : 1d;
        }

        static double GetSubjectiveCloseRangePreferenceFactor(double distanceYards)
        {
            if (distanceYards <= 2000d)
            {
                return LerpByDistance(1d, 0.9d, distanceYards, 0d, 2000d);
            }

            if (distanceYards <= 4500d)
            {
                return LerpByDistance(0.9d, 0.7d, distanceYards, 2000d, 4500d);
            }

            if (distanceYards <= 8000d)
            {
                return LerpByDistance(0.7d, 0.5d, distanceYards, 4500d, 8000d);
            }

            return subjectiveCloseRangePreferenceMinFactor;
        }

        static double GetThreatDistanceFactor(double distanceYards)
        {
            var preferenceFactor = GetSubjectiveCloseRangePreferenceFactor(distanceYards);
            var denominator = 1d - subjectiveCloseRangePreferenceMinFactor;
            if (denominator <= 0d)
                return shortThreatMaxDistanceFactor;

            var normalizedPreference = (preferenceFactor - subjectiveCloseRangePreferenceMinFactor) / denominator;
            if (normalizedPreference < 0d)
                normalizedPreference = 0d;
            else if (normalizedPreference > 1d)
                normalizedPreference = 1d;

            return shortThreatMinDistanceFactor
                + (shortThreatMaxDistanceFactor - shortThreatMinDistanceFactor) * normalizedPreference;
        }

        static double LerpByDistance(double startValue, double endValue, double distanceYards, double startDistanceYards, double endDistanceYards)
        {
            if (endDistanceYards <= startDistanceYards)
                return endValue;

            var t = (distanceYards - startDistanceYards) / (endDistanceYards - startDistanceYards);
            if (t < 0d)
                t = 0d;
            else if (t > 1d)
                t = 1d;
            return startValue + (endValue - startValue) * t;
        }

        static double GetCurrentTargetFireEffectivenessFactor(BatteryRecord battery)
        {
            if (battery.currentTarget == null)
                return 1d;

            var factor = 1d;

            if (battery.original is BatteryStatus gunBattery)
            {
                var currentTargetShip = battery.currentTarget.original as ShipLog;
                var hasTrackingBonus = gunBattery.fireControlSystemStatusRecords.Any(fcs =>
                    fcs.IsOperational() &&
                    fcs.GetTarget() == currentTargetShip &&
                    (fcs.trackingState == TrackingSystemState.Tracking || fcs.trackingState == TrackingSystemState.Hitting)
                );
                if (hasTrackingBonus)
                {
                    factor += currentTargetTrackingEffectivenessBonus;
                }

                var averageProcessSeconds = gunBattery.mountStatus
                    .Where(mnt => mnt.IsOperational() && mnt.GetFiringTarget() == currentTargetShip)
                    .Select(mnt => mnt.processSeconds)
                    .DefaultIfEmpty(0d)
                    .Average();
                factor += averageProcessSeconds / 120d;
                return factor;
            }

            if (battery.original is RapidFiringBatteryStatusOneSide rapidBattery)
            {
                var currentTargetShip = battery.currentTarget.original as ShipLog;
                var processingSeconds = rapidBattery.original.targettingRecords
                    .Where(r => r.location == rapidBattery.side && r.GetTarget() == currentTargetShip)
                    .Select(r => r.processingSeconds)
                    .DefaultIfEmpty(0d)
                    .First();
                factor += processingSeconds / 120d;
            }

            return factor;
        }
    }

    internal sealed class WeaponTargetAssignmentSolverState
    {
        public readonly List<WeaponTargetAssignmentSolver.ShooterRecord> shooters;
        public readonly List<WeaponTargetAssignmentSolver.TargetRecord> targets;

        public WeaponTargetAssignmentSolverState(
            List<WeaponTargetAssignmentSolver.ShooterRecord> shooters,
            List<WeaponTargetAssignmentSolver.TargetRecord> targets)
        {
            this.shooters = shooters;
            this.targets = targets;
        }
    }

    public sealed class WeaponTargetAssignmentGainRow
    {
        public int scanOrder;
        public string shooterName;
        public string batteryName;
        public string targetName;
        public double finalGain;
        public double rawGainBeforeStickiness;
        public double distanceYards;
        public double targetSelfFirepower;
        public double targetLongTermThreat;
        public double targetShortTermThreat;
        public double threatDistanceFactor;
        public double targetSurvivability;
        public double targetUrgencyFactor;
        public double currentUnderFirepower;
        public int currentOverConcentrationScore;
        public double tryAddedFirepowerScoreBase;
        public double tryAddedFirepowerScoreEffective;
        public int tryAddedOverconcentrationScore;
        public bool isCurrentTarget;
        public double currentTargetFireEffectivenessFactor;
        public double changeTargetMultiplier;

        internal WeaponTargetAssignmentSolver.BatteryRecord batteryRecord;
        internal WeaponTargetAssignmentSolver.TargetRecord targetRecord;
    }

    public sealed class WeaponTargetAssignmentInspectionSession
    {
        readonly WeaponTargetAssignmentSolver solver;
        readonly WeaponTargetAssignmentSolverState state;

        public List<WeaponTargetAssignmentGainRow> CurrentRows { get; private set; } = new();
        public WeaponTargetAssignmentGainRow BestRow { get; private set; }
        public int StepIndex { get; private set; }
        public bool CanStep => BestRow != null && BestRow.finalGain > 0d;

        internal WeaponTargetAssignmentInspectionSession(WeaponTargetAssignmentSolver solver, WeaponTargetAssignmentSolverState state)
        {
            this.solver = solver;
            this.state = state;
            RecomputeDecisionSpace();
        }

        public void RecomputeDecisionSpace()
        {
            CurrentRows = solver.BuildGainRows(state, out var bestRow);
            BestRow = bestRow;
        }

        public bool StepNext()
        {
            if (!CanStep)
                return false;

            solver.ApplyDecision(BestRow.batteryRecord, BestRow.targetRecord, BestRow.tryAddedFirepowerScoreEffective);
            BestRow.batteryRecord.original.SetFiringTarget(BestRow.targetRecord.original);
            StepIndex++;
            RecomputeDecisionSpace();
            return true;
        }
    }

}
