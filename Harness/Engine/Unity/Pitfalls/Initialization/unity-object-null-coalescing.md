---
summary: Editor GetComponent의 Unity fake-null은 C# ??로 처리되지 않을 수 있다
status: active
updated: 2026-10-28
source: Task-20261027-076
---

# Unity Object와 null 병합

## 언제 읽나
- `GetComponent<T>() ?? AddComponent<T>()`를 썼는데 프리팹에 컴포넌트나 직렬화 참조가 누락될 때.

## 증상과 원인
- Settings v2 생성기의 `GetComponent<CanvasGroup>() ?? AddComponent<CanvasGroup>()`가 EditMode Preview Scene에서 컴포넌트를 만들지 않았다.
- 같은 조건에서 반환값은 `value == null`이 true지만 `ReferenceEquals(value, null)`은 false인 Unity fake-null이었다. `??`는 Unity의 오버로드된 null 비교를 사용하지 않는다.

## 대응·검증
- Unity Object는 `var value = GetComponent<T>(); if (value == null) value = AddComponent<T>();`로 처리한다.
- 생성 후 컴포넌트 개수와 실제 직렬화 참조를 검사한다. 프리팹 저장 성공만으로 충분하지 않다.
- Task-076에서는 수정 후 Preview Scene에 CanvasGroup 1개가 생성됨을 확인하고, 기존 제품 프리팹의 누락 컴포넌트/참조만 PrefabUtility로 보완했다.
