using System;

namespace BehindTheCurtain.Core
{
    internal sealed partial class PuppetActions
    {
        internal int? SurroundSlot => surroundTarget.HasValue ? surroundSlot : (int?)null;
        private AgentId? surroundTarget;
        private int surroundSlot;
        private int surroundCount;
        private Vec2 surroundDirection;
        private bool surroundClosing;
        private Blackboard? surroundBoard;
        private AgentId surroundOwner;
        private string? surroundKey;

        private static bool IsPressuring(string? action) => action == ActionIds.Approach
            || action == ActionIds.Attack || action == ActionIds.Flank || action == ActionIds.Pressure
            || action == ActionIds.Harass || action == ActionIds.Surround;

        private static int PressureCount(UtilityContext c, AgentState target, out Vec2 front)
        {
            double x = 0, z = 0; int count = 1;
            foreach (var other in c.World.GetParticipatingAgents())
            {
                if (other.Side != Side.Puppets || other.Id.Equals(c.Agent.Id)
                    || !other.Target.HasValue || !other.Target.Value.Equals(target.Id)
                    || !IsPressuring(other.CurrentAction)) continue;
                x += other.Position.X; z += other.Position.Z; count++;
            }
            front = count == 1 ? new Vec2(0, 1)
                : Direction(target.Position, new Vec2(x / (count - 1), z / (count - 1)), new Vec2(0, 1));
            return count;
        }

        private double SurroundSuitability(UtilityContext c)
        {
            var target = ApproachTarget(c);
            if (target == null) return 0;
            double distance = Distance(c.Agent.Position, target.Position);
            return distance > c.World.GetAttackRange(c.Agent.Id, settings).Value
                && distance <= settings.ThreatRadius && PressureCount(c, target, out _) >= 3 ? 1 : 0;
        }

        private void ResetSurround()
        {
            if (surroundKey != null) surroundBoard?.ReleaseReservation(surroundKey, surroundOwner);
            surroundTarget = null; surroundKey = null; surroundBoard = null;
        }

        private (Vec2 Goal, AgentId? Target) ResolveSurround(UtilityContext c)
        {
            flankTarget = null; harassTarget = null; Phase = null; HarassAttack = false;
            if (surroundTarget.HasValue && !c.World.IsParticipating(surroundTarget.Value)) ResetSurround();
            var target = surroundTarget.HasValue && c.World.IsParticipating(surroundTarget.Value)
                ? c.World.Get(surroundTarget.Value).Value : ApproachTarget(c);
            if (target == null) { ResetSurround(); return (c.Agent.Position, null); }
            int count = PressureCount(c, target, out var front);
            // A claimed slot and reference frame last for this surround episode.
            // Attack transitions in the snapshot must not repeatedly reassign a moving puppet.
            if (surroundTarget.HasValue) count = surroundCount;
            string prefix = "surround:" + target.Id.Value + ":";
            if (!surroundTarget.HasValue)
            {
                // Share a fixed reference frame while this target's ring is occupied.
                // Recomputing F as the puppets rotate would rotate the ring itself.
                bool occupied = false;
                for (int i = 0; i < count; i++) occupied |= c.Blackboard.GetReservation(prefix + i, c.Tick).Success;
                string frameKey = prefix + "front:" + count;
                var frame = c.Blackboard.GetFact<Vec2>(frameKey);
                if (occupied && frame.Success) front = frame.Value;
                else c.Blackboard.SetFact(frameKey, front);
                var radial = Direction(target.Position, c.Agent.Position, front);
                double best = double.NegativeInfinity; int slot = -1;
                for (int i = 0; i < count; i++)
                {
                    var reservation = c.Blackboard.GetReservation(prefix + i, c.Tick);
                    if (reservation.Success && !reservation.Value.Owner.Equals(c.Agent.Id)) continue;
                    var direction = Rotate(front, 2 * Math.PI * i / count);
                    double alignment = radial.X * direction.X + radial.Z * direction.Z;
                    if (alignment > best) { best = alignment; slot = i; surroundDirection = direction; }
                }
                if (slot < 0) return (c.Agent.Position, target.Id);
                surroundSlot = slot; surroundCount = count; surroundTarget = target.Id;
                surroundKey = prefix + surroundSlot; surroundBoard = c.Blackboard; surroundOwner = c.Agent.Id;
                surroundClosing = false;
            }
            var reserved = c.Blackboard.Reserve(surroundKey!, c.Agent.Id,
                c.Tick == long.MaxValue ? c.Tick : c.Tick + 1, c.Tick);
            if (!reserved.Success) { ResetSurround(); return (c.Agent.Position, target.Id); }
            double distance = Distance(c.Agent.Position, target.Position);
            var current = Direction(target.Position, c.Agent.Position, surroundDirection);
            double delta = Math.Atan2(current.X * surroundDirection.Z - current.Z * surroundDirection.X,
                current.X * surroundDirection.X + current.Z * surroundDirection.Z);
            double step = settings.MoveSpeed * c.World.GetMoveSpeedScale(c.Agent.Id).Value
                * c.World.MovementScale(c.Agent.Id) / settings.TicksPerSecond;
            if (Math.Abs(delta) * distance <= step) surroundClosing = true;
            double radius = surroundClosing
                ? Math.Min(distance, c.World.GetAttackRange(c.Agent.Id, settings).Value - settings.AttackArrivalMargin)
                : Math.Min(Math.Max(distance, settings.FlankRadius), distance);
            var next = surroundClosing ? surroundDirection
                : Rotate(current, Math.Sign(delta) * Math.Min(Math.Abs(delta), step / Math.Max(distance, step)));
            return (new Vec2(target.Position.X + next.X * radius, target.Position.Z + next.Z * radius), target.Id);
        }

        private Vec2 ConstrainSurround(UtilityContext c, Vec2 next)
        {
            if (surroundClosing || !surroundTarget.HasValue || !c.World.IsParticipating(surroundTarget.Value)) return next;
            var target = c.World.Get(surroundTarget.Value).Value;
            double radius = Distance(c.Agent.Position, target.Position);
            var radial = Direction(target.Position, next, surroundDirection);
            return new Vec2(target.Position.X + radial.X * radius, target.Position.Z + radial.Z * radius);
        }
    }
}
