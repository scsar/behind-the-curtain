# Core.Sim

`contracts27`은 각 설정으로 `PuppetSimulation.Names`의 9개 시나리오 ×
stress `none`, `tension`, `mixed`를 시드 1, 기본 장비, cast 없이 실행한 계약 실패 실행 수다.
`TensionTests.StressContractsAndDeterminism`과 같이 진동 > 3, 왕복 > 0,
군체 동기화 >= 0.2, 겹치기 > 1, 무반응 > 0 중 하나라도 위반하면 1회로 센다.
기존 15회 집계와 분리하며 정렬에는 사용하지 않는다. 표 아래 `rank N 27조합:` 줄은
실패 실행별 이름과 위반 기준·값을 표시한다. `rank N 진동` 줄은 15회와 27조합의
진동 실패마다 최대 창의 개체, tick, 이전>다음 행동, 대상 거리를 표시한다.
15회 실행은 `15회 none/시나리오 seed N`으로 구분한다. 동률이면 먼저 기록한 창을 쓴다.

진동은 판단 주기와 무관하게 3초 창 `(tick - 3 * TicksPerSecond, tick]` 안의
공격 거리 경계 전환을 제외한 행동 전환이 3회를 넘는 경우다.
attack 진입 시 새 대상 거리가 공격 거리 이하이거나, attack 이탈 시 직전 tick의
대상 거리가 공격 거리 초과이면 경계 전환이다. 두 거리 모두 전환 tick의 현재 위치로
측정하며 duo는 해당 대상 플레이어, 그 외는 player를 사용한다.
원래 `switches=` 기록, 왕복, 군체 동기화 및 Groggy 뒤 재개 처리는 유지한다.

## 수치 탐색

```sh
dotnet run --project src/Core.Sim -- --sweep
dotnet run --project src/Core.Sim -- --sweep PuppetSettings.reevaluationIntervalTicks=10,15,20,30 BasicActionSettings.harassIntervalTicks=45,90 BasicActionSettings.harassRecoverTicks=15,30
```

`--sweep`는 첫 인자로 지정한다. 공개 생성자의 숫자 매개변수를
`Class.name=value,value`로 지정하면 모든 조합을 실행한다. 이름은 U7과 같으며
`arenaHalfSize`, `ticksPerSecond`는 제외한다. 정수는 `Math.Round`로 반올림한다.
기본값만 실행하는 빈 탐색도 지원한다. `SweepSettings.SweepMaxCombinations`는 기본 64이며
한도를 넘거나 인자 형식이 잘못되면 실행 전 종료 코드 2를 반환한다.
알 수 없는 이름, 숫자가 아닌 값, Core가 거부한 설정은 해당 행에 `rejected`와 이유를 표시한다.

각 조합은 approach, healer, standstill, duo/mixed/cast, duo-swap/mixed/cast를
시드 1~3으로 실행한다. 금지 패턴과 끊김, 행동 지속, 견제 사이클, 공격 체류는 이 15회에서 집계한다.
orbit 3회는 반응 지연 (iii)와 orbit gap에만 사용한다. 금지 패턴 합계는 기존 계약의
도망 tick, ping-pong 횟수, 과도한 행동 전환·군체 동기화·복수 막아서기 계약 실패,
무반응 tick, 위치 떨림 tick, 비활동 tick, 포위 자리 충돌 횟수를 합한다.
서로 단위가 다른 기존 실패 지표를 합하므로 0인지 여부를 우선해서 비교한다.
표 아래에는 합계가 0이 아닌 행의 패턴별 실패를 표시한다. 진동·군체 동기화·겹치기는
실패한 실행 수이며, 나머지는 기존 합계와 같은 단위로 15회를 더한다.

끊김은 이동 → 6~45 tick 정지 → 이동 중, 정지 전체가 Active 상태의
approach/flank/surround/pressure 또는 harass Waiting/Withdraw였을 때만 센다.
이동 임계값은 tick당 0.01m다. 열린 정지 구간은 세지 않는다.
반응 지연은 대상 교대, 플레이어 정지 후 1초간 행동이 안정되는 시작 tick,
orbit에서 intercept가 힐러(id 13)와 플레이어를 잇는 선분을 벗어났다 돌아오는 사건을 잰다.
intercept frame만 관측하며, 선분에서 1m 이내에 한 번 들어온 뒤 1m 밖으로 나간 tick부터
다시 1m 이내로 돌아온 tick까지를 이탈로 센다. 종료까지 복귀하지 않으면 마지막 frame까지 센다.
(iii)는 세 실행의 모든 이탈을 합쳐 평균한다. 이탈이 없으면 평균에 0을 넣고
`0(벗어남 없음)`으로 표시한다. `orbit gap(m)`은 같은 세 실행의
`InterceptDistanceSum / InterceptSettledTicks`를 실행별로 구해 평균한다.
대상 교대 지연은 대상이 달라진 비힐러 개체의 평균을 먼저 구한 뒤 시드 평균을 낸다.
대상 교대가 없는 시드는 평균에서 제외한다. 전 시드에서 교대가 없거나,
정지 후 안정화를 시나리오 종료까지 관측하지 못하면 `n/a`를 표시한다.
세 사건 중 `n/a`가 있으면 전체 평균도 `n/a`이며 동률 정렬에서 마지막으로 보낸다.
행동 지속은 첫·마지막 구간도 포함하여 15회의 모든 개체 구간을 가중 평균한다.

