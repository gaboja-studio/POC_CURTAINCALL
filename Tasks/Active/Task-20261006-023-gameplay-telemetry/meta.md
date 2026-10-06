# Task-20261006-023

- **Title:** 콘텐츠별 개인 행동 JSON 기록
- **Type:** feat
- **Status:** working
- **Assignee:** @MoHoDu
- **Domain:** `Docs/Domains/tightrope-course.md`
- **Current Skill:** PM 승인 일괄 구현
- **Updated:** 2026-10-06

Status 순서: scaffolded(PM 준비) → assigned(인계) → working(작업 중) → submitted(PR 제출) → integrated(공동 테스트 통과) → done(dev 병합). 막히면 blocked.

## Workspace

- **Integration:** integration/tightrope-prototype
- **Branch:** integration/tightrope-prototype (2026-10-06 승인 예외, 별도 브랜치/PR 생략)

## Goal

- 개인×콘텐츠×시도 JSON으로 실제 행동·사지 결과를 대조하고 콘텐츠별 최근 10개·저장 실패/중단 상태를 확인한다. 최종 행동 hook·공유 ID 연결은 022에서 완성하며 외부 전송은 하지 않는다.

## Scope

- 작업 구역·공용 파일·수정 금지 목록은 `setup.md`(PM 작성)가 기준이다.
- AI Setup Allowed: None

## Decisions

- Open: 없음 (2026-10-06 후속 계획 승인)
- Decided: 015 결과 병합 후 구현, 016에는 기록 계약 제공. 개인/콘텐츠별 최근 10개, 익명 ID, bounded buffer·checkpoint·복구·늦은 결과 병합. 새 패키지·Google 업로드 제외.
