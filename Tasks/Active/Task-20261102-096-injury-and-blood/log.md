# Log

특별한 문제·해결·검사 결과·다음 할 일·PR 링크만 짧게 남긴다.

- 2026-11-02 검사: 안전성=PASS; 문서/규칙=FAIL; 컴파일=SKIPPED(선행 실패); 테스트=SKIPPED(자동 테스트 선택 안 함)

## 2026-11-02
- 구현: `HumanoidPlayerPresentation.ApplySurface`가 `InjuryBloodEffects.IsVisible`이 거짓이면 Clean 고정. 의상 검사는 SetUp에서 `ClientSettings.ShowBloodEffect`를 켠 상태로 고정·복원.
- check-work: 안전성 PASS, 문서/규칙 FAIL(기존 Task-081 log.md 200줄 HARD, 이번 변경과 무관) → 컴파일 SKIPPED.
- 대신 열린 에디터(MCP)에서 확인: 컴파일 오류 없음. PlayMode `HumanoidPlayerTests` 26개 중 24 PASS, 2 FAIL(`CrawlSlopeAndJumpRecordClearance...`, `InitialInjuryRebindBatchCuts...`: 기대 `Crawl`, 실제 `ItemCrawl_Cut2/3`, 애니메이션 이름 문제로 의상 변경과 무관). 새 검사 `BloodSettingOffKeepsCleanSurfaceRegardlessOfInjury` PASS.
- 다음: 사람 플레이로 설정 토글 시 의상 전환 확인.

- 2026-11-02 PR: https://github.com/gaboja-studio/POC_CURTAINCALL/pull/267 (fix/settings/injury-and-blood → dev)
- origin/dev(c4b51736) 병합: 충돌 없음. 테스트·에디터 종료로 바뀐 TMP 폰트 아틀라스 에셋 2개(Pretendard-SemiBold SDF, escape-labels)는 의도하지 않은 변경이라 되돌림. 병합 후 컴파일은 에디터 종료로 미실행.
- PR #267 제출: https://github.com/gaboja-studio/POC_CURTAINCALL/pull/267
- 병합 후 재확인(에디터 재시작): 컴파일 오류 0. PlayMode HumanoidPlayerTests 29/31 PASS, 2 FAIL은 병합 전과 같은 기어가기 애니메이션 이름 검사(변경 전 코드 재현은 미확인). PR 본문 검사 결과 갱신.
