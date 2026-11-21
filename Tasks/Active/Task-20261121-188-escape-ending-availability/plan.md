# Task-20261121-188

- **Title:** 탈출 결과 조회와 복귀 대기 복구
- **Type:** fix
- **Assignee:** @RagSpirin
- **Branch:** fix/escape-ending-availability
- **Base:** dev

PR #470 이후 탈출 결과 화면과 씬 전환의 경계 조건을 검사한다. 현재 엔딩의 결과만 반영하고 명부가 비거나 콘텐츠가 먼저 사라져도 전환이 멈추지 않게 한다.

후속 검사에서는 탈출로 시작 시계·반출교·협동 도착과 하층 게이트의 새 결함을 판단한다.
