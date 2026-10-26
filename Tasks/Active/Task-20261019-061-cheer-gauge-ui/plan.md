# Task-20261019-061

- **Title:** UI 씬 통합 · 공연 UI 및 대기실 기본 HUD 연결
- **Type:** fix
- **Assignee:** @MoHoDu
- **Branch:** fix/tightrope-gauge-visibility
- **Base:** dev

## Context
- 2026-10-21 사용자: W2 저장 후 W3 편집·확정·준비와4일차 편성 요구 제거. 실제 진행5개 씬만, 검증은 최종 필수 최소로 제한. W2는22ceebf push 완료, W3는 후속 미커밋.
- 2026-10-21 사용자: W2 상점·서비스·장부를 Assets 완성 프리팹으로 실제 데이터 연결하고 대체 임시 창 정리. waiting-room-shop.md에 구현·검증 기록.
- 2026-10-21 사용자: 상점/스케줄 등 입력을 점유하는 창을 열면 커서 자동 표시, 종료하면 마우스 시선 조작과 숨김/잠금 복귀. 기존 공통 입력 차단 요청에 연결.
- 2026-10-21 사용자: 수화기 위치/표현을 먼저 복구하고 Assets 완성 WorldLabels로 W4·W5 실제 데이터 연결 및 대체 임시 이름표 정리.
- Assets 정본 C1/PerformanceHud 프리팹을 씬 인스턴스로 적용하고 기존 게임 공개 데이터에 연결한다.
- 사용자 지정 적용 대상: Start/Lobby/WaitingRoomBase/Tightrope/HumanCannon 5개 씬. 첫 단계는 두 공연 씬 HUD이며 백스테이지·탈출로 신규 씬은 만들지 않는다.
- 제작 원본/폰트/PNG/importer/9-slice는 보존. 인계 Packages ZIP 사용 금지.


## 현재 참고


## 남은 확인
- 로비 실제 F/눈/복사/ESC·공연 전환 시 HUD 숨김 확인 후 전체 UI 누락 점검.
- 다인 음성/원격 차폐ACK·재접속/지연, 상점 다인·의료 복구·3일밤→4일아침 실제 동선 확인 남음.
- [남은 계획](../../../Docs/Guides/Ui-scene-integration/next-plan.md)과 단계별 가이드의 미실행 항목 유지.
- 후속 hover/외줄 조작 안내 교체 완료. 배치용 컴파일 PASS, 실행 검증은 사용자 요청으로 미실행. Task Integration=dev로 PR 준비, 생성/병합 안 함. 상세 ui-audit.md.

## 기록
- 현재 할 일은 [todo.md](todo.md), 특별한 문제·결과·다음 행동은 [log.md](log.md).
- [이전 계획](History/Harness-v1/plan.md) · [현재 상태·미검증 원문](History/Harness-v1/handoff.md) · [목표·결정 원문](History/Harness-v1/meta.md).
- [기존 경로·참조 정보](History/Harness-v1/setup.md)는 참고용이다. 작업 구역·인계·Status·순차 공동 테스트 제한은 적용하지 않는다.
