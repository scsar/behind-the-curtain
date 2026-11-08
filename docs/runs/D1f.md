판단 주기 기본값이 30에서 10으로 바뀐 뒤(`ad3ff80`, D1) 실패하는 테스트 16개를 고친다. **src/Core와 src/Core.Sim은 바꾸지 않는다. 테스트만 바꾼다.** 지금 `dotnet test tests/Core.Tests --no-restore -m:1`은 675개 중 16개가 실패한다: 궤적 해시 14개, AttackRequiresHumanInRangeAndDoesNotMoveOrDamage, PassbyRecoversWithoutCrossingPlayer. 이 16개 말고 다른 테스트가 실패하면 고치지 말고 멈춘 뒤 보고한다.

1) PuppetBrainTests.AttackRequiresHumanInRangeAndDoesNotMoveOrDamage: 이 테스트는 사람을 공격 거리 바로 밖(2.001)으로 옮긴 뒤 30 tick을 돌리고 "공격 불가, 다가가기"를 기대한다. 판단 주기가 30이면 그 안에 판단이 한 번뿐이라 맞았지만, 10이면 다가가서 다시 공격 거리에 들어가므로 기대가 깨진다. 테스트의 의도("공격 거리를 벗어나면 다음 판단에서 공격 후보가 안 되고 다가가기를 고른다")를 판단 주기와 상관없이 검사하도록, `Run(brain, 1, 30)`을 다음 판단 시점까지만 돌리게 바꾼다: `Run(brain, 1, new PuppetSettings().ReevaluationIntervalTicks)`(또는 같은 값). 나머지 단언은 그대로 둔다. 판단 주기를 테스트 안에 숫자로 고정하지 않는다.
2) InterceptRegressionTests.PassbyRecoversWithoutCrossingPlayer: 판단 주기 10에서는 사람에게 지나쳐진 가드(10)가 사람을 쫓아가 공격하고, 다른 가드(11)가 막아서기를 넘겨받는다. 이 넘겨받기도 정상으로 본다(2026-11-08 결정). 바꾼다:
   - Gap: 가드 10 하나 대신, passby 시나리오에서 Protect 능력을 가진 개체(지금 10과 11) 각각의 '힐러(13)-사람 선분'까지 거리 중 **작은 값**. 어떤 개체가 Protect를 갖는지는 시나리오의 장비에서 읽거나, 10과 11로 적고 그 이유를 주석에 남긴다.
   - 복귀 기한: `crossed + 60`을 `crossed + PassbyRecoveryDeadlineTicks`로 바꾸고 이 상수를 90(3초, 임의값. T16b-2r 관찰 약 2.1초와 주기 10의 실측 약 2.5초를 덮는 값)으로 둔다. 상수 옆에 임의값이라는 주석을 단다.
   - 지나쳐진 뒤 Gap > 1인 순간이 없으면(바로 넘겨받음) 복귀는 crossed로 본다(First가 예외를 던지지 않게).
   - "복귀 뒤 80% 이상 유지"와 "가로지르지 않음(사람과 0.5m 이상)"은 그대로 두되, 가로지르지 않음은 Protect 개체 둘 모두에 적용한다.
   - 실패 메시지에 crossed, departed, recovered, 유지 비율, 막아서기를 맡은 개체 id를 넣는다.
3) 궤적 해시 14개: DuoSimulationTests.LegacySevenTrajectoriesHaveReviewedHashes(7), InterceptRegressionTests.NonInterceptTrajectoryHasReviewedHash(3), TargetSelectionTests.DuoTrajectoriesHaveReviewedHashes(4). 기대 해시 문자열만 새 값으로 바꾼다. 시나리오, 설정, 검사 줄은 바꾸지 않는다. 판단 주기를 명시한 해시 테스트는 바뀌면 안 된다.
   갱신한 해시마다 근거 표에 시나리오 이름과, `dotnet run --project src/Core.Sim -- --puppets <시나리오>`(Duo는 해당 loadout 옵션 포함)를 판단 주기 30(`ad3ff80` 이전 기본값, 설정으로 주어 재현)과 10(현재 기본값)으로 돌린 금지 패턴 지표(도망 tick, 최대 행동 전환, 동시 전환 비율, 막아서기 인원 최대, 무반응)와 막아서기 품질(within1m)을 전후로 적는다. 금지 패턴 지표가 하나라도 기준을 넘으면 그 해시는 갱신하지 말고 보고한다.
4) 전체: `dotnet test tests/Core.Tests --no-restore -m:1`이 675개 모두 통과해야 한다(실패 0, 건너뜀 0). 테스트를 지우거나 Skip하지 않는다.
마지막 메시지에 '검증 근거' 표를 둔다: 1)~4) 각각 바꾼 파일과 테스트 이름. 2)의 crossed, recovered, 유지 비율, 막아서기를 맡은 개체 id 실제 값. 3)의 테스트별 갱신 개수.

## 리뷰 포인트
- [리뷰 화면] 변경 파일 → tests/Core.Tests 아래만 바뀌었고 src/ 아래 파일이 없다
- [리뷰 화면] 검증 → dotnet test passed(675 통과, 실패 0, 건너뜀 0)
- [리뷰 화면] 삭제된 테스트 → 없다(⚠ 표시가 없다)
- [리뷰 화면] 해시 테스트 파일의 diff → 바뀐 줄이 기대 해시 문자열뿐이다
- [리뷰 화면] AttackRequiresHumanInRangeAndDoesNotMoveOrDamage diff → 바뀐 것은 돌리는 tick 수(다음 판단 시점)뿐이고 단언은 그대로다
- [근거 표] passby 실제 값 → recovered - crossed가 90 이하이고(예상 약 74), 유지 비율 80% 이상, 막아서기를 맡은 개체에 11이 있다
- [근거 표] 갱신한 해시마다 주기 30 → 10 전후 지표가 있고 금지 패턴 지표가 모두 기준 안이다
