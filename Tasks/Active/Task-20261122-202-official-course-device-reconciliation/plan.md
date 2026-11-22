# Task-20261122-202

- **Title:** 정식 묘기와 탈출로 A 기물 상태 복구
- **Type:** fix
- **Assignee:** @RagSpirin
- **Branch:** fix/official-course-device-reconciliation
- **Base:** dev

## Context
Start의 정식 인간대포·외줄 묘기와 탈출로 A에서 이전 공식 동선·기물 검사의 통과 조건과 겹치지 않는 진행 차단을 찾고 수정한다.

## 계획
승강기의 도중 진입과 탑승자 교체·재시작, 목마의 발판 경계·부상·지연 입력, 탈출로 A 절단기의 지지면을 검사한다. 신규 EditMode 검사에서 충돌·상태 분기를 재현한다.

## 미정·주의
사람이 실제 플레이를 맡는다. 이 작업은 Start에서 연결된 실제 씬을 기준으로 하며 탈출로 B·C를 범위에서 제외한다.
