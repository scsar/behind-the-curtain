# 다음 jevcode 실행 목록

규칙 코어의 첫 목표: **카운터로 합동 공격을 끊는 규칙**과 **적이 전술을 관찰해 대응을 바꾸는 규칙**을 Unity 없이 테스트로 고정한다(기획 문서의 "첫 플레이 검증 후보"). 공통 규칙, 용어, 테스트 실행은 `AGENTS.md`에 있으므로 명령에는 할 일과 정의만 적는다.

## 명령을 쓰는 원칙

T4 첫 시도가 "원거리 지속"을 다르게 해석해서 거절된 뒤 정한 원칙이다.

- **용어는 계산식으로 정의한다.** "~이면", "약하면", "최근일수록" 같은 말은 식이나 기준값으로 바꾼다.
- **기획 수치는 설정 객체로 주입하고 기본값을 명령에 적는다.** Codex가 임의로 정하게 두지 않는다. 기본값은 모두 임시값이다.
- **테스트는 경계를 지정한다.** 기준 바로 아래, 정확히 기준, 입력 0개, 설정 변경.
- **마지막 메시지에 계산 방식을 설명하게 한다.** 리뷰할 때 코드를 읽지 않고도 확인할 수 있도록.

**긴 명령은 `docs/runs/<id>.md` 파일로 두고 `--prompt-file`로 넘긴다(T17부터).** 파일 끝의 `## 리뷰 포인트`는 실행기에 보내지 않고 리뷰 화면의 체크리스트가 된다. 붙여 넣기 실수(cmd 줄 잘림, 따옴표 미완성, 예전 명령 재사용)를 피한다. **여러 줄 명령은 PowerShell에서 실행한다.** cmd.exe는 첫 줄바꿈 뒤를 잘라 버린다(T4 retry 두 번이 이 때문에 정의 없이 전달됨). run 시작 시 출력되는 `task: N자, 끝: ...`가 명령의 마지막 문장과 같은지 확인한다.

**리뷰 포인트 쓰는 법 (U6부터, T16b-2 리뷰에서 정함).** 체크리스트만 위에서부터 따라가면 판정할 수 있어야 한다.

- **형식:** `- [어디서] 무엇을 하면 → 무엇이 보이면 통과`. 한 항목에 확인 하나.
- **[어디서]는 다섯 가지:** `[리뷰 화면]`(공개 API, 삭제된 테스트, 기존 동작 변경, 테스트 목록, 변경 파일) / `[근거 표]`(실행기 마지막 메시지의 '검증 근거' 표) / `[시뮬레이터]`(그대로 붙여 넣을 명령을 함께 적음) / `[Play]` / `[터미널]`.
- **그 run이 끝난 시점에 확인할 수 있는 것만 넣는다.** `[Play]`는 바뀐 동작이 이미 Unity에 연결되어 화면에서 보일 때만 넣는다(U 단계, 또는 Unity가 이미 쓰는 Core 동작을 고친 run). 연결 전인 Core 기능(예: T16b-2 때의 배역 배분)에는 넣지 않는다.
- **코드를 읽어야 판단하는 항목은 넣지 않는다**("어떤 경로로도 ~하지 않는지" 등). 대신 그것을 검사하는 테스트를 명령에 요구하고, 리뷰 포인트는 "`[근거 표]` 그 테스트 이름이 있고 `[리뷰 화면]` 테스트 목록에도 있다"로 쓴다.
- **테스트(통과·실패)에는 불변식만 둔다**(T16b-3a-1, 1f에서 두 번 연속 실패한 뒤 정함): 금지 패턴, 결정론, 행동 정의의 경계(각도, 거리, 단계 tick). 실행해 보지 않은 분포 숫자(점유율 구간, 몰림 비율 감소)는 테스트가 아니라 보고로 요구하고 리뷰에서 사람이 판단한다. 행동을 바꾸는 run은 궤적 해시를 갱신하라고 명시하되, 바뀐 시나리오마다 이유와 전후 지표를 적게 한다.
- **명령 본문은 마지막 메시지에 '검증 근거' 표를 요구한다.** 요구 항목 번호마다 그것을 확인하는 테스트 이름, 출력 줄, 바꾼 파일·메서드.
- **지표가 100%, 0, 0.00처럼 극단값이면 그 지표가 실제로 무엇을 재는지 먼저 의심한다**(T16b-2f). 극단값은 바로 실패가 아니라 **다른 근거로 확인하라는 신호**다: 같은 리뷰 포인트에 순환을 배제하는 근거(대상 고정, 실제 이동 거리 등)를 함께 적고, 그 근거가 맞으면 통과다(T16b-2r의 orbit 100%가 그 예). 지표가 0인 경우를 통과로 치지 않는다. 검증할 상황이 없으면 그 상황이 생기는 시나리오를 만든다.
- **중요하지만 아직 확인할 수 없는 것**은 체크리스트가 아니라 이 목록에 "다음 단계에서 확인"으로 적는다.
- 확인하지 못한 항목은 체크하지 않고, 리뷰 메모에 이유와 대신 무엇으로 판정했는지 적는다.

## 매번의 흐름

```
jevcode run → 리뷰 화면(jevcode-ui.bat)에서 확인 → [수락하고 커밋] 또는 거절 후 되돌리기 → 다음 run
```

- **거절한 경우:** 리뷰 화면의 **"거절하고 되돌리기"**를 누른다. 이 run이 바꾼 것만 되돌린다(새 파일과 그 .meta 삭제, 수정 파일 복원). 명령을 고쳐 `--tag retry`로 다시 run한다.
- **정리 안 된 run이 있으면 다음 run이 멈춘다.** 리뷰, 커밋, 되돌리기 중 남은 것을 먼저 처리한다.
- **실패·거절 리뷰:** 원인과 "이 화면만으로 설명할 수 있었나"를 남긴다.
- **범위 밖 제안:** 리뷰 요점의 "제안"은 이 목록에 추가하거나 다음 run으로 돌린다.
- **직접 작업한 경우:** jevcode 없이 코드 작업을 했다면 "직접 사용 기록"을 누른다.

## 목록

- [x] **T1 뼈대 + TickWindow** — `39c326a`
- [x] **T2 합동 공격 수명주기** — `1b059a0`
- [x] **T3 카운터 판정** — `e8722c4`

- [x] **T4 플레이어 행동 기록** — `69551e9` (첫 시도 거절: "원거리 지속" 해석, 광역 기준 임의 설정. 아래 수정 명령으로 retry해 완료)
  ```powershell
  jevcode run --tag retry "CombatObserver 추가. 교전 중 공격 이벤트(광역 공격으로 맞힌 적 수, 주 대상 역할 Healer/Guard/Attacker, 거리 Melee/Ranged, 발생 tick)를 받아 교전 종료 시 BehaviorProfile을 만든다. 세 경향은 모두 0~1이며 정의는 다음과 같다.
  1) 광역 의존도 = 광역 효과로 적을 MinimumAreaHitCount명 이상 맞힌 공격 수 / 전체 공격 수.
  2) 회복 역할 집중도 = 주 대상이 Healer인 공격 수 / 전체 공격 수.
  3) 원거리 지속 공격 비율 = 원거리 공격이 SustainedRangedStreak회 이상 연속된 구간에 속한 공격 수 / 전체 공격 수. 근접 공격이 끼면 연속이 끊긴다.
  MinimumAreaHitCount(기본 2)와 SustainedRangedStreak(기본 3)는 ObserverSettings로 주입하는 기획 임시값이다. 공격이 0회면 세 값 모두 0.
  테스트: 각 정의별 계산, 연속 구간이 기준 바로 아래와 정확히 기준일 때, 근접이 연속을 끊는 경우, 설정값을 바꿨을 때, 모든 값이 0~1 범위인지.
  마지막 메시지에 세 값의 계산식과 0~1 보장 방식을 명시한다."
  ```
  리뷰 포인트: 세 정의가 명령과 같은지, 연속 구간 경계 테스트.

- [x] **T5 연출가 대응 선택** — `c434855`
  ```powershell
  jevcode run --tag feature "ITacticSelector와 규칙 기반 RuleTacticSelector 추가. BehaviorProfile을 받아 Tactic 하나를 고른다.
  대응: SpreadFormation(광역 의존 대응), GuardHealer(회복 역할 집중 대응), ShieldAdvance(원거리 지속 대응), 그리고 기본 대응 StandardFormation.
  선택 규칙: 각 경향이 TacticSettings의 임계값(기본 모두 0.5) 이상이면 후보가 된다. 후보가 없으면 StandardFormation. 후보가 여럿이면 (경향값 - 임계값)이 가장 큰 것을 고르고, 같으면 GuardHealer > SpreadFormation > ShieldAdvance 순서로 고른다.
  Tactic은 데이터로 Id, Weakness(IsolatedPuppets / ExposedConnectionPoint / OpenFlank / None), PreparationTickCost(합동 공격 준비에 더 드는 tick, 기본값 SpreadFormation 10, GuardHealer 5, ShieldAdvance 15, StandardFormation 0)를 가진다. 임계값과 비용은 모두 기획 임시값이다.
  테스트: 각 대응 선택, 임계값 바로 아래와 정확히 임계값, 후보 없음, 후보 여럿일 때 차이 비교, 동률일 때 우선순위, 설정값 변경.
  마지막 메시지에 선택 규칙을 표로 요약한다."
  ```
  리뷰 포인트: 동률 우선순위, 비용 값이 데이터로 분리됐는지.
  > `ITacticSelector`는 나중에 게임 안에서 Jev를 시험할 자리다. 규칙과 Jev를 같은 인터페이스로 비교할 수 있다.

- [x] **T6 원정 내 기억** — `f58f7f2`
  ```powershell
  jevcode run --tag feature "Expedition 추가. 한 원정 안에서 교전이 끝날 때마다 BehaviorProfile을 기억에 합치고, 다음 교전의 대응은 기억된 프로필로 고른다.
  합치는 식: 첫 교전은 기억 = 이번 프로필. 이후에는 각 경향마다 기억 = Decay × 기억 + (1 - Decay) × 이번 값. Decay는 ExpeditionSettings로 주입하며 기본 0.5, 허용 범위 0 이상 1 미만(범위 밖이면 생성 거부). 이 식은 0~1 값의 가중 평균이라 결과도 0~1이다.
  EndExpedition 후에는 기억을 비우고, 새 원정의 첫 교전은 기억 없이 StandardFormation으로 시작한다.
  테스트: 첫 교전, 두 번째와 세 번째 교전의 계산값, Decay 0과 0.99, 범위 밖 Decay 거부, 원정 종료 후 초기화, 원정을 넘어 기억이 남지 않음.
  마지막 메시지에 합치는 식과 예시 계산 한 줄을 적는다."
  ```
  리뷰 포인트: 첫 교전 처리, Decay 경계.

- [x] **T7 대응의 공격 구성과 공정성 불변식** — `8489ed5` (기획 확인 완료, 2026-09-22)
  > T5는 대응을 고르기만 해서, "대응이 바뀌어도 카운터 규칙은 같다"를 검사할 대상이 없다. 그래서 대응이 합동 공격 구성(전조 길이, 연결부 배치)을 정하되, 연결부별 허용 간섭 방식은 바꾸지 못하게 하는 최소 연결을 넣는다. 이 기본 규칙은 기획 확인을 마쳤다.
  ```powershell
  jevcode run --tag feature "대응이 합동 공격 구성을 정하는 JointAttackPlan 추가. Tactic마다 Plan(기준 공격 설정)을 만든다: 전조 길이, 카운터 창 길이, 노출할 ConnectionPoint 목록. 대응은 전조 길이를 PreparationTickCost만큼 늘리고, 노출 연결부의 위치와 개수를 바꿀 수 있다.
  공정성 불변식(FairnessRules, 기획 임시값): 카운터 창 길이 MinCounterWindowTicks(기본 10) 이상, 노출 연결부 1개 이상, 연결부 종류별 허용 간섭 방식(Cut/Shoot/Disrupt)은 ConnectionPointCatalog에 고정되어 대응이 바꿀 수 없다.
  CounterResolver의 판정은 공격 상태, 시도 tick, 간섭 방식, 대상 연결부만으로 정해지며 어떤 대응이 만든 공격인지와 무관해야 한다.
  테스트: 모든 대응 x 여러 프로필 x 시드 조합으로 만든 Plan이 불변식을 지키는지, 같은 카운터 시도가 대응과 무관하게 같은 판정을 받는지. 불변식을 어기는 Plan은 생성 단계에서 거부.
  규칙 코드를 바꿔야 하면 이유를 마지막 메시지에 남긴다."
  ```
  리뷰 포인트: 불변식이 실제로 대응을 바꿔 가며 검사되는지(형식적인 테스트가 아닌지), 기획 의도와 맞는지.

- [x] **T8 카운터 부분 성공** — `c5121fb`
  > 판정 규칙을 늘리지 않고 변화를 만드는 축: 연결부를 나눠 끊는 협동. T3의 "창당 1회 성공"을 바꾸는 작업이라 기존 동작 보존 조건을 건다.
  ```powershell
  jevcode run --tag feature "카운터 부분 성공 추가. 기존 T3 규칙 '한 카운터 창에서 한 번만 성공'을 '연결부마다 한 번 성공'으로 바꾼다. 합동 공격의 필수 연결부 수를 N, 창이 닫힐 때까지 끊긴 수를 k라 하면: k = N이면 Broken(기존과 같음), 0 < k < N이면 Weakened로 실행되고 Severity = (N - k) / N, k = 0이면 Executed로 Severity = 1. Severity는 0~1이며 피해량 계산은 이번 범위가 아니다(값만 제공). N = 1인 공격은 기존 동작과 완전히 같아야 한다.
  테스트: N = 1에서 기존 T3 테스트가 모두 그대로 통과, N = 2에서 k = 0, 1, 2, 같은 연결부를 두 번 끊기 거부, 창 경계 tick에서 끊기, Severity 범위.
  기존 테스트를 바꿔야 한다면 바꾼 이유를 마지막 메시지에 테스트별로 적는다."
  ```
  리뷰 포인트: 기존 파일(M)의 변경이 처음으로 의미 있는 run. 리뷰 요점의 ⚠ 삭제된 테스트, N = 1 보존 테스트.

