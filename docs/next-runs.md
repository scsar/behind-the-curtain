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

- **거절한 경우:** `git restore .`, `git clean -nd`로 확인 후 `git clean -fd`. 명령을 고쳐 `--tag retry`로 다시 run한다.
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
3. 수락하고 커밋한다.
4. Unity가 새로 만든 .meta 파일은 따로 커밋한다: `git add -- '*.meta'` 후 `git commit -m "chore(client): Unity meta"`.

- [ ] **U1 규칙 코어를 Unity 로컬 패키지로 연결**
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

- [ ] **U2 쿼터뷰 장면과 이동**
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

- [ ] **U3 합동 공격과 카운터 입력**
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

- [ ] **U4 연출가의 적응을 화면에서 읽기**
  ```powershell
  jevcode run --tag feature "프로토타입에 원정 루프를 붙인다. 교전 = 합동 공격 3사이클. 교전이 끝나면 Expedition에 BehaviorProfile을 합치고, 다음 교전의 대응(Tactic)과 JointAttackPlan을 정한다.
  플레이어 공격(CombatObserver에 기록): Space=광역 근접(반경 3m 안의 인형 모두), F=원거리 단일(가장 가까운 인형), 마우스 왼쪽 클릭=근접 단일. 공격 대상 역할은 인형마다 고정(앞 2개 Guard, 뒤 왼쪽 Attacker, 뒤 오른쪽 Healer, Healer는 초록색).
  대응을 진형으로 보여준다: SpreadFormation은 인형 간격 2배, GuardHealer는 Guard 하나가 Healer 앞으로, ShieldAdvance는 인형들이 플레이어 쪽으로 2m 전진, StandardFormation은 기본 진형. JointAttackPlan의 연결부 위치(앞/옆/뒤)를 실제 인형 기준 위치로 배치한다.
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
