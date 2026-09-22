using System;
using System.Collections.Generic;

namespace BehindTheCurtain.Core
{
    public sealed class ConsiderationScore
    {
        internal ConsiderationScore(string id, double input, double value)
        { Id = id; Input = input; CurveValue = value; }
        public string Id { get; }
        public double Input { get; }
        public double CurveValue { get; }
    }

    public sealed class ActionScore
    {
        internal ActionScore(string id, List<ConsiderationScore> considerations, double product,
            double weight, double intention, double bonus, double score)
        {
            ActionId = id; Considerations = considerations.AsReadOnly(); Product = product;
            BaseWeight = weight; IntentionMultiplier = intention; PersistenceBonus = bonus; Score = score;
        }
        public string ActionId { get; }
        public IReadOnlyList<ConsiderationScore> Considerations { get; }
        public double Product { get; }
        public double BaseWeight { get; }
        public double IntentionMultiplier { get; }
        public double PersistenceBonus { get; }
        public double Score { get; }
        public bool Eligible => Score > 0;
    }

    public sealed class UtilitySelection
    {
        internal UtilitySelection(string? action, long tick, bool reevaluated, IReadOnlyList<ActionScore> scores)
        { ActionId = action; EvaluatedAtTick = tick; Reevaluated = reevaluated; Scores = scores; }
        public string? ActionId { get; }
        public long EvaluatedAtTick { get; }
        public bool Reevaluated { get; }
        public IReadOnlyList<ActionScore> Scores { get; }
    }

    /// <summary>
    /// One selector per agent, bound to one world. Selection does not execute actions or mutate World.
    /// The caller applies the result using World.UpdateAction. Candidate order is part of the input.
    /// </summary>
    public sealed class UtilitySelector
    {
        private readonly World world;
        private readonly AgentId agentId;
        private readonly UtilityAction[] actions;
        private readonly UtilitySettings settings;
        private readonly DeterministicRandom random;
        private UtilitySelection? previous;
        private long? lastRequestedTick;

        private UtilitySelector(World world, AgentId agentId, UtilityAction[] actions,
            UtilitySettings settings, DeterministicRandom random)
        { this.world = world; this.agentId = agentId; this.actions = actions; this.settings = settings; this.random = random; }

        public static AiResult<UtilitySelector> Create(World world, AgentId agentId,
            IReadOnlyList<UtilityAction> actions, UtilitySettings settings, DeterministicRandom random)
        {
            if (world == null) return AiResult<UtilitySelector>.Fail(AiFailure.InvalidValue);
            var agent = world.Get(agentId);
            if (!agent.Success) return AiResult<UtilitySelector>.Fail(agent.Failure);
            if (settings == null || !settings.IsValid || random == null)
                return AiResult<UtilitySelector>.Fail(AiFailure.InvalidSettings);
            if (actions == null) return AiResult<UtilitySelector>.Fail(AiFailure.InvalidAction);
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var copy = new UtilityAction[actions.Count];
            for (int i = 0; i < actions.Count; i++)
            {
                var action = actions[i];
                if (action == null || string.IsNullOrWhiteSpace(action.Id))
                    return AiResult<UtilitySelector>.Fail(AiFailure.InvalidAction);
                if (!ids.Add(action.Id)) return AiResult<UtilitySelector>.Fail(AiFailure.DuplicateAction);
                if (!AiNumber.Weight(action.BaseWeight)) return AiResult<UtilitySelector>.Fail(AiFailure.InvalidWeight);
                if (action.Considerations == null) return AiResult<UtilitySelector>.Fail(AiFailure.InvalidConsideration);
                foreach (var c in action.Considerations)
                {
                    if (c == null || string.IsNullOrWhiteSpace(c.Id) || c.Input == null)
                        return AiResult<UtilitySelector>.Fail(AiFailure.InvalidConsideration);
                    if (c.Curve == null || !c.Curve.IsValid) return AiResult<UtilitySelector>.Fail(AiFailure.InvalidCurve);
                }
                copy[i] = action;
            }
            return AiResult<UtilitySelector>.Ok(new UtilitySelector(world, agentId, copy, settings, random));
        }

        public AiResult<UtilitySelection> Select(long tick)
        {
            if (lastRequestedTick.HasValue && tick < lastRequestedTick.Value)
                return AiResult<UtilitySelection>.Fail(AiFailure.InvalidTick);
            // Unsigned difference handles the full signed tick range without overflow.
            if (previous != null && unchecked((ulong)tick - (ulong)previous.EvaluatedAtTick)
                < (ulong)settings.ReevaluationIntervalTicks)
            {
                lastRequestedTick = tick;
                return AiResult<UtilitySelection>.Ok(new UtilitySelection(previous.ActionId,
                    previous.EvaluatedAtTick, false, previous.Scores));
            }
            var agent = world.Get(agentId).Value;
            var board = world.GetBlackboard(agent.Side).Value;
            var context = new UtilityContext(world, agent, board, tick);
            var scores = new List<ActionScore>();
            var best = new List<string>();
            double highest = 0;
            foreach (var action in actions)
            {
                double product = 1;
                var values = new List<ConsiderationScore>();
                foreach (var c in action.Considerations)
                {
                    double input;
                    try { input = c.Input(context); }
                    catch (Exception) { return AiResult<UtilitySelection>.Fail(AiFailure.InvalidInput); }
                    var curved = c.Curve.Evaluate(input);
                    if (!curved.Success) return AiResult<UtilitySelection>.Fail(curved.Failure);
                    values.Add(new ConsiderationScore(c.Id, input, curved.Value));
                    product *= curved.Value;
                }
                double intention = board.GetIntention(action.Id).Value;
                double weighted = product == 0 || action.BaseWeight == 0 || intention == 0
                    ? 0 : product * action.BaseWeight * intention;
                double bonus = weighted > 0 && action.Id == agent.CurrentAction ? settings.PersistenceBonus : 0;
                double score = weighted + bonus;
                if (!AiNumber.Finite(score)) return AiResult<UtilitySelection>.Fail(AiFailure.InvalidWeight);
                scores.Add(new ActionScore(action.Id, values, product, action.BaseWeight, intention, bonus, score));
                if (score <= 0 || score < highest) continue;
                if (score > highest) { best.Clear(); highest = score; }
                best.Add(action.Id);
            }
            string? selected = best.Count == 0 ? null : best.Count == 1 ? best[0]
                : best[random.NextInt(0, best.Count).Value];
            previous = new UtilitySelection(selected, tick, true, scores.AsReadOnly());
            lastRequestedTick = tick;
            return AiResult<UtilitySelection>.Ok(previous);
        }
    }
}
