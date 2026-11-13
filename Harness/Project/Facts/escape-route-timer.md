---
summary: 탈출로 제한시간/카운트다운은 제거됐으며, 추격자 연출용 서버 시작 시각과 재시작 동기화만 유지한다.
status: active
updated: 2026-11-13
source: Task-20261103-100 사용자 타이머 제거 요청 (2026-11-13); 이전 Task-20261027-073 8분 제한을 대체
---
# 탈출로 연출 시계와 제한시간 제거
- 탈출로는 제한시간이 없다. 480초 카운트다운·시간 초과 리타이어·ESCAPE_TIMEOUT 발생 경로를 제거했다. 빠른 전진은 추격자와 실제 위험물이 압박한다.
- EscapeRouteTimer는 기존 씬/프리팹 참조를 보존하는 이름이다. 이제 StartedAt/HasStarted/Clock/ElapsedSeconds만 제공하는 내부 연출 시계이며 Duration/Remaining/Expired API는 없다.
- 시작홀 첫 정상 참가자 진입을 호스트가 기록하고 시작 시각·세대·리비전/재시작 토큰을 공유한다. 늦게 합류한 클라이언트와 재시작도 같은 추격자 라운드를 사용한다.
- A와 전체 A 추격자 테스트 씬의 EscapeCountdownDisplay 인스턴스를 비활성화한다. 이전 씬의 display 참조가 남아 있어도 내부 시계가 Hide 처리한다. 대기실 시간과 공용 RestTimer 프리팹은 변경하지 않는다.
- 추격자 등장/조준/도약·불꽃벽 점화 후 진행 시간은 유지한다. 출구는 필수 배선 절단 후 개방하며 기존 도착/리타이어/이탈 집계를 사용한다.
- 로그 호환: ESCAPE_STARTED durationMs=0/timeLimitEnabled=false, ESCAPE_SECTION remainingMs=null 및 elapsedMs, ESCAPE_ENDED timeout=false. 시작/진행 시간은 제한시간을 뜻하지 않는다.
- 이전 8분 규칙과 검사는 [과거 기록](../../../Tasks/Active/Task-20261027-073-exit-route-space-design/escape-countdown.md)이다.
