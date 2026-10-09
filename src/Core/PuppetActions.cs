using System;
using System.Collections.Generic;

namespace BehindTheCurtain.Core
{
    /// <summary>Pure candidates. Intentions come exclusively from the selector's blackboard.</summary>
    internal sealed partial class PuppetActions
    {
        private readonly PuppetSettings settings;
        private readonly Vec2 formation;
        private AgentId? protectedHealer;
        private Vec2? interceptWaypoint;
        private bool interceptWaypointReached;
        private Vec2 interceptWaypointThreat;
        internal PuppetActions(PuppetSettings settings, Vec2 formation)
        { this.settings = settings; this.formation = formation; }

        internal IReadOnlyList<UtilityAction> Create(TargetRole role, DeterministicRandom random, DirectorProfile profile, Loadout loadout)
        {
            var result = new List<UtilityAction>();
            foreach (var id in new[] { ActionIds.Approach, ActionIds.Intercept, ActionIds.Spread,
                ActionIds.Hold, ActionIds.Attack, ActionIds.KeepDistance, ActionIds.Pressure, ActionIds.Flank, ActionIds.Harass, ActionIds.Surround })
            {
                if (id == ActionIds.Pressure && (role != TargetRole.Guard || loadout.HasCapability(CapabilityTag.Protect))) continue;
                double weight = RoleContract.ForRole(role).EffectiveWeight(settings, loadout, id);
                double preference = (id == ActionIds.Flank || id == ActionIds.Surround) ? (loadout.RangeBand == RangeBand.Melee ? 1 : settings.FlankNonMeleeScale)
                    : id == ActionIds.Harass ? (loadout.RangeBand == RangeBand.Melee ? settings.HarassMeleeScale : 1)
                    : id == ActionIds.Approach ? (loadout.RangeBand == RangeBand.Melee ? 1 : settings.ApproachNonMeleeScale) : 1;
                double jitter = 1 + (random.NextDouble() * 2 - 1) * settings.WeightJitter;
                // Capability eligibility is checked before role/situation. Intercept has no pull share.
                // A role-specific intention explicitly enables a default-zero candidate at unit weight.
                result.Add(new UtilityAction(id, new[] { Factor("contract", c =>
                    (id == ActionIds.Intercept && !loadout.HasCapability(CapabilityTag.Protect) ? 0 :
                        (weight > 0 || (id != ActionIds.Pressure && c.Blackboard.HasIntention(role + ":" + id)) ? 1 : 0) * Suitability(c, id))) },
                    (weight > 0 ? weight : 1) * jitter * preference) { TensionWeighted = true });
            }
            if (profile.EnableWaver) result.Add(new UtilityAction(ActionIds.Waver, Array.Empty<UtilityConsideration>(), 0)
                { TensionWeighted = true, Pull = profile.WaverPull });
            return result;
        }

        private double Suitability(UtilityContext c, string id)
        {
            var human = Nearest(c, Human);
            double distance = human == null ? double.PositiveInfinity : Distance(c.Agent.Position, human.Position);
            if (id == ActionIds.Flank) return FlankSuitability(c);
            if (id == ActionIds.Surround) return SurroundSuitability(c);
            if (id == ActionIds.Harass) return Nearest(c, Human) == null ? 0 : 1;
            if (id == ActionIds.Pressure)
            {
                var threat = Threat(c, TargetFactIds.HealerThreat);
                return threat != null && AttackTarget(c) == null && Distance(c.Agent.Position, threat.Position) > c.World.GetAttackRange(c.Agent.Id, settings).Value ? 1 : 0;
            }
            if (id == ActionIds.Approach)
            {
                var target = ApproachTarget(c);
                return target != null && Distance(c.Agent.Position, target.Position) > c.World.GetAttackRange(c.Agent.Id, settings).Value ? 1 : 0;
            }
            if (id == ActionIds.Attack) return AttackTarget(c) == null ? 0 : 1;
            if (id == ActionIds.Hold) return distance > settings.ThreatRadius ? 1 : 0;
            if (id == ActionIds.KeepDistance) return human == null ? 0 :
                distance <= settings.KeepMax || c.Agent.CurrentAction == id
                    || c.Blackboard.HasIntention(c.Agent.Role + ":" + id) ? 1 : 0;
            if (id == ActionIds.Intercept)
            {
                var healer = Healer(c);
                var threat = Threat(c, TargetFactIds.HealerThreat);
                if (healer == null || healer.Id.Equals(c.Agent.Id) || threat == null) return 0;
                if (AttackTarget(c) != null && Distance(threat.Position, healer.Position) >= Distance(c.Agent.Position, healer.Position)) return 0;
                var reservation = c.Blackboard.GetReservation("intercept:" + healer.Id.Value, c.Tick);
                if (reservation.Success && !reservation.Value.Owner.Equals(c.Agent.Id)) return 0;
                var tactic = c.Blackboard.GetFact<TacticId>("tactic");
                var strength = c.Blackboard.GetFact<double>(TargetFactIds.HealerThreatStrength);
                return tactic.Success && tactic.Value == TacticId.GuardHealer ? 1
                    : Math.Max(settings.PassiveInterceptScale, (strength.Success ? AiNumber.Clamp(strength.Value) : 0)
                        * settings.IndividualThreatInterceptScale);
            }
            var nearest = Nearest(c, a => a.Side == Side.Puppets && !a.Id.Equals(c.Agent.Id));
            return nearest == null ? 0 : AiNumber.Clamp(1 - Distance(c.Agent.Position, nearest.Position) / settings.SpreadDistance);
        }

