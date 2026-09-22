namespace BehindTheCurtain.Core
{
    public interface ITacticSelector
    {
        Tactic Select(BehaviorProfile profile);
    }
}
