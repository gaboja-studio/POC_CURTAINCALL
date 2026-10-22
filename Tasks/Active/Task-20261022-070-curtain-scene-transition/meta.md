# Task-20261022-070

- **Title:** 커튼 전환 동기화 및 탐색 경고 상단 표시 긴급 수정
- **Type:** fix
- **Status:** submitted
- **Assignee:** @MoHoDu
- **Domain:** 시작·로비 / 장소 전환
- **Current Skill:** submit-work
- **Updated:** 2026-10-22

## Workspace

- **Integration:** dev
- **Branch:** fix/curtain-scene-transition
- **Previous Task:** 없음 (PM 긴급 수정)

## Goal

- 연결된 참가자 모두의 커튼 닫힘 확인 후 씬 전환, 각 PC의 씬·배경·UI 준비 후 열림.
- 탐색 경고를 기존 상단 배너 위치·최상위 정렬로 표시(알림 시점 유지).

## Scope

- PM이 2026-10-22 채팅에서 기존 Task-061·062와 겹치는 커튼 4개 파일 승인 후 CommonWaitingRoomUI.cs의 긴급 UI 수정을 추가 승인.
- 범위는 setup.md. 다른 worktree의 변경을 가져오거나 기존 Task 소유권을 일괄 해제하지 않는다.
- AI Setup Allowed: None

## Decisions

- Open: 실제 2인 확인.
- Decided: 추가 UI 최소 검증 후 커튼 패치와 같은 dev PR에 제출(2026-10-22 사용자 요청).
- Decided: 별도 worktree, fix에서 dev로 직접 PR, 기존 씬·에셋·세팅 유지.
