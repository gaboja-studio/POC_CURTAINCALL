# Plan

## Context

프로젝트 개요와 내용을 팀원이 찾아볼 수 있는 문서로 정리한다. 제공된 `game-flow.md`를 기획 기준으로 사용하되 미정 항목과 저장소의 실제 구현 상태를 구분한다.

## Approach

1. 제공된 게임 흐름 원문을 `Docs/References/game-flow.md`에 그대로 보존한다.
2. `Harness/Project/Facts/overview.md`와 `game-flow.md`에 기획·구현 상태·미정 사항을 구분해 요약하고 `index.md`로 연결한다.
3. 원문의 기본값·숫자를 확정으로 바꾸거나 없는 묘기 세부 스펙을 추정하지 않는다.
4. 문서 검사와 공동 리뷰 후 별도 지시에 따라 integration/document-project로 제출한다.

## Files

- 수정: `Harness/Project/Facts/overview.md`, `game-flow.md`, `index.md`, 이 Task의 setup·plan·todo·handoff.
- 원본 보존: `Docs/References/game-flow.md` (setup.md에 배정, Integration 소유 표 PM 확인 필요).
- 읽기 전용: AGENTS.md, `Harness/Project/Facts/unity-project.md`. Assets 폴더 규칙은 Task-20260930-002의 소유다.

## Acceptance Criteria

- 확정 정보·TBD·확인 필요 사항이 명확히 구분된다.
- 프로젝트 목적·범위·현재 상태·내용과 참고 문서 링크가 제공된다.
- 원본이 바이트 단위로 보존되고 Facts frontmatter·색인·크기·링크 검사가 통과한다. 구역 검사는 브랜치/파서 제한을 별도 보고한다.

## Dependencies

- 선행 Task 없음. Task-20260930-002와 독립 구현 가능.
- 공용 문서 링크를 수정해야 한다면 PM이 Integration 소유 표에 배정한다.

## Risks

- 미정 정보를 확정 사실로 쓰는 오류, 기존 사실 문서와 중복·불일치.

## Auto Verification

- 구역 검사: 변경 경로를 setup.md와 수동 대조. Scripts/verify-scope.ps1은 현재 워크트리 브랜치와 Harness 경로 파서 제한으로 PASS 판정 불가; PM 후속 조정.
- 1 문서/규칙: Scripts/verify-knowledge.ps1, verify-context.ps1, verify-fast.ps1 및 링크·원본 해시 수동 점검.
- 2 컴파일: 문서만 변경하면 생략하고 사유 기록.
- 3 테스트: Unity 테스트는 문서 작업에 해당 없음. 미실행을 PASS로 표기하지 않는다.
- 4 실행 로그: 게임 실행 불필요. 문서 검사 결과를 handoff.md에 기록.

## 공동 테스트 항목

1. 처음 읽는 사람이 프로젝트 목적·범위·현재 상태를 설명할 수 있는지 확인한다.
2. 기획 기준·현재 구현 상태·※미정/임시값을 구분하고 원본 링크를 따라갈 수 있는지 확인한다.
3. Assets 규칙 문서와 책임·내용이 중복되지 않는지 통합 후 확인한다.
