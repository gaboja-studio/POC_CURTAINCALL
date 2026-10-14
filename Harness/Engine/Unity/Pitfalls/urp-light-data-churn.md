---
summary: URP 프로젝트에서 씬을 열면 Light·Camera에 UniversalAdditionalLightData/CameraData가 저절로 붙어 .unity가 바뀐다. 씬을 고치지 않았으면 커밋하지 말고 되돌린다
status: active
updated: 2026-10-14
source: Task-20261014-033
---

# URP 조명 데이터가 씬에 저절로 추가됨

## 언제 읽나

`git status`에 열어 보기만 한 `.unity`가 수정으로 잡혔을 때. 특히 다른 Task 소유 씬이 바뀌어 구역 검사가 막힐 때.

## 증상

- diff가 30줄 남짓: `Directional Light`의 `m_Component`에 항목 하나가 늘고, `UniversalAdditionalLightData`(guid `474bcb49853aa07438625e644c072ee6`) 블록이 새로 생긴다.
- 씬을 저장한 적이 없어도 플레이·씬 전환 뒤에 생긴다.

## 원인

URP는 Light·Camera에 추가 데이터 컴포넌트가 없으면 자동으로 붙인다. 빈 템플릿으로 만든 테스트 씬은 처음 열 때 이 컴포넌트가 없어서 생긴다. 사람의 편집이 아니다.

## 대처

- 그 씬을 고치는 Task가 아니면 **커밋하지 않고 되돌린다**: `git checkout -- "<.unity 경로>"`. 남의 작업 구역 파일은 되돌리지 않으면 구역 검사가 막는다.
- 그 씬을 소유한 Task라면 한 번 저장해 함께 커밋하면 다시 생기지 않는다.

## 확인 방법

`git diff -- "*.unity"`가 `UniversalAdditional*Data` 블록 추가뿐인지 본 뒤 되돌린다.
