# Task-20261122-199

- **Title:** 정식 묘기와 탈출로 A의 구간 진행 상태 수정
- **Type:** fix
- **Assignee:** @RagSpirin
- **Branch:** fix/official-path-continuity
- **Base:** origin/dev 4c62737d

## 범위

Start에서 진입하는 인간대포·외줄 묘기와 탈출로 A. 공용 기물의 운반·지지면과 A 종료 판정을 비교한다. 기존 제출에서 본 기물 탑승, 외줄 구조 요청, 결과창 입력은 재검사 대상에서 제외한다.

## 계획

기물 낙하 중 지지면 변경과 원격 소유자 상태, A 외줄 지지면 구분, 목마 탑승자의 출구 판정을 검사한다. 씬 변경 없이 기존 판정 함수에 보완하고 Unity EditMode로 검증한다.
