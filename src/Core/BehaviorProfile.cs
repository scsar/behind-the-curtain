using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace BehindTheCurtain.Core
{
    public enum TendencyId { AreaDependency, HealerFocus, SustainedRanged }

    /// <summary>Immutable ratios in [0, 1]. Missing tendencies, including the default profile, are zero.</summary>
    public readonly struct BehaviorProfile : IEquatable<BehaviorProfile>
    {
        private readonly IReadOnlyDictionary<TendencyId, double>? values;
        private static readonly IReadOnlyDictionary<TendencyId, double> zeros = CreateZeros();
        private static IReadOnlyDictionary<TendencyId, double> CreateZeros()
        {
            var result = new Dictionary<TendencyId, double>();
            foreach (TendencyId id in Enum.GetValues(typeof(TendencyId))) result[id] = 0;
            return new ReadOnlyDictionary<TendencyId, double>(result);
        }
        private BehaviorProfile(Dictionary<TendencyId, double> values, bool hasAttacks)
        { this.values = new ReadOnlyDictionary<TendencyId, double>(values); HasAttacks = hasAttacks; }
        /// <summary>Observation metadata; tendency value equality deliberately excludes it.</summary>
        public bool HasAttacks { get; }
        public IReadOnlyDictionary<TendencyId, double> Values => values ?? zeros;
        public double this[TendencyId id] => Values.TryGetValue(id, out var value) ? value : 0;
        public double AreaDependency => this[TendencyId.AreaDependency];
        public double HealerFocus => this[TendencyId.HealerFocus];
        public double SustainedRangedRatio => this[TendencyId.SustainedRanged];

        public bool Equals(BehaviorProfile other)
        {
            foreach (TendencyId id in Enum.GetValues(typeof(TendencyId)))
                if (!this[id].Equals(other[id])) return false;
            return true;
        }
        public override bool Equals(object? obj) => obj is BehaviorProfile other && Equals(other);
        public override int GetHashCode()
        {
            unchecked
            {
                var hash = 17;
                foreach (TendencyId id in Enum.GetValues(typeof(TendencyId))) hash = hash * 31 + this[id].GetHashCode();
                return hash;
            }
        }

        internal BehaviorProfile Scale(double ratio)
        {
            var scaled = new Dictionary<TendencyId, double>();
            foreach (var pair in Values) scaled[pair.Key] = pair.Value * ratio;
            return new BehaviorProfile(scaled, HasAttacks);
        }
        internal BehaviorProfile Merge(BehaviorProfile current, ExpeditionSettings settings)
        {
            var merged = new Dictionary<TendencyId, double>();
            foreach (TendencyId id in Enum.GetValues(typeof(TendencyId)))
            {
                var next = current.HasAttacks ? current[id] : 0;
                var weight = !current.HasAttacks ? settings.AbsentFallWeight
                    : next > this[id] ? settings.RiseWeight : settings.FallWeight;
                merged[id] = (1 - weight) * this[id] + weight * next;
            }
            return new BehaviorProfile(merged, HasAttacks || current.HasAttacks);
        }
        internal BehaviorProfile(long total, long area, long healer, long sustainedRanged)
            : this(new Dictionary<TendencyId, double> {
                [TendencyId.AreaDependency] = total == 0 ? 0 : (double)area / total,
                [TendencyId.HealerFocus] = total == 0 ? 0 : (double)healer / total,
                [TendencyId.SustainedRanged] = total == 0 ? 0 : (double)sustainedRanged / total }, total > 0) { }
    }
}
