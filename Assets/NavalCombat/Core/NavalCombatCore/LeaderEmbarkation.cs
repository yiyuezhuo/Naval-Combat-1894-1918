using System;
using System.Collections.Generic;
using System.Linq;
using CoreUtils;

namespace NavalCombatCore
{
    public enum LeaderPost { Captain, Commander, DeputyCommander, ChiefOfStaff, StaffOfficer, VisitingOfficial }

    public class EmbarkedLeader
    {
        public string leaderObjectId;
        public string commandObjectId;
        public LeaderPost post;
        public GlobalString title;
        public int? displayPriority;

        public int GetDisplayPriority() => displayPriority ?? (post switch
        {
            LeaderPost.VisitingOfficial => 0,
            LeaderPost.Commander => 10,
            LeaderPost.DeputyCommander => 20,
            LeaderPost.ChiefOfStaff => 30,
            LeaderPost.Captain => 40,
            _ => 50
        });

        public int GetCommandDepth()
        {
            var command = EntityManager.Instance.Get<ShipGroup>(commandObjectId);
            if (command == null) return int.MaxValue;
            int depth = 0;
            var visited = new HashSet<string>();
            while (command != null && visited.Add(command.objectId))
            {
                depth++;
                command = EntityManager.Instance.Get<ShipGroup>(command.parentObjectId);
            }
            return depth;
        }
    }

    public class LeaderAppointment : EmbarkedLeader
    {
        public string shipObjectId;
    }

    public partial class NavalGameState
    {
        public bool leaderEmbarkationInitialized;
        public List<LeaderAppointment> leaderAppointments = new();

        public void InitializeLeaderEmbarkation()
        {
            if (leaderEmbarkationInitialized) return;
            if (leaderAppointments.GroupBy(a => a.leaderObjectId).Any(group => group.Select(a => a.shipObjectId).Distinct().Count() > 1))
                throw new InvalidOperationException("A leader cannot be appointed to multiple ships in the same scenario.");
            foreach (var appointment in leaderAppointments)
            {
                if (!leaders.Any(l => l.objectId == appointment.leaderObjectId) ||
                    !shipLogs.Any(s => s.objectId == appointment.shipObjectId) ||
                    (!string.IsNullOrEmpty(appointment.commandObjectId) &&
                     !shipGroups.Any(g => g.objectId == appointment.commandObjectId) &&
                     !shipLogs.Any(s => s.objectId == appointment.commandObjectId)))
                    throw new InvalidOperationException($"Invalid leader appointment: {appointment.leaderObjectId}, ship {appointment.shipObjectId}");
            }
            foreach (var ship in shipLogs)
            {
                ship.embarkedLeaders = new List<EmbarkedLeader>();
                if (ship.leader != null)
                    ship.embarkedLeaders.Add(new EmbarkedLeader
                    {
                        leaderObjectId = ship.leader.objectId,
                        commandObjectId = ship.objectId,
                        post = LeaderPost.Captain
                    });
            }
            foreach (var group in shipGroups.OrderBy(group => group.objectId, StringComparer.Ordinal))
            {
                if (group.leader == null) continue;
                var flagship = group.Walk<ShipLog>()
                    .Where(ship => ship.controlMode == ControlMode.Independent)
                    .OrderByDescending(ship => ship.shipClass?.displacementTons ?? 0)
                    .ThenBy(ship => ship.objectId, StringComparer.Ordinal)
                    .FirstOrDefault();
                if (flagship == null) continue;
                flagship.embarkedLeaders.Add(new EmbarkedLeader
                {
                    leaderObjectId = group.leader.objectId,
                    commandObjectId = group.objectId,
                    post = LeaderPost.Commander
                });
            }
            SeparateSuperiorAndSubordinateCommanders();
            foreach (var appointments in leaderAppointments.GroupBy(a => a.leaderObjectId))
            {
                foreach (var ship in shipLogs)
                    ship.embarkedLeaders.RemoveAll(e => e.leaderObjectId == appointments.Key);
                foreach (var appointment in appointments)
                    shipLogs.Single(s => s.objectId == appointment.shipObjectId).embarkedLeaders.Add(new EmbarkedLeader
                    {
                        leaderObjectId = appointment.leaderObjectId,
                        commandObjectId = appointment.commandObjectId,
                        post = appointment.post,
                        title = appointment.title?.Clone(),
                        displayPriority = appointment.displayPriority
                    });
            }
            leaderEmbarkationInitialized = true;
        }

        void SeparateSuperiorAndSubordinateCommanders()
        {
            var moves = new List<(ShipLog source, ShipLog target, EmbarkedLeader entry)>();
            foreach (var ship in shipLogs)
            {
                var commanders = ship.embarkedLeaders.Where(entry => entry.post == LeaderPost.Commander).ToList();
                foreach (var entry in commanders)
                {
                    var command = EntityManager.Instance.Get<ShipGroup>(entry.commandObjectId);
                    if (command == null || !commanders.Any(other => other.leaderObjectId != entry.leaderObjectId &&
                        IsSuperiorCommand(other.commandObjectId, command))) continue;
                    var tail = FindFormationTail(ship, command);
                    if (tail != ship) moves.Add((ship, tail, entry));
                }
            }
            foreach (var move in moves)
            {
                move.source.embarkedLeaders.Remove(move.entry);
                move.target.embarkedLeaders.Add(move.entry);
            }
        }

        static bool IsSuperiorCommand(string candidateId, ShipGroup command)
        {
            var visited = new HashSet<string>();
            while (command != null && visited.Add(command.objectId))
            {
                command = EntityManager.Instance.Get<ShipGroup>(command.parentObjectId);
                if (command != null && command.objectId == candidateId) return true;
            }
            return false;
        }

        static ShipLog FindFormationTail(ShipLog head, ShipGroup command)
        {
            var members = command.Walk<ShipLog>().Distinct().ToList();
            var visited = new HashSet<ShipLog> { head };
            var current = head;
            while (true)
            {
                // Relative-position escorts are not part of the line ahead.
                var followers = members.Where(ship => ship != current && ship.controlMode == ControlMode.FollowTarget &&
                    ship.GetControlModeAndTargetInlucdeNonMap().Item2 == current).ToList();
                if (followers.Count == 0) return current;
                // Branching formations and cycles do not identify a unique tail.
                if (followers.Count != 1 || !visited.Add(followers[0])) return head;
                current = followers[0];
            }
        }
    }
}
