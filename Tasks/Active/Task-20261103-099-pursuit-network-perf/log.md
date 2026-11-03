# Log

특별한 문제·해결·검사 결과·다음 할 일·PR 링크만 짧게 남긴다.

- 2026-11-03 기준 측정(baseline-20261103.json, 에디터·오프라인, 씬을 이미 여러 번 연 따뜻한 상태):
  - 로드 3.5초, 최장 프레임 1,041ms(GC.Collect 286ms, LoadLevelAsync Integrate 791ms 포함).
  - 씬: GameObject 30,952 / MeshRenderer 23,299(정적 배칭 180) / LODGroup 12,579 / Collider 3,764 / 실시간 조명 268(그림자 11).
  - 렌더: Lobby batches 6,110·그림자 캐스터 4,608·삼각형 230만(기준 초과), 숙소 2,527, 연습실 151.
  - 스냅샷: 추격자 26체 JSON 17,288B(≈169KB/s/클라이언트) → 바이너리 419B(≈4KB/s), 41배 감소.
- 2026-11-03 중요: PlayMode 테스트 중 추격 대기실 로드에서 `[Session] 메인 스레드 정지 12503ms`(차가운 로드) 경고. Relay 10초 기준 초과 → 튕김 원인 재현. 측정 도구의 1.0초와 차이 = 첫 로드(셰이더·에셋 캐시) 영향으로 추정.
- 2026-11-03 PlayMode 실패 3건 처리:
  - PursuitCheats_ReceivedCancellation: 스냅샷 타입 public 변경으로 리플렉션(NonPublic) 실패 → 테스트에 Public 포함(이번 변경 영향).
  - PursuitBehaviour_CaptureFourDirections: 단독 재실행 통과(순서 의존 불안정).
  - PursuitBehaviour_GraphHearing: dev 설정 에셋 1단계 순찰 이탈 30m로 100m 이동 시 Search 대신 복귀(dev에서도 실패하는 기존 문제) → 테스트 픽스처에서 이탈 제한 0.
- 2026-11-03 테스트: Pursuit EditMode 42/42 PASS. PlayMode 전체 재실행 12/14 — 실패 2건은 순서·환경 의존(CaptureFourDirections: 카메라 테스트 직후 런처 실패, 단독 PASS / GraphHearing: Vivox `Target Object Does Not Exist` 오류 로그, 단독 PASS). 이번 변경 영향 아님으로 판단.
- 다음: 다인 실기, 차가운 로드 멈춤 분석(후속 Task).

- 2026-11-03 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

## 2026-11-03 현재 브랜치 통합
- feat/waiting-room/pursuit-system에서 90a3f05c 병합. 새 피드백/치트/배치 단계와 전등·경량 스냅샷을 함께 유지.
- v3 바이너리에 poseEpoch/보호 치트/포획 누적 추가. 상태 신뢰성(4Hz+변경), 위치 비신뢰성(20Hz) 경로 분리 유지. 두 경로 준비 알림 적용.
- 코덱 왕복은 추가 상태값까지 검증. 컴파일 PASS, Pursuit EditMode46/46 PASS. 초기 check-work는 에디터 재시작 중 ENV_BLOCKED였으며 준비 완료 후 컴파일 통과.
- PlayMode 전체16개 중14개 PASS. 실패2개: 카메라 테스트 직후 런처 시작 실패(Activation), Vivox Target Object Does Not Exist(CaptureFourDirections). 두 항목을 개별 재검증한다. 다중 이름 세미콜론 필터는 NO_PROJECT_TESTS여서 결과로 세지 않음.
- 실패2개 각각 단독1/1 PASS. 전체14/16 + 개별2/2로 기록(전체 통과 아님). 이어서 사용자 요청대로 통합 커밋/푸시 후 P1을 진행한다.
