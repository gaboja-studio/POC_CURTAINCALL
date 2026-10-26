# Task-20261015-040

- **Title:** 인간 대포 1단계 — 발사와 트램폴린 받기
- **Type:** feat
- **Assignee:** @RagSpirin
- **Branch:** feat/human-cannon-launch-rebound
- **Base:** integration/main-stage-human-cannon

## Context
- 첫 받기 위치에서 시작하는 장애물 없는 4인 기본 묘기로 발사·수평 비행·이동/점프 트램폴린 받기를 검증한다.
- PR #93/8293bb9로 인간 대포 통합에 Squash 완료, 2026-10-15 PM 완료 확인. F/WASD·거리·트램폴린/지상 점프 1~9·쿠션 1~5 사용자 정상 확인. 4인 검증은 전체 기능 통합 후 진행.
CC-124의 첫 단계 구현은 #93 통합 완료. 아래 구현/검증 계획은 보존한다. 2026-10-15 [PM 인계·순차 실행 승인](../../../Harness/Project/Decisions/Human-cannon/sequential-handoff.md)으로 041에 구역/씬을 반환했고, 4인 지상1+비행3 검증은 전체 기능 통합 후 수행한다.

## 현재 참고
- 단계: integrated — PR #93/8293bb9 통합·2026-10-15 PM 완료 확인, 담당 @RagSpirin. 041 브랜치 da4747c 수신·040 구역/씬 반환 완료.
- 작성한 것: 대포/WASD·트램폴린 정면 운반/회전·점프/놓기·지상 점프 반동·무주인/인계·호스트 요청 검증. 추가: 쿠션 윗면 정상 착지→호스트 확정/비행 해제/지상 조작 복귀, 일반 바닥 사망 유지, 안전 착지 횟수 HUD/R 초기화.
- 사용자 확인 범위: 기존 F 1~4·거리/WASD 정상. 10-15 트램폴린/지상 점프 1~9는 혼자 플레이 확인으로 명시, 추가 쿠션 1~5(착지/조작/횟수/바닥 사망/R) 모두 정상 보고. 4인 공동 검증은 미확인.
- 반복 테스트: R → 방 유지·전원 복구·점유/비행/키 정리·장치 복원. 오류 점검 수정: 빈 대포도 revision 증가/Reset 전송, 이전 탑승 거부·늦은 발사/취소 위치를 출발점 복구. 발사 revision+반동 segment 검증, 운반자 정상 점프 제외.
- 씬: HumanCannon.unity 대포1·트램폴린1(4×4m/높이0.5m/z25.03)·주황 쿠션1(4×4m/높이0.2m, x6/z25.03)·발판/눈금/출발4/LAN/HUD 저장. root10, dirty=false. 잡기2m·대포 수평8/상승12·WASD3m/s, 반동 전진0/상승12, 공용 점프0.8m 유지.

## 남은 확인
1. [현재 PM 승인](../../../Harness/Project/Decisions/Human-cannon/sequential-handoff.md): 040~044 전부 인계 완료·순차 실행 가능. 041이 assigned/Active Task/구역·씬 소유자다. 추가 인계 승인 불필요.
2. 전체 기능 통합 후 멀티: 역할 교환·이동/점프 받기·다인/무주인 반동·사망/접속해제·공중 인계·쿠션 착지. plan 3묶음 보존, 현재 미실행.

## 기록
- 현재 할 일은 [todo.md](todo.md), 특별한 문제·결과·다음 행동은 [log.md](log.md).
- [이전 계획](History/Harness-v1/plan.md) · [현재 상태·미검증 원문](History/Harness-v1/handoff.md) · [목표·결정 원문](History/Harness-v1/meta.md).
- [기존 경로·참조 정보](History/Harness-v1/setup.md)는 참고용이다. 작업 구역·인계·Status·순차 공동 테스트 제한은 적용하지 않는다.
