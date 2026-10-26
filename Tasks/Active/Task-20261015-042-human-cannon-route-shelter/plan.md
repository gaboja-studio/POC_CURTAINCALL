# Task-20261015-042

- **Title:** 인간 대포 3단계 — 분리 코스와 쉼터
- **Type:** feat
- **Assignee:** @RagSpirin
- **Branch:** feat/human-cannon-route-shelter
- **Base:** integration/main-stage-human-cannon

## Context
- 지상 이동장치/비행 대포 3대로 서로 분리 출발→첫 받기→생존 비행자 전원 합류 후 쉼터 자동 승강·교대·재출발을 검증한다.
- 마지막 출발 여부와 무관한 예고 후 불꽃 압박·구간 경계 종료를 검증하고 쿠션/최종 구간에 별도 타이머를 두지 않는다.
042는 submitted/submit-work·단독 Active다. 041 #108/292667c 통합과 최신 부모 수신을 확인했다. 2026-10-16 사용자 전체 직접 확인 완료 후 PR #111로 제출했다. 실제4인 공동 테스트는 전체 통합 후 별도로 기록한다.

## 현재 참고
- integrated — 2026-10-16 PR #111/75862ef 병합·사용자 전달 PM 확인, 043 HEAD0128623 수신 확인. 구현 저장87f383f. [최종 검증](History/20261016-submit-verification.md).
- PR: https://github.com/gaboja-studio/POC_CURTAINCALL/pull/111
- 대상: feat/human-cannon-route-shelter → integration/main-stage-human-cannon(Squash), 통합 완료. 기존 PM 위임에 따라 042 코드·씬 소유를 043으로 반환했다.
- 구현: 대포3대 분리 출발·첫 받기·쉼터 합류/교대·불꽃 압박/경계 종료. 리프트3대는 트램폴린을 든1명만1.5초 자동 출발, 조건 충족 대기/이동 초록·그 외 파랑. 과승 양문·안전 바닥/복귀 계단·쉼터 낙차 반영.
- 피드백: 톱 중심 지면+1.2m/장벽2.4m·잡기 중 지하 접힘, 네 봉 가속300/각도80/속도180. 손잡이 잡기 초록/놓기 원색. 운반자 통과 전 비행 차단 유지. 시험 수치는 최종 밸런스 확정이 아니다.

## 남은 확인
1. #111 통합 완료. 기존 위임 문서8개에 대한 자동 scope FAIL과 위임 근거는 PR 이력에 보존한다.
2. 043 단독 작업 → 완료 후 044 순차 이관. 042는 추가 구현하지 않는다.
3. 전체 통합 후 plan의 실제4인 공동 테스트와 테스트 씬 유지/삭제 결정.

## 기록
- 현재 할 일은 [todo.md](todo.md), 특별한 문제·결과·다음 행동은 [log.md](log.md).
- [이전 계획](History/Harness-v1/plan.md) · [현재 상태·미검증 원문](History/Harness-v1/handoff.md) · [목표·결정 원문](History/Harness-v1/meta.md).
- [기존 경로·참조 정보](History/Harness-v1/setup.md)는 참고용이다. 작업 구역·인계·Status·순차 공동 테스트 제한은 적용하지 않는다.
