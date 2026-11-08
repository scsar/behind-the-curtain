# Temperament simulation report

All 18 runs: seed=1, stress=none, loadout=mixed, cast=true, default settings,
0..300 ticks (10 seconds). Same loadout and casting for every row.

Reproduce each row from the repository root:

```powershell
dotnet run --project src/Core.Sim --no-build -- --puppets healer --loadout mixed --cast --temperament none
```

Replace healer and none with the scenario and preset in the row.
The added `temperamentMetrics` line prints measured distances and target switches.

Distances are metres, averaged over eligible agent-ticks. Intercept measures the
actual decision goal (including detours/holding) against the healer's position
before movement. Harass waiting and keep-distance measure agent-to-target distance
after movement. Target switches count consecutive valid target id changes,
excluding initial acquisition/loss. `n/a` means no eligible action samples.

**within1m uses the existing default-settings segment: every nonzero-preset value
is approximate.** It measures distance to the segment, not distance to the healer.
Forbidden tuple: maximum action switches/3s, ping-pongs, simultaneous-switch ratio,
maximum interceptors, missed attack windows. Limits: <=3, 0, <20%, <=1, 0.

| Scenario | Preset | Intercept goal to healer | Harass waiting to target | Keep distance | Target switches (agent:count) | Concentration ticks/total(ratio) | within1m (nonzero: approximate) | Forbidden tuple |
|---|---|---:|---:|---:|---|---|---|---|
| healer | none | 3.175 | 4.516 | 5.416 | 10:0,11:0,12:0,13:0 | 280/301(93.0%) | 100.0% | 2/0/0.0%/1/0 |
| healer | bold | 2.763 | 4.241 | 3.676 | 10:0,11:0,12:0,13:0 | 280/301(93.0%) | 100.0% | 3/0/0.0%/1/0 |
| healer | cautious | 4.102 | 4.802 | 6.552 | 10:0,11:0,12:0,13:0 | 271/301(90.0%) | 94.4% | 2/0/0.0%/1/0 |
| healer | persistent | 3.175 | 4.516 | 5.416 | 10:0,11:0,12:0,13:0 | 280/301(93.0%) | 100.0% | 2/0/0.0%/1/0 |
| healer | whimsical | 3.175 | 4.516 | 5.416 | 10:0,11:0,12:0,13:0 | 280/301(93.0%) | 100.0% | 2/0/0.0%/1/0 |
| healer | mixed | 3.148 | 4.516 | 5.416 | 10:0,11:0,12:0,13:0 | 280/301(93.0%) | 100.0% | 2/0/0.0%/1/0 |
| duo | none | 1.397 | 5.252 | 7.001 | 10:0,11:0,12:1,13:0 | 46/301(15.3%) | 97.4% | 1/0/0.0%/1/0 |
| duo | bold | 1.815 | 5.025 | n/a | 10:0,11:0,12:1,13:0 | 46/301(15.3%) | 97.4% | 1/0/0.0%/1/0 |
| duo | cautious | 0.970 | 5.484 | 7.308 | 10:0,11:0,12:1,13:1 | 46/301(15.3%) | 97.0% | 1/0/0.0%/1/0 |
| duo | persistent | 1.397 | 5.252 | 7.001 | 10:0,11:0,12:1,13:0 | 46/301(15.3%) | 97.4% | 1/0/0.0%/1/0 |
| duo | whimsical | 1.397 | 5.224 | 7.001 | 10:0,11:0,12:1,13:0 | 32/301(10.6%) | 97.4% | 1/0/0.0%/1/0 |
| duo | mixed | 1.815 | 5.224 | 7.001 | 10:0,11:0,12:1,13:0 | 32/301(10.6%) | 97.4% | 1/0/0.0%/1/0 |
| duo-swap | none | 1.439 | 5.292 | 6.842 | 10:0,11:1,12:2,13:1 | 46/301(15.3%) | 97.4% | 1/0/0.0%/1/0 |
| duo-swap | bold | 1.878 | 5.423 | n/a | 10:0,11:1,12:1,13:0 | 46/301(15.3%) | 97.4% | 1/0/0.0%/1/0 |
| duo-swap | cautious | 1.039 | 5.523 | 7.347 | 10:0,11:1,12:2,13:2 | 46/301(15.3%) | 97.0% | 1/0/11.1%/1/0 |
| duo-swap | persistent | 1.439 | 5.292 | 6.842 | 10:0,11:1,12:2,13:1 | 46/301(15.3%) | 97.4% | 1/0/0.0%/1/0 |
| duo-swap | whimsical | 1.439 | 5.377 | 6.842 | 10:0,11:1,12:2,13:1 | 32/301(10.6%) | 97.4% | 1/0/0.0%/1/0 |
| duo-swap | mixed | 1.878 | 5.428 | 6.842 | 10:0,11:1,12:2,13:1 | 32/301(10.6%) | 97.4% | 1/0/0.0%/1/0 |

The guard in duo moves its intercept goal farther from the healer when bold
(1.815 versus cautious 0.970). The ranged attacker waits closer when bold
(5.025 versus cautious 5.484). Whimsical reduces duo concentration from 15.3%
to 10.6%. Persistent matches baseline in these particular inputs; the formula
boundary tests establish the changed hold settings, not a claim of more switches
in every scenario. Single-human healer inputs cannot expose target preference.
Aggregate intercept distances in healer need not be monotonic: bold also changes
the moving healer's keep-distance band and the segment projection/holding path.
Bold duo has no keep-distance samples: its shortened KeepMax can prevent initial
eligibility under the existing action rule. It is reported as n/a, not zero.

## Unchanged contracts27 failures

| Preset | Passing combinations | Failing combinations | Existing behavior |
|---|---:|---|---|
| bold | 24/27 | none/orbit: 6; tension/orbit: 6; mixed/orbit: 5 | agent 13 spread/hold at 6m threat boundary; mixed includes waver |
| cautious | 27/27 | none | - |
| persistent | 27/27 | none | - |
| whimsical | 27/27 | none | - |
| mixed | 26/27 | tension/healer: 4 | agent 11 intercept/waver |

Bold/none/orbit action window: 127 spread>hold, 137 hold>spread,
167 spread>hold, 177 hold>spread, 187 spread>hold, 197 hold>spread, all at 6m.
The test `ExistingDistanceSettingsReproduceBoldOscillationWithoutTemperament`
recreates identical hashes in all three stress modes using only existing distance
configuration and zero temperament. This isolates the existing selection behavior
from the new temperament application code.

Mixed/tension/healer action window: 132 intercept>waver (1.639m),
142 waver>intercept (1.907m), 162 intercept>waver (1.101m),
202 waver>intercept (1.937m). This is the existing low-tension utility competition,
not a new pre-switch waver. Persistence is irrelevant with one human.
No thresholds, hash expectations, selection rules or waver rules were changed.
The failing contracts tests remain visible; the complete suite is not green.