- [x] **T9 복잡도 예산** — `ca091d3`
  > 카운터가 많아져도 어지럽지 않게 상한을 코드로 고정한다. 연출가가 아무리 적응해도 예산을 넘지 못한다.
  ```powershell
  jevcode run --tag feature "복잡도 예산 추가. FairnessRules에 MaxOpenCounterWindows(기본 2)와 MaxRequiredPointsPerAttack(기본 2)를 기획 임시값으로 추가한다. 한 교전의 합동 공격 계획 목록을 받는 AttackSchedule을 만들고, 어느 tick에서든 열린 카운터 창 수가 MaxOpenCounterWindows 이하여야 한다(창은 양끝 tick 포함이므로 끝 tick과 시작 tick이 같으면 겹친 것으로 센다). 필수 연결부가 MaxRequiredPointsPerAttack를 넘는 계획, 또는 예산을 넘는 일정은 생성 단계에서 거부하고 사유를 돌려준다. 연출가 대응이 만든 계획도 반드시 AttackSchedule을 거친다.
  테스트: 창 2개 겹침 허용, 3개 겹침 거부, 끝 tick = 시작 tick인 경계, 겹치지 않는 3개 허용, 연결부 3개 거부, 설정값 변경, 모든 대응과 시드 조합에서 예산 유지(T7 불변식 테스트와 같은 방식)."
  ```
  리뷰 포인트: 경계 tick 겹침 규칙, 연출가 경로가 AttackSchedule을 우회하지 않는지.

- [x] **T10 콘솔 시뮬레이터** — `cfba2dc` (검증이 병렬 빌드로 멈춰 unknown, 수동으로 208개 통과 확인) — 부분 성공과 복잡도 예산이 들어간 뒤에 해야 의미가 있어 T8에서 옮겼다
  ```powershell
  jevcode run --tag feature "src/Core.Sim 콘솔 앱(net10.0) 추가. 인자: --seed <정수> (기본 1), --encounters <정수> (기본 3).
  플레이어 전략 두 개: AreaFirst(공격의 70%가 적 3명을 맞히는 광역 근접), HealerFirst(공격의 70%가 Healer를 노리는 원거리 단일). 나머지 30%는 시드 기반 무작위로 역할과 거리를 고른다. 교전당 공격 20회, 합동 공격 2회.
  한 원정을 시뮬레이션하고 교전마다 한 줄씩 출력한다: 교전 번호, 관찰된 세 경향(소수 둘째 자리), 기억 반영 후 고른 대응, 합동 공격 카운터 성공 수/시도 수. 카운터 시도는 시드 기반으로 창 안/밖을 섞는다.
  솔루션에 포함한다. 테스트는 같은 시드면 출력이 같고, 다른 시드면 다를 수 있음을 확인한다(결정성).
  마지막 메시지에 기본 인자로 실행한 출력 예시를 붙인다."
  ```
  리뷰 포인트: 직접 실행해서 교전이 진행될수록 대응이 바뀌는지 눈으로 확인. 재미 검증 전에 규칙을 감각으로 확인하는 용도다.

## 2단계: 수치 감 잡기

T10 시뮬레이션에서 발견한 것: 기본값(Decay 0.5, 임계값 0.5)에서는 플레이어가 전략을 바꾼 교전의 다음 대응이 반응하지 않는다. 회복 집중 0.80이 기억과 섞여 0.475가 되어 임계값에 못 미친다. "적이 얼마나 빨리 나를 읽어야 재미있나"를 숫자로 비교하는 단계다.

- [x] **T11 시뮬레이터 수치 인자와 기억 표시** — `e8a83cf`
  ```powershell
  jevcode run --tag feature "Core.Sim에 기획 수치 인자를 추가한다. 기존 --seed, --encounters 동작은 그대로 둔다.
  --decay <실수>: ExpeditionSettings.Decay. 0 이상 1 미만, 기본 0.5.
  --threshold <실수>: TacticSettings의 세 임계값(AreaDependencyThreshold, HealerFocusThreshold, SustainedRangedThreshold)을 한꺼번에 설정. 0~1, 기본 0.5.
  --area-threshold, --healer-threshold, --ranged-threshold <실수>: 개별 임계값. --threshold보다 우선한다.
  --strategies <쉼표 목록>: 교전 순서대로 쓸 전략(AreaFirst, HealerFirst). 기본은 지금처럼 번갈아 쓰기. 목록이 교전 수보다 짧으면 마지막 전략을 반복한다.
  실수는 InvariantCulture로 파싱하고, 범위 밖이나 알 수 없는 인자는 사유를 출력하고 종료 코드 2.
  출력: 첫 줄에 적용된 설정값을 한 줄로 표시한다. 교전마다 기존 열에 더해 기억에 합친 뒤의 세 경향(Memory Area/Healer/Ranged, 소수 둘째 자리)을 표시해, 어떤 값 때문에 NextTactic이 정해졌는지 보이게 한다.
  테스트: 각 인자 파싱, 범위 경계(decay 0 허용, 1 거부, threshold 0과 1 허용, 1.01 거부), 개별 임계값 우선, 전략 목록 반복 규칙, 설정 줄과 Memory 열 출력, 기존 인자만 줬을 때의 NextTactic 순서가 이전과 같음, 결정성."
  ```
  리뷰 포인트: 직접 실행해서 비교한다.
  ```powershell
  dotnet run --project src/Core.Sim -- --seed 1 --strategies AreaFirst,HealerFirst,HealerFirst
  dotnet run --project src/Core.Sim -- --seed 1 --strategies AreaFirst,HealerFirst,HealerFirst --decay 0.3
  ```
  두 번째에서 두 번째 교전 뒤 바로 GuardHealer가 나오면 인자가 제대로 반영된 것이다(손 계산: 회복 기억 0.3 x 0.15 + 0.7 x 0.80 = 0.605).

- [ ] **T12 Decay 비교표 (선택, 보류)** — T11로 값을 하나씩 바꿔 비교할 수 있어 Unity 프로토타입을 먼저 한다
  ```powershell
  jevcode run --tag feature "Core.Sim에 --sweep-decay <쉼표 목록> 인자를 추가한다. 목록의 각 Decay 값으로 같은 시드와 같은 전략 순서의 원정을 돌리고, 한 줄에 한 Decay씩 교전별 NextTactic만 나열한 비교표를 출력한다(예: 'decay=0.30  Spread > GuardHealer > GuardHealer'). 다른 인자(--seed, --encounters, --strategies, 임계값)는 모든 줄에 똑같이 적용한다. --sweep-decay와 --decay를 함께 주면 거부한다.
  테스트: 목록 파싱, 각 줄이 같은 인자로 단독 실행한 결과의 NextTactic 순서와 같음, 잘못된 값 거부, 결정성."
  ```
  리뷰 포인트: `--sweep-decay 0.1,0.3,0.5,0.7 --strategies AreaFirst,HealerFirst,HealerFirst,AreaFirst`로 적응 속도 차이를 한눈에 본다.

## 3단계: Unity 프로토타입 (쿼터뷰)

목표: 합동 공격 전조 → 카운터 입력 → 인형이 무너짐을 **직접 눌러 보고**, 연출가의 대응 변화가 화면에서 읽히는지 확인한다. 기획 문서의 "재미 검증"의 첫 단계다. Unity 관련 규칙은 `AGENTS.md`의 "Unity 규칙"에 있다.

**Unity run의 흐름 (기존 흐름과 다른 점)**

1. run 전에 **Unity 에디터는 열어 두되 창을 건드리지 않는다**(포커스를 주면 파일 변경을 감지해 .meta 등을 만들고, 그 변경이 run 기록에 섞인다). **MCP for Unity 서버는 꺼 둔다.** 샌드박스가 로컬 서버 접속을 막지 않기 때문이다. 켜져 있으면 run 시작 때 경고가 뜬다.
2. run이 끝나면 jevcode가 자동으로 Unity 재컴파일과 콘솔 에러를 확인한다(`jevcode init --unity` 설정, 검증 항목 `unity: recompile + console errors (host)`). 에디터가 꺼져 있으면 `unknown`으로 기록된다. 리뷰 화면에서 코드와 이 결과를 본 뒤, **Play로 동작을 직접 확인**한다.
3. 수락하고 커밋한다. 이 run이 추가한 파일의 .meta도 함께 커밋된다(따로 커밋할 필요 없음).
4. 수치는 씬에 직접 배치한 `PrototypeBootstrap`의 Inspector에서 조정한다(빈 오브젝트에 컴포넌트 추가, 씬 저장). 씬에 저장된 값이 코드 기본값보다 우선하므로, run이 기본값을 바꿨다면 컴포넌트 ⋮ 메뉴의 **Reset**을 눌러야 반영된다.

- [x] **U1 규칙 코어를 Unity 로컬 패키지로 연결** — `f1b4a47` (bin/obj 삭제는 Codex 정책에 막혀 수동 처리)
  ```powershell
  jevcode run --tag scaffold "규칙 코어를 Unity에서 쓸 수 있게 연결한다.
  1) 저장소 루트에 Directory.Build.props를 만들어 UseArtifactsOutput을 켠다(빌드 산출물을 artifacts/로). src/Core, tests 아래의 기존 bin/, obj/ 폴더는 삭제한다. .gitignore에 artifacts/를 추가한다. 이유: Unity가 패키지 폴더 안의 Core.dll까지 가져오면 같은 타입이 두 번 정의된다.
  2) src/Core/Core.csproj에 LangVersion 9.0을 설정한다(Unity 6 호환).
  3) src/Core에 package.json(name com.behindthecurtain.core, version 0.1.0, unity 6000.3, displayName Behind the Curtain Core)과 BehindTheCurtain.Core.asmdef(noEngineReferences true, rootNamespace BehindTheCurtain.Core)를 만든다. nullable 경고를 없애기 위해 asmdef 옆에 csc.rsp(-nullable:enable)를 둔다.
  4) Packages/manifest.json의 dependencies에 com.behindthecurtain.core 항목을 값 file:../src/Core 로 추가한다.
  5) Assets/Scripts/Prototype/BehindTheCurtain.Prototype.asmdef를 만들고 BehindTheCurtain.Core와 Unity.InputSystem을 참조한다.
  dotnet test tests/Core.Tests --no-restore -m:1이 계속 통과해야 한다. 마지막 메시지에 Unity에서 확인할 항목(패키지가 Package Manager에 보이는지, 콘솔 에러 0)을 적는다."
  ```
  리뷰 포인트: Unity를 열어 Package Manager의 In Project에 Behind the Curtain Core가 보이고 콘솔 에러가 0인지. `artifacts/`가 무시되는지.

- [x] **U2 쿼터뷰 장면과 이동** — 오브젝트는 Play 때 코드가 생성한다(에디터에서는 씬이 비어 보이는 게 정상)
  > 사람이 먼저 할 일: Unity에서 빈 씬을 만들어 `Assets/Scenes/Prototype.unity`로 저장하고 커밋한다(씬 파일은 에이전트가 만들지 않는다).
  ```powershell
  jevcode run --tag feature "Assets/Scripts/Prototype에 PrototypeBootstrap을 만든다. RuntimeInitializeOnLoadMethod(AfterSceneLoad)로, 활성 씬 이름이 Prototype일 때만 동작한다. 다른 씬에는 아무것도 하지 않는다.
  생성할 것: 20x20 바닥(Plane), 플레이어(Capsule, 파란색), 인형 파티 4개(Cube, 회색, 플레이어 앞 8m에 2x2 진형), 조명 1개, 카메라.
  카메라: 쿼터뷰. 플레이어를 따라가며 피치 50도, 요 45도, 거리 14m, 원근 투영 FOV 30. 따라가기는 부드럽게(SmoothDamp).
  이동: WASD를 카메라 기준 방향으로 변환해 이동, 속도 5m/s. Keyboard.current로 입력을 읽는다. 물리 없이 transform으로 이동하고 바닥 밖으로 나가지 않게 제한한다.
  수치(카메라 각도, 거리, 속도)는 PrototypeSettings(ScriptableObject가 아닌 일반 직렬화 클래스, 기본값 코드에 명시)로 모은다.
  규칙 코어 테스트는 계속 통과해야 한다. 마지막 메시지에 Play에서 확인할 항목을 적는다."
  ```
  리뷰 포인트: Play에서 쿼터뷰로 보이는지, WASD가 화면 기준으로 움직이는지, 다른 씬에 영향이 없는지.

- [x] **U2b 조작 모드 두 개 (로아식 / 몬헌식) 비교** — `d85493f`. 결정(2026-09-22): 둘 다 유지한다. A(로아식)가 기본, B(몬헌식)는 Tab으로 고르는 조작 옵션.
  > 결정 전 비교용. A(쿼터뷰 + 커서 조준, 로스트아크에 가까움)를 기본으로 하고, B(커서 잠금 + 카메라와 플레이어 함께 회전, 몬스터헌터에 가까움)를 Tab으로 바꿔 본다. U3·U4까지 해 본 뒤 하나로 정한다. 핵심 판단 기준: 진형과 뒤쪽 연결부가 읽히는지(A가 유리) vs 직접 싸우는 손맛(B가 유리).
  > 입력 배치: 좌클릭 근접, F 원거리, Space 광역(U4), 1/2/3 카운터(U3), 가운데 버튼 카메라 회전(A 모드).
  ```powershell
  jevcode run --tag feature "PrototypeBootstrap에 조작 모드 두 개를 추가하고 Tab으로 전환한다. 입력은 Mouse.current/Keyboard.current로 읽는다. 화면 왼쪽 위(OnGUI)에 현재 모드 이름을 표시한다.
  모드 A CursorAim(기본): 커서는 자유. 마우스 위치에서 바닥 평면으로 레이를 쏜 지점을 플레이어가 바라본다(회전 속도 TurnSpeed, 기본 720도/초). 카메라 각도는 기본적으로 고정이고, 가운데 버튼을 누른 채 드래그할 때만 플레이어 중심으로 회전하며 그동안만 커서를 잠그고 숨긴다.
  모드 B LockedTurn: 커서를 화면 가운데에 잠그고 숨긴다(Cursor.lockState Locked). 마우스 가로 이동으로 카메라 요와 플레이어 방향이 함께 회전하고, 세로 이동으로 피치를 바꾼다. 플레이어는 항상 카메라 정면을 바라본다.
  공통: 피치는 MinPitch~MaxPitch(기본 25~75도), 휠 스크롤로 거리 MinDistance~MaxDistance(기본 8~24m, 한 칸 ZoomStep 1.5m). 회전과 거리는 부드럽게 따라간다. WASD는 카메라 요 기준. Esc를 누르거나 창이 포커스를 잃으면(OnApplicationFocus false) 커서 잠금을 즉시 해제한다.
  플레이어의 현재 바라보는 방향을 AimDirection으로 노출한다(다음 단계에서 원거리 공격 방향으로 쓴다).
  감도(RotateSensitivity 기본 0.2도/픽셀)와 위 수치는 PrototypeSettings에 둔다. 기존 기본값(피치 50, 요 45, 거리 14)이 시작 값이다. 기존 동작 변경 섹션에 카메라와 이동 코드의 변경점을 적는다. 규칙 코어 테스트는 계속 통과해야 한다."
  ```
  리뷰 포인트: Tab으로 모드가 바뀌고 이름이 보이는지. B 모드에서 **Esc로 빠져나올 수 있는지**(안 되면 에디터 조작이 막힌다). A 모드에서 캐릭터가 커서를 바라보는지. 두 모드로 인형 주변을 돌며 진형이 읽히는지 비교.

