---
summary: 통합 대기실의 플레이·편집 경로와 추격 및 리허설 구성
status: active
updated: 2026-11-19
source: Task-20261104-100, Task-20261119-176
---

# 통합 대기실

- 플레이, Dev 시작, 탐색 및 추격 배치 편집은 모두 `Assets/Scenes/InGame/WaitingRoom/WaitingRoom.unity`를 사용한다.
- 빌드에 등록된 대기실은 이 씬 하나다. Base·Pursuit 씬과 Dev Mode의 대기실 씬 선택 메뉴는 제거했다.
- 리허설 코스와 모니터는 Rehearsal 단계에서 작동한다. 추격 방, 순찰 경로, 배치 및 장애물은 같은 씬에 저장되며 추격 활성화는 WaitingRoom 게임 설정을 따른다.
- 방 배치 변경은 통합 씬에서 직접 저장한다. Base 폴더에 남아 있는 공용 모델·머티리얼·제작 도구는 통합 씬에서도 사용한다.
