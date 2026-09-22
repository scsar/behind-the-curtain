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
                    points = new[] { new ConnectionPoint(ConnectionPointKind.Thread, ConnectionPointPosition.Front) };
                    break;
                case TacticId.SpreadFormation:
                    points = new[] {
                        new ConnectionPoint(ConnectionPointKind.Thread, ConnectionPointPosition.Left),
                        new ConnectionPoint(ConnectionPointKind.Anchor, ConnectionPointPosition.Right) };
                    break;
                case TacticId.GuardHealer:
                    points = new[] {
                        new ConnectionPoint(ConnectionPointKind.Thread, ConnectionPointPosition.Rear),
                        new ConnectionPoint(ConnectionPointKind.Signal, ConnectionPointPosition.Left) };
                    break;
                case TacticId.ShieldAdvance:
                    points = new[] { new ConnectionPoint(ConnectionPointKind.Thread, ConnectionPointPosition.Right) };
                    break;
                default: plan = null; return false;
            }
            return JointAttackPlan.TryCreate(this, JointAttackPlan.DefaultTelegraphTicks,
                JointAttackPlan.DefaultCounterWindowTicks, points, new FairnessRules(), out plan);
        }
    }
}