        internal void Reserve(UtilityContext c, string? action)
        {
            if (action != ActionIds.Intercept) { protectedHealer = null; interceptWaypoint = null; interceptWaypointReached = false; return; }
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
            if (action != ActionIds.Surround) ResetSurround();
            if (action == ActionIds.Surround) return ResolveSurround(c);
            if (action == ActionIds.Flank) return ResolveFlank(c);
            if (action == ActionIds.Harass) return ResolveHarass(c);
            ResetBasicActions();
            if (action == ActionIds.Approach || action == ActionIds.Pressure)
            {
                var target = action == ActionIds.Pressure ? Threat(c, TargetFactIds.HealerThreat) : ApproachTarget(c);
                return (target == null ? c.Agent.Position : MoveTowards(target.Position, c.Agent.Position, c.World.GetAttackRange(c.Agent.Id, settings).Value - settings.AttackArrivalMargin), target?.Id);
            }
            if (action == ActionIds.Attack) return (c.Agent.Position, AttackTarget(c)?.Id);
            if (action == ActionIds.Hold) return (formation, null);
            if (action == ActionIds.Intercept)
            {
                var threat = Threat(c, TargetFactIds.HealerThreat);
                var healer = Healer(c);
                if (threat == null || healer == null)
                    return (c.Agent.Position, null);
                var goal = InterceptGeometry.ClosestPoint(c.Agent.Position, healer.Position, threat.Position, settings);
                if (Distance(c.Agent.Position, goal) <= settings.InterceptHoldTolerance)
                { interceptWaypoint = null; interceptWaypointReached = false; return (c.Agent.Position, threat.Id); }
                if (interceptWaypoint.HasValue)
                {
                    if (Distance(interceptWaypointThreat, threat.Position) == 0 &&
                        Distance(c.Agent.Position, interceptWaypoint.Value) <= settings.InterceptWaypointTolerance)
                    {
                        interceptWaypoint = null;
                        interceptWaypointReached = true;
                        return (goal, threat.Id);
                    }
                    interceptWaypoint = Detour(c.Agent.Position, goal, threat.Position);
                    interceptWaypointThreat = threat.Position;
                    if (Distance(c.Agent.Position, interceptWaypoint.Value) <= settings.InterceptWaypointTolerance)
                    {
                        interceptWaypoint = null; interceptWaypointReached = true;
                        return (goal, threat.Id);
                    }
                    return (interceptWaypoint.Value, threat.Id);
                }
                if (interceptWaypointReached && Distance(interceptWaypointThreat, threat.Position) > settings.InterceptWaypointTolerance) interceptWaypointReached = false;
                var closest = InterceptGeometry.Project(threat.Position, c.Agent.Position, goal);
                if (!interceptWaypointReached && Distance(closest, threat.Position) < settings.InterceptClearance)
                {
                    interceptWaypointThreat = threat.Position;
                    interceptWaypoint = Detour(c.Agent.Position, goal, threat.Position);
                    return (interceptWaypoint.Value, threat.Id);
                }
                return (goal, threat.Id);
            }
            if (action == ActionIds.KeepDistance)
            {
                var human = Nearest(c, Human);
                if (human == null) return (c.Agent.Position, null);
                double distance = Distance(c.Agent.Position, human.Position);
                if (c.Agent.Role == TargetRole.Healer && c.Blackboard.GetReservation("intercept:" + c.Agent.Id.Value, c.Tick).Success)
                {
                    if (distance >= settings.KeepMin && distance <= settings.KeepMax) return (c.Agent.Position, human.Id);
                    var radial = Direction(human.Position, c.Agent.Position, new Vec2(0, -1));
                    double boundary = distance < settings.KeepMin ? settings.KeepMin : settings.KeepMax;
                    return (new Vec2(human.Position.X + radial.X * boundary, human.Position.Z + radial.Z * boundary), human.Id);
                }
                var guard = Nearest(c, a => a.Side == Side.Puppets && a.Role == TargetRole.Guard
                    && !a.Id.Equals(c.Agent.Id));
                var direction = Direction(human.Position, guard?.Position ?? c.Agent.Position, new Vec2(0, -1));
                // Escape radially first; crossing the threat to reach a guard is never safe.
                if (distance < settings.KeepMin)
                    direction = Direction(human.Position, c.Agent.Position, new Vec2(0, -1));
                var radialDirection = Direction(human.Position, c.Agent.Position, new Vec2(0, -1));
                double alignment = radialDirection.X * direction.X + radialDirection.Z * direction.Z;
                if (distance >= settings.KeepMin && distance <= settings.KeepMax
                    && (guard == null || (c.Agent.Role == TargetRole.Healer
                        && alignment >= Math.Cos(settings.KeepAlignTolerance * Math.PI / 180))))
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

        private Vec2 Detour(Vec2 position, Vec2 goal, Vec2 threat)
        {
            var along = Direction(position, goal, new Vec2(0, 1));
            var left = new Vec2(threat.X - along.Z * settings.InterceptClearance,
                threat.Z + along.X * settings.InterceptClearance);
            var right = new Vec2(threat.X + along.Z * settings.InterceptClearance,
                threat.Z - along.X * settings.InterceptClearance);
            return Distance(position, left) <= Distance(position, right) ? left : right;
        }

        internal Vec2 ConstrainMovement(UtilityContext c, string? action, Vec2 next)
        {
            if (action == ActionIds.Surround) return ConstrainSurround(c, next);
            if (action == ActionIds.Flank) return ConstrainFlank(c, next);
            if (action == ActionIds.Harass) return next;
            if (action == ActionIds.Spread || action == ActionIds.Intercept || action == ActionIds.Pressure) return next;
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
            // Hold destinations can move behind us. Never retreat from a nearby human
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
            return target != null && Distance(c.Agent.Position, target.Position) <= c.World.GetAttackRange(c.Agent.Id, settings).Value
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