- [x] **U3 합동 공격과 카운터 입력** — `dbd4a40`. 플레이 결과: 동작은 정상. 다만 Shoot은 제약이 없어 늘 정답이고, Cut과 Disrupt는 거리만 달라 체감 차이가 없다 → U3b
  ```powershell
  jevcode run --tag feature "프로토타입에 합동 공격 한 사이클을 연결한다. 판정은 모두 src/Core(JointAttack, CounterResolver, JointAttackPlan)를 쓴다.
  시간: 고정 30 tick/초. PrototypeClock이 Time.deltaTime을 누적해 정수 tick을 진행한다.
  사이클: 3초 대기 → 전조(인형 위로 조종 실을 LineRenderer로 표시, 점점 팽팽해지는 느낌으로 색을 흰색에서 주황으로) → 카운터 창(실이 밝게 깜박임, 노출 연결부를 인형 위 작은 구로 표시) → 실행 또는 붕괴 → 반복.
  연결부 색은 간섭 방식별로 고정: Cut 빨강, Shoot 파랑, Disrupt 노랑.
  입력: 1=Cut(거리 2.5m 이내), 2=Shoot(거리 무관), 3=Disrupt(거리 6m 이내). 누르면 해당 방식을 허용하는 가장 가까운 노출 연결부를 대상으로 CounterResolver를 호출한다. 거리 조건을 못 채우면 시도하지 않고 화면에 사유를 1초 표시한다.
  결과 표현: 연결부를 끊으면 그 구가 사라짐. Broken이면 인형들이 기울며 주저앉음(0.5초 회전). Weakened면 인형이 짧게 흔들리고 화면 가장자리가 옅게 붉어짐. Executed면 화면이 붉게 번쩍임.
  화면 왼쪽 위에 상태 텍스트(OnGUI): 현재 단계, 남은 창 tick, 끊은 연결부 수/필수 수, 마지막 판정 사유.
  수치(대기 시간, 거리, 색)는 PrototypeSettings에 둔다. 규칙 코어 테스트는 계속 통과해야 한다."
  ```
  리뷰 포인트: **직접 해 보기.** 전조가 읽히는지, 창이 너무 짧거나 길지 않은지, 1·2·3의 차이가 느껴지는지. 이 run부터가 재미 검증이다.

- [x] **U3b 간섭 방식마다 다른 대가** — `1812a71`. 정적 테스트 결과: 취소 사유는 "대상 없음"만 나옴(압박 부재), Shoot이 시야 밖 대상에도 쓰임 → U3c, U3d
  > 방향(2026-09-22): 거리 말고 대가로 역할을 나눈다. Cut = 즉발 근접, Shoot = 정지 조준 + 쿨다운, Disrupt = 채널링 + 범위. 규칙 코어는 바꾸지 않고, 입력 조건만 프로토타입에서 조정한다. 수치가 굳으면 Core로 옮긴다.
  ```powershell
  jevcode run --tag feature "PrototypeJointAttack의 카운터 입력에 방식별 대가를 추가한다. 연결부를 끊는 판정은 지금처럼 CounterResolver.Resolve를 쓰고, src/Core는 바꾸지 않는다.
  Cut(1): 지금과 같다. 거리 CutRange 이내, 누르는 즉시 가장 가까운 Cut 연결부를 대상으로 판정.
  Shoot(2): 누르면 조준을 시작한다. 조준 대상은 누른 순간 가장 가까운 Shoot 연결부로 고정. ShootAimTicks(기본 18, 0.6초) 동안 플레이어가 움직이지 않아야 하고, 이동 입력이 있으면 취소된다. 조준이 끝난 tick에 판정한다. 판정 뒤(성공이든 실패든) ShootCooldownTicks(기본 60, 2초) 동안 Shoot을 시작할 수 없다. 거리 제한은 없다.
  Disrupt(3): DisruptRange 이내에 Disrupt 연결부가 하나 이상 있을 때 채널링을 시작한다. DisruptChannelTicks(기본 45, 1.5초) 동안 움직이지 않아야 하고, 이동하면 취소된다. 끝난 tick에 그 순간 DisruptRange 안에 있는 노출된 Disrupt 연결부를 모두 차례로 판정한다.
  조준과 채널링은 동시에 하나만 진행한다. 진행 중에 다른 방식을 누르면 진행 중인 것을 취소하고 새로 시작한다(Cut은 취소 후 즉시 판정). 카운터 창이 끝나면 진행 중인 것을 취소한다.
  표시: 플레이어 머리 위에 진행 게이지(조준은 파랑, 채널링은 노랑), 조준 중에는 플레이어에서 대상 연결부로 가는 얇은 파란 선. Disrupt 채널링 중에는 바닥에 DisruptRange 반경의 노란 원. 취소되면 사유(이동, 창 종료, 대상 없음)를 1초 표시한다. OnGUI 상태 텍스트에 Shoot 쿨다운 남은 시간을 추가한다.
  수치는 PrototypeSettings에 둔다. 기존 동작 변경 섹션에 Shoot과 Disrupt의 입력 변화를 적는다. 규칙 코어 테스트는 계속 통과해야 한다. 마지막 메시지에 Play에서 확인할 항목을 적는다."
  ```
  리뷰 포인트: **직접 해 보기.** 세 방식이 다른 역할로 느껴지는지(가까운 건 자르고, 모인 Signal은 채널링, 먼 Anchor는 한 발 아껴 쏘기). 창 90tick 안에 전부 끊는 게 가능하지만 빠듯한지. 채널링 중 가만히 있어야 하는 게 답답한지, 긴장되는지. 다음 후보(이번엔 안 함): 후면 Anchor는 정면에서 못 쏘는 몸통 가림, 가까이 온 플레이어를 인형이 치는 근접 위험.

- [x] **U3c Shoot은 겨냥하고, 가려지면 못 쏜다** — `7273636`
  ```powershell
  jevcode run --tag feature "PrototypeJointAttack의 Shoot 대상 선택을 바꾼다. 판정은 지금처럼 CounterResolver.Resolve를 쓰고 src/Core는 바꾸지 않는다.
  대상: 플레이어의 AimDirection(A 모드는 커서, B 모드는 카메라 정면)을 기준으로, 수평 각도 ShootConeDegrees(기본 20도) 안에 있는 노출된 Shoot 연결부 가운데 각도가 가장 작은 것. 원뿔 안에 없으면 조준을 시작하지 않고 '조준 대상 없음'을 1초 표시한다.
  가림: 플레이어의 ConnectionHeight 높이에서 대상 연결부까지 Physics.Raycast로 확인해, 인형 몸통 콜라이더에 먼저 막히면 조준을 시작하지 않고 '가려짐'을 표시한다(플레이어 자신과 대상 연결부의 콜라이더는 제외).
  조준 중에는 매 tick 원뿔과 가림을 다시 확인하고, 어느 하나라도 벗어나면 취소하고 사유(조준 벗어남 / 가려짐)를 표시한다.
  미리보기: 카운터 창 동안, 지금 2를 누르면 대상이 될 Shoot 연결부를 흰 테두리(약간 큰 반투명 구)로 표시한다. 가려져 있으면 회색 테두리로 표시한다.
  수치는 PrototypeSettings에 둔다. 기존 동작 변경 섹션에 대상 선택 방식의 변화를 적는다. 규칙 코어 테스트는 계속 통과해야 한다. 마지막 메시지에 Play에서 확인할 항목을 적는다."
  ```
  리뷰 포인트: 후면 Anchor를 쏘려면 돌아 들어가야 하는지. 미리보기 테두리 덕분에 "왜 안 쏴지지?"가 없는지. 원뿔 20도가 너무 빡빡하거나 헐겁지 않은지(A·B 모드 둘 다).

- [x] **U3d 압박: 인형이 움직이고 바닥 경고가 떨어진다** — `851fff3`
  ```powershell
  jevcode run --tag feature "합동 공격 동안 플레이어에게 압박을 준다. 판정은 src/Core를 쓰고, 이 run은 src/Core를 바꾸지 않는다.
  인형 이동: 전조와 카운터 창 동안 인형 파티 전체가 진형을 유지한 채 파티 중심을 기준으로 PartyOrbitSpeed(기본 15도/초)로 천천히 돈다. 방향은 사이클마다 System.Random으로 정하고, 시드는 PrototypeSettings에 Seed(기본 1)로 추가한다. 연결부는 인형을 따라 움직인다. 바닥 밖으로는 나가지 않는다.
  바닥 경고: 카운터 창 동안 SlamIntervalTicks(기본 45)마다, 플레이어의 현재 위치를 중심으로 반경 SlamRadius(기본 2m)의 붉은 반투명 원을 바닥에 표시한다. SlamWarningTicks(기본 24) 뒤에 원이 한 번 번쩍이고, 그 순간 원 안에 있는 플레이어는 피격된다.
  피격: 진행 중인 Shoot 조준이나 Disrupt 채널링을 취소하고 사유 '피격'을 표시한다. 플레이어가 짧게 빨갛게 깜박인다. 체력은 두지 않고 OnGUI 상태 텍스트에 이번 사이클 피격 횟수만 표시한다.
  수치는 PrototypeSettings에 둔다. 기존 동작 변경 섹션에 인형 위치가 이제 움직인다는 점을 적는다. 규칙 코어 테스트는 계속 통과해야 한다. 마지막 메시지에 Play에서 확인할 항목을 적는다."
  ```
  리뷰 포인트: **여기서부터 재미 판단.** 채널링과 조준에 긴장감이 생기는지, 답답하기만 한지. 취소 사유로 이동·피격·창 종료가 실제로 나오는지. 인형이 도는 속도 때문에 Cut이 쫓아가기 귀찮은 일이 되지 않는지.

- [x] **U3e 연결부를 몸통 면에 붙이고 가림을 고친다 (U3 단계 마무리)** — `dcc1f54`. U3 단계 종료
  > U3c의 가림은 판정 코드는 맞지만, 레이(바닥 기준 2.2m)가 연결부(1.7m)로 내려가는 동안 인형 상자(0~1m) 위를 지나 한 번도 막히지 않았다. 연결부도 모두 자기 인형 머리 위에 있어서 앞/옆/뒤 구분이 없었다. 이 run으로 U3 단계를 닫는다. 이후 아이디어는 `docs/planning.md`의 이후 후보로 보낸다.
  ```powershell
  jevcode run --tag fix "프로토타입의 연결부 배치와 Shoot 가림을 고친다. 판정은 지금처럼 src/Core를 쓰고 src/Core는 바꾸지 않는다.
  배치: 각 연결부를 ConnectionPoint.Position(Front/Left/Right/Rear)에 따라 자기 인형 몸통의 해당 면 중앙(인형 높이의 절반, 면에서 ConnectionSurfaceOffset 기본 0.05m 바깥)에 둔다. 방향 기준은 인형 파티의 정면이다. 처음에는 플레이어 시작 위치를 향하고, U3d의 파티 회전을 따라 함께 돈다. 연결부 구는 매 프레임 이 위치를 따라간다. 조종 실(LineRenderer)은 새 연결부 위치에서 위로 올라간다.
  Shoot: 레이 출발점은 플레이어 발밑에서 ShootOriginHeight(기본 1.0m) 높이로 따로 둔다. ConnectionHeight와 섞어 쓰지 않는다. 레이는 대상 연결부 ShootOcclusionMargin(기본 0.1m) 앞에서 멈춘다. 대상의 자기 인형 몸통도 가림에 포함한다. 그래서 뒤쪽 면의 연결부는 정면에서 쏘면 막힌다.
  Cut과 Disrupt의 거리는 새 연결부 위치를 기준으로 잰다.
  바닥 경고: SlamIntervalTicks 기본값을 45에서 120으로 바꾼다(대규모 공격 주기, Disrupt 채널링 45tick이 두 경고 사이에 들어가게).
  수치는 PrototypeSettings에 둔다. 기존 동작 변경 섹션에 연결부 위치, 레이 높이, 경고 주기의 변화를 적는다. 사람이 할 일에 '씬에 배치한 PrototypeBootstrap이 있으면 컴포넌트를 Reset해야 새 기본값이 반영됨'을 적는다. 규칙 코어 테스트는 계속 통과해야 한다. 마지막 메시지에 Play에서 확인할 항목을 적는다."
  ```
  리뷰 포인트: 연결부가 인형의 앞/옆/뒤 면에 붙어 보이는지. 뒤쪽 면의 Anchor를 정면에서 겨누면 회색(가려짐) 미리보기가 뜨고, 돌아 들어가면 흰색이 되는지. 경고 주기가 늘어서 채널링을 끝낼 수 있는지. 이걸로 U3 단계를 닫고 U4로 간다.

- [x] **U4 연출가의 적응을 화면에서 읽기** — 거절·되돌림(`20260922-191244-5a47`). 원인은 명령 쪽: Core의 임시 배치(기본 진형은 Thread 하나)를 그대로 써서 Cut 한 번에 전부 쓰러짐, 공격에 화면 반응이 없음, 관찰 상황이 보이지 않음 → U4r 이전 명령은 혼동을 막기 위해 지웠다(git 기록에 있음). **U4r 명령을 쓴다.**

