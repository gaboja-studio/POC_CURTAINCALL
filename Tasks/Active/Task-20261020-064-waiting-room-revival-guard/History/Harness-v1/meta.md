# Task-20261020-064

- **Title:** 인간대포 종료 후 대기실 부활 관전 잔류 수정
- **Type:** fix
- **Status:** working
- **Assignee:** @MoHoDu
- **Domain:** player-sync
- **Current Skill:** investigate-bug
- **Updated:** 2026-10-20

## Workspace

- **Integration:** dev
- **Branch:** fix/waiting-room-revival-guard
- **Previous Task:**

## Goal

- 첫째날 인간대포 사망자가 밤 정비에서 부활한 뒤 이전 씬의 피해 판정으로 다시 사망하지 않고 대기실에서 조작·시점을 복구한다.

## Scope

- setup.md의 세 파일과 현재 Task 문서. 기존 테스트 씬 재사용.
- AI Setup Allowed: PM 직접 로컬 착수, 회귀 테스트·최소 수정·검증 (2026-10-20 승인 계획).

## Decisions

- Open: 실제 호스트/참가자 공동 플레이 검증.
- Decided: 카메라 강제 초기화 대신 기존 관리형 공연 사망 가드를 보강한다. 단독 연습과 공연 중 사망은 유지한다. 커밋·push·PR 없음.
