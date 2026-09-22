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
    }
}
