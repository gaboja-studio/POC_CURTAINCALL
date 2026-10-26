# 공연 참가자 누락 수정 — 2026-10-26

## 요청·Git
- PM: 최신 dev를 받은 뒤 현재 브랜치에서 참가자/시작 사건 누락 수정.
- fix/dashboard-refresh-cache에서 dev 7d87630671f1c4185148a7164a14042a70ab0f31까지 FF. 작업 전 128개 상태 항목을 `.harness/local/timeline-host-sync-20261026/`에 보관하고 stash 후 복원했다. handoff 충돌은 두 기록을 보존해 해결했다. 복구 stash는 남겨 두었다.
- 다른 Task 문서 이동·기존 서버 작업을 유지했다. commit/push/빌드/Google 배포 없음.

## 변경
- PerformanceLogAdapter: 공식 명단 PERFORMANCE_ROSTER, 호스트의 개인별 시작·구간·최대 거리·PLAYER_ARRIVED 관측. 준비 전 시작은 재시도하고 저장 성공 뒤에만 발행 완료 처리한다. 게임 판정·씬·원천 클래스는 수정하지 않았다.
- 연결 공연의 로컬은 LocalPlaced, 원격은 실제 출발 위치 관측으로 준비를 확인한다. 늦게 부상/발사/도착만 보인 참가자는 시작 좌표를 null로 두고 FIRST_GAMEPLAY_EVENT 표시한다. 원격이 출발 위치 관측 전에 이동한 상황은 정확한 출발 시각/좌표 보장을 하지 않는다.
- core 계약과 C# 계약/생성 카탈로그에 두 사건 추가. 집계는 명단을 실패로 세지 않으며 같은 개인의 최대 거리를 중복 표본으로 세지 않는다.
- 대시보드: 명단만 있는 참가자도 표시, 참가 인원/사건 확인 인원 분리. HOST_PLAYER와 구형 LOCAL 중복 표시 방지, 시작·사망·절단·추락·도착·퇴장 보존. 관측하지 못한 사건이나 위치를 보간하지 않는다.

## 최소 검증
- Unity 연결 확인 후 수정, 최종 컴파일 오류 0. EventContractTests 10/10 PASS.
- Tightrope_PublicSettlement_UsesSharedAttemptAndSingleHostOutcome 1/1 PASS, 13.49초. 실제 외줄 씬/LAN 호스트의 명단·시작·동일 시도 정산 1회 확인. 공개 API 합성 정산이며 사람 조작·다중 PC 검사가 아니다.
- 해당 PlayMode는 업로드 루프를 멈추고 Temp 전용 outbox로 격리했다. 새 계약을 구형 Google에 보내지 않았다. 증거 `.harness/local/host-timeline-playmode.json`.
- `node --test Scripts/Telemetry/core.test.cjs Scripts/Telemetry/dashboard.test.cjs Scripts/Telemetry/session-store.test.cjs`: 71/71 PASS. 4인 중 일부 사건 없음, 다음 날 2명, 구형 LOCAL 유지/신형 HOST 우선, 많은 사건 속 시작·도착·사망 유지 포함.
- 문서/규칙 개별 검사 PASS(기존 크기 경고). 전체 구역 검사 FAIL: 기존 다른 Task 문서 이동 160개. 이번 코드 경로 위반 없음. 에디터 검사 뒤 에셋/ProjectSettings/Packages 새 자동 변경 없음.

## 적용 순서·제약
1. 새 core.gs와 dashboard.html을 기존 Google 프로젝트에 반영하고 수신·대시보드 배포를 갱신한다. 현재 운영 배포는 그대로다.
2. 그다음 수정 게임 빌드를 배포한다. 구형 서버는 새 이벤트 포함 배치를 거절할 수 있으므로 순서를 지킨다.
3. 다중 PC의 늦은 로드·중도 이탈·재접속은 추후 공동 검증한다. 과거 실제 미수집 시작 사건은 복원할 수 없다.