- [x] **U4r 연출가의 적응을 화면에서 읽기 (재시도)** — `96b5861`. 의도대로 동작. 적응이 배치만 바꿔 체감은 약하고, 연결부 찾기는 재미 요소. 다음은 인형 AI와 아군 봇 설계(`docs/planning.md`)
  > U4를 다시 한다. 달라진 점: 대응마다 연결부 3개(종류별 하나, 위치만 다름), 공격 시 화면 반응, 관찰 막대. 배치표는 만들어 보고 고친다.
  >
  > | 대응 | 무엇에 대한 대응 | Thread | Anchor | Signal |
  > |---|---|---|---|---|
  > | StandardFormation | - | 앞 | 왼쪽 | 오른쪽 |
  > | SpreadFormation | 광역 의존 | 왼쪽 | 오른쪽 | 뒤 |
  > | GuardHealer | 힐러 집중 | 뒤 | 앞 | 왼쪽 |
  > | ShieldAdvance | 원거리 | 앞 | 뒤 | 오른쪽 |
  ```powershell
  jevcode run --tag retry "프로토타입에 원정 루프를 붙인다. 교전 = 합동 공격 EncounterCycles(기본 3)사이클. 교전이 끝나면 Expedition에 BehaviorProfile을 합치고 다음 교전의 대응(Tactic)과 JointAttackPlan을 정한다. 판정은 모두 src/Core를 쓴다.
  1) src/Core Tactic.TryCreatePlan의 연결부 배치를 대응마다 3개로 바꾼다. 각 대응은 Thread, Anchor, Signal을 하나씩 가지며 위치만 다르다. StandardFormation: Thread 앞, Anchor 왼쪽, Signal 오른쪽. SpreadFormation: Thread 왼쪽, Anchor 오른쪽, Signal 뒤. GuardHealer: Thread 뒤, Anchor 앞, Signal 왼쪽. ShieldAdvance: Thread 앞, Anchor 뒤, Signal 오른쪽. 이전 배치를 전제로 한 기존 테스트는 새 배치에 맞게 고치고, 어떤 테스트를 왜 바꿨는지 기존 동작 변경 섹션에 적는다. 테스트를 지우지 않는다.
  2) src/Core CombatObserver에 교전을 끝내지 않고 지금까지의 관찰을 읽는 CurrentProfile(BehaviorProfile, 읽기 전용, 상태를 바꾸지 않음)을 추가하고 테스트한다. 화면 표시는 이것만 쓰고, Unity 쪽에서 비율을 따로 계산하지 않는다.
  3) 플레이어 공격(CombatObserver에 기록): Space=광역 근접(반경 3m 안의 인형 모두), F=원거리 단일(AimDirection 쪽 가장 가까운 인형), 마우스 왼쪽 클릭=근접 단일(3m 안의 가장 가까운 인형). 인형 역할은 고정: 앞 2개 Guard, 뒤 왼쪽 Attacker, 뒤 오른쪽 Healer(초록색). 공격이 닿은 인형은 0.15초 하얗게 번쩍이고 그 위에 '광역', '원거리', '근접' 글자가 0.6초 떠오른다. 닿은 인형이 없으면 플레이어 옆에 '빗나감'을 띄우고 기록하지 않는다.
  4) 대응을 진형으로 보여준다: SpreadFormation은 인형 간격 2배, GuardHealer는 Guard 하나가 Healer 앞으로, ShieldAdvance는 인형들이 플레이어 쪽으로 2m 전진, StandardFormation은 기본 진형. 진형은 교전마다 기본 배치에서 다시 계산한다. 진형은 파티 중심 기준의 인형 위치로 적용하고, U3d의 파티 회전과 바닥 경고, U3e의 몸통 면 연결부 배치는 그대로 유지한다. 연결부 i는 지금처럼 인형 i에 붙는다.
  5) 표시: 화면 왼쪽 위에 현재 교전 번호, 대응 이름, 그리고 CurrentProfile의 세 경향(광역 의존, 힐러 집중, 원거리 지속)을 막대로, 기준선(TacticSettings 기본값)을 세로선으로 그린다(OnGUI). 교전 사이에 2초간 화면 가운데에 '연출가가 각본을 고친다: <대응 이름>'과 다음 교전에 쓰일 세 경향 값을 표시한다.
  수치는 PrototypeSettings에 둔다. 규칙 코어 테스트는 계속 통과해야 한다. 마지막 메시지에 세 경향이 각각 어떤 공격으로 올라가는지 한 줄씩 설명하고, Play에서 확인할 항목을 적는다."
  ```
  리뷰 포인트: 첫 교전에서 빨강·파랑·노랑 연결부가 모두 보이는지. 공격을 누르면 인형이 번쩍이고 막대가 움직이는지. 힐러만 때렸을 때 힐러 집중 막대가 기준선을 넘고, **다음 교전에서** 진형과 연결부 위치가 바뀌는지. 방패 전진에서 뒤쪽 Anchor를 쏘려면 돌아 들어가야 하는지. 바뀐 테스트가 새 배치 때문인지(⚠ 표시 확인).

## 4단계: 인형 AI (`docs/ai-design.md`)

목표: 연출가의 적응이 배치뿐 아니라 **행동**으로 드러나게 한다. 판단은 전부 규칙 코어에 두고, Unity는 보여 주기만 한다. 순서는 T13(뼈대) → T14(개인별 관찰, 의도 가중치) → T15(인형 행동과 시나리오 테스트) → U5(Unity 연결, 임시 압박 제거) → T16(공격 특성, 파츠, 노출). 봇은 그 뒤.

- [x] **T13 월드 상태, 블랙보드, Utility 행동 선택기 (뼈대)** — `c0695e1`. 기존 동작 변경 없음, "의존한 기존 코드" 섹션이 처음으로 채워짐(TargetRole 재사용)
  > 행동 내용은 T15에서 넣는다. 이 run은 구조와 결정론만 만든다. 기존 코드의 동작은 바꾸지 않는다.
  ```powershell
  jevcode run --tag feature "docs/ai-design.md의 '구조'와 '월드 상태'를 따라 src/Core에 인형 AI의 뼈대를 만든다. Unity 코드와 기존 Core 타입의 동작은 바꾸지 않는다.
  1) DeterministicRandom: 시드로 만드는 자체 난수(SplitMix64 등). System.Random은 쓰지 않는다(런타임마다 결과가 달라질 수 있어 서버 판정과 맞지 않음). NextDouble, NextInt(범위) 제공. 같은 시드면 같은 수열임을 테스트한다.
  2) 월드 상태: 평면 좌표 Vec2(x, z), AgentId, Side(Puppets, Players), 사람/봇 구분, 인형 역할(기존 TargetRole 재사용), 위치, 방향, 반지름, 현재 행동과 대상. World는 개체 추가·조회·위치 갱신을 결과 값으로 처리한다(잘못된 입력은 실패 사유 반환, 예외 없음). 체력·피해는 넣지 않는다.
  3) 블랙보드: 편마다 하나. 사실(키 → 값)과 예약(키, 소유 개체, 만료 tick)을 둔다. 다른 개체가 유효하게 예약한 키는 예약할 수 없고, 만료 tick이 지나면 풀린다. 의도 가중치(행동 id → 배율)를 담을 자리를 둔다(값 채우기는 T14).
  4) Utility 행동 선택기: 행동 정의 = id + 고려 요소 목록 + 기본 가중치. 고려 요소 = 입력(0~1로 정규화된 값을 주는 함수) + 응답 곡선(Linear, Quadratic, Logistic, Step, 매개변수 포함, 결과는 0~1로 자름). 점수 = 곡선 값들의 곱 × 기본 가중치 × 블랙보드 의도 배율. 고려 요소 하나라도 0이면 그 행동은 선택되지 않는다. 지금 하던 행동에는 지속 보너스(설정값)를 더해 비슷한 점수에서 흔들리지 않게 한다. 동점은 DeterministicRandom으로 가른다. 재평가는 설정한 tick 간격마다만 한다.
  5) 선택 결과는 고른 행동뿐 아니라 모든 행동의 점수와 고려 요소별 값을 함께 돌려준다(개발용 표시와 리뷰에서 '왜 이 행동인가'를 설명하는 용도).
  수치(재평가 간격, 지속 보너스 등)는 설정 타입에 두고 임의로 정한 값임을 표시한다.
  테스트: 같은 시드와 입력이면 같은 선택 순서, 고려 요소 0이면 제외, 지속 보너스로 인한 유지와 차이가 클 때의 전환, 재평가 간격, 예약 충돌과 만료, 의도 배율이 선택을 바꿈, 잘못된 입력의 실패 사유. 규칙 코어 테스트는 계속 통과해야 한다."
  ```
  리뷰 포인트: 기존 동작 변경이 "없음"인지. 선택 결과에 점수와 고려 요소 값이 모두 담기는지(나중에 "왜 이렇게 움직였나"를 기록만으로 설명하는 근거). System.Random을 쓰지 않았는지. "의존한 기존 코드"에 TargetRole 재사용이 적혀 있는지.

- [x] **T14 공격자별 관찰(사람만), 경향을 키로, 대응 → 의도 가중치** — `3acd60f`. 가정 "관찰되지 않은 개인은 0 프로필과 합쳐 감쇠"는 의심도 규칙으로 바꾸기로 함(T15)
  > `docs/ai-design.md`의 "관찰: 파티 + 개인별"과 "대응 → 의도". 행동 내용(T15) 전에, 연출가가 블랙보드에 무엇을 올리는지를 만든다. 기존 세 경향의 판정 결과는 바뀌지 않아야 한다.
  ```powershell
  jevcode run --tag feature "docs/ai-design.md의 '관찰: 파티 + 개인별'과 '대응 → 의도'를 src/Core에 구현한다. Unity 코드는 바꾸지 않는다.
  1) 공격자: AttackEvent에 공격자 AgentId(T13)와 사람/봇 구분을 더한다. 기존 생성자는 그대로 두고(공격자 미상, 사람으로 취급), 공격자를 받는 생성자를 추가한다. CombatObserver는 봇의 공격을 기록하지 않는다(파티 경향에도, 개인별 경향에도 넣지 않음).
  2) 경향을 키로: TendencyId 열거형(AreaDependency, HealerFocus, SustainedRanged)을 만들고, BehaviorProfile이 TendencyId → 값의 모음을 갖게 한다. 기존 AreaDependency·HealerFocus·SustainedRangedRatio 속성은 이 모음을 읽는 속성으로 남겨 기존 코드와 테스트가 그대로 통과하게 한다. 새 경향을 추가할 때 열거형 항목과 계산만 늘리면 되게 한다.
  3) 개인별 경향: CombatObserver가 사람 공격자마다 같은 경향을 따로 센다. 교전 중 읽기(개인별 CurrentProfile)와 교전 종료 시 결과(파티 프로필 + 개인별 프로필)를 제공한다. 공격자 미상 공격은 파티 경향에만 들어간다. Expedition은 개인별 기억도 기존 파티 기억과 같은 감쇠 규칙으로 합친다. 개인별 기억은 원정이 끝나면 지운다(원정 간 유지는 열린 질문, docs/ai-design.md).
  4) 대응 → 의도: 대응(TacticId)마다 행동 id → 배율 표를 두고, 교전 시작 시 인형 편 블랙보드의 SetIntention으로 올린다. 행동 id는 문자열 상수로 한곳에 모은다: approach, intercept, spread, hold, attack. 표(임의 값, 설정으로 분리): StandardFormation 모두 1.0 / SpreadFormation spread 2.0, hold 0.7 / GuardHealer intercept 2.5 / ShieldAdvance approach 2.0, hold 0.5.
  5) 표적 사실: 교전 시작 시 개인별 기억으로 블랙보드 사실을 올린다. 'target.healerThreat' = 힐러 집중이 가장 높은 사람 플레이어, 'target.rangedThreat' = 원거리 지속이 가장 높은 사람 플레이어. 해당자가 없거나 값이 0이면 올리지 않는다. 동점은 AgentId가 작은 쪽.
  6) 연출가의 판단은 교전 사이에만 바뀐다: 의도와 표적 사실은 교전 시작 때 한 번 올리고, 교전 중에는 바꾸지 않는다.
  수치는 설정 타입에 두고 임의로 정한 값임을 표시한다.
  테스트: 봇 공격은 파티·개인 경향 어디에도 들어가지 않음, 기존 세 경향 값과 대응 선택이 이전과 같음(기존 테스트 통과), 개인별 경향 분리(A는 힐러만, B는 광역만 → 각자 다름), 힐러 위협자 선정과 동점 처리, 대응별 의도 배율이 블랙보드에 올라감, 교전 중 의도 불변, 원정 종료 시 개인별 기억 삭제. 규칙 코어 테스트는 계속 통과해야 한다."
  ```
  리뷰 포인트: 기존 동작 변경에 AttackEvent·BehaviorProfile·CombatObserver·Expedition이 적혀 있고, **기존 테스트를 고치지 않고** 통과했는지(⚠ 테스트 변경이 없어야 정상). 봇 제외가 파티 경향에도 적용됐는지. "의존한 기존 코드"에 RuleTacticSelector와 T13 블랙보드가 적혀 있는지.

