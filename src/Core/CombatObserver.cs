namespace BehindTheCurtain.Core
{
    /// <summary>Observes one encounter using constant memory and no external clock.</summary>
    public sealed class CombatObserver
    {
        private readonly ObserverSettings settings;
        private long total;
        private long area;
        private long healer;
        private long rangedStreak;
        private long sustainedRanged;
        private long lastTick;
        private bool ended;
        private BehaviorProfile profile;

        /// <summary>Immutable snapshot; reading never ends or changes observation.</summary>
        public BehaviorProfile CurrentProfile => new BehaviorProfile(total, area, healer, sustainedRanged);

        private CombatObserver(ObserverSettings settings) => this.settings = settings;

        /// <summary>Rejects null settings and nonpositive thresholds without throwing.</summary>
        public static bool TryCreate(ObserverSettings? settings, out CombatObserver? observer)
        {
            observer = null;
            if (settings == null || !settings.IsValid)
                return false;
            observer = new CombatObserver(settings);
            return true;
        }

        /// <summary>
        /// Accepts nondecreasing signed ticks; same-tick attacks follow input order.
        /// Time gaps do not break streaks. Invalid events, backwards ticks, capacity overflow,
        /// and events after EndEncounter return false without changing observations.
        /// </summary>
        public bool Observe(AttackEvent attack)
        {
            if (ended || !attack.IsValid || total == long.MaxValue
                || (total > 0 && attack.Tick < lastTick))
                return false;

            total++;
            lastTick = attack.Tick;
            if (attack.AreaHitCount >= settings.MinimumAreaHitCount)
                area++;
            if (attack.PrimaryTargetRole == TargetRole.Healer)
                healer++;

            if (attack.Range == AttackRange.Melee)
                rangedStreak = 0;
            else
            {
                rangedStreak++;
                // Crossing the threshold includes the entire prefix, exactly once.
                if (rangedStreak == settings.SustainedRangedStreak)
                    sustainedRanged += rangedStreak;
                else if (rangedStreak > settings.SustainedRangedStreak)
                    sustainedRanged++;
            }
            return true;
        }

        /// <summary>
        /// Ends observation and returns the same profile on repeated calls.
        /// Each numerator counts a subset of accepted attacks, so division yields [0, 1].
        /// </summary>
        public BehaviorProfile EndEncounter()
        {
            if (!ended)
            {
                profile = new BehaviorProfile(total, area, healer, sustainedRanged);
                ended = true;
            }
            return profile;
        }
    }
}
