using System;

namespace BehindTheCurtain.Core
{
    public enum HarassPhase { Waiting, Strike, Recover, Withdraw }

    internal sealed partial class PuppetActions
    {
        private AgentId? flankTarget;
        private Vec2 flankDirection;
        private double flankRadius;
        private bool flankClosing;
        private AgentId? harassTarget;
        private long harassPhaseTick;
        private long harassIntervalTick;
        private Vec2 harassDirection;
        internal HarassPhase? Phase { get; private set; }
        internal bool HarassAttack { get; private set; }
        internal bool IsCommitted(UtilityContext c)
        {
            if (harassTarget.HasValue) return c.World.IsParticipating(harassTarget.Value);
            return flankTarget.HasValue && c.World.IsParticipating(flankTarget.Value)
                && Distance(c.Agent.Position, c.World.Get(flankTarget.Value).Value.Position) > c.World.GetAttackRange(c.Agent.Id, settings).Value;
        }
        internal string? CommittedAction => harassTarget.HasValue ? ActionIds.Harass : flankTarget.HasValue ? ActionIds.Flank : null;

        internal void ResetBasicActions()
        {
            flankTarget = null; harassTarget = null; Phase = null; HarassAttack = false;
        }

        private bool Front(UtilityContext c, AgentState target, out Vec2 direction)
        {
            double x = 0, z = 0; int count = 0;
            foreach (var other in c.World.GetParticipatingAgents())
            {
                if (other.Side != Side.Puppets || other.Id.Equals(c.Agent.Id) || !other.Target.HasValue
                    || !other.Target.Value.Equals(target.Id)) continue;
                if (other.CurrentAction != ActionIds.Approach && other.CurrentAction != ActionIds.Attack
                    && other.CurrentAction != ActionIds.Pressure) continue;
                x += other.Position.X; z += other.Position.Z; count++;
            }
            direction = count == 0 ? new Vec2(0, 0) : Direction(target.Position, new Vec2(x / count, z / count), new Vec2(0, 1));
            return count > 0;
        }

        private double FlankSuitability(UtilityContext c)
        {
            var target = ApproachTarget(c);
            if (target == null) return 0;
            double distance = Distance(c.Agent.Position, target.Position);
            return distance > c.World.GetAttackRange(c.Agent.Id, settings).Value
                && distance <= settings.ThreatRadius && Front(c, target, out _) ? 1 : 0;
        }

        private static Vec2 Rotate(Vec2 direction, double angle) => new Vec2(
            direction.X * Math.Cos(angle) - direction.Z * Math.Sin(angle),
            direction.X * Math.Sin(angle) + direction.Z * Math.Cos(angle));

        private (Vec2 Goal, AgentId? Target) ResolveFlank(UtilityContext c)
        {
            harassTarget = null; Phase = null; HarassAttack = false;
            if (flankTarget.HasValue && !c.World.IsParticipating(flankTarget.Value)) flankTarget = null;
            var target = flankTarget.HasValue && c.World.IsParticipating(flankTarget.Value)
                ? c.World.Get(flankTarget.Value).Value : ApproachTarget(c);
            if (target == null || (!flankTarget.HasValue && !Front(c, target, out _)))
            { flankTarget = null; return (c.Agent.Position, null); }
            double distance = Distance(c.Agent.Position, target.Position);
            if (!flankTarget.HasValue)
            {
                Front(c, target, out var front);
                var left = Rotate(front, settings.FlankAngle * Math.PI / 180);
                var right = Rotate(front, -settings.FlankAngle * Math.PI / 180);
                var radial = Direction(target.Position, c.Agent.Position, front);
                flankDirection = radial.X * left.X + radial.Z * left.Z >= radial.X * right.X + radial.Z * right.Z ? left : right;
                // Never expand an already smaller radius: outward travel would be fleeing.
                flankRadius = Math.Max(distance, settings.FlankRadius);
                flankTarget = target.Id; flankClosing = false;
            }
            var current = Direction(target.Position, c.Agent.Position, flankDirection);
            double delta = Math.Atan2(current.X * flankDirection.Z - current.Z * flankDirection.X,
                current.X * flankDirection.X + current.Z * flankDirection.Z);
            double step = settings.MoveSpeed * c.World.GetMoveSpeedScale(c.Agent.Id).Value
                * c.World.MovementScale(c.Agent.Id) / settings.TicksPerSecond;
            if (Math.Abs(delta) * distance <= step) flankClosing = true;
            double radius = flankClosing ? Math.Min(distance, c.World.GetAttackRange(c.Agent.Id, settings).Value - settings.AttackArrivalMargin)
                : Math.Min(flankRadius, distance);
            var next = flankClosing ? flankDirection : Rotate(current, Math.Sign(delta) * Math.Min(Math.Abs(delta), step / Math.Max(distance, step)));
            return (new Vec2(target.Position.X + next.X * radius, target.Position.Z + next.Z * radius), target.Id);
        }