- [x] **T15 의심도 기억과 인형 행동 (시나리오 테스트)** — `d1a26eb`. 311개 통과. 느린 하강 때문에 T10 시뮬레이션에서 분산 진형이 더 오래 남음(의도한 효과, U5 플레이에서 답답한지 확인)
  > `docs/ai-design.md`의 "의심도", "대응 → 의도", "인형 행동 후보". 연출가의 적응이 **행동**으로 드러나는 첫 run이다. 판단은 Core에만 있고 Unity는 U5에서 연결한다. 합동 공격 중의 행동, 미끼, 파츠 노출은 이번에 다루지 않는다.
  ```powershell
  jevcode run --tag feature "docs/ai-design.md의 '의심도', '대응 → 의도', '인형 행동 후보'를 src/Core에 구현한다. Unity 코드는 바꾸지 않는다. T13(World, Blackboard, UtilitySelector)과 T14(개인별 기억, ActionIds, TargetFactIds)를 쓴다.
  1) 의심도: 기억을 합칠 때 세 경우를 나눈다. 이번 값이 기억보다 높으면 RiseWeight(기본 0.5)만큼 이번 값 쪽으로, 공격 기록은 있지만 이번 값이 기억보다 낮으면 FallWeight(기본 0.2)만큼, 그 플레이어의 공격 기록이 아예 없으면 AbsentFallWeight(기본 0.1)만큼 0 쪽으로 옮긴다. 경향마다 따로 적용한다. 파티 기억도 같은 규칙이다(교전 전체에 공격 기록이 없으면 '기록 없음'). ExpeditionSettings에 세 값을 두고, 기존 Decay는 RiseWeight = 1 - Decay로 이어지게 해 오르는 쪽의 결과는 이전과 같게 한다. 내려가는 쪽 결과가 바뀌어 고쳐야 하는 기존 테스트는 고치되, 어떤 테스트를 왜 바꿨는지 기존 동작 변경 섹션에 적는다.
  2) 인형 행동: ActionIds의 approach, intercept, spread, hold, attack을 UtilityAction으로 정의한다. 대상과 목표 지점은 블랙보드 사실과 World에서만 얻는다.
     - approach: 대상 = 'target.rangedThreat'가 있으면 그 플레이어, 없으면 가장 가까운 사람 플레이어. 멀수록 점수가 높다.
     - intercept (Guard만): 'target.healerThreat'와 힐러가 모두 있을 때만. 목표 지점 = 힐러와 위협자를 잇는 선 위에서 힐러로부터 InterceptDistance(기본 1.5m). 위협자가 힐러에 가깝고, Guard가 목표 지점에서 멀수록 점수가 높다. 위협자가 움직이면 목표 지점도 따라간다(따라다니며 막기).
     - spread: 가장 가까운 다른 인형과의 거리가 SpreadDistance(기본 3m)보다 가까울수록 점수가 높다. 목표 지점은 가까운 인형들의 반대쪽.
     - hold: 교전 시작 때의 진형 자리로 돌아간다. 낮은 기본 점수(항상 선택 가능한 후보).
     - attack: AttackRange(기본 2m) 안에 사람 플레이어가 있을 때만. 제자리에서 대상 지정만 한다(피해는 T16).
  3) 실행: PuppetBrain(또는 같은 역할의 타입)이 매 tick 인형마다 UtilitySelector로 행동을 고르고, 목표 지점으로 MoveSpeed(기본 3m/s)만큼 이동시켜 World에 반영한다. 방향은 이동 방향. 경기장 경계(ArenaHalfSize 기본 10m, Unity 바닥 20x20과 같음) 밖으로 나가지 않는다. World에 현재 행동과 대상을 갱신하는 메서드를 추가한다. 플레이어 위치는 호출자가 World에 넣는다.
  4) 표시용 노출: 인형마다 마지막 UtilitySelection(모든 행동의 점수와 고려 요소 값)과 현재 목표 지점을 읽을 수 있게 한다(U5 개발용 표시).
  수치는 설정 타입에 두고 임의로 정한 값임을 표시한다.
  시나리오 테스트(시드 고정, tick 단위 시뮬레이션): (a) 힐러 보호: 1교전에서 플레이어 A가 힐러만 공격 → 2교전에서 Guard 하나가 N tick 안에 힐러와 A 사이(선분까지 거리 0.5m 이내)로 가고, A가 옆으로 움직이면 따라간다. (b) 방패 전진: 원거리 위협자에게 인형들이 거리를 좁힌다. (c) 분산: 인형 간 최소 거리가 교전 시작보다 커진다. (d) 기본 진형: 위협이 없으면 진형 자리 근처를 지킨다. (e) 의심도: 힐러를 노리던 A가 공격은 하되 힐러를 안 치면 힐러 집중이 천천히 줄고, A가 아예 공격하지 않은 교전에서는 더 천천히 준다. 버릇을 바꾼 다음 교전에서도 A가 여전히 힐러 위협자로 남는지 확인한다. (f) 결정론: 같은 시드와 입력이면 모든 인형의 위치와 행동 순서가 같다. (g) 경계 밖으로 나가지 않는다. 규칙 코어 테스트는 계속 통과해야 한다."
  ```
  리뷰 포인트: 기존 동작 변경에 의심도(내려가는 쪽)만 적혀 있고, 고친 기존 테스트가 그 이유 때문인지(⚠ 표시 확인). 시나리오 (a)가 "따라다니며 막기"를 실제로 검사하는지(위협자가 움직이는 단계가 있는지). "의존한 기존 코드"에 T13·T14 타입이 적혀 있는지.

- [x] **U5 인형이 Core의 판단대로 움직인다 (임시 압박 제거)** — `4de467f`. 연결은 동작. 플레이 결과: 가까이 가면 도망, 군체처럼 동시에 움직임, 한두 명이 항상 앞을 막음 → 원인은 T15 명세, T15r로 Core만 고친다(U5는 다시 돌리지 않음)
  > T15의 PuppetBrain을 프로토타입에 연결한다. 적응이 **행동**으로 보이는지 처음 확인하는 run이다. 임시 압박(U3d)의 파티 회전은 걷어내고, 바닥 경고는 **인형의 공격 행동**이 만드는 것으로 바꾼다. 판단용 표시는 개발용 오버레이로 옮긴다(`docs/planning.md` "적응 변화의 전달 원칙").
  ```powershell
  jevcode run --tag feature "프로토타입의 인형 이동을 src/Core의 PuppetBrain(T15)이 정하게 한다. 판단은 Core만 한다. src/Core는 바꾸지 않는다.
  1) 교전 시작: Expedition.TryStartEncounter 뒤 EncounterWorld에 플레이어(AgentId 1, 사람, Side.Players)와 인형 4개(AgentId 10~13, 앞 2개 Guard, 뒤 왼쪽 Attacker, 뒤 오른쪽 Healer)를 지금 진형 위치로 넣고 PuppetBrain을 만든다. U4r의 대응별 진형은 시작 위치로만 쓴다.
  2) 매 tick(PrototypeClock): 플레이어 위치와 방향을 World에 넣고 PuppetBrain을 한 tick 진행한 뒤, 인형 transform을 World 위치로 옮긴다(프레임 사이 보간). Unity 좌표 x, z = Core Vec2(x, z). 연결부와 조종 실은 지금처럼 인형을 따라간다.
  3) 플레이어 공격 기록: 기존 Space/F/좌클릭 기록에 공격자 AgentId 1(사람)을 붙인다.
  4) 임시 압박 교체: PrototypeJointAttack.Pressure.cs의 파티 회전을 제거한다. 플레이어 발밑에 주기적으로 떨어지던 바닥 경고도 제거하고, 대신 인형의 현재 행동이 attack으로 바뀔 때 그 대상의 위치에 같은 경고 원(SlamWarningTicks 뒤 판정, 피격 시 조준·채널링 취소)을 만든다. 같은 인형은 AttackCooldownTicks(기본 90) 안에 다시 만들지 않는다. 피격 판정과 취소 동작은 기존 코드를 재사용한다.
  5) 개발용 오버레이: F1로 켜고 끈다(기본 꺼짐). 켜면 인형마다 머리 위에 현재 행동과 대상 id, 목표 지점까지의 선, PuppetBrain.GetDecision의 상위 3개 행동 점수를 보여 준다. U4r의 관찰 막대, 대응 이름, 교전 사이의 '연출가가 각본을 고친다' 문구도 이 오버레이 안으로 옮긴다(꺼져 있으면 보이지 않음). 교전 번호와 카운터 조작 안내 같은 조작 정보는 그대로 둔다.
  6) 합동 공격 전조와 카운터 창 동안에도 PuppetBrain은 계속 돈다(인형이 움직이는 동안 연결부를 찾는다). 이 동작은 임시이며 합동 공격 중 행동은 열린 질문이다(docs/ai-design.md).
  수치는 PrototypeSettings에 둔다. 기존 동작 변경 섹션에 파티 회전 제거, 바닥 경고의 출처 변경, 표시가 오버레이로 옮겨진 것을 적는다. 사람이 할 일에 'PrototypeBootstrap 컴포넌트 Reset'이 필요한지 적는다. 규칙 코어 테스트는 계속 통과해야 한다. 마지막 메시지에 Play에서 확인할 항목을 적는다."
  ```
  리뷰 포인트: **오버레이를 끄고 먼저 해 본다.** 힐러만 때린 다음 교전에서 Guard가 나를 따라다니며 힐러 앞을 막는지 **행동만 보고** 알아챌 수 있는지. 그다음 F1을 켜서 실제 판단(intercept, 점수)과 맞는지 확인한다. 버릇을 바꾼 뒤 한두 교전 동안 계속 의심하는 게 재미있는지, 답답한지. 바닥 경고가 "인형이 나를 친다"로 읽히는지.

- [x] **T15r 역할별 행동 계약과 금지 패턴 테스트 (T15 수정)** — `b0fd82c`. 방해는 동작(근접·광역으로 치면 힐러 외 전원이 따라옴). 플레이 결과: 인형들을 지나쳐 가면 내 뒤에서 멀뚱히 서 있음, 힐러를 원거리로 쳐도 같은 교전에서는 Guard가 막지 않고 나를 쫓음, 맞은 힐러는 나를 쫓는 Guard 뒤에 붙어 같이 끌려옴 → 인형이 역할에 갇힘. 인형 상태 층을 설계에 추가(`docs/ai-design.md` "인형 상태"), T16에 합친다. "뒤에서 대기"는 원인 확인 결과 U5f와 T16b로 나눠 고친다(`docs/ai-design.md` "인형 상태")
  > `docs/ai-design.md`의 "행동 계약". T15 명세가 요소 하나씩만 정해서 생긴 도망·군체·독점을 고친다. Core만 바꾸고 U5의 Unity 코드는 그대로 쓴다. 끝나면 시뮬레이션 궤적을 먼저 확인하고 Play한다.
  ```powershell
  jevcode run --tag fix "docs/ai-design.md의 '행동 계약'(역할별 계약, 금지 패턴)을 src/Core의 인형 행동(PuppetActions, PuppetBrain, PuppetSettings)에 적용한다. Unity 코드는 바꾸지 않는다. 공개 API(PuppetBrain.GetDecision, World 등)는 U5가 쓰므로 유지한다.
  1) 역할별 가중치 표: 모든 역할이 같은 행동 후보(approach, attack, hold, intercept, spread, keepDistance)를 갖고, 역할(TargetRole)마다 기본 가중치 표를 둔다. 가중치 0은 '기본적으로 안 함'이지 금지가 아니다. 기본값: Guard는 hold(앞자리)·approach·attack·intercept, Attacker는 approach·attack·spread, Healer는 hold(뒷자리)·keepDistance·spread(keepDistance는 새 행동 id로 ActionIds에 추가). 기본값에서는 Healer만 물러난다. intercept는 행동 정의상 Guard 전용이 아니라 가중치로만 Guard에 켜 둔다.
  1-1) 대응이 역할별로 바꾼다: 의도 배율을 (역할, 행동) 단위로 둔다. 블랙보드 키는 '<역할>:<행동>'이고, 없으면 기존 '<행동>' 키(T14)로 떨어진다(기존 대응 표와 호환). IntentionSettings에 역할별 항목을 넣을 수 있게 하고, 기본 대응 표는 지금 값을 유지한다. 행동이 플레이어 패턴에 따라 바뀌어야 하므로, 새 대응은 이 표에 행을 더하는 것만으로 만들 수 있어야 한다.
  2) engage(기존 approach를 대체하되 id는 approach를 유지해 의도 표와 호환): 대상은 방패 전진이면 원거리 위협자, 아니면 가장 가까운 사람. 공격 거리 밖이면 거리와 관계없이 일정한 점수(가까워져도 떨어지지 않음), 공격 거리 안이면 0이 되고 attack이 이어받는다. attack의 기본 가중치는 engage보다 높다.
  3) hold: 위협 반경(ThreatRadius, 기본 6m) 안에 사람이 없을 때만 높고, 있으면 거의 0.
  4) keepDistance(Healer): 가장 가까운 사람과의 거리를 KeepMin(5m)~KeepMax(8m)로 유지하고, 가장 가까운 Guard 뒤(사람 반대쪽)를 목표로 한다.
  5) intercept: Guard만, 힐러 보호가 아닌 대응에서는 기본 가중치를 낮춘다. 블랙보드 예약('intercept:<힐러 id>')으로 한 명만 맡고, 맡은 Guard가 계속 선택하는 동안 예약을 갱신한다. Expedition이 힐러·원거리 위협 사실을 올리는 기준을 ThreatFactThreshold(기본 0.35) 이상으로 바꾼다(T14 동작 변경).
  6) 개체 차이: 인형마다 행동 가중치에 시드 기반 ±WeightJitter(15%)를 곱한다(AgentId와 시드로 결정, 교전 동안 고정). 재평가 시점을 인형마다 엇갈리게 한다(인형 순번 × 간격 / 인형 수만큼 오프셋).
  7) 시뮬레이션 궤적: src/Core.Sim에 --puppets <approach|circle|retreat|healer> 옵션을 추가한다. 플레이어 한 명이 정해진 경로로 움직이는 10초 시나리오를 돌려 0.5초마다 tick, 플레이어 위치, 인형별 역할·행동·위치를 출력하고, 끝에 금지 패턴 지표(도망 tick 수, 인형별 행동 전환 수, 동시 전환 비율, 막아서기 인원 최대값, 공격 거리 안 체류 시 공격 여부)를 출력한다. approach = 12m에서 5m/s로 파티 정면을 향해 와서 3m에 멈춤, circle = 파티 중심에서 4m 반지름으로 돈다, retreat = 3m에서 12m로 물러남, healer = 힐러 집중 기억이 있는 상태로 힐러 쪽으로 돌아 들어감.
  수치는 설정 타입에 두고 임의로 정한 값임을 표시한다.
  테스트: docs/ai-design.md '금지 패턴' 표의 기준을 위 네 시나리오로 검사한다. '항상 금지'(진동, 군체 동기화, 겹치기, 무반응)는 기본 대응과 아래 사용자 정의 대응 모두에서, '기본값에서만 금지'(이유 없는 도망, 막아서기 과다)는 기본 대응에서 검사한다. 역할별 의도 확인: 테스트 전용 대응으로 Attacker의 keepDistance를 켜면 Attacker가 거리 범위를 일관되게 유지하며 물러나고(진동 없음), 켜지 않으면 Attacker는 물러나지 않는다. 추가로 같은 상황에서 Guard·Attacker·Healer가 서로 다른 행동을 고르는지, 같은 시드면 궤적이 같은지. 기존 T15 테스트 중 approach가 가까워지면 약해지는 것을 전제로 한 것은 새 계약에 맞게 고치고, 무엇을 왜 바꿨는지 기존 동작 변경 섹션에 적는다. 규칙 코어 테스트는 계속 통과해야 한다. 마지막 메시지에 네 시나리오의 금지 패턴 지표를 표로 적는다."
  ```
  리뷰 포인트: 마지막 메시지의 지표 표가 모든 기준을 통과하는지. 리뷰 뒤 `dotnet run --project src/Core.Sim -- --puppets approach`(나머지 셋도)로 궤적을 직접 확인한다(사람이 하거나 Claude에게 요청). 이상이 없을 때만 Play로 **재미**를 본다: 다가가면 Guard가 맞서고 Attacker가 파고드는지, Healer만 뒤로 빠지는지, 힐러를 노리던 나를 Guard 한 명이 따라다니는지.

