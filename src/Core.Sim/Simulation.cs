using System.Globalization;
using BehindTheCurtain.Core;

namespace BehindTheCurtain.Core.Sim;

public enum PlayerStrategy { AreaFirst, HealerFirst }

public static class Simulation
{
    public const int DefaultSeed = 1;
    public const int DefaultEncounters = 3;
    public const int AttacksPerEncounter = 20;
    public const int StrategyAttacksPerEncounter = 14;
    public const int AreaHitCount = 3;
    public const int JointAttacksPerEncounter = 2;

    // Provisional simulation choice: each joint counter has a 50% in-window chance.
    private const double InWindowProbability = 0.5;
    private static readonly TargetRole[] Roles = Enum.GetValues<TargetRole>();
    private static readonly AttackRange[] Ranges = Enum.GetValues<AttackRange>();

    public static int Run(string[] args, TextWriter output, TextWriter error)
    {
        var seed = DefaultSeed;
        var encounters = DefaultEncounters;
        var seen = new HashSet<string>();
        for (var i = 0; i < args.Length; i += 2)
        {
            if ((args[i] != "--seed" && args[i] != "--encounters")
                || !seen.Add(args[i]) || i + 1 == args.Length
                || !int.TryParse(args[i + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
                || (args[i] == "--encounters" && value <= 0))
            {
                error.WriteLine("Usage: Core.Sim [--seed <int>] [--encounters <positive int>]");
                return 1;
            }
            if (args[i] == "--seed") seed = value;
            else encounters = value;
        }

        var random = new Random(seed);
        var selector = new RuleTacticSelector();
        if (!Expedition.TryCreate(new ExpeditionSettings(), selector, out var expedition))
            return Fail(error);

        long tick = 0;
        for (var index = 0; index < encounters; index++)
        {
            var strategy = index % 2 == 0 ? PlayerStrategy.AreaFirst : PlayerStrategy.HealerFirst;
            if (!expedition!.TryStartEncounter(out var tactic)
                || !CombatObserver.TryCreate(new ObserverSettings(), out var observer)
                || !tactic.TryCreatePlan(out var plan))
                return Fail(error);

            foreach (var attack in CreateAttacks(strategy, random, tick))
                if (!observer!.Observe(attack)) return Fail(error);
            tick += AttacksPerEncounter;

            var scheduled = new ScheduledAttackPlan[JointAttacksPerEncounter];
            for (var i = 0; i < scheduled.Length; i++)
            {
                scheduled[i] = new ScheduledAttackPlan(plan!, tick);
                tick += plan!.TelegraphTicks + plan.CounterWindowTicks;
            }
            if (!AttackSchedule.TryCreate(scheduled, new FairnessRules(), out var schedule, out _))
                return Fail(error);

            var successes = 0;
            foreach (var attack in schedule!.Attacks)
            {
                var inside = random.NextDouble() < InWindowProbability;
                var attemptTick = inside
                    ? random.NextInt64(attack.CounterWindow.Start, attack.CounterWindow.End + 1)
                    : (random.Next(2) == 0 ? attack.CounterWindow.Start - 1 : attack.CounterWindow.End + 1);
                // One coordinated attempt per joint attack; use each point's valid method.
                foreach (var point in attack.ConnectionPoints)
                    CounterResolver.Resolve(attack, attemptTick, point.AllowedMethods, point);
                if (attack.State == JointAttackState.Broken) successes++;
                else attack.Advance(attack.ExecutionTick);
            }

            var profile = observer!.EndEncounter();
            if (!expedition.EndEncounter(profile)) return Fail(error);
            var nextTactic = selector.Select(expedition.Memory!.Value);
            output.WriteLine(FormattableString.Invariant(
                $"Encounter {index + 1} [{strategy}] Area={profile.AreaDependency:F2} Healer={profile.HealerFocus:F2} Ranged={profile.SustainedRangedRatio:F2} NextTactic={nextTactic.Id} Counter={successes}/{JointAttacksPerEncounter}"));
        }
        expedition!.EndExpedition();
        return 0;
    }

    private static AttackEvent[] CreateAttacks(PlayerStrategy strategy, Random random, long startTick)
    {
        var attacks = new AttackEvent[AttacksPerEncounter];
        for (var i = 0; i < attacks.Length; i++)
        {
            var preferred = i < StrategyAttacksPerEncounter;
            // Provisional choices: area attacks primarily target Guard; random attacks are single-target.
            var role = preferred ? (strategy == PlayerStrategy.HealerFirst ? TargetRole.Healer : TargetRole.Guard)
                : Roles[random.Next(Roles.Length)];
            var range = preferred ? (strategy == PlayerStrategy.AreaFirst ? AttackRange.Melee : AttackRange.Ranged)
                : Ranges[random.Next(Ranges.Length)];
            attacks[i] = new AttackEvent(preferred && strategy == PlayerStrategy.AreaFirst ? AreaHitCount : 0,
                role, range, 0);
        }
        random.Shuffle(attacks);
        for (var i = 0; i < attacks.Length; i++)
            attacks[i] = new AttackEvent(attacks[i].AreaHitCount, attacks[i].PrimaryTargetRole,
                attacks[i].Range, startTick + i);
        return attacks;
    }

    private static int Fail(TextWriter error)
    {
        error.WriteLine("Simulation rejected an internal configuration or state transition.");
        return 2;
    }
}
