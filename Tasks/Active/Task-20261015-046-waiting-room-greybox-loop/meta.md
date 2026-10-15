# Task-20261015-046

- **Title:** 대기실 1단계 — 8공간 그레이박스와 공연 왕복
- **Type:** feat
- **Status:** assigned
- **Assignee:** @gyumin9048-ctrl
- **Domain:** common-game-systems
- **Current Skill:** start-work
- **Updated:** 2026-10-15

Status 순서: scaffolded(PM 준비) → assigned(기술 배정, 실전달 별도) → working(작업 중) → submitted(PR 제출) → integrated(검증 통과) → done(dev 병합). 막히면 blocked.

## Workspace

- **Integration:** integration/waiting-room-base
- **Branch:** feat/waiting-room-greybox-loop
- **Previous Task:**

## Goal

- 4인이 ① 로비에 스폰 → 대기실 이동 → 대기실 문 밖 ⑥ 대체 영역 전원 집결 시 더미 공연 전환 → 공연 종료 후 ① 복귀(사망자 외팔 부활)하는 기본 루프를 네 화면 동일하게 검증한다.
- 기준 씬은 `WaitingRoomBase.unity`(waiting_room.unity 복사본)이다. 원본 맵 씬은 수정하지 않는다.
- 작업 PR은 integration/waiting-room-base로 Squash한다. 기능 구현은 아직 시작하지 않았다.

## Scope

- 작업 구역·공용 파일·수정 금지 목록은 `setup.md`(PM 작성)가 기준이다.
- AI Setup Allowed: None
- 타이머·상점·밤 정비·전문가 서비스는 2~5단계(Task-047~050) 범위다.

## Decisions

- Decided: 2026-10-15 PM — ⑥ 무대 입구 통로·⑦ 공연 스테이지 입구는 백스테이지. 대기실 그레이박스는 ①~⑤·⑧과 대기실 문. ⑥은 테스트 씬에 집결 판정용 대체 통로만 두고 백스테이지 연결(CC-111) 때 교체, ⑦은 만들지 않는다.
- Decided: 2026-10-15 PM — waiting_room.unity 복사본을 1차 테스트 씬(WaitingRoomBase.unity)으로 삼아 그 씬 기준으로 작업. 원본 반영은 통합 브랜치에서 나중에 PM이 정한다.
- Decided: 2026-10-15 PM이 담당자 실제 전달 확인, 기술 배정(sync PR #97) 완료. 배정 문서 수신 뒤 바로 start-work.
- Decided: CC-132 URL 연결·1단계 제목 확인. 백스테이지·플레이어 상태/조작은 범위 밖(기존 공용 조작 사용).
- Decided: 공연 전환·복귀는 공용 더미 공간(CC-119)·공간 연결(CC-111)·부상(CC-109)을 읽기/재사용. 공용 수정은 별도 소유 Task.