`harass cycle`은 완결된 찌르기 중 (대기 + 제한) / 찌르기의 백분율이다(없으면 `n/a`).
찌르기는 행동 attack, HarassPhase Recover인 frame이다. Withdraw 마지막 frame 다음 frame을
대기(Waiting), 제한(Strike 또는 다음 찌르기), 일반 공격(attack, phase 없음), 기타로 나눈다.
Withdraw 진입 전에 끊기거나 다른 행동으로 바뀌면 기타다. 종료 때 미완결인 사이클은
분자와 분모 모두에서 제외한다. 표 아래에 각 행의 찌르기와 네 가지 끝의 횟수를 표시한다.
`attack dwell(tick)`은 attack frame이 연속되고 frame 사이 이동이 MovementThreshold 이하인
가장 긴 구간의 tick 수다. 각 실행의 모든 개체 중 최댓값을 구한 뒤 15회 평균한다.
끊김에서 제외하는 공격 정지를 이 열로 확인한다.

첫 행은 비교용 기본값이다. 후보는 실패 0 우선, 끊김 오름차순, 지연 평균 오름차순이며
거부 행은 마지막이다. 완전 동률이면 입력 조합 순서를 유지한다.
마지막 열은 기본값과 다른 적용값만 U7 F6 형식으로 표시한다(기본값이면 빈 문자열).
임계값은 `SweepSettings`의 임의로 정한 임시 측정 수치다. MovementThreshold는 0.01m,
PassbyReturnDistance는 이름을 유지하며 orbit 복귀 거리 1.0m에 사용한다.
`BasicActionSettings.harassWithdrawMaxTicks`도 U7과 같은 이름으로 탐색할 수 있다(기본 60 tick, 임의값).

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
- `switches`: total changes per agent; `maxSwitches/3s`: maximum excluding attack
  range boundary transitions in any sliding 3-second window (90 ticks by default);
  `pingPong`: approach/hold or approach/keepDistance round trips.
- `simultaneous`: ticks with at least two changes remaining after excluding transitions into reservation actions (`intercept`, `surround`), divided by ticks with any change.
- `coordinated`: ticks with at least two changes and at least one excluded reservation-action transition, using the same denominator. Mixed ticks can count in both.
- `surroundOverlap`: missing, conflicting, or duplicate surround slot reservations (must be zero).
- `maxIntercept`: simultaneous interceptors; `intercept`: intercept agent ticks
  divided by all puppet agent ticks (the 20% default limit excludes GuardHealer).
- `residence`: longest continuous tick count with a human in any puppet's attack
  range; `attackTicks`: ticks with an in-range attack and valid target, or a harass
  action in Strike/Recover/Withdraw; `missedAttack`: ticks beyond 30 consecutive
  in-range ticks with neither response. Waiting does not qualify for the harass exception.

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

### Two-player baseline (no learning)

Run `dotnet run --project src/Core.Sim -- --puppets duo` or
`dotnet run --project src/Core.Sim -- --puppets duo-swap`.
Both add human Players A (id 1) and B (id 2). The previous encounter contains
one observation of B attacking the healer. Core action rules are unchanged.
The original seven scenarios still contain only A.

Arbitrarily chosen prototype inputs live in `PuppetScenarioSettings`: A starts
at (0,12), walks at 5m/s to (0,4), and stops; B starts at (9,-1).
In duo-swap, at tick 150 (5 seconds at the default 30 ticks/s), both start
straight paths to exchange positions at 5m/s. The 10.296m exchange takes
about 2.059 seconds; it does not teleport them at 5 seconds.
The regression checks interception against both B's current segment and its
final destination segment by tick 210, two seconds after movement starts.

For these paths, distance-based forbidden-pattern and intercept-quality metrics
use each puppet's actual Target. Sample `targets` rows show both players and
all puppet target ids. Final `targetDistribution` reports:

- `total`: all puppet-ticks targeting a player (includes healer targets).
- `playerN`: that player's puppet-ticks and fraction of total; zero total yields 0%.
- `concentrated`: ticks where all non-healers target the same player, divided by
  ticks where at least one non-healer has a target. A missing target prevents
  concentration; healer targets do not affect this ratio.
- `interceptTargets`: intercept puppet-ticks per player id.

Metrics include ticks 0 through 300 (301 frames by default). These new scenarios
are verified with default equipment and no stress. The original seven-scenario
all-loadout regression keeps its original scope.
