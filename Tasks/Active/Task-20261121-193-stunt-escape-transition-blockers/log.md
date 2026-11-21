# Log

- 2026-11-21. DEV 8b0fa5db. 직전 13건은 [PR #475](https://github.com/gaboja-studio/POC_CURTAINCALL/pull/475)로 제출. 기존 에셋 저장소와 중복 Task ID 경고를 확인했다. 외부 에셋은 작업에서 제외한다.
- 탈출로 입구. 최신 소유자 위치로 진입을 확인한다. 리스폰 확인은 동일 아바타와 유예 시간에만 적용하고 새 캠페인에는 넘기지 않는다.
- 탈출로 협동 장치. 최종 주자 레버가 나타난 뒤에도 위쪽 버튼을 정상적으로 고르고 누를 수 있다. 호스트는 최신 소유자 위치와 기존 벽 차단을 사용한다. 하부 작업자 선출은 실제 회전된 작업 구역과 기물 탑승 상태를 확인한다.
- 묘기. 늦은 이전 착지가 다음 봉 비행 기록을 지우지 않는다. 이전 봉 해제는 현재 비행을 덮어쓰지 않으며, 같은 비행의 최신 상태가 먼저 오면 해당 봉의 해제를 복구한다. 재활성화할 연습 그네의 등록 수를 보호한다.
- 실제 멀티플레이는 사용자가 확인한다.
- 신규 TransitionAudit EditMode 12건 통과, 실패 0건. 결과 .harness/local/stunt-escape-transition-verified.xml. 이미 통과한 이전 검사 묶음은 재실행하지 않았다.
- check-work의 변경 범위 및 문서·규칙 통과. 컴파일·테스트는 별도 EditMode를 사용해 check-work에서 생략했다. git diff --check 통과.

- 2026-11-21 PR: https://github.com/gaboja-studio/POC_CURTAINCALL/pull/476 (fix/stunt-escape-transition-blockers → dev)
- DEV 검토용 [PR #476](https://github.com/gaboja-studio/POC_CURTAINCALL/pull/476)로 제출했다.
