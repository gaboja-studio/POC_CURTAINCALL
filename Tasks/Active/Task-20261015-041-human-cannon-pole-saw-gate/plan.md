# Task-20261015-041

- **Title:** 인간 대포 2단계 — 봉과 필수 톱관문
- **Type:** feat
- **Assignee:** @RagSpirin
- **Branch:** feat/human-cannon-pole-saw-gate
- **Base:** integration/main-stage-human-cannon

## Context
- 봉 F 잡기·W/S 반동·놓기 도약과 톱 수납/제한시간/예고 재동작, 필수 관문 열기→지상 통과→다시 받기, 담당 사망 후 반동 중 F 인계를 외팔 조건에서도 검증한다.
041은 현재 Active Task다. 040 #93/8293bb9를 현재 브랜치 da4747c로 수신했다. AI가 041 전체 코드·씬 초안을 먼저 작성했다. 사용자 봉 조작·Space·톱 변화 확인 완료, 관문·사망 인계 실제 확인은 4인 플레이로 연기. 2026-10-15 최신 지시로 기존 관리 문서 포함·MPPM 설정 제외하여 041만 제출하며 이전 PM 선반영 대기는 해제한다. 042 구현/전환은 새 채팅에서 진행한다.

## 현재 참고
- integrated / write-handoff — 2026-10-16 PR #108 MERGED(292667c)·042 dae56af 수신 확인. 기존 PM 위임으로 041 Allowed를 반환하고 코드·테스트 구역/씬을 042에 단독 이관했다. 봉 조작·Space·톱 변화 확인 완료, 관문·사망 인계 실제 확인은 전체 통합 후 4인 플레이에 남긴다.
- 현재 PC 재개: fc6b306 수신 상태에서 CLI beta.6→beta.13 갱신, 사용자 Pipeline Stop/Start 후 C:\Unity\POC_CURTAINCALL ready 연결 복구. 이후 봉 입력·HUD 코드만 변경, 씬·공용 입력 변경 없음.
- 2026-10-15 PM 정정: Task 안에서도 한 기능 구현→사용자 확인→다음 기능 구현. 5Task 인계 완료는 내부 기능 일괄 구현 승인이 아니다. 담당 @RagSpirin.
- PR: https://github.com/gaboja-studio/POC_CURTAINCALL/pull/108 — feat/human-cannon-pole-saw-gate → integration/main-stage-human-cannon, MERGED 292667c68ed7f25543b332ec162c6bd886a087db. 042 착수 기준 dae56afe38f7fa939fcc103f22d158262ad9a664가 포함하며 당시 원격 feat도 동일했다. 전환 문서는2026-10-16 다른 PC 재개용042 저장 묶음에 포함, 상위 통합 반영은 별도.
- 구현 초안: 일반/연결 봉 조작·톱 수납→예고→재동작·호스트 점유/비행 식별자 동기화·사망/연결 해제/R 복구. 운반자 관문 통과 후 비행자 위/옆 통과 허용, 040 중앙 재반동·외곽 F 인계 재사용 및 봉 점유 중 중복 운반/반동/착지 차단.

## 남은 확인
1. 필수 관문 열기→운반자 통과→너머 다시 받기·우회 차단은 사용자 지시로 이후 실제 4인 플레이에 남긴다. 현재 PASS로 처리하거나 즉시 재확인을 요구하지 않는다. 재현·예상은 plan의 공동 테스트 2번 참조.
2. 사망 인계도 사용자 지시로 이후 실제 4인 플레이에 남긴다. 기존 소유권 해제·제자리 반동·공중 F 손잡이 인계 경로만 점검 완료. plan 공동 테스트 3번 유지, 실제 플레이 PASS 아님.
3. 042에서 분리 출발·첫 받기부터 구현한다. 041 제출 당시 Assets38+관리/작업 문서35=73파일, 자동 구역 검사의 관리 문서30개 실패는 사용자 포함 승인과 함께 PR에 공개했다. 이 통합이 기존 미검증 항목의 PASS를 뜻하지 않는다.

## 기록
- 현재 할 일은 [todo.md](todo.md), 특별한 문제·결과·다음 행동은 [log.md](log.md).
- [이전 계획](History/Harness-v1/plan.md) · [현재 상태·미검증 원문](History/Harness-v1/handoff.md) · [목표·결정 원문](History/Harness-v1/meta.md).
- [기존 경로·참조 정보](History/Harness-v1/setup.md)는 참고용이다. 작업 구역·인계·Status·순차 공동 테스트 제한은 적용하지 않는다.
