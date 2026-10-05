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
        string? puppetScenario = null;
        string stress = "none";
        string loadout = "presets";
        bool cast = false;
        var seed = DefaultSeed;
        var encounters = DefaultEncounters;
        var decay = ExpeditionSettings.DefaultDecay;
        var threshold = TacticSettings.DefaultThreshold;
        double? areaThreshold = null, healerThreshold = null, rangedThreshold = null;
        PlayerStrategy[]? strategies = null;
        var seen = new HashSet<string>();
        for (var i = 0; i < args.Length; i += 2)
        {
            var option = args[i];
            if (option == "--cast")
            {
                if (!seen.Add(option)) return ArgumentError(error, "Duplicate argument: --cast");
                cast = true; i--; continue;
            }
            if (option is not ("--seed" or "--encounters" or "--decay" or "--threshold"
                or "--area-threshold" or "--healer-threshold" or "--ranged-threshold" or "--strategies" or "--puppets" or "--stress" or "--loadout"))
                return ArgumentError(error, $"Unknown argument: {option}");
            if (!seen.Add(option)) return ArgumentError(error, $"Duplicate argument: {option}");
            if (i + 1 == args.Length) return ArgumentError(error, $"Missing value for {option}");
            var input = args[i + 1];
            if (option == "--loadout")
            {
                if (input is not ("presets" or "mixed" or "noprotect")) return ArgumentError(error, "Expected presets|mixed|noprotect");
                loadout = input;
            }
            else if (option == "--stress")
            {
                if (input is not ("none" or "tension" or "mixed")) return ArgumentError(error, "Expected none|tension|mixed");
                stress = input;
            }
            else if (option == "--puppets")
            {
                if (!PuppetSimulation.Names.Contains(input)) return ArgumentError(error, "Expected approach|circle|retreat|healer|standstill|orbit|passby|duo|duo-swap");
                puppetScenario = input;
            }
            else if (option is "--seed" or "--encounters")
            {
                if (!int.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
                    || (option == "--encounters" && value <= 0))
                    return ArgumentError(error, $"{option} requires {(option == "--seed" ? "an int" : "a positive int")}: {input}");
                if (option == "--seed") seed = value;
                else encounters = value;
            }
            else if (option == "--strategies")
            {
                var names = input.Split(',');
                strategies = new PlayerStrategy[names.Length];
                for (var j = 0; j < names.Length; j++)
                {
                    switch (names[j].Trim())
                    {
                        case "AreaFirst": strategies[j] = PlayerStrategy.AreaFirst; break;
                        case "HealerFirst": strategies[j] = PlayerStrategy.HealerFirst; break;
                        default: return ArgumentError(error, $"Unknown strategy '{names[j]}'; expected AreaFirst or HealerFirst.");
                    }
                }
            }
            else
            {
                if (!double.TryParse(input, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
                    || !double.IsFinite(value) || value < 0
                    || (option == "--decay" ? value >= 1 : value > 1))
                    return ArgumentError(error, $"{option} requires an invariant real number in {(option == "--decay" ? "[0, 1)" : "[0, 1]")}: {input}");
                switch (option)
                {
                    case "--decay": decay = value; break;
                    case "--threshold": threshold = value; break;
                    case "--area-threshold": areaThreshold = value; break;
                    case "--healer-threshold": healerThreshold = value; break;
                    case "--ranged-threshold": rangedThreshold = value; break;
                }
            }
        }

        if (cast && puppetScenario == null) return ArgumentError(error, "--cast requires --puppets");
        if (seen.Contains("--loadout") && puppetScenario == null) return ArgumentError(error, "--loadout requires --puppets");
        if (seen.Contains("--stress") && puppetScenario == null) return ArgumentError(error, "--stress requires --puppets");
        if (puppetScenario != null)
        {
            PuppetSimulation.Print(PuppetSimulation.Run(puppetScenario, unchecked((ulong)seed), null, null, null, stress, loadout: loadout, cast: cast), output);
            return 0;
        }
        var random = new Random(seed);
        var tacticSettings = new TacticSettings(areaThreshold ?? threshold,
            healerThreshold ?? threshold, rangedThreshold ?? threshold);
        if (!RuleTacticSelector.TryCreate(tacticSettings, out var selector)
            || !Expedition.TryCreate(new ExpeditionSettings(decay), selector, out var expedition))
            return Fail(error);

        output.WriteLine(FormattableString.Invariant(
            $"Settings Seed={seed} Encounters={encounters} Decay={decay} AreaThreshold={tacticSettings.AreaDependencyThreshold} HealerThreshold={tacticSettings.HealerFocusThreshold} RangedThreshold={tacticSettings.SustainedRangedThreshold} Strategies={(strategies == null ? "Alternating(AreaFirst,HealerFirst)" : string.Join(",", strategies))}"));
        long tick = 0;
        for (var index = 0; index < encounters; index++)
        {
            var strategy = strategies == null
                ? (index % 2 == 0 ? PlayerStrategy.AreaFirst : PlayerStrategy.HealerFirst)
                : strategies[Math.Min(index, strategies.Length - 1)];
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
            if (!AttackSchedule.TryCreate(scheduled, new FairnessRules(maxRequiredPointsPerAttack: Tactic.RequiredConnectionPointCount), out var schedule, out _))
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

            var observation = observer!.EndEncounterWithIndividuals();
            var profile = observation.PartyProfile;
            if (!expedition.EndEncounterWithIndividuals(observation)) return Fail(error);
            var memory = expedition.Memory!.Value;
            var nextTactic = selector!.Select(memory);
            output.WriteLine(FormattableString.Invariant(
                $"Encounter {index + 1} [{strategy}] Stage={tactic.Id} Intent approach={expedition.PreviousIntentions[ActionIds.Approach]:F2} intercept={expedition.PreviousIntentions[ActionIds.Intercept]:F2} spread={expedition.PreviousIntentions[ActionIds.Spread]:F2} hold={expedition.PreviousIntentions[ActionIds.Hold]:F2} HealerThreatStrength={expedition.EncounterWorld!.GetBlackboard(Side.Puppets).Value.GetFact<double>(TargetFactIds.HealerThreatStrength).Value:F2} Area={profile.AreaDependency:F2} Healer={profile.HealerFocus:F2} Ranged={profile.SustainedRangedRatio:F2} NextTactic={nextTactic.Id} Counter={successes}/{JointAttacksPerEncounter} Memory Area={memory.AreaDependency:F2} Healer={memory.HealerFocus:F2} Ranged={memory.SustainedRangedRatio:F2}"));
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
                attacks[i].Range, startTick + i, new AgentId(1), true);
        return attacks;
    }

    private static int ArgumentError(TextWriter error, string reason)
    {
        error.WriteLine(reason);
        error.WriteLine("Puppets: Core.Sim --puppets <approach|circle|retreat|healer|standstill|orbit|passby|duo|duo-swap> [--loadout <presets|mixed>] [--stress <none|tension|mixed>] [--seed <int>]");
        error.WriteLine("Usage: Core.Sim [--seed <int>] [--encounters <positive int>] [--decay <0..1 exclusive>] [--threshold <0..1>] [--area-threshold <0..1>] [--healer-threshold <0..1>] [--ranged-threshold <0..1>] [--strategies <AreaFirst,HealerFirst,...>]");
        return 2;
    }

    private static int Fail(TextWriter error)
    {
        error.WriteLine("Simulation rejected an internal configuration or state transition.");
        return 2;
    }
}
