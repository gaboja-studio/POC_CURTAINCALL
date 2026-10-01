---
summary: Unity가 씬·프리팹 YAML에 줄 끝 공백(`m_Name: `)을 저장해 git diff --check·verify-fast가 실패함
status: active
updated: 2026-10-01
source: Task-20260930-004
---

# Unity YAML 줄 끝 공백으로 diff --check 실패

## 언제 읽나

씬·프리팹을 저장한 뒤 `verify-fast`·`save-work`가 `Fast: FAIL (git diff --check)`, `trailing whitespace`로 멈출 때.

## 증상

`m_Name: `, `value: ` 처럼 값이 빈 줄 끝에 공백이 남아 `git diff --check`가 오류를 낸다.

## 원인

Unity 직렬화 형식이다. 공백을 지워도 다음 저장 때 다시 생긴다.

## 대처

- `.gitattributes`의 `[attr]unity-yaml` 매크로에 `-whitespace`를 둔다(2026-10-01 적용, 통합 브랜치 기준).
- 씬 파일의 공백을 손으로 지우지 않는다.

## 확인 방법

`git check-attr whitespace -- Assets/Scenes/Tests/PlayerControl/PlayerControl.unity` → `unset`, 그리고 `git diff --check` 통과.
