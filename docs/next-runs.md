# 다음 jevcode 실행 목록

규칙 코어의 첫 목표: **카운터로 합동 공격을 끊는 규칙**과 **적이 전술을 관찰해 대응을 바꾸는 규칙**을 Unity 없이 테스트로 고정한다(기획 문서의 "첫 플레이 검증 후보"). 공통 규칙, 용어, 테스트 실행은 `AGENTS.md`에 있으므로 명령에는 할 일과 정의만 적는다.

## 명령을 쓰는 원칙

T4 첫 시도가 "원거리 지속"을 다르게 해석해서 거절된 뒤 정한 원칙이다.

- **용어는 계산식으로 정의한다.** "~이면", "약하면", "최근일수록" 같은 말은 식이나 기준값으로 바꾼다.
- **기획 수치는 설정 객체로 주입하고 기본값을 명령에 적는다.** Codex가 임의로 정하게 두지 않는다. 기본값은 모두 임시값이다.
- **테스트는 경계를 지정한다.** 기준 바로 아래, 정확히 기준, 입력 0개, 설정 변경.
- **마지막 메시지에 계산 방식을 설명하게 한다.** 리뷰할 때 코드를 읽지 않고도 확인할 수 있도록.

**여러 줄 명령은 PowerShell에서 실행한다.** cmd.exe는 첫 줄바꿈 뒤를 잘라 버린다(T4 retry 두 번이 이 때문에 정의 없이 전달됨). run 시작 시 출력되는 `task: N자, 끝: ...`가 명령의 마지막 문장과 같은지 확인한다.

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

- [ ] **U3e 연결부를 몸통 면에 붙이고 가림을 고친다 (U3 단계 마무리)**
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

- [ ] **U4 연출가의 적응을 화면에서 읽기**
  ```powershell
  jevcode run --tag feature "프로토타입에 원정 루프를 붙인다. 교전 = 합동 공격 3사이클. 교전이 끝나면 Expedition에 BehaviorProfile을 합치고, 다음 교전의 대응(Tactic)과 JointAttackPlan을 정한다.
  플레이어 공격(CombatObserver에 기록): Space=광역 근접(반경 3m 안의 인형 모두), F=원거리 단일(가장 가까운 인형), 마우스 왼쪽 클릭=근접 단일. 공격 대상 역할은 인형마다 고정(앞 2개 Guard, 뒤 왼쪽 Attacker, 뒤 오른쪽 Healer, Healer는 초록색).
  대응을 진형으로 보여준다: SpreadFormation은 인형 간격 2배, GuardHealer는 Guard 하나가 Healer 앞으로, ShieldAdvance는 인형들이 플레이어 쪽으로 2m 전진, StandardFormation은 기본 진형. 연결부 위치(앞/옆/뒤)는 U3e에서 만든 몸통 면 배치를 그대로 쓴다.
  교전 사이에 2초간 화면 가운데에 '연출가가 각본을 고친다: <대응 이름>'과 기억된 세 경향을 표시한다.
  PrototypeSettings에 교전당 사이클 수를 둔다. 규칙 코어 테스트는 계속 통과해야 한다."
  ```
  리뷰 포인트: 광역만 쓰다가 회복 역할 노리기로 바꿨을 때, **한 교전 뒤에** 진형이 바뀌는 게 눈에 보이는지(T11에서 본 적응 지연). 그게 재미있는지, 답답한지.

## 참고

- **Unity 클라이언트에서 할 일(규칙 코어 범위 밖):** 간섭 방식마다 색과 소리를 고정한 신호 언어. 강한 명령과 카운터 기회에 필요한 실만 선명하게 표시.

- **순서대로 진행한다.** 뒤 작업이 앞 작업의 타입을 쓴다. 앞 작업에서 이름이 바뀌었다면 명령의 이름도 맞춰 고친다.
- **기본값은 모두 임시값이다.** 임계값, 비용, 감쇠, 불변식 최소값은 재미 검증 뒤 조정할 대상이다. 설정 객체로 분리돼 있으면 코드 수정 없이 바꿀 수 있다.
- **태그:** 기능 `feature`, 거절 후 재시도 `retry`, 수정 `fix`, 정리 `refactor`, 파일을 바꾸지 않는 분석·설계 `analysis`.
- **실행기 전환 실험:** 한도가 끝나 실행기를 바꿀 때는 `--tag switch-cold`와 `--tag switch-brief`로 한 번씩 돌린다(jevcode A0 README 참고).
