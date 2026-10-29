---
summary: NGO는 NetworkObject 없는 로컬 additive 씬도 늦은 참가 동기화 목록에 넣을 수 있으므로 공개 검증 콜백으로 제외한다
status: active
updated: 2026-10-21
source: Task-20261021-068
---

# 로컬 배경 씬과 NGO 동기화

## 언제 읽나

Unity SceneManager로 로컬 시각 전용 배경을 additive 로드하며 NGO도 함께 사용할 때.

## 함정

- NGO 2.13.3은 서버의 Synchronize에서 Unity SceneManager의 현재 씬 전체를 순회한다.
- NetworkObject가 없거나 NGO가 아닌 Unity API로 열었다고 자동 제외되지 않는다.
- 따라서 각 피어가 이미 연 배경을 늦은 참가 동기화가 다시 열 수 있다.

## 대처

- 서버가 클라이언트를 동기화하기 전에 NetworkSceneManager.VerifySceneBeforeLoading을 설치한다.
- 배경 씬 이름에는 false를 반환하고 기존 검증 콜백은 보존한다. Unity API의 로컬 로드는 이 콜백과 별개다.
- NetworkManager가 재시작하면 새 SceneManager에 다시 설치한다.
- Single 동기화는 클라이언트의 로컬 배경을 제거할 수 있으므로 실제 묘기 로드 후 다시 조정한다.
- ClientSynchronizationMode를 배경 때문에 임의로 바꾸지 않는다. 게임 전체 씬 수명 정책에 영향을 준다.
- 내부 ExcludeSceneFromSychronization 필드는 공개 API가 아니므로 reflection으로 수정하지 않는다.

## 근거

- NGO NetworkSceneManager.cs의 서버 Synchronize 전체 씬 순회 및 VerifySceneBeforeLoading 공개 delegate.
- Task-068 PlayMode의 HostSynchronizationExcludesLocalBackground 통과. 실제 두 프로세스 늦은 참가 검증과는 구분한다.
