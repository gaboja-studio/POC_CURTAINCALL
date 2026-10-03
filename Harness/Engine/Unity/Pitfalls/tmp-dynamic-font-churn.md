---
summary: TMP 동적 폰트 에셋(Atlas Population Mode = Dynamic)은 에디터·빌드 중에 글자 아틀라스가 채워지거나 비워지며 .asset이 바뀐다. 사람이 고치지 않았으면 커밋하지 말고 되돌린다
status: active
updated: 2026-10-03
source: Task-20260930-005
---

# TMP 동적 폰트 에셋이 저절로 수정됨

## 언제 읽나

`git status`에 `* SDF.asset`(TextMesh Pro 폰트 에셋)이 수정으로 잡혔는데 폰트를 고친 적이 없을 때. 저장 전 변경 목록을 정리할 때.

## 증상

- 폰트를 건드리지 않았는데 `Assets/Resources/Fonts/.../* SDF.asset`이 수정됨으로 나온다.
- diff가 수천 줄: 아틀라스 텍스처 `m_Width/m_Height`가 2048 ↔ 1로 바뀌고 `_typelessdata`·글자 표(`m_Unicode`)가 생기거나 사라진다.

## 원인

동적 폰트 에셋은 화면에 처음 나온 글자를 실행 중에 아틀라스에 추가하고, 그 결과가 에셋에 저장된다. `Clear Dynamic Data On Build`가 켜져 있으면 빌드·정리 때 아틀라스를 비운다(1×1). 어느 쪽이든 의도한 편집이 아니다.

## 대처

- 폰트를 직접 바꾼 게 아니면 **커밋하지 않고 되돌린다**: `git checkout -- "<폰트 .asset 경로>"`.
- 계속 잡히는 게 귀찮으면 PM이 결정한다: 쓰는 글자(한글 2350자 등)를 미리 굽고 Atlas Population Mode를 Static으로 바꾸는 방법(폰트 담당 배정 필요). 로컬에서만 숨기는 `git update-index --skip-worktree`는 진짜 수정도 가려서 쓰지 않는다.

## 확인 방법

`git diff --stat -- "Assets/Resources/Fonts/"`가 비어 있는지 확인한 뒤 저장한다.