## 5단계: 인형 상태 (`docs/ai-design.md` "인형 상태")

> 핵심 논지는 다양성이다. 인형의 행동을 배역 하나가 아니라 몸·결·실·상황·연출가의 겹침으로 만든다. 한 run이 너무 무거워지지 않게(T15r 한 번에 Pro 5시간 한도의 33%) T16을 넷으로 나누고, T16a는 다시 상태(T16a-1)와 실(T16a-2)로 나눴다. T16b 명령은 T16a-2와 T17을 확인한 뒤에 쓴다.

- [x] **U5f 인형 공격이 계속 나가고, 인형이 대상을 본다 (U5 수정)** — `76ad5e8`
  > T15r Play의 "지나치면 뒤에서 멀뚱히 서 있음" 중 두 원인. 작은 수정이다. 세 번째 원인(Guard가 자리로 돌아감)은 T16b에서 고친다.
  ```powershell
  jevcode run --tag fix "인형 공격이 한 번만 나가고 멈추는 문제와 인형이 대상을 보지 않는 문제를 고친다. 다른 행동 선택 규칙은 바꾸지 않는다.
  1) Unity(Assets/Scripts/Prototype/PrototypeJointAttack.Brain.cs): 지금은 인형의 CurrentAction이 attack으로 '바뀌는 tick'에만 바닥 경고(CreateSlam)를 만든다. 그래서 사람이 공격 거리 안에 머무르면 첫 공격 뒤로는 서 있기만 하고, attack에 들어갈 때 쿨다운 중이었으면 한 번도 치지 않는다. 바꾼다: CurrentAction이 attack이고 대상이 있고 tick >= attackReadyTicks인 동안 매번 경고를 만든다. 쿨다운은 지금처럼 AttackCooldownTicks(기본 90)다.
  2) Core(src/Core/PuppetBrain.cs): 지금은 방향을 이동 방향으로만 정하므로, 제자리 행동(attack 등) 중에 사람이 지나쳐 가면 인형이 등을 보인 채 서 있다. 바꾼다: 그 tick에 이동하지 않았고 결정에 대상(Target)이 있으면 방향을 대상 쪽으로 돌린다(즉시 회전, 회전 속도는 두지 않는다). 이동한 tick은 지금처럼 이동 방향. 대상이 없고 이동도 없으면 방향을 유지한다.
  테스트(Core, tests/Core.Tests): (a) 공격 거리 안의 사람이 인형 뒤쪽으로 지나가면 다음 tick에 인형 방향과 '인형→사람' 단위 벡터의 내적이 0.99 이상. (b) 이동한 tick의 방향은 이동 방향. (c) 대상 없음·이동 없음이면 방향이 이전과 같다. (d) 같은 시드와 입력이면 방향까지 같은 궤적. 기존 PuppetContract 테스트(금지 패턴)는 계속 통과해야 한다. 규칙 코어 테스트는 계속 통과해야 한다.
  기존 동작 변경 섹션에 경고 발생 조건 변경(바뀌는 순간 → attack인 동안 쿨다운마다)과 방향 규칙 변경을 적는다. 마지막 메시지에 Play에서 확인할 항목을 적는다."
  ```
  리뷰 포인트: Play에서 인형들 사이를 지나 뒤쪽 2m 안에 가만히 서 본다. 3초마다 바닥 경고가 계속 떨어지는지, 인형들이 나를 향해 도는지. 공격이 너무 잦아 답답하면 AttackCooldownTicks만 조정한다(코드 수정 없이).

- [x] **T16a-1 개체 상태 (Core)** — `66fbb87`
  > `docs/ai-design.md` "인형 상태"의 몸과 적의 두 종류. **상태 기계만 만든다.** 행동 점수는 건드리지 않으므로, 입력이 없으면 동작이 T15r과 같다. 장력을 점수에 반영하고 연출가가 조이는 것은 T16a-2다.
  ```powershell
  jevcode run --tag feature "docs/ai-design.md의 '인형 상태' 중 개체 상태, 몸, 적의 두 종류(인형 / 인형화된 사람)를 src/Core에 구현한다. 행동 점수 계산은 바꾸지 않는다(장력의 점수 반영과 조율은 다음 run T16a-2). Unity 코드는 바꾸지 않는다. 공개 API(PuppetBrain.GetDecision, World 등)는 U5가 쓰므로 유지하고, 새 기능은 추가로만 넣는다.
  1) 개체 상태: 인형 편 개체마다 상태를 둔다. Kind(Puppet | Humanized, 기본 Puppet), Health(기본 100), Posture(기본 100), Tension(0~1, 시작 1), Status(Active | Groggy | Released | Broken | Downed). 수치 항목은 식별자 → 값의 모음으로 늘릴 수 있게 한다(관찰 경향과 같은 방식). 호출자가 교전 시작 때 개체별 Kind와 초기값을 줄 수 있다.
  2) 입력 API(공격 판정은 T16c 이후이므로 호출자가 부른다): ApplyDamage(id, health, posture, tick), ReduceTension(id, amount, tick). 음수·NaN·없는 id·Active나 Groggy가 아닌 개체는 결과값으로 거부한다(예외 아님). Groggy 개체의 ReduceTension은 거부한다(ApplyDamage는 받는다).
  3) 전이: Health <= 0이면 Puppet은 Broken, Humanized는 Downed(인형화가 풀리고 쓰러짐 = 구출). Tension <= 0이면 Puppet은 Released(풀려남). Humanized는 Groggy가 되어 GroggyTicks(기본 60) 동안 행동을 고르지 않고 이동하지 않으며, 끝나면 Tension = ReconnectTension(기본 0.5)으로 Active. Broken·Released·Downed는 행동 선택에서 빠지고, 다른 개체의 대상 고르기·예약·흩어지기 거리 계산에서도 빠진다. Downed는 그 교전에서 다시 연결되지 않는다(어떤 입력으로도 Active가 되지 않음). 모든 상태 변화(입력과 전이)는 (tick, 개체, 종류, 이전 값, 새 값, 원인 입력)으로 사건 기록에 남긴다. 다음 run이 이 기록을 원인 표시에 쓴다.
  4) 몸: Posture < PostureStaggerThreshold(기본 30)이면 이동 속도 × PostureSlowScale(기본 0.5). Posture는 초당 PostureRecoverPerSecond(기본 10)씩 100까지 회복한다(tick 단위로 계산).
  5) 표시용 노출: 개체별 상태(Kind, Health, Posture, Tension, Status)와 사건 기록을 읽을 수 있게 한다(F1 오버레이용, Unity 연결은 다음에).
  수치는 설정 타입에 두고 임의로 정한 값임을 표시한다.
  테스트: (a) 회귀: 입력이 없으면 기존 PuppetContract·PuppetBrain 테스트가 그대로 통과한다. (b) 전이 경계: Health 정확히 0 → Broken/Downed, 0.0001 → Active. Tension 정확히 0 → Released/Groggy, 0.0001 → Active. Groggy는 정확히 GroggyTicks tick 뒤 Active, Tension 0.5. Groggy 중에는 행동을 고르지 않고 위치가 그대로다. (c) 거부: 음수·NaN·없는 id, Downed·Broken·Released 개체에 대한 입력, Groggy 개체의 ReduceTension. Downed 개체는 어떤 입력 순서로도 Active가 되지 않는다. (d) 제외: Released·Broken·Downed 개체는 다른 개체의 선택·대상·예약·흩어지기 계산에 나타나지 않는다(예: Healer가 Broken이면 intercept 대상이 없다). (e) 몸: Posture 29.9는 느려지고 30은 그대로. 회복량이 초당 10, 100에서 멈춘다. (f) 사건 기록: 입력 하나와 그로 인한 전이가 순서대로, 원인 입력과 함께 남는다. (g) 결정론: 같은 시드와 입력이면 상태와 궤적이 같다. 규칙 코어 테스트는 계속 통과해야 한다.
  마지막 메시지에 상태 전이 표(상태, 조건, 다음 상태, Puppet과 Humanized의 차이)를 적는다."
  ```
  리뷰 포인트: 행동 점수 계산(UtilitySelector, PuppetActions의 점수 부분)이 diff에 없는지. 제외 (d)가 대상·예약·흩어지기 세 곳을 모두 검사하는지. Downed가 되돌아올 길이 정말 없는지.

- [x] **T16a-2 실: 장력, 조율 예산, 실 회수 (Core)** — `f22a46e`
  > T16a-1의 상태 위에 실을 올린다. **장력이 낮아지면 배역이 옅어지고 비틀거리며, 연출가가 한정된 예산으로 다시 조인다.** 비틀거림은 결이 들어가기 전의 임시 끌림이다(결은 T16b). T16a-1에서 이름이 바뀌었다면 명령의 이름도 맞춰 고친다.
  ```powershell
  jevcode run --tag feature "docs/ai-design.md의 '인형 상태' 중 실 장력의 점수 반영, 연출가의 조율 예산, 실 회수, 원인 기록을 src/Core에 구현한다. T16a-1의 개체 상태·입력 API·사건 기록을 쓴다. Unity 코드는 바꾸지 않는다. 공개 API는 유지하고, 새 기능은 추가로만 넣는다.
  1) 장력과 행동 점수: 각 후보의 최종 점수 = Tension × (지금의 점수: 역할 가중치 × 의도 × 요소 × 개체 차이) + (1 - Tension) × 끌림 점수. 이번 run의 끌림은 새 행동 waver 하나다(ActionIds에 추가): 제자리에 멈춰 비틀거림, 이동 없음, 대상 없음, 끌림 점수 WaverPull(기본 1). 다른 후보의 끌림 점수는 0이다. waver의 배역 가중치는 모든 역할 0. 그래서 Tension = 1이면 지금(T15r)과 완전히 같고, 장력이 낮을수록 비틀거림이 이긴다. 결에 따른 끌림은 T16b에서 이 자리에 들어간다.
  2) 조율 예산: 연출가는 매 tick 예산 TuneBudgetPerSecond(기본 0.2) / TicksPerSecond만큼 한 개체의 Tension을 올린다(1을 넘지 않음). 한 tick에 한 개체만 조이고, 그동안 다른 개체는 풀린 채 남는다. 대상은 Active이고 Tension < 1인 개체 중 DirectorProfile의 우선순위 규칙으로 고른다: OutOfPosition(교전 시작 때의 진형 자리에서 가장 먼 개체, 기본값 = 첫 연출가 '무대감독'), LowestTension(장력이 가장 낮은 개체), Attacking(지금 attack 중인 개체 우선, 없으면 LowestTension). 동점은 AgentId가 작은 쪽. 조인 대상과 양을 tick마다 조회할 수 있게 한다(Unity가 '실을 붙잡는 동작'을 그릴 수 있게).
  3) 실 회수: 개체가 Broken이 되면 예산이 ReclaimBudgetPerString(기본 0.1)만큼, Downed가 되면 ReclaimBudgetPerString × HumanizedReclaimRatio(기본 0.5)만큼 늘어난다. Released는 늘지 않는다(실이 끊겼으므로). 늘어난 예산은 TuneBudgetPerSecond × TuneBudgetMaxScale(기본 2)을 넘지 않는다. 회수도 T16a-1의 사건 기록에 (tick, 개체, 늘어난 양, 회수 후 예산)으로 남긴다.
  4) 원인 기록: 선택된 행동이 waver이거나 그 역할의 기본 가중치가 0인 행동이면, 그 개체의 가장 최근 상태 변화 사건(T16a-1의 사건 기록)을 결정에 붙인다. 사건이 없으면 '의도'(블랙보드의 역할별 의도)를 원인으로 붙인다.
  5) 표시용 노출: 최종 점수에서 배역 몫과 끌림 몫, 붙은 원인, 이번 tick의 조율 대상과 현재 예산을 읽을 수 있게 한다.
  6) 시뮬레이션: src/Core.Sim의 --puppets에 --stress <none|tension|mixed> 옵션을 더한다. none = 지금과 같음. tension = 2초에 Attacker Tension -0.8, 4초에 Guard 둘 각각 -0.6. mixed = Healer를 Humanized로 두고 3초에 Healer Tension -1.0(그로기 → 재연결), 5초에 Attacker Health -100(Broken). 0.5초마다 출력에 개체별 Tension·Posture·Status, 조율 대상, 현재 예산을 더하고, 끝에 사건 기록을 출력한다.
  수치는 설정 타입에 두고 임의로 정한 값임을 표시한다.
  테스트: (a) 회귀: Tension이 모두 1이고 입력이 없으면 --puppets 네 시나리오의 궤적(tick별 위치와 행동)이 이 run 이전과 같다. 이전 궤적을 테스트 안에 고정값으로 두지 말고, waver와 조율을 끈 설정으로 같은 시나리오를 돌린 결과와 비교한다. (b) 점수: Tension 0.5에서 waver와 배역 행동의 점수가 식대로. Tension이 낮아질수록 waver 비율이 커진다(0.9, 0.5, 0.1 비교). (c) 조율: 한 tick 증가량이 예산 이하, 한 tick에 한 개체만. 세 우선순위 규칙이 각각 맞는 대상을 고르고 동점은 AgentId. (d) 실 회수: Broken 뒤 예산 +0.1, Downed 뒤 +0.05, Released 뒤 변화 없음, Groggy는 변화 없음. 여러 개체가 부서져도 예산이 기본의 2배에서 정확히 멈춘다. 회수 뒤 남은 개체의 장력이 더 빨리 회복된다. (e) 원인 기록: waver가 선택되면 직전 ReduceTension 사건이 붙는다. (f) 금지 패턴: stress 세 경우 모두에서 진동, 군체 동기화, 겹치기가 통과한다. waver 중인 개체는 무반응 판정에서 빼고, 남은 Active 개체로 판정한다. (g) 결정론: 같은 시드와 입력이면 상태, 예산, 궤적이 같다. 규칙 코어 테스트는 계속 통과해야 한다.
  마지막 메시지에 stress 세 경우의 사건 기록 요약(전이와 회수)과 각 경우의 금지 패턴 지표를 표로 적는다."
  ```
  리뷰 포인트: 회귀 (a)가 실제로 두 설정의 궤적을 비교하는지(같다고 주장만 하지 않는지). `dotnet run --project src/Core.Sim -- --puppets approach --stress tension`과 `--stress mixed`로 장력이 떨어진 개체가 비틀거리다 연출가가 조이면 돌아오는지, 무대감독이 자리를 벗어난 개체부터 조이는지, Attacker가 부서진 뒤 예산이 늘고 Healer 그로기 때는 늘지 않는지 확인한다. Unity 연결 전이므로 Play 확인은 없다.

