# 최신 dev 통합 검사 (2026-10-29)

## 범위·결과
- 작업 브랜치: `feat/tight-rope/enhancement`, 담당: @lakuase2. 사용자 요청에 따라 저장·push·dev PR 제출. dev 직접 병합은 범위 밖이다.
- 병합 대상: `origin/dev` `93490861`(PR #232 포함). 출발 HEAD `0c5a1eb4`. 기존 PR #226은 이미 MERGED이며 이번 기능 차이는 후속 해제봉·전광판·봉 위험 회복이다.
- `git merge --no-commit --no-ff origin/dev`: `PlayerBalance.cs` 자동 병합, 미해결 충돌 0개. 씬·프리팹 수동 YAML 편집 없음.
- dev의 `IsSuspended`·`GaugeFallCatcher`·M2 구조 기능과 `RecoverWhileSupported`를 함께 유지했다. 회복 테스트를 `ContinueWhileAirborne=true`, `IsSuspended=true`인 실제 봉 바인딩 조건으로 보강했다.
- `_ThirdParty`는 dev 지정 `f5131050`으로 동기화하고 LFS pull했다. private origin/main 조상 확인, private 파일 변경 없음.

## 검사
- doctor(-SkipEditor): FAIL 0 / WARN 1(unity-cli 문서 버전 차이). 이번 작업에서 한 번 실행. 검사는 연결된 Unity MCP 사용.
- Unity 6000.3.25f1 컴파일 PASS. 시작 씬 Start, dirty 0. eval 최초 timeout 후 에디터 포커스로 연결 복구.
- EditMode `Tightrope`: 274/274 PASS. 보강 후 재실행도 274/274.
- PlayMode `Relay`: 1/2 PASS, 1 FAIL.
- PlayMode `Trapeze`: 6/9 PASS, 3 FAIL.
- PlayMode `Swing`: 2/6 PASS, 4 FAIL(인간 대포 1건 포함).
- PlayMode `PerformanceSceneBindingPlayTests`: 13/13 PASS.
- PlayMode `BalanceRiskBlinkPlayTests`: 2/2 PASS.
- PlayMode `Hang`: 부분 문자열 필터가 Change/Changes까지 포함해 25건 실행, 23 PASS / 2 FAIL. 이 결과를 외줄 매달림 전용 검사로 해석하지 않는다. 실제 매달림 사망·팔 접촉 표현 및 telemetry hanging timeout 항목은 PASS.
- `check-work.ps1`: 안전성 PASS, 문서/규칙 PASS, 컴파일 PASS, 자동 전체 테스트 SKIPPED(선택 안 함). 길이 WARN 19 / hard 0. 별도 실행 테스트는 위 결과를 따른다.

## 최초 통합 시 실패·미검증 (수정 전 기록)

아래는 Draft 제출 당시 결과다. 후속 수정과 재검사는 마지막 절을 따른다.
- `Multiplayer_RelayGrabWaitsForPositionButExpiresAndCancelsAcrossReset`: :37 pending 1 기대 / 0 실제. 기존 2026-10-28 log와 같은 실패. 실제 잡기·기한 만료·리셋 검증 전에 중단.
- `PoleInteraction_TrapezeUsesExistingOutlineAndFPrompt`: :25 true 기대 / false. `TrapezeCatchWindowStartsAtHalfRangeAfterFastLaunch`: :97 F 입력 true 기대 / false. `TrapezeEarlyFDoesNotPullAFallingPlayerUpAndAbortClearsTheRequest`: :71 true 기대 / false. 기존 log의 3건과 동일.
- `PoleInteraction_SwingUsesExistingOutlineAndFPrompt`: `TightropeSwingPolePlayTests.cs:46`, 기존 공용 UI 외곽선 후보 검사 실패. 신규 봉 회복 호출 전 경로.
- `SwingPoleRejectsAnotherOwnerAndRestartRestoresTheHeldPlayersControls`, `SwingPoleUsesCannonMomentumAndAirControlThenTeammateCatchesTheLanding`: F 탑승 확정 timeout. fixture·입력·소유권 판정은 dev와 동일하며 신규 회복은 OwnHeld 이후만 호출된다. 일시적 점유 여부까지 런타임 추적하지 않아 원인 확정은 아님.
- 인간 대포 `PoleSaw_ActualSwingAvoidsContact_ThenCrossingCutsWithoutForcingRelease`: `FinalCannonPlayTests.Enhancement.cs:124`, 해당 Pole 참조를 가진 활성 PoleSaw를 Single로 찾지 못함. 씬·생산 코드·fixture는 dev와 동일.
- 추가 부분 문자열 실행 실패: 인간 대포 `OriginalCourse_DifficultyChangesBeforeLaunch_ThenLocks`(:17, 10 기대 / 7 실제), 외줄 `PracticeBotChangesUpperRoleByNormalReleaseWithoutRestartingCourse`(목마 연결 timeout). 원인 미확정, 이번 제출에서 임의 수정하지 않음.
- dev 단독 별도 실행은 하지 않았다. 정적 비교/과거 기록은 dev 재현 확인을 대체하지 않는다. 전체 PlayMode/전체 EditMode 성공을 주장하지 않는다.
- 실제 2인 네트워크 연결·무작위 줄 분포·전광판 플레이 시인성·봉 회복 체감은 사람 확인 필요. dev 병합 전 담당자 검토 대상이다.

## 제출 경로
- `Scripts/Lib/common.ps1`의 `Get-PrFlow` 정규식은 중첩 브랜치 이름을 거부한다. 기존 PR #226과 같이 저장소 PR 템플릿에 맞춰 `gh pr create --base dev --head feat/tight-rope/enhancement`를 사용한다. 브랜치 이름이나 하네스를 임의 수정하지 않는다.
- 병합 방식 Squash, 지정 PM 팀 검토 요청. 테스트 실패·미검증을 PR 본문에 공개한다.

## 실패 후속 수정 (2026-10-29)

### 확인된 원인과 수정
- Relay 대기 요청: `entry - 2m`가 현재 잡기 반경 안이라 즉시 잡혀 pending 0개였다. 4m 밖에서 요청하고 실제 위치 도착·중복 기한·만료·놓기 취소·재시작을 검사한다.
- Swing 탑승 및 봇 역할 교대: `Running`/`PerformanceOpen`은 배치 확인·입력 재개 완료를 보장하지 않는다. 공통 BeginPerformance가 참가자 배치 확인과 InputGate 재개를 기다린다.
- Swing/Trapeze UI: 입력 리더 활성화 직후 gate 재무장 프레임과 F 안내 alpha 0의 진입 연출을 기다리지 않았다. 기존 UI를 그대로 사용하고 조건 대기·카메라 시점 유지만 보강했다.
- Trapeze UI: 첫 봉을 혼자 점프해서 잡던 낡은 fixture를 실제 부상 동료 목마 분리 점프로 바꿨다. 실제 진입 자격·범위·외곽선·F 문구·잡힌 뒤 숨김을 검사한다.
- Trapeze 비거리/추락: 32a38793에서 절반 비거리 예약이 실제 구형 접촉 판정으로 바뀌었다. 테스트도 범위 밖 F 거절과 서버 위치 지연 요청을 분리한다. 중복 기한 불변·비행 지속·원거리 순간이동 금지·위치 대기 만료·abort 정리를 유지한다. 절반 비거리 테스트는 `TrapezeCatchRequiresActualNextPoleContactAfterFastLaunch`로 개명했다.
- HumanCannon: 현재 저장 코스의 봉은 7개(휴식 5+관문 2), 저장 봉 톱은 제거됐다. 봉 수 기대를 7로 맞추고 톱 회귀는 테스트 수명 동안만 임시 톱을 실제 호스트 hazards에 등록해 회피·접촉 절단·점유 유지를 검사한 뒤 복구한다.
- 제품 runtime·저장 씬·프리팹·private 에셋은 수정하지 않았다. 테스트 assertion을 생략하거나 제품 입력 잠금을 강제로 풀지 않았다.

### 최종 재검사 결과
- Relay 2/2 PASS.
- Swing 최종 6/6 PASS(인간 대포 2건 포함). UI 단독 PoleInteraction 2/2(Swing·Trapeze)도 PASS.
- Trapeze 최종 9/9 PASS. 중간 실행의 UI fixture property reflection 오류도 수정하고 전체 Trapeze를 재실행했다.
- PracticeBotChangesUpperRoleByNormalReleaseWithoutRestartingCourse 1/1 PASS. 이후 Hang 부분 문자열 재검사에서도 PASS.
- Hang 부분 문자열 25/25 PASS(매달림 전용 25건이 아님). HumanCannon 난도 변경/잠금 및 봇 역할 교대의 최초 실패 2건 모두 포함된다.
- HumanCannon PoleSaw 회귀는 Swing 필터 실행에서 PASS.
- 테스트 도구가 일부 요청에 HTTP 400을 반환해도 실제 실행이 진행되는 경우가 있어 test_status로 완료/상세를 확인했다. 실패 응답을 성공으로 간주하거나 중복 실행하지 않았다.
- 후속 `check-work.ps1`: 안전성·문서/규칙·컴파일 PASS, 길이 WARN 19/hard 0, dirty scene 0. 자동 전체 테스트는 SKIPPED이며 별도 실행 결과와 구분한다. 전체 프로젝트 테스트는 실행하지 않았다. `verify-knowledge.ps1` PASS.
- 후속 수정 검증 시점에는 로컬 미커밋이며 PR #233 본문/Ready 상태를 바꾸지 않았다. 이후 사용자 요청으로 수정 commit/push 및 PR 검사 결과 갱신을 진행한다. Draft는 유지한다. 실제 2인 및 최종 플레이 경험은 여전히 사람 확인 대상이다.
