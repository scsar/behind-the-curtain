using System;
using System.Collections.Generic;

namespace BehindTheCurtain.Core
{
    /// <summary>Pure candidates. Intentions come exclusively from the selector's blackboard.</summary>
    internal sealed class PuppetActions
    {
        private readonly PuppetSettings settings;
        private readonly Vec2 formation;
        private AgentId? protectedHealer;
        internal PuppetActions(PuppetSettings settings, Vec2 formation)
        { this.settings = settings; this.formation = formation; }

        internal IReadOnlyList<UtilityAction> Create(TargetRole role, DeterministicRandom random)
        {
            var result = new List<UtilityAction>();
            foreach (var id in new[] { ActionIds.Approach, ActionIds.Intercept, ActionIds.Spread,
                ActionIds.Hold, ActionIds.Attack, ActionIds.KeepDistance })
            {
                double weight = settings.GetRoleWeight(role, id);
                double jitter = 1 + (random.NextDouble() * 2 - 1) * settings.WeightJitter;
                // A role-specific intention explicitly enables a default-zero candidate at unit weight.
                result.Add(new UtilityAction(id, new[] { Factor("contract", c =>
                    (weight > 0 || c.Blackboard.HasIntention(role + ":" + id) ? 1 : 0) * Suitability(c, id)) },
                    (weight > 0 ? weight : 1) * jitter));
            }
            return result;
        }

        private double Suitability(UtilityContext c, string id)
        {
            var human = Nearest(c, Human);
            double distance = human == null ? double.PositiveInfinity : Distance(c.Agent.Position, human.Position);
            if (id == ActionIds.Approach)
            {
                var target = ApproachTarget(c);
                return target != null && Distance(c.Agent.Position, target.Position) > settings.AttackRange ? 1 : 0;
            }
            if (id == ActionIds.Attack) return AttackTarget(c) == null ? 0 : 1;
            if (id == ActionIds.Hold) return distance > settings.ThreatRadius ? 1 : 0;
            if (id == ActionIds.KeepDistance) return human == null ? 0 :
                distance <= settings.KeepMax || c.Agent.CurrentAction == id
                    || c.Blackboard.HasIntention(c.Agent.Role + ":" + id) ? 1 : 0;
            if (id == ActionIds.Intercept)
            {
                if (AttackTarget(c) != null) return 0;
                var healer = Healer(c);
                if (healer == null || healer.Id.Equals(c.Agent.Id) || Threat(c, TargetFactIds.HealerThreat) == null) return 0;
                var reservation = c.Blackboard.GetReservation("intercept:" + healer.Id.Value, c.Tick);
                if (reservation.Success && !reservation.Value.Owner.Equals(c.Agent.Id)) return 0;
                var tactic = c.Blackboard.GetFact<TacticId>("tactic");
                return tactic.Success && tactic.Value == TacticId.GuardHealer ? 1 : settings.PassiveInterceptScale;
            }
            var nearest = Nearest(c, a => a.Side == Side.Puppets && !a.Id.Equals(c.Agent.Id));
            return nearest == null ? 0 : AiNumber.Clamp(1 - Distance(c.Agent.Position, nearest.Position) / settings.SpreadDistance);
        }

        internal void Reserve(UtilityContext c, string? action)
        {
            if (action != ActionIds.Intercept) { protectedHealer = null; return; }
            var healer = Healer(c);
            if (healer == null) return;
            var reserved = c.Blackboard.Reserve("intercept:" + healer.Id.Value, c.Agent.Id,
                c.Tick == long.MaxValue ? c.Tick : c.Tick + 1, c.Tick);
            if (reserved.Success) protectedHealer = healer.Id;
        }

        private static UtilityConsideration Factor(string id, Func<UtilityContext, double> input) =>
            new UtilityConsideration(id, input, new ResponseCurve(ResponseCurveKind.Linear, 1, 0));

        internal (Vec2 Goal, AgentId? Target) Resolve(UtilityContext c, string? action)
        {
            if (action == ActionIds.Approach)
            {
                var target = ApproachTarget(c);
                return (target == null ? c.Agent.Position : MoveTowards(target.Position, c.Agent.Position, settings.AttackRange - settings.AttackArrivalMargin), target?.Id);
            }
            if (action == ActionIds.Attack) return (c.Agent.Position, AttackTarget(c)?.Id);
            if (action == ActionIds.Hold) return (formation, null);
            if (action == ActionIds.Intercept)
            {
                var threat = Threat(c, TargetFactIds.HealerThreat);
                var healer = Healer(c);
                if (threat == null || healer == null)
                    return (c.Agent.Position, null);
                // Short segments stop at the threat rather than extending behind it.
                return (MoveTowards(healer.Position, threat.Position, settings.InterceptDistance), threat.Id);
            }
            if (action == ActionIds.KeepDistance)
            {
                var human = Nearest(c, Human);
                if (human == null) return (c.Agent.Position, null);
                double distance = Distance(c.Agent.Position, human.Position);
                var guard = Nearest(c, a => a.Side == Side.Puppets && a.Role == TargetRole.Guard
                    && !a.Id.Equals(c.Agent.Id));
                var direction = Direction(human.Position, guard?.Position ?? c.Agent.Position, new Vec2(0, -1));
                // Escape radially first; crossing the threat to reach a guard is never safe.
                if (distance < settings.KeepMin)
                    direction = Direction(human.Position, c.Agent.Position, new Vec2(0, -1));
                if (guard == null && distance >= settings.KeepMin && distance <= settings.KeepMax)
                    return (c.Agent.Position, human.Id);
                double radius = distance < settings.KeepMin ? settings.KeepMin :
                    Math.Max(settings.KeepMin, Math.Min(settings.KeepMax,
                        guard == null ? settings.KeepMax : Distance(guard.Position, human.Position) + settings.GuardRearOffset));
                return (new Vec2(human.Position.X + direction.X * radius,
                    human.Position.Z + direction.Z * radius), human.Id);
            }
            if (action == ActionIds.Spread)
            {
                var away = new Vec2(0, 0);
                AgentState? nearest = null;
                double nearestDistance = double.PositiveInfinity;
                foreach (var other in c.World.GetParticipatingAgents())
                {
                    if (other.Side != Side.Puppets || other.Id.Equals(c.Agent.Id)) continue;
                    double distance = Distance(c.Agent.Position, other.Position);
                    if (distance >= settings.SpreadDistance) continue;
                    if (distance < nearestDistance) { nearest = other; nearestDistance = distance; }
                    var direction = Away(c.Agent, other);
                    double weight = 1 - distance / settings.SpreadDistance;
                    away = new Vec2(away.X + direction.X * weight, away.Z + direction.Z * weight);
                }
                // Symmetric crowds and coincident spawns still have a deterministic escape direction.
                if (away.X == 0 && away.Z == 0 && nearest != null) away = Away(c.Agent, nearest);
                var unit = Unit(away);
                return (new Vec2(c.Agent.Position.X + unit.X * settings.SpreadDistance,
                    c.Agent.Position.Z + unit.Z * settings.SpreadDistance), null);
            }
            return (c.Agent.Position, null);
        }

