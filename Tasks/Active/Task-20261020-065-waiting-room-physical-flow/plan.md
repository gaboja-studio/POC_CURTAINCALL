# Task-20261020-065

- **Title:** 실제 대기실 복귀와 물리 출구 진행 연결
- **Type:** fix
- **Assignee:** @gyumin9048-ctrl
- **Branch:** fix/waiting-room-bugs-improvements
- **Base:** dev

## Context
- 공연 종료 후 실제 대기실로 복귀하고, 개인의 물리 퇴장으로 팀 탐색 타이머가 시작되어야 한다.
- 더미 탐색 씬으로 전원을 이동하지 않고 실제 무대 입구 집결로 다음 공연에 진입한다.
공연 후 실제 대기실 대신 예전 UI 선택용 그레이박스가 나타난다. 첫 개인 퇴장으로 팀 탐색 타이머가 시작되어야 하지만 SpaceLinkDirector는 대기실 전체를 DummyExploration으로 교체한다.

## 현재 참고
- PR: https://github.com/gaboja-studio/POC_CURTAINCALL/pull/203 (dev 대상, 대기실 후속 수정 제출; 이전 #191과 구분)
- 제출 검증: 최신 dev 충돌 없이 병합·push. 전체 검사는900초 시간 초과 후 취소했고, 사용자 지시로 관련 PlayMode8/8 PASS만 검사해 제출했다. 전체 통과를 뜻하지 않는다. 구역·문서·컴파일 PASS, 원격 사람 공동 검증 미실행.
- PM 긴급 승인으로 @gyumin9048-ctrl 임시 담당, `fix/waiting-room-bugs-improvements` → dev. [exception.md](exception.md), 상세 변경·검증 이력 [waiting-room-followup.md](waiting-room-followup.md).
- 최신 추가 천: 사진은 CanvasOnCrate_4. 같은 원인의 CanvasOnCrate_0/1·FoldedCanvas까지4곳을 실제 벽에 연결된 줄에 걸었다. 전체 메시 상단 윤곽 지지·실내/보관실 재생성 연동, 장식 충돌체0. 씬 저장 완료.
- 숙소 천4개는 침대 위 이동을 폐기하고 빨랫줄에 걸었다. Rope/Rod 모두 실제 천장 면을 먼저 확인하고 양끝 연결선2개와 고정부2개를 생성한다. 가정한 WallAnchor 좌표 고정 방식은 폐기했다.

## 남은 확인
- PM은 후속 PR에서 Task-060 아트 도구·Task-062 PlayerMover/Humanoid/Camera 변경의 긴급 예외를 검토한다.
- 원격2프로세스 공동 검증 미실행(MPPM 구성0). 사람 공동 확인: 부활·관전 해제 / 퇴장·팀 타이머·잔류·집결 / 밤 잠금·F 스케줄·전원 준비. 실제 상점 구매·수령 및 모든 카메라 가림 구간도 미검증.
- 기존 Additive 씬 개수 실패 및 키보드 회귀 불안정은 남아 있다. PR 병합·커밋·push는 사용자 요청 시 진행.
- 교훈 제안: 메시 복제 시 모든 생성 경로에서 실제 지지물도 함께 검사한다. 숙소만 확인하면 상자 장식의 동일 원인을 놓친다. 상세 후속 기록 참고.

## 기록
- 현재 할 일은 [todo.md](todo.md), 특별한 문제·결과·다음 행동은 [log.md](log.md).
- [이전 계획](History/Harness-v1/plan.md) · [현재 상태·미검증 원문](History/Harness-v1/handoff.md) · [목표·결정 원문](History/Harness-v1/meta.md).
- [기존 경로·참조 정보](History/Harness-v1/setup.md)는 참고용이다. 작업 구역·인계·Status·순차 공동 테스트 제한은 적용하지 않는다.
