# Task-20261022-070

- **Title:** 커튼 전환 동기화 및 탐색 경고 상단 표시 긴급 수정
- **Type:** fix
- **Assignee:** @MoHoDu
- **Branch:** fix/curtain-scene-transition
- **Base:** dev

## Context
- 연결된 참가자 모두의 커튼 닫힘 확인 후 씬 전환, 각 PC의 씬·배경·UI 준비 후 열림.
- 탐색 경고를 기존 상단 배너 위치·최상위 정렬로 표시(알림 시점 유지).
참가자는 NGO Load 통지를 받은 뒤 커튼을 닫으므로 씬 변경이 닫힘보다 먼저 보일 수 있다. 전환 UI가 아직 생성되지 않았으면 수신 구독 자체도 없다.

## 현재 참고
- Branch: fix/curtain-scene-transition → dev. Worktree: D:/github/POC_CURTAINCALL-curtain-fix.
- 메인 프로젝트·다른 에이전트 변경 보존. 전용 Unity 6000.3.25f1, 7801 사용. 최신 dev e990b31(#215) 병합(f3150e7), 충돌 없음.
- 승인된 코드·테스트 다섯 파일: 커튼 닫힘 ACK·로컬 준비 장벽·늦은 종료 방어, 탐색 경고 상단 배너·최상위 정렬.
- PM이 Task-061·062 중복 경로 및 추가 UI 긴급 예외 승인. 코드 커밋 ea8c263 push 완료, dev PR 등록.
- PR: https://github.com/gaboja-studio/POC_CURTAINCALL/pull/214

## 남은 확인
1. PR #214 리뷰 후 dev 병합(미실행). 자동 에셋 변경은 계속 제출 제외.
2. 신규 참가자 최초 NGO 동기화는 별도 경로라는 한계를 PR에 유지.
3. 실제 2인 시나리오 수행. Task-061·062 담당자는 병합 후 중복 파일 재확인.

## 기록
- 현재 할 일은 [todo.md](todo.md), 특별한 문제·결과·다음 행동은 [log.md](log.md).
- [이전 계획](History/Harness-v1/plan.md) · [현재 상태·미검증 원문](History/Harness-v1/handoff.md) · [목표·결정 원문](History/Harness-v1/meta.md).
- [기존 경로·참조 정보](History/Harness-v1/setup.md)는 참고용이다. 작업 구역·인계·Status·순차 공동 테스트 제한은 적용하지 않는다.