        internal Vec2 ConstrainMovement(UtilityContext c, string? action, Vec2 next)
        {
            if (action == ActionIds.Spread) return next;
            var human = Nearest(c, Human);
            if (human == null) return next;
            double before = Distance(c.Agent.Position, human.Position);
            if (action == ActionIds.KeepDistance)
            {
                // Follow the guard around the safe band rather than cutting through its interior.
                if (before >= settings.KeepMin && Distance(next, human.Position) < settings.KeepMin)
                {
                    var direction = Direction(human.Position, next, new Vec2(0, -1));
                    return new Vec2(human.Position.X + direction.X * settings.KeepMin,
                        human.Position.Z + direction.Z * settings.KeepMin);
                }
                return next;
            }
            // Hold/intercept destinations can move behind us. Never retreat from a nearby human
            // unless the selected action explicitly calls for it.
            return before <= settings.ThreatRadius && Distance(next, human.Position) > before
                ? c.Agent.Position : next;
        }

        private static bool Human(AgentState a) => a.Side == Side.Players && a.IsHuman;
        private static AgentState? Threat(UtilityContext c, string key)
        {
            var fact = c.Blackboard.GetFact<AgentId>(key);
            if (!fact.Success) return null;
            var found = c.World.Get(fact.Value);
            return found.Success && Human(found.Value) ? found.Value : null;
        }
        private static AgentState? ApproachTarget(UtilityContext c)
        {
            var tactic = c.Blackboard.GetFact<TacticId>("tactic");
            return (tactic.Success && tactic.Value == TacticId.ShieldAdvance
                ? Threat(c, TargetFactIds.RangedThreat) : null) ?? Nearest(c, Human);
        }
        private AgentState? AttackTarget(UtilityContext c)
        {
            var target = Nearest(c, Human);
            return target != null && Distance(c.Agent.Position, target.Position) <= settings.AttackRange
                ? target : null;
        }
        private AgentState? Healer(UtilityContext c)
        {
            // A running intercept keeps its reserved healer even if healers exchange proximity.
            if (protectedHealer.HasValue && c.World.IsParticipating(protectedHealer.Value))
                return c.World.Get(protectedHealer.Value).Value;
            protectedHealer = null;
            return Nearest(c, a => a.Side == Side.Puppets && a.Role == TargetRole.Healer);
        }
        private static AgentState? Nearest(UtilityContext c, Func<AgentState, bool> predicate)
        {
            AgentState? nearest = null;
            double best = double.PositiveInfinity;
            foreach (var a in c.World.GetParticipatingAgents())
            {
                if (!predicate(a)) continue;
                double distance = Distance(c.Agent.Position, a.Position);
                if (distance < best) { best = distance; nearest = a; }
            }
            return nearest;
        }
        private static Vec2 Away(AgentState a, AgentState b) => Distance(a.Position, b.Position) == 0
            ? new Vec2(a.Id.Value < b.Id.Value ? -1 : 1, 0)
            : Unit(new Vec2(a.Position.X - b.Position.X, a.Position.Z - b.Position.Z));
        internal static double Distance(Vec2 a, Vec2 b) => Length(new Vec2(a.X - b.X, a.Z - b.Z));
        private static double Length(Vec2 v)
        {
            double scale = Math.Max(Math.Abs(v.X), Math.Abs(v.Z));
            if (scale == 0) return 0;
            return scale * Math.Sqrt((v.X / scale) * (v.X / scale) + (v.Z / scale) * (v.Z / scale));
        }
        private static Vec2 Unit(Vec2 v)
        {
            double length = Length(v);
            return length == 0 ? new Vec2(0, 0) : new Vec2(v.X / length, v.Z / length);
        }
        internal static Vec2 MoveTowards(Vec2 from, Vec2 to, double distance)
        {
            double length = Distance(from, to);
            if (length <= distance) return to;
            var unit = Unit(new Vec2(to.X - from.X, to.Z - from.Z));
            return new Vec2(from.X + unit.X * distance, from.Z + unit.Z * distance);
        }
        internal static Vec2 Direction(Vec2 from, Vec2 to, Vec2 fallback) =>
            from.X == to.X && from.Z == to.Z ? fallback : Unit(new Vec2(to.X - from.X, to.Z - from.Z));
    }
}
