namespace BehindTheCurtain.Core
{
    public sealed class RuleTacticSelector : ITacticSelector
    {
        private readonly TacticSettings settings;

        public RuleTacticSelector() : this(new TacticSettings()) { }

        private RuleTacticSelector(TacticSettings settings) => this.settings = settings;

        /// <summary>Rejects null settings, thresholds outside [0, 1], and negative costs.</summary>
        public static bool TryCreate(TacticSettings? settings, out RuleTacticSelector? selector)
        {
            selector = null;
            if (settings == null || !settings.IsValid)
                return false;
            selector = new RuleTacticSelector(settings);
            return true;
        }

        public Tactic Select(BehaviorProfile profile)
        {
            var selected = new Tactic(TacticId.StandardFormation, TacticWeakness.None,
                settings.StandardFormationPreparationTickCost);
            var bestMargin = double.NegativeInfinity;

            // Highest priority first; strict improvement preserves exact ties.
            Consider(profile.HealerFocus, settings.HealerFocusThreshold,
                new Tactic(TacticId.GuardHealer, TacticWeakness.ExposedConnectionPoint,
                    settings.GuardHealerPreparationTickCost), ref selected, ref bestMargin);
            Consider(profile.AreaDependency, settings.AreaDependencyThreshold,
                new Tactic(TacticId.SpreadFormation, TacticWeakness.IsolatedPuppets,
                    settings.SpreadFormationPreparationTickCost), ref selected, ref bestMargin);
            Consider(profile.SustainedRangedRatio, settings.SustainedRangedThreshold,
                new Tactic(TacticId.ShieldAdvance, TacticWeakness.OpenFlank,
                    settings.ShieldAdvancePreparationTickCost), ref selected, ref bestMargin);
            return selected;
        }

        private static void Consider(double tendency, double threshold, Tactic candidate,
            ref Tactic selected, ref double bestMargin)
        {
            var margin = tendency - threshold;
            if (tendency >= threshold && margin > bestMargin)
            {
                selected = candidate;
                bestMargin = margin;
            }
        }
    }
}
