using System;
using System.Collections.Generic;

namespace BehindTheCurtain.Core
{
    internal sealed partial class PuppetActions
    {
        private IReadOnlyDictionary<AgentId, (string? Action, AgentId? Target)> pressureTargets
            = new Dictionary<AgentId, (string? Action, AgentId? Target)>();
        private AgentId? retainedTarget;
        private long targetChangedTick;
        private AgentState? selectedTarget;
        private bool prepared;

        private static int HumanCount(UtilityContext c)
        {
            int count = 0;
            foreach (var a in c.World.GetParticipatingAgents()) if (Human(a)) count++;
            return count;
        }
        private int OtherPressure(UtilityContext c, AgentId target)
        {
            int count = 0;
            foreach (var other in c.World.GetParticipatingAgents())
            {
                if (other.Side != Side.Puppets || other.Id.Equals(c.Agent.Id)) continue;
                var state = pressureTargets.TryGetValue(other.Id, out var pending)
                    ? pending : (other.CurrentAction, other.Target);
                if (IsPressuring(state.Item1) && state.Item2.HasValue && state.Item2.Value.Equals(target)) count++;
            }
            return count;
        }
        private double TargetScore(UtilityContext c, AgentState target)
        {
            int count = OtherPressure(c, target.Id);
            return 1 / (1 + Distance(c.Agent.Position, target.Position) / settings.TargetDistanceScale)
                * (count < settings.PressCap ? 1 : Math.Pow(settings.CrowdPenalty, count - settings.PressCap + 1))
                * settings.PressureFactor(count);
        }
        private AgentState? BestTarget(UtilityContext c, double radius)
        {
            AgentState? best = null; double score = -1;
            foreach (var human in c.World.GetParticipatingAgents())
            {
                if (!Human(human) || Distance(c.Agent.Position, human.Position) > radius) continue;
                double candidate = TargetScore(c, human);
                if (candidate > score || (candidate == score && best != null && human.Id.Value < best.Id.Value))
                { best = human; score = candidate; }
            }
            return best;
        }
        private AgentState? SelectPressureTarget(UtilityContext c)
            => prepared ? selectedTarget : ChoosePressureTarget(c);

        private AgentState? ChoosePressureTarget(UtilityContext c)
        {
            if (HumanCount(c) <= 1) return Nearest(c, Human);
            var best = BestTarget(c, settings.ThreatRadius) ?? Nearest(c, Human);
            if (best == null || !retainedTarget.HasValue || !c.World.IsParticipating(retainedTarget.Value)) return best;
            var current = c.World.Get(retainedTarget.Value).Value;
            if (!Human(current) || (Distance(c.Agent.Position, current.Position) > settings.ThreatRadius
                && Distance(c.Agent.Position, best.Position) <= settings.ThreatRadius)) return best;
            if (OtherPressure(c, current.Id) >= settings.PressCap) return best;
            if (unchecked((ulong)c.Tick - (ulong)targetChangedTick) < (ulong)settings.TargetMinHoldTicks
                || TargetScore(c, best) <= TargetScore(c, current) * settings.TargetSwitchMargin) return current;
            return best;
        }
        internal void PrepareTargets(UtilityContext c,
            IReadOnlyDictionary<AgentId, (string? Action, AgentId? Target)> pressure)
        {
            pressureTargets = pressure;
            prepared = false;
            if (!retainedTarget.HasValue && c.Agent.Target.HasValue && IsPressuring(c.Agent.CurrentAction))
            { retainedTarget = c.Agent.Target; targetChangedTick = c.Tick; }
            selectedTarget = ChoosePressureTarget(c);
            prepared = true;
            if (HumanCount(c) > 1)
            {
                if (flankTarget.HasValue && (selectedTarget == null || !flankTarget.Value.Equals(selectedTarget.Id))) flankTarget = null;
                if (surroundTarget.HasValue && (selectedTarget == null || !surroundTarget.Value.Equals(selectedTarget.Id))) ResetSurround();
                if (harassTarget.HasValue && (selectedTarget == null || !harassTarget.Value.Equals(selectedTarget.Id)))
                { harassTarget = null; Phase = null; }
            }
        }
        internal void RememberTarget(UtilityContext c, string? action, AgentId? target)
        {
            if (!IsPressuring(action) || !target.HasValue) return;
            if (!retainedTarget.HasValue || !retainedTarget.Value.Equals(target.Value))
            { retainedTarget = target; targetChangedTick = c.Tick; }
        }
    }
}
