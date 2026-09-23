using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace BehindTheCurtain.Core
{
    public static class ActionIds
    {
        public const string Waver = "waver";
        public const string Approach = "approach", Intercept = "intercept", Spread = "spread",
            Hold = "hold", Attack = "attack", KeepDistance = "keepDistance";
    }
    public static class TargetFactIds
    {
        public const string HealerThreat = "target.healerThreat", RangedThreat = "target.rangedThreat";
    }
    public sealed class IntentionSettings
    {
        // Provisional design multipliers; tune after playtesting.
        public IntentionSettings(double spread = 2.0, double spreadHold = 0.7,
            double guardIntercept = 2.5, double shieldApproach = 2.0, double shieldHold = 0.5,
            IReadOnlyDictionary<TacticId, IReadOnlyDictionary<string, double>>? overrides = null)
        {
            var tables = new Dictionary<TacticId, IReadOnlyDictionary<string, double>>();
            foreach (TacticId id in System.Enum.GetValues(typeof(TacticId)))
            {
                var table = new Dictionary<string, double> {
                    [ActionIds.Approach] = 1, [ActionIds.Intercept] = 1, [ActionIds.Spread] = 1,
                    [ActionIds.Hold] = 1, [ActionIds.Attack] = 1 };
                if (id == TacticId.SpreadFormation) { table[ActionIds.Spread] = spread; table[ActionIds.Hold] = spreadHold; }
                if (id == TacticId.GuardHealer) table[ActionIds.Intercept] = guardIntercept;
                if (id == TacticId.ShieldAdvance) { table[ActionIds.Approach] = shieldApproach; table[ActionIds.Hold] = shieldHold; }
                if (overrides != null && overrides.TryGetValue(id, out var entries))
                    foreach (var entry in entries) table[entry.Key] = entry.Value;
                tables[id] = new ReadOnlyDictionary<string, double>(table);
            }
            Tables = new ReadOnlyDictionary<TacticId, IReadOnlyDictionary<string, double>>(tables);
        }
        public IReadOnlyDictionary<TacticId, IReadOnlyDictionary<string, double>> Tables { get; }
        internal bool IsValid
        {
            get
            {
                foreach (var table in Tables.Values)
                    foreach (var weight in table.Values) if (!AiNumber.Weight(weight)) return false;
                return true;
            }
        }
    }
}
