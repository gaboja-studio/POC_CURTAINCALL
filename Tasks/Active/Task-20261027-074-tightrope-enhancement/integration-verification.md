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

## 실패·미검증 (PASS 아님)
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
