# Task-20261102-093

- **Title:** 시스템 버그 수정: 열쇠 소지 상체 애니메이션 떨림
- **Type:** fix
- **Assignee:** @MoHoDu
- **Branch:** fix/fix-systems
- **Base:** dev

## Context
열쇠(손에 든 도구)를 들고 있을 때 상체 팔이 둠칫둠칫 튀는 버그 수정. 이동·정지 중 들기 자세가 자연스러워야 한다.

## 계획
1. 실제 프리팹 PlayMode 측정으로 원인 확정.
2. 상호작용 동작은 상체 레이어(ItemRightHand), 계속 든 자세(들기·조준·꺼내기·넣기)는 오른팔 전용 레이어(ItemRightArm)로 분리.
3. 회귀 테스트 추가.

## 미정·주의
Task-091(상하체 분리)의 ItemRightHand 상체 마스크는 상호작용용으로 유지했다. 공용 CircusPlayer.controller에 레이어 1개 추가(기존 상태 변경 없음).
