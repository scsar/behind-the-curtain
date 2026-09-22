using System;
using System.Collections.Generic;

namespace BehindTheCurtain.Core
{
    /// <summary>Pure candidates. Intentions come exclusively from the selector's blackboard.</summary>
    internal sealed class PuppetActions
    {
        private readonly PuppetSettings settings;
        private readonly Vec2 formation;
        internal PuppetActions(PuppetSettings settings, Vec2 formation)
        { this.settings = settings; this.formation = formation; }

        internal IReadOnlyList<UtilityAction> Create() => new[] {
            new UtilityAction(ActionIds.Approach, new[] {
                Factor("targetDistance", c => {
                    var target = ApproachTarget(c);
                    return target == null ? 0 : Far(Distance(c.Agent.Position, target.Position));
                }) }, settings.ApproachWeight),
            new UtilityAction(ActionIds.Intercept, new[] {
                Factor("healerThreatProximity", c => {
                    var threat = Threat(c, TargetFactIds.HealerThreat);
                    var healer = Healer(c);
                    return c.Agent.Role != TargetRole.Guard || threat == null || healer == null ? 0
                        : 1 - Far(Distance(healer.Position, threat.Position));
                }),
                Factor("interceptDisplacement", c => settings.InterceptPositionFloor
                    + (1 - settings.InterceptPositionFloor)
                    * Far(Distance(c.Agent.Position, Resolve(c, ActionIds.Intercept).Goal)))
            }, settings.InterceptWeight),
            new UtilityAction(ActionIds.Spread, new[] {
                Factor("nearestPuppetProximity", c => {
                    var nearest = Nearest(c, a => a.Side == Side.Puppets && !a.Id.Equals(c.Agent.Id));
                    return nearest == null ? 0 : AiNumber.Clamp(1
                        - Distance(c.Agent.Position, nearest.Position) / settings.SpreadDistance);
                }) }, settings.SpreadWeight),
            new UtilityAction(ActionIds.Hold, Array.Empty<UtilityConsideration>(), settings.HoldWeight),
            new UtilityAction(ActionIds.Attack, new[] {
                Factor("humanInRange", c => AttackTarget(c) == null ? 0 : 1)
            }, settings.AttackWeight)
        };

        private static UtilityConsideration Factor(string id, Func<UtilityContext, double> input) =>
            new UtilityConsideration(id, input, new ResponseCurve(ResponseCurveKind.Linear, 1, 0));

        internal (Vec2 Goal, AgentId? Target) Resolve(UtilityContext c, string? action)
        {
            if (action == ActionIds.Approach)
            {
                var target = ApproachTarget(c);
                return (target?.Position ?? c.Agent.Position, target?.Id);
            }
            if (action == ActionIds.Attack) return (c.Agent.Position, AttackTarget(c)?.Id);
            if (action == ActionIds.Hold) return (formation, null);
            if (action == ActionIds.Intercept)
            {
                var threat = Threat(c, TargetFactIds.HealerThreat);
                var healer = Healer(c);
                if (c.Agent.Role != TargetRole.Guard || threat == null || healer == null)
                    return (c.Agent.Position, null);
                // Short segments stop at the threat rather than extending behind it.
                return (MoveTowards(healer.Position, threat.Position, settings.InterceptDistance), threat.Id);
            }
            if (action == ActionIds.Spread)
            {
                var away = new Vec2(0, 0);
                AgentState? nearest = null;
                double nearestDistance = double.PositiveInfinity;
                foreach (var other in c.World.GetAgents())
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

        private double Far(double distance) => distance / (settings.DistanceScale + distance);
        private static bool Human(AgentState a) => a.Side == Side.Players && a.IsHuman;
        private static AgentState? Threat(UtilityContext c, string key)
        {
            var fact = c.Blackboard.GetFact<AgentId>(key);
            if (!fact.Success) return null;
            var found = c.World.Get(fact.Value);
            return found.Success && Human(found.Value) ? found.Value : null;
        }
        private static AgentState? ApproachTarget(UtilityContext c) =>
            Threat(c, TargetFactIds.RangedThreat) ?? Nearest(c, Human);
        private AgentState? AttackTarget(UtilityContext c)
        {
            var target = Nearest(c, Human);
            return target != null && Distance(c.Agent.Position, target.Position) <= settings.AttackRange
                ? target : null;
        }
        private static AgentState? Healer(UtilityContext c) =>
            Nearest(c, a => a.Side == Side.Puppets && a.Role == TargetRole.Healer);
        private static AgentState? Nearest(UtilityContext c, Func<AgentState, bool> predicate)
        {
            AgentState? nearest = null;
            double best = double.PositiveInfinity;
            foreach (var a in c.World.GetAgents())
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
