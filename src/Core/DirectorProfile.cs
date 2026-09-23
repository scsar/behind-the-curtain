using System;
namespace BehindTheCurtain.Core
{
    public enum TunePriority { OutOfPosition, LowestTension, Attacking }
    /// <summary>Provisional, arbitrarily chosen T16a-2 tuning values.</summary>
    public sealed class DirectorProfile
    {
        public DirectorProfile(TunePriority priority = TunePriority.OutOfPosition, double tuneBudgetPerSecond = 0.2,
            double reclaimBudgetPerString = 0.1, double humanizedReclaimRatio = 0.5,
            double tuneBudgetMaxScale = 2, double waverPull = 1, bool enableWaver = true)
        { Priority = priority; TuneBudgetPerSecond = tuneBudgetPerSecond; ReclaimBudgetPerString = reclaimBudgetPerString;
          HumanizedReclaimRatio = humanizedReclaimRatio; TuneBudgetMaxScale = tuneBudgetMaxScale; WaverPull = waverPull; EnableWaver = enableWaver; }
        public TunePriority Priority { get; }
        public double TuneBudgetPerSecond { get; }
        public double ReclaimBudgetPerString { get; }
        public double HumanizedReclaimRatio { get; }
        public double TuneBudgetMaxScale { get; }
        public double WaverPull { get; }
        public bool EnableWaver { get; }
        internal bool IsValid => Priority >= TunePriority.OutOfPosition && Priority <= TunePriority.Attacking
            && AiNumber.Weight(TuneBudgetPerSecond) && AiNumber.Weight(ReclaimBudgetPerString)
            && AiNumber.Weight(HumanizedReclaimRatio) && HumanizedReclaimRatio <= 1
            && AiNumber.Weight(TuneBudgetMaxScale) && TuneBudgetMaxScale >= 1
            && AiNumber.Finite(TuneBudgetPerSecond * TuneBudgetMaxScale) && AiNumber.Weight(WaverPull);
    }
    public sealed class DecisionCause
    {
        internal DecisionCause(PuppetStateEvent? stateEvent, string intentionKey, double intention)
        { StateEvent = stateEvent; IntentionKey = intentionKey; Intention = intention; }
        public PuppetStateEvent? StateEvent { get; }
        public string IntentionKey { get; }
        public double Intention { get; }
    }
}
