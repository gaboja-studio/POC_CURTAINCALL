# Setup

PM 직접 연결 작업. 현재 프로젝트·연결된 Unity를 사용하며 다른 worktree는 만들지 않는다.

## JIRA

- 관련 JIRA: 없음 (PM 직접 요청, 순차 gate 통합 아님)

## 작업 구역

- `Assets/Scripts/Systems/PerformanceBackground/`
- `Assets/Scripts/Systems/PerformanceBackground.meta`
- Task 문서: 이 Task 폴더 (자동 포함)

## 예약 구역

- 없음

## 배정된 공용 파일

- `ProjectSettings/EditorBuildSettings.asset` — 배경 씬 추가만
- `Assets/_ThirdParty` — #196의 b6cc29c7 포인터 반영만. private 파일 직접 편집 없음

## 수정 금지

- `Assets/Scenes/` — 기존 묘기·코스·카메라 저장본 유지
- `Assets/Resources/` — 기존 게임 설정 유지
- `Assets/_ThirdParty/` — private 에셋 직접 수정·생성 없음
- `Packages/`
- `ProjectSettings/Packages/` — 기존 Vivox 로컬 변경 보존·커밋 제외
- 다른 Task의 스크립트·씬·공용 프리팹

## Exclusive Assets

- EditorBuildSettings.asset은 이번 연결 작업에서 배경 씬 추가만 수행한다.
- 연결된 에디터의 경로와 dirty 상태를 확인한 뒤 에디터 API로 처리한다.

## 테스트 씬 처리

- 결정: 새 테스트 씬은 만들지 않고 기존 HumanCannon/Tightrope와 런타임 임시 씬을 검증에 사용한다.
- 결정자·날짜: PM 요청 기반, 2026-10-21
