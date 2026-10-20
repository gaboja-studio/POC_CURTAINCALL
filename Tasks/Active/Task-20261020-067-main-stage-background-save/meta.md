# Task-20261020-067

- **Title:** 공용 공연장 배경 제작 및 다른 PC 인계
- **Type:** feat
- **Status:** working
- **Assignee:** @RagSpirin
- **Domain:** MainStage
- **Current Skill:** save-work
- **Updated:** 2026-10-20

## Workspace

- **Integration:** integration/main-stage
- **Branch:** feat/main-stage-background
- **Previous Task:** 없음

## Goal

- 묘기별 씬과 함께 사용할 거대한 원형 폐공장 서커스 배경을 정적 에셋으로 제공한다.
- 배경 씬과 종속 에셋을 다른 PC에서 동일하게 받을 수 있게 저장한다.

## Scope

- setup.md에 지정된 private 에셋 폴더와 공개 gitlink, 인계 문서만 변경한다.
- Task-025의 기존 main_stage.unity 및 묘기 로직 구역은 변경하지 않는다.

## Decisions

- Open: 실제 플레이 카메라 시각 검토와 대상 PC 성능 측정.
- Decided: 2026-10-20 사용자 지시 — 모든 신규 에셋은 _ThirdParty, 동일 공연장을 여러 묘기에서 공용으로 사용.
- Decided: 2026-10-20 현재 대화 사용자가 PM임을 확인하고 저장 지시. 기존 제작 작업을 이 Task에 소급 등록하고 기존 feat/main-stage-background를 유지한다. exception.md에 승인 범위를 기록한다.
