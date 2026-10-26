# Task-20261015-043

- **Title:** 인간 대포 4단계 — 최종 구간과 실제 도착
- **Type:** feat
- **Assignee:** @RagSpirin
- **Branch:** feat/human-cannon-finale-result
- **Base:** integration/main-stage-human-cannon

## Context
- 쿠션≠성공/회복/부활/재시작, 1인 대포 4대·동일 힘·새 F 발사·부상 무관, 공유 톱 주기와 협동 후 단독 완수를 검증한다.
- 공용 실패 기준을 생존 1명 미만으로 변경하고, 실제 목적지 도착 성공/도착 전 전멸 실패를 공용 결과·런 종료에 연결해 검증한다. 연결 전에는 통합 종료 검증 미완료다.
043 working. 042 #111/75862ef 통합 후 HEAD0128623의 최신 부모204be8c·dev d8b6db2 수신 확인, 042 구역·씬 반환 → 043 단독 이관. 2026-10-16 사용자 전달 PM 승인으로 공용 생존 1명 기준 변경부터 진행한다.

## 현재 참고
- integrated / integrate-work — #121/70e242a 병합·044 HEAD9a0d078 수신, 구역·씬 반환 완료. 기존 제출 기록: [PR #121](https://github.com/gaboja-studio/POC_CURTAINCALL/pull/121), 구현52b3367 push 완료. [제출 검증/위임 근거](History/20261016-submit-verification.md). 최신 사용자 실입력·실제4인 확인은 미기록 유지.
- 공용 실패 생존1명 기준·쿠션·독립 최종4대·모든 대포 잠금 해제·원본 오른쪽10구간 연습(F8/R/F9) 구현.
- 최신 범위 지시 반영: 원본 전체/모든 연습 기본1인칭·운반자유3인칭/놓기1인칭, 모든 대포 탑승3인칭/발사즉시1인칭. 별도 범위가 없으면 기존 연속 코스 전체가 기본. [검증](History/20261016-course-wide-carry-camera.md).
- 최종 가로/수직 톱·최초 진입 공유 시각·후발 발사 유지·접촉당1부위, FINISH 실제 도착→공용 정산/종료 연결. Waiting1인 연습은 별도 연습 판정.
- PR: https://github.com/gaboja-studio/POC_CURTAINCALL/pull/121 — 대상 integration/main-stage-human-cannon(Squash). 044 전환·병합 완료, 실제4인·사용자 체감 미기록, 실제 공연장/공용 공간 라우팅 자산은 범위 밖.

## 남은 확인
1. HumanCannon 원본 코스 시작1인칭·잡기3인칭/정지 둘러보기/놓기1인칭 확인. F8→2/7 운반·4~6 승강기에서도 동일 동작 확인.
2. F8→10 최종 대포에서 F 탑승→놓고 새 F 발사→WASD 회피→FINISH 착지, R 재시도·외팔 프리셋 확인.
3. 043 #121 통합 확인·044 이관 완료. 위임 전환8개 자동 FAIL·승인 근거는 PR에 보존한다. 원본 단독 경로·직접 플레이 결과 별도 기록.

## 기록
- 현재 할 일은 [todo.md](todo.md), 특별한 문제·결과·다음 행동은 [log.md](log.md).
- [이전 계획](History/Harness-v1/plan.md) · [현재 상태·미검증 원문](History/Harness-v1/handoff.md) · [목표·결정 원문](History/Harness-v1/meta.md).
- [기존 경로·참조 정보](History/Harness-v1/setup.md)는 참고용이다. 작업 구역·인계·Status·순차 공동 테스트 제한은 적용하지 않는다.
