# 로그 시스템

## 담당 파일
- Assets/Scripts/Systems/TelemetryPipeline/TelemetryRuntime.cs — Start 흐름 bootstrap·생명주기 관측·전송.
- Assets/Scripts/Systems/TelemetryPipeline/DurableOutbox.cs — 디스크 영속화·복구·ACK.
- Assets/Scripts/Systems/TelemetryPipeline/LogEvent.cs — 전체 게임 v1 DTO·JSON.
- NetworkLogAdapter.cs — 호스트 생성 방 ID 공유·방 연결 시 신원 고정·서명·퇴장.
- PerformanceLogAdapter.cs — 공개 결과·구간·사망/손상·점프·대포·협동 관측.
- WaitingRoomLogAdapter.cs·CarryLogTracker.cs — 화면·구매·의료·기물 UID 이동 차이.
- WaitingRoomZoneMap.cs·WaitingRoomTiming·StorageArrivalTracker.cs — 실제 구역 경계·배치/준비/집결 단조 시간·수령부터 소품실 도착 시간.
- InputLogAdapter.cs — 키 코드·uGUI 클릭·Vivox 본인 발화 구간.
- Scripts/Telemetry/core.gs — 68종 이벤트 계약·검증·집계, 실패/운반·미수령/의료 미이용 요약.
- Scripts/Telemetry/management.gs — 등록 이메일 인증·멤버/보관 설정·삭제 규칙/인증·원본 정리.
- PlaytestBuildTags.cs·PlaytestSurveyRuntime.cs·SurveyLaunchPolicy.cs — 독립 LOGS/SURVEY_010·최초 밤/명시적 ESC 나가기·로컬 사용자 캐시.
- Scripts/Telemetry/surveys-{core,server}.gs — 두 응답 시트 읽기·자동 연결 코드·세션/슬롯 매칭·문항 분포/중앙 시트.
- Scripts/Telemetry/server.gs — Google 수신·Drive 원본·Sheets 적재·권한·트리거.
- Scripts/Telemetry/dashboard.html — Apps Script 웹 화면.
- Scripts/Telemetry/*.test.cjs — Node 계약·집계·서버 복구 테스트.
- Assets/Scripts/Systems/Telemetry/ — 기존 외줄 로컬 기록기, 변경하지 않음.

## 공개 진입점
- TelemetryRuntime.Instance.Emit(eventType, payload, authority): 로컬 디스크 기록. 실패하면 false.
- SetContext(roomId, runId, attemptId, authenticatedPlayerId): 상세 adapter 연결용.
- Flush(): 현재 배치를 immutable outbox 파일로 닫기.
- LastError/RemoteConfigured: 상태 확인, endpoint/token 원문 출력 금지.
- 서버 refreshDashboard: 등록 멤버 인증 후 적재·집계·페이지 데이터 반환. 멤버/보관/삭제 관리는 소유자만 허용.
- Recorded: 디스크 영속화 후 분리된 LogEvent 사본을 발행. 관측자 오류는 저장/게임에 전파하지 않는다.
- 원천 공개 API: CourseSections.SectionAt(position), Cannon.LandingConfirmed, Piggyback.HandholdConfirmed, DeliverySlot.DeliveryStageChanged, Shop/Carry/Expert.OperationRejected. 현재 목적은 Task-069 plan.md, 옛 경로 정보는 History/Harness-v1/setup.md 참고.
- 추가 관측: Campaign.RestExpired/LocalReadyChanged, WaitingRoomSpawn.PlayerPlaced, ExpertRoomView.LocalMoved, CannonHazards.ContactDetected/HitConfirmed. 확정 피해 기록은 실제 신체/생존 상태와 결합한다.

## 문서
- [기획자용 Windows·Mac 하네스 안내](../../Harness/Project/Guides/Log-system/index.md) — PC 검증·빌드·관리자/팀원 접속·통계 분석
- [기획자 사용 안내](../Log-system/operator-guide.md), [통계 읽는 법](../Log-system/reading-guide.md)
- [관리자 운영·게임 버전 변경](../Log-system/admin-guide.md)
- [로그/설문 빌드 태그·설문 운영](../Log-system/survey-guide.md)
- [계약](../Log-system/schema.md), [이벤트 목록](../Log-system/event-catalog.md)
- [연결 단계](../Log-system/implementation-plan.md), [운영·설치](../Log-system/setup.md)
- [실제 연결 범위와 미수집 지표](../Log-system/coverage.md)
- [대시보드 화면·평균 자금·공연 좌표 도식 기준](../Log-system/dashboard-design.md)

## 주의
- 현재 Task는 기반. 등록된 모든 이벤트가 실제 연결된 상태는 아니다.
- HOST는 게임 판정의 관측 권위 필드. 클라이언트 업로드 token은 인증된 판정의 증명이 아니다.
- LIVE/TEST를 혼합하지 않는다. Editor 검증은 TEST.
- gameVersion은 발생 시 Application.version(현재 0.1.0). Events V열과 대시보드 전체 필터로 구분하며 재전송/과거 기록을 새 버전으로 덮지 않는다.
- 기존 씬/프리팹을 수정할 때는 별도 구역 소유권이 필요하다.
