---
summary: EditMode 테스트에서 일반 컴포넌트의 Awake 의존 참조를 명시적으로 초기화한다
status: active
updated: 2026-10-20
source: Task-20261019-062
---

# EditMode 테스트의 Awake 초기화 누락

## 언제 읽나

EditMode에서 AddComponent로 만든 컴포넌트가 실제 Play에서는 정상인데 참조 접근 시 NullReferenceException을 낼 때.

## 증상

- TightropeSurfaceControlTests 3개가 NetworkPlayer.CurrentJump에서 실패했다.
- fixture는 Mover/Balance/Controller의 Awake만 수동 호출하고 NetworkPlayer 초기화는 빠뜨렸다.

## 원인

일반 MonoBehaviour의 Awake가 EditMode AddComponent에서 실행됐다고 가정하면 안 된다. NetworkPlayer는 Awake에서 Mover 등의 참조를 채우므로 이후 상태 조회가 초기화되지 않은 참조를 읽는다.

## 대처

- fixture가 의존 컴포넌트와 조회 대상의 초기화 순서를 명시적으로 재현한다.
- 위 fixture는 기존 수동 초기화 다음에 NetworkPlayer.Awake 호출을 추가한다.
- 테스트 준비 누락을 숨기기 위해 production getter에 무조건 기본값 반환을 추가하거나 단언을 제거하지 않는다.

## 확인 방법

문제 fixture를 먼저 다시 실행하고 전체 EditMode를 재검사한다. PlayMode 회귀도 별도로 확인한다.
