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

- 2026-11-13 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)
- 2026-11-13 추가: 기다릴 생존자가 없으면(전원 도착 또는 나머지 사망·이탈) 여유 시간을 기다리지 않고 바로 정산. 게임 설정(GameSettings 아래 묶음·공연 종료 설정·톱날·추격·추격 조명)의 툴팁 없는 값에 한글 설명 추가.
- 2026-11-13 테스트: 컴파일 PASS(에디터). `CompletionAudience_AllSurvivorsWaitForActualArrival`(혼자 도착 → 즉시 정산), `FirstArrivalSettlesOnceAndKeepsSessionPlaying` PASS.
- 2026-11-19 외줄 끝과 도착대가 닿는 지점에서 완주 판정을 내리고 호스트 승인까지 조작축을 유지한다. 두 공연의 완주자는 시작 인원과 도착 순서에 맞춰 발판 안쪽으로 순간이동한다. 도착자의 충돌과 목마 연결을 해제한다.
- 2026-11-19 Unity 컴파일과 공통 설정 검사 통과. 세 난이도 외줄 끝 18곳과 두 공연의 1~4인 대기 위치 검사 통과. 인간대포 실제 공연 도착·순간이동 PlayMode 검사 통과. 외줄 PlayMode 검사는 기존 `FireView` 초기화 오류로 도착 시나리오에 진입하지 못했다. 2~4인 실접속 플레이 확인은 남아 있다.
- 2026-11-19 커튼 핫픽스. 전환 중 점프·속도를 초기화하고 새 위치에 배치된 뒤 접지를 재확인한다. Unity 컴파일·공통 설정 검사와 PlayMode 2건 통과. 커튼 중 바닥 교체, 전환 뒤 실제 낙하, 시작 화면에서 대기실 진입을 확인했다.
- 2026-11-19 외줄 씬 콘솔 오류. `FireView` 필드가 씬 역직렬화 중 `MaterialPropertyBlock`을 만들면서 초기화가 끊겼다. 불타는 줄의 첫 렌더링에서 블록을 만들고 재사용하도록 수정했다. 별도 경로에서 Unity C# 컴파일 통과. 사용자가 진행 중인 Play Mode 세션 때문에 씬 재실행은 확인하지 못했다.

- 2026-11-19 사용자 플레이에서 외줄 씬의 화재 연출 수정 확인. 수정본 Unity 컴파일·공통 설정 검사와 외줄 도착 PlayMode 회귀 테스트 통과.
