namespace BehindTheCurtain.Core
{
    public enum TacticId
    {
        StandardFormation,
        SpreadFormation,
        GuardHealer,
        ShieldAdvance
    }

    public enum TacticWeakness
    {
        None,
        IsolatedPuppets,
        ExposedConnectionPoint,
        OpenFlank
    }

    /// <summary>Immutable tactic data; preparation cost is additional integer ticks.</summary>
    public readonly struct Tactic
    {
        public const int RequiredConnectionPointCount = 3;

        internal Tactic(TacticId id, TacticWeakness weakness, long preparationTickCost)
        {
            Id = id;
            Weakness = weakness;
            PreparationTickCost = preparationTickCost;
        }

        public TacticId Id { get; }
        public TacticWeakness Weakness { get; }
        public long PreparationTickCost { get; }

        /// <summary>Provisional, deterministic layouts; profiles influence tactic selection, not counter rules.</summary>
        public bool TryCreatePlan(out JointAttackPlan? plan)
        {
            ConnectionPoint[] points;
            switch (Id)
            {
                case TacticId.StandardFormation:
                    points = new[] {
                        new ConnectionPoint(ConnectionPointKind.Thread, ConnectionPointPosition.Front),
                        new ConnectionPoint(ConnectionPointKind.Anchor, ConnectionPointPosition.Left),
                        new ConnectionPoint(ConnectionPointKind.Signal, ConnectionPointPosition.Right) };
                    break;
                case TacticId.SpreadFormation:
                    points = new[] {
                        new ConnectionPoint(ConnectionPointKind.Thread, ConnectionPointPosition.Left),
                        new ConnectionPoint(ConnectionPointKind.Anchor, ConnectionPointPosition.Right),
                        new ConnectionPoint(ConnectionPointKind.Signal, ConnectionPointPosition.Rear) };
                    break;
                case TacticId.GuardHealer:
                    points = new[] {
                        new ConnectionPoint(ConnectionPointKind.Thread, ConnectionPointPosition.Rear),
                        new ConnectionPoint(ConnectionPointKind.Anchor, ConnectionPointPosition.Front),
                        new ConnectionPoint(ConnectionPointKind.Signal, ConnectionPointPosition.Left) };
                    break;
                case TacticId.ShieldAdvance:
                    points = new[] {
                        new ConnectionPoint(ConnectionPointKind.Thread, ConnectionPointPosition.Front),
                        new ConnectionPoint(ConnectionPointKind.Anchor, ConnectionPointPosition.Rear),
                        new ConnectionPoint(ConnectionPointKind.Signal, ConnectionPointPosition.Right) };
                    break;
                default: plan = null; return false;
            }
            return JointAttackPlan.TryCreate(this, JointAttackPlan.DefaultTelegraphTicks,
                JointAttackPlan.DefaultCounterWindowTicks, points, new FairnessRules(maxRequiredPointsPerAttack: RequiredConnectionPointCount), out plan);
        }
    }
}
