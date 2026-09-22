namespace BehindTheCurtain.Core
{
    public enum TargetRole
    {
        Healer,
        Guard,
        Attacker
    }

    public enum AttackRange
    {
        Melee,
        Ranged
    }

    public readonly struct AttackEvent
    {
        /// <param name="areaHitCount">Enemies hit by the area effect; zero for non-area attacks.</param>
        /// <param name="primaryTargetRole">Role of the primary target only.</param>
        /// <param name="range">Melee or ranged attack.</param>
        /// <param name="tick">Signed integer occurrence tick.</param>
        public AttackEvent(int areaHitCount, TargetRole primaryTargetRole, AttackRange range, long tick)
        {
            AreaHitCount = areaHitCount;
            PrimaryTargetRole = primaryTargetRole;
            Range = range;
            Tick = tick;
        }

        public int AreaHitCount { get; }
        public TargetRole PrimaryTargetRole { get; }
        public AttackRange Range { get; }
        public long Tick { get; }

        internal bool IsValid => AreaHitCount >= 0
            && (PrimaryTargetRole == TargetRole.Healer || PrimaryTargetRole == TargetRole.Guard
                || PrimaryTargetRole == TargetRole.Attacker)
            && (Range == AttackRange.Melee || Range == AttackRange.Ranged);
    }
}
