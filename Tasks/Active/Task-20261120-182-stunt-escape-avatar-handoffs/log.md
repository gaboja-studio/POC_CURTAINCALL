# Log

- 2026-11-20 DEV e711f713 기준. 이동 상태가 부활 뒤 최초로 도착하면 기존 순서 기반 해제가 적용되지 않았다. 출발 시점의 부활 번호를 호스트 발판 상태와 함께 전달하며 해당 번호의 아바타에만 탑승과 도착을 적용한다. 발판 메시지의 버전과 크기도 맞췄다.
- 탈출로 입장 완료 뒤 이전 배치 기록이 새 아바타의 배치를 건너뛰던 경우를 재현했다. 같은 참가자의 새 아바타에는 저장한 출발 자세로 배치하고, 이미 배치한 아바타는 다시 옮기지 않는다.
- 신규 EditMode 두 사례가 수정 전에 실패했다. 실제 플레이 및 네트워크 세션 검증은 사용자 범위다.
- 2026-11-20 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS(배치 EditMode); 테스트=PASS(신규 순서 2건과 부활 번호에 맞춘 호스트·참가자 검사 2건). 실제 다인 플레이는 미실행.

- 2026-11-20 PR: https://github.com/gaboja-studio/POC_CURTAINCALL/pull/463 (fix/stunt-escape-avatar-handoffs → dev)
- 2026-11-20 DEV 검토용 [PR #463](https://github.com/gaboja-studio/POC_CURTAINCALL/pull/463)에 제출했다.
