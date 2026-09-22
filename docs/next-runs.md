# 다음 jevcode 실행 목록

규칙 코어의 첫 목표: **카운터로 합동 공격을 끊는 규칙**과 **적이 전술을 관찰해 대응을 바꾸는 규칙**을 Unity 없이 테스트로 고정한다(기획 문서의 "첫 플레이 검증 후보"). 공통 규칙, 용어, 테스트 실행은 `AGENTS.md`에 있으므로 명령에는 할 일과 정의만 적는다.

## 명령을 쓰는 원칙

T4 첫 시도가 "원거리 지속"을 다르게 해석해서 거절된 뒤 정한 원칙이다.

- **용어는 계산식으로 정의한다.** "~이면", "약하면", "최근일수록" 같은 말은 식이나 기준값으로 바꾼다.
- **기획 수치는 설정 객체로 주입하고 기본값을 명령에 적는다.** Codex가 임의로 정하게 두지 않는다. 기본값은 모두 임시값이다.
- **테스트는 경계를 지정한다.** 기준 바로 아래, 정확히 기준, 입력 0개, 설정 변경.
- **마지막 메시지에 계산 방식을 설명하게 한다.** 리뷰할 때 코드를 읽지 않고도 확인할 수 있도록.

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

- [ ] **T5 연출가 대응 선택**
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

- [ ] **T6 원정 내 기억**
  ```powershell
  jevcode run --tag feature "Expedition 추가. 한 원정 안에서 교전이 끝날 때마다 BehaviorProfile을 기억에 합치고, 다음 교전의 대응은 기억된 프로필로 고른다.
  합치는 식: 첫 교전은 기억 = 이번 프로필. 이후에는 각 경향마다 기억 = Decay × 기억 + (1 - Decay) × 이번 값. Decay는 ExpeditionSettings로 주입하며 기본 0.5, 허용 범위 0 이상 1 미만(범위 밖이면 생성 거부). 이 식은 0~1 값의 가중 평균이라 결과도 0~1이다.
  EndExpedition 후에는 기억을 비우고, 새 원정의 첫 교전은 기억 없이 StandardFormation으로 시작한다.
  테스트: 첫 교전, 두 번째와 세 번째 교전의 계산값, Decay 0과 0.99, 범위 밖 Decay 거부, 원정 종료 후 초기화, 원정을 넘어 기억이 남지 않음.
  마지막 메시지에 합치는 식과 예시 계산 한 줄을 적는다."
  ```
  리뷰 포인트: 첫 교전 처리, Decay 경계.

- [ ] **T7 대응의 공격 구성과 공정성 불변식** (기획 확인 필요)
  > T5는 대응을 고르기만 해서, "대응이 바뀌어도 카운터 규칙은 같다"를 검사할 대상이 없다. 아래는 대응이 합동 공격에 영향을 주는 최소한의 연결 방식 제안이다. 기획 의도와 다르면 고쳐서 쓴다.
  ```powershell
  jevcode run --tag feature "대응이 합동 공격 구성을 정하는 JointAttackPlan 추가. Tactic마다 Plan(기준 공격 설정)을 만든다: 전조 길이, 카운터 창 길이, 노출할 ConnectionPoint 목록. 대응은 전조 길이를 PreparationTickCost만큼 늘리고, 노출 연결부의 위치와 개수를 바꿀 수 있다.
  공정성 불변식(FairnessRules, 기획 임시값): 카운터 창 길이 MinCounterWindowTicks(기본 10) 이상, 노출 연결부 1개 이상, 연결부 종류별 허용 간섭 방식(Cut/Shoot/Disrupt)은 ConnectionPointCatalog에 고정되어 대응이 바꿀 수 없다.
  CounterResolver의 판정은 공격 상태, 시도 tick, 간섭 방식, 대상 연결부만으로 정해지며 어떤 대응이 만든 공격인지와 무관해야 한다.
  테스트: 모든 대응 x 여러 프로필 x 시드 조합으로 만든 Plan이 불변식을 지키는지, 같은 카운터 시도가 대응과 무관하게 같은 판정을 받는지. 불변식을 어기는 Plan은 생성 단계에서 거부.
  규칙 코드를 바꿔야 하면 이유를 마지막 메시지에 남긴다."
  ```
  리뷰 포인트: 불변식이 실제로 대응을 바꿔 가며 검사되는지(형식적인 테스트가 아닌지), 기획 의도와 맞는지.

- [ ] **T8 콘솔 시뮬레이터 (선택)**
  ```powershell
  jevcode run --tag feature "src/Core.Sim 콘솔 앱(net10.0) 추가. 인자: --seed <정수> (기본 1), --encounters <정수> (기본 3).
  플레이어 전략 두 개: AreaFirst(공격의 70%가 적 3명을 맞히는 광역 근접), HealerFirst(공격의 70%가 Healer를 노리는 원거리 단일). 나머지 30%는 시드 기반 무작위로 역할과 거리를 고른다. 교전당 공격 20회, 합동 공격 2회.
  한 원정을 시뮬레이션하고 교전마다 한 줄씩 출력한다: 교전 번호, 관찰된 세 경향(소수 둘째 자리), 기억 반영 후 고른 대응, 합동 공격 카운터 성공 수/시도 수. 카운터 시도는 시드 기반으로 창 안/밖을 섞는다.
  솔루션에 포함한다. 테스트는 같은 시드면 출력이 같고, 다른 시드면 다를 수 있음을 확인한다(결정성).
  마지막 메시지에 기본 인자로 실행한 출력 예시를 붙인다."
  ```
  리뷰 포인트: 직접 실행해서 교전이 진행될수록 대응이 바뀌는지 눈으로 확인. 재미 검증 전에 규칙을 감각으로 확인하는 용도다.

## 참고

- **순서대로 진행한다.** 뒤 작업이 앞 작업의 타입을 쓴다. 앞 작업에서 이름이 바뀌었다면 명령의 이름도 맞춰 고친다.
- **기본값은 모두 임시값이다.** 임계값, 비용, 감쇠, 불변식 최소값은 재미 검증 뒤 조정할 대상이다. 설정 객체로 분리돼 있으면 코드 수정 없이 바꿀 수 있다.
- **태그:** 기능 `feature`, 거절 후 재시도 `retry`, 수정 `fix`, 정리 `refactor`, 파일을 바꾸지 않는 분석·설계 `analysis`.
- **실행기 전환 실험:** 한도가 끝나 실행기를 바꿀 때는 `--tag switch-cold`와 `--tag switch-brief`로 한 번씩 돌린다(jevcode A0 README 참고).