- [x] **T17 대응을 두 층으로 (Core, 연출가)** — `5802111`. Play: 힐러를 원거리로 치면 그 교전에서 하나가 거리를 유지하며 막아 준다. 시뮬레이터 출력에서는 무대 구성이 한 번도 바뀌지 않아 "번갈아 문제"는 단위 테스트로만 확인됨(T18a에서 다룸). 이후 설계 변경: 역할 → 능력 기반 행동, 시뮬레이션 플레이어와 쌍방 학습(`docs/ai-design.md`). 다음 순서는 T16b-1 → T16b-2 → T18a → T16b-3 → T18b
  > `docs/ai-design.md` "대응: 무대 구성과 행동 의도". T15r Play에서 호위와 돌격이 교전마다 번갈아 나온 원인(대응을 하나만 고름)을 고친다. 무대 구성은 하나, 행동 의도는 섞고, 힐러 위협자는 대응과 독립적으로 반영한다. 원정 사이 기억(개인 일부 유지, 파티 초기화)도 여기서 한다. 원거리 위협자의 독립 반영은 T16b(상황)에서 한다.
  ```powershell
  jevcode run --tag feature --prompt-file docs/runs/T17.md
  ```
  명령 본문과 리뷰 포인트는 `docs/runs/T17.md`에 있다. 리뷰 포인트는 실행기에 보내지 않고 리뷰 화면에 체크리스트로 나온다(jevcode `--prompt-file`).

- [x] **T16b-1 능력: 공유 장비 데이터와 능력이 여는 행동 (Core)** — `3f49355`. Play 변화 없음(프리셋이 예전 역할과 같음)
  > `docs/ai-design.md` "능력 기반 행동". 장비 정의(무기 프로필, 능력 태그, 비용 두 가지), 장비 구성, 장비 프리셋(방패형·근접형·회복형)으로 역할 표를 옮겨 회귀 보존, 막아서기는 `Protect`가 있을 때만, 공격 거리와 이동 속도는 장비에서. 배역 배분과 행동 계약은 T16b-2.
  ```powershell
  jevcode run --tag feature --prompt-file docs/runs/T16b-1.md
  ```

- [x] **T16b-2 배역: 연출가의 배역 배분과 행동 계약 (Core)** — `6cea1e9`. 시뮬레이터(`--loadout mixed --cast`)로 배분 규칙 확인. Unity는 배분 미연결·프리셋이라 Play 변화 없음 → U6. 리뷰 포인트가 확인 수단을 밝히지 않아 확인이 어려웠음 → 위 "리뷰 포인트 쓰는 법". 관찰(원인 미확인): `--puppets healer`에서 Guard가 intercept 상태로 제자리에 서 있음 → T16b-3 전에 프리셋으로도 같은지 확인
  > `docs/ai-design.md` "배역은 행동 계약이다", "연출가: 판을 짜는 존재". 연출가가 장비 능력을 보고 배역을 나눔(Heal → Healer, Protect → Guard 최대 2, 나머지 Attacker, 줄 개체가 없으면 비움), 배역마다 계약(목표 + 우선 → 대체 → 기본 행동), 능력이 없으면 가중치를 대체 행동으로 옮김, 새 기본 행동 pressure(힐러 위협자 압박), 배역 수행 불능 시 정지 금지 패턴. 배분을 부르지 않는 기존 경로는 그대로.
  ```powershell
  jevcode run --tag feature --prompt-file docs/runs/T16b-2.md
  ```

- [x] **U6 장비 라인업과 배역 배분을 화면에 (Unity)** — `2a708a4`. 처음으로 run 뒤 Unity 확인까지 passed. 리뷰 메모: "어느 정도 예상이 가지만, 무기가 다양하지 않아서인지 일반 검과 방패를 든 인형이 힐러 앞 가드를 서는 경우가 있었다"(설계대로: 방패 = Protect). 결론: 지금은 장비보다 **행동 패턴의 다양함**을 보강하는 게 먼저(사용자 판단, 2026-10-05). 순서: T16b-2f → T16b-3a-1(측면 돌기, 포위, 견제) → T16b-3b(결) → T16b-3a-2(치고 빠지기, 모이기) → T16b-3c(상황, 회복·보호) → T18a. 다섯 행동을 한 run에 넣지 않는 이유: 점수 균형이 한꺼번에 흔들리면 원인을 가리기 어렵고, 치고 빠지기·모이기는 금지 패턴과 정면으로 부딪힌다
  > 교전마다 장비 라인업(A 기준 / B 섞음 / C 방패 없음)을 바꾸고 연출가의 배역 배분을 부른다. 자리 번호로 배역을 가정하던 곳(관찰에 넘기는 대상 배역, GuardHealer 진형)을 실제 배역으로 바꾸고, F1 오버레이에 배역·계약 목표·우선/대체·능력·장력을 더한다. Core는 바꾸지 않는다. **Unity 에디터를 열어 둔 채 돌린다**(run 뒤의 Unity 확인이 에디터 연결을 쓴다. T17, T16b-1은 연결이 없어 unknown이었다).
  ```powershell
  jevcode run --tag feature --prompt-file docs/runs/U6.md
  ```

- [x] **T16b-2f 막아서기가 제자리에 멈추는 문제 (Core, fix)** — 거절·되돌림(`20261005-212613-5e9f`, 따로 볼 run: 명령 결함). 검증은 통과했지만 (1) 플레이어가 멈추면 막아서는 인형과 힐러가 좌우로 떪(서로의 현재 위치를 목표로 쫓음), (2) 막아서기 품질 100%·0.005m는 힐러가 탱커 뒤로 줄을 서서 생긴 순환 지표, (3) circle은 막아서기 상황이 없어 0. → T16b-2r
  > T16b-2 리뷰 때 본 관찰. 프리셋으로도 같아서 예전부터 있던 문제다. 막아서기 목표가 사람에게서 멀어지는 방향이면 T15r의 "멀어지지 않기" 규칙이 이동을 막아 제자리에 선다. 목표를 선분 위 가장 가까운 점으로 바꾸고, 막아서기는 그 규칙과 "이유 없는 도망" 판정에서 뺀다. 시뮬레이터에 막아서기 품질 지표를 더한다. T18a의 "배역 목표 달성"이 이 보호를 재므로 그 전에 고친다.
  ```powershell
  jevcode run --tag fix --prompt-file docs/runs/T16b-2f.md
  ```

- [x] **T16b-2r 막아서기와 힐러 거리 유지 (Core, retry)** — `71e7ce0`. Play 정상(정지 시 떨림 없음, 지나쳐도 돌아와 막음). 시뮬레이터: standstill 방향 뒤집힘 0, orbit 100%·평균 0.25m·이동 9.2m(힐러 고정이라 순환 아님), passby 약 2.1초 뒤 복귀 후 힐러와 플레이어 사이에서 공격, 힐러는 8m 물러남. 리뷰 포인트의 "orbit 1.0 미만" 기준은 명령 오류. 비용: 입력 250만 토큰, 19분(지금까지 가장 무거움) → 다음 run부터 더 작게
  > T16b-2f의 재시도. 막아서기 목표는 선분 위 가장 가까운 점 + 여유 구간 0.3m, 힐러는 막아서는 개체가 있으면 Guard 위치를 쓰지 않고 거리 범위 안이면 멈춤(순환 끊기). 새 금지 패턴 "위치 떨림", 새 시나리오 standstill(정지)·orbit(힐러 고정, 탱커만 선을 만들 수 있음)·passby(플레이어가 막아서는 인형들을 지나 힐러에게 감, Play에서 본 "반대편에 서 있음"), 지나쳐진 막아서기는 공격보다 우선하고 플레이어를 비켜 돌아감, 막아서기 품질은 막아서기가 없으면 n/a. Unity 에디터를 열어 두고 돌린다.
  ```powershell
  jevcode run --tag retry --prompt-file docs/runs/T16b-2r.md
  ```

- [x] **T16b-3a-0 플레이어 둘 시나리오와 대상 분포 (Core.Sim)** — `ec97204`. 입력 57만 토큰, 7분(작게 나눈 효과). 기준선: 대상 선택은 "가장 가까운 사람" 하나뿐. duo에서 막아서는 가드만 B를 막고 나머지는 가까운 A를 침(플레이어1 69.3% / 2 30.7%, 몰림 0%는 막아서기 대상이 따로 정해져 있어서). duo-swap에서 위치를 바꾸면 모두가 앞에 선 B에게 몰림(몰림 비율 35.2%)
  > 새 행동(측면 돌기, 포위, 견제) 전에, 플레이어가 둘일 때 지금의 인형들이 누구를 노리고 누구를 막는지 기준선을 만든다. duo(근접 A 정면 + 원거리 B 옆, 힐러 위협자는 B), duo-swap(5초에 위치 교대). 대상 분포·몰림 비율·막아서기 대상 지표. Core 행동 규칙은 바꾸지 않는다. 3a-1을 셋으로 나눈 첫 번째(3a-0 → 3a-1 측면 돌기·견제 → 3a-1b 포위와 협동 구분). T16b-2r이 너무 무거워서 작게 나눴다.
  ```powershell
  jevcode run --tag feature --prompt-file docs/runs/T16b-3a-0.md
  ```

- [x] **T16b-3a-1 측면 돌기와 견제 (Core)** — `1e7a9d3`. 수락했지만 dotnet test 3개 실패(507 중): 2개는 명령의 가중치 오류(무기 선호를 견제에만 걸어 창·활의 견제가 다가가기와 동점, 근접은 0.06 → 견제 0회), 1개는 예전부터 있던 문제(noprotect 플레이어 둘에서 pressure가 "멀어지지 않기"에 묶이고 힐러가 매 tick 떪). 에이전트가 점수를 임의로 올리지 않고 미충족을 그대로 보고함. 비용: 입력 350만 토큰, 18분(27조합 검사 요구가 큼) → T16b-3a-1f
  > "새 행동 추가 절차"대로 새 기본 행동 둘. 측면 돌기: 다른 인형이 정면에서 붙잡은 사람의 옆으로 원호로 돌아 들어감(도는 동안 공격 안 함). 견제: 덜 노려지는 다른 사람을 공격 거리 바로 밖에서 붙잡아 두고, 주기적으로 짧게 찌른 뒤 회복 동안 멈춤(틈). 근접 무기는 측면 돌기, 창·활은 견제를 선호. 기준선 대비 duo-swap 몰림 비율이 낮아져야 한다(견제가 다른 사람을 붙잡음).
  ```powershell
  jevcode run --tag feature --prompt-file docs/runs/T16b-3a-1.md
  ```

- [x] **T16b-3a-1f 가중치, pressure 묶임, 힐러 떨림 (Core, fix)** — 거절(`20261005-230721-92f6`, 따로 볼 run: 명령 결함). 세 수정은 맞게 했지만 516개 중 12개 실패: 예전 궤적 해시 갱신을 명령하지 않음(8), 창 견제 상한 90%는 근거 없는 숫자(실제 99.3%), 근접 Attacker뿐인 duo-swap에 몰림 감소를 요구한 모순, mixed 조합의 금지 패턴(새로 드러남). Play: 새 행동이 동작은 하지만 굼뜨고 불연속적(앞으로 조금 왔다가 몇 초 뒤 물러남) → T16b-3a-1r
  > 3a-1에서 실패한 테스트 셋을 통과시킨다. 무기 선호를 다가가기에도 걸고(창·활은 다가가기 × 0.5), 견제 0.3·측면 돌기 0.3으로 올린다(±15% 개체 차이에도 순서가 안 바뀌게 미리 계산함). pressure를 "멀어지지 않기"에서 빼고, 막아서는 개체가 없을 때도 힐러가 거리 범위 안이고 Guard 뒤 방향에서 20도 안이면 멈춘다. 검사 범위는 바뀐 행동과 관련된 시나리오만 요구한다.
  ```powershell
  jevcode run --tag fix --prompt-file docs/runs/T16b-3a-1f.md
  ```

- [x] **T16b-3a-1r 가중치, pressure 묶임, 힐러 떨림 (Core, retry)** — `1e4043c`. dotnet test 577 통과(실패·건너뜀 0), Unity 통과. 해시 갱신 5개는 모두 힐러 정렬 유지 때문(해시 테스트는 새 행동을 끈 상태), 막아서기 품질은 그대로(healer 80.0%, orbit 100.0%·9.2m). 삭제 테스트 둘은 의도된 것(4(c) 명시, 문제 증명용 테스트 → 반대 테스트로 대체). **판단 거리: 견제가 오히려 몰림을 늘림** — duo mixed cast 몰림 100%(301 tick 내내 모두 B), duo-swap mixed cast 57.5%. "덜 노려지는 사람"을 셀 때 힐러의 거리 유지 대상과 막아서기 대상이 어떻게 세어지는지 의심 → T16b-3a-1b에서 견제 대상 선택과 함께 다룸. 에이전트의 마지막 메시지가 중국어(번체)로 옴 → AGENTS.md에 한국어 규칙 추가 예정. 비용: 396만 토큰, 23분(가장 큼)
  > 3a-1f의 재시도. 세 수정은 같고, 테스트 기준을 바로잡는다: 궤적 해시는 이유와 전후 지표를 적고 갱신(막아서기 품질이 나빠지면 갱신 대신 원인 수정), 점유율은 순서만 검사, 모순된 몰림 감소 테스트는 지우고 보고만, mixed 조합 금지 패턴은 원인이 새 행동 안이면 고치고 밖이면 사유를 적어 건너뜀.
  ```powershell
  jevcode run --tag retry --prompt-file docs/runs/T16b-3a-1r.md
  ```

