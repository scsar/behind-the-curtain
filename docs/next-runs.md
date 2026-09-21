# 다음 jevcode 실행 목록

규칙 코어의 첫 목표: **카운터로 합동 공격을 끊는 규칙**과 **적이 전술을 관찰해 대응을 바꾸는 규칙**을 Unity 없이 테스트로 고정한다(기획 문서의 "첫 플레이 검증 후보"). 공통 규칙, 용어, 테스트 실행은 `AGENTS.md`에 있으므로 명령에는 할 일만 적는다.

## 매번의 흐름

```
jevcode run → 리뷰 화면(jevcode-ui.bat)에서 확인 → [수락하고 커밋] 또는 거절 후 되돌리기 → 다음 run
```

- **거절한 경우:** 터미널에서 `git restore .`, `git clean -fd`로 되돌린 뒤, 문제를 반영해 같은 작업을 다시 run한다.
- **실패·거절 리뷰:** 원인과 "이 화면만으로 설명할 수 있었나"를 꼭 남긴다.
- **범위 밖 제안:** 마지막 메시지의 "제안" 섹션에 나온 항목은 이 목록에 추가하거나 다음 run으로 돌린다.
- **직접 작업한 경우:** jevcode 없이 코드 작업을 했다면 리뷰 화면의 "직접 사용 기록"을 누른다.

## 시작 전 (한 번)

```powershell
cd C:\Github\behind-the-curtain
jevcode init --verify "dotnet test tests/Core.Tests --no-restore" --timeout-min 30
```

## 목록

- [x] **T1 뼈대 + TickWindow** — 완료 (`39c326a`, run `20260922-003333-b2b3`). 리뷰는 아직 남음.

- [ ] **T2 합동 공격 수명주기**
  ```powershell
  jevcode run --tag feature "JointAttack 추가: 상태는 Telegraphing -> CounterWindowOpen -> Executed 또는 Broken. 생성 시 전조 시작 tick, 카운터 창(TickWindow), 실행 tick을 받는다. Advance(tick)로 진행하고, 창 안에서 Break()하면 Broken, 창이 지나면 Executed. 잘못된 전이는 결과값으로 거부. 상태 전이 테스트 전부."
  ```
  리뷰 포인트: 창의 시작·끝 tick에서의 전이, 이미 끝난 공격에 Advance·Break 호출.

- [ ] **T3 카운터 판정**
  ```powershell
  jevcode run --tag feature "카운터 판정 추가: JointAttack은 노출된 ConnectionPoint를 1개 이상 가지며, 각 연결부는 끊을 수 있는 간섭 방식(Cut/Shoot/Disrupt)을 가진다. CounterResolver.Resolve(attack, 시도 tick, 간섭 방식, 대상 연결부)는 성공/실패와 실패 사유(창 밖, 방식 불일치, 이미 사용됨, 연결부 없음)를 돌려준다. 한 카운터 창에서는 한 번만 성공. 사유별 테스트."
  ```
  리뷰 포인트: 성공 시 T2의 Break와 연결되는지, 실패 사유가 빠짐없이 구분되는지.

- [ ] **T4 플레이어 행동 기록**
  ```powershell
  jevcode run --tag feature "CombatObserver 추가: 교전 중 이벤트(광역 공격으로 맞힌 적 수, 공격 대상 역할 Healer/Guard/Attacker, 공격 거리 Melee/Ranged)를 받아 교전 종료 시 BehaviorProfile을 만든다. 최소 세 가지 경향을 0~1로: 광역 의존도, 회복 역할 집중도, 원거리 지속 공격 비율. 계산 규칙을 테스트로 고정."
  ```
  리뷰 포인트: 이벤트가 0개일 때, 경향 값이 0~1을 벗어나지 않는지.

- [ ] **T5 연출가 대응 선택**
  ```powershell
  jevcode run --tag feature "ITacticSelector와 규칙 기반 RuleTacticSelector 추가: BehaviorProfile을 받아 대응 하나를 고른다. 광역 의존이면 SpreadFormation(약점: 떨어진 인형 고립), 회복 집중이면 GuardHealer(약점: 호위 이동 시 다른 연결부 노출), 원거리면 ShieldAdvance(약점: 측면). 각 Tactic은 비용과 약점을 데이터로 가진다. 경향이 약하면 기본 대응 유지. 임계값과 우선순위를 테스트로 고정."
  ```
  리뷰 포인트: 임계값이 상수로 분리됐는지, 두 경향이 동시에 강할 때의 우선순위.
  > `ITacticSelector`는 나중에 게임 안에서 Jev를 시험할 자리다. 규칙과 Jev를 같은 인터페이스로 비교할 수 있다.

- [ ] **T6 원정 내 기억**
  ```powershell
  jevcode run --tag feature "Expedition 추가: 한 원정 안에서 교전이 끝날 때마다 BehaviorProfile을 누적해 다음 교전의 대응 선택에 쓰고, 최근 교전일수록 가중치를 높인다(감쇠 계수는 설정값). 원정이 끝나면 기억 초기화. 원정을 넘어 기억이 남지 않는다는 것도 테스트."
  ```
  리뷰 포인트: 첫 교전(기억 없음)의 동작, 감쇠 계수 경계값.

- [ ] **T7 공정성 불변식 테스트**
  ```powershell
  jevcode run --tag feature "공정성 원칙을 테스트로 고정: 적이 어떤 대응을 고르든 카운터 신호의 의미와 판정 규칙은 바뀌지 않고, 이미 익힌 간섭 방식이 설명 없이 무효화되지 않는다. 여러 프로필과 시드 조합으로 대응을 바꿔 가며 같은 카운터 시도가 같은 판정을 받는지 검증. 규칙 코드를 바꿔야 하면 이유를 남긴다."
  ```
  리뷰 포인트: 테스트가 실제로 대응을 바꿔 가며 검사하는지(형식적인 테스트가 아닌지).

- [ ] **T8 콘솔 시뮬레이터 (선택)**
  ```powershell
  jevcode run --tag feature "src/Core.Sim 콘솔 앱(net10.0) 추가: 미리 짠 플레이어 전략 두 개(광역 위주, 회복 집중)로 한 원정의 교전 3회를 시뮬레이션하고, 교전마다 관찰된 프로필, 연출가의 대응, 카운터 성공 여부를 표로 출력. 시드를 인자로 받는다. 솔루션에 포함하고, 테스트는 결정성(같은 시드면 같은 출력)만."
  ```
  리뷰 포인트: 직접 실행해서 적응 변화가 눈에 보이는지. 재미 검증 전에 규칙을 감각적으로 확인하는 용도다.

## 참고

- **순서대로 진행한다.** 뒤 작업이 앞 작업의 타입을 쓴다.
- **대응 종류, 경향, 임계값은 초안이다.** 기획 문서의 예시를 옮긴 것이라 재미 검증 뒤 바뀔 수 있다. 명령은 자유롭게 고쳐 쓴다.
- **태그:** 기능 `feature`, 수정 `fix`, 정리 `refactor`, 파일을 바꾸지 않는 분석·설계 `analysis`.
- **실행기 전환 실험:** 한도가 끝나 실행기를 바꿀 때는 `--tag switch-cold`와 `--tag switch-brief`로 한 번씩 돌린다(jevcode A0 README 참고).
