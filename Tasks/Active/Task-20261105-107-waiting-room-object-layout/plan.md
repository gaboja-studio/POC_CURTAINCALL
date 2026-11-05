# Task-20261105-107

- **Title:** 대기실 기존 기능 연동 오브젝트 배치 에디터
- **Type:** feat
- **Assignee:** @gyumin9048-ctrl
- **Branch:** feat/waiting-room-object-layout
- **Base:** dev

## Context
추격자 창과 독립된 상호작용 기물 전용 배치 창. 기존 상호작용·발견·드롭 시스템을 재사용하며 맵 구조물은 보호한다.

## 계획
- 유일한 대상은 Assets/Scenes/InGame/WaitingRoom/WaitingRoom.unity. 내부 맵과 층/관리실 보기, 미리보기 팔레트, 더블클릭 초점, 기본 드래그 이동/Alt 드래그 배치를 제공한다.
- 기존 기물은 조건 ID와 규칙을 유지하며 상호작용 위치·연결된 발견물·드롭 지점을 함께 이동·회전한다. 스케줄표는 실제 Board 기준으로 편집한다.
- 새 도구/장비는 기존 ItemCatalog/FoundItemSpot을, 낡은 상자는 ExplorationGate/ExplorationDrops를 사용한다. 고유 ID/번호 충돌을 예방한다.
- 기물의 생성 조건·드롭 기물·상대 위치 편집을 제공한다. 벽·문·방/일반 모델 편집은 차단한다.

## 미정·주의
- 사용자 요청으로 이후 추가 테스트와 플레이 조작은 실행하지 않는다. 컴파일과 기능 연결·데이터 검증만 수행한다.
- 사용자 셰이더·escape-labels 변경은 보존한다. origin/dev 추가 11커밋은 fetch했으나 Unity 실행 중이므로 병합하지 않았다.
- 실제 플레이 상호작용·다인 동기화·최종 조작 테스트는 사용자가 진행한다. commit/push는 요청하지 않았으므로 하지 않는다.
