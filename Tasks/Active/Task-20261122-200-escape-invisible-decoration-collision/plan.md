# Task-20261122-200

- **Title:** 탈출로 A 투명 장식 충돌 제거
- **Type:** fix
- **Assignee:** @RagSpirin
- **Branch:** fix/escape-invisible-decoration-collision
- **Base:** dev

## 범위

컨베이어 하차장 전원 레버 주변과 탈출로 A 전체에서 표시가 제거된 장식에 남은 충돌을 정리한다.

## 계획

씬의 활성 충돌체, 자체 렌더러, 자식 외형과 기믹 참조를 함께 검토한다. 기존 배칭 장부는 현재 씬에서 메시를 실제로 쓰는지 확인한 뒤 판단한다. 최종 씬에서 제거 대상 외의 직렬화 데이터를 비교하고 EditMode로 검증한다.
