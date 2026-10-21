# Task-20261020-065

- **Title:** 실제 대기실 복귀와 물리 출구 진행 연결
- **Type:** fix
- **Status:** submitted
- **Assignee:** @gyumin9048-ctrl
- **Domain:** common-game-systems
- **Current Skill:** submit-work
- **Updated:** 2026-10-21

## Workspace

- **Integration:** dev
- **Branch:** fix/waiting-room-bugs-improvements
- **Previous Task:** Task-20261020-064

## Goal

- 공연 종료 후 실제 대기실로 복귀하고, 개인의 물리 퇴장으로 팀 탐색 타이머가 시작되어야 한다.
- 더미 탐색 씬으로 전원을 이동하지 않고 실제 무대 입구 집결로 다음 공연에 진입한다.

## Scope

- 작업 구역은 setup.md 참조. 실제 씬 연결·전환 정책·개인 재진입 보정 구현 및 자동 검증 완료. 실제 2인 공동 확인 대기, PR #191 제출(dev 대상·미병합).
- AI Setup Allowed: None

## Decisions

- Decided (2026-10-21 16:20 KST, PM 채팅 긴급 승인): 기존 Task-065의 입구 후속 수정을 현재 작업자 @gyumin9048-ctrl에게 임시 재배정한다. 기존 브랜치는 fix/waiting-room-physical-flow, 담당은 @MoHoDu였다. 현재 브랜치에서 상점 위치 복구·떠 있는 소품 조사·개인 재입장 허용 및 관련 회귀 검증을 수행한다. 예외는 exception.md에 기록하며 커밋·push는 별도 요청 전까지 하지 않는다.

- Open: 실제 2인 공동 검증.
- Decided: 사용자 명시 승인으로 Task-064만 로컬 커밋 6bd83ca에 분리, 로컬 통합 기준으로 사용. 이후 승인으로 Task-065와 함께 push·dev PR #191 제출.
- Decided: 밤 타이머 도입·새 백스테이지 제작·미병합 공간 브랜치 병합은 제외.
