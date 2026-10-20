# Task-20261020-067

- **Title:** 공용 공연장 배경 제작 및 다른 PC 인계
- **Type:** feat
- **Status:** working
- **Assignee:** @RagSpirin
- **Domain:** MainStage
- **Current Skill:** integrate-editor
- **Updated:** 2026-10-21

## Workspace

- **Integration:** integration/main-stage
- **Branch:** feat/main-stage-background-submit
- **Previous Task:** 없음

## Goal

- 묘기별 씬과 함께 사용할 거대한 원형 폐공장 서커스 배경을 정적 에셋으로 제공한다.
- 배경 씬과 종속 에셋을 다른 PC에서 동일하게 받을 수 있게 저장한다.

## Scope

- setup.md에 지정된 private 에셋 폴더와 공개 gitlink, 인계 문서만 변경한다.
- Task-025의 기존 main_stage.unity 및 묘기 로직 구역은 변경하지 않는다.
- 2026-10-21 현재 대화 PM의 후속 적용 지시로 인간 대포·외줄 실제 묘기 씬의 배경 배치/환경 연결을 setup에 추가. 묘기 로직과 코스 크기는 유지한다.

## Decisions
- Open: 관객 없는 원본 공연장 미감·지지 구조 보강·대상 PC 성능.
- Decided: 현재 대화 PM 승인으로 Task 등록/기존 브랜치 유지. 저장 예외는 exception.md, 이전 제작·정리 결정은 [이력](History/001-background-art-before-trick-integration.md).
- Decided: 신규 자산은 _ThirdParty, 원형 공연장을 여러 묘기에서 공유. 원본 지름 1164, 후속 깊이 요청으로 바닥 Y-1080(묘기 씬 -360).
- Decided: 10-21 사용자 참조 — 위협적인 철골 지하 도시, 주황빛·옅은 스모그·조금 올라온 구조물, 하부 원형 관람대와 연속 원형 관중석. 등기구 축소는 취소.
- Decided: 10-21 사용자 — 묘기별 크기 조정/실제 적용, 인간 대포부터. 공통 원본을 보존하고 공유 프리팹 인스턴스로 인간 대포·외줄 씬에 적용.
- Decided: 10-21 사용자 — 플레이어에게 아득한 깊이. 근거리 관중석/조명 유지, 기존 도시 아래로 작업장 3층·철골·외벽·안개를 확장.
- Decided: 10-21 사용자 — 올라온 구조물 다시 낮추고 중앙 밀집 해소. 하부 도시·관람대를 외벽 주변으로 이동하고 통로/철골로 벽과 연결. 깊이는 유지.
- Decided: 10-21 최신 사용자 — 관객 모델 제거, 인간 대포/외줄에서 공연장 제거, 원본 공연장만 유지. 좌석/관람대는 유지하며 적용용 공유 프리팹 삭제. 묘기 씬 git diff 0.
