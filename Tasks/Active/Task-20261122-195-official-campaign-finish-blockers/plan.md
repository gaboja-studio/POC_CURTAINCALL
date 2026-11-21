# Task-20261122-195

- **Title:** 정식 묘기 정산과 탈출로 결승 진행불가 수정
- **Type:** fix
- **Assignee:** @RagSpirin
- **Branch:** fix/official-campaign-finish-blockers
- **Base:** dev

## 범위

Start의 정식 인간대포·외줄과 탈출로 A의 진행 문제를 점검한다. 직전 12건은 PR #477로 제출하고 새 작업은 origin/dev 8b0fa5db에서 분리한다.
탈출로 B·C, 환풍 구역, 튜토리얼, F8 직접 실행은 제외한다.

## 계획

- 보관한 11건을 정식 빌드 씬의 컴포넌트·프리팹 참조에 따라 선별한다.
- 실제 도착 및 정산, 참가자의 절단기 사용, 외줄 화염의 아바타 생명 주기를 점검한다.
- 신규 조건을 EditMode와 정적 검사로 검증한다. 실제 플레이 검증은 사용자가 한다.