        private Vec2 ConstrainFlank(UtilityContext c, Vec2 next)
        {
            if (flankClosing || !flankTarget.HasValue || !c.World.IsParticipating(flankTarget.Value)) return next;
            var target = c.World.Get(flankTarget.Value).Value;
            double radius = Math.Min(flankRadius, Distance(c.Agent.Position, target.Position));
            var radial = Direction(target.Position, next, flankDirection);
            return new Vec2(target.Position.X + radial.X * radius, target.Position.Z + radial.Z * radius);
        }

        private AgentState? HarassTarget(UtilityContext c)
        {
            AgentState? best = null; int fewest = int.MaxValue; double closest = double.PositiveInfinity;
            foreach (var human in c.World.GetParticipatingAgents())
            {
                if (!Human(human)) continue;
                int count = 0;
                foreach (var other in c.World.GetParticipatingAgents())
                    if (other.Side == Side.Puppets && !other.Id.Equals(c.Agent.Id) && other.Target.HasValue
                        && other.Target.Value.Equals(human.Id)) count++;
                double distance = Distance(c.Agent.Position, human.Position);
                if (count < fewest || (count == fewest && distance < closest))
                { best = human; fewest = count; closest = distance; }
            }
            return best;
        }

        private (Vec2 Goal, AgentId? Target) ResolveHarass(UtilityContext c)
        {
            flankTarget = null; HarassAttack = false;
            if (harassTarget.HasValue && !c.World.IsParticipating(harassTarget.Value))
            { harassTarget = null; Phase = null; }
            var target = harassTarget.HasValue ? c.World.Get(harassTarget.Value).Value : HarassTarget(c);
            if (target == null) { Phase = null; return (c.Agent.Position, null); }
            if (!harassTarget.HasValue)
            {
                harassTarget = target.Id; Phase = HarassPhase.Waiting;
                harassIntervalTick = c.Tick;
                harassDirection = Direction(target.Position, c.Agent.Position, new Vec2(0, -1));
            }
            double range = c.World.GetAttackRange(c.Agent.Id, settings).Value;
            double distance = Distance(c.Agent.Position, target.Position);
            if (Phase == HarassPhase.Waiting && unchecked((ulong)c.Tick - (ulong)harassIntervalTick) >= (ulong)settings.HarassIntervalTicks)
                Phase = HarassPhase.Strike;
            if (Phase == HarassPhase.Waiting && distance < range + settings.HarassMargin)
                Phase = HarassPhase.Withdraw;
            if (Phase == HarassPhase.Strike && distance <= range)
            {
                HarassAttack = true; Phase = HarassPhase.Recover; harassPhaseTick = c.Tick;
                return (c.Agent.Position, target.Id);
            }
            if (Phase == HarassPhase.Recover)
            {
                if (unchecked((ulong)c.Tick - (ulong)harassPhaseTick) <= (ulong)settings.HarassRecoverTicks)
                    return (c.Agent.Position, target.Id);
                Phase = HarassPhase.Withdraw;
                harassDirection = Direction(target.Position, c.Agent.Position, harassDirection);
            }
            double radius = Phase == HarassPhase.Strike ? range - settings.AttackArrivalMargin : range + settings.HarassMargin;
            var goal = new Vec2(target.Position.X + harassDirection.X * radius, target.Position.Z + harassDirection.Z * radius);
            if (Phase == HarassPhase.Withdraw && Distance(c.Agent.Position, goal) <= settings.AttackArrivalMargin)
            { harassTarget = null; Phase = HarassPhase.Waiting; }
            return (goal, target.Id);
        }
    }
}
