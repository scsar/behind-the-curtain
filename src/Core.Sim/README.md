# Core.Sim

```sh
dotnet run --project src/Core.Sim
dotnet run --project src/Core.Sim -- --seed 42 --encounters 6
```

Defaults: seed `1`, encounters `3`. Seeds are signed 32-bit integers; encounters must
be positive. Invalid, unknown, or duplicate arguments return exit code `1`.

One expedition alternates AreaFirst (odd encounters) and HealerFirst (even encounters).
Each encounter shuffles exactly 14 preferred attacks and 6 random single-target
attacks using the supplied seed. AreaFirst hits three enemies in melee;
HealerFirst targets the healer at range. Random attacks choose role and range uniformly.
The provisional primary target for area attacks is Guard.

Each output line reports the current encounter's observed AreaDependency, HealerFocus,
and SustainedRangedRatio, followed by the next tactic selected from updated expedition
memory. The current encounter uses the tactic selected from previous encounters;
the first uses StandardFormation. The final line also reports a next-tactic preview.

Two nonoverlapping joint attacks use the current tactic's plans and preparation cost.
Each receives one coordinated counter attempt against all connection points with their
valid interference methods. Provisional timing sampling gives a 50% chance of a tick
inside the inclusive window, otherwise one tick before or after it. Success means the
whole joint attack is broken; the denominator is always two. Random sampling may
produce all successes or all failures in a short expedition.

Observer thresholds, memory decay, and tactic settings use the Core defaults.
All time is integer ticks. Each run owns its random generator and expedition memory;
numeric output uses invariant formatting with two decimal places.
