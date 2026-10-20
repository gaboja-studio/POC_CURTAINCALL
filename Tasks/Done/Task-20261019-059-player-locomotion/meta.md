# Task-20261019-059

- **Title:** 달리기·앉기 공용 조작
- **Type:** feat
- **Status:** done
- **Assignee:** @MoHoDu
- **Domain:** player-control
- **Current Skill:** start-work
- **Updated:** 2026-10-19

Status 순서: scaffolded(PM 준비) → assigned(기술 배정, 실전달 별도) → working(작업 중) → submitted(PR 제출) → integrated(검증 통과) → done(dev 병합). 막히면 blocked.

## Workspace

- **Integration:** integration/player-locomotion
- **Branch:** feat/player-locomotion
- **Previous Task:**

## Goal

- 대기실·배치 미리보기·외줄/인간 대포 플랫폼에서 LShift 홀드 달리기·LCtrl 홀드 앉기가 같은 규칙으로 동작하고, 묘기 전용 조작에서는 입력 로그만 남으며 온라인에서 앉기 자세가 공유됨을 확인한다.

## Scope

- 작업 구역·공용 파일·수정 금지 목록은 `setup.md`(PM 작성)가 기준이다.
- AI Setup Allowed: None

## Decisions

- Open: None
- Decided: 2026-10-19 PM @MoHoDu — 홀드 방식(조합 토글 미적용), 동시 홀드 앉기 우선, 달리기 1.5·앉기 이동 0.5·높이 비율 0.6(공통 설정), 앉기 시 점프 금지·기립 공간 검사·1인칭 눈높이 반영·온라인 공유, 전용 애니메이션 없음.
