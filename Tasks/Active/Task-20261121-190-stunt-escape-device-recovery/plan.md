# Task-20261121-190

- **Title:** 묘기·탈출로 기물 상태 복구
- **Type:** fix
- **Assignee:** @RagSpirin
- **Branch:** fix/stunt-escape-device-recovery
- **Base:** dev


## Context

탈출로와 묘기 씬에서 기물 상태 갱신 및 구조·복귀가 중단되는 새 조건을 점검한다. 직전 진행불가 수정 PR #472와 분리한다.

## 계획

- 탈출로 외줄 복귀 장치와 절단 결과를 확인한다.
- 외줄 구조 상태 메시지와 회차·캐릭터 교체 조건을 확인한다.
- 인간대포 다인 봉 해제·재생성과 착지 판정 연결을 확인한다.
- Unity 배치 EditMode와 정적 검사로 확인한다.
