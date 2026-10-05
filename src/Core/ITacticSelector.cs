namespace BehindTheCurtain.Core
{
    public interface ITacticSelector
    {
        Tactic Select(BehaviorProfile profile);
        System.Collections.Generic.IReadOnlyDictionary<TacticId, double> GetStrengths(BehaviorProfile profile);
    }
}
