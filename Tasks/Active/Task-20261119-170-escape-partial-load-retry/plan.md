# Task-20261119-170

- **Title:** 탈출로 다인 부분 로드 재시도 복구
- **Type:** fix
- **Assignee:** @RagSpirin
- **Branch:** fix/escape-partial-load-retry
- **Base:** dev

## 범위

호스트의 탈출로 로드만 성공한 경우를 팀 전체 로드 완료와 구분한다. 미완료 씬은 배치 전 정리하고 씬 로드를 다시 요청한다. 실제 배치가 승인된 이후에는 기존 확인과 위치를 유지한다.
- 후속 범위는 실제 Start → Lobby → WaitingRoomPursuit → escape_route 동선의 승인, UI 대기, 캐릭터 재생성이다.
- 탈출로 내부 후속 범위는 플레이 참가 로스터, 협동 발판과 작업자 예약, 발사기 소유권, 출구·엔딩 집계다.
