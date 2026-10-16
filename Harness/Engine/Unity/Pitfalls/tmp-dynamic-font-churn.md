---
summary: TMP 동적 폰트 에셋(Atlas Population Mode = Dynamic)은 에디터·빌드 중에 글자 아틀라스가 채워지거나 비워지며 .asset이 바뀐다. 이 프로젝트의 한글 폰트는 Static으로 구워 해결, 새 글자가 빠지면 Bake Static Fonts 메뉴로 다시 굽는다
status: active
updated: 2026-10-16
source: Task-20260930-005, fix/static-fonts (PM 요청 2026-10-16)
---

# TMP 동적 폰트 에셋이 저절로 수정됨

## 언제 읽나

`git status`에 `* SDF.asset`(TextMesh Pro 폰트 에셋)이 수정으로 잡혔는데 폰트를 고친 적이 없을 때. 화면에 □(빠진 글자)가 보이거나 Console에 "character ... was not found" 경고가 나올 때.

## 증상

- 폰트를 건드리지 않았는데 `Assets/Resources/Fonts/.../* SDF.asset`이 수정됨으로 나온다.
- diff가 수천 줄: 아틀라스 텍스처 `m_Width/m_Height`가 2048 ↔ 1로 바뀌고 `_typelessdata`·글자 표(`m_Unicode`)가 생기거나 사라진다.

## 원인

동적 폰트 에셋은 화면에 처음 나온 글자를 실행 중에 아틀라스에 추가하고, 그 결과가 에셋에 저장된다. `Clear Dynamic Data On Build`가 켜져 있으면 빌드·정리 때 아틀라스를 비운다(1×1). 어느 쪽이든 의도한 편집이 아니다.

## 대처

- 2026-10-16 해결: `Pretendard-Medium SDF`와 더미 전용 `dummy-korean`을 **Static**으로 구웠다(코드 `Assets/Scripts`에 나오는 글자 + ASCII + 기호, `Clear Dynamic Data On Build` 끔). Play·저장 뒤에도 .asset이 바뀌지 않는다.
- 화면에 새 글자를 쓰는 코드를 추가했는데 □가 보이거나 "character ... was not found" 경고가 나오면 `Tools/CurtainCall/Bake Static Fonts`를 실행하고 바뀐 폰트 .asset을 함께 저장한다(`Assets/Scripts/Systems/Fonts/Editor/StaticFontBaker.cs`). 더미 빌더도 필요할 때 다시 굽는다.
- 새 한글 폰트를 Dynamic으로 만들면 같은 문제가 다시 생긴다. 만들면 `StaticFontBaker.Fonts`에 경로를 추가한다.
- 그래도 폰트 .asset이 잡히면, 직접 바꾼 게 아니면 **커밋하지 않고 되돌린다**: `git checkout -- "<폰트 .asset 경로>"`. 로컬에서만 숨기는 `git update-index --skip-worktree`는 진짜 수정도 가려서 쓰지 않는다.

## 확인 방법

`git diff --stat -- "Assets/Resources/Fonts/"`가 비어 있는지 확인한 뒤 저장한다. 폰트 Inspector의 Atlas Population Mode가 Static인지 본다.
