using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace BehindTheCurtain.Core
{
    public static class TemperamentAxisIds
    {
        public const string Boldness = "boldness";
        public const string Persistence = "persistence";
    }

    /// <summary>Immutable open collection. Missing axes are zero; World validates inputs.</summary>
    public sealed class Temperament
    {
        public Temperament(IReadOnlyDictionary<string, double>? values = null)
        {
            var copy = new Dictionary<string, double>();
            if (values != null) foreach (var pair in values) copy[pair.Key] = pair.Value;
            Values = new ReadOnlyDictionary<string, double>(copy);
        }
        public IReadOnlyDictionary<string, double> Values { get; }
        public double this[string axis] => Values.TryGetValue(axis, out var value) ? value : 0;
        internal static Temperament Zero { get; } = new Temperament();
        internal bool IsZero
        {
            get { foreach (var value in Values.Values) if (value != 0) return false; return true; }
        }
        internal bool IsValid
        {
            get
            {
                foreach (var pair in Values)
                    if (string.IsNullOrWhiteSpace(pair.Key) || !AiNumber.Finite(pair.Value)
                        || pair.Value < -1 || pair.Value > 1) return false;
                return true;
            }
        }
    }

    /// <summary>Provisional, arbitrarily chosen scales; round ticks to nearest, ties to even.</summary>
    public sealed class TemperamentSettings
    {
        public TemperamentSettings(double temperamentDistanceScale = .3,
            double temperamentPersistenceScale = .5, double whimPressureWeight = 1)
        {
            TemperamentDistanceScale = temperamentDistanceScale;
            TemperamentPersistenceScale = temperamentPersistenceScale;
            WhimPressureWeight = whimPressureWeight;
        }
        public double TemperamentDistanceScale { get; }
        public double TemperamentPersistenceScale { get; }
        public double WhimPressureWeight { get; }
        internal bool IsValid => AiNumber.Weight(TemperamentDistanceScale) && TemperamentDistanceScale < 1
            && AiNumber.Weight(TemperamentPersistenceScale) && TemperamentPersistenceScale <= 1
            && AiNumber.Weight(WhimPressureWeight);
    }
}
