# Task-20261122-194

- **Title:** Start 정식 묘기와 탈출로 진행불가 수정
- **Type:** fix
- **Assignee:** @RagSpirin
- **Branch:** fix/start-campaign-progression-blockers
- **Base:** dev

## Context

Start에서 방을 연 실제 런이 로비와 대기실을 거쳐 활성 빌드 목록의 인간대포·외줄·탈출로 A에 진입하는 경로만 수정과 검사의 대상으로 삼는다.

## 계획

- 빌드 목록과 실제 런이 선택하는 씬을 대조한다.
- 네트워크 로드·언로드와 배치 확인, 같은 씬의 다음 공연, 전환 중 조작과 정산 연결을 점검한다.
- 이전 작업은 보존한다. 직접 플레이 기록 대신 확인한 코드와 새 EditMode 사례를 기록한다.
