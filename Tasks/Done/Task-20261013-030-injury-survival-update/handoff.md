# Handoff

덮어쓰는 스냅샷이다. 다음 AI(또는 다음 날의 나)는 이 파일부터 읽는다.

## 지금 상태

- 단계: done(#62 병합, 공동 테스트 통과, 2026-10-14 PM 판단으로 Done 이동). 테스트 씬 유지. 브랜치는 최신 integration(028·033·034 포함)으로 fast-forward.
- 동작하는 것(코드): `NetworkPlayer.ServerCut(개수)` 고정 순서 절단(외줄 `ServerCutPart`·`ServerCutLegThenArm`은 그대로), `ServerKill`·`ServerCatch`(장소 `SurvivalRules.ServerPlace`: 공연/탐색/탈출로, 새 상태 `Retired`=3), `ServerSetOneArm`·`ServerRevive(AllDead)`·`ServerRestorePart`(역순)·`ServerRestoreAll`/`Everyone`, 이벤트 `Revived`. `InjuryRevival`(자동 생성)이 `CampaignRun.StepChanged`로 쉬는 시간·밤 시작 = 외팔 부활, 세대 2+ 도입 = 전원 복구, 장소 기본값 설정. 점프 높이 배율(다리 1 ×0.75, 2 ×0.5 임시), 걷기 속도·점프 높이 공통 세팅 이전(값 그대로), `ExplorationBodyPenalty.Shared`(임시값), `SurvivalRules.ItemSlots`, `BodyRestartRecovery` 폐기 예정 표시, 테스트 화면 `InjurySurvivalTestUI`
- 테스트 씬(AI 배치, PM 위임): Ground·PlayerSpawnPoints(4)·PlayerFollowCamera·SessionUI(`NetworkSessionTestUI`)·InjurySurvivalTest(`InjurySurvivalTestUI`+`CampaignFlowTestUI`). 테스트 화면이 내 캐릭터 균형을 끔(외줄 밖에선 서 있다 넘어짐)
- 막힌 것: 없음
- PR: https://github.com/gaboja-studio/POC_CURTAINCALL/pull/62

## 다음 할 일

1. 없음. 후속 수정은 `fix/survival-followups` PR(외줄 세팅 칸 삭제·리타이어 관전)

## PM이 확인할 것

- 확인 요청: 없음. 외줄 세팅 안 쓰는 칸은 삭제하기로 결정(후속 fix PR)
- 대기 중인 결정: 없음
- 기록: 균형 함정 → `Harness/Project/Pitfalls/balance-on-outside-tightrope.md`(2026-10-14)
- 공용 파일 요청: 리타이어 관전은 결정(후속 fix PR). 남은 것: "모두 재시작" 요청이 `TightropeRun`을 직접 찾음(외줄 수정 필요, 4단계)

## Verification

- 구역 검사: PASS (10-14)
- 1 문서/규칙: PASS (10-14)
- 2 컴파일: PASS (10-14)
- 3 테스트: PASS (EditMode, 5/5, 10-14)
- 4 실행 로그: PASS (AI, Play Mode 호스트 1인, 판정 함수 직접 호출) — 절단 왼다리→오른다리→왼팔→오른팔·4번째 사망, 쉬는 시간 시작 외팔 부활, 역순 복구(왼팔→오른다리), 세대 교체 전원 복구, 추락 즉사, 탐색 잡힘(외팔→사망), 탈출로 잡힘 = 리타이어, 기물 칸 2/4·1/3·0, 점프 ×0.75/×0.5. 게임 오류 0(에디터 검색 색인 예외 1건은 무관). 작업자 플레이 테스트 성공(@MoHoDu, 10-14). integration merge 후 판정 재확인 PASS
- 5 공동 테스트: PASS (2026-10-14 사용자, 통합 브랜치 `InjurySurvival.unity`, 항목 1~3 + 점프 높이·장소 규칙)
