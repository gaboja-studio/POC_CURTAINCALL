# Log

특별한 문제·해결·검사 결과·다음 할 일·PR 링크만 짧게 남긴다.

- 2026-11-13 테스트: PerformanceReward.EditModeTests 64/65 PASS. 실패 1건 `AudienceReactionTests.CutIsAwardedOnceAndSurvivorDoublesCheer`(기대 50, 실제 150)은 점수 규칙·설정값 쪽이며 이번 변경과 무관(모델 점수 로직 미수정, 변경 전 재확인은 안 함).
- 2026-11-13 테스트(병합 전): `TightropeCampaignPlayTests` 100개 중 68 PASS / 31 FAIL / 1 SKIP. `CompletionAudience_AllSurvivorsWaitForActualArrival` PASS(첫 도착 → 5초 → 성공 정산). 관련 실패 `FirstArrivalSettlesOnceAndKeepsSessionPlaying`은 대기 시간에 여유 시간이 빠져 있어 테스트 대기 시간을 늘렸다. 나머지 실패는 카메라·톱날·아트·시작 흐름 등 다른 영역이며 변경 전 기준은 확인하지 못했다.
- 발견: 공용 런의 외줄은 SpaceLink 외부 관리 경로(TightropeRun.ServerSucceed → CampaignTightropeAdapter)로 정산된다. Bridge 경로만 고치면 실제 게임에 반영되지 않는다.
- 2026-11-13 origin/dev 병합: 충돌 없음. dev에 같은 번호 Task-123이 생겨 이 Task를 124로 바꿨다. _ThirdParty는 dev 기록 커밋으로 맞췄다.
- 2026-11-13 수정: 포기 시 쓰러짐 제거(정산 모델에서만 사망, 결과 화면 동안 세계 정지), 결과창 '생존 보너스', 도착자 여유 시간 관전·조작 잠금.
- 2026-11-13 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS(에디터); 테스트=병합 후 PlayMode 재실행은 MCP 시간 초과로 결과 미수신.

- 2026-11-13 PR: https://github.com/gaboja-studio/POC_CURTAINCALL/pull/366 (fix/main-stage/result-and-rewards → dev)
- 2026-11-13 PR: https://github.com/gaboja-studio/POC_CURTAINCALL/pull/366 (dev, 지정 팀원 검토 대기)
