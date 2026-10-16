# 042 승강기 출발 가능 색상 — 2026-10-16

- 사용자 요청: 승강기가 움직일 조건이 됐을 때만 초록, 아니면 기존 파란색 유지.
- LateUpdate 표시를 단순1인 점유에서 Boarding.IsCounting 또는 Moving 상태로 변경. 조건 충족1.5초 대기/이동은 초록, 빈손/쉼터 미합류/취소/도착은 파랑. 과승·재시작 대기도 파랑 우선.
- 기존 호스트 상태/snapshot으로 표시하며 프로토콜·씬·공유 머티리얼 변경 없음. HUD에도 색상 의미 표시. 독립 읽기 검토에서 누락 없음.
- check-work -Mode PlayMode: 기존 scope FAIL13(위임 문서8+기존 변경5), 후속 SKIPPED. 수동 구역 감사 및 기존 주요3파일 hash 동일.
- 별도 컴파일 PASS(completed), EditMode269/269 PASS(failed/skipped0), PlayMode NO_PROJECT_TESTS. 문서 검사는 handoff 참조.
- 단일 호스트에서3대 모두 빈손2초 대기/파랑→잡기/초록→놓기 취소/파랑→재소지 새1.5초/초록→Moving/초록→Arrived/파랑 확인. 각 발판 바닥 Renderer2개의 실제 PropertyBlock 색상을 검사했다.
- 합성 위치 배치·RequestGrab/RequestDrop 직접 호출이며 실제 입력·원격 참가자 검증은 아니다. 쉼터 미합류/과승 색상은 상태 조건 코드 검토이며 이번 실행으로 별도 재현하지 않았다.
- 로그 .harness/local/task042-lift-color-runtime-console.json, seq109279~109327.3대 COMPLETE, Console 오류0/경고0. 콜백 해제·재시작 후Stop.
- 원격 도착 색상은 Arrived 상태 수신 시 바뀐다. 실제 다인 동기화·사용감은 plan의 사람 확인에 남긴다. commit/push/PR 미실행.
