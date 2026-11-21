# Task-20261121-191

- **Title:** 탈출로·묘기 지형 진행불가 검사
- **Type:** fix
- **Assignee:** @RagSpirin
- **Branch:** fix/escape-stunt-terrain-progression
- **Base:** dev


## Context

탈출로와 인간대포·외줄 묘기의 실제 바닥, 외줄, 머리 위 충돌과 낙사 및 복구 판정이 달라 진행이 막히는 조건을 점검한다.

## 계획

- 탈출로 합류 바닥·심부 구조·첫 외줄과 상단 외줄을 검증한다.
- 두 묘기의 접지와 조작 인계, 운반 기물의 복구 지면을 점검한다.
- 저장된 씬 및 재현용 콜라이더를 Unity EditMode로 검사한다.