- [x] **T16b-3a-1b 포위, 협동과 군체 구분, 견제 대상 선택 (Core)** — `e017e3f`. 592 통과, 막아서기 품질 그대로, approach에서 포위 5.0%·자리 겹침 0. 마지막 메시지가 한국어로 옴(AGENTS.md 규칙 효과). 리뷰 포인트 "duo mixed 몰림 100%가 아니다"는 미통과(여전히 100%)지만 수락: 원인은 견제가 아니라 **대상 재선택이 없는 구조** — (1) 첫 대상이 시작 위치의 최근접으로 정해져 바뀌지 않음(근접 Attacker가 옆에 선 B를 끝까지 침), (2) 첫 tick엔 다른 인형의 대상이 비어 견제 대상 계산이 무의미, (3) 공격(0.8)이 견제를 덮고 공격 대상은 "공격 거리 안 최근접". 명령 작성 때 원인을 견제 하나로 좁게 짐작한 설계 실수 → 대상 재선택 fix(U7 뒤). 비용: 347만 토큰, 14분
  > 새 기본 행동 포위: 같은 사람을 압박하는 인형이 셋 이상이면 블랙보드 예약으로 360/N도 자리를 나눠 원호로 둘러쌈(둘이면 측면 돌기의 영역). 군체 동기화는 예약 행동(intercept, surround)으로 바뀐 동시 전환을 협동(coordinated)으로 빼고 셈. 견제 대상은 압박 행동만 세고(keepDistance·intercept 제외) 대기 단계마다 다시 고름(3a-1r의 몰림 100% 원인). 테스트는 불변식만, 분포는 보고.
  ```powershell
  jevcode run --tag feature --prompt-file docs/runs/T16b-3a-1b.md
  ```

- [x] **U7 인형 행동 수치를 Unity에서 조절 (Unity)** — `c5a51ba`. 입력 30만 토큰, 3분. 리뷰 포인트 8개 모두 통과. 자동 연결 43개(PuppetSettings 30, BasicActionSettings 11, SurroundSettings 2), 옮긴 필드 없음(14개 모두 기본값과 같음). 쓰는 법: Prototype 씬에 PrototypeBootstrap 오브젝트를 두고 우클릭 "인형 수치 목록 채우기" → 값 수정 → Play 중 F5 적용 → 좋은 조합은 F6 한 줄. 주의: F5는 PuppetBrain.Create를 다시 불러 진형 자리를 그 순간 위치로 다시 잡는다(교전 시작 직후 F5 권장)
  > **T16b-3a-1b 뒤에 돌린다(2026-10-05 결정: 측면 돌기·견제·포위가 다 들어온 뒤 한 번에 맞춘다). 명령은 자동 연결 방식(설정 이름-값 목록을 Core 생성자 인자 이름으로 맞춰 넣음)으로 고쳐 썼고, F1 라벨이 인형을 가리는 문제(상자를 인형 머리 위로)도 넣었다.** "굼뜨고 불연속적"이라는 Play 피드백을 숫자 하나 바꿀 때마다 run을 돌리지 않고 직접 맞추기 위한 도구. Core의 PuppetSettings·BasicActionSettings 숫자를 인스펙터에 모두 연결(초깃값 = Core 기본값), F5로 지금 교전에 다시 적용, 잘못된 값은 경고 후 이전 값 유지, F6으로 기본값과 다른 값만 한 줄 기록·복사. Core는 바꾸지 않는다. **T16b-3a-1r 뒤에 돌린다**(3a-1r이 새 설정 ApproachNonMeleeScale, KeepAlignTolerance를 더하므로). 좋은 값을 찾으면 그 줄을 주면 기본값 반영 run을 쓴다.
  ```powershell
  jevcode run --tag feature --prompt-file docs/runs/U7.md
  ```

- [x] **T16b-3a-1c 대상 선택: 붐빔과 대상 유지 (Core, fix)** — `d93a28d`. 622 통과, 해시를 하나도 갱신하지 않고 통과(사람 하나 시나리오 불변). duo mixed 몰림 100% → 15.3%, 플레이어1 21.6% → 43.8%, duo-swap mixed 57.5% → 15.3%. 비용: 221만 토큰, 12분. 견제의 성격이 바뀜: 대상이 "가장 덜 압박받는 사람"에서 일반 대상 점수("붐비지 않은 가까운 사람")로, 대기 단계마다 다시 고르던 것이 대상 유지 규칙으로(명령이 3a-1b 규칙과의 관계를 밝히지 않은 결과) → T16b-3b에서 결(집착 ↔ 변덕 등)로 다시 본다
  > 대상이 "가장 가까운 사람"뿐이라 사람이 둘이면 위치만으로 몰림이 정해진다(duo mixed 몰림 100%). 사람이 둘 이상일 때만 대상 점수 = 거리 × 붐빔 배율(이미 둘이 압박 중이면 크게 깎음), 바꿀 때는 1.3배 차이와 30 tick 유지(왔다 갔다 막기), 공격은 지금 대상 우선, 견제는 첫 판단에 대상을 고르지 않고 견제 대상만 친다. 사람이 하나인 시나리오는 해시를 갱신하지 않고 통과해야 한다(강한 회귀 보장).
  ```powershell
  jevcode run --tag fix --prompt-file docs/runs/T16b-3a-1c.md
  ```

- [x] **S1 수치 탐색 (Core.Sim)** — `4928fd9`. 71만 토큰, 9분. 판단 주기를 30 → 10/15로 줄이면 끊김 6 → 0, 반응 지연 26 → 18 tick이지만 금지 패턴 6건 실패(어느 패턴인지 표에 안 나옴). 견제 주기 45는 오히려 끊김 13. 반응 지연 (iii)은 모든 조합 0(지표 설계 미스). Play 메모: "굼뜸 변화 없음, 창으로 공격 후 물러나는 동작이 없다" → 원인은 견제의 Withdraw 단계에서 대상이 공격 거리 안이면 일반 공격으로 바뀌는 규칙(3a-1r에서 무반응 금지 패턴을 통과시키려고 들어감). 끊김 지표가 공격 정지를 정상으로 빼서 이 문제를 못 잡음 → T16b-3a-1d
  > "굼뜨고 불연속적"을 손으로 하나씩 맞추지 않기 위한 도구. `--sweep 이름=값,...`으로 조합을 돌리고(최대 64), 조합마다 금지 패턴 실패, 끊김(움직이다 6~45 tick 멈췄다 다시 움직임, 공격·회복 정지는 제외), 반응 지연(위협 반경 진입부터 대상 행동까지 tick)을 표로 낸다. 각 줄에 U7 F6 형식 줄이 붙어 상위 조합을 Unity에 바로 넣어 비교할 수 있다. Core 규칙은 바꾸지 않는다.
  ```powershell
  jevcode run --tag feature --prompt-file docs/runs/S1.md
  ```

- [x] **T16b-3a-1d 견제가 물러나지 않는 문제 + 탐색 보강 (Core, Core.Sim, fix)** — `2b0ba46`. 수락. 입력 244만 토큰(캐시 235만), 11분. 실행 전 코드 대조로 명령을 고침(`ca39bf5`): 대기 중 일반 공격 검사 순서, 물러남 제한 60 tick(넘으면 찌르기), 간격 기준 시점(거리 맞추기 물러남으로 다시 세지 않음), 교체할 기존 테스트 지정. 테스트 664개 통과, 해시는 duo/duo-swap mixed만 바뀜.
  - Play 메모: "확실히 공격 주기가 줄었다는 체감이 듦".
  - 탐색 결과(판단 주기 10·15·20·30 × persistenceBonus 0.02·0.05): 견제 찌르기 8(15회 합), 대기 7, 제한 1, 일반 공격 0. persistenceBonus는 거의 영향 없음. 반응 지연 (iii)은 모든 조합 `0(벗어남 없음)`, orbit gap은 모든 조합 0.25m(이 탐색 축에 반응하지 않음). 제자리 공격 체류는 약 190~200 tick.
  - 판단 주기 10·15·20의 실패는 모두 **진동 6**(healer, duo-swap × 시드 3). 추적: 개체 11이 판단할 때마다 행동을 바꾼다. ① attack ↔ approach: 공격 거리(2m) 경계에서 움직이는 플레이어를 쫓는 흐름. 주기 30에도 같은 흐름이 있지만 3초 창에 판단이 최대 3번이라 기준(3초에 3회 이하)을 구조적으로 넘을 수 없다. 즉 기준이 판단 주기에 묶여 있다. ② healer의 intercept ↔ attack 교대(1.9m ↔ 1.4m): 진짜 흔들림으로 보인다(S2 사전 추적에서 정정: 한 개체의 흔들림이 아니라 가드 둘이 막아서기 자리를 넘겨받는 현상. 진짜 흔들림은 tension stress의 waver 교대). → 다음 단계 "판단 주기 결정"의 근거.

- [x] **S2 진동 판정 정비 + 27조합 열 (Core.Sim)** — `ede0ce8`. 69만 토큰(캐시 64만), 5분. 675 통과, 해시 불변. 사전 계산과 일치(persistenceBonus 0.02: 주기 30 → 0, 20 → 1 tension/circle 진동, 15 → 1 tension/healer 진동, 10 → 0). 주기 10·15·20의 forbidden 6 → 0. **주기 10만 두 지속 보너스 모두 forbidden 0·contracts27 0·끊김 0**(반응 지연 평균 28 → 21.6 tick, (i)은 35 → 40.8로 약간 늦음). 사전 계산에 없던 것: persistenceBonus 0.05에서 주기 30이 tension/circle 무반응 4, 주기 20은 0 → waver 흔들림이 지속 보너스에 따라 다른 주기로 옮겨 다님. Codex CLI 0.164.0(A-gate 0.155.1과 다름, `sandboxVerified: false`), Unity 확인은 에디터 미연결로 unknown(Unity 변경 없음). → 판단 주기 10 후보, Play 확인 뒤 기본값 반영
  > 진동 판정에서 공격 거리 경계 전환(사람을 쫓는 attack 진입·이탈)을 뺀다. --sweep에 27조합 계약 열과 진동 원인 줄을 더한다. Core는 바꾸지 않고 해시도 그대로. 실행 전 사전 계산: 경계 전환을 빼면 진동 실패는 주기 30: 0, 20: tension/circle, 15: tension/healer, 10: 0. 남은 둘은 막아서기·자리 지키기 점수가 판단마다 0과 양수를 오가고 그 사이를 waver가 채우는 흔들림이다(고치는 것은 다음 단계). 같은 추적에서 none/healer의 가드 둘이 막아서기 자리를 서로 넘겨받는 현상도 봤다: 선 위에서 공격 중인 가드가 막아서기로 치지 않아서 다른 가드가 막아서기로 간다(후보, 아직 run 없음).
  ```powershell
  jevcode run --tag feature --prompt-file docs/runs/S2.md
  ```

- [ ] **D1 판단 주기 기본값 30 → 10 (Core)** — U7 Play(2026-11-08): "확실히 줄었고 리듬 괜찮아지고 덜 떤다". 기본값 하나만 바꾼다. 기본 설정으로 도는 해시는 갱신하되 시나리오마다 전후 지표를 적고, 금지 패턴 실패나 막아서기 품질 저하면 멈추고 보고. 사전 기대: 탐색 baseline 행 = S2의 주기 10 행(forbidden 0, stutter 0, mean 21.61, contracts27 0). 씬의 PuppetTuning 목록에 30이 저장돼 있으면 덮어쓰므로 확인.
  ```powershell
  jevcode run --tag feature --prompt-file docs/runs/D1.md
  ```

- [ ] **U8 마리오네트 모양과 행동 자세 (Unity)** — 상자로는 동작이 읽히지 않는다는 Play 피드백(2026-11-08). 결(T16b-3b)의 미묘한 차이를 Play로 보려면 먼저 행동이 보여야 해서 3b 앞에 둔다. 상자는 콜라이더·연결부 기준으로 남기고 시각 자식만 더한다. 이동 방향(앞으로 숙임/뒤로 젖힘), 공격 준비와 바닥 경고 동기화, 견제 네 단계, 막아서기 자세, F2로 상자와 비교. Unity에서는 장력·피해가 연결되지 않아 waver·그로기·쓰러짐 자세는 범위 밖. Blender 모델(따로 조각난 마리오네트 부위)은 이걸로 Play한 뒤 부족하면 검토한다. **Unity 에디터를 열어 두고, MCP for Unity와 Blender MCP는 끄고 돌린다.**
  ```powershell
  jevcode run --tag feature --prompt-file docs/runs/U8.md
  ```

## 참고

- **Unity 클라이언트에서 할 일(규칙 코어 범위 밖):** 간섭 방식마다 색과 소리를 고정한 신호 언어. 강한 명령과 카운터 기회에 필요한 실만 선명하게 표시.

- **순서대로 진행한다.** 뒤 작업이 앞 작업의 타입을 쓴다. 앞 작업에서 이름이 바뀌었다면 명령의 이름도 맞춰 고친다.
- **기본값은 모두 임시값이다.** 임계값, 비용, 감쇠, 불변식 최소값은 재미 검증 뒤 조정할 대상이다. 설정 객체로 분리돼 있으면 코드 수정 없이 바꿀 수 있다.
- **태그:** 기능 `feature`, 거절 후 재시도 `retry`, 수정 `fix`, 정리 `refactor`, 파일을 바꾸지 않는 분석·설계 `analysis`.
- **실행기 전환 실험:** 한도가 끝나 실행기를 바꿀 때는 `--tag switch-cold`와 `--tag switch-brief`로 한 번씩 돌린다(jevcode A0 README 참고).
