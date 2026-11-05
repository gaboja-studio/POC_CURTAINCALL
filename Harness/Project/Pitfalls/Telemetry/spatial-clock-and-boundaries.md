---
summary: 소유자 공간 기록의 서버 시각·씬 종료 전송·서버 계약 배포
status: active
updated: 2026-11-05
source: Task-20261105-106-telemetry-spatial-timeline
---

# 공간 기록: 시각과 전송 경계를 분리한다

2026-11-05 Task-106.

- 위치는 소유자가 수집하고 공용 추격자·물체는 호스트가 수집한다. PC별 occurredAt만으로 동선을 합치면 시계 오차가 이동 순서를 바꾼다. roomId와 NGO ServerTime 기반 timelineMs를 같이 보존한다. 서로 다른 시계는 UTC_UNALIGNED로 표시한다.
- 디스크 체크포인트는 데이터 보호이고 HTTP 전송 경계는 별개다. HoldNewBatches 중 파일은 NextPath/NextRelayPath에서 제외하고 씬·활동/런/접속 종료에 해제한다. 복구 파일은 다음 실행에서 전송 가능해야 한다.
- 250건/크기 제한으로 씬 기록은 여러 배치가 된다. 배치 ID/이벤트 ID/ACK는 그대로 유지하며 하나의 거대 요청으로 만들지 않는다.
- 새 이벤트 계약은 수신 서버에도 필요하다. Apps Script 하나의 서로 다른 배포 버전에서 dashboardEnabled=true(대시보드)와 false(수신기)를 각각 보존한다. 공유/인증 설정을 바꾸지 않는다.
- 0.2.0 첫 가이드 쉬는 시간은 튜토리얼 완료 전 집결을 거부한다. 조기 집결 테스트는 SetTutorialDone/RefreshGuideRest 후 타임아웃 미발생을 검사해야 한다.
- 평면도를 방 Bounds 몇 개로 대체하지 않는다. 추격자 배치 도구와 같은 층 절단 카메라로 실제 방 구조를 캡처하고 월드 X/Z 범위와 mapId를 함께 보존한다. 모든 층은 공통 시각을 쓰는 별도 캔버스로 표시한다.
- 탈출로의 실제 씬 이름은 소문자 escape_route이다. SceneType 대소문자 처리와 단계 분류를 함께 검사한다. HumanCannonCourseSections를 탈출로 구간으로 재사용하지 않고 실제 배치 그룹의 공간 범위를 사용한다. 높이가 겹치는 탈출로는 전체 지도와 구간별 절단 높이를 분리한다.
- 미술 캡처 JPEG는 로컬 산출물로 두고 Apps Script 인증 API로만 제공한다. 공개 HTML에 base64 이미지를 넣지 않는다. 런타임 생성 외줄 코스는 정적 씬 캡처가 비어 있으므로 생성 후 줄의 경로를 기록한다.
- [기획용 수집표](../../../../Docs/Log-system/m2-spatial-timeline.md), [공간 어댑터](../../../../Assets/Scripts/Systems/TelemetryPipeline/SpatialLogAdapter.cs).
