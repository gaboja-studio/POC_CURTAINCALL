---
summary: 호스트 종료로 퇴장된 참가자에게 종료 설문이 열리지 않을 때
status: active
updated: 2026-11-05
source: human, fix/survey-host-disconnect, Task-20261105-109
---

# 참가자의 종료 설문과 연결 종료 순서

## 언제 읽나

직접 ESC 나가기는 설문이 열리지만 호스트 종료로 퇴장된 참가자는 열리지 않을 때.

## 원인과 대처

- 메뉴의 OnLeaveConfirmed만 구독하면 참가자의 강제 퇴장을 놓친다. 로컬 NetworkSessionManager.Disconnected도 구독한다.
- NGO가 플레이어를 먼저 없앨 수 있다. 연결 종료 알림 전까지 마지막 런 ID·로컬 슬롯을 보존하고, 종료 알림에서 설문 예약 후 비운다.
- Disconnected 호출을 못 받거나 session이 먼저 사라져도 Update에서 Offline/세션 소멸을 확인한 뒤 마지막 정보로 예약하고 비운다. 먼저 currentRun을 지우면 참가자는 다시 나가기 버튼을 누를 기회가 없다.
- 실제 브라우저는 Start 복귀·Offline 확인 뒤 연다. 메뉴와 연결 종료의 중복 알림은 pendingCode, 최초 1회는 기존 SurveyLaunchCache로 막는다.
- 런을 시작하지 않은 로비 퇴장에는 런 스냅샷이 없으므로 설문을 예약하지 않는다.

## 확인

SurveyBootstrapTests PlayMode 3개 통과: 참가자 슬롯 1~3·스냅샷 보존·중복 예약·런 없는 퇴장·설문 전용 초기화. 연결 종료를 주입한 로직 검사이며 실제 두 PC 플레이·브라우저 실행 검증과 구분한다.
후속 회귀 검사는 연결 종료 콜백 없이 Offline이 되는 참가자 1~3의 예약을 검사한다. 테스트의 M1 설정은 팀의 현재 M2 수집 설정과 분리하고 종료 때 원본 설정을 복원한다.
