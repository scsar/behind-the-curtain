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

## Puppet behavior contracts

```sh
dotnet run --project src/Core.Sim -- --puppets approach --seed 1
dotnet run --project src/Core.Sim -- --puppets circle --seed 1
dotnet run --project src/Core.Sim -- --puppets retreat --seed 1
dotnet run --project src/Core.Sim -- --puppets healer --seed 1
dotnet test tests/Core.Tests --no-restore -m:1
```

These scenarios use `PuppetBrain` and an Expedition-created world with two Guards,
one Attacker and one Healer. `healer` first records an actual healer-focus observation
and starts the next encounter. Other paths start with StandardFormation.
Output samples ticks 0 through 300 every 15 ticks (30 ticks/second).
The initial action assignment is not an action *change*.

Paths and tolerances are provisional values in `PuppetScenarioSettings`: approach
travels from z=12 to z=3 at 5m/s, retreat reverses that path, circle has radius 4m
and angular speed 0.5 radians/second, and healer follows the first half of that
circle to z=-4 before stopping. The simulation arena half-size is 30m so the
12m paths and distance keeping are not clipped by the prototype's 10m arena.

Metrics are measured every tick, not from the printed samples:

- `flee`: non-distance-keeping agent ticks moving away from a human within 6m,
  measured against the same player position before/after movement. Spread allows
  the documented 0.3m distance increase. Player movement alone does not count.
- `switches`: total changes per agent; `maxSwitches/3s`: maximum in any sliding
  90-tick window; `pingPong`: approach/hold or approach/keepDistance round trips.
- `simultaneous`: ticks with at least two changes divided by ticks with any change.
- `maxIntercept`: simultaneous interceptors; `intercept`: intercept agent ticks
  divided by all puppet agent ticks (the 20% default limit excludes GuardHealer).
- `residence`: longest continuous tick count with a human in any puppet's attack
  range; `attackTicks`: ticks with an in-range attack and valid target;
  `missedAttack`: ticks beyond 30 consecutive in-range ticks with no attack.

`PuppetContractTests` applies always-forbidden checks to all four paths both with
defaults and with a table-only custom response (`Attacker:keepDistance = 4`).
It also checks that this Attacker never stays outside the 5–8m band for more than
30 consecutive ticks, including initial acquisition, and that it actually retreats.

### Tuning and compatibility

`PuppetSettings.RoleWeights` is an immutable role/action table, optionally overridden
at construction. Every role has all six candidates. `IntentionSettings.overrides`
accepts tactic rows containing `Guard:attack`, `Attacker:keepDistance`, etc. A role
key wins over the legacy action-only key, including an explicit zero. A positive
role key enables a default-zero action using unit base weight, then applies its
multiplier and the agent's fixed jitter. Existing action-only tactic multipliers
are unchanged. Diagnostics show the effective candidate base weight; disabled
default-zero actions have a zero eligibility consideration.

New provisional Core defaults: 30-tick reevaluation, ordinal × interval / count
phase offsets after the initial assignment, ±15% fixed per-agent/action jitter,
6m/s keepDistance movement (versus 3m/s ordinary movement), 2m Guard rear offset,
0.05m attack arrival margin and 0.02 passive intercept scale. Faster distance
keeping lets the 5m/s approach path satisfy the one-second recovery contract.
Formation hold uses the positions captured when the brain is created; callers
still place the front Guards and rear Healer. `DistanceScale` and
`InterceptPositionFloor` remain accepted for source compatibility with T15 but
no longer shape engage/intercept scores.
