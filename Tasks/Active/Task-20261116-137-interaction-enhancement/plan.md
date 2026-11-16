# Task-20261116-137

- **Title:** 상호작용 고도화(우선순위·기준점·아웃라인·T 드랍·대기실 목마 상태)
- **Type:** fix
- **Assignee:** @MoHoDu
- **Branch:** fix/interaction
- **Base:** dev

## Context
기획서 Confluence "상호작용 고도화"(pages/43286568, 2026-11-16 버그 리포트 후속).
겹친 대상 중 엉뚱한 상호작용이 먼저 일어나고, 아웃라인·범위가 실제 F 대상과 달라 보이는 문제를 고친다.

## 계획
1. 설정 에셋 `Assets/Resources/Settings/Interaction/InteractionFocusSettings.asset`: 기준점 모양·크기·색, 아웃라인 두께·색, 대기실/공연 우선순위 순서, 대기실 목마 상태 규칙 켜기.
2. 공통 선택 `CommonInteractionSelection`: 상호작용 거리 안 → 분류 우선순위 → 화면 기준점 거리 → 플레이어 거리(기준점은 거르지 않음).
3. 안내·아웃라인 `CommonInteractionUI`: 거리 안 대상은 얇게, 선택 대상은 강조. 바닥 기물 이름표 제거. 화면 중앙 기준점 `InteractionCrosshair`.
4. 대기실 `ShopReach`: 가장 가까운 대상 대신 공통 선택 대상만 안내·F. 기물 내려놓기 F 길게 → T(`DropItem` 입력).
5. 대기실 목마(`PiggybackSystem`): 침대·사망·포획 시 그 단원과 위 단원 안전 하차, 앉은 채 탑승·해제 허용, TV 보기 범위 이탈 시 종료.

## 미정·주의
- 기획 공백은 사용자 확인 후 Confluence "구현 세부 규칙"에 기록(2026-11-16).
- 다른 작업자 커밋 49614ab6(`fix/waiting-room-bugs-20261116`, NightBed·ExplorationGate·ExplorationPanel·WaitingRoom.unity)과 겹치지 않게 그 파일은 수정하지 않았다. 침대 점유 판정은 그쪽 변경을 그대로 쓴다.
