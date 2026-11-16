# Task-20261116-138

- **Title:** 대기실 구조 변경 뒤 추격 NavMesh·연결 링크·오클루전 다시 굽기
- **Type:** fix
- **Assignee:** @MoHoDu
- **Branch:** fix/waiting-room/pursuit-nav-bake
- **Base:** dev

## Context
대기실 구조·오브젝트가 많이 바뀌어 추격자 길찾기(NavMesh)·건넘 연결·오클루전을 다시 굽는다(2026-11-16 사용자). 대상은 현재 쓰는 `Assets/Scenes/InGame/WaitingRoom/WaitingRoom.unity` 하나뿐이다(다른 대기실 씬은 굽지 않음).

## 계획
- `Tools > Curtain Call > Pursuit > 2 Bake navigation`이 실제 대기실 씬에서 동작하게 고친다(옛 테스트 씬 경로 → WaitingRoom, 기존 `PursuitNavMesh.asset`에 덮어쓰기).
- 굽기마다 옛 `TraversalLinks`를 지우고 새로 만든다.
- 키 1.7m보다 큰 단단한 충돌체 안쪽에 생기는 길 조각을 굽기 단계에서 막는다(아래 log 참고).
- 오클루전은 기존 메뉴 `Tools/CurtainCall/대기실/오클루전 굽기`.

## 미정·주의
- 플레이 확인(추격자가 물체 속으로 들어가지 않는지, 오르기)은 사람이 한다.
