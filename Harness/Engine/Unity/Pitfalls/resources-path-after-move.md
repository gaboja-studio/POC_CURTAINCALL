---
summary: Resources 폴더 안에서 에셋을 옮기면 Resources.Load 문자열 경로가 깨지고, 패키지 설정 에셋(DOTweenSettings·UnityPlayerAccountSettings)은 Resources 루트에서만 찾는다
status: active
updated: 2026-10-08
source: human (2026-10-08 폴더 재배치)
---

# Resources 안 폴더 이동 후 로딩 실패

## 언제 읽나

`Assets/Resources/` 아래 폴더·에셋을 옮기거나 이름을 바꿀 때, 또는 옮긴 뒤 세팅이 기본값으로 동작할 때.

## 증상

- 컴파일은 통과하는데 실행 중 `Resources.Load`가 null을 돌려 세팅·프리팹을 못 읽는다.
- DOTween·Player Accounts가 설정을 못 찾아 기본값으로 돌거나 Resources 루트에 새 설정 파일을 만든다.

## 원인

- `Resources.Load("A/B")`는 GUID가 아니라 **문자열 경로**로 찾는다. Unity 안에서 옮겨 `.meta`가 유지돼도 코드의 문자열은 바뀌지 않는다.
- `DOTweenSettings`, `UnityPlayerAccountSettings`는 패키지가 Resources 루트 이름으로 찾는다.

## 대처

- 옮기기 전에 `grep -rn 'Resources.Load' Assets/Scripts`로 경로 문자열을 찾아 함께 고친다.
- 패키지 설정 에셋 두 개는 `Assets/Resources/` 루트에 둔다.

## 확인 방법

에디터에서 `Resources.Load`로 옮긴 경로를 직접 불러 null이 아닌지 확인한다(2026-10-08: `Settings/GameSettings/GameSettings`, `DOTweenSettings`, `UnityPlayerAccountSettings` 모두 확인).
